using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Yudian;

/// <summary>
/// 灰模基线场景：6 设施 + 12 机器人 + 带土坡/矿点凹陷的地面，全部参数从具名 fixture 读取。
/// 命令行用户参数（-- 之后）：--benchmark、--duration N、--fixture path、--frames-csv path、--summary-json path。
/// </summary>
public partial class Main : Node3D
{
    private const string DefaultFixture = "fixtures/s_small.json";

    // ---- fixture schema ----

    private sealed class FixtureConfig
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "s_small";
        [JsonPropertyName("seed")] public int Seed { get; set; } = 20261004;
        [JsonPropertyName("scale")] public ScaleConfig Scale { get; set; } = new();
        [JsonPropertyName("terrain")] public TerrainConfig Terrain { get; set; } = new();
        [JsonPropertyName("camera_path")] public CameraPathConfig CameraPath { get; set; } = new();
        [JsonPropertyName("quality")] public QualityConfig Quality { get; set; } = new();
        [JsonPropertyName("target_resolution")] public ResConfig TargetResolution { get; set; } = new();
        [JsonPropertyName("benchmark")] public BenchConfig Benchmark { get; set; } = new();
    }

    private sealed class ScaleConfig
    {
        [JsonPropertyName("robots_total")] public int RobotsTotal { get; set; } = 12;
        [JsonPropertyName("robots_per_type")] public int RobotsPerType { get; set; } = 4;
        [JsonPropertyName("facilities")] public int Facilities { get; set; } = 6;
        [JsonPropertyName("ring_radius")] public float RingRadius { get; set; } = 14f;
    }

    private sealed class TerrainConfig
    {
        [JsonPropertyName("size")] public float Size { get; set; } = 60f;
        [JsonPropertyName("segments")] public int Segments { get; set; } = 64;
        [JsonPropertyName("mound")] public FeatureConfig Mound { get; set; } = new();
        [JsonPropertyName("mineral_pit")] public FeatureConfig MineralPit { get; set; } = new();
    }

    // Amount > 0 = 土坡高度，Amount < 0 = 凹陷深度（高斯截面占位，非真实地形改造）。
    private sealed class FeatureConfig
    {
        [JsonPropertyName("position")] public float[] Position { get; set; } = [12f, -10f];
        [JsonPropertyName("radius")] public float Radius { get; set; } = 6f;
        [JsonPropertyName("amount")] public float Amount { get; set; } = 1.8f;
    }

    private sealed class CameraPathConfig
    {
        [JsonPropertyName("center")] public float[] Center { get; set; } = [0f, 0f, 0f];
        [JsonPropertyName("radius")] public float Radius { get; set; } = 24f;
        [JsonPropertyName("height")] public float Height { get; set; } = 13f;
        [JsonPropertyName("period_seconds")] public float PeriodSeconds { get; set; } = 40f;
    }

    private sealed class QualityConfig
    {
        [JsonPropertyName("msaa_3d")] public int Msaa3d { get; set; } = 4;
        [JsonPropertyName("fxaa")] public bool Fxaa { get; set; } = false;
        [JsonPropertyName("scaling_3d_scale")] public float Scaling3dScale { get; set; } = 1f;
        [JsonPropertyName("shadows")] public bool Shadows { get; set; } = true;
    }

    private sealed class ResConfig
    {
        [JsonPropertyName("width")] public int Width { get; set; } = 1920;
        [JsonPropertyName("height")] public int Height { get; set; } = 1200;
    }

    private sealed class BenchConfig
    {
        [JsonPropertyName("duration_seconds")] public float DurationSeconds { get; set; } = 45f;
        [JsonPropertyName("frames_csv")] public string FramesCsv { get; set; } = "benchmarks/s_small_frames.csv";
        [JsonPropertyName("summary_json")] public string SummaryJson { get; set; } = "benchmarks/s_small_summary.json";
    }

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

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

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
    private Godot.FileAccess _csv;
    private ulong _lastUsec;
    private long _frames;
    private double _elapsed;
    private double _animTime;

    public override void _Ready()
    {
        // 用户参数（"--" 之后）优先；同时兼容未加分隔符直接传参的调用方——
        // Godot 4.7 对未知引擎参数静默忽略，若只查用户参数会导致基准模式永不激活、进程挂住。
        var userArgs = OS.GetCmdlineUserArgs();
        var allArgs = OS.GetCmdlineArgs();
        string Val(string name) => ArgValue(userArgs, name) ?? ArgValue(allArgs, name);

        // 裸传的 --benchmark 会被引擎同名 CLI 选项（--benchmark: Benchmark the run time）吞掉，
        // 只有 "--" 之后的才到得了游戏；--duration/--yudian-benchmark 等非引擎选项裸传会保留。
        // 激活基准：--benchmark（须在 "--" 后）或裸传 --yudian-benchmark，或显式 --duration，
        // 另留 YUDIAN_BENCHMARK=1 环境变量兜底，避免调用方因分隔符约定而挂死在默认模式。
        var durationValue = Val("--duration");
        _benchmark = Array.IndexOf(userArgs, "--benchmark") >= 0
                     || Array.IndexOf(allArgs, "--yudian-benchmark") >= 0
                     || durationValue is not null
                     || System.Environment.GetEnvironmentVariable("YUDIAN_BENCHMARK") == "1";
        double? durationArg = durationValue is { } d ? double.Parse(d, Inv) : null;

        var fixturePath = Val("--fixture") ?? DefaultFixture;
        _cfg = LoadFixture(fixturePath);
        _duration = durationArg ?? _cfg.Benchmark.DurationSeconds;

        ApplyWindowAndQuality();
        BuildLightAndEnvironment();
        BuildTerrain();
        BuildFacilities();
        SpawnRobots();
        BuildCamera();

        _framesPath = ResolvePath(Val("--frames-csv") ?? _cfg.Benchmark.FramesCsv);
        _summaryPath = ResolvePath(Val("--summary-json") ?? _cfg.Benchmark.SummaryJson);

        if (_benchmark)
            StartBenchmark();

        GD.Print($"[Yudian] fixture={_cfg.Name} seed={_cfg.Seed} facilities={_facilityPositions.Count} robots={_robots.Count} benchmark={_benchmark}");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
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

        if (_benchmark)
        {
            // 相机沿固定圆形路径环绕，角速度由 fixture 的 period_seconds 决定。
            float ang = MathF.Tau * (float)_animTime / _cfg.CameraPath.PeriodSeconds;
            _camera.Position = _cameraCenter + new Vector3(MathF.Cos(ang) * _cfg.CameraPath.Radius, _cfg.CameraPath.Height, MathF.Sin(ang) * _cfg.CameraPath.Radius);
            _camera.LookAt(_cameraCenter);

            RecordFrame();
        }
    }

    // ---- setup ----

    // 必须经 Godot.FileAccess 读：导出包里 fixture 位于 PCK 内，System.IO 只能看到散文件。
    private static FixtureConfig LoadFixture(string path)
    {
        var resPath = Path.IsPathRooted(path) ? path : "res://" + path;
        using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
        if (f == null)
            throw new InvalidOperationException($"无法打开 fixture {path}: {Godot.FileAccess.GetOpenError()}");
        var text = f.GetAsText();
        return JsonSerializer.Deserialize<FixtureConfig>(text, JsonOpts) ?? throw new InvalidOperationException($"fixture {path} 解析为空");
    }

    private static string ArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }

    private static string ResolvePath(string p) =>
        Path.IsPathRooted(p) ? p : Path.Combine(ProjectSettings.GlobalizePath("res://"), p);

    private void ApplyWindowAndQuality()
    {
        GetWindow().Size = new Vector2I(_cfg.TargetResolution.Width, _cfg.TargetResolution.Height);
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

    // 高斯截面占位高度；地形网格顶点、机器人和设施 Y 都用同一函数，保证一致。
    private float TerrainHeight(float x, float z)
    {
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
        _features.Add(_cfg.Terrain.Mound);
        _features.Add(_cfg.Terrain.MineralPit);

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
        int n = _cfg.Scale.Facilities;
        float ring = _cfg.Scale.RingRadius;
        for (int i = 0; i < n; i++)
        {
            float ang = MathF.Tau * i / n;
            float x = MathF.Cos(ang) * ring, z = MathF.Sin(ang) * ring;
            float y = TerrainHeight(x, z);
            _facilityPositions.Add(new Vector3(x, y, z));
            var root = new Node3D { Position = new Vector3(x, y, z), Name = $"Facility_{i}" };
            AddChild(root);
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
    // 沿设施间固定闭环航点匀速巡逻（运动学插值，无导航系统）。
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
                var root = new Node3D { Name = $"Robot_{types[t].Name}_{k + 1}" };
                AddChild(root);
                MeshPart(root, types[t].Mesh(), types[t].Color, Vector3.Zero);
                if (t == 0)
                    MeshPart(root, new SphereMesh { Radius = 0.12f, Height = 0.24f }, new Color(0.9f, 0.9f, 0.9f), new Vector3(0, 0.72f, 0));

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
                    pts[i] = _facilityPositions[order[i]];
                var cum = new float[5];
                for (int i = 0; i < 4; i++)
                    cum[i + 1] = cum[i] + pts[i].DistanceTo(pts[(i + 1) % 4]);

                _robots.Add(new Patrol { Node = root, Points = pts, Cum = cum, Speed = types[t].Speed, Dist = rng.Randf() * cum[4] });
            }
        }
    }

    private void BuildCamera()
    {
        _camera = new Camera3D { Fov = 60f };
        _cameraCenter = new Vector3(_cfg.CameraPath.Center[0], _cfg.CameraPath.Center[1], _cfg.CameraPath.Center[2]);
        AddChild(_camera);

        if (!_benchmark)
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

        Directory.CreateDirectory(Path.GetDirectoryName(_framesPath)!);
        _csv = Godot.FileAccess.Open(_framesPath, Godot.FileAccess.ModeFlags.Write);
        if (_csv == null)
        {
            GD.PushError($"无法打开帧记录文件 {_framesPath}: {Godot.FileAccess.GetOpenError()}");
            GetTree().Quit(1);
            return;
        }
        _csv.StoreLine("frame,time_s,frame_ms,fps");
        _lastUsec = Time.GetTicksUsec();
    }

    private void RecordFrame()
    {
        if (_csv == null)
            return;
        ulong now = Time.GetTicksUsec();
        double frameMs = (now - _lastUsec) / 1000.0;
        _lastUsec = now;
        _frames++;
        _elapsed += frameMs / 1000.0;

        _csv.StoreLine(string.Create(Inv, $"{_frames},{_elapsed:0.000},{frameMs:0.000},{1000.0 / frameMs:0.00}"));
        if (_frames % 300 == 0)
            _csv.Flush();

        if (_elapsed >= _duration)
            FinishBenchmark();
    }

    private void FinishBenchmark()
    {
        _csv!.Flush();
        _csv.Close();
        _csv = null;

        double avgFps = _elapsed > 0 ? _frames / _elapsed : 0;
        var win = DisplayServer.WindowGetSize();
        float scale = GetViewport().Scaling3DScale;
        var summary = new Dictionary<string, object>
        {
            ["fixture"] = _cfg.Name,
            ["avg_fps"] = Math.Round(avgFps, 2),
            ["frames"] = _frames,
            ["duration_requested_s"] = _duration,
            ["duration_actual_s"] = Math.Round(_elapsed, 3),
            ["window_resolution"] = $"{win.X}x{win.Y}",
            ["internal_3d_resolution"] = $"{(int)(win.X * scale)}x{(int)(win.Y * scale)}",
            ["scaling_3d_scale"] = scale,
            ["msaa_3d"] = _cfg.Quality.Msaa3d,
            ["renderer"] = ProjectSettings.GetSettingWithOverride(new StringName("rendering/renderer/rendering_method")).AsString(),
            ["godot_version"] = Engine.GetVersionInfo()["string"].AsString(),
            ["seed"] = _cfg.Seed,
        };

        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(_summaryPath)!);
        File.WriteAllText(_summaryPath, json);

        GD.Print($"[Yudian] benchmark done: {_summaryPath}\n{json}");
        GetTree().Quit();
    }
}
