#nullable enable
using System;
using System.Globalization;
using Godot;

namespace Yudian.PlayerUI;

/// <summary>
/// E01 玩家界面（D1.0）：RTS 斜俯视镜头（WASD/拖动平移、滚轮缩放）、鼠标选择机器人/地面、
/// 固定半径 2m 选区预览与现场范围标记、真实目标卡与指令按钮。
/// 只经 Main 公开合同读取事实与下达命令（ReadPlayerState / ReadPlayerRobots /
/// PreviewLevel / QueueLevel / QueuePlayerAction），不持有任务或地形事实、不本地推进进度。
/// 世界点击走 _UnhandledInput，UI 点击不穿透；命令按钮带冷却，双击不重复提交。
/// </summary>
public partial class PlayerController : Node
{
    // 未来美术消费点：颜色、半径与阈值集中在此，A01/A03 接手时整体替换。
    private static class Palette
    {
        public static readonly Color PanelBg = Color.FromHtml("f2eee7");
        public static readonly Color PanelBorder = Color.FromHtml("c9c0b2");
        public static readonly Color Text = Color.FromHtml("2b2622");
        public static readonly Color TextDim = Color.FromHtml("6b6258");
        public static readonly Color Accent = Color.FromHtml("c96f2e");
        public static readonly Color AccentText = Color.FromHtml("fff8f0");
        public static readonly Color Legal = Color.FromHtml("3e7c4f");
        public static readonly Color Illegal = Color.FromHtml("b0392e");
        public static readonly Color Site = Color.FromHtml("c96f2e");
        public static readonly Color JobActive = Color.FromHtml("e0972e");
        public static readonly Color JobDone = Color.FromHtml("8a8378");
        public static readonly Color Select = Color.FromHtml("efe3c2");
    }

    private const float SiteRadiusM = 2f;
    private const float CommandCooldownS = 0.35f;
    private const float PreviewIntervalS = 0.15f;
    private const float MinDistanceM = 7f, MaxDistanceM = 70f;
    private const float ClickSlackPx = 8f;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private Main _world = null!;
    private Camera3D _camera = null!;
    private bool _initialized;

    // 镜头状态：保持初始斜俯视姿态，仅平移焦点与改变视距。
    private Basis _cameraBasis = Basis.Identity;
    private Vector3 _focus = Vector3.Zero;
    private float _distance = 26f;
    private Vector2 _mousePos;
    private Vector2? _panGrab;
    private bool _rightDragged;
    private Vector2 _leftPress;
    private bool _leftTracking;

    // 选择与预览：只存选择指针与最近一次预览结果，不存世界事实。
    private Vector3? _selectedSite;
    private Vector3? _hoverPoint;
    private string? _selectedRobotId;
    private PlayerSitePreview? _preview;
    private float _previewTimer;
    private float _commandCooldown;
    private PlayerRobotView[] _robots = [];

    private Node3D _markers = null!;
    private Mesh _ringMesh = null!, _discMesh = null!;
    private Marker _previewMarker = null!, _siteMarker = null!, _jobMarker = null!, _selectMarker = null!;
    private Label _statusLabel = null!, _jobLabel = null!, _selectionLabel = null!, _worldLabel = null!;
    private Button _confirmButton = null!, _cancelButton = null!, _pauseButton = null!,
        _saveButton = null!, _loadButton = null!, _recoverButton = null!;

    private sealed record Marker(Node3D Root, MeshInstance3D Ring, MeshInstance3D? Disc);

    public void Initialize(Main world, Camera3D camera)
    {
        _world = world;
        _camera = camera;
        var viewDir = -_camera.GlobalBasis.Z;
        float t = viewDir.Y < -0.001f ? -_camera.GlobalPosition.Y / viewDir.Y : 20f;
        _distance = Mathf.Clamp(t, MinDistanceM, MaxDistanceM);
        var hit = _camera.GlobalPosition + viewDir * t;
        _focus = new Vector3(hit.X, 0, hit.Z);
        _cameraBasis = _camera.GlobalBasis.Orthonormalized();
        _mousePos = _camera.GetViewport().GetVisibleRect().Size / 2f;

        BuildMarkers();
        BuildHud();
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        if (!_initialized) return;
        float dt = (float)delta;
        _commandCooldown = MathF.Max(0f, _commandCooldown - dt);
        var state = _world.ReadPlayerState();
        _robots = _world.ReadPlayerRobots();
        UpdateCamera(state.ExtentM + 4f, dt);
        UpdateAimAndPreview(state, dt);
        UpdateHud(state);
        UpdateMarkers(state);
    }

    // ---- 输入 ----

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_initialized) return;
        switch (@event)
        {
            case InputEventMouseMotion motion:
                _mousePos = motion.Position;
                if (_panGrab is { } grab && MousePlanePoint(grab) is { } grabPoint && MousePlanePoint(motion.Position) is { } now)
                {
                    _focus += grabPoint - now;
                    if (motion.Relative.LengthSquared() > 0.25f) _rightDragged = true;
                }
                break;
            case InputEventMouseButton button:
                HandleMouseButton(button);
                break;
        }
    }

    private void HandleMouseButton(InputEventMouseButton button)
    {
        if (button.Pressed)
        {
            switch (button.ButtonIndex)
            {
                case MouseButton.Left:
                    _leftPress = button.Position;
                    _leftTracking = true;
                    break;
                case MouseButton.Right:
                    _panGrab = button.Position;
                    _rightDragged = false;
                    break;
                case MouseButton.Middle:
                    _panGrab = button.Position;
                    break;
                case MouseButton.WheelUp:
                    _distance = Mathf.Clamp(_distance * 0.88f, MinDistanceM, MaxDistanceM);
                    break;
                case MouseButton.WheelDown:
                    _distance = Mathf.Clamp(_distance / 0.88f, MinDistanceM, MaxDistanceM);
                    break;
            }
            return;
        }
        switch (button.ButtonIndex)
        {
            case MouseButton.Left when _leftTracking:
                _leftTracking = false;
                if (button.Position.DistanceTo(_leftPress) <= ClickSlackPx)
                    HandleClick(button.Position);
                break;
            case MouseButton.Right:
                _panGrab = null;
                if (!_rightDragged) ClearSelection(); // 右键单击取消选择，拖动是平移
                break;
            case MouseButton.Middle:
                _panGrab = null;
                break;
        }
    }

    private void HandleClick(Vector2 screenPos)
    {
        string? robot = PickRobot(screenPos);
        if (robot != null)
        {
            _selectedRobotId = robot;
            _selectedSite = null;
            _preview = null;
            return;
        }
        if (GroundHit(screenPos, out var point))
        {
            _selectedSite = point;
            _selectedRobotId = null;
            _preview = null;
            return;
        }
        // 空点：仅清除选择，不下任何命令。
        ClearSelection();
    }

    private void ClearSelection()
    {
        _selectedRobotId = null;
        _selectedSite = null;
        _preview = null;
    }

    // ---- 相机 ----

    private void UpdateCamera(float focusLimit, float dt)
    {
        var flatForward = -_cameraBasis.Z;
        flatForward.Y = 0f;
        flatForward = flatForward.Normalized();
        var flatRight = _cameraBasis.X;
        flatRight.Y = 0f;
        flatRight = flatRight.Normalized();

        float axis = Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1f :
            Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? -1f : 0f;
        float side = Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1f :
            Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? -1f : 0f;
        float speed = _distance * 0.9f * dt;
        _focus += (flatForward * axis + flatRight * side) * speed;

        _focus = new Vector3(Mathf.Clamp(_focus.X, -focusLimit, focusLimit), 0,
            Mathf.Clamp(_focus.Z, -focusLimit, focusLimit));
        _camera.GlobalTransform = new Transform3D(_cameraBasis, _focus + _cameraBasis.Z * _distance);
    }

    // ---- 选择几何 ----

    private Vector3? MousePlanePoint(Vector2 screenPos)
    {
        var origin = _camera.ProjectRayOrigin(screenPos);
        var dir = _camera.ProjectRayNormal(screenPos);
        if (dir.Y >= -1e-4f) return null;
        float t = -origin.Y / dir.Y;
        return t > 0 && float.IsFinite(t) ? origin + dir * t : null;
    }

    private bool GroundHit(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.Zero;
        var origin = _camera.ProjectRayOrigin(screenPos);
        var dir = _camera.ProjectRayNormal(screenPos);
        if (dir.Y >= -1e-4f) return false;
        using var query = PhysicsRayQueryParameters3D.Create(origin + dir * 0.5f, origin + dir * 400f, 1);
        using var hit = _markers.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;
        point = hit["position"].AsVector3();
        return true;
    }

    private float? GroundY(float x, float z)
    {
        using var query = PhysicsRayQueryParameters3D.Create(new Vector3(x, 60f, z), new Vector3(x, -60f, z), 1);
        using var hit = _markers.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? null : hit["position"].AsVector3().Y;
    }

    private string? PickRobot(Vector2 screenPos)
    {
        var origin = _camera.ProjectRayOrigin(screenPos);
        var dir = _camera.ProjectRayNormal(screenPos);
        string? best = null;
        float bestAlong = float.MaxValue;
        foreach (var robot in _robots)
        {
            var center = robot.Position + Vector3.Up * 0.45f;
            float along = (center - origin).Dot(dir);
            if (along <= 0.5f) continue;
            float miss = (origin + dir * along).DistanceTo(center);
            if (miss <= MathF.Max(robot.Radius + 0.5f, along * 0.02f) && along < bestAlong)
            {
                bestAlong = along;
                best = robot.Id;
            }
        }
        return best;
    }

    // 望山/驮运不能当筑垒执行：非筑垒选择一律传 null，由权威自动选可达者。
    private string? SelectedWorkerId()
        => _selectedRobotId is { } id && id.StartsWith("Robot_Zhulei_", StringComparison.Ordinal) ? id : null;

    // ---- 预览 ----

    private void UpdateAimAndPreview(PlayerReadModel state, float dt)
    {
        bool jobActive = state.Job is { Active: true };
        if (_selectedSite == null && !jobActive)
            _hoverPoint = MousePlanePoint(_mousePos);
        var aim = _selectedSite ?? _hoverPoint;
        if (jobActive || aim is not { } target)
        {
            _preview = null;
            return;
        }
        _previewTimer -= dt;
        // 版本或目标变化立即重预览，避免拿旧版本号下达。
        if (_preview == null || _previewTimer <= 0f || _preview.Center != target || _preview.Version != state.Version)
        {
            _preview = _world.PreviewLevel(target, SelectedWorkerId());
            _previewTimer = PreviewIntervalS;
        }
    }

    // ---- HUD ----

    private void BuildHud()
    {
        var hud = new CanvasLayer { Name = "PlayerHud" };
        AddChild(hud);

        var panel = new PanelContainer
        {
            Name = "InfoPanel",
            Position = new Vector2(16, 16),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        hud.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);
        box.AddChild(NewLabel("余电 · 整平作业", 20, Palette.Text));
        _statusLabel = NewLabel("", 16, Palette.Accent);
        _statusLabel.Name = "StatusLabel";
        _statusLabel.CustomMinimumSize = new Vector2(430, 0);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        box.AddChild(_statusLabel);
        _jobLabel = NewLabel("", 17, Palette.Text);
        _jobLabel.Name = "JobLabel";
        _jobLabel.CustomMinimumSize = new Vector2(430, 0);
        _jobLabel.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        box.AddChild(_jobLabel);
        _selectionLabel = NewLabel("", 15, Palette.TextDim);
        _selectionLabel.Name = "SelectionLabel";
        _selectionLabel.CustomMinimumSize = new Vector2(430, 0);
        _selectionLabel.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        box.AddChild(_selectionLabel);
        _worldLabel = NewLabel("", 14, Palette.TextDim);
        _worldLabel.Name = "WorldLabel";
        box.AddChild(_worldLabel);

        var bar = new CenterContainer
        {
            Name = "CommandBar",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -80, OffsetBottom = -14,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hud.AddChild(bar);
        var barPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        barPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        bar.AddChild(barPanel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        barPanel.AddChild(row);

        _confirmButton = MakeButton("ConfirmButton", "确认整平", DoConfirm, accent: true);
        _cancelButton = MakeButton("CancelButton", "取消任务", () => _world.QueuePlayerAction("cancel"));
        _pauseButton = MakeButton("PauseButton", "暂停", () => _world.QueuePlayerAction("pause"));
        _saveButton = MakeButton("SaveButton", "保存", () => _world.QueuePlayerAction("save"));
        _loadButton = MakeButton("LoadButton", "读取", () => _world.QueuePlayerAction("load"));
        _recoverButton = MakeButton("RecoverButton", "故障恢复", () => _world.QueuePlayerAction("recover"));
        row.AddChild(_confirmButton);
        row.AddChild(_cancelButton);
        row.AddChild(_pauseButton);
        row.AddChild(_saveButton);
        row.AddChild(_loadButton);
        row.AddChild(_recoverButton);
    }

    private static Label NewLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = Palette.PanelBg,
        BorderColor = Palette.PanelBorder,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
    };

    private static StyleBoxFlat ButtonStyle(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6,
    };

    // 所有命令按钮统一走冷却包装：双击的第二下被丢弃，不重复提交。
    private Button MakeButton(string name, string text, Action command, bool accent = false)
    {
        var button = new Button
        {
            Text = text,
            Name = name,
            FocusMode = Control.FocusModeEnum.None,
        };
        if (accent)
        {
            button.AddThemeStyleboxOverride("normal", ButtonStyle(Palette.Accent));
            button.AddThemeStyleboxOverride("hover", ButtonStyle(Palette.Accent.Lightened(0.1f)));
            button.AddThemeStyleboxOverride("pressed", ButtonStyle(Palette.Accent.Darkened(0.15f)));
            button.AddThemeStyleboxOverride("disabled", ButtonStyle(Palette.PanelBorder));
            button.AddThemeColorOverride("font_color", Palette.AccentText);
            button.AddThemeColorOverride("font_hover_color", Palette.AccentText);
            button.AddThemeColorOverride("font_pressed_color", Palette.AccentText);
            button.AddThemeColorOverride("font_disabled_color", Palette.TextDim);
        }
        button.Pressed += () =>
        {
            if (_commandCooldown > 0f) return;
            _commandCooldown = CommandCooldownS;
            command();
        };
        return button;
    }

    private void DoConfirm()
    {
        if (_preview is not { Legal: true } preview) return;
        _world.QueueLevel(preview.Center, preview.Version, SelectedWorkerId());
    }

    private void UpdateHud(PlayerReadModel state)
    {
        _statusLabel.Text = state.Notice;
        _jobLabel.Text = state.Job is not { } job
            ? "当前没有任务；点击地面选择整平位置，确认后筑垒自动前往"
            : string.Format(Inv,
                "任务 {0} · {1} · 执行者 {2} · 进度 {3:P0}\n中心 ({4:F1}, {5:F1}){6}",
                job.Id, job.Stage, WorkerDisplay(job.WorkerId), job.Progress,
                job.Center.X, job.Center.Z, job.Active ? "" : " · 非活动");

        _selectionLabel.Text =
            _selectedRobotId is { } id
                ? Array.Find(_robots, r => r.Id == id) is { } robot
                    ? $"已选 {robot.Name}（{id}）" + (id.StartsWith("Robot_Zhulei_", StringComparison.Ordinal)
                        ? " · 确认时由该筑垒执行"
                        : " · 望山/驮运不执行整平，确认时自动选择可达筑垒")
                    : "已选机器人（等待世界状态）"
                : _selectedSite is { } site
                    ? string.Format(Inv, "已选位置 ({0:F1}, {1:F1}) · {2}", site.X, site.Z, _preview?.Reason ?? "预览中…")
                    : "左键点选机器人或地面；右键取消选择";

        _worldLabel.Text = string.Format(Inv,
            "权威 v{0} · 模拟 {1:F0}s · 存档 {2} · {3}{4}",
            state.Version, state.TimeSeconds, state.SaveExists ? "已有" : "无",
            state.Paused ? "已暂停" : "运行中", state.Ready ? "" : " · 世界恢复中");

        bool jobActive = state.Job is { Active: true };
        _confirmButton.Disabled = !(_preview is { Legal: true } && !jobActive && state.Ready) || _commandCooldown > 0f;
        _cancelButton.Disabled = !jobActive;
        _pauseButton.Text = state.Paused ? "继续" : "暂停";
        _saveButton.Disabled = !state.Ready;
        _loadButton.Disabled = !state.Ready || !state.SaveExists;
    }

    private string WorkerDisplay(string? workerId)
        => workerId == null ? "未分配"
            : Array.Find(_robots, r => r.Id == workerId) is { } robot ? robot.Name : workerId;

    // ---- 标记 ----

    private void BuildMarkers()
    {
        _markers = new Node3D { Name = "Markers" };
        AddChild(_markers);
        _ringMesh = FlatRingMesh(0.92f, 1f, 48);
        _discMesh = FlatDiscMesh(1f, 48);
        _previewMarker = MakeMarker(withDisc: true);
        _siteMarker = MakeMarker(false);
        _jobMarker = MakeMarker(false);
        _selectMarker = MakeMarker(false);
    }

    private Marker MakeMarker(bool withDisc)
    {
        var root = new Node3D { Visible = false };
        var ring = new MeshInstance3D { Mesh = _ringMesh, MaterialOverride = MarkerMaterial(Palette.Site, 1f) };
        root.AddChild(ring);
        MeshInstance3D? disc = null;
        if (withDisc)
        {
            disc = new MeshInstance3D { Mesh = _discMesh, MaterialOverride = MarkerMaterial(Palette.Site, 0.14f) };
            root.AddChild(disc);
        }
        _markers.AddChild(root);
        return new Marker(root, ring, disc);
    }

    private static StandardMaterial3D MarkerMaterial(Color color, float alpha)
        => new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(color.R, color.G, color.B, alpha),
            Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 20,
        };

    private void UpdateMarkers(PlayerReadModel state)
    {
        var job = state.Job;
        Color previewColor = _preview is { Legal: true } ? Palette.Legal : Palette.Illegal;
        PlaceMarker(_previewMarker, _preview?.Center, previewColor, SiteRadiusM, showDisc: _preview != null);
        PlaceMarker(_siteMarker, _selectedSite, Palette.Site, SiteRadiusM);
        PlaceMarker(_jobMarker, job?.Center, job is { Active: true } ? Palette.JobActive : Palette.JobDone, SiteRadiusM);
        var selected = Array.Find(_robots, r => r.Id == _selectedRobotId);
        PlaceMarker(_selectMarker, selected?.Position, Palette.Select, (selected?.Radius ?? 1f) + 0.35f);
    }

    private void PlaceMarker(Marker marker, Vector3? pos, Color color, float radius, bool showDisc = true)
    {
        if (pos is not { } p || GroundY(p.X, p.Z) is not { } y)
        {
            marker.Root.Visible = false;
            return;
        }
        marker.Root.Visible = true;
        marker.Root.Position = new Vector3(p.X, y + 0.07f, p.Z);
        marker.Root.Scale = new Vector3(radius, 1f, radius);
        ((StandardMaterial3D)marker.Ring.MaterialOverride).AlbedoColor = new Color(color.R, color.G, color.B, 1f);
        if (marker.Disc != null)
        {
            marker.Disc.Visible = showDisc;
            ((StandardMaterial3D)marker.Disc.MaterialOverride).AlbedoColor = new Color(color.R, color.G, color.B, 0.14f);
        }
    }

    private static Mesh FlatRingMesh(float inner, float outer, int segments)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float a0 = MathF.Tau * i / segments, a1 = MathF.Tau * (i + 1) / segments;
            Vector3 i0 = At(inner, a0), i1 = At(inner, a1), o0 = At(outer, a0), o1 = At(outer, a1);
            st.AddVertex(i0); st.AddVertex(i1); st.AddVertex(o0);
            st.AddVertex(o0); st.AddVertex(i1); st.AddVertex(o1);
        }
        return st.Commit();
    }

    private static Mesh FlatDiscMesh(float radius, int segments)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float a0 = MathF.Tau * i / segments, a1 = MathF.Tau * (i + 1) / segments;
            st.AddVertex(Vector3.Zero);
            st.AddVertex(At(radius, a0));
            st.AddVertex(At(radius, a1));
        }
        return st.Commit();
    }

    private static Vector3 At(float radius, float angle)
        => new(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
}
