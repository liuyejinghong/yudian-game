#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Yudian.PlayerUI;

// 玩家输入只排队意图；世界、教学与存档事实全部由 Main 权威持有。
// 本控制器不读写存档、不反解析文案计算规则、不拥有教学达成、不操作全局 SceneTree 暂停。
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
        public static readonly Color Warn = Color.FromHtml("a8341f");
        public static readonly Color Ok = Color.FromHtml("2f7d3b");
        public static readonly Color Scrim = new(0.13f, 0.11f, 0.09f, 0.94f);
        public static readonly Color ScrimLight = new(0.10f, 0.09f, 0.08f, 0.66f);
        public static readonly Color Hint = Color.FromHtml("d9822b");
    }

    private const float SiteRadiusM = 2f;
    private const float CommandCooldownS = 0.35f;
    private const float PreviewIntervalS = 0.15f;
    private const float PlanIntervalS = 0.15f;
    private const float EconomyIntervalS = 0.25f;
    private const float ConnectIntervalS = 0.3f;
    private const float MinDistanceM = 7f, MaxDistanceM = 70f;
    private const float ClickSlackPx = 8f;
    private const double SlotEchoSeconds = 0.6;
    private const double SettingsEchoWaitS = 0.75; // 提交后等权威回显的窗口；超窗未回显按失败回旧值
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // 字号角色：普通常驻正文 18，目标标题 24；全部经 UI 缩放后应用。
    private const int SizeTitle = 44, SizeHeading = 24, SizeBody = 18, SizeSmall = 16, SizeTag = 14;

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
    private readonly Dictionary<string, Button> _modeButtons = [];

    // 权威生命周期（页面/菜单/忙状态）。
    private PlayerLifecycleView? _lifecycle;
    private string _page = "";
    private float _pageGate; // 回到 game 的短门禁：吞掉关闭页面的第二击，不选中世界
    private long _lastEpoch = long.MinValue;

    // 设置：从 ReadSettings 读真实值，经 QueueSettings 写；UI/镜头按该值实际生效。
    private PlayerSettingsView? _settingsView;
    private double _uiScale = 1;
    private float _camSpeedMul = 1f;
    private bool _helpDetailed = true;
    private long _appliedSettingsRevision = long.MinValue;
    private double _appliedScale = -1;
    private bool _settingsDirty;    // 设置页有未回显的本地草案（只显示等待，不预应用）
    private double _settingsQueuedAt = -1;
    private double _draftScale = 1;
    private double _draftSpeed = 1;
    private string _draftHelp = "detailed";

    // 教学/经济/建造方案/接线：全部为只读 DTO 缓存。
    private TutorialView? _tutorial;
    private readonly HashSet<string> _learnedOps = [];
    private readonly List<(string Key, string Action, string? ObjectId)> _pendingLearn = [];
    private string _learnedStage = "";
    private PlayerEconomyView? _economy;
    private float _economyTimer;
    private BuildPlanView? _plan;
    private float _planTimer;
    private ConnectionOptionView[] _connectOptions = [];
    private float _connectTimer;
    private bool _connectPanelOpen;
    private string? _connectFacilityId;
    private string? _connectSourceId;
    private readonly List<(ConnectionOptionView Option, Button Button)> _connectRows = [];
    private long _connectOptionsEpoch = long.MinValue;

    // 存档页/新游戏页的本地选择（乐观高亮；权威 SelectedSlot 回显后采用）。
    private int _slotSelection = 1;
    private double _slotTouchedAt = -1;

    // 缩放需要重贴的控件登记。
    private readonly List<(Control Control, int BaseSize)> _sizeTargets = [];
    private readonly List<(Control Control, Vector2 BaseSize)> _minSizeTargets = [];

    private Node3D _markers = null!;
    private GodotObject _markerFactory = null!;
    private Marker _previewMarker = null!, _siteMarker = null!, _jobMarker = null!, _selectMarker = null!;

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
        var lifecycle = _world.ReadLifecycle();
        ObserveLifecycle(lifecycle, dt);
        ReadSettingsAndApply();

        bool inGame = _page == "game";
        _hudRoot.Visible = inGame; // 菜单/主页期间隐藏局内 HUD
        if (inGame)
        {
            FlushPendingLearning(lifecycle);
            _robots = _world.ReadPlayerRobots();
            _bootstrap = _world.ReadBootstrap();
            UpdateCamera(state.ExtentM + 4f, dt);
            UpdateAimAndPreview(state, dt);
        }
        else
        {
            _preview = null;
            _plan = null;
        }

        RefreshScreens(lifecycle, state);
        if (inGame) RefreshGameHud(state, dt);
        UpdateMarkers(state);
    }

    // 生命周期观察：epoch 变化清选择/草案/输入状态；页面变化时切换输入归属与初始焦点。
    private void ObserveLifecycle(PlayerLifecycleView lifecycle, float dt)
    {
        if (_lifecycle is { } previous && (previous.Page != lifecycle.Page || previous.Busy != lifecycle.Busy))
            ClearInputTracking();
        _lifecycle = lifecycle;
        _pageGate = MathF.Max(0f, _pageGate - dt);
        if (lifecycle.WorldEpoch != _lastEpoch)
        {
            bool first = _lastEpoch == long.MinValue;
            _lastEpoch = lifecycle.WorldEpoch;
            _connectOptionsEpoch = long.MinValue;
            if (!first)
            {
                ClearSelection();
                _plan = null;
                _learnedOps.Clear();
                _pendingLearn.Clear(); // 上一个世界的未确认操作不跨世界发送
                _learnedStage = "";
                _settingsDirty = false; // 未回显的设置草案不跨世界显示，回到权威实际值
                _settingsQueuedAt = -1;
                _panGrab = null;
                _leftTracking = false;
                _rightDragged = false;
                _connectPanelOpen = false;
                _pageGate = CommandCooldownS;
            }
        }
        if (lifecycle.Page != _page)
        {
            _page = lifecycle.Page;
            if (System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1")
                GD.Print($"PLAYER_GUI_PAGE page={_page} epoch={lifecycle.WorldEpoch} slot={lifecycle.SelectedSlot}");
            if (_page == "game" && _pageGate <= 0f) _pageGate = CommandCooldownS;
            if (_page is "slots" or "newgame") SyncSlotSelection(lifecycle.SelectedSlot, force: true);
            if (_page == "settings") _settingsDirty = false;
        }
        SyncSlotSelection(lifecycle.SelectedSlot, force: false);
    }

    private void SyncSlotSelection(int authoritative, bool force)
    {
        if (authoritative is < 1 or > 3) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (!force && _slotTouchedAt >= 0 && now - _slotTouchedAt < SlotEchoSeconds) return;
        _slotSelection = authoritative;
    }

    // ---- 输入 ----

    private void ClearInputTracking()
    {
        _panGrab = null;
        _leftTracking = false;
        _rightDragged = false;
    }

    public override void _Input(InputEvent @event)
    {
        // 释放可能被 HUD 或菜单截住；在 unhandled 分派前清拖动起点，保留正常右键点击判定。
        if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right or MouseButton.Middle })
            _panGrab = null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_initialized) return;
        var lifecycle = _lifecycle ?? _world.ReadLifecycle();
        bool inGame = lifecycle.Page == "game";
        var state = _world.ReadPlayerState();
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click && System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1")
            GD.Print($"PLAYER_GUI_CLICK double={click.DoubleClick} page={lifecycle.Page} gate={_pageGate.ToString(Inv)}");
        // Esc：顶层返回。接线面板先关闭；页面按各自 Back 语义；局内打开暂停菜单（不再是旧"只清除选区"）。
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            if (_connectPanelOpen) { CloseConnectPanel(); return; }
            if (!inGame) { QueueLifecycle(EscapeActionFor(lifecycle.Page)); return; }
            QueueLifecycle("pause");
            return;
        }
        // 菜单/确认/恢复期间不把鼠标键盘透传为世界命令；页面输入归属由权威 ReadLifecycle 判定。
        if (!inGame || lifecycle.Busy) return;
        if (_pageGate > 0f) return;
        switch (@event)
        {
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.F:
                var target = _selectedSite ?? Array.Find(_robots, r => r.Id == _selectedRobotId)?.Position ?? state.Job?.Center;
                if (target is { } p) _focus = new Vector3(p.X, 0, p.Z);
                break;
            case InputEventMouseMotion motion:
                _mousePos = motion.Position; _aimCleared = false;
                if (_panGrab is { } grab && MousePlanePoint(grab) is { } grabPoint && MousePlanePoint(motion.Position) is { } now)
                {
                    var delta = grabPoint - now;
                    if (delta.LengthSquared() > 1e-8f) NoteCameraOperation();
                    _focus += delta;
                    _panGrab = motion.Position;
                    if (motion.Relative.LengthSquared() > 0.25f) _rightDragged = true;
                }
                break;
            case InputEventMouseButton button:
                HandleMouseButton(button);
                break;
            case InputEventMagnifyGesture magnify when magnify.Factor > 0f && float.IsFinite(magnify.Factor):
                _distance = Mathf.Clamp(_distance / ZoomSteps((float)magnify.Factor), MinDistanceM, MaxDistanceM);
                NoteCameraOperation();
                break;
            case InputEventPanGesture pan when float.IsFinite(pan.Delta.X) && float.IsFinite(pan.Delta.Y):
                var axes = FlatAxes();
                _focus += (axes.Forward * pan.Delta.Y - axes.Right * pan.Delta.X) * (_distance * 0.004f * _camSpeedMul);
                NoteCameraOperation();
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
                    _leftTracking = !button.DoubleClick;
                    break;
                case MouseButton.Right:
                    _panGrab = button.Position;
                    _rightDragged = false;
                    break;
                case MouseButton.Middle:
                    _panGrab = button.Position;
                    break;
                case MouseButton.WheelUp:
                    _distance = Mathf.Clamp(_distance * ZoomSteps(0.88f), MinDistanceM, MaxDistanceM);
                    NoteCameraOperation();
                    break;
                case MouseButton.WheelDown:
                    _distance = Mathf.Clamp(_distance / ZoomSteps(0.88f), MinDistanceM, MaxDistanceM);
                    NoteCameraOperation();
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
            _plan = null;
            ReportLearning("select-robot", robot); // 只记录真实发生的选中操作
            return;
        }
        if (GroundHit(screenPos, out var point))
        {
            _selectedSite = point;
            _preview = null;
            _plan = null;
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
        _plan = null;
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

    private float ZoomSteps(float baseFactor) => Mathf.Pow(baseFactor, _camSpeedMul);

    private void UpdateCamera(float focusLimit, float dt)
    {
        float rotation = (Input.IsKeyPressed(Key.Q) ? 1 : 0) - (Input.IsKeyPressed(Key.E) ? 1 : 0);
        _cameraBasis = _cameraBasis.Rotated(Vector3.Up, rotation * dt).Orthonormalized();
        var axes = FlatAxes();

        // 接线面板打开时方向键让位给列表焦点（WASD 仍可平移）。
        bool up = Input.IsKeyPressed(Key.W) || (!_connectPanelOpen && Input.IsKeyPressed(Key.Up));
        bool down = Input.IsKeyPressed(Key.S) || (!_connectPanelOpen && Input.IsKeyPressed(Key.Down));
        bool right = Input.IsKeyPressed(Key.D) || (!_connectPanelOpen && Input.IsKeyPressed(Key.Right));
        bool left = Input.IsKeyPressed(Key.A) || (!_connectPanelOpen && Input.IsKeyPressed(Key.Left));
        float axis = up ? 1f : down ? -1f : 0f;
        float side = right ? 1f : left ? -1f : 0f;
        if (rotation != 0 || axis != 0 || side != 0) NoteCameraOperation();
        float speed = _distance * 0.9f * dt * _camSpeedMul;
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
        NoteCameraOperation();
    }
    private void ZoomCamera(float scale) { _distance = Mathf.Clamp(_distance * ZoomSteps(scale), MinDistanceM, MaxDistanceM); NoteCameraOperation(); }
    private void RotateCamera(float radians) { _cameraBasis = _cameraBasis.Rotated(Vector3.Up, radians).Orthonormalized(); NoteCameraOperation(); }
    private void ResetCamera()
    {
        _cameraBasis = _initialBasis;
        _focus = _initialFocus;
        _distance = _initialDistance;
        NoteCameraOperation();
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
        {
            var button = ModeToggle("ModeButton_" + blueprint.Id, blueprint.Name, blueprint);
            _rowModes.AddChild(button);
            _modeButtons[blueprint.Id] = button;
        }
        _rotateButton = MakeButton("RotateButton", "朝向 0°", RotateYaw);
        _rotateButton.FocusMode = Control.FocusModeEnum.All;
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
        StyleButton(button, accent: false);
        _sizeTargets.Add((button, SizeBody));
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
        _plan = null;
    }

    private void RotateYaw()
    {
        _yawSteps = (_yawSteps + 1) % 4;
        _rotateButton!.Text = "朝向 " + _yawSteps * 90 + "°";
        _preview = null;
        _plan = null;
    }

    // ---- 预览与建造方案 ----

    private void UpdateAimAndPreview(PlayerReadModel state, float dt)
    {
        bool jobActive = state.Job is { Active: true };
        // 活动工程期间仍允许建设模式继续瞄准（只读草案，不预约）；整平模式仍需先完成或取消。
        if (_selectedSite == null && !_aimCleared)
            _hoverPoint = MousePlanePoint(_mousePos);
        var aim = _selectedSite ?? _hoverPoint;
        if (aim is not { } target || (!Building && jobActive))
        {
            _preview = null;
            _plan = null;
            return;
        }
        _previewTimer -= dt;
        _planTimer -= dt;
        bool previewStale = _preview == null || _previewTimer <= 0f ||
            _preview.Center.X != target.X || _preview.Center.Z != target.Z || _preview.Version != state.Version;
        if (previewStale)
        {
            _preview = Building
                ? _world.PreviewBuild(_buildType!, target, YawRadians())
                : _world.PreviewLevel(target, SelectedWorkerId());
            _previewTimer = PreviewIntervalS;
            _plan = null; // 位置/版本变化后草案必须重读，不能拿旧版本号下达
        }
        if (Building)
        {
            bool planStale = _plan == null || _planTimer <= 0f ||
                _plan.Center.X != target.X || _plan.Center.Z != target.Z ||
                Math.Abs(_plan.Yaw - YawRadians()) > 1e-4f;
            if (planStale)
            {
                _plan = _world.ReadBuildPlan(_buildType!, target, YawRadians());
                _planTimer = PlanIntervalS;
            }
        }
        else _plan = null;
    }

    // ---- 排队意图（按动作键控防双击；单次只排队一个意图，权威再验一次） ----

    private readonly Dictionary<string, double> _actionGates = [];

    // 同一动作在冷却窗口内只排队一次；不同动作互不阻塞（如 Esc 开暂停后立即 Esc 返回）。
    // _commandCooldown 仍作为"最近一次命令"的展示窗，用于按钮禁用态显示。
    private bool GateAction(string key)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (_actionGates.TryGetValue(key, out double last) && now - last < CommandCooldownS) return false;
        _actionGates[key] = now;
        _commandCooldown = CommandCooldownS;
        return true;
    }

    private void QueueLifecycle(string action, int slot = 0, bool confirmed = false)
    {
        if (action.Length == 0 || !GateAction("lifecycle:" + action)) return;
        _world.QueueLifecycle(action, slot, confirmed);
    }

    private void QueueSettingsFromDraft()
    {
        // 只排队并显示"等待回显"；字号/镜头/详略一律以 ReadSettings 实际值为准，失败保旧值。
        _settingsDirty = true;
        _settingsQueuedAt = Time.GetTicksMsec() / 1000.0;
        _world.QueueSettings(_draftScale, _draftSpeed, _draftHelp);
    }

    private void ChangeCameraSpeed(double value)
    {
        _draftSpeed = Math.Clamp(value, 0.5, 2);
        QueueSettingsFromDraft();
    }

    private void ChangeScale(double value)
    {
        _draftScale = value;
        QueueSettingsFromDraft();
    }

    private void ChangeHelp(bool detailed)
    {
        _draftHelp = detailed ? "detailed" : "brief";
        QueueSettingsFromDraft();
    }

    // ---- 设置读取与应用 ----

    private void ReadSettingsAndApply()
    {
        var settings = _world.ReadSettings();
        _settingsView = settings;
        if (settings.Revision == _appliedSettingsRevision)
        {
            // 提交后未回显：命令可能失败/被拒；短窗口后放弃草案，显示权威旧值（失败保旧值）。
            if (_settingsDirty && Time.GetTicksMsec() / 1000.0 - _settingsQueuedAt > SettingsEchoWaitS)
            {
                _settingsDirty = false;
                _draftScale = settings.UiScale;
                _draftSpeed = settings.CameraSpeed;
                _draftHelp = settings.HelpDetail;
            }
            return;
        }
        _appliedSettingsRevision = settings.Revision;
        _settingsDirty = false;
        _draftScale = settings.UiScale;
        _draftSpeed = settings.CameraSpeed;
        _draftHelp = settings.HelpDetail;
        ApplySettings(settings.UiScale, (float)settings.CameraSpeed, settings.HelpDetail);
    }

    private void ApplySettings(double scale, float cameraSpeed, string helpDetail)
    {
        _uiScale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        _camSpeedMul = float.IsFinite(cameraSpeed) ? Mathf.Clamp(cameraSpeed, 0.5f, 2f) : 1f;
        _helpDetailed = helpDetail != "brief";
        if (!Mathf.IsEqualApprox((float)_uiScale, (float)_appliedScale)) ApplyUiScale();
    }

    private void ApplyUiScale()
    {
        _appliedScale = _uiScale;
        foreach (var (control, baseSize) in _sizeTargets)
            if (IsInstanceValid(control)) control.AddThemeFontSizeOverride("font_size", Math.Max(1, (int)Math.Round(baseSize * _uiScale, MidpointRounding.AwayFromZero)));
        foreach (var (control, baseSize) in _minSizeTargets)
            if (IsInstanceValid(control)) control.CustomMinimumSize = baseSize * (float)_uiScale;
    }

    // 自测接缝：只由 tools/player-ui-tests 的自测调用，默认玩家流程不经过这里。
    internal void ApplySettingsForTest(double uiScale, double cameraSpeed, string helpDetail)
        => ApplySettings(uiScale, (float)cameraSpeed, helpDetail);

    internal double UiScaleForTest => _uiScale;
    internal float CameraSpeedForTest => _camSpeedMul;
    internal bool HelpDetailedForTest => _helpDetailed;
    internal string PageForTest => _page;
    internal string? BuildTypeForTest => _buildType;
    internal Vector3? SelectedSiteForTest => _selectedSite;
    internal bool ConnectPanelOpenForTest => _connectPanelOpen;
    internal float CameraDistanceForTest => _distance;
    internal Vector3 CameraFocusForTest => _focus;

    // ---- 教学学习操作上报（只报真实发生的相机/选中操作，不伪造达成；每世界每阶段首次一次） ----

    private void NoteCameraOperation() => ReportLearning("camera");

    private void ReportLearning(string action, string? objectId = null)
    {
        // 真实操作先进 pending；发送由 FlushPendingLearning 在权威队列空闲时逐条放入，忙时不吞也不假记。
        var tutorial = _tutorial ??= _world.ReadTutorial();
        if (tutorial is not { Enabled: true, Completed: false }) return;
        string key = _learnedStage + "/" + action + "/" + (objectId ?? "");
        if (_learnedOps.Contains(key)) return;
        foreach (var pending in _pendingLearn)
            if (pending.Key == key) return;
        _pendingLearn.Add((key, action, objectId));
    }

    // 每帧最多把一条真实未确认的学习操作放入权威空槽（Busy=false 表示队列空闲）；
    // 忙时保留 pending 等下一帧重试，避免 observe-team 的相机/唯一选中被软卡丢报。
    private void FlushPendingLearning(PlayerLifecycleView lifecycle)
    {
        if (_pendingLearn.Count == 0) return;
        if (_tutorial is not { Enabled: true, Completed: false }) { _pendingLearn.Clear(); return; }
        if (lifecycle.Busy) return;
        var (key, action, objectId) = _pendingLearn[0];
        _pendingLearn.RemoveAt(0);
        if (!_learnedOps.Add(key)) return;
        _world.QueueTutorialAction(action, objectId);
    }

    // ---- HUD/页面辅助（两个 partial 共用） ----

    private static Label NewLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private Label TrackLabel(string name, string text, int size, Color color, bool wrap = true)
    {
        var label = NewLabel(text, size, color);
        label.Name = name;
        if (wrap)
        {
            label.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        label.AddThemeConstantOverride("line_separation", 2);
        _sizeTargets.Add((label, size));
        return label;
    }

    private Label InfoLabel(string name, int size, Color color) => TrackLabel(name, "", size, color);

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = Palette.PanelBg,
        BorderColor = Palette.PanelBorder,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
    };

    private static StyleBoxFlat ButtonStyle(Color bg, Color? border = null, int borderWidth = 0) => new()
    {
        BgColor = bg,
        BorderColor = border ?? bg,
        BorderWidthLeft = borderWidth, BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth, BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6,
    };

    private Button MakeButton(string name, string text, Action command, bool accent = false, bool commandCooldown = true)
    {
        var button = new Button
        {
            Text = text,
            Name = name,
            FocusMode = Control.FocusModeEnum.None,
        };
        StyleButton(button, accent);
        _sizeTargets.Add((button, SizeBody));
        button.Pressed += () =>
        {
            if (commandCooldown && !GateAction("cmd:" + name)) return;
            command();
        };
        return button;
    }

    private static void StyleButton(Button button, bool accent, bool focusable = false)
    {
        var bg = accent ? Palette.Accent : Palette.PanelBg;
        var fg = accent ? Palette.AccentText : Palette.Text;
        button.AddThemeStyleboxOverride("normal", ButtonStyle(bg));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(accent ? Palette.Accent.Lightened(0.1f) : bg.Lightened(0.1f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(accent ? Palette.Accent.Darkened(0.15f) : bg.Darkened(0.08f)));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(Palette.PanelBorder));
        if (focusable)
            button.AddThemeStyleboxOverride("focus", ButtonStyle(bg, Palette.Accent, 2));
        button.AddThemeColorOverride("font_color", fg);
        button.AddThemeColorOverride("font_hover_color", fg);
        button.AddThemeColorOverride("font_pressed_color", fg);
        button.AddThemeColorOverride("font_focus_color", fg);
        button.AddThemeColorOverride("font_disabled_color", Palette.TextDim);
    }

    // 页面/菜单按钮：保留键盘焦点并可被焦点环高亮。命令冷却交给各自 Queue 包装器，避免双重门禁吞意图。
    private Button MenuButton(string name, string text, Action command, bool accent = false)
    {
        var button = MakeButton(name, text, command, accent, commandCooldown: false);
        button.FocusMode = Control.FocusModeEnum.All;
        StyleButton(button, accent, focusable: true);
        button.CustomMinimumSize = new Vector2(260, 44);
        _minSizeTargets.Add((button, new Vector2(260, 44)));
        return button;
    }

    // 镜头按钮：不参与工程冷却，并保留键盘焦点（可访问性）。
    private void AddCamButton(HFlowContainer row, string name, string text, Action command)
    {
        var button = MakeButton(name, text, command, commandCooldown: false);
        button.FocusMode = Control.FocusModeEnum.All;
        row.AddChild(button);
    }

    // 确认按钮在 MakeButton 层已过冷却；这里只提交意图，不重复门禁。
    private void DoConfirm()
    {
        if (_preview is not { Legal: true } preview) return;
        if (Building && _buildType is { } type)
        {
            if (_plan is { CanSubmit: false }) return; // 草案不可提交时不发明意图
            _world.QueueBuild(type, preview.Center, YawRadians(), _plan?.ObservedVersion ?? preview.Version);
        }
        else
        {
            _world.QueueLevel(preview.Center, preview.Version, SelectedWorkerId());
        }
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

    // ---- 标记 ----

    private void BuildMarkers()
    {
        _markers = new Node3D { Name = "Markers" }; AddChild(_markers);
        _markerFactory = ResourceLoader.Load<GDScript>("res://assets/d1-art/markers.gd").New().AsGodotObject();
        _previewMarker = new Marker(); _siteMarker = new Marker(); _jobMarker = new Marker(); _selectMarker = new Marker();
    }

    private void UpdateMarkers(PlayerReadModel state)
    {
        bool inGame = _page == "game";
        float radius = _preview?.Radius ?? (Building ? _buildRadius : SiteRadiusM);
        PlaceMarker(_previewMarker, inGame ? _preview?.Center : null, _preview is { Legal: true } ? "legal" : "illegal", radius, state);
        PlaceMarker(_siteMarker, inGame && _preview == null ? _selectedSite : null, "hover", radius, state);
        PlaceMarker(_jobMarker, inGame ? state.Job?.Center : null, _world.PlayerJobApplied ? "committed" : "hover", SiteRadiusM, state);
        var selected = Array.Find(_robots, r => r.Id == _selectedRobotId);
        PlaceMarker(_selectMarker, inGame ? selected?.Position : null, "selected", (selected?.Radius ?? 1f) + .35f, state);
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

    // ---- 展示映射（只做类型/阶段到中文的呈现，不反算规则数值） ----

    private static string StageDisplay(string stage) => stage switch
    {
        "Travelling" => "前往工位", "Working" => "整平中", "WaitingForSpace" => "等待场地腾空",
        "AwaitingPhysics" => "确认地表", "Completed" => "已完成", "Cancelled" => "已取消",
        "Failed" => "无法继续", _ => stage,
    };

    private static string TypeDisplay(string type) => type switch
    {
        "solar" => "太阳能阵列", "charger" => "充电桩", "repair" => "维修站",
        "processor" => "加工设施", "storage" => "仓储",
        _ => type,
    };

    private static string NextActionDisplay(string action, string? neededType) => action switch
    {
        "observe" => "移动镜头并点选一台机器人",
        "build" => neededType is { Length: > 0 } type ? "建设 " + TypeDisplay(type) : "选择建造类型",
        "connect" => "接通电缆",
        "choose" => "选择发展方向",
        "wait" => "等待施工完成",
        "recover-power" => "恢复既有设施供电",
        "continue" => "同一世界继续经营",
        _ => action,
    };

    private static string EscapeActionFor(string page) => page switch
    {
        "pause" => "resume",
        "leave" => "leave-cancel",
        "settings" or "slots" or "newgame" or "confirm-newgame" or "confirm-load" => "back",
        _ => "", // home：没有上一层
    };

    private string WorkerDisplay(string? workerId)
        => string.IsNullOrEmpty(workerId) ? "未分配" // 加工/发展任务可能没有执行者字符串
            : Array.Find(_robots, r => r.Id == workerId) is { } robot ? robot.Name : workerId;

    private static string FacilityName(BootstrapReadModel boot, string id)
        => Array.Find(boot.Facilities, f => f.Id == id) is { } facility ? facility.Name : id;

    private static string F(float v) => v.ToString("G4", Inv);

    private static string CostText(MaterialAmountView[] amounts)
        => amounts.Length == 0 ? "无" : string.Join(" ", amounts.Where(a => a.Quantity != 0).Select(a => a.Name + "×" + a.Quantity));

    private static string GapText(MaterialAmountView[] amounts)
    {
        var gaps = amounts.Where(a => a.Quantity > 0).ToArray();
        return gaps.Length == 0 ? "无" : string.Join(" ", gaps.Select(a => a.Name + " 缺" + a.Quantity));
    }

    public override void _ExitTree() => _markerFactory?.Dispose();
}
