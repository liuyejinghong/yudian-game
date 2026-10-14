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
    private const float ObjectsIntervalS = 0.25f;
    private const float DetailIntervalS = 0.3f;
    private const float WorkIntervalS = 0.4f;
    private const float ManagementIntervalS = 0.3f;
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
    private Vector2 _mousePos;
    private Vector2? _panGrab;
    private bool _rightDragged;
    private Vector2 _leftPress;
    private bool _leftTracking, _aimCleared;

    // 选择与预览：typed 对象只存 Kind+Id 指针，详情按 epoch 重新解析；不存世界事实。
    private Vector3? _selectedSite;
    private Vector3? _hoverPoint;
    private WorldObjectKind _selectedKind;
    private string? _selectedObjectId;
    private PlayerObjectDetailView? _objectDetail;
    private long _objectDetailEpoch = long.MinValue;
    private float _objectTimer;
    private PlayerSitePreview? _preview;
    private float _previewTimer;
    private float _commandCooldown;
    private PlayerRobotView[] _robots = [];
    private WorldObjectView[] _worldObjects = [];
    private float _objectsTimer;

    // 场地模式：0 浏览、1 整地选址、2 建设选址；互斥于对象卡；进入时保存 returncontext（含当时 world epoch）。
    private int _placementMode;
    private sealed record PlacementContext(Basis Basis, Vector3 Focus, float Distance, WorldObjectKind? Kind, string? ObjectId, long Epoch);
    private PlacementContext? _placementReturn;

    // 建设模式：null 为整地；蓝图与基地状态只信 ReadBootstrap，每帧重读。
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
    private long _lastTransitionRevision = long.MinValue; // 读档/新游戏尝试轮次：每次真实尝试（含校验失败）都递增
    private int _authorityReleaseFrames; // 权威管理临停解除的连续观察帧数

    // 管理层：同步结果成功才标 open；关闭只释放自己的临停，不翻转用户手动暂停。
    private bool _managementOpen, _managementTabMaterials = true, _managementClosePending;
    private PlayerMaterialsView? _materialsView;
    private PlayerDefinitionsView? _definitions;
    private PlayerWorkView? _work;
    private PlayerWorkView[] _workHistory = [];
    private float _managementTimer, _workTimer;
    private string _materialFilter = "";
    private string _materialLocationFilter = "";
    private bool _cancelConfirmOpen;
    private string? _cancelGoalId;
    private string _managementFilterSignature = "";

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

    // 管理层打开期间收回键盘焦点的局内控件（只顶层接受焦点）。
    private readonly List<Button> _gameFocusables = [];

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
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        if (!_initialized) return;
        float dt = (float)delta;
        _commandCooldown = MathF.Max(0f, _commandCooldown - dt);
        var state = _world.ReadPlayerState();
        var lifecycle = _world.ReadLifecycle();
        ObserveLifecycle(lifecycle, state, dt);
        RetryCloseManagement(lifecycle);
        ReadSettingsAndApply();

        bool inGame = _page == "game";
        _hudRoot.Visible = inGame; // 菜单/主页期间隐藏局内 HUD
        if (inGame)
        {
            FlushPendingLearning(lifecycle);
            _robots = _world.ReadPlayerRobots();
            _bootstrap = _world.ReadBootstrap();
            _objectsTimer -= dt;
            if (_objectsTimer <= 0f)
            {
                _worldObjects = _world.ReadWorldObjects();
                _objectsTimer = ObjectsIntervalS;
            }
            _workTimer -= dt;
            if (_workTimer <= 0f)
            {
                _work = _world.ReadWork();
                _workHistory = _world.ReadWorkHistory();
                _workTimer = WorkIntervalS;
            }
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

    // 生命周期观察：尝试轮次/epoch 变化清选择与草案；页面变化切换输入归属；离开 game 或权威临停解除同步丢弃管理层状态。
    private void ObserveLifecycle(PlayerLifecycleView lifecycle, PlayerReadModel state, float dt)
    {
        if (_lifecycle is { } previous && (previous.Page != lifecycle.Page || previous.Busy != lifecycle.Busy))
            ClearInputTracking();
        _lifecycle = lifecycle;
        _pageGate = MathF.Max(0f, _pageGate - dt);
        // 读档/新游戏尝试轮次：权威在每次真实尝试（含校验失败）的执行入口递增；见到新轮次即清选址/预览/returncontext，
        // 同 epoch 的失败尝试也不复用上一轮草案。首次观察不算变化。
        if (lifecycle.WorldTransitionRevision != _lastTransitionRevision)
        {
            bool firstRevision = _lastTransitionRevision == long.MinValue;
            _lastTransitionRevision = lifecycle.WorldTransitionRevision;
            if (!firstRevision)
            {
                ClearSelection();
                _placementMode = 0;
                _placementReturn = null;
                _preview = null;
                _plan = null;
                SyncModeToggles();
            }
        }
        // 离开 game（菜单/离开确认/生命周期切换）：同步丢弃管理覆盖层状态；返回局内不复活旧管理。
        if ((_managementOpen || _managementClosePending) && lifecycle.Page != "game")
            DiscardManagementUiState();
        // 权威已解除管理临停（如管理期间被生命周期边界清掉）：连续两帧未见临停即同步丢弃，UI 不再自认打开。
        if (_managementOpen && !_managementClosePending && lifecycle.Page == "game" && !lifecycle.Busy
            && lifecycle.WorldEpoch == CurrentEpoch() && !state.ManagementPaused)
        {
            _authorityReleaseFrames++;
            if (_authorityReleaseFrames >= 2) { _authorityReleaseFrames = 0; DiscardManagementUiState(); }
        }
        else _authorityReleaseFrames = 0;
        if (lifecycle.WorldEpoch != _lastEpoch)
        {
            bool first = _lastEpoch == long.MinValue;
            _lastEpoch = lifecycle.WorldEpoch;
            _connectOptionsEpoch = long.MinValue;
            if (!first)
            {
                DiscardManagementUiState();
                ClearSelection();
                _plan = null;
                _placementMode = 0;
                _placementReturn = null;
                _preview = null;
                _materialsView = null;
                _definitions = null;
                _work = null;
                _workHistory = [];
                _materialFilter = "";
                _materialLocationFilter = "";
                _managementFilterSignature = "";
                _relatedSignature = "";
                _relatedWorkGoalId = null; // 新世界无旧预约；关联子详情与行随世界丢弃
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
        if (lifecycle.Busy) { _preview = null; _plan = null; } // loading/busy 期间临时预览不可复用
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
        // Esc 每次只退一层：确认/子详情 → 管理层 → 显式接线 → 选址预览 → 局内暂停；Esc 不取消正式工程。
        // 菜单页的 Esc 始终走页面 Back 语义，不被局内覆盖层状态截走。
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            if (!inGame) { QueueLifecycle(EscapeActionFor(lifecycle.Page)); return; }
            if (_cancelConfirmOpen) { CloseCancelConfirm(); return; } // Esc 只退取消确认层
            if (_managementOpen)
            {
                if (_relatedWorkGoalId != null) { CloseRelatedWorkDetail(); return; } // Esc 先退关联工程子详情
                CloseManagement();
                return;
            }
            if (_connectPanelOpen) { CloseConnectPanel(); return; }
            if (PlacementActive) { ExitPlacement(); return; } // 普通预览取消不确认
            QueueLifecycle("pause");
            return;
        }
        // 管理层门禁：遮罩挡鼠标，其余键盘也不透传为世界操作。
        if (_managementOpen) return;
        // 菜单/确认/恢复期间不把鼠标键盘透传为世界命令；页面输入归属由权威 ReadLifecycle 判定。
        if (!inGame || lifecycle.Busy) return;
        if (_pageGate > 0f) return;
        switch (@event)
        {
            case InputEventKey key when key.Pressed && !key.Echo && key.Keycode == Key.F:
                Vector3? target = _selectedSite ?? ObjectPosition(_selectedKind, _selectedObjectId) ?? state.Job?.Center;
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
                if (!_rightDragged && !PlacementActive) ClearSelection(); // 右键单击取消选择（选址模式退出走 Esc/退出按钮）
                break;
            case MouseButton.Middle:
                _panGrab = null;
                break;
        }
    }

    private void HandleClick(Vector2 screenPos)
    {
        _aimCleared = false;
        if (PlacementActive)
        {
            // 建设模式未选蓝图：左键不选址、不预览；必须先在蓝图行选择类型。
            if (_placementMode == 2 && _buildType == null) return;
            // 选址模式：左键仅选址与移动预览；同一次点击不选机器人、不提交。
            if (GroundHit(screenPos, out var point))
            {
                _selectedSite = point;
                _preview = null;
                _plan = null;
            }
            return;
        }
        if (PickWorldObject(screenPos) is { } pick)
        {
            _selectedKind = pick.Kind;
            _selectedObjectId = pick.Id;
            _objectDetail = null;
            _objectTimer = 0f;
            _objectDetailEpoch = long.MinValue;
            _selectedSite = null;
            _preview = null;
            _plan = null;
            if (pick.Kind == WorldObjectKind.Robot) ReportLearning("select-robot", pick.Id); // 只记录真实发生的选中操作
            return;
        }
        // 空点：仅清除选择，不下任何命令。
        ClearSelection();
    }

    private void ClearSelection()
    {
        _hoverPoint = null; _aimCleared = true;
        _selectedObjectId = null;
        _objectDetail = null;
        _selectedSite = null;
        _preview = null;
        _plan = null;
    }

    // ---- 相机 ----

    // 水平视轴供键盘、手势共用。
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
        if (_managementOpen)
        {
            // 管理层门禁：每帧键盘轮询不驱动镜头；只保持当前姿态。
            _camera.GlobalTransform = new Transform3D(_cameraBasis, _focus + _cameraBasis.Z * _distance);
            return;
        }
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

    // typed 浏览选择：机器人/矿点/设施统一按权威 ReadWorldObjects 投影拾取，取最近者。
    private (WorldObjectKind Kind, string Id)? PickWorldObject(Vector2 screenPos)
    {
        var origin = _camera.ProjectRayOrigin(screenPos);
        var dir = _camera.ProjectRayNormal(screenPos);
        (WorldObjectKind Kind, string Id)? best = null;
        float bestAlong = float.MaxValue;
        foreach (var view in _worldObjects)
        {
            var center = view.Position + Vector3.Up * (view.Kind == WorldObjectKind.Robot ? 0.45f : 0.6f);
            float along = (center - origin).Dot(dir);
            if (along <= 0.5f) continue;
            float miss = (origin + dir * along).DistanceTo(center);
            if (miss <= MathF.Max(view.Radius + 0.6f, along * 0.02f) && along < bestAlong)
            {
                bestAlong = along;
                best = (view.Kind, view.Id);
            }
        }
        return best;
    }

    private Vector3? ObjectPosition(WorldObjectKind kind, string? id)
        => id is { } wanted && Array.Find(_worldObjects, o => o.Kind == kind && o.Id == wanted) is { } view ? view.Position : null;

    // ---- 场地模式（建设/整地）与 returncontext ----

    private bool BootstrapOn => _bootstrap is { Enabled: true };
    private bool PlacementActive => _placementMode != 0;
    private bool Building => _placementMode == 2 && _buildType != null;
    private float YawRadians() => _yawSteps switch
    {
        1 => MathF.PI / 2f,
        2 => MathF.PI,
        3 => -MathF.PI / 2f,
        _ => 0f,
    };

    private void EnterPlacement(int mode)
    {
        if (_managementOpen || _cancelConfirmOpen) return; // 管理层/确认层期间动作门禁：不能只靠焦点抑制
        if (!BootstrapOn) return;
        if (_placementMode == mode) { ExitPlacement(); return; } // 重复入口：再次点击退出选址
        if (!PlacementActive)
            _placementReturn = new PlacementContext(_cameraBasis, _focus, _distance,
                _selectedObjectId is { } id ? _selectedKind : null, _selectedObjectId, CurrentEpoch());
        _placementMode = mode;
        _selectedSite = null;
        _preview = null;
        _plan = null;
        _yawSteps = 0;
        if (_rotateButton != null) _rotateButton.Text = "朝向 0°";
        _selectedObjectId = null; // 建设卡与对象卡互斥
        _objectDetail = null;
        _hoverPoint = null;
        SyncModeToggles();
    }

    // 退出选址：恢复进入前镜头与有效对象；对象按 epoch+stableID 重验，消失则清选。
    private void ExitPlacement()
    {
        if (!PlacementActive) return;
        _placementMode = 0;
        _selectedSite = null;
        _preview = null;
        _plan = null;
        _yawSteps = 0;
        SyncModeToggles();
        var restore = _placementReturn;
        _placementReturn = null;
        if (restore is { } ctx)
        {
            _cameraBasis = ctx.Basis;
            _focus = ctx.Focus;
            _distance = ctx.Distance;
            // returncontext 记录进入时 epoch：世界已切换（load/newgame）时只恢复镜头，不恢复旧世界对象选择。
            if (ctx.Epoch == CurrentEpoch() && ctx.Kind is { } kind && ctx.ObjectId is { } id
                && _world.ReadObjectDetail(kind, id, _lastEpoch) != null)
            {
                _selectedKind = kind;
                _selectedObjectId = id;
                _objectDetail = null;
                _objectTimer = 0f;
                _objectDetailEpoch = long.MinValue;
            }
        }
    }

    // ---- 管理层（独立临停；打开/关闭都走权威同步结果） ----

    private long CurrentEpoch() => _lifecycle?.WorldEpoch ?? _lastEpoch;

    private void OpenManagement(bool materialsTab)
    {
        if (_page != "game") return;
        if (_managementOpen)
        {
            if (_managementTabMaterials == materialsTab) { CloseManagement(); return; } // 重复入口关闭同视图
            _managementTabMaterials = materialsTab; // 同一覆盖层内切换标签
            _cancelConfirmOpen = false;
            SyncCancelConfirmVisibility();
            _managementTimer = 0f;
            return;
        }
        CloseConnectPanel(); // 单一管理层，不与接线面板叠层
        if (!_world.SetPlayerManagement(true, CurrentEpoch())) return; // 同步结果成功才开
        _managementOpen = true;
        _managementTabMaterials = materialsTab;
        _cancelConfirmOpen = false;
        _cancelGoalId = null;
        _managementTimer = 0f;
        _managementFilterSignature = "";
        _managementOverlay.Visible = true; // 遮罩当帧即挡世界点击
        SuppressGameFocus();
        GrabManagementFocus();
    }

    private void CloseManagement()
    {
        if (!_managementOpen) return;
        long epoch = CurrentEpoch();
        if (_world.SetPlayerManagement(false, epoch))
        {
            _managementOpen = false;
            _managementClosePending = false;
            _cancelConfirmOpen = false;
            _cancelGoalId = null;
            SyncCancelConfirmVisibility();
            _managementOverlay.Visible = false;
            UnsuppressGameFocus();
            RevalidateSelectedObject();
        }
        else _managementClosePending = true; // 命令在途：下一帧重试释放自己的临停
    }

    private void RetryCloseManagement(PlayerLifecycleView lifecycle)
    {
        if (!_managementClosePending) return;
        if (lifecycle.Page != "game" || lifecycle.WorldEpoch != CurrentEpoch())
        {
            // 权威已在生命周期切换时清临停；UI 只丢自己的覆盖层状态。
            _managementClosePending = false;
            _managementOpen = false;
            _cancelConfirmOpen = false;
            SyncCancelConfirmVisibility();
            _managementOverlay.Visible = false;
            UnsuppressGameFocus();
            return;
        }
        if (_world.SetPlayerManagement(false, lifecycle.WorldEpoch))
        {
            _managementClosePending = false;
            _managementOpen = false;
            _cancelConfirmOpen = false;
            _cancelGoalId = null;
            SyncCancelConfirmVisibility();
            _managementOverlay.Visible = false;
            UnsuppressGameFocus();
            RevalidateSelectedObject();
        }
    }

    // 管理层期间被抑制的局内按钮焦点恢复。
    private void SuppressGameFocus() { foreach (var button in _gameFocusables) button.FocusMode = Control.FocusModeEnum.None; }
    private void UnsuppressGameFocus() { foreach (var button in _gameFocusables) button.FocusMode = Control.FocusModeEnum.All; }

    // 权威侧已结束管理（生命周期边界/临停解除/离开 game）：UI 只丢弃自己的覆盖层状态，不复活、不再发命令。
    private void DiscardManagementUiState()
    {
        _managementOpen = false;
        _managementClosePending = false;
        _cancelConfirmOpen = false;
        _cancelGoalId = null;
        _relatedWorkGoalId = null;
        _authorityReleaseFrames = 0;
        SyncCancelConfirmVisibility();
        _managementOverlay.Visible = false;
        UnsuppressGameFocus();
    }

    private void RevalidateSelectedObject()
    {
        if (_selectedObjectId is not { } id) return;
        if (_world.ReadObjectDetail(_selectedKind, id, CurrentEpoch()) == null)
        {
            _selectedObjectId = null;
            _objectDetail = null;
        }
    }

    // ---- 预览与建造方案 ----

    private void UpdateAimAndPreview(PlayerReadModel state, float dt)
    {
        if (!PlacementActive)
        {
            _preview = null;
            _plan = null;
            _hoverPoint = null;
            return;
        }
        if (_placementMode == 2 && _buildType == null)
        {
            // 建设模式未选蓝图：无预览、无草案，也不整地 fallback。
            _preview = null;
            _plan = null;
            return;
        }
        bool jobActive = state.Job is { Active: true };
        // 活动工程期间仍允许建设模式继续瞄准（只读草案，不预约）；整地模式仍需先完成或取消。
        if (_selectedSite == null && !_aimCleared)
            _hoverPoint = MousePlanePoint(_mousePos);
        var aim = _selectedSite ?? _hoverPoint;
        if (aim is not { } target || (_placementMode == 1 && jobActive))
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
                : _world.PreviewLevel(target);
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
    internal bool ManagementOpenForTest => _managementOpen;
    internal bool ManagementMaterialsTabForTest => _managementTabMaterials;
    internal bool CancelConfirmOpenForTest => _cancelConfirmOpen;
    internal int PlacementModeForTest => _placementMode;
    internal string? SelectedObjectIdForTest => _selectedObjectId;
    internal WorldObjectKind SelectedKindForTest => _selectedKind;
    internal PlayerWorkView? WorkForTest => _work;

    // 自测接缝：只由 tools/player-ui-tests 的相机诊断/布局用例调用，恢复测试记录的初姿；玩家流程不经过，不改生产相机路径。
    internal void ResetCameraForTest(Basis basis, Vector3 focus, float distance)
    {
        _cameraBasis = basis.Orthonormalized();
        _focus = new Vector3(focus.X, 0, focus.Z);
        _distance = Mathf.Clamp(distance, MinDistanceM, MaxDistanceM);
        _camera.GlobalTransform = new Transform3D(_cameraBasis, _focus + _cameraBasis.Z * _distance);
    }

    // 等价按 F 把镜头焦点移到世界位置；只动镜头，不改世界、选择或工程状态。
    internal void FocusCameraForTest(Vector3 worldXz)
    {
        _focus = new Vector3(worldXz.X, 0, worldXz.Z);
        _camera.GlobalTransform = new Transform3D(_cameraBasis, _focus + _cameraBasis.Z * _distance);
    }

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

    // 确认按钮在 MakeButton 层已过冷却；这里只提交意图，不重复门禁。
    private void DoConfirm()
    {
        if (_managementOpen || _cancelConfirmOpen) return; // 管理层/确认层期间不提交
        if (_placementMode == 2 && _buildType == null) return; // 未选蓝图：不提交，也不整地 fallback
        if (_preview is not { Legal: true } preview) return;
        if (Building && _buildType is { } type)
        {
            if (_plan is { CanSubmit: false }) return; // 草案不可提交时不发明意图
            _world.QueueBuild(type, preview.Center, YawRadians(), _plan?.ObservedVersion ?? preview.Version);
        }
        else
        {
            _world.QueueLevel(preview.Center, preview.Version);
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
        PlaceMarker(_siteMarker, inGame && PlacementActive && _preview == null ? _selectedSite : null, "hover", radius, state);
        PlaceMarker(_jobMarker, inGame ? state.Job?.Center : null, _world.PlayerJobApplied ? "committed" : "hover", SiteRadiusM, state);
        Vector3? selectedPos = null;
        float selectedRadius = 1.35f;
        if (_selectedObjectId is { } sid && Array.Find(_worldObjects, o => o.Id == sid && o.Kind == _selectedKind) is { } view)
        {
            selectedPos = view.Position;
            selectedRadius = view.Radius + 0.35f;
        }
        PlaceMarker(_selectMarker, inGame ? selectedPos : null, "selected", selectedRadius, state);
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

    private static string TypeDisplay(string type) => type switch
    {
        "solar" => "太阳能阵列", "charger" => "充电桩", "repair" => "维修站",
        "processor" => "加工设施", "storage" => "仓储", "connection" => "电缆接线",
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

    private string FacilitySourceName(string sourceId)
        => _bootstrap is { } boot && Array.Find(boot.Facilities, f => f.Id == sourceId) is { } facility ? facility.Name : TypeDisplay(sourceId);

    private static string F(float v) => v.ToString("G4", Inv);

    private static string CostText(MaterialAmountView[] amounts)
        => amounts.Length == 0 ? "无" : string.Join(" ", amounts.Where(a => a.Quantity != 0).Select(a => a.Name + "×" + a.Quantity));

    private static string GapText(MaterialAmountView[] amounts)
    {
        var gaps = amounts.Where(a => a.Quantity > 0).ToArray();
        return gaps.Length == 0 ? "无" : string.Join(" ", gaps.Select(a => a.Name + " 缺" + a.Quantity));
    }

    private static string WorkKindDisplay(PlayerWorkKind kind) => kind switch
    {
        PlayerWorkKind.Build => "建设", PlayerWorkKind.Connection => "接线", PlayerWorkKind.Restock => "补料",
        PlayerWorkKind.Mine => "采矿", PlayerWorkKind.Recipe => "加工", PlayerWorkKind.Haul => "运输",
        PlayerWorkKind.Level => "整平", _ => "工程",
    };

    private static string WorkStageDisplay(PlayerWorkStage stage) => stage switch
    {
        PlayerWorkStage.Authorized => "已授权", PlayerWorkStage.PreparingMaterials => "准备材料",
        PlayerWorkStage.Travelling => "前往工位", PlayerWorkStage.Mining => "采矿中",
        PlayerWorkStage.Processing => "加工中", PlayerWorkStage.Transporting => "运输中",
        PlayerWorkStage.WaitingForRoute => "等待运输路线", PlayerWorkStage.Levelling => "整平中",
        PlayerWorkStage.AwaitingPhysics => "确认地表", PlayerWorkStage.Constructing => "施工中",
        PlayerWorkStage.Completed => "已完成", PlayerWorkStage.Cancelled => "已取消",
        PlayerWorkStage.Blocked => "受阻", _ => "进行中",
    };

    private static string WorkStatusDisplay(PlayerWorkStatus status) => status switch
    {
        PlayerWorkStatus.Running => "进行中", PlayerWorkStatus.Waiting => "等待",
        PlayerWorkStatus.Blocked => "受阻", PlayerWorkStatus.Completed => "已完成",
        PlayerWorkStatus.Cancelled => "已取消", PlayerWorkStatus.Failed => "无法继续",
        _ => "进行中",
    };

    // 拒因显示：权威 Reason 原文优先；确无原因时按枚举给"原因未知"，不伪归因。
    private static string WorkReasonDisplay(PlayerReasonCode code, string reason)
        => reason.Length > 0 ? reason : code switch
        {
            PlayerReasonCode.None => "",
            PlayerReasonCode.Unknown => "原因未知",
            PlayerReasonCode.WaitingForWorker => "等待执行机器人",
            PlayerReasonCode.WaitingForStation => "等待工位",
            PlayerReasonCode.WaitingForRoute => "等待运输路线",
            PlayerReasonCode.WaitingForPower => "等待供电",
            PlayerReasonCode.WaitingForProjection => "等待地表确认",
            PlayerReasonCode.Completed => "已完成",
            PlayerReasonCode.Cancelled => "已取消",
            PlayerReasonCode.Blocked => "受阻",
            _ => "",
        };

    private static string ContainerKindDisplay(MaterialContainerKind kind) => kind switch
    {
        MaterialContainerKind.Warehouse => "仓库", MaterialContainerKind.RobotCargo => "机器人携带",
        MaterialContainerKind.Mine => "矿点", MaterialContainerKind.Worksite => "工地",
        MaterialContainerKind.Processing => "加工", MaterialContainerKind.Consumed => "已消耗",
        _ => "其他",
    };

    public override void _ExitTree() => _markerFactory?.Dispose();
}
