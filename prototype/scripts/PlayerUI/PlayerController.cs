#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Yudian.PlayerUI;

// Player input queues commands; terrain and job facts remain in Main.
public partial class PlayerController : Node
{
    private static class Palette
    {
        public static readonly Color PanelBg = Color.FromHtml("f2eee7");
        public static readonly Color PanelBorder = Color.FromHtml("c9c0b2");
        public static readonly Color Text = Color.FromHtml("2b2622");
        public static readonly Color TextDim = Color.FromHtml("6b6258");
        public static readonly Color Accent = Color.FromHtml("c96f2e");
        public static readonly Color AccentText = Color.FromHtml("fff8f0");
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
    private bool _leftTracking, _aimCleared;

    // 选择与预览：只存选择指针与最近一次预览结果，不存世界事实。
    private Vector3? _selectedSite;
    private Vector3? _hoverPoint;
    private string? _selectedRobotId;
    private PlayerSitePreview? _preview;
    private float _previewTimer;
    private float _commandCooldown;
    private PlayerRobotView[] _robots = [];

    // 建设模式：null 为整平；蓝图与基地状态只信 ReadBootstrap，每帧重读。
    private BootstrapReadModel? _bootstrap;
    private string? _buildType;
    private BuildBlueprintView? _currentBlueprint;
    private float _buildRadius = SiteRadiusM;
    private int _yawSteps;
    private bool _modesBuilt;

    private Node3D _markers = null!;
    private GodotObject _markerFactory = null!;
    private Marker _previewMarker = null!, _siteMarker = null!, _jobMarker = null!, _selectMarker = null!;
    private Label _titleLabel = null!, _statusLabel = null!, _jobLabel = null!, _selectionLabel = null!,
        _worldLabel = null!, _stockLabel = null!, _supportLabel = null!, _facilityLabel = null!;
    private ScrollContainer _infoScroll = null!;
    private Button _confirmButton = null!, _cancelButton = null!, _pauseButton = null!,
        _saveButton = null!, _loadButton = null!, _recoverButton = null!,
        _connectButton = null!, _retryButton = null!;
    private HBoxContainer _rowModes = null!;
    private Button? _rotateButton;
    private ButtonGroup _modeGroup = null!;

    private sealed class Marker
    {
        public MeshInstance3D? Node;
        public Vector3? Center;
        public string Kind = "";
        public float Radius;
        public long Version = -1;
    }

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
        _bootstrap = _world.ReadBootstrap();
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
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.Escape:
                ClearSelection(); break;
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.F:
                var target = _selectedSite ?? Array.Find(_robots, r => r.Id == _selectedRobotId)?.Position ?? _world.ReadPlayerState().Job?.Center;
                if (target is { } p) _focus = new Vector3(p.X, 0, p.Z);
                break;
            case InputEventMouseMotion motion:
                _mousePos = motion.Position; _aimCleared = false;
                if (_panGrab is { } grab && MousePlanePoint(grab) is { } grabPoint && MousePlanePoint(motion.Position) is { } now)
                {
                    _focus += grabPoint - now;
                    _panGrab = motion.Position;
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
        _aimCleared = false;
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
            _preview = null;
            return;
        }
        // 空点：仅清除选择，不下任何命令。
        ClearSelection();
    }

    private void ClearSelection()
    {
        _hoverPoint = null; _aimCleared = true;
        _selectedRobotId = null;
        _selectedSite = null;
        _preview = null;
    }

    // ---- 相机 ----

    private void UpdateCamera(float focusLimit, float dt)
    {
        float rotation = (Input.IsKeyPressed(Key.Q) ? 1 : 0) - (Input.IsKeyPressed(Key.E) ? 1 : 0);
        _cameraBasis = _cameraBasis.Rotated(Vector3.Up, rotation * dt).Orthonormalized();
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

    // ---- 建设模式 ----

    private bool BootstrapOn => _bootstrap is { Enabled: true };
    private bool Building => BootstrapOn && _buildType != null;
    private float YawRadians() => _yawSteps switch
    {
        1 => MathF.PI / 2f,
        2 => MathF.PI,
        3 => -MathF.PI / 2f,
        _ => 0f,
    };

    // 蓝图按钮只在首次读到 Enabled 且有蓝图时建立一次；种类来自配置，不写死。
    private void EnsureBootstrapControls(BootstrapReadModel boot)
    {
        if (_modesBuilt) return;
        _modesBuilt = true;
        _modeGroup = new ButtonGroup();
        var level = ModeToggle("ModeLevelButton", "整平", null);
        level.ButtonPressed = true;
        _rowModes.AddChild(level);
        foreach (var blueprint in boot.Blueprints)
            _rowModes.AddChild(ModeToggle("ModeButton_" + blueprint.Id, blueprint.Name, blueprint));
        _rotateButton = MakeButton("RotateButton", "朝向 0°", RotateYaw);
        _rowModes.AddChild(_rotateButton);
    }

    private Button ModeToggle(string name, string text, BuildBlueprintView? blueprint)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.ButtonGroup = _modeGroup;
        button.Pressed += () =>
        {
            button.ButtonPressed = true; // ButtonGroup 允许全部弹起，这里强制保持单选
            SelectMode(blueprint);
        };
        return button;
    }

    private void SelectMode(BuildBlueprintView? blueprint)
    {
        _currentBlueprint = blueprint;
        _buildType = blueprint?.Id;
        _buildRadius = blueprint?.Radius ?? SiteRadiusM;
        _yawSteps = 0;
        if (_rotateButton != null) _rotateButton.Text = "朝向 0°";
        _preview = null;
    }

    private void RotateYaw()
    {
        _yawSteps = (_yawSteps + 1) % 4;
        _rotateButton!.Text = "朝向 " + _yawSteps * 90 + "°";
        _preview = null;
    }

    // ---- 预览 ----

    private void UpdateAimAndPreview(PlayerReadModel state, float dt)
    {
        bool jobActive = state.Job is { Active: true };
        if (_selectedSite == null && !jobActive && !_aimCleared)
            _hoverPoint = MousePlanePoint(_mousePos);
        var aim = _selectedSite ?? _hoverPoint;
        if (jobActive || aim is not { } target)
        {
            _preview = null;
            return;
        }
        _previewTimer -= dt;
        // 版本或目标变化立即重预览，避免拿旧版本号下达；建设预览中心 Y 由权威归零，只比 XZ。
        if (_preview == null || _previewTimer <= 0f ||
            _preview.Center.X != target.X || _preview.Center.Z != target.Z || _preview.Version != state.Version)
        {
            _preview = Building
                ? _world.PreviewBuild(_buildType!, target, YawRadians())
                : _world.PreviewLevel(target, SelectedWorkerId());
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
        _titleLabel = NewLabel("余电 · 整平作业", 20, Palette.Text);
        box.AddChild(_titleLabel);
        // 信息区限高滚动：设施与库存随游戏增长，面板不得遮满 1280x800 世界。
        _infoScroll = new ScrollContainer
        {
            Name = "InfoScroll",
            CustomMinimumSize = new Vector2(284, 400),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(_infoScroll);
        var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 6);
        _infoScroll.AddChild(info);
        _statusLabel = InfoLabel("StatusLabel", 16, Palette.Accent);
        _jobLabel = InfoLabel("JobLabel", 17, Palette.Text);
        _selectionLabel = InfoLabel("SelectionLabel", 15, Palette.TextDim);
        _worldLabel = InfoLabel("WorldLabel", 14, Palette.TextDim);
        _stockLabel = InfoLabel("StockLabel", 14, Palette.TextDim);
        _supportLabel = InfoLabel("SupportLabel", 14, Palette.Accent);
        _facilityLabel = InfoLabel("FacilityLabel", 14, Palette.TextDim);
        info.AddChild(_statusLabel);
        info.AddChild(_jobLabel);
        info.AddChild(_selectionLabel);
        info.AddChild(_worldLabel);
        info.AddChild(_stockLabel);
        info.AddChild(_supportLabel);
        info.AddChild(_facilityLabel);

        var hints = new Label { Text = "WASD／方向键平移 · 右键拖动 · 滚轮缩放 · Q/E 旋转 · F 定位 · Esc 清除选区",
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -136, OffsetBottom = -114,
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        hints.AddThemeFontSizeOverride("font_size", 14); hud.AddChild(hints);
        var bar = new CenterContainer
        {
            Name = "CommandBar",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -112, OffsetBottom = -14,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hud.AddChild(bar);
        var barPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        barPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        bar.AddChild(barPanel);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        barPanel.AddChild(stack);
        _rowModes = new HBoxContainer { Name = "BuildBar", Visible = false };
        _rowModes.AddThemeConstantOverride("separation", 8);
        stack.AddChild(_rowModes);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        stack.AddChild(row);

        _confirmButton = MakeButton("ConfirmButton", "确认整平", DoConfirm, accent: true);
        _cancelButton = MakeButton("CancelButton", "取消任务", () => _world.QueuePlayerAction("cancel"));
        _pauseButton = MakeButton("PauseButton", "暂停", () => _world.QueuePlayerAction("pause"));
        _saveButton = MakeButton("SaveButton", "保存", () => _world.QueuePlayerAction("save"));
        _loadButton = MakeButton("LoadButton", "读取", () => _world.QueuePlayerAction("load"));
        _recoverButton = MakeButton("RecoverButton", "故障恢复", () => _world.QueuePlayerAction("recover"));
        _connectButton = MakeButton("ConnectButton", "连接电缆", () => _world.QueuePlayerAction("connect"));
        _retryButton = MakeButton("RetryButton", "重试工程/保障", () => _world.QueuePlayerAction("retry"));
        row.AddChild(_confirmButton);
        row.AddChild(_cancelButton);
        row.AddChild(_pauseButton);
        row.AddChild(_saveButton);
        row.AddChild(_loadButton);
        row.AddChild(_recoverButton);
        row.AddChild(_connectButton);
        row.AddChild(_retryButton);
    }

    private static Label InfoLabel(string name, int size, Color color)
    {
        var label = NewLabel("", size, color);
        label.Name = name;
        label.CustomMinimumSize = new Vector2(258, 0);
        label.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
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
        if (BootstrapOn && _buildType is { } type)
            _world.QueueBuild(type, preview.Center, YawRadians(), preview.Version);
        else
            _world.QueueLevel(preview.Center, preview.Version, SelectedWorkerId());
    }

    private void UpdateHud(PlayerReadModel state)
    {
        bool boot = BootstrapOn;
        if (boot) EnsureBootstrapControls(_bootstrap!);
        _titleLabel.Text = boot ? "余电 · 基地建设" : "余电 · 整平作业";
        _rowModes.Visible = boot;
        _connectButton.Visible = boot;
        _retryButton.Visible = boot;
        _stockLabel.Visible = boot;
        _facilityLabel.Visible = boot;
        _supportLabel.Visible = false;
        _confirmButton.Text = Building ? "确认建设" : "确认整平";

        _statusLabel.Text = state.Notice;
        _jobLabel.Text = state.Job is not { } job
            ? "当前没有任务；" + (Building
                ? "选择建设类型后点击地面放置，确认后自动派驮运与筑垒"
                : "点击地面选择整平位置，确认后筑垒自动前往")
            : string.Format(Inv,
                "任务 {0} · {1} · 执行者 {2} · 进度 {3:P0}\n中心 ({4:F1}, {5:F1}){6}",
                job.Id, StageDisplay(job.Stage), WorkerDisplay(job.WorkerId), job.Progress,
                job.Center.X, job.Center.Z, job.Active ? "" : " · 非活动");

        _selectionLabel.Text = _selectedSite is { } site
            ? string.Format(Inv, "已选位置 ({0:F1}, {1:F1}) · {2}\n{3}", site.X, site.Z, _preview?.Reason ?? "预览中…", ModeHint())
            : _selectedRobotId is { } id && Array.Find(_robots, r => r.Id == id) is { } robot
                ? $"已选 {robot.Name} · " + (SelectedWorkerId() != null && !Building ? "再点地面指定整平位置" : Building ? "建设由系统自动派工" : "本批仅筑垒执行整平")
                : "左键点选机器人或地面；右键取消选择";

        _worldLabel.Text = string.Format(Inv,
            "地表 v{0} · {1:F0}s · 存档 {2} · {3}{4}",
            state.Version, state.TimeSeconds, state.SaveExists ? "已有" : "无",
            state.Paused ? "已暂停" : "运行中", state.Ready ? "" : " · 世界恢复中");

        if (_bootstrap is { } b && boot)
        {
            _stockLabel.Text = "库存 " + b.Stock + "\n" + b.Power;
            _facilityLabel.Text = b.Facilities.Length == 0 ? "暂无设施" :
                string.Join("\n", b.Facilities.Select(f =>
                    $"{f.Name} · {(f.Built ? "已建成" : "建设中")}{(f.Powered ? " · 供电" : "")}" +
                    (f.Source is { } source ? " · 电缆←" + FacilityName(b, source) : "")));
            if (_selectedRobotId is { } robotId && Array.Find(b.Robots, r => r.Id == robotId) is { } support)
            {
                _supportLabel.Visible = true;
                _supportLabel.Text = $"电量 {support.Energy:0.#}/{support.Capacity:0} · 耐久 {support.Durability:0.#}/{support.Capacity:0} · 载货 {support.Cargo}" +
                    "\n" + support.State + (support.Reason.Length > 0 ? "：" + support.Reason : "");
            }
        }

        bool jobActive = state.Job is { Active: true };
        _confirmButton.Disabled = !(_preview is { Legal: true } && !jobActive && state.Ready) || _commandCooldown > 0f;
        _cancelButton.Disabled = !jobActive;
        _pauseButton.Text = state.Paused ? "继续" : "暂停";
        _saveButton.Disabled = !state.Ready;
        _loadButton.Disabled = !state.Ready || !state.SaveExists;
    }

    private string ModeHint()
        => Building
            ? $"成本 {_currentBlueprint?.Cost ?? "…"} · 朝向 {_yawSteps * 90}° · 自动派驮运与筑垒"
            : SelectedWorkerId() is { } selectedWorker ? WorkerDisplay(selectedWorker) : "自动分配筑垒";

    private static string FacilityName(BootstrapReadModel boot, string id)
        => Array.Find(boot.Facilities, f => f.Id == id) is { } facility ? facility.Name : id;

    private string WorkerDisplay(string? workerId)
        => workerId == null ? "未分配"
            : Array.Find(_robots, r => r.Id == workerId) is { } robot ? robot.Name : workerId;

    private static string StageDisplay(string stage) => stage switch
    {
        "Travelling" => "前往工位", "Working" => "整平中", "WaitingForSpace" => "等待场地腾空",
        "AwaitingPhysics" => "确认地表", "Completed" => "已完成", "Cancelled" => "已取消",
        "Failed" => "无法继续", _ => stage
    };

    private void BuildMarkers()
    {
        _markers = new Node3D { Name = "Markers" }; AddChild(_markers);
        _markerFactory = ResourceLoader.Load<GDScript>("res://assets/d1-art/markers.gd").New().AsGodotObject();
        _previewMarker = new Marker(); _siteMarker = new Marker(); _jobMarker = new Marker(); _selectMarker = new Marker();
    }
    private void UpdateMarkers(PlayerReadModel state)
    {
        // 预览半径按权威返回的 Radius；蓝图未出预览前用蓝图半径兜底。
        float radius = _preview?.Radius ?? (Building ? _buildRadius : SiteRadiusM);
        PlaceMarker(_previewMarker, _preview?.Center, _preview is { Legal: true } ? "legal" : "illegal", radius, state);
        PlaceMarker(_siteMarker, _preview == null ? _selectedSite : null, "hover", radius, state);
        PlaceMarker(_jobMarker, state.Job?.Center, _world.PlayerJobApplied ? "committed" : "hover", SiteRadiusM, state);
        var selected = Array.Find(_robots, r => r.Id == _selectedRobotId);
        PlaceMarker(_selectMarker, selected?.Position, "selected", (selected?.Radius ?? 1f) + .35f, state);
    }
    private void PlaceMarker(Marker marker, Vector3? center, string kind, float radius, PlayerReadModel state)
    {
        if (!state.Ready) center = null;
        if (marker.Center == center && marker.Kind == kind && marker.Radius == radius && marker.Version == state.Version) return;
        if (marker.Node != null) { _markers.RemoveChild(marker.Node); marker.Node.QueueFree(); marker.Node = null; }
        marker.Center = center; marker.Kind = kind; marker.Radius = radius; marker.Version = state.Version;
        if (center is not { } p) return;
        marker.Node = _markerFactory.Call("build", new Vector2(p.X, p.Z), radius, kind,
            Callable.From<float, float, float>(_world.SamplePlayerGround)).AsGodotObject() as MeshInstance3D;
        if (marker.Node != null) _markers.AddChild(marker.Node);
    }
    public override void _ExitTree() => _markerFactory?.Dispose();
}
