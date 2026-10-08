#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using SHA256 = System.Security.Cryptography.SHA256;
using System.Runtime.InteropServices;
using Godot;
using Yudian.Terrain;

namespace Yudian;

/// <summary>
/// 灰模基线场景：6 设施 + 12 机器人 + 带土坡/矿点凹陷的地面，全部参数从具名 fixture 读取。
/// 命令行用户参数（-- 之后）：--benchmark、--duration N、--fixture path、--frames-csv path、--summary-json path。
/// </summary>
public partial class Main : Node3D
{
    // ---- runtime state ----

    private sealed class Patrol
    {
        public required Node3D Node;
        public required Vector3[] Points;
        public required float[] Cum;
        public required float Speed;
        public float Dist;
    }

    private sealed class Spinner
    {
        public required Node3D Node;
        public required float SpeedRad;
        public float Phase;
    }

    private sealed class Floater
    {
        public required Node3D Node;
        public required float BaseY;
        public required float Amp;
        public required float SpeedRad;
        public float Phase;
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private FixtureConfig _cfg = null!;
    private readonly List<FeatureConfig> _features = new();
    private readonly List<Vector3> _facilityPositions = new();
    private readonly List<Patrol> _robots = new();
    private readonly List<Spinner> _spinners = new();
    private readonly List<Floater> _floaters = new();
    private Camera3D _camera = null!;
    private Vector3 _cameraCenter;

    private bool _benchmark;
    private double _duration;
    private string _framesPath = "";
    private string _summaryPath = "";
    private BenchmarkRecorder? _recorder;
    private ulong _lastUsec;
    private string _fixtureHash = "";
    private string _runId = "";
    private double _animTime;
    private bool _captured;
    private string? _guiCaptureKey;
    private string? _guiCapturePending;

    public override void _Ready()
    {
        try
        {
            var options = BenchmarkOptions.Parse(OS.GetCmdlineUserArgs(), OS.GetCmdlineArgs(),
                System.Environment.GetEnvironmentVariable("YUDIAN_BENCHMARK") == "1");
            _benchmark = options.Benchmark;
            _cfg = LoadFixture(options.FixturePath);
            _features.Add(_cfg.Terrain.Mound);
            _features.Add(_cfg.Terrain.MineralPit);
            _playerMode = !options.Benchmark && !options.LiveTerrain;
            _entryOpen = _playerMode && new[] { "YUDIAN_PLAYER_SELF_TEST", "YUDIAN_BOOTSTRAP_SELF_TEST", "YUDIAN_DEVELOPMENT_SELF_TEST" }
                .All(name => System.Environment.GetEnvironmentVariable(name) != "1");
            _liveMode = options.LiveTerrain || _playerMode;
            if (_liveMode) PrepareLiveTerrain();
            _duration = options.Duration ?? _cfg.Benchmark.DurationSeconds;
            ValidateProbeGeometry();
            _runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", Inv) + "-" + Guid.NewGuid().ToString("N")[..8];
            if (_benchmark)
            {
                _framesPath = OutputPath(options.FramesCsv, _cfg.Benchmark.FramesCsv);
                _summaryPath = OutputPath(options.SummaryJson, _cfg.Benchmark.SummaryJson);
                _recorder = new BenchmarkRecorder(_framesPath, _summaryPath);
                GetTree().AutoAcceptQuit = false;
            }
            ApplyWindowAndQuality();
            BuildLightAndEnvironment();
            if (_liveMode) BuildLiveTerrain();
            else { BuildTerrain(); BuildFacilities(); SpawnRobots(); }
            BuildCamera();
            if (_playerMode)
            {
                var controller = new Yudian.PlayerUI.PlayerController { Name = "PlayerController" }; AddChild(controller); controller.Initialize(this, _camera);
            }
            if (_benchmark) StartBenchmark();
            if (_liveMode) GD.Print($"MAIN_GROUND_IDENTITY build_sha256={AssemblyHash()} loaded_mvid={typeof(Main).Assembly.ManifestModule.ModuleVersionId} CLR={System.Environment.Version} display={DisplayServer.GetName()}");
            GD.Print($"[Yudian] fixture={_cfg.Name} seed={_cfg.Seed} facilities={_facilityPositions.Count} robots={_robots.Count} benchmark={_benchmark}");
        }
        catch (Exception e) { Fail(e); }
    }

    public override void _Process(double delta)
    {
        var capture = System.Environment.GetEnvironmentVariable("YUDIAN_CAPTURE_PNG");
        if (!_captured && capture != null && Engine.GetProcessFrames() > 120)
        {
            _captured = true;
            var error = GetViewport().GetTexture().GetImage().SavePng(capture);
            if (error != Error.Ok) { Fail(new IOException("截图保存失败: " + error)); return; }
        }
        var captureDirectory = System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_CAPTURE_DIR");
        if (_playerMode && _groundReady && captureDirectory != null && System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1")
        {
            string id = BootstrapEnabled && _buildJob != null ? _buildJob.Id : _levelJob?.Id ?? "none";
            string stage = BootstrapEnabled && _buildJob != null ? _buildJob.Stage : _levelJob?.Stage.ToString() ?? "Idle";
            string key = $"{_liveTerrain!.Current.Version}-{id}-{stage}-{_userPaused}-{ReadPlayerState().Ready}-{_entryOpen}-{_playerNotice.GetHashCode():x8}";
            if (key != _guiCaptureKey)
            {
                if (_guiCapturePending == key)
                {
                    Directory.CreateDirectory(captureDirectory);
                    var result = GetViewport().GetTexture().GetImage().SavePng(Path.Combine(captureDirectory, $"{Engine.GetProcessFrames()}-{key}.png"));
                    if (result != Error.Ok) { Fail(new IOException("图形测试截图保存失败: " + result)); return; }
                    _guiCaptureKey = key;
                }
                _guiCapturePending = key;
            }
        }
        float dt = _playerMode && (_entryOpen || _userPaused || _loadPending != null) ? 0 : (float)delta;
        _animTime += dt;

        foreach (var s in _spinners)
            s.Node.RotateY(s.SpeedRad * dt);
        foreach (var f in _floaters)
            f.Node.Position = new Vector3(f.Node.Position.X, f.BaseY + f.Amp * MathF.Sin((float)_animTime * f.SpeedRad + f.Phase), f.Node.Position.Z);

        foreach (var r in _robots)
        {
            r.Dist = (r.Dist + r.Speed * dt) % r.Cum[^1];
            int i = 0;
            while (i < r.Points.Length - 1 && r.Cum[i + 1] < r.Dist)
                i++;
            float segLen = r.Cum[i + 1] - r.Cum[i];
            float t = segLen > 0f ? (r.Dist - r.Cum[i]) / segLen : 0f;
            Vector3 p = r.Points[i].Lerp(r.Points[(i + 1) % r.Points.Length], t);
            p.Y = TerrainHeight(p.X, p.Z) + 0.45f;
            r.Node.Position = p;
        }

        if (_benchmark && _recorder != null)
        {
            float ang = MathF.Tau * (float)_animTime / _cfg.CameraPath.PeriodSeconds;
            _camera.Position = _cameraCenter + new Vector3(MathF.Cos(ang) * _cfg.CameraPath.Radius, _cfg.CameraPath.Height, MathF.Sin(ang) * _cfg.CameraPath.Radius);
            _camera.LookAt(_cameraCenter);
            try { RecordFrame(); }
            catch (Exception e) { Fail(e); }
        }
    }

    // ---- setup ----

    // 必须经 Godot.FileAccess 读：导出包里 fixture 位于 PCK 内，System.IO 只能看到散文件。
    private FixtureConfig LoadFixture(string path)
    {
        var resPath = Path.IsPathRooted(path) || path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal) ? path : "res://" + path;
        using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
        if (f == null)
            throw new InvalidOperationException($"无法打开 fixture {path}: {Godot.FileAccess.GetOpenError()}");
        var text = f.GetAsText();
        _fixtureHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return FixtureConfig.ParseValidated(text);
    }

    private void ValidateProbeGeometry()
    {
        foreach (var pair in new[] { (_cfg.Terrain.Mound, "terrain.mound.radius"), (_cfg.Terrain.MineralPit, "terrain.mineral_pit.radius") })
            if (pair.Item1.Radius * pair.Item1.Radius == 0)
                throw new ArgumentException(pair.Item2 + " 的平方下溢，不能生成有限地形");
        if (_cfg.Scale.RobotsTotal > 0)
        {
            float chord = 2 * _cfg.Scale.RingRadius * MathF.Sin(MathF.PI / _cfg.Scale.Facilities);
            if (chord * chord == 0)
                throw new ArgumentException("scale.ring_radius 导致巡逻路段长度下溢");
        }
        if (!float.IsFinite(MathF.Tau * (float)(_duration + 60) / _cfg.CameraPath.PeriodSeconds))
            throw new ArgumentException("camera_path.period_seconds 导致相机角度溢出");
    }

    private string OutputPath(string? explicitPath, string fixturePath)
    {
        if (explicitPath == null)
            return ProjectSettings.GlobalizePath($"user://benchmarks/{_runId}/{Path.GetFileName(fixturePath)}");
        if (explicitPath.StartsWith("res://", StringComparison.Ordinal))
            throw new ArgumentException("输出不得使用 res://");
        return Path.GetFullPath(explicitPath.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(explicitPath) : explicitPath);
    }

    private void ApplyWindowAndQuality()
    {
        var size = new Vector2I(_cfg.TargetResolution.Width, _cfg.TargetResolution.Height);
        if (_playerMode && DisplayServer.GetName() != "headless")
        {
            var available = DisplayServer.ScreenGetUsableRect().Size - new Vector2I(60, 100);
            size = new Vector2I(Math.Min(size.X, available.X), Math.Min(size.Y, available.Y));
        }
        if (!OS.GetCmdlineArgs().Contains("--resolution")) GetWindow().Size = size;
        var vp = GetViewport();
        vp.Msaa3D = _cfg.Quality.Msaa3d switch
        {
            2 => Viewport.Msaa.Msaa2X,
            4 => Viewport.Msaa.Msaa4X,
            8 => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled,
        };
        vp.ScreenSpaceAA = _cfg.Quality.Fxaa ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        vp.Scaling3DScale = _cfg.Quality.Scaling3dScale;
    }

    private void BuildLightAndEnvironment()
    {
        var sun = new DirectionalLight3D { ShadowEnabled = _cfg.Quality.Shadows };
        sun.RotationDegrees = new Vector3(-55, -35, 0);
        AddChild(sun);

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.12f, 0.13f, 0.15f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.57f, 0.6f),
            AmbientLightEnergy = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = env });
    }

    // 旧灰模采样高斯高度；live模式只查询已同步的原生地形。
    private float TerrainHeight(float x, float z)
    {
        if (_liveTerrain != null) return GroundHeight(x, z);
        float h = 0f;
        foreach (var f in _features)
        {
            float dx = x - f.Position[0], dz = z - f.Position[1];
            h += f.Amount * MathF.Exp(-(dx * dx + dz * dz) / (f.Radius * f.Radius));
        }
        return h;
    }

    private void BuildTerrain()
    {
        var pm = new PlaneMesh { Size = new Vector2(_cfg.Terrain.Size, _cfg.Terrain.Size), SubdivideWidth = _cfg.Terrain.Segments, SubdivideDepth = _cfg.Terrain.Segments };
        var arrays = pm.GetMeshArrays();
        var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (int i = 0; i < verts.Length; i++)
            verts[i].Y += TerrainHeight(verts[i].X, verts[i].Z);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;

        var am = new ArrayMesh();
        am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var st = new SurfaceTool();
        st.CreateFrom(am, 0);
        st.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = Mat(new Color(0.52f, 0.52f, 0.55f)) });

        // 矿点标识：凹陷中心的深色圆片占位。
        var pit = _cfg.Terrain.MineralPit;
        var ore = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.6f, Height = 0.12f },
            MaterialOverride = Mat(new Color(0.22f, 0.17f, 0.12f)),
        };
        ore.Position = new Vector3(pit.Position[0], TerrainHeight(pit.Position[0], pit.Position[1]) + 0.06f, pit.Position[1]);
        AddChild(ore);
    }

    // 六类设施沿内环均布，几何与颜色各不相同；缓速自转或悬浮占位动画。
    private void BuildFacilities()
    {
        if (BootstrapEnabled) { InitializeBootstrap(); return; }
        int n = _cfg.Scale.Facilities;
        float ring = _cfg.Scale.RingRadius;
        for (int i = 0; i < n; i++)
        {
            float ang = MathF.Tau * i / n;
            float x = MathF.Cos(ang) * ring, z = MathF.Sin(ang) * ring;
            if (_playerMode && i % 6 == 5) { x = 7; z = -22; }
            float y = TerrainHeight(x, z);
            _facilityPositions.Add(new Vector3(x, y, z));
            var root = new Node3D { Position = new Vector3(x, y, z), Name = $"Facility_{i}" };
            AddChild(root);
            if (_playerMode)
            {
                string[] paths = ["facilities/solar-r1/solar-r1.glb", "facilities/processor-r1/processor-r1.glb",
                    "lowfi-batch-r1/models/storage.glb", "lowfi-batch-r1/models/charger.glb", "lowfi-batch-r1/models/repair.glb", "lowfi-batch-r1/models/lander.glb"];
                root.AddChild(GD.Load<PackedScene>("res://assets/" + paths[i % 6]).Instantiate<Node3D>());
                continue;
            }
            switch (i % 6)
            {
                case 0: // 太阳能阵列：板 + 柱，慢速自转
                    MeshPart(root, new BoxMesh { Size = new Vector3(4.2f, 0.12f, 2.6f) }, new Color(0.16f, 0.28f, 0.58f), new Vector3(0, 1.55f, 0), new Vector3(-28, 0, 0));
                    MeshPart(root, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.5f }, new Color(0.35f, 0.35f, 0.38f), new Vector3(0, 0.75f, 0));
                    _spinners.Add(new Spinner { Node = root, SpeedRad = 0.18f, Phase = ang });
                    break;
                case 1: // 加工设施：罐 + 厂房，慢速自转
                    MeshPart(root, new CylinderMesh { TopRadius = 1.4f, BottomRadius = 1.6f, Height = 2.2f }, new Color(0.85f, 0.46f, 0.15f), new Vector3(0, 1.1f, 0));
                    MeshPart(root, new BoxMesh { Size = new Vector3(2.8f, 1.2f, 2.8f) }, new Color(0.7f, 0.4f, 0.18f), new Vector3(0, 0.6f, 0));
                    _spinners.Add(new Spinner { Node = root, SpeedRad = 0.12f, Phase = ang });
                    break;
                case 2: // 仓储：大箱体，慢速自转
                    MeshPart(root, new BoxMesh { Size = new Vector3(4.5f, 2.2f, 3.2f) }, new Color(0.45f, 0.42f, 0.4f), new Vector3(0, 1.1f, 0));
                    _spinners.Add(new Spinner { Node = root, SpeedRad = 0.08f, Phase = ang });
                    break;
                case 3: // 充电桩：三柱一排，缓慢悬浮
                    for (int k = 0; k < 3; k++)
                    {
                        var post = MeshPart(root, new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.3f, Height = 1.4f }, new Color(0.9f, 0.78f, 0.2f), new Vector3((k - 1) * 1.3f, 0.7f, 0));
                        _floaters.Add(new Floater { Node = post, BaseY = 0.7f, Amp = 0.12f, SpeedRad = 1.4f, Phase = k * 1.1f + ang });
                    }
                    break;
                case 4: // 维修站：基座 + 环形雷达，慢速自转
                    MeshPart(root, new BoxMesh { Size = new Vector3(2.6f, 1.0f, 2.6f) }, new Color(0.78f, 0.22f, 0.2f), new Vector3(0, 0.5f, 0));
                    MeshPart(root, new TorusMesh { InnerRadius = 0.7f, OuterRadius = 0.9f }, new Color(0.85f, 0.85f, 0.85f), new Vector3(0, 1.6f, 0), new Vector3(90, 0, 0));
                    _spinners.Add(new Spinner { Node = root, SpeedRad = 0.5f, Phase = ang });
                    break;
                case 5: // 着陆器：胶囊舱体，悬浮占位
                    MeshPart(root, new CylinderMesh { TopRadius = 0.4f, BottomRadius = 1.1f, Height = 0.5f }, new Color(0.5f, 0.5f, 0.55f), new Vector3(0, 0.25f, 0));
                    var body = MeshPart(root, new CapsuleMesh { Radius = 1.0f, Height = 2.8f }, new Color(0.8f, 0.82f, 0.86f), new Vector3(0, 1.8f, 0));
                    _floaters.Add(new Floater { Node = body, BaseY = 1.8f, Amp = 0.25f, SpeedRad = 1.1f, Phase = ang });
                    break;
            }
        }
    }

    private static MeshInstance3D MeshPart(Node3D parent, Mesh mesh, Color color, Vector3 pos, Vector3? rotDeg = null)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = Mat(color), Position = pos };
        if (rotDeg is { } r)
            mi.RotationDegrees = r;
        parent.AddChild(mi);
        return mi;
    }

    // 12 台机器人：望山（胶囊+球顶）/ 筑垒（方箱）/ 驮运（楔块），各 4 台，
    // 固定闭环航点；旧模式插值，live模式原生贴地移动，均不提供寻路。
    private void SpawnRobots()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)_cfg.Seed };

        (string Name, Color Color, Func<Mesh> Mesh, float Speed)[] types =
        [
            ("Wangshan", new Color(0.1f, 0.66f, 0.6f), () => new CapsuleMesh { Radius = 0.32f, Height = 1.1f }, 3.0f),
            ("Zhulei", new Color(0.55f, 0.3f, 0.8f), () => new BoxMesh { Size = new Vector3(0.9f, 0.7f, 1.25f) }, 1.6f),
            ("Tuoyun", new Color(0.92f, 0.62f, 0.15f), () => new PrismMesh { Size = new Vector3(1.15f, 0.85f, 1.0f) }, 2.2f),
        ];

        int perType = _cfg.Scale.RobotsPerType;
        for (int t = 0; t < types.Length; t++)
        {
            for (int k = 0; k < perType; k++)
            {
                Node3D root = _liveMode ? new GroundPatrol() : new Node3D();
                root.Name = $"Robot_{types[t].Name}_{k + 1}";
                AddChild(root);
                if (!(_playerMode && (t == 1 || (BootstrapEnabled && t == 2)))) MeshPart(root, types[t].Mesh(), types[t].Color, _liveMode ? new Vector3(0, .55f, 0) : Vector3.Zero);
                if (t == 0)
                    MeshPart(root, new SphereMesh { Radius = 0.12f, Height = 0.24f }, new Color(0.9f, 0.9f, 0.9f), new Vector3(0, _liveMode ? 1.27f : .72f, 0));

                // 固定航点：从 6 个设施里取 4 个组成闭环（种子决定顺序，所有运行一致）。
                var order = new List<int>();
                for (int i = 0; i < _facilityPositions.Count; i++)
                    order.Add(i);
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = rng.RandiRange(0, i);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                var pts = new Vector3[4];
                for (int i = 0; i < 4; i++)
                    pts[i] = BootstrapEnabled ? new Vector3(10 * MathF.Cos(i*MathF.PI/2),0,10 * MathF.Sin(i*MathF.PI/2)) : _facilityPositions[order[i]];
                var cum = new float[5];
                for (int i = 0; i < 4; i++)
                    cum[i + 1] = cum[i] + pts[i].DistanceTo(pts[(i + 1) % 4]);

                float dist = rng.Randf() * cum[4];
                if (root is GroundPatrol ground)
                {
                    int i = 0; while (i < 3 && cum[i + 1] < dist) i++;
                    Vector3 p = pts[i].Lerp(pts[(i + 1) % 4], (dist - cum[i]) / (cum[i + 1] - cum[i]));
                    if (_playerMode)
                    {
                        p = t == 1 ? new Vector3(20, 0, -22 + k * 6)
                            : new Vector3(-22 + k * 3, 0, t == 0 ? -22 : -6);
                    }
                    p.Y = GroundHeight(p.X, p.Z) + .02f;
                    ground.Position = p;
                    if (_playerMode) { ground.PatrolEnabled = false; ground.BodyRadius = t == 1 ? .9f : BootstrapEnabled && t == 2 ? 1.4f : .55f; }
                    ground.Initialize(pts, types[t].Speed);
                    _groundRobots.Add(ground);
                    if (_playerMode && t == 1) AttachPlayerVisual(ground);
                    if (BootstrapEnabled && t == 2) AttachHaulerVisual(ground);
                }
                else _robots.Add(new Patrol { Node = root, Points = pts, Cum = cum, Speed = types[t].Speed, Dist = dist });
            }
        }
        if (BootstrapEnabled) InitializeRobotHealth();
    }

    private void BuildCamera()
    {
        _camera = new Camera3D { Fov = 60f };
        _cameraCenter = new Vector3(_cfg.CameraPath.Center[0], _cfg.CameraPath.Center[1], _cfg.CameraPath.Center[2]);
        AddChild(_camera);

        if (_playerMode)
        {
            using var cameraFile = Godot.FileAccess.Open("res://assets/d1-art/camera.json", Godot.FileAccess.ModeFlags.Read);
            using var config = JsonDocument.Parse(cameraFile.GetAsText());
            var normal = config.RootElement.GetProperty("cameras").GetProperty("normal");
            var pos = normal.GetProperty("position"); var aim = normal.GetProperty("target");
            _camera.Position = new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle());
            _camera.Fov = normal.GetProperty("fov").GetSingle();
            _camera.Near = .05f; _camera.Far = 200;
            var target = new Vector3(aim[0].GetSingle(), aim[1].GetSingle(), aim[2].GetSingle());
            // Actual base layout needs 1.6× the art overlay's normal distance.
            _camera.Position = target + (_camera.Position - target) * 1.6f;
            _camera.LookAt(target);
        }
        else if (!_benchmark)
        {
            // 非基准模式：静态斜俯视取景，便于人工查看灰模。
            _camera.Position = _cameraCenter + new Vector3(_cfg.CameraPath.Radius * 0.55f, _cfg.CameraPath.Height, _cfg.CameraPath.Radius * 0.75f);
            _camera.LookAt(_cameraCenter);
        }
        _camera.Current = true;
    }

    private static StandardMaterial3D Mat(Color c) => new() { AlbedoColor = c, Roughness = 1f };

    // ---- benchmark ----

    private void StartBenchmark()
    {
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        _lastUsec = Time.GetTicksUsec();
        GD.Print($"[Yudian] run={_runId} frames={_framesPath} summary={_summaryPath}");
    }

    private void RecordFrame()
    {
        ulong now = Time.GetTicksUsec();
        double frameMs = (now - _lastUsec) / 1000.0;
        _lastUsec = now;
        if (frameMs == 0) return;
        _recorder!.Record(frameMs);
        if (_recorder.Elapsed >= _duration)
        {
            _recorder.Finish(Summary(), "completed");
            _recorder.Dispose();
            _recorder = null;
            SetProcess(false);
            GD.Print($"[Yudian] benchmark completed: {_summaryPath}");
            GetTree().Quit();
        }
    }

    private Dictionary<string, object> Summary()
    {
        var vp = GetViewport();
        var window = DisplayServer.WindowGetSize();
        var size = vp.GetVisibleRect().Size;
        bool headless = DisplayServer.GetName() == "headless";
        object build = "EVIDENCE_MISSING";
        if (Godot.FileAccess.FileExists("res://build-info.json"))
        {
            using var f = Godot.FileAccess.Open("res://build-info.json", Godot.FileAccess.ModeFlags.Read);
            if (f != null) build = JsonSerializer.Deserialize<JsonElement>(f.GetAsText());
        }
        return new Dictionary<string, object>
        {
            ["schema_version"] = 1, ["run_id"] = _runId, ["fixture"] = _cfg.Name,
            ["fixture_sha256"] = _fixtureHash, ["seed"] = _cfg.Seed,
            ["duration_requested_s"] = _duration,
            ["requested"] = new { resolution = _cfg.TargetResolution, quality = _cfg.Quality, scale = _cfg.Scale },
            ["applied"] = new { msaa_3d = vp.Msaa3D.ToString(), fxaa = vp.ScreenSpaceAA.ToString(), scaling_3d_scale = vp.Scaling3DScale, vsync = DisplayServer.WindowGetVsyncMode().ToString(), max_fps = Engine.MaxFps },
            ["observed"] = new { headless, rendering_device = RenderingServer.GetRenderingDevice() != null,
                rendering_method = headless ? "NOT_AVAILABLE" : RenderingServer.GetCurrentRenderingMethod(),
                rendering_driver = headless ? "NOT_AVAILABLE" : RenderingServer.GetCurrentRenderingDriverName(),
                gpu = headless ? "NOT_AVAILABLE" : RenderingServer.GetVideoAdapterName(),
                gpu_api_version = headless ? "NOT_AVAILABLE" : RenderingServer.GetVideoAdapterApiVersion(),
                window_pixels = new[] {window.X, window.Y}, viewport_size = new[] {size.X, size.Y},
                internal_3d_size = new { status = "estimated", formula = "viewport_size * applied.scaling_3d_scale", width = size.X * vp.Scaling3DScale, height = size.Y * vp.Scaling3DScale },
                robots = _robots.Count, facilities = _facilityPositions.Count },
            ["graphical_performance_eligible"] = !headless,
            ["godot_version"] = Engine.GetVersionInfo()["string"].AsString(),
            ["runtime"] = new { framework = RuntimeInformation.FrameworkDescription, version = System.Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(), os = RuntimeInformation.OSDescription },
            ["assembly_sha256"] = AssemblyHash(),
            ["assembly_hash_source"] = string.IsNullOrEmpty(typeof(Main).Assembly.Location) ? "Debug file with loaded MVID check" : "assembly location file",
            ["assembly_mvid"] = typeof(Main).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["build"] = build,
            ["memory"] = "EVIDENCE_MISSING: external process measurement required",
            ["load_scope"] = "gray-r1 rendering, fixed patrol and animation only; no AI/navigation/save/dynamic terrain"
        };
    }

    private static string AssemblyHash()
    {
        var assembly = typeof(Main).Assembly;
        string path = assembly.Location;
        if (string.IsNullOrEmpty(path))
        {
            // Godot开发态从内存加载；核对已有Debug文件，不能把空Location当路径。
            path = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/Yudian.dll");
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != assembly.ManifestModule.ModuleVersionId)
                throw new InvalidOperationException("loaded assembly MVID differs from Debug file");
        }
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private void Fail(Exception e)
    {
        SetProcess(false);
        SetPhysicsProcess(false);
        PauseGround(true);
        GD.PushError($"[Yudian] {e.GetType().Name}: {e.Message}");
        try { _recorder?.Dispose(); }
        catch (Exception cleanup) { GD.PushError($"[Yudian] 关闭记录失败: {cleanup.Message}"); }
        finally { _recorder = null; GetTree().Quit(1); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _recorder != null)
        {
            try { _recorder.Finish(Summary(), "interrupted"); }
            catch (Exception e) { Fail(e); return; }
            _recorder.Dispose();
            _recorder = null;
            GetTree().Quit(130);
        }
    }

    public override void _ExitTree()
    {
        foreach (var texture in _cargoGlyphTextures.Values) texture.Dispose();
        _playerSurface?.Dispose(); _committedMask?.Dispose();
        if (_recorder == null) return;
        try { _recorder.Finish(Summary(), "interrupted"); }
        catch (Exception e) { GD.PushError($"benchmark 中断记录失败: {e.Message}"); GetTree().Quit(1); return; }
        finally { _recorder.Dispose(); _recorder = null; }
        GetTree().Quit(130);
    }
}
