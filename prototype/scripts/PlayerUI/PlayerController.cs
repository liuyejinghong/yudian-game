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
    private Basis _initialBasis = Basis.Identity;
    private Vector3 _initialFocus = Vector3.Zero;
    private float _initialDistance = 26f;
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
        _worldLabel = null!, _stockLabel = null!, _supportLabel = null!, _facilityLabel = null!,
        _devTitleLabel = null!, _devBodyLabel = null!, _devDirectionsLabel = null!;
    private ScrollContainer _infoScroll = null!;
    private Button _confirmButton = null!, _cancelButton = null!, _pauseButton = null!,
        _saveButton = null!, _saveQuitButton = null!, _loadButton = null!, _recoverButton = null!,
        _connectButton = null!, _retryButton = null!, _restockButton = null!, _legacyButton = null!;
    private HBoxContainer _rowModes = null!, _rowOps = null!;
    private Button? _rotateButton;
    private ButtonGroup _modeGroup = null!;

    // 启动入口：EntryOpen/EntryLoading 由 Main 权威给出，UI 只做遮罩、按钮与禁透传。
    private Control _entryOverlay = null!, _infoPanel = null!, _commandBar = null!, _hintsLabel = null!;
    private Label _entryNotice = null!;
    private Button _entryNewGameButton = null!, _entryLoadButton = null!, _entryQuitButton = null!;
    private bool _entryFocusTaken;
    private Button _devDirectionsToggle = null!, _facilityToggle = null!;

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
        _initialBasis = _cameraBasis;
        _initialFocus = _focus;
        _initialDistance = _distance;
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        if (!_initialized) return;
        float dt = (float)delta;
        _commandCooldown = MathF.Max(0f, _commandCooldown - dt);
        var state = _world.ReadPlayerState();
        // UpdateEntryHud 必须每帧调用：入口关闭后的第一帧要靠它隐藏遮罩、恢复运行中 HUD。
        UpdateEntryHud(state);
        if (state.EntryOpen || state.EntryLoading) return; // 入口期间权威冻结世界交互
        _entryFocusTaken = false;
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
        var state = _world.ReadPlayerState();
        if (state.EntryOpen || state.EntryLoading) return; // 入口期间世界不接收任何输入
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
            case InputEventMagnifyGesture magnify when magnify.Factor > 0f && float.IsFinite(magnify.Factor):
                _distance = Mathf.Clamp(_distance / magnify.Factor, MinDistanceM, MaxDistanceM);
                break;
            case InputEventPanGesture pan when float.IsFinite(pan.Delta.X) && float.IsFinite(pan.Delta.Y):
                var axes = FlatAxes();
                _focus += (axes.Forward * pan.Delta.Y - axes.Right * pan.Delta.X) * (_distance * 0.004f);
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

    // 水平视轴供键盘、手势和按钮共用。
    private (Vector3 Forward, Vector3 Right) FlatAxes()
    {
        var forward = -_cameraBasis.Z;
        forward.Y = 0f;
        var right = _cameraBasis.X;
        right.Y = 0f;
        return (forward.Normalized(), right.Normalized());
    }

    private void UpdateCamera(float focusLimit, float dt)
    {
        float rotation = (Input.IsKeyPressed(Key.Q) ? 1 : 0) - (Input.IsKeyPressed(Key.E) ? 1 : 0);
        _cameraBasis = _cameraBasis.Rotated(Vector3.Up, rotation * dt).Orthonormalized();
        var axes = FlatAxes();

        float axis = Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1f :
            Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? -1f : 0f;
        float side = Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1f :
            Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? -1f : 0f;
        float speed = _distance * 0.9f * dt;
        _focus += (axes.Forward * axis + axes.Right * side) * speed;

        _focus = new Vector3(Mathf.Clamp(_focus.X, -focusLimit, focusLimit), 0,
            Mathf.Clamp(_focus.Z, -focusLimit, focusLimit));
        _camera.GlobalTransform = new Transform3D(_cameraBasis, _focus + _cameraBasis.Z * _distance);
    }

    // ---- 镜头按钮 ----
    private void PanCamera(float side, float forward)
    {
        var axes = FlatAxes();
        _focus += (axes.Forward * forward + axes.Right * side) * (_distance * 0.3f);
    }
    private void ZoomCamera(float scale) => _distance = Mathf.Clamp(_distance * scale, MinDistanceM, MaxDistanceM);
    private void RotateCamera(float radians) => _cameraBasis = _cameraBasis.Rotated(Vector3.Up, radians).Orthonormalized();
    private void ResetCamera()
    {
        _cameraBasis = _initialBasis;
        _focus = _initialFocus;
        _distance = _initialDistance;
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
        _infoPanel = panel;

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
        _devTitleLabel = InfoLabel("DevTitleLabel", 14, Palette.Accent);
        _devBodyLabel = InfoLabel("DevBodyLabel", 14, Palette.TextDim);
        _devDirectionsLabel = InfoLabel("DevDirectionsLabel", 14, Palette.TextDim);
        // 优先级：当前动作/目标/下一步 → 库存电力/保障/选机；设施与两方向长说明折叠可查。
        info.AddChild(_statusLabel);
        info.AddChild(_jobLabel);
        info.AddChild(_devTitleLabel);
        info.AddChild(_devBodyLabel);
        info.AddChild(_stockLabel);
        info.AddChild(_supportLabel);
        info.AddChild(_selectionLabel);
        info.AddChild(_worldLabel);
        _devDirectionsToggle = FoldToggle("DevDirectionsToggle", "方向详情", _devDirectionsLabel);
        info.AddChild(_devDirectionsToggle);
        info.AddChild(_devDirectionsLabel);
        _facilityToggle = FoldToggle("FacilityToggle", "设施", _facilityLabel);
        info.AddChild(_facilityToggle);
        info.AddChild(_facilityLabel);

        var hints = new Label
        {
            Name = "Hints",
            Text = "按住 WASD／方向键平移 · 右键拖动或双指平移 · 滚轮／捏合缩放 · 按住 Q/E 旋转 · F 定位 · Esc 清除选区",
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -212, OffsetBottom = -190,
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        hints.AddThemeFontSizeOverride("font_size", 14); hud.AddChild(hints);
        _hintsLabel = hints;
        var bar = new CenterContainer
        {
            Name = "CommandBar",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -186, OffsetBottom = -14,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hud.AddChild(bar);
        _commandBar = bar;
        var barPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        barPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        bar.AddChild(barPanel);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        barPanel.AddChild(stack);
        _rowModes = new HBoxContainer { Name = "BuildBar", Visible = false };
        _rowModes.AddThemeConstantOverride("separation", 8);
        stack.AddChild(_rowModes);
        var camRow = new HBoxContainer { Name = "CameraRow" };
        camRow.AddThemeConstantOverride("separation", 6);
        stack.AddChild(camRow);
        AddCamButton(camRow, "CameraForwardButton", "前", () => PanCamera(0f, 1f));
        AddCamButton(camRow, "CameraBackButton", "后", () => PanCamera(0f, -1f));
        AddCamButton(camRow, "CameraLeftButton", "左", () => PanCamera(-1f, 0f));
        AddCamButton(camRow, "CameraRightButton", "右", () => PanCamera(1f, 0f));
        AddCamButton(camRow, "CameraZoomInButton", "拉近", () => ZoomCamera(0.8f));
        AddCamButton(camRow, "CameraZoomOutButton", "拉远", () => ZoomCamera(1.25f));
        AddCamButton(camRow, "CameraRotateLeftButton", "左转", () => RotateCamera(-Mathf.Pi / 12f));
        AddCamButton(camRow, "CameraRotateRightButton", "右转", () => RotateCamera(Mathf.Pi / 12f));
        AddCamButton(camRow, "CameraResetButton", "复位", ResetCamera);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        stack.AddChild(row);

        // 第四行经营按钮：与建设命令分开，1280 宽底栏放得下全部入口。
        _rowOps = new HBoxContainer { Name = "OpsBar" };
        _rowOps.AddThemeConstantOverride("separation", 10);
        stack.AddChild(_rowOps);

        _confirmButton = MakeButton("ConfirmButton", "确认整平", DoConfirm, accent: true);
        // 固定宽度：整平/建设两态文案长度不同，避免按钮文字切换时整行移动。
        _confirmButton.CustomMinimumSize = new Vector2(176, 0);
        _cancelButton = MakeButton("CancelButton", "取消任务", () => _world.QueuePlayerAction("cancel"));
        _pauseButton = MakeButton("PauseButton", "暂停", () => _world.QueuePlayerAction("pause"));
        _saveButton = MakeButton("SaveButton", "保存", () => _world.QueuePlayerAction("save"));
        _saveQuitButton = MakeButton("SaveQuitButton", "保存退出", () => _world.QueuePlayerAction("savequit"));
        _loadButton = MakeButton("LoadButton", "读取", () => _world.QueuePlayerAction("load"));
        _recoverButton = MakeButton("RecoverButton", "故障恢复", () => _world.QueuePlayerAction("recover"));
        _connectButton = MakeButton("ConnectButton", "连接电缆", () => _world.QueuePlayerAction("connect"));
        _retryButton = MakeButton("RetryButton", "重试工程/保障", () => _world.QueuePlayerAction("retry"));
        _restockButton = MakeButton("RestockButton", "补维修耗材", () => _world.QueuePlayerAction("restock"));
        _legacyButton = MakeButton("LegacyLoadButton", "读取旧档", () => _world.QueuePlayerAction("legacy"));
        row.AddChild(_confirmButton);
        row.AddChild(_cancelButton);
        row.AddChild(_pauseButton);
        row.AddChild(_saveButton);
        row.AddChild(_saveQuitButton);
        row.AddChild(_loadButton);
        row.AddChild(_recoverButton);
        row.AddChild(_connectButton);
        row.AddChild(_retryButton);
        _rowOps.AddChild(_restockButton);
        _rowOps.AddChild(_legacyButton);

        BuildEntryOverlay(hud);
    }

    // 启动薄入口：标题、一句先建保障提示、Notice、新游戏/继续存档/退出；退出是普通关闭，不保存新局。
    private void BuildEntryOverlay(CanvasLayer hud)
    {
        var overlay = new ColorRect
        {
            Name = "EntryOverlay",
            Color = new Color(0.13f, 0.11f, 0.09f, 0.92f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(overlay);
        _entryOverlay = overlay;

        var center = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        var entryPanel = new PanelContainer { Name = "EntryPanel" };
        entryPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        center.AddChild(entryPanel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        box.CustomMinimumSize = new Vector2(300, 0);
        entryPanel.AddChild(box);

        var title = NewLabel("余电", 30, Palette.Accent);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        var hint = NewLabel("先建保障（太阳能、充电、维修），再发展生产。", 15, Palette.Text);
        hint.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(hint);
        _entryNotice = NewLabel("", 14, Palette.TextDim);
        _entryNotice.Name = "EntryNotice";
        _entryNotice.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _entryNotice.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(_entryNotice);

        _entryNewGameButton = EntryButton("EntryNewGameButton", "开始新游戏", () => _world.QueuePlayerAction("newgame"));
        _entryLoadButton = EntryButton("EntryLoadButton", "继续存档", () => _world.QueuePlayerAction("load"));
        _entryQuitButton = EntryButton("EntryQuitButton", "退出", () => GetTree().Quit());
        box.AddChild(_entryNewGameButton);
        box.AddChild(_entryLoadButton);
        box.AddChild(_entryQuitButton);
    }

    private Button EntryButton(string name, string text, Action command)
    {
        var button = MakeButton(name, text, command, accent: true);
        button.FocusMode = Control.FocusModeEnum.All; // 入口必须可纯键盘操作
        button.CustomMinimumSize = new Vector2(240, 40);
        return button;
    }

    // 折叠开关：文字等宽（▸/▾ 同宽），展开状态不移动周围布局。
    private Button FoldToggle(string name, string text, Control target)
    {
        var button = new Button
        {
            Name = name,
            Text = text + " ▸",
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(96, 24),
        };
        button.Toggled += on =>
        {
            target.Visible = on;
            button.Text = text + (on ? " ▾" : " ▸");
        };
        target.Visible = false;
        return button;
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

    // 工程命令防双击；镜头操作不占用命令冷却。
    private Button MakeButton(string name, string text, Action command, bool accent = false, bool commandCooldown = true)
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
            if (commandCooldown)
            {
                if (_commandCooldown > 0f) return;
                _commandCooldown = CommandCooldownS;
            }
            command();
        };
        return button;
    }

    // 镜头按钮：不参与工程冷却，并保留键盘焦点（可访问性）。
    private void AddCamButton(HBoxContainer row, string name, string text, Action command)
    {
        var button = MakeButton(name, text, command, commandCooldown: false);
        button.FocusMode = Control.FocusModeEnum.All;
        row.AddChild(button);
    }

    private void DoConfirm()
    {
        if (_preview is not { Legal: true } preview) return;
        if (BootstrapOn && _buildType is { } type)
            _world.QueueBuild(type, preview.Center, YawRadians(), preview.Version);
        else
            _world.QueueLevel(preview.Center, preview.Version, SelectedWorkerId());
    }

    // 入口遮罩：显示/按钮态全部来自 EntryOpen/EntryLoading/Ready/SaveExists/Notice，UI 不自行判断载入结果。
    private void UpdateEntryHud(PlayerReadModel state)
    {
        bool entry = state.EntryOpen || state.EntryLoading;
        _entryOverlay.Visible = entry;
        _infoPanel.Visible = !entry;
        _commandBar.Visible = !entry;
        _hintsLabel.Visible = !entry;
        if (!entry) return;
        if (!_entryFocusTaken && state.Ready)
        {
            _entryFocusTaken = true;
            _entryNewGameButton.GrabFocus(); // 键盘用户直接可回车开始
        }
        _entryNotice.Text = state.Notice;
        _entryNotice.Visible = state.Notice.Length > 0;
        _entryNewGameButton.Disabled = state.EntryLoading || !state.Ready;
        _entryLoadButton.Disabled = state.EntryLoading || !state.Ready || !state.SaveExists;
        _entryQuitButton.Disabled = state.EntryLoading;
        _entryLoadButton.Text = state.EntryLoading ? "载入中…" : "继续存档";
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
        // 设施列表收在折叠开关后：开关随 bootstrap 显隐，展开状态由开关自己管理。
        _facilityToggle.Visible = boot;
        if (!boot) _facilityLabel.Visible = false;
        _supportLabel.Visible = false;
        _confirmButton.Text = Building ? "建设·含前置授权" : "确认整平";

        _statusLabel.Text = state.Notice;
        // 任务行不露内部 goal/build ID；运输等 0 进度阶段不显示"进度 0%"冒充总进度。
        _jobLabel.Text = state.Job is not { } job
            ? "当前没有任务；" + (Building
                ? "选择建设类型后点击地面放置，确认后自动派驮运与筑垒"
                : "点击地面选择整平位置，确认后筑垒自动前往")
            : string.Format(Inv, "{0} · 执行者 {1}{2}\n中心 ({3:F1}, {4:F1}){5}",
                StageDisplay(job.Stage), WorkerDisplay(job.WorkerId),
                job.Progress > 0 ? string.Format(Inv, " · 进度 {0:P0}", job.Progress) : "",
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

        // 发展只读面板：仅展示权威 DevelopmentReadModel，不在 UI 侧计算成本、缺口或产物。
        // 现货/在途/已备/在制与两方向字段按有值显示；方向长说明折叠。
        var dev = _world.ReadDevelopment();
        bool devOn = dev.Enabled;
        _devTitleLabel.Visible = devOn;
        _devBodyLabel.Visible = devOn;
        _devDirectionsToggle.Visible = devOn;
        _rowOps.Visible = devOn;
        _legacyButton.Disabled = !state.Ready;
        if (devOn)
        {
            _devTitleLabel.Text = "发展 · " + dev.Provider;
            var lines = new[]
            {
                dev.Goal.Length > 0 ? "目标 " + dev.Goal : "",
                dev.Stage.Length > 0 ? "当前 " + dev.Stage : "",
                dev.Reason.Length > 0 ? "下一步 " + dev.Reason : "",
                dev.Need.Length > 0 ? "净缺口 " + dev.Need : "",
                dev.Supply.Length > 0 ? "供给 " + dev.Supply : "",
                dev.Prepared.Length > 0 ? "已备 " + dev.Prepared : "",
                dev.Transit.Length > 0 ? "在途 " + dev.Transit : "",
                dev.InProcess.Length > 0 ? "在制 " + dev.InProcess : "",
                // 旧 Directions 为空时才回落到既有 Choices 文本。
                dev.Directions is { Length: > 0 } || dev.Choices.Length == 0 ? "" : "后续方向 " + dev.Choices,
                dev.Mines.Length > 0 ? "矿点 " + dev.Mines : "",
            }.Where(s => s.Length > 0);
            _devBodyLabel.Text = string.Join("\n", lines);
            var dirs = dev.Directions;
            if (dirs is { Length: > 0 })
                _devDirectionsLabel.Text = string.Join("\n\n", dirs.Select(d =>
                    $"{d.Name} · {(d.Feasible ? "可行" : "暂不可行")}\n成本 {d.Cost}\n缺口 {d.Need}\n后果 {d.Consequence}" +
                    (d.Reason.Length > 0 ? "\n" + d.Reason : "")));
            else
                _devDirectionsLabel.Text = "";
            _devDirectionsToggle.Visible = dirs is { Length: > 0 };
            if (!_devDirectionsToggle.Visible) _devDirectionsLabel.Visible = false;
        }

        bool jobActive = state.Job is { Active: true };
        _confirmButton.Disabled = !(_preview is { Legal: true } && !jobActive && state.Ready) || _commandCooldown > 0f;
        _cancelButton.Disabled = !jobActive;
        _pauseButton.Text = state.Paused ? "继续" : "暂停";
        _saveButton.Disabled = !state.Ready;
        _saveQuitButton.Disabled = !state.Ready;
        _loadButton.Disabled = !state.Ready || !state.SaveExists;
    }

    private string ModeHint()
        => Building
            ? $"成本 {_currentBlueprint?.Cost ?? "…"} · 朝向 {_yawSteps * 90}° · 确认即一次授权：缺料时自动采集/运输/加工并完成该建设，无需逐配方手点"
            : SelectedWorkerId() is { } selectedWorker ? WorkerDisplay(selectedWorker) : "自动分配筑垒";

    private static string FacilityName(BootstrapReadModel boot, string id)
        => Array.Find(boot.Facilities, f => f.Id == id) is { } facility ? facility.Name : id;

    private string WorkerDisplay(string? workerId)
        => string.IsNullOrEmpty(workerId) ? "未分配" // 发展/加工任务可能没有执行者字符串
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
