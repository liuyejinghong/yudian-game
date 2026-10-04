using System;
using Godot;
namespace Yudian;

public partial class AssetPreview : Node3D
{
    private VisualStateBridge[] _bridges = [];
    private Node3D[] _entities = [];
    private bool _captured;
    private Label _label = null!;

    public override void _Ready()
    {
        try
        {
            var sample = GD.Load<PackedScene>("res://assets/calibration/art_i00.glb");
            _bridges = new VisualStateBridge[3];
            _entities = new Node3D[3];
            for (int i = 0; i < 3; i++)
            {
                var entity = new Node3D { Name = $"EntityRoot_{i}", Position = new Vector3((i-1)*2,0,0), Rotation = new Vector3(0, i*MathF.PI/2,0) };
                AddChild(entity);
                var visual = sample.Instantiate<Node3D>();
                visual.Name = "VisualRoot";
                entity.AddChild(visual);
                _entities[i] = entity;
                _bridges[i] = new VisualStateBridge(visual);
                CheckImport(visual);
            }
            AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(12,8) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.42f,.31f,.23f), Roughness = 1 } });
            var camera = new Camera3D { Position = new Vector3(5,6,8), Current = true, Fov = 50 };
            AddChild(camera); camera.LookAt(Vector3.Zero);
            var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-55,-30,0), ShadowEnabled = true };
            AddChild(sun);
            AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(.2f,.22f,.25f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .6f } });
            var layer = new CanvasLayer(); AddChild(layer);
            _label = new Label { Position = new Vector2(20,20) }; layer.AddChild(_label);
            SetPreviewState("idle");
            if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--art-self-test") >= 0)
            {
                foreach (var state in VisualStateBridge.States) SetPreviewState(state);
                SetPreviewState("work");
                var transforms = Array.ConvertAll(_entities, e => e.Transform);
                foreach (var bridge in _bridges) bridge.Preview(1);
                for (int i = 0; i < _entities.Length; i++)
                    if (_entities[i].Transform != transforms[i]) throw new Exception("视觉动作改变EntityRoot");
                SetPreviewState("idle");
                bool rejected = false;
                try { _bridges[0].SetState("produce_resources"); }
                catch (ArgumentException) { rejected = true; }
                if (!rejected) throw new Exception("未知状态未被拒绝");
                GD.Print("ART_I00_BRIDGE_OK: seven states, unknown rejected, EntityRoot fixed");
                GetTree().Quit();
            }
            GD.Print("ART_I00_IMPORT_OK: GLB meters, Y-up, -Z forward, ground origin, 4 sockets, EntityRoot/VisualRoot");
        }
        catch (Exception e) { GD.PushError(e.Message); GetTree().Quit(1); }
    }

    private static void CheckImport(Node3D visual)
    {
        var body = visual.FindChild("Body", true, false) as MeshInstance3D ?? throw new Exception("Body missing");
        var bounds = body.Mesh.GetAabb();
        if (MathF.Abs(bounds.Position.Y) > .0001 || bounds.Size.DistanceTo(new Vector3(.9f,.7f,1.25f)) > .0001)
            throw new Exception("GLB 原点或米制比例错误");
        var nose = visual.FindChild("ForwardMarker", true, false) as MeshInstance3D ?? throw new Exception("前向标识缺失");
        if (nose.Mesh.GetAabb().Position.Z >= -.625f) throw new Exception("GLB 前向不是 -Z");
        foreach (var name in new[] { "Socket_Cargo", "Socket_TowFront", "Socket_TowRear", "Socket_Charge" })
            if (visual.FindChild(name, true, false) is not Node3D) throw new Exception(name + " missing");
        if (visual.Position.Length() > .0001 || visual.Scale.DistanceTo(Vector3.One) > .0001)
            throw new Exception("VisualRoot 存在未声明位移或缩放");
    }

    private void SetPreviewState(string state)
    {
        foreach (var bridge in _bridges) bridge.SetState(state);
        _label.Text = $"ART-I00 calibration only / preview state: {state}\n1 idle  2 move  3 work  4 charge  5 disabled  6 towed  7 maintenance\nNo game simulation, cargo, power or repair effects.";
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode >= Key.Key1 && key.Keycode <= Key.Key7)
            SetPreviewState(VisualStateBridge.States[(int)key.Keycode-(int)Key.Key1]);
    }

    public override void _Process(double delta)
    {
        var capture = System.Environment.GetEnvironmentVariable("YUDIAN_CAPTURE_PNG");
        if (!_captured && capture != null && Engine.GetProcessFrames() > 120)
        {
            _captured = true;
            var error = GetViewport().GetTexture().GetImage().SavePng(capture);
            if (error != Error.Ok) { GD.PushError("截图保存失败: " + error); GetTree().Quit(1); }
        }
        foreach (var bridge in _bridges) bridge.Preview(delta);
    }
}
