using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Yudian.Terrain;

/// <summary>独立工程探针；基准只读，候选显示没有世界提交或持久化。</summary>
public partial class TerrainProbe : Node3D
{
    private const string Fixtures = "res://fixtures/terrain-probe-r1/";
    private TerrainSnapshot _base;
    private TerrainPatch _level, _dig;
    private MeshInstance3D _stageNode, _fineNode;
    private ArrayMesh _stage, _fine;
    private Camera3D _camera;
    private Label _status;
    private string _mode = "none";
    private int _count, _temporaryReleased;
    private int _captureStep;
    private double _captureElapsed;
    private string _captureDir;
    private bool _cullCheck;

    public override void _Ready()
    {
        try
        {
            string assembly = typeof(TerrainProbe).Assembly.Location;
            string expected = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/Yudian.dll");
            string hash = Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(assembly)));
            Require(Path.GetFullPath(assembly) == Path.GetFullPath(expected), "current Debug assembly location");
            Require(hash == Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(expected))), "current Debug assembly hash");
            GD.Print($"PROBE_IDENTITY CLR={System.Environment.Version} assembly={assembly} SHA256={hash} display={DisplayServer.GetName()} adapter={RenderingServer.GetVideoAdapterName()}");
            var args = OS.GetCmdlineUserArgs();
            _cullCheck = Array.IndexOf(args, "--terrain-cull-check") >= 0;
            int captureArg = Array.IndexOf(args, "--terrain-capture-dir");
            if (captureArg >= 0)
            {
                Require(captureArg + 1 < args.Length, "capture directory argument");
                _captureDir = args[captureArg + 1];
                Require(Path.IsPathFullyQualified(_captureDir) && !Directory.Exists(_captureDir), "new absolute capture directory");
                Directory.CreateDirectory(_captureDir);
            }
            _base = TerrainDataCodec.ParseSnapshot(Read("base.json"));
            _level = TerrainDataCodec.ParsePatch(Read("level.json"));
            _dig = TerrainDataCodec.ParsePatch(Read("dig.json"));
            BuildScene();
            Select("base");
            if (_cullCheck) SetCullView(false);
            if (Array.IndexOf(args, "--terrain-probe-self-test") >= 0)
            {
                SelfTest();
                ReleaseMeshes();
                Require(_stage == null && _fine == null && _stageNode.Mesh == null && _fineNode.Mesh == null, "exit resource cleanup");
                GD.Print("PROBE_SELF_TEST PASS");
                GetTree().Quit(0);
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"PROBE_FAIL {ex}");
            GetTree().Quit(1);
        }
    }

    private static string Read(string name) => Godot.FileAccess.GetFileAsString(Fixtures + name);

    private void BuildScene()
    {
        AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("202b38"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White, AmbientLightEnergy = .65f } });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -25, 0), LightEnergy = 1.2f });
        var material = new StandardMaterial3D {
            AlbedoColor = _cullCheck ? new Color("ff55aa") : new Color("7fac9e"),
            Roughness = .9f, CullMode = BaseMaterial3D.CullModeEnum.Back,
            ShadingMode = _cullCheck ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel };
        _stageNode = new MeshInstance3D { Position = new Vector3(-8, 0, 0), MaterialOverride = material };
        _fineNode = new MeshInstance3D { Position = new Vector3(8, 0, 0), MaterialOverride = material };
        AddChild(_stageNode); AddChild(_fineNode);
        _camera = new Camera3D { Position = new Vector3(23, 24, 29), Fov = 52, Current = true };
        AddChild(_camera); _camera.LookAt(Vector3.Zero);
        if (!_cullCheck)
        {
            var ui = new CanvasLayer(); AddChild(ui);
            var panel = new VBoxContainer { Position = new Vector2(20, 20) }; ui.AddChild(panel);
            panel.AddChild(new Label { Text = "地形候选探针 · 左：Stage 7×7 / 右：Heightfield 13×13\n完整重建后替换；没有世界提交、导航或保存" });
            var row = new HBoxContainer(); panel.AddChild(row);
            AddButton(row, "1 基准", () => Select("base"));
            AddButton(row, "2 整平候选", () => Select("level"));
            AddButton(row, "3 开挖候选", () => Select("dig"));
            AddButton(row, "撤销候选预览", () => Select("base"));
            AddButton(row, "拒绝过期", () => InjectFailure(false));
            AddButton(row, "拒绝外围修改", () => InjectFailure(true));
            _status = new Label(); panel.AddChild(_status);
        }
    }

    private static void AddButton(HBoxContainer row, string text, Action action)
    {
        var button = new Button { Text = text }; button.Pressed += action; row.AddChild(button);
    }

    private void Select(string mode)
    {
        if (mode == _mode) { GD.Print($"PROBE_NOOP mode={mode} replacements={_count}"); return; }
        TerrainPatch patch = mode switch { "base" => null, "level" => _level, "dig" => _dig, _ => throw new ArgumentException("mode") };
        Replace(mode,
            () => patch == null ? StageMeshBuilder.Build(_base) : StageMeshBuilder.Build(patch, _base),
            () => patch == null ? HeightfieldMeshBuilder.Build(_base) : HeightfieldMeshBuilder.Build(patch, _base));
    }

    private void Replace(string mode, Func<TerrainMesh> stageBuilder, Func<TerrainMesh> fineBuilder)
    {
        ArrayMesh nextStage = null, nextFine = null;
        try
        {
            var watch = Stopwatch.StartNew();
            TerrainMesh stageCpu = stageBuilder(); nextStage = TerrainMeshAdapter.Create(stageCpu);
            double stageMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            TerrainMesh fineCpu = fineBuilder(); nextFine = TerrainMeshAdapter.Create(fineCpu);
            double fineMs = watch.Elapsed.TotalMilliseconds;
            ArrayMesh oldStage = _stage, oldFine = _fine;
            _stageNode.Mesh = nextStage; _fineNode.Mesh = nextFine;
            _stage = nextStage; _fine = nextFine; nextStage = null; nextFine = null;
            _mode = mode; _count++;
            oldStage?.Dispose(); oldFine?.Dispose();
            string result = $"mode={_mode} replacements={_count} base_version={_base.Version}\n" +
                $"Stage cpu={stageCpu.Vertices.Count} emitted={stageCpu.Indices.Count} triangles={stageCpu.Indices.Count / 3} build+submit_ms={stageMs:F3}\n" +
                $"Heightfield cpu={fineCpu.Vertices.Count} emitted={fineCpu.Indices.Count} triangles={fineCpu.Indices.Count / 3} build+submit_ms={fineMs:F3}";
            if (_status != null) _status.Text = result;
            GD.Print("PROBE_REPLACE " + result.Replace('\n', ' '));
        }
        finally
        {
            if (nextStage != null) { nextStage.Dispose(); _temporaryReleased++; }
            if (nextFine != null) { nextFine.Dispose(); _temporaryReleased++; }
        }
    }

    private void InjectFailure(bool perimeter)
    {
        try
        {
            TerrainPatch patch = perimeter ? TerrainDataCodec.ParsePatch(Read("invalid-perimeter.json")) : _level;
            TerrainSnapshot current = perimeter ? _base : TerrainDataCodec.ParseSnapshot(Read("stale-current.json"));
            Replace("rejected", () => StageMeshBuilder.Build(patch, current), () => HeightfieldMeshBuilder.Build(patch, current));
            throw new InvalidOperationException("invalid input unexpectedly accepted");
        }
        catch (ArgumentException ex)
        {
            GD.Print($"PROBE_REJECT mode={_mode} replacements={_count} reason={ex.Message}");
            if (_status != null) _status.Text += "\n已拒绝：" + ex.Message;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.Key1: Select("base"); break;
            case Key.Key2: Select("level"); break;
            case Key.Key3: Select("dig"); break;
            case Key.Key4: InjectFailure(false); break;
            case Key.Key5: InjectFailure(true); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    private void SelfTest()
    {
        string baseline = TerrainDataCodec.Serialize(_base);
        Require(_mode == "base" && _count == 1, "cold start base");
        var stage = _stage; var fine = _fine; Select("base");
        Require(_count == 1 && ReferenceEquals(stage, _stage) && ReferenceEquals(fine, _fine), "duplicate base noop");
        Select("level"); Require(_count == 2 && !GodotObject.IsInstanceValid(stage) && !GodotObject.IsInstanceValid(fine), "old resources released");
        stage = _stage; fine = _fine; Select("level");
        Require(_count == 2 && ReferenceEquals(stage, _stage) && ReferenceEquals(fine, _fine), "duplicate candidate noop");
        InjectFailure(false); InjectFailure(true);
        Require(_count == 2 && _mode == "level" && ReferenceEquals(stage, _stage) && ReferenceEquals(fine, _fine), "real rejection preserves display");
        int released = _temporaryReleased;
        try { Replace("broken", () => StageMeshBuilder.Build(_base), () => throw new ArgumentException("injected second build failure")); }
        catch (ArgumentException) { }
        Require(_temporaryReleased == released + 1 && _count == 2 && ReferenceEquals(stage, _stage) && ReferenceEquals(fine, _fine), "partial candidate released and old pair preserved");
        for (int i = 0; i < 30; i++) Select(i % 2 == 0 ? "dig" : "level");
        Require(_count == 32 && _mode == "level", "30 replacements");
        Select("base"); Require(_count == 33 && TerrainDataCodec.Serialize(_base) == baseline && _base.Version == 9, "preview reset retains immutable base");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("check failed: " + name);
        GD.Print("PROBE_CHECK PASS " + name);
    }

    public override void _Process(double delta)
    {
        if (_captureDir == null) return;
        _captureElapsed += delta;
        if (_captureElapsed < 1.5) return;
        _captureElapsed = 0;
        try
        {
            string name = _cullCheck ? (_captureStep == 0 ? "top" : "bottom") : _mode;
            using var image = GetViewport().GetTexture().GetImage();
            Require(image.SavePng(Path.Combine(_captureDir, name + ".png")) == Error.Ok, "capture " + name);
            if (_cullCheck)
            {
                if (_captureStep == 0) SetCullView(true); else GetTree().Quit();
            }
            else
            {
                switch (_captureStep) { case 0: Select("level"); break; case 1: Select("dig"); break; default: GetTree().Quit(); break; }
            }
            _captureStep++;
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private void SetCullView(bool bottom)
    {
        // 完全无 UI / 地板，正下方对照只留下被 back-cull 剔除的地形。
        _camera.Position = new Vector3(0, bottom ? -30 : 30, 0);
        _camera.LookAt(Vector3.Zero, Vector3.Forward);
    }

    private void ReleaseMeshes()
    {
        if (_stageNode != null) _stageNode.Mesh = null;
        if (_fineNode != null) _fineNode.Mesh = null;
        _stage?.Dispose(); _fine?.Dispose(); _stage = null; _fine = null;
    }

    public override void _ExitTree() => ReleaseMeshes();
}
