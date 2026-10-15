#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Yudian.PlayerUI;

// 分层局内 HUD：
//   顶部常驻细条：库内现货（Stored 口径）、发电/s、昼夜、真实警报、用户暂停与暂停菜单；
//   左上教学目标卡（可折叠，仅教学进行时占位）+ 低频详情折叠（教学明细/发展/设施/帮助）；
//   右侧唯一语境栏：typed 对象卡 / 选址准备卡互斥；独立活动工程一行不随选中消失；
//   单管理覆盖层：基地物资 / 工程 标签互切，重复入口关闭同视图；
//   底部工具组：建设 / 整地 / 基地物资 / 工程 + 接线 + 菜单 + 低频诊断折叠。
// 刷新走 revision/节流；列表只在签名变化时重建，不每帧重拼控件树。
public partial class PlayerController
{
    private Control _hudRoot = null!;
    private Control _commandBar = null!;
    private PanelContainer _commandPanel = null!;
    private ScrollContainer _infoScroll = null!, _planScroll = null!, _connectScroll = null!, _objectScroll = null!;
    private ScrollContainer _infoDetailsScroll = null!, _planDetailsScroll = null!;
    private ScrollContainer _planReasonScroll = null!, _planCostScroll = null!; // 固定区内拒因小滚动块 / 默认可见的内部成本滚动区
    private VBoxContainer _infoCore = null!, _planCore = null!;
    private Button _infoDetailsToggle = null!, _planDetailsToggle = null!;
    private HFlowContainer _rowModes = null!, _toolsRow = null!, _diagRow = null!;
    private ButtonGroup _modeGroup = null!;
    private Button? _rotateButton;
    private Button _confirmButton = null!, _diagToggle = null!;
    private Button _restockButton = null!, _legacyButton = null!, _recoverButton = null!, _retryButton = null!, _connectNextButton = null!, _connectOpenButton = null!;
    private Button _toolBuildButton = null!, _toolLevelButton = null!, _toolMaterialsButton = null!, _toolWorkButton = null!, _exitPlacementButton = null!;

    // 顶栏
    private PanelContainer _topBar = null!;
    private readonly Dictionary<string, Button> _topStockChips = [];
    private static readonly string[] TopStockIds = ["iron", "copper", "parts", "cable", "kit"];
    private Label _topPowerLabel = null!, _topDayLabel = null!, _topAlertLabel = null!;
    private Button _topPauseButton = null!, _topMenuButton = null!;

    // 左列：教学目标卡与低频详情
    private PanelContainer _infoPanel = null!;
    private VBoxContainer _goalCore = null!;
    private Button _goalFoldToggle = null!;
    private bool _goalCollapsed; // 目标卡整卡收成一行（仅标题）
    private Label _statusLabel = null!;
    private Label _goalTitleLabel = null!, _goalInstructionLabel = null!, _goalWhyLabel = null!,
        _goalActionLabel = null!, _goalCoverageLabel = null!, _goalProgressLabel = null!;
    private Button _milestonesToggle = null!;
    private Label _milestonesLabel = null!;
    private Label _noticeDetailsLabel = null!, _instructionDetailsLabel = null!, _worldLabel = null!, _stockDetailsLabel = null!;
    private Label _devTitleLabel = null!, _devBodyLabel = null!, _devDirectionsLabel = null!;
    private Button _devDirectionsToggle = null!, _facilityToggle = null!;
    private Label _facilityLabel = null!;
    private Label _hintsLabel = null!;

    // 右列：活动工程行 + 语境栏（对象卡 / 选址准备卡 / 接线）
    private PanelContainer _workRowPanel = null!;
    private Label _workRowLabel = null!;
    private Button _workRowButton = null!;
    private PanelContainer _planPanel = null!, _objectPanel = null!;
    private Label _planTitleLabel = null!, _selectionLabel = null!, _planTagLabel = null!, _planReasonLabel = null!,
        _planCostLabel = null!, _planAutoLabel = null!, _planSupplyLabel = null!,
        _planPowerLabel = null!, _planConsequenceLabel = null!, _planRecoveryLabel = null!, _planStockLabel = null!;
    private Label _objectKindLabel = null!, _objectTitleLabel = null!, _objectDescLabel = null!, _objectBodyLabel = null!, _objectHintLabel = null!;

    private PanelContainer _connectPanel = null!;
    private VBoxContainer _connectCore = null!; // 接线固定核心（标题/提示/成本/缺口/拒因小scroll/操作按钮），不随候选列表滚动
    private Label _connectTitleLabel = null!, _connectHintLabel = null!, _connectCostLabel = null!, _connectGapLabel = null!, _connectReasonLabel = null!;
    private ScrollContainer _connectReasonScroll = null!;
    private ScrollContainer _connectListScroll = null!;
    private VBoxContainer _connectListBox = null!;
    private Button _connectSubmitButton = null!, _connectCloseButton = null!;
    private float _connectContextHeight = 160f; // 当前帧接线面板实际可用高度（UpdateColumnGeometry 同帧写入，接线预算只读它）

    // 管理覆盖层（root 内最后绘制：高于命令栏与教学结尾卡；取消确认又是其内最顶层）
    private Control _managementOverlay = null!;
    private PanelContainer _managementPanel = null!;
    private Control _cancelShield = null!, _cancelConfirmCard = null!;
    private Label _managementTitleLabel = null!;
    private Button _managementMaterialsTabButton = null!, _managementWorkTabButton = null!, _managementCloseButton = null!;
    private ScrollContainer _managementScroll = null!;
    private VBoxContainer _managementMaterialsBox = null!, _managementWorkBox = null!;
    private HFlowContainer _materialFilterRow = null!, _locationFilterRow = null!;
    private readonly List<(string Id, Button Button)> _materialFilterChips = [];
    private readonly List<(string Kind, Button Button)> _locationFilterChips = [];
    private Label _matSummaryLabel = null!, _matLocationsLabel = null!, _matReservationsLabel = null!,
        _matBatchesLabel = null!, _matRelatedLabel = null!;
    private VBoxContainer _matRelatedBox = null!;
    private readonly List<(HBoxContainer Row, Button? Open)> _relatedWorkRows = [];
    private string _relatedSignature = "";
    private string? _relatedWorkGoalId; // 关联工程子详情：Esc/返回先退该层回物资标签
    private VBoxContainer _relatedWorkBox = null!;
    private Button _relatedWorkBackButton = null!;
    private Label _relatedWorkDetailLabel = null!;
    private Label _workCurrentLabel = null!, _workHistoryLabel = null!;
    private Button _workCancelButton = null!;
    private PanelContainer _cancelConfirmBox = null!;
    private Label _cancelConfirmLabel = null!;
    private Button _cancelConfirmYesButton = null!, _cancelConfirmNoButton = null!;
    private bool _managementFocusPending;
    private bool _cancelFocusPending;
    private bool _managementFocusSuppressed; // 取消确认独占：管理层其余可聚焦控件临时 FocusMode.None，确认关闭后恢复

    private Control _conclusionCard = null!;
    private Label _conclusionListLabel = null!;
    private Button _conclusionContinueButton = null!;

    private string? _hintedModeType;
    private static readonly string HelpDetailedText =
        "WASD／方向键平移 · 右键拖动或双指平移 · 滚轮／捏合缩放 · Q/E 旋转 · F 定位选中 · 左键选择或选址 · 右键取消选择 · Esc 逐层返回";
    private static readonly string HelpBriefText =
        "WASD 平移 · 滚轮缩放 · Q/E 旋转 · 左键选择 · Esc 菜单";

    private void BuildHud()
    {
        var hud = new CanvasLayer { Name = "PlayerHud" };
        AddChild(hud);
        var root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(root);
        _hudRoot = root;

        BuildTopBar(root);

        // —— 左列：教学目标卡 + 低频详情折叠（高度按底部命令栏封顶） ——
        var infoScroll = new ScrollContainer
        {
            Name = "InfoScroll",
            Position = new Vector2(16, 60),
            Size = new Vector2(420, 560),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        _infoScroll = infoScroll;
        root.AddChild(infoScroll);
        var info = new PanelContainer { Name = "InfoPanel", MouseFilter = Control.MouseFilterEnum.Stop, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddThemeStyleboxOverride("panel", PanelStyle());
        infoScroll.AddChild(info);
        _infoPanel = info;
        var infoBox = new VBoxContainer { Name = "InfoStack" };
        infoBox.AddThemeConstantOverride("separation", 4);
        info.AddChild(infoBox);

        // 教学目标卡：仅教学进行时占位；整卡可收成一行；无教学目标（含 freeplay 结尾确认后）整块隐藏，不造 pin。
        _goalCore = new VBoxContainer { Name = "GoalCore" };
        _goalCore.AddThemeConstantOverride("separation", 4);
        infoBox.AddChild(_goalCore);
        _infoCore = _goalCore; // 布局计算用核心区引用（目标卡收起时最小高度为折叠行，详情获得其余高度）
        _goalFoldToggle = new Button { Name = "GoalFoldToggle", Text = "目标 ▾", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _sizeTargets.Add((_goalFoldToggle, SizeSmall));
        _goalFoldToggle.Toggled += on =>
        {
            _goalCollapsed = !on;
            _goalFoldToggle.Text = on ? "目标 ▾" : "目标 ▸";
        };
        _goalFoldToggle.ButtonPressed = true; // 默认展开
        _goalCore.AddChild(_goalFoldToggle);
        _statusLabel = InfoLabel("StatusLabel", SizeSmall, Palette.Warn);
        _goalTitleLabel = InfoLabel("GoalTitleLabel", SizeHeading, Palette.Text);
        _goalInstructionLabel = InfoLabel("GoalInstructionLabel", SizeBody, Palette.Text);
        _goalActionLabel = InfoLabel("GoalActionLabel", SizeBody, Palette.Accent);
        _goalCore.AddChild(_statusLabel);
        _goalCore.AddChild(_goalTitleLabel);
        _goalCore.AddChild(_goalInstructionLabel);
        _goalWhyLabel = InfoLabel("GoalWhyLabel", SizeBody, Palette.TextDim); // why 属于展开的目标核心
        _goalCore.AddChild(_goalWhyLabel);
        _goalCore.AddChild(_goalActionLabel);

        _infoDetailsScroll = new ScrollContainer
        {
            Name = "InfoDetailsScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 180),
        };
        _infoDetailsToggle = FoldToggle("InfoDetailsToggle", "目标详情", _infoDetailsScroll);
        infoBox.AddChild(_infoDetailsToggle);
        infoBox.AddChild(_infoDetailsScroll);
        var infoDetails = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        infoDetails.AddThemeConstantOverride("separation", 6);
        _infoDetailsScroll.AddChild(infoDetails);
        _noticeDetailsLabel = InfoLabel("NoticeDetailsLabel", SizeSmall, Palette.TextDim);
        _instructionDetailsLabel = InfoLabel("InstructionDetailsLabel", SizeBody, Palette.Text);
        _goalCoverageLabel = InfoLabel("GoalCoverageLabel", SizeSmall, Palette.Hint);
        _goalProgressLabel = InfoLabel("GoalProgressLabel", SizeTag, Palette.TextDim);
        _worldLabel = InfoLabel("WorldLabel", SizeTag, Palette.TextDim);
        _stockDetailsLabel = InfoLabel("StockDetailsLabel", SizeSmall, Palette.TextDim);
        infoDetails.AddChild(_noticeDetailsLabel);
        infoDetails.AddChild(_instructionDetailsLabel);
        infoDetails.AddChild(_goalCoverageLabel);
        infoDetails.AddChild(_goalProgressLabel);
        infoDetails.AddChild(_stockDetailsLabel);
        _milestonesToggle = FoldToggle("MilestonesToggle", "教学进度明细", _milestonesLabel = InfoLabel("MilestonesLabel", SizeSmall, Palette.TextDim));
        infoDetails.AddChild(_milestonesToggle);
        infoDetails.AddChild(_milestonesLabel);
        infoDetails.AddChild(_worldLabel);

        _devTitleLabel = InfoLabel("DevTitleLabel", SizeSmall, Palette.Accent);
        _devBodyLabel = InfoLabel("DevBodyLabel", SizeSmall, Palette.TextDim);
        infoDetails.AddChild(_devTitleLabel);
        infoDetails.AddChild(_devBodyLabel);
        _devDirectionsToggle = FoldToggle("DevDirectionsToggle", "发展详情", _devDirectionsLabel = InfoLabel("DevDirectionsLabel", SizeSmall, Palette.TextDim));
        infoDetails.AddChild(_devDirectionsToggle);
        infoDetails.AddChild(_devDirectionsLabel);

        _facilityToggle = FoldToggle("FacilityToggle", "设施", _facilityLabel = InfoLabel("FacilityLabel", SizeSmall, Palette.TextDim));
        infoDetails.AddChild(_facilityToggle);
        infoDetails.AddChild(_facilityLabel);
        _hintsLabel = TrackLabel("Hints", HelpDetailedText, SizeSmall, Palette.TextDim);
        infoDetails.AddChild(_hintsLabel);

        // —— 右列：独立活动工程一行（不随选中/建设改变消失；有活动才显示） ——
        var workRow = new PanelContainer { Name = "WorkRowPanel", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        workRow.AddThemeStyleboxOverride("panel", PanelStyle());
        root.AddChild(workRow);
        _workRowPanel = workRow;
        var workRowBox = new HBoxContainer { Name = "WorkRowBox" };
        workRowBox.AddThemeConstantOverride("separation", 8);
        workRow.AddChild(workRowBox);
        _workRowLabel = InfoLabel("WorkRowLabel", SizeSmall, Palette.Text);
        _workRowLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _workRowButton = MakeButton("WorkRowButton", "工程详情", () => OpenManagement(false), commandCooldown: false);
        RegisterGameFocusable(_workRowButton);
        workRowBox.AddChild(_workRowLabel);
        workRowBox.AddChild(_workRowButton);

        // —— 右列：选址准备卡（固定标题/操作区＋默认可见的内部成本滚动区；长详情折叠） ——
        // 外层 PlanScroll 保节点名与必要消费，但不再承载超长全部内容滚动：固定区受实际视口约束，
        // 开始/退出按钮先于多行成本/来源；完整拒因在固定区内自己的小滚动块，不截断。
        var planScroll = new ScrollContainer
        {
            Name = "PlanScroll",
            Size = new Vector2(380, 560),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        _planScroll = planScroll;
        root.AddChild(planScroll);
        var plan = new PanelContainer { Name = "PlanPanel", MouseFilter = Control.MouseFilterEnum.Stop, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        plan.AddThemeStyleboxOverride("panel", PanelStyle());
        planScroll.AddChild(plan);
        _planPanel = plan;
        var planBox = new VBoxContainer { Name = "PlanStack" };
        planBox.AddThemeConstantOverride("separation", 4);
        plan.AddChild(planBox);
        _planCore = new VBoxContainer { Name = "PlanCore" };
        _planCore.AddThemeConstantOverride("separation", 4);
        planBox.AddChild(_planCore);
        _planTitleLabel = InfoLabel("PlanTitleLabel", SizeHeading, Palette.Text);
        _selectionLabel = InfoLabel("SelectionLabel", SizeBody, Palette.Text);
        _planTagLabel = InfoLabel("PlanTagLabel", SizeSmall, Palette.Accent);
        _planReasonLabel = InfoLabel("PlanReasonLabel", SizeSmall, Palette.Warn);
        _planCostLabel = InfoLabel("PlanCostLabel", SizeBody, Palette.Text);
        _planAutoLabel = TrackLabel("PlanAutoLabel", "缺料由机器人自动备料并施工", SizeSmall, Palette.TextDim);
        _planSupplyLabel = InfoLabel("PlanSupplyLabel", SizeSmall, Palette.Text);
        _planPowerLabel = InfoLabel("PlanPowerLabel", SizeSmall, Palette.Text);
        _planConsequenceLabel = InfoLabel("PlanConsequenceLabel", SizeSmall, Palette.TextDim);
        _planCore.AddChild(_planTitleLabel);
        _planCore.AddChild(_selectionLabel);
        _planCore.AddChild(_planTagLabel);
        // 当前阻塞固定在屏内：完整拒因放在固定区内的小滚动块（默认可见，超长可区内滚动），不截断也不进折叠。
        var planReasonScroll = new ScrollContainer
        {
            Name = "PlanReasonScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 76),
        };
        _minSizeTargets.Add((planReasonScroll, new Vector2(0, 76)));
        _planReasonScroll = planReasonScroll;
        _planCore.AddChild(planReasonScroll);
        planReasonScroll.AddChild(_planReasonLabel);
        var planActions = new HBoxContainer { Name = "PlanActions" };
        planActions.AddThemeConstantOverride("separation", 8);
        _confirmButton = MakeButton("ConfirmButton", "确认整平", DoConfirm, accent: true);
        _confirmButton.CustomMinimumSize = new Vector2(176, 44);
        _minSizeTargets.Add((_confirmButton, new Vector2(176, 44)));
        RegisterGameFocusable(_confirmButton);
        StyleButton(_confirmButton, accent: true, focusable: true);
        _exitPlacementButton = MakeButton("ExitPlacementButton", "退出选址", () => ExitPlacement(), commandCooldown: false);
        RegisterGameFocusable(_exitPlacementButton);
        planActions.AddChild(_confirmButton);
        planActions.AddChild(_exitPlacementButton);
        _planCore.AddChild(planActions);
        _planCore.AddChild(_planAutoLabel);
        // 成本/供料/后果在默认可见的内部滚动区：完整需求/已有/缺口可查，超长允许区内滚动，主按钮无需滚到最底。
        var planCostScroll = new ScrollContainer
        {
            Name = "PlanCostScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 64), // 实际高度由 UpdateColumnGeometry 按视口剩余预算给出（真实像素，不重复乘缩放）
        };
        _planCostScroll = planCostScroll;
        planBox.AddChild(planCostScroll);
        var planCostBox = new VBoxContainer { Name = "PlanCostStack", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        planCostBox.AddThemeConstantOverride("separation", 4);
        planCostScroll.AddChild(planCostBox);
        planCostBox.AddChild(_planCostLabel);
        planCostBox.AddChild(_planSupplyLabel);
        planCostBox.AddChild(_planConsequenceLabel);
        _planDetailsScroll = new ScrollContainer
        {
            Name = "PlanDetailsScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 120),
        };
        _planDetailsToggle = FoldToggle("PlanDetailsToggle", "物料与来源详情", _planDetailsScroll);
        planBox.AddChild(_planDetailsToggle);
        planBox.AddChild(_planDetailsScroll);
        var planDetails = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _planDetailsScroll.AddChild(planDetails);
        _planRecoveryLabel = InfoLabel("PlanRecoveryLabel", SizeSmall, Palette.Hint);
        _planStockLabel = InfoLabel("PlanStockLabel", SizeSmall, Palette.TextDim);
        planDetails.AddChild(_planPowerLabel);
        planDetails.AddChild(_planRecoveryLabel);
        planDetails.AddChild(_planStockLabel);

        // —— 右列：typed 对象卡（选中矿点/机器人/设施时出现；无选中不留空板） ——
        var objectScroll = new ScrollContainer
        {
            Name = "ObjectScroll", Visible = false,
            Size = new Vector2(380, 560),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        _objectScroll = objectScroll;
        root.AddChild(objectScroll);
        var objectPanel = new PanelContainer { Name = "ObjectPanel", MouseFilter = Control.MouseFilterEnum.Stop, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        objectPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        objectScroll.AddChild(objectPanel);
        _objectPanel = objectPanel;
        var objectBox = new VBoxContainer { Name = "ObjectStack" };
        objectBox.AddThemeConstantOverride("separation", 4);
        objectPanel.AddChild(objectBox);
        _objectKindLabel = InfoLabel("ObjectKindLabel", SizeTag, Palette.Accent);
        _objectTitleLabel = InfoLabel("ObjectTitleLabel", SizeHeading, Palette.Text);
        _objectDescLabel = InfoLabel("ObjectDescLabel", SizeSmall, Palette.TextDim);
        _objectBodyLabel = InfoLabel("ObjectBodyLabel", SizeBody, Palette.Text);
        _objectHintLabel = TrackLabel("ObjectHintLabel", "Esc 或右键取消选择；建设与整地在底部工具选择后左键仅选址", SizeSmall, Palette.TextDim);
        objectBox.AddChild(_objectKindLabel);
        objectBox.AddChild(_objectTitleLabel);
        objectBox.AddChild(_objectDescLabel);
        objectBox.AddChild(_objectBodyLabel);
        objectBox.AddChild(_objectHintLabel);

        // —— 右列：显式接线面板（针对具体设施与来源；不自动挑选） ——
        // 外层 ConnectScroll 保节点名与必要消费，但纵向不滚全部内容（N001）：固定核心
        // （标题/提示/成本/缺口/完整拒因小滚动块/确认与关闭按钮）钉在顶部常驻可见；
        // 候选列表 expand 只用核心之外的实际剩余空间，自身 rect 完整在外层遮罩内、内部滚动到底。
        // 活动工程行挤压高度、拒因与候选 pair 变长时，收缩的是列表高度本身，核心按钮与列表末项都可达。
        var connectWrap = new ScrollContainer
        {
            Name = "ConnectScroll", Visible = false,
            Size = new Vector2(380, 560),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        _connectScroll = connectWrap;
        root.AddChild(connectWrap);
        var connect = new PanelContainer { Name = "ConnectPanel", MouseFilter = Control.MouseFilterEnum.Stop, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        connect.AddThemeStyleboxOverride("panel", PanelStyle());
        connectWrap.AddChild(connect);
        _connectPanel = connect;
        var connectBox = new VBoxContainer();
        connectBox.AddThemeConstantOverride("separation", 6);
        connect.AddChild(connectBox);
        _connectCore = new VBoxContainer { Name = "ConnectCore" };
        _connectCore.AddThemeConstantOverride("separation", 6);
        connectBox.AddChild(_connectCore);
        var connectTitle = TrackLabel("ConnectTitleLabel", "接线", SizeHeading, Palette.Text);
        _connectTitleLabel = connectTitle;
        _connectHintLabel = InfoLabel("ConnectHintLabel", SizeSmall, Palette.TextDim);
        _connectCostLabel = InfoLabel("ConnectCostLabel", SizeBody, Palette.Text);
        _connectGapLabel = InfoLabel("ConnectGapLabel", SizeBody, Palette.Text);
        _connectReasonLabel = InfoLabel("ConnectReasonLabel", SizeSmall, Palette.Warn);
        _connectCore.AddChild(connectTitle);
        _connectCore.AddChild(_connectHintLabel);
        _connectCore.AddChild(_connectCostLabel);
        _connectCore.AddChild(_connectGapLabel);
        // 完整拒因固定在核心内的小滚动块：高度在 RefreshConnectPanel 按文本行数自适应（这里只给基线）。
        var connectReasonScroll = new ScrollContainer
        {
            Name = "ConnectReasonScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 40),
        };
        _connectReasonScroll = connectReasonScroll;
        _connectCore.AddChild(connectReasonScroll);
        connectReasonScroll.AddChild(_connectReasonLabel);
        var connectActions = new HBoxContainer();
        connectActions.AddThemeConstantOverride("separation", 8);
        _connectSubmitButton = PanelActionButton("ConnectSubmitButton", "确认接线", SubmitConnectionSelection, accent: true);
        _connectCloseButton = PanelActionButton("ConnectCloseButton", "关闭", CloseConnectPanel);
        connectActions.AddChild(_connectSubmitButton);
        connectActions.AddChild(_connectCloseButton);
        _connectCore.AddChild(connectActions);
        // 候选列表：expand 拿固定核心之外的实际剩余空间，min 不保底（极端矮视口允许列表很小，
        // 但不为凑 96px 顶破外层遮罩）；实际高度每帧按视口给出，真实像素，不重复乘缩放。
        var connectScroll = new ScrollContainer
        {
            Name = "ConnectList",
            CustomMinimumSize = new Vector2(0, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _connectListScroll = connectScroll;
        connectBox.AddChild(connectScroll);
        _connectListBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _connectListBox.AddThemeConstantOverride("separation", 4);
        connectScroll.AddChild(_connectListBox);


        // —— 底部：提示折叠 + 工具组 + 蓝图行（选址时）+ 低频诊断 ——
        var bar = new CenterContainer
        {
            Name = "CommandBar",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -196, OffsetBottom = -12,
            GrowVertical = Control.GrowDirection.Begin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(bar);
        _commandBar = bar;
        var barPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        _commandPanel = barPanel;
        barPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        barPanel.CustomMinimumSize = new Vector2(840, 0);
        bar.AddChild(barPanel);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        barPanel.AddChild(stack);

        _rowModes = new HFlowContainer { Name = "BuildBar", Visible = false };
        _rowModes.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(_rowModes);

        _toolsRow = new HFlowContainer { Name = "ToolsRow" };
        _toolsRow.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(_toolsRow);
        _toolBuildButton = MakeToolToggle("ToolBuildButton", "建设", on =>
        {
            if (on) EnterPlacement(2);
            else if (_placementMode == 2) ExitPlacement();
        });
        _toolLevelButton = MakeToolToggle("ToolLevelButton", "整地", on =>
        {
            if (on) EnterPlacement(1);
            else if (_placementMode == 1) ExitPlacement();
        });
        _toolMaterialsButton = MakeToolButton("ToolMaterialsButton", "基地物资", () => OpenManagement(true));
        _toolWorkButton = MakeToolButton("ToolWorkButton", "工程", () => OpenManagement(false));
        _toolsRow.AddChild(_toolBuildButton);
        _toolsRow.AddChild(_toolLevelButton);
        _toolsRow.AddChild(_toolMaterialsButton);
        _toolsRow.AddChild(_toolWorkButton);
        _connectOpenButton = MakeButton("ExplicitConnectButton", "接线…", OpenConnectPanel, commandCooldown: false);
        RegisterGameFocusable(_connectOpenButton);
        var gameMenuButton = MakeButton("GameMenuButton", "菜单", () => QueueLifecycle("pause"), commandCooldown: false);
        RegisterGameFocusable(gameMenuButton);
        _diagToggle = MakeButton("DiagToggle", "诊断 ▸", ToggleDiagnostics, commandCooldown: false);
        RegisterGameFocusable(_diagToggle);
        _toolsRow.AddChild(_connectOpenButton);
        _toolsRow.AddChild(gameMenuButton);
        _toolsRow.AddChild(_diagToggle);

        _diagRow = new HFlowContainer { Name = "DiagRow", Visible = false };
        _diagRow.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(_diagRow);
        // 补维修耗材是真实经营动作：常规工具行入口，不进诊断折叠；旧档/故障恢复/重试/自动接线为诊断能力。
        _restockButton = MakeButton("RestockButton", "补维修耗材", () => _world.QueuePlayerAction("restock"));
        _toolsRow.AddChild(_restockButton);
        _legacyButton = MakeButton("LegacyLoadButton", "读取旧档", () => _world.QueuePlayerAction("legacy"));
        _recoverButton = MakeButton("RecoverButton", "故障恢复", () => _world.QueuePlayerAction("recover"));
        _retryButton = MakeButton("RetryButton", "重试工程/保障", () => _world.QueuePlayerAction("retry"));
        _connectNextButton = MakeButton("ConnectButton", "自动接最近电缆", () => _world.QueuePlayerAction("connect"));
        _diagRow.AddChild(_legacyButton);
        _diagRow.AddChild(_recoverButton);
        _diagRow.AddChild(_retryButton);
        _diagRow.AddChild(_connectNextButton);

        // —— 教学结尾卡片（同世界继续；卡片外的世界仍可操作） ——
        var conclusion = new CenterContainer
        {
            Name = "ConclusionCard",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        conclusion.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(conclusion);
        _conclusionCard = conclusion;
        var conclusionPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        conclusionPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        conclusionPanel.CustomMinimumSize = new Vector2(420, 0);
        _minSizeTargets.Add((conclusionPanel, new Vector2(420, 0)));
        conclusion.AddChild(conclusionPanel);
        var conclusionBox = new VBoxContainer();
        conclusionBox.AddThemeConstantOverride("separation", 8);
        conclusionPanel.AddChild(conclusionBox);
        conclusionBox.AddChild(TrackLabel("ConclusionTitle", "教学完成", SizeHeading, Palette.Accent));
        _conclusionListLabel = InfoLabel("ConclusionLabel", SizeBody, Palette.Text);
        conclusionBox.AddChild(_conclusionListLabel);
        _conclusionContinueButton = MenuButton("ConclusionContinueButton", "继续经营（同一世界）", () => QueueTutorial("continue"), accent: true);
        conclusionBox.AddChild(_conclusionContinueButton);
        conclusionBox.AddChild(MenuButton("ConclusionSaveHomeButton", "保存并返回主页", () => QueueLifecycle("home")));

        // —— 管理覆盖层：root 内最后建立 → 高于命令栏与教学结尾卡；遮罩 Stop 挡所有局内控件 ——
        var overlay = new ColorRect { Name = "ManagementOverlay", Color = Palette.ScrimLight, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(overlay);
        _managementOverlay = overlay;
        var managementCenter = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        managementCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(managementCenter);
        var managementPanel = new PanelContainer { Name = "ManagementPanel", MouseFilter = Control.MouseFilterEnum.Stop };
        managementPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        managementPanel.CustomMinimumSize = new Vector2(760, 520); // 实际尺寸由 UpdateColumnGeometry 随缩放并 clamp 到视口
        _managementPanel = managementPanel;
        managementCenter.AddChild(managementPanel);
        var managementBox = new VBoxContainer { Name = "ManagementStack" };
        managementBox.AddThemeConstantOverride("separation", 6);
        managementPanel.AddChild(managementBox);
        var managementHeader = new HBoxContainer { Name = "ManagementHeader" };
        managementHeader.AddThemeConstantOverride("separation", 8);
        managementBox.AddChild(managementHeader);
        _managementTitleLabel = TrackLabel("ManagementTitleLabel", "基地管理", SizeHeading, Palette.Text);
        managementHeader.AddChild(_managementTitleLabel);
        _managementMaterialsTabButton = new Button { Name = "ManagementMaterialsTabButton", Text = "基地物资", ToggleMode = true, FocusMode = Control.FocusModeEnum.All };
        _managementWorkTabButton = new Button { Name = "ManagementWorkTabButton", Text = "工程", ToggleMode = true, FocusMode = Control.FocusModeEnum.All };
        StyleButton(_managementMaterialsTabButton, accent: false, focusable: true);
        StyleButton(_managementWorkTabButton, accent: false, focusable: true);
        _sizeTargets.Add((_managementMaterialsTabButton, SizeBody));
        _sizeTargets.Add((_managementWorkTabButton, SizeBody));
        _managementMaterialsTabButton.Toggled += on => { if (on) SwitchManagementTab(true); };
        _managementWorkTabButton.Toggled += on => { if (on) SwitchManagementTab(false); };
        managementHeader.AddChild(_managementMaterialsTabButton);
        managementHeader.AddChild(_managementWorkTabButton);
        var managementSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        managementHeader.AddChild(managementSpacer);
        _managementCloseButton = PanelActionButton("ManagementCloseButton", "关闭", CloseManagement);
        managementHeader.AddChild(_managementCloseButton); // 关闭常驻滚动区外，高倍率不被挤出

        _managementScroll = new ScrollContainer
        {
            Name = "ManagementScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        managementBox.AddChild(_managementScroll);
        _managementMaterialsBox = new VBoxContainer { Name = "ManagementMaterialsBox", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _managementMaterialsBox.AddThemeConstantOverride("separation", 6);
        _managementScroll.AddChild(_managementMaterialsBox);
        _managementMaterialsBox.AddChild(TrackLabel("MatSummaryCaption", "库存口径（库内现货 / 机器人携带 / 加工预计，不跨口径求和）", SizeSmall, Palette.TextDim));
        _matSummaryLabel = InfoLabel("MatSummaryLabel", SizeSmall, Palette.Text);
        _managementMaterialsBox.AddChild(_matSummaryLabel);
        _managementMaterialsBox.AddChild(TrackLabel("MatFilterCaption", "按物资过滤", SizeSmall, Palette.Text));
        _materialFilterRow = new HFlowContainer { Name = "MaterialFilterRow" };
        _materialFilterRow.AddThemeConstantOverride("h_separation", 6);
        _managementMaterialsBox.AddChild(_materialFilterRow);
        _managementMaterialsBox.AddChild(TrackLabel("LocFilterCaption", "按物理位置过滤", SizeSmall, Palette.Text));
        _locationFilterRow = new HFlowContainer { Name = "LocationFilterRow" };
        _locationFilterRow.AddThemeConstantOverride("h_separation", 6);
        _managementMaterialsBox.AddChild(_locationFilterRow);
        BuildLocationFilterChips();
        _managementMaterialsBox.AddChild(TrackLabel("MatLocationsCaption", "按位置（数量=现物，含预留子集）", SizeSmall, Palette.Text));
        _matLocationsLabel = InfoLabel("MatLocationsLabel", SizeSmall, Palette.Text);
        _managementMaterialsBox.AddChild(_matLocationsLabel);
        _managementMaterialsBox.AddChild(TrackLabel("MatReservationsCaption", "预留（真实工程占用）", SizeSmall, Palette.Text));
        _matReservationsLabel = InfoLabel("MatReservationsLabel", SizeSmall, Palette.Text);
        _managementMaterialsBox.AddChild(_matReservationsLabel);
        _managementMaterialsBox.AddChild(TrackLabel("MatRelatedCaption", "关联工程", SizeSmall, Palette.Text));
        _matRelatedLabel = InfoLabel("MatRelatedLabel", SizeSmall, Palette.Text);
        _managementMaterialsBox.AddChild(_matRelatedLabel);
        _matRelatedBox = new VBoxContainer { Name = "MatRelatedBox", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _matRelatedBox.AddThemeConstantOverride("separation", 4);
        _managementMaterialsBox.AddChild(_matRelatedBox);
        _managementMaterialsBox.AddChild(TrackLabel("MatBatchesCaption", "加工批次（预计产出不计入库存）", SizeSmall, Palette.Text));
        _matBatchesLabel = InfoLabel("MatBatchesLabel", SizeSmall, Palette.Text);
        _managementMaterialsBox.AddChild(_matBatchesLabel);

        _managementWorkBox = new VBoxContainer { Name = "ManagementWorkBox", Visible = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _managementWorkBox.AddThemeConstantOverride("separation", 6);
        _managementScroll.AddChild(_managementWorkBox);
        _relatedWorkBox = new VBoxContainer { Name = "RelatedWorkBox", Visible = false };
        _relatedWorkBox.AddThemeConstantOverride("separation", 4);
        _managementWorkBox.AddChild(_relatedWorkBox);
        _relatedWorkBackButton = PanelActionButton("RelatedWorkBackButton", "返回物资", CloseRelatedWorkDetail);
        _relatedWorkBox.AddChild(_relatedWorkBackButton);
        _relatedWorkBox.AddChild(TrackLabel("RelatedWorkCaption", "关联工程详情", SizeSmall, Palette.Text));
        _relatedWorkDetailLabel = InfoLabel("RelatedWorkDetailLabel", SizeSmall, Palette.Text);
        _relatedWorkBox.AddChild(_relatedWorkDetailLabel);
        _managementWorkBox.AddChild(TrackLabel("WorkCurrentCaption", "当前工程", SizeSmall, Palette.Text));
        _workCurrentLabel = InfoLabel("WorkCurrentLabel", SizeSmall, Palette.Text);
        _managementWorkBox.AddChild(_workCurrentLabel);
        _workCancelButton = PanelActionButton("WorkCancelButton", "取消工程…", RequestCancelWork);
        _managementWorkBox.AddChild(_workCancelButton);
        _managementWorkBox.AddChild(TrackLabel("WorkHistoryCaption", "本次已有工程记录", SizeSmall, Palette.Text));
        _workHistoryLabel = InfoLabel("WorkHistoryLabel", SizeSmall, Palette.TextDim);
        _managementWorkBox.AddChild(_workHistoryLabel);

        // 取消确认是管理层内最顶层：透明独占层挡住面板其余控件，确认卡居中唯一可点/可聚焦。
        _cancelShield = new ColorRect { Name = "CancelConfirmShield", Color = new Color(0, 0, 0, 0), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _cancelShield.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(_cancelShield);
        var cancelCard = new CenterContainer { Name = "CancelConfirmCard", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        cancelCard.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(cancelCard);
        _cancelConfirmCard = cancelCard;
        _cancelConfirmBox = new PanelContainer { Name = "CancelConfirmBox", Visible = false };
        _cancelConfirmBox.AddThemeStyleboxOverride("panel", ButtonStyle(Palette.PanelBg.Lightened(0.06f), Palette.Warn, 2));
        cancelCard.AddChild(_cancelConfirmBox);
        var cancelConfirmStack = new VBoxContainer();
        cancelConfirmStack.AddThemeConstantOverride("separation", 6);
        _cancelConfirmBox.AddChild(cancelConfirmStack);
        _cancelConfirmLabel = InfoLabel("CancelConfirmLabel", SizeSmall, Palette.Text);
        cancelConfirmStack.AddChild(_cancelConfirmLabel);
        var cancelConfirmActions = new HBoxContainer();
        cancelConfirmActions.AddThemeConstantOverride("separation", 8);
        _cancelConfirmYesButton = PanelActionButton("CancelConfirmYesButton", "确认取消", ConfirmCancelWork, accent: true);
        _cancelConfirmNoButton = PanelActionButton("CancelConfirmNoButton", "返回", CloseCancelConfirm);
        cancelConfirmActions.AddChild(_cancelConfirmYesButton);
        cancelConfirmActions.AddChild(_cancelConfirmNoButton);
        cancelConfirmStack.AddChild(cancelConfirmActions);

        BuildScreens(hud, root);
    }

    private void BuildTopBar(Control root)
    {
        // 顶栏不使用 TopWide 锚点（高度会被 offset 拉成 0/负值）；几何统一由 UpdateColumnGeometry 按内容驱动。
        var bar = new PanelContainer { Name = "TopBar", MouseFilter = Control.MouseFilterEnum.Stop };
        bar.AddThemeStyleboxOverride("panel", PanelStyle());
        root.AddChild(bar);
        _topBar = bar;
        var box = new HBoxContainer { Name = "TopBarBox" };
        box.AddThemeConstantOverride("separation", 8);
        bar.AddChild(box);
        foreach (var id in TopStockIds)
        {
            var chip = new Button
            {
                Name = "TopStockChip_" + id,
                Text = "—",
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = "打开基地物资并按此物资过滤（库内现货口径）",
            };
            string captured = id;
            StyleButton(chip, accent: false);
            _sizeTargets.Add((chip, SizeSmall));
            chip.Pressed += () =>
            {
                if (_managementOpen && _managementTabMaterials && _materialFilter == captured) { CloseManagement(); return; } // 重复入口关闭同视图
                _materialFilter = captured;
                OpenManagement(true);
            };
            _topStockChips[id] = chip;
            box.AddChild(chip);
        }
        _topPowerLabel = InfoLabel("TopPowerLabel", SizeSmall, Palette.Text);
        _topDayLabel = InfoLabel("TopDayLabel", SizeSmall, Palette.Text);
        _topAlertLabel = InfoLabel("TopAlertLabel", SizeSmall, Palette.Warn);
        box.AddChild(_topPowerLabel);
        box.AddChild(_topDayLabel);
        box.AddChild(_topAlertLabel);
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddChild(spacer);
        _topPauseButton = MakeButton("TopPauseButton", "暂停", () => _world.QueuePlayerAction("pause"));
        _topMenuButton = MakeButton("TopMenuButton", "菜单", () => QueueLifecycle("pause"), commandCooldown: false);
        RegisterGameFocusable(_topPauseButton);
        RegisterGameFocusable(_topMenuButton);
        box.AddChild(_topPauseButton);
        box.AddChild(_topMenuButton);
    }

    // 局内工具按钮：可切换、保留键盘焦点并登记到管理层焦点抑制名单。
    private Button MakeToolToggle(string name, string text, Action<bool> onToggled)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.All,
        };
        StyleButton(button, accent: false, focusable: true);
        _sizeTargets.Add((button, SizeBody));
        button.Toggled += on => onToggled(on);
        _gameFocusables.Add(button);
        return button;
    }

    private Button MakeToolButton(string name, string text, Action command)
    {
        var button = MakeButton(name, text, command, commandCooldown: false);
        button.FocusMode = Control.FocusModeEnum.All;
        StyleButton(button, accent: false, focusable: true);
        _gameFocusables.Add(button);
        return button;
    }

    private void RegisterGameFocusable(Button button) => _gameFocusables.Add(button);

    // 蓝图按钮只在首次读到 Enabled 且有蓝图时建立一次；种类来自配置，不写死。
    private void EnsureBootstrapControls(BootstrapReadModel boot)
    {
        if (_modesBuilt) return;
        _modesBuilt = true;
        _modeGroup = new ButtonGroup();
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
            if (_placementMode != 2) EnterPlacement(2);
            SelectMode(blueprint);
        };
        return button;
    }

    private void SelectMode(BuildBlueprintView? blueprint)
    {
        if (_managementOpen || _cancelConfirmOpen) return; // 管理层/确认层期间动作门禁
        _currentBlueprint = blueprint;
        _buildType = blueprint?.Id;
        _buildRadius = blueprint?.Radius ?? SiteRadiusM;
        if (_placementMode != 2) _placementMode = 2;
        _selectedSite = null;
        _preview = null;
        _plan = null;
        SyncModeToggles();
    }

    private void RotateYaw()
    {
        _yawSteps = (_yawSteps + 1) % 4;
        _rotateButton!.Text = "朝向 " + _yawSteps * 90 + "°";
        _preview = null;
        _plan = null;
    }

    // 工具/蓝图按钮的按压态与状态机同步； setter 只在值变化时发信号，不形成回环。
    private void SyncModeToggles()
    {
        if (_toolBuildButton != null) _toolBuildButton.ButtonPressed = _placementMode == 2;
        if (_toolLevelButton != null) _toolLevelButton.ButtonPressed = _placementMode == 1;
        foreach (var (id, button) in _modeButtons)
            button.ButtonPressed = _placementMode == 2 && id == _buildType;
        if (_rotateButton != null) _rotateButton.Visible = _placementMode == 2;
    }

    // 面板内动作按钮：保留键盘焦点，宽度适配面板；冷却由命令自身负责，避免双重门禁。
    private Button PanelActionButton(string name, string text, Action command, bool accent = false)
    {
        var button = MakeButton(name, text, command, accent, commandCooldown: false);
        button.FocusMode = Control.FocusModeEnum.All;
        StyleButton(button, accent, focusable: true);
        button.CustomMinimumSize = new Vector2(120, 40);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _minSizeTargets.Add((button, new Vector2(120, 40)));
        return button;
    }

    // 两列高度按底部命令栏封顶：常用行默认同屏；只有低频明细超出时才出现滚动。
    private void UpdateColumnGeometry()
    {
        var viewport = _camera.GetViewport().GetVisibleRect().Size;
        _commandPanel.CustomMinimumSize = new Vector2(MathF.Min(980f, viewport.X - 32f), 0);
        _commandBar.OffsetTop = -_commandPanel.GetCombinedMinimumSize().Y - 12f;
        float barTop = _commandBar.GetGlobalRect().Position.Y;
        float topBarHeight = MathF.Max(36f, _topBar.GetCombinedMinimumSize().Y);
        _topBar.Position = new Vector2(8f, 8f);
        _topBar.Size = new Vector2(MathF.Max(200f, viewport.X - 16f), topBarHeight);
        float top = 8f + topBarHeight + 8f;
        float height = MathF.Max(160f, (barTop > top ? barTop : viewport.Y) - top - 8f);
        float leftWidth = MathF.Min(430f, (viewport.X - 160f) * 0.4f);
        float rightWidth = MathF.Min(430f, (viewport.X - 160f) * 0.4f);
        _infoScroll.Position = new Vector2(16f, top);
        _infoScroll.Size = new Vector2(leftWidth, height);
        bool workRow = _workRowPanel.Visible;
        float rowHeight = workRow ? MathF.Max(40f, _workRowPanel.GetCombinedMinimumSize().Y) : 0f;
        _workRowPanel.Position = new Vector2(viewport.X - rightWidth - 16f, top);
        _workRowPanel.Size = new Vector2(rightWidth, rowHeight);
        float contextTop = top + (workRow ? rowHeight + 6f : 0f);
        float contextHeight = MathF.Max(160f, (barTop > contextTop ? barTop : viewport.Y) - contextTop - 8f);
        _planScroll.Position = new Vector2(viewport.X - rightWidth - 16f, contextTop);
        _planScroll.Size = new Vector2(rightWidth, contextHeight);
        _objectScroll.Position = _planScroll.Position;
        _objectScroll.Size = _planScroll.Size;
        _connectScroll.Position = _planScroll.Position;
        _connectScroll.Size = _planScroll.Size;
        // N001 后续：接线预算不在几何阶段计算——那时 RefreshConnectPanel 尚未写入本帧的提示/成本/拒因文本，
        // 读到的是上一帧的核心 min（选择变化帧直接错）。这里只记录当前帧真实可用高度，
        // 列表/拒因块预算由 UpdateConnectLayout 在内容同帧写入后统一分配。
        _connectContextHeight = contextHeight;
        // 核心不会随详情滚走；低频区域只使用核心剩下的高度。
        _infoDetailsScroll.CustomMinimumSize = new Vector2(0, MathF.Max(80f, height - _infoCore.GetCombinedMinimumSize().Y - _infoDetailsToggle.Size.Y - 32f));
        // 建设卡：默认可见的成本滚动区拿固定核心之外的剩余预算（真实像素，clamp 保底 64、封顶 360），
        // 折叠详情只拿成本之后的余量，避免把内容重新顶出视口。
        _planCostScroll.CustomMinimumSize = new Vector2(0, Mathf.Clamp(contextHeight - _planCore.GetCombinedMinimumSize().Y - _planDetailsToggle.Size.Y - 56f, 64f, 360f));
        _planDetailsScroll.CustomMinimumSize = new Vector2(0, MathF.Max(80f, contextHeight - _planCore.GetCombinedMinimumSize().Y
            - _planCostScroll.GetCombinedMinimumSize().Y - _planDetailsToggle.Size.Y - 32f));
        // 管理面板 760x520 基数随缩放实际生效，但不超出视口（留 24px 边距）；溢出由内部滚动接管，
        // 标题/标签/关闭与取消确认卡都在滚动区外，高倍率不把关闭/确认/关键反馈挤出。
        float mgmtWidth = MathF.Min(760f * (float)_uiScale, viewport.X - 24f);
        float mgmtHeight = MathF.Min(520f * (float)_uiScale, viewport.Y - 24f);
        _managementPanel.CustomMinimumSize = new Vector2(MathF.Max(320f, mgmtWidth), MathF.Max(240f, mgmtHeight));
    }

    private void ToggleDiagnostics()
    {
        _diagRow.Visible = !_diagRow.Visible;
        _diagToggle.Text = "诊断 " + (_diagRow.Visible ? "▾" : "▸");
    }

    // ---- 局内 HUD 刷新 ----

    private void RefreshGameHud(PlayerReadModel state, float dt)
    {
        bool visible = _page == "game";
        _hudRoot.Visible = visible;
        if (!visible) return;

        _economyTimer -= dt;
        if (_economyTimer <= 0f || _economy == null)
        {
            _economy = _world.ReadEconomy();
            _economyTimer = EconomyIntervalS;
        }
        _tutorial = _world.ReadTutorial();
        if (_tutorial.StageId != _learnedStage) _learnedStage = _tutorial.StageId;

        RefreshWorkRow();          // 先决定活动工程行可见性，再布局
        UpdateColumnGeometry();
        RefreshTopBar(state);
        RefreshGoalPanel(state);
        RefreshContextColumn(state, dt);
        RefreshManagement(state, dt);
        RefreshDevPanel();
        RefreshBottomBar(state);

        _conclusionCard.Visible = _tutorial is { Enabled: true, ShowConclusion: true, Completed: true };
        if (_conclusionCard.Visible && _tutorial is { } done)
            _conclusionListLabel.Text = "已完成 " + done.Achieved + "/" + done.Total + " 项，世界继续运行；可继续自主经营，或保存后返回主页。";
    }

    // 顶部真实警报：typed 工程状态（受阻/无法继续；等待运输路线/等待工位取 ReasonCode）+ 已建成用电设施未接线的真实事实。
    // 夜间等待日照（WaitingForPower）等其余等待不进顶栏，未接线只按真实 Source 计数；通知原文只作为通知展示；不用中文关键字猜障碍。
    private string[] RealAlerts(PlayerReadModel state)
    {
        var alerts = new List<string>();
        _ = state;
        if (_work is { Active: true } work && (work.Status is PlayerWorkStatus.Blocked or PlayerWorkStatus.Failed
                || work.ReasonCode is PlayerReasonCode.WaitingForRoute or PlayerReasonCode.WaitingForStation))
            alerts.Add("当前工程" + WorkStatusDisplay(work.Status) +
                (work.ReasonCode != PlayerReasonCode.None || work.Reason.Length > 0
                    ? "：" + WorkReasonDisplay(work.ReasonCode, work.Reason) : ""));
        if (_bootstrap is { Enabled: true } boot)
        {
            int unconnected = boot.Facilities.Count(f => f.Built && f.Type is "charger" or "repair" or "processor" && f.Source == null);
            if (unconnected > 0) alerts.Add(unconnected + " 座已建成设施未接线");
        }
        return alerts.ToArray();
    }

    private void RefreshTopBar(PlayerReadModel state)
    {
        var economy = _economy ?? _world.ReadEconomy();
        foreach (var (id, chip) in _topStockChips)
        {
            var stock = Array.Find(economy.Materials, m => m.Id == id);
            chip.Visible = stock != null;
            if (stock is { } s) chip.Text = s.Name + " " + s.Stored; // 只显示库内现货口径
        }
        _topPowerLabel.Text = "发电 " + economy.GenerationPerSecond.ToString("0.##", Inv) + "/s";
        _topDayLabel.Text = economy.Daylight ? "白天" : "夜间";
        var alerts = RealAlerts(state);
        _topAlertLabel.Visible = alerts.Length > 0;
        if (alerts.Length > 0)
            _topAlertLabel.Text = "⚠ " + alerts[0] + (alerts.Length > 1 ? "（+" + (alerts.Length - 1) + "）" : "");
        _topAlertLabel.TooltipText = alerts.Length == 0 ? "" : string.Join("\n", alerts);
        _topPauseButton.Text = state.Paused ? "继续" : "暂停";
        _topPauseButton.Disabled = _commandCooldown > 0f;
    }

    private void RefreshGoalPanel(PlayerReadModel state)
    {
        var tutorial = _tutorial;
        // 教学结尾确认后（Completed 且不再显示结论卡）无活动教学目标：目标卡整块隐藏，不造 pin。
        bool teaching = tutorial is { Enabled: true } live && (!live.Completed || live.ShowConclusion);
        _goalCore.Visible = teaching;
        _goalFoldToggle.Visible = teaching;
        _milestonesToggle.Visible = teaching;
        if (tutorial is not { Enabled: true } t)
        {
            _goalWhyLabel.Visible = false;
            _goalCoverageLabel.Visible = false;
            _goalProgressLabel.Visible = false;
            _milestonesLabel.Visible = false;
            _statusLabel.Visible = false;
            _noticeDetailsLabel.Visible = false;
            ApplyModeHint(null);
            return;
        }

        string notice = state.Notice.Length > 0 ? state.Notice : _lifecycle?.Notice ?? "";
        bool expanded = !_goalCollapsed;
        _statusLabel.Visible = false; // 通知原文只通知（见详情）；警报由顶栏 typed 工程状态决定
        _noticeDetailsLabel.Text = notice;
        _noticeDetailsLabel.Visible = notice.Length > 0;

        _goalTitleLabel.Text = t.Title;
        _goalInstructionLabel.Text = CompactTutorialInstruction(t);
        _instructionDetailsLabel.Text = t.Instruction;
        _goalWhyLabel.Text = "为什么：" + t.Why;
        _goalWhyLabel.Visible = expanded && _helpDetailed && t.Why.Length > 0; // why 在展开的目标核心
        _goalActionLabel.Text = "下一步：" + NextActionDisplay(t.NextAction, t.NeededType) + " · " + t.Achieved + "/" + t.Total;
        _goalInstructionLabel.Visible = expanded;
        _goalActionLabel.Visible = expanded;
        _goalProgressLabel.Text = "教学进度 " + t.Achieved + "/" + t.Total;
        _goalCoverageLabel.Text = CoverageHint(t);
        _goalCoverageLabel.Visible = expanded && _helpDetailed && _goalCoverageLabel.Text.Length > 0;

        var milestones = t.Milestones;
        _milestonesLabel.Text = milestones.Length == 0
            ? "（暂无可核对的里程碑条目）"
            : string.Join("\n", milestones.Select(m => (m.Achieved ? "✓ " : "· ") + m.Title));
        _milestonesToggle.Text = (_milestonesLabel.Visible ? "教学进度明细 ▾" : "教学进度明细 ▸");
        _milestonesToggle.Visible = teaching; // freeplay 结尾确认后随目标卡一起隐藏

        ApplyModeHint(teaching && !t.Completed && ShowButtonHints(t) ? t.NeededType : null);
    }

    private static string CompactTutorialInstruction(TutorialView tutorial) => tutorial.StageId switch
    {
        "observe-team" => "移动镜头，再点选机器人查看电量与耐久。",
        "solar" => "选址与朝向决定12m供电覆盖和运输距离。",
        "charger" or "repair" or "processor" => "在已建太阳能12m内选址，检查成本后建设。",
        "charger-wire" or "repair-wire" or "processor-wire" => tutorial.NextAction == "recover-power"
            ? "规划12m内新太阳能，再明确接线。" : "选设施与电源，付费铺线；建成不等于接通。",
        "first-direction" => "仓储增加容量与供料位置；太阳能增加功率与覆盖。",
        "second-direction" => "自主规划另一方向，比较成本、覆盖与物流。",
        "second-charger" => "新阵列12m内建桩，用新仓储供料。",
        "second-charger-wire" => "明确接入新太阳能，检查12m与线缆成本。",
        "conclusion" or "freeplay" => "继续同一世界经营，或保存返回主页。",
        _ => tutorial.Title,
    };

    // 早期阶段（含冻结的 observe-team 认识镜头/选择）高亮下一合法建设类型按钮并给覆盖提示；
    // 进入发展方向阶段后撤去逐按钮提示。上报学习操作不受此列表限制。
    private static bool ShowButtonHints(TutorialView tutorial)
        => tutorial.StageId is "observe-team" or "solar" or "charger" or "charger-wire" or "repair" or "repair-wire" or "processor" or "processor-wire";

    private static string CoverageHint(TutorialView tutorial)
    {
        if (tutorial.NextAction == "build")
            return tutorial.NeededType switch
            {
                "solar" => "首个太阳能阵列的 12m 覆盖决定后续设施能从哪里取电；准备卡显示实际覆盖距离。",
                "charger" or "repair" or "processor" => "用电设施必须落在已建太阳能的 12m 覆盖内；准备卡显示每座太阳能的距离与范围。",
                "storage" => "仓储位置影响运输距离；准备卡显示各仓库路线距离。",
                _ => "",
            };
        if (tutorial.NextAction == "connect")
            return "接线需明确选择供电源；面板列出每座已建太阳能的距离与范围，不会自动挑选。";
        if (tutorial.NextAction == "recover-power")
            return "为既有设施补覆盖：需要新增太阳能或明确接线，成本按真实材料核算。";
        return "";
    }

    private void ApplyModeHint(string? type)
    {
        if (_hintedModeType == type) return;
        _hintedModeType = type;
        foreach (var (id, button) in _modeButtons)
        {
            bool hinted = type != null && id == type;
            button.AddThemeStyleboxOverride("normal", hinted ? ButtonStyle(Palette.PanelBg, Palette.Hint, 3) : ButtonStyle(Palette.PanelBg));
            button.AddThemeStyleboxOverride("hover", hinted ? ButtonStyle(Palette.PanelBg.Lightened(0.1f), Palette.Hint, 3) : ButtonStyle(Palette.PanelBg.Lightened(0.1f)));
            button.AddThemeStyleboxOverride("pressed", ButtonStyle(Palette.PanelBg.Darkened(0.08f)));
            button.AddThemeColorOverride("font_color", Palette.Text);
            button.AddThemeColorOverride("font_hover_color", Palette.Text);
            button.AddThemeColorOverride("font_pressed_color", Palette.Text);
            button.AddThemeColorOverride("font_disabled_color", Palette.TextDim);
        }
    }

    // 独立活动工程一行：只消费权威 ReadWork；点击进入工程管理。
    private void RefreshWorkRow()
    {
        bool active = _work is { Active: true };
        _workRowPanel.Visible = active;
        if (_work is not { Active: true } work) return;
        string progress = work.Progress is { } p && work.ProgressBasis != PlayerProgressBasis.None
            ? string.Format(Inv, " · {0:P0}", p) : "";
        string status = work.Status switch
        {
            PlayerWorkStatus.Waiting => "（等待）", PlayerWorkStatus.Blocked => "（受阻）",
            PlayerWorkStatus.Failed => "（无法继续）", _ => "",
        };
        _workRowLabel.Text = "当前工程：" + WorkKindDisplay(work.Kind) + " · " + WorkStageDisplay(work.Stage) + status + progress +
            (work.Reason.Length > 0 ? " · " + work.Reason : "");
    }

    // 右侧唯一语境栏：对象卡 / 选址准备卡 / 接线互斥。
    private void RefreshContextColumn(PlayerReadModel state, float dt)
    {
        bool connectOpen = _connectPanelOpen;
        bool placement = PlacementActive;
        bool objectShow = _selectedObjectId != null && !placement;
        _connectScroll.Visible = connectOpen;
        _planScroll.Visible = placement && !connectOpen;
        _objectScroll.Visible = objectShow && !connectOpen;
        if (connectOpen) { RefreshConnectPanel(dt); return; }
        if (placement) RefreshPlacementCard(state);
        if (objectShow) RefreshObjectPanel(state, dt);
    }

    private void RefreshObjectPanel(PlayerReadModel state, float dt)
    {
        if (_selectedObjectId is not { } id)
        {
            _objectDetail = null;
            return;
        }
        _objectTimer -= dt;
        if (_objectTimer <= 0f || _objectDetailEpoch != _lastEpoch || _objectDetail == null)
        {
            _objectDetail = _world.ReadObjectDetail(_selectedKind, id, _lastEpoch); // 每次按 epoch 重新解析权威
            _objectDetailEpoch = _lastEpoch;
            _objectTimer = DetailIntervalS;
        }
        if (_objectDetail is not { } detail)
        {
            _selectedObjectId = null; // 旧 epoch 或对象已消失：清选，不显示空板
            return;
        }
        _objectKindLabel.Text = detail.Object.Kind switch
        {
            WorldObjectKind.Mine => "矿点", WorldObjectKind.Robot => "机器人", _ => "设施",
        };
        _objectTitleLabel.Text = detail.Object.Name;
        _objectDescLabel.Text = detail.Description;
        _objectDescLabel.Visible = detail.Description.Length > 0;
        _objectBodyLabel.Text = DescribeObjectDetail(detail);
    }

    private string DescribeObjectDetail(PlayerObjectDetailView detail)
    {
        var lines = new List<string>();
        switch (detail)
        {
            case { Mine: { } mine }:
                lines.Add("剩余储量 " + mine.Remaining.ToString(Inv));
                if (mine.Products is { Length: > 0 } products)
                    lines.Add("可加工为 " + CostText(products) + "（机器人沿真实加工链生产）");
                if (mine.Prerequisite.Length > 0) lines.Add("前置 " + mine.Prerequisite);
                break;
            case { Robot: { } robot }:
                lines.Add(string.Format(Inv, "电量 {0:0.#}/{1:0} · 耐久 {2:0.#}/{3:0}", robot.Energy, robot.Capacity, robot.Durability, robot.Capacity));
                lines.Add("分工 " + robot.Role + " · 状态 " + robot.State + (robot.Reason.Length > 0 ? "：" + robot.Reason : ""));
                if (robot.Cargo is { Length: > 0 } cargo) lines.Add("携带 " + CostText(cargo));
                break;
            case { Facility: { } facility }:
                lines.Add(facility.Built ? "已建成" : "建设中");
                string connectState = facility.Connected
                    ? "已接线" + (facility.SourceId is { } source ? " ← " + FacilitySourceName(source) : "")
                    : "未接线";
                lines.Add(connectState + " · " + facility.OperatingState);
                if (facility.Stored is { Length: > 0 } stored) lines.Add("库内 " + CostText(stored));
                break;
        }
        lines.Add(string.Format(Inv, "位置 ({0:F1}, {1:F1})", detail.Object.Position.X, detail.Object.Position.Z));
        return string.Join("\n", lines);
    }

    private void RefreshPlacementCard(PlayerReadModel state)
    {
        bool inBuildMode = _placementMode == 2;
        bool building = Building; // 建设模式且已选蓝图
        _planTitleLabel.Text = !inBuildMode ? "整地选址"
            : building ? "准备建设：" + (_currentBlueprint?.Name ?? TypeDisplay(_buildType ?? ""))
            : "准备建设：先选择蓝图";
        if (!inBuildMode)
            _selectionLabel.Text = _selectedSite is { } site
                ? string.Format(Inv, "已选位置 ({0:F1}, {1:F1}) · 左键仅选址，筑垒自动执行", site.X, site.Z)
                : "瞄准地面选择位置 · 整平由有能力的筑垒自动执行，无需先选机器人";
        else if (!building)
            _selectionLabel.Text = "建设模式：先在下方蓝图行选择类型；选定前不选址、不预览、不提交";
        else
            _selectionLabel.Text = _selectedSite is { } site
                ? string.Format(Inv, "已选位置 ({0:F1}, {1:F1}) · 朝向 {2}° · 左键仅选址", site.X, site.Z, _yawSteps * 90)
                : "瞄准地面选择位置 · 左键仅选址，不选机器人";

        bool jobActive = state.Job is { Active: true };
        var preview = _preview;
        var plan = _plan;
        bool busy = _lifecycle?.Busy == true;
        bool canConfirm = building
            ? (plan is { } p ? p.CanSubmit : preview is { Legal: true }) && state.Ready && !busy
            : !inBuildMode && preview is { Legal: true } && !jobActive && state.Ready && !busy;
        _confirmButton.Text = inBuildMode ? "开始建造" : "确认整平";
        _confirmButton.Disabled = !canConfirm || _commandCooldown > 0f;

        // 当前阻塞固定可见：建设未选蓝图 / 候选非法 / 不可提交 / 有活动工程时整地不可下。
        var blockers = new List<string>();
        if (inBuildMode && !building)
            blockers.Add("先在下方蓝图行选择建设类型；选定前无预览、不可提交，也不会整地 fallback");
        else
        {
            if (!building && jobActive) blockers.Add("先完成或取消当前工程");
            if (preview is { } pv && !pv.Legal && pv.Reason.Length > 0) blockers.Add(pv.Reason);
            if (building)
            {
                if (plan is not { } draft && preview is { Legal: true })
                    blockers.Add("选择位置后显示完整成本、缺口与后果");
                if (plan is { } d)
                {
                    if (!d.CandidateLegal && d.CandidateReason.Length > 0) blockers.Add(d.CandidateReason);
                    if (!d.CanSubmit && d.SubmitReason.Length > 0 && d.SubmitReason != d.CandidateReason) blockers.Add(d.SubmitReason);
                }
            }
        }
        // N002：preview 与草案可能给出同一条拒因；按首次出现精确保序去重，不同原因都保留。
        _planReasonLabel.Text = string.Join("；", blockers.Distinct());
        _planReasonLabel.Visible = blockers.Count > 0;
        _planReasonScroll.Visible = blockers.Count > 0; // 固定区内的拒因小滚动块：完整拒因不截断

        _planTagLabel.Text = building
            ? (canConfirm ? "可提交 · 尚未下达" : "尚未下达")
            : inBuildMode ? "尚未下达 · 先选蓝图"
            : (canConfirm ? "可整平 · 尚未下达" : "尚未下达");
        _planTagLabel.Visible = true;
        _planAutoLabel.Visible = building;

        if (building && plan is { } ready)
        {
            _planCostScroll.Visible = true; // 默认可见的内部成本滚动区：完整需/已有/缺口/供料/后果可查
            _planCostLabel.Text = CostEligibleGapText(ready);
            _planCostLabel.Visible = true;
            var supplyLines = new List<string>();
            var chosenRoute = ready.SupplyId is { } supplyId ? Array.Find(ready.SupplyRoutes, r => r.SupplyId == supplyId) : null;
            if (chosenRoute is { } route)
                supplyLines.Add("供料 " + route.Name + " 距离 " + route.RouteDistanceM.ToString("0.#", Inv) + "m · " + (route.RouteFound ? "路线可达" : "路线不可达"));
            else if (ready.SupplyId is { } supplyText)
                supplyLines.Add("供料 " + supplyText);
            _planSupplyLabel.Text = string.Join("\n", supplyLines);
            _planSupplyLabel.Visible = supplyLines.Count > 0;
            _planPowerLabel.Text = ready.SourceId is { } sourceId && Array.Find(ready.PowerSources, s => s.SourceId == sourceId) is { } source
                ? "电源 " + source.Name + " " + source.DistanceM.ToString("0.#", Inv) + "/" + source.RangeM.ToString("0.#", Inv) + "m · " +
                  (source.InRange ? "覆盖" : "超出范围")
                : ready.PowerSources.Length == 0 ? "尚无已建太阳能提供覆盖" : "";
            _planPowerLabel.Visible = _planPowerLabel.Text.Length > 0;
            _planConsequenceLabel.Text = ready.Consequence;
            _planConsequenceLabel.Visible = ready.Consequence.Length > 0;
            _planRecoveryLabel.Text = ready.Recovery ? "恢复路径：为既有保障设施补覆盖/接线" : "";
            _planDetailsToggle.Visible = true;
            var extras = new List<string>();
            if (ready.Stock.Length > 0)
                extras.Add("规划可用（含携带与加工预计，未证明全图路线可达；可提交的已有量见成本行）：\n" +
                    string.Join("\n", ready.Stock.Select(s => s.Name + " " + s.Available + "（携带" + s.InTransit + " 预计" + s.InProcess + "）")));
            foreach (var extra in ready.SupplyRoutes.Where(r => r != chosenRoute))
                extras.Add("备选 " + extra.Name + " " + extra.RouteDistanceM.ToString("0.#", Inv) + "m · " + (extra.RouteFound ? "可达" : "不可达"));
            foreach (var other in ready.PowerSources.Where(s => s.SourceId != ready.SourceId).Take(4))
                extras.Add("太阳能 " + other.Name + " " + other.DistanceM.ToString("0.#", Inv) + "m/" + other.RangeM.ToString("0.#", Inv) + "m" + (other.InRange ? " · 覆盖" : " · 超范围"));
            _planStockLabel.Text = string.Join("\n", extras);
            _planStockLabel.Visible = extras.Count > 0;
        }
        else
        {
            _planCostScroll.Visible = false;
            _planCostLabel.Visible = false;
            _planSupplyLabel.Visible = false;
            _planPowerLabel.Visible = false;
            _planConsequenceLabel.Visible = false;
            _planRecoveryLabel.Visible = false;
            _planStockLabel.Visible = false;
            _planDetailsToggle.Visible = false;
            _planDetailsScroll.Visible = false;
        }
    }

    // 建设卡成本行：完整真实成本 + authority 同资格已有（EligibleStock）+ 缺口，同口径不跨账。
    private static string CostEligibleGapText(BuildPlanView draft)
    {
        var lines = new List<string>();
        foreach (var cost in draft.Cost.Where(c => c.Quantity != 0))
        {
            int eligible = draft.EligibleStock is { } elig ? Array.Find(elig, e => e.Id == cost.Id)?.Quantity ?? 0 : -1;
            int gap = Array.Find(draft.Gap, g => g.Id == cost.Id)?.Quantity ?? 0;
            string gapText = gap > 0 ? " · 还需补 " + gap : " · 缺口无";
            lines.Add(eligible >= 0
                ? string.Format(Inv, "{0} 需 {1} · 已有 {2}{3}", cost.Name, cost.Quantity, eligible, gapText)
                : cost.Name + " 需 " + cost.Quantity + gapText);
        }
        return lines.Count == 0 ? "无成本" : string.Join("\n", lines);
    }

    private void RefreshDevPanel()
    {
        // 设施折叠：开关随 bootstrap 显隐；开关收起时明细不可见（与 dev 启用状态无关）。
        _facilityToggle.Visible = BootstrapOn;
        if (!_facilityToggle.Visible) _facilityLabel.Visible = false;
        if (_bootstrap is { } boot)
        {
            bool daylight = _economy?.Daylight ?? true;
            // 连接状态只对需要接线的设施显示；仓库/着陆器/太阳能显示各自真实运行，不标"缺电"。
            _facilityLabel.Text = boot.Facilities.Length == 0 ? "暂无设施" : string.Join("\n", boot.Facilities.Select(f =>
            {
                string state = f.Type is "charger" or "repair" or "processor"
                    ? (f.ServiceConnected ? "已接线" + (f.Source is { } s ? " ←" + FacilitySourceName(s) : "") : "未接线")
                    : f.Type == "solar" ? (f.Built ? (daylight ? "日照发电" : "夜间等待日照") : "建设中")
                    : f.Type is "lander" or "storage" ? (f.Built ? "仓内现货可供料；不需要供电接线" : "建设中")
                    : (f.Built ? "已建成" : "建设中");
                return $"{f.Name} · {state}";
            }));
        }
        var elapsed = TimeSpan.FromSeconds(Math.Max(0, _world.ReadPlayerState().TimeSeconds));
        _worldLabel.Text = string.Format(Inv, "{0}分{1:00}秒 · 槽 {2} · 存档 {3} · {4}{5}",
            (int)elapsed.TotalMinutes, elapsed.Seconds, _lifecycle?.ActiveSlot ?? 0,
            _world.ReadPlayerState().SaveExists ? "已有" : "无",
            _world.ReadPlayerState().Paused ? "已暂停" : "运行中", _world.ReadPlayerState().Ready ? "" : " · 世界恢复中");
        _stockDetailsLabel.Visible = false;
        if (_economy is { } economy && economy.Materials.Length > 0)
        {
            // 完整口径明细回看：库内现货 / 机器人携带 / 加工预计；不显示跨口径合计。
            _stockDetailsLabel.Text = string.Join("\n", economy.Materials.Select(m =>
                m.Name + " 存" + m.Stored + " · 携带" + m.InTransit + " · 加工预计" + m.InProcess));
            _stockDetailsLabel.Visible = _helpDetailed;
        }

        // 发展只读面板：核心行常驻两行内；已备/矿点/两方向长明细进折叠。
        var dev = _world.ReadDevelopment();
        bool devOn = dev.Enabled;
        _devTitleLabel.Visible = devOn;
        _devBodyLabel.Visible = devOn;
        _devDirectionsToggle.Visible = devOn;
        if (!devOn)
        {
            _devDirectionsLabel.Visible = false;
            return;
        }
        _devTitleLabel.Text = "发展 · " + dev.Provider;
        string core = dev.Goal.Length > 0 ? "目标 " + dev.Goal : "";
        if (dev.Stage.Length > 0) core += (core.Length > 0 ? " · " : "") + "当前 " + dev.Stage;
        if (dev.Need.Length > 0) core += "\n净缺口 " + dev.Need;
        if (dev.Reason.Length > 0) core += "\n下一步 " + dev.Reason;
        _devBodyLabel.Text = core;
        var detail = new List<string>();
        if (dev.Directions is { Length: > 0 } dirs)
            detail.Add(string.Join("\n\n", dirs.Select(d =>
                $"{d.Name} · {(d.Feasible ? "可行" : "暂不可行")}\n成本 {d.Cost}\n缺口 {d.Need}\n后果 {d.Consequence}" +
                (d.Reason.Length > 0 ? "\n" + d.Reason : ""))));
        if (dev.Choices.Length > 0) detail.Add("后续方向 " + dev.Choices);
        if (dev.Supply.Length > 0) detail.Add("供给 " + dev.Supply);
        if (dev.Transit.Length > 0) detail.Add("在途 " + dev.Transit);
        if (dev.InProcess.Length > 0) detail.Add("在制 " + dev.InProcess);
        if (dev.Prepared.Length > 0) detail.Add("已备 " + dev.Prepared);
        if (dev.Mines.Length > 0) detail.Add("矿点 " + dev.Mines);
        _devDirectionsLabel.Text = string.Join("\n\n", detail);
        _devDirectionsToggle.Visible = detail.Count > 0;
        if (detail.Count == 0) _devDirectionsLabel.Visible = false; // 开关隐藏时明细一并收起
    }

    private void RefreshBottomBar(PlayerReadModel state)
    {
        bool boot = BootstrapOn;
        if (boot) EnsureBootstrapControls(_bootstrap!);
        _toolBuildButton.Visible = boot;
        _toolLevelButton.Visible = boot;
        _toolLevelButton.Disabled = !boot || state.Job is { Active: true };
        _toolBuildButton.Disabled = !boot;
        SyncModeToggles();
        _rowModes.Visible = _placementMode == 2;
        _hintsLabel.Text = _helpDetailed ? HelpDetailedText : HelpBriefText;

        bool bootDiag = boot;
        _connectOpenButton.Visible = bootDiag;
        // 诊断折叠与旧档/故障恢复/自动接线只在明确 GUI 工程/诊断 flag 下出现；普通包不显示。
        bool engDiag = System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1"
            || System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_ENG_DIAG") == "1";
        _diagToggle.Visible = engDiag;
        if (!engDiag) _diagRow.Visible = false;
        _retryButton.Visible = bootDiag;
        _connectNextButton.Visible = bootDiag;
        var dev = _world.ReadDevelopment();
        _restockButton.Visible = dev.Enabled; // 真实经营动作保持常规入口，不随诊断一起误藏
        _legacyButton.Visible = engDiag && dev.Enabled;
        _legacyButton.Disabled = !state.Ready;
    }

    // ---- 管理覆盖层 ----

    private void SwitchManagementTab(bool materials)
    {
        if (!_managementOpen || _managementTabMaterials == materials) return;
        _managementTabMaterials = materials; // 同一覆盖层内的标签切换，不叠层
        _cancelConfirmOpen = false;
        SyncCancelConfirmVisibility();
        _managementTimer = 0f;
        SyncManagementTabButtons();
    }

    private void SyncManagementTabButtons()
    {
        _managementMaterialsTabButton.ButtonPressed = _managementOpen && _managementTabMaterials;
        _managementWorkTabButton.ButtonPressed = _managementOpen && !_managementTabMaterials;
        _managementMaterialsBox.Visible = _managementOpen && _managementTabMaterials;
        _managementWorkBox.Visible = _managementOpen && !_managementTabMaterials;
    }

    private void RefreshManagement(PlayerReadModel state, float dt)
    {
        _ = state; // 管理层只消费自己的节流缓存
        _managementOverlay.Visible = _managementOpen;
        SyncCancelConfirmVisibility();
        if (!_managementOpen) return;
        if (_cancelFocusPending)
        {
            _cancelFocusPending = false;
            _cancelConfirmNoButton.GrabFocus(); // 取消确认默认焦点在安全项（返回）
        }
        if (_managementFocusPending)
        {
            _managementFocusPending = false;
            SyncManagementTabButtons();
            _managementCloseButton.GrabFocus();
        }
        _managementTimer -= dt;
        if (_managementTimer <= 0f)
        {
            _materialsView = _world.ReadMaterialsDetail();
            _work = _world.ReadWork();
            _workHistory = _world.ReadWorkHistory();
            _definitions ??= _world.ReadDefinitions(); // 定义按世界缓存；epoch 变化时已清
            _managementTimer = ManagementIntervalS;
            RebuildManagementFilters();
        }
        if (_managementTabMaterials) RefreshManagementMaterials();
        else RefreshManagementWork();
    }

    private void RebuildManagementFilters()
    {
        var defs = _definitions;
        string signature = defs is { } d ? string.Join(",", d.Materials.Select(m => m.Id)) : "";
        if (signature == _managementFilterSignature) { RefreshFilterChipStyles(); return; }
        _managementFilterSignature = signature;
        foreach (var (_, button) in _materialFilterChips)
        {
            _materialFilterRow.RemoveChild(button);
            button.QueueFree();
        }
        _materialFilterChips.Clear();
        AddFilterChip(_materialFilterRow, _materialFilterChips, "", "全部", () => _materialFilter = "");
        if (defs is { } definitions)
            foreach (var material in definitions.Materials)
            {
                string captured = material.Id;
                AddFilterChip(_materialFilterRow, _materialFilterChips, captured, material.Name, () => _materialFilter = captured);
            }
        RefreshFilterChipStyles();
    }

    private void BuildLocationFilterChips()
    {
        AddFilterChip(_locationFilterRow, _locationFilterChips, "", "全部", () => _materialLocationFilter = "");
        foreach (var (kind, label) in new[]
        {
            (MaterialContainerKind.Warehouse, "仓库"), (MaterialContainerKind.RobotCargo, "机器人携带"),
            (MaterialContainerKind.Mine, "矿点"), (MaterialContainerKind.Worksite, "工地"),
            (MaterialContainerKind.Processing, "加工"),
        })
        {
            string captured = label;
            AddFilterChip(_locationFilterRow, _locationFilterChips, captured, label, () => _materialLocationFilter = captured);
        }
    }

    private void AddFilterChip(HFlowContainer row, List<(string, Button)> registry, string key, string label, Action apply)
    {
        // 普通按钮（非 toggle）：选中态由 HighlightChip 按筛选状态给出，不能让 toggle 的 pressed 样式盖住选中高亮。
        var chip = new Button
        {
            Name = "FilterChip_" + (key.Length == 0 ? "all" : key),
            Text = label,
            FocusMode = ManagementFocusMode(),
        };
        StyleButton(chip, accent: false, focusable: true);
        _sizeTargets.Add((chip, SizeSmall));
        chip.Pressed += () => { apply(); RefreshFilterChipStyles(); };
        row.AddChild(chip);
        registry.Add((key, chip));
    }

    private void RefreshFilterChipStyles()
    {
        foreach (var (key, button) in _materialFilterChips)
            HighlightChip(button, _materialFilter == key);
        foreach (var (key, button) in _locationFilterChips)
            HighlightChip(button, _materialLocationFilter == key);
    }

    // 选中态覆盖 normal/hover/focus 与字色；选中时焦点边框用深色，避免 Accent 边框落在 Accent 底色上消失。
    private void HighlightChip(Button chip, bool on)
    {
        var fill = on ? Palette.Accent : Palette.PanelBg;
        var text = on ? Palette.AccentText : Palette.Text;
        chip.AddThemeStyleboxOverride("normal", ButtonStyle(fill));
        chip.AddThemeStyleboxOverride("hover", ButtonStyle(fill.Lightened(0.1f)));
        chip.AddThemeStyleboxOverride("focus", ButtonStyle(fill, on ? Palette.Text : Palette.Accent, 2));
        chip.AddThemeColorOverride("font_color", text);
        chip.AddThemeColorOverride("font_hover_color", text);
        chip.AddThemeColorOverride("font_focus_color", text);
    }

    private void GrabManagementFocus() => _managementFocusPending = true; // 等可见后下一帧抢焦点

    private void RefreshManagementMaterials()
    {
        var view = _materialsView;
        if (view == null)
        {
            _matSummaryLabel.Text = "读取中…";
            return;
        }
        // 顶部条与顶栏同口径：Stored=库内现货；InTransit=机器人携带；InProcess=加工预计。不跨口径求和。
        var summary = new List<string>();
        foreach (var m in view.Summary)
        {
            if (_materialFilter.Length > 0 && m.Id != _materialFilter) continue;
            string description = _definitions is { } defs && Array.Find(defs.Materials, d => d.Id == m.Id) is { } def && def.Description.Length > 0
                ? " — " + def.Description : "";
            summary.Add($"{m.Name}：库内现货 {m.Stored} · 机器人携带 {m.InTransit} · 加工预计产出 {m.InProcess}{description}");
        }
        _matSummaryLabel.Text = summary.Count == 0 ? "（无匹配物资口径数据）" : string.Join("\n", summary);

        var locations = new List<string>();
        foreach (var container in view.Locations)
        {
            if (_materialLocationFilter.Length > 0 && ContainerKindDisplay(container.Kind) != _materialLocationFilter) continue;
            var mats = container.Materials.Where(x => _materialFilter.Length == 0 || x.Id == _materialFilter).ToArray();
            if (mats.Length == 0) continue;
            string pos = container.Position is { } p ? string.Format(Inv, " · ({0:F0}, {1:F0})", p.X, p.Z) : "";
            locations.Add($"{container.Name} · {ContainerKindDisplay(container.Kind)}{pos} · 容量 {container.Capacity}\n  " +
                string.Join("\n  ", mats.Select(x => $"{x.Name} {x.Quantity}（预留 {x.Reserved} · 未预留 {x.Unreserved}）")));
        }
        _matLocationsLabel.Text = locations.Count == 0 ? "（无匹配位置；库存可能都在机器人携带或加工链中）" : string.Join("\n", locations);

        var reservations = view.Reservations
            .Where(r => _materialFilter.Length == 0 || r.MaterialId == _materialFilter)
            .ToArray();
        _matReservationsLabel.Text = reservations.Length == 0
            ? "（当前没有真实工程预留）"
            : string.Join("\n", reservations.Select(r =>
                $"{WorkNameForGoal(r.GoalId)} 在 {ContainerName(view, r.ContainerId)} 预留 {r.Name}×{r.Quantity}"));

        // 关联工程：预约按真实 GoalId/TaskId 解析到工程记录；有记录的行提供实际按钮进入同层详情。
        var relatedGoals = reservations.Select(r => r.GoalId).Distinct().ToList();
        string relatedSignature = string.Join("|", relatedGoals.Select(g =>
            g + ":" + (FindWorkForReservation(g) is { } match ? match.Name + ":" + match.Stage : "-")));
        if (relatedSignature != _relatedSignature)
        {
            _relatedSignature = relatedSignature;
            RebuildRelatedWorkRows(relatedGoals);
        }
        _matRelatedLabel.Visible = relatedGoals.Count == 0;
        _matRelatedLabel.Text = "（无关联工程）";

        var batches = view.Batches
            .Where(b => _materialFilter.Length == 0 || b.Input.Any(i => i.Id == _materialFilter) || b.ExpectedOutput.Any(o => o.Id == _materialFilter))
            .ToArray();
        _matBatchesLabel.Text = batches.Length == 0
            ? "（当前没有加工批次）"
            : string.Join("\n", batches.Select(b =>
            {
                string recipe = _definitions is { } defs && Array.Find(defs.Recipes, r => r.Id == b.RecipeId) is { } recipeDef ? recipeDef.Name : b.RecipeId;
                return $"{recipe} @ {FacilitySourceName(b.FacilityId)} · {(b.Paid ? "已付料" : "未付料")} · {WorkStageDisplay(b.Stage)}" +
                    (b.Progress is { } progress ? string.Format(Inv, " · {0:P0}", progress) : "") +
                    "\n  投入 " + CostText(b.Input) + " · 预计产出 " + CostText(b.ExpectedOutput) + "（不计入现货）";
            }));
    }

    // 预约按真实 GoalId/TaskId 解析到工程记录；解析不到就明说无记录，不向玩家展示内部目标 ID。
    private PlayerWorkView? FindWorkForReservation(string goalId)
        => _work is { } current && (current.GoalId == goalId || current.TaskId == goalId) ? current
            : Array.Find(_workHistory, w => w.GoalId == goalId || w.TaskId == goalId);

    private string WorkNameForGoal(string goalId)
        => FindWorkForReservation(goalId) is { } work ? work.Name : "预留工程（尚无记录）";

    private void RebuildRelatedWorkRows(List<string> goals)
    {
        foreach (var row in _relatedWorkRows)
        {
            _matRelatedBox.RemoveChild(row.Row);
            row.Row.QueueFree();
        }
        _relatedWorkRows.Clear();
        foreach (string goal in goals)
        {
            var work = FindWorkForReservation(goal);
            var rowBox = new HBoxContainer { Name = "RelatedWorkRow" };
            rowBox.AddThemeConstantOverride("separation", 8);
            var label = InfoLabel("RelatedWorkRowLabel", SizeSmall, Palette.Text);
            label.Text = work is null
                ? "预留对应的工程尚无记录"
                : WorkKindDisplay(work.Kind) + " · " + work.Name + " · " + WorkStageDisplay(work.Stage) + " · " + WorkStatusDisplay(work.Status) +
                  (work.Reason.Length > 0 ? " · " + work.Reason : "");
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rowBox.AddChild(label);
            Button? openButton = null;
            if (work != null)
            {
                string captured = goal;
                openButton = MakeButton("RelatedWorkButton", "查看关联工程", () => OpenRelatedWorkDetail(captured), commandCooldown: false);
                openButton.FocusMode = ManagementFocusMode();
                StyleButton(openButton, accent: false, focusable: true);
                openButton.CustomMinimumSize = new Vector2(150, 32);
                _minSizeTargets.Add((openButton, new Vector2(150, 32)));
                rowBox.AddChild(openButton);
            }
            _matRelatedBox.AddChild(rowBox);
            _relatedWorkRows.Add((rowBox, openButton));
        }
    }

    // 同层进入关联工程详情：切到工程标签并展开该工程；Esc/返回按钮先退该子详情回物资标签。
    private void OpenRelatedWorkDetail(string goalId)
    {
        if (FindWorkForReservation(goalId) == null) return;
        _relatedWorkGoalId = goalId;
        if (_managementOpen) SwitchManagementTab(false);
    }

    private void CloseRelatedWorkDetail()
    {
        _relatedWorkGoalId = null;
        _relatedWorkBox.Visible = false;
        if (_managementOpen && !_managementTabMaterials) SwitchManagementTab(true);
    }

    private void CloseCancelConfirm()
    {
        _cancelConfirmOpen = false;
        _cancelGoalId = null;
        SyncCancelConfirmVisibility();
    }

    private void SyncCancelConfirmVisibility()
    {
        bool open = _managementOpen && _cancelConfirmOpen;
        _cancelShield.Visible = open;
        _cancelConfirmCard.Visible = open;
        _cancelConfirmBox.Visible = open; // 卡内面板属性同步，供测试/调试读取真实开关态
        SyncManagementFocus(open);
    }

    // 取消确认是管理层唯一的指针/焦点层（Esc 只退该层）：打开时其余管理控件（含动态筛选芯片与关联入口）不可聚焦，关闭后恢复。
    private void SyncManagementFocus(bool suppressed)
    {
        if (_managementFocusSuppressed == suppressed) return;
        _managementFocusSuppressed = suppressed;
        foreach (var button in ManagementFocusables()) button.FocusMode = ManagementFocusMode();
    }

    private Control.FocusModeEnum ManagementFocusMode()
        => _managementFocusSuppressed ? Control.FocusModeEnum.None : Control.FocusModeEnum.All;

    private IEnumerable<Button> ManagementFocusables()
    {
        yield return _managementMaterialsTabButton;
        yield return _managementWorkTabButton;
        yield return _managementCloseButton;
        yield return _workCancelButton;
        yield return _relatedWorkBackButton;
        foreach (var (_, chip) in _materialFilterChips) yield return chip;
        foreach (var (_, chip) in _locationFilterChips) yield return chip;
        foreach (var (_, relatedOpen) in _relatedWorkRows)
            if (relatedOpen != null) yield return relatedOpen;
    }

    private static string ContainerName(PlayerMaterialsView view, string containerId)
        => Array.Find(view.Locations, c => c.Id == containerId) is { } container ? container.Name : containerId;

    private void RefreshManagementWork()
    {
        // 关联工程子详情：从物资预约进入；目标消失时自动收起子层。
        if (_relatedWorkGoalId is { } wantedGoal)
        {
            if (FindWorkForReservation(wantedGoal) is not { } related)
            {
                _relatedWorkGoalId = null;
                _relatedWorkBox.Visible = false;
            }
            else
            {
                string relatedProgress = related.Progress is { } rz && related.ProgressBasis != PlayerProgressBasis.None
                    ? string.Format(Inv, "\n进度 {0:P0}", rz) : "";
                _relatedWorkBox.Visible = true;
                _relatedWorkDetailLabel.Text = WorkKindDisplay(related.Kind) + " · " + related.Name +
                    "\n阶段 " + WorkStageDisplay(related.Stage) + " · " + WorkStatusDisplay(related.Status) + relatedProgress +
                    (related.Reason.Length > 0 || related.ReasonCode != PlayerReasonCode.None
                        ? "\n" + WorkReasonDisplay(related.ReasonCode, related.Reason)
                        : "") +
                    "\n执行者 " + (related.WorkerIds.Length == 0 ? "未分配" : string.Join("、", related.WorkerIds.Select(WorkerDisplay)));
            }
        }
        else _relatedWorkBox.Visible = false;

        if (_work is { Active: true } work)
        {
            string progress = work.Progress is { } p && work.ProgressBasis != PlayerProgressBasis.None
                ? string.Format(Inv, " · 进度 {0:P0}", p) : "";
            _workCurrentLabel.Text = WorkKindDisplay(work.Kind) + " · " + work.Name +
                "\n阶段 " + WorkStageDisplay(work.Stage) + " · " + WorkStatusDisplay(work.Status) + progress +
                (work.Reason.Length > 0 || work.ReasonCode != PlayerReasonCode.None
                    ? "\n" + WorkReasonDisplay(work.ReasonCode, work.Reason)
                    : "") +
                "\n执行者 " + (work.WorkerIds.Length == 0 ? "未分配" : string.Join("、", work.WorkerIds.Select(WorkerDisplay)));
            _workCancelButton.Visible = true;
            _workCancelButton.Disabled = false;
            // N004：关联工程详情打开时，按钮必须指向/显示明确当前目标——正在查看的是历史关联
            // 也不默默取消当前不同目标，按钮文本直接点名当前工程。
            bool viewingOther = _relatedWorkGoalId is { } related && related != work.GoalId && related != work.TaskId;
            _workCancelButton.Text = viewingOther ? "取消当前工程「" + work.Name + "」…" : "取消工程…";
        }
        else
        {
            _workCurrentLabel.Text = "当前没有进行中的工程；返回局内用底部工具选择整地或建设位置后下达。";
            _workCancelButton.Visible = false;
        }

        // 确认卡的可见性由 SyncCancelConfirmVisibility 统一管理（独占顶层卡）；这里只刷新文本。
        if (_cancelConfirmOpen)
        {
            if (_cancelGoalId is { } goal && _work is { Active: true } current && current.GoalId == goal)
            {
                string happened = current.Progress is { } pp && current.ProgressBasis != PlayerProgressBasis.None
                    ? string.Format(Inv, "阶段 {0} · 已完成约 {1:P0}", WorkStageDisplay(current.Stage), pp)
                    : "阶段 " + WorkStageDisplay(current.Stage);
                _cancelConfirmLabel.Text = $"取消「{current.Name}」？\n已发生：{happened}。\n已交付与已消耗的材料按真实规则保留；取消后此目标不再继续，机器人停止后续工序。";
                _cancelConfirmYesButton.Visible = true;
            }
            else
            {
                _cancelConfirmLabel.Text = "该工程已变化、完成或不再是当前目标；请重新查看后再取消。";
                _cancelConfirmYesButton.Visible = false;
            }
        }

        _workHistoryLabel.Text = _workHistory.Length == 0
            ? "（尚无记录）"
            : string.Join("\n", _workHistory.Select(h =>
                h.Name + " · " + WorkStageDisplay(h.Stage) + " · " + WorkStatusDisplay(h.Status) +
                (h.Reason.Length > 0 ? " · " + h.Reason : "")));
    }

    // 取消工程：后果确认后同步关闭管理，再重取目标/版本/epoch 排队；旧目标必须拒。
    private void RequestCancelWork()
    {
        if (_work is not { Active: true } work) return;
        // 关联工程详情打开时也可直接取消（N004 旧实现在此 return 造成启用按钮无反馈）：
        // 确认卡与按钮文本都指向当前活动目标；确认提交前仍按 retarget/version/epoch 重验。
        _cancelGoalId = work.GoalId;
        _cancelConfirmOpen = true;
        _cancelFocusPending = true;
        SyncCancelConfirmVisibility();
    }

    private void ConfirmCancelWork()
    {
        if (_cancelGoalId is not { } goal || !GateAction("cancel-work:" + goal)) return; // 双击只提交一次
        var before = _world.ReadWork();
        if (before is not { Active: true } || before.GoalId != goal)
        {
            CloseCancelConfirm(); // 旧目标必须拒，不能悄悄取消新目标
            return;
        }
        long epoch = CurrentEpoch();
        CloseManagement(); // 确认后同步关闭管理；世界命令在管理门禁下会被拒
        if (_world.ReadPlayerState().ManagementPaused) return; // 关闭未完成（命令在途）：本次不提交，等重试后重新确认
        var current = _world.ReadWork();
        if (current is not { Active: true } work || work.GoalId != goal || CurrentEpoch() != epoch) return;
        _world.QueueCancelWork(goal, work.ObservedVersion, epoch);
    }

    // ---- 显式接线 ----

    private void OpenConnectPanel()
    {
        if (_managementOpen) return; // 管理层优先，不叠层
        _connectPanelOpen = true;
        _connectTimer = 0f;
        _connectOptionsEpoch = long.MinValue;
        _connectSignature = "";
        _connectFacilityId ??= _tutorial?.FacilityId;
        _connectSourceId ??= _tutorial?.SourceId;
    }

    private void CloseConnectPanel()
    {
        _connectPanelOpen = false;
        _connectFacilityId = null;
        _connectSourceId = null;
    }

    private void RefreshConnectPanel(float dt)
    {
        _connectScroll.Visible = _connectPanelOpen;
        if (!_connectPanelOpen) return;
        _connectTimer -= dt;
        if (_connectTimer <= 0f || _connectOptionsEpoch != _lastEpoch)
        {
            _connectOptions = _world.ReadConnectionOptions(null); // 不过滤成看似全部合法
            _connectOptionsEpoch = _lastEpoch;
            _connectTimer = ConnectIntervalS;
            EnsureConnectionSelection();
            if (ConnectSignature() != _connectSignature)
            {
                _connectSignature = ConnectSignature();
                RebuildConnectRows();
            }
        }
        if (_tutorial is { NextAction: "connect" } t && t.FacilityId is { Length: > 0 } wanted)
        {
            var hint = Array.Find(_connectOptions, o => o.FacilityId == wanted);
            _connectHintLabel.Text = hint is { } h
                ? "教学目标：为 " + h.FacilityName + " 明确选择供电源（不会自动挑选）"
                : "教学目标：为 " + wanted + " 选择供电源";
        }
        else _connectHintLabel.Text = "逐个明确选择设施与供电源；不会自动接线，已接线不重复铺线。";

        var selected = CurrentConnectOption();
        // 固定核心内常驻所选 source/target 与整条费用：选中行滚出列表可视区也能看清在确认什么（N001）。
        _connectCostLabel.Text = selected is { } selectedCost
            ? "已选 " + selectedCost.FacilityName + " ← " + selectedCost.SourceName + "\n成本 " + CostText(selectedCost.Cost)
            : "成本：先选择设施与供电源";
        _connectGapLabel.Text = selected is { } selectedGap ? "净缺口 " + GapText(selectedGap.Gap) : "净缺口：先选择设施与供电源";
        _connectReasonLabel.Text = selected is { CanSubmit: false } refused
            ? !refused.CandidateLegal ? refused.CandidateReason : refused.SubmitReason : "";
        bool reasonVisible = _connectReasonLabel.Text.Length > 0;
        _connectReasonLabel.Visible = reasonVisible;
        _connectReasonScroll.Visible = reasonVisible; // 完整拒因固定核心内小滚动块，超长区内滚动不截断
        bool canSubmit = selected is { CanSubmit: true } && !(_lifecycle?.Busy ?? false) && _commandCooldown <= 0f;
        _connectSubmitButton.Disabled = !canSubmit;
        _connectSubmitButton.Text = selected is not null ? "确认接线" : "先选来源";
        // N001 后续：文本与可见性写入后同帧统一分配布局预算（真实字体度量，不按字符数猜行数），
        // 选择变化的当帧即得到正确的列表/拒因高度，不会出现 0 高或裁一帧。
        UpdateConnectLayout(selected);
    }

    // N001 后续：接线面板布局预算同帧统一计算。优先级：标题/完整成本/净缺口/确认与关闭常驻 →
    // 候选列表保一个完整最长多行 pair 的实际高度 → 拒因块拿剩余预算（超长区内滚动，不截断不省略）。
    // 有真实选择时非关键重复 hint 让位（未选择仍显示帮助）；总预算不超过外层遮罩当前帧高度，
    // 不把固定保底盲加回去顶破视口；选择/拒因长短变化都只在预算内重新分配。
    private void UpdateConnectLayout(ConnectionOptionView? selected)
    {
        float height = _connectContextHeight;
        const float panelPadV = 22f; // PanelStyle 上下 ContentMargin 10+10 加底边框
        const float boxSep = 6f;     // connectBox 与 connectCore 的 separation
        // 面板内容宽：滚动区宽 - 左右内边距 14+14 与边框；核心行量宽再留 2px 余量（宁可略高不裁）。
        float width = MathF.Max(80f, _connectScroll.Size.X - 32f);
        float barAllowance = MathF.Max(12f, MathF.Ceiling(_connectListScroll.GetVScrollBar().GetCombinedMinimumSize().X));

        bool hintVisible = selected == null; // 有真实选择时非关键重复 hint 让位；未选择仍帮助
        _connectHintLabel.Visible = hintVisible;

        // 核心行高按真实字体度量定 min：容器布局与本预算一致；度量宽略窄、高度略加，方向上只可能高不裁。
        float titleH = MeasureLabelHeight(_connectTitleLabel, width) + 2f;
        float hintH = hintVisible ? MeasureLabelHeight(_connectHintLabel, width) + 2f : 0f;
        float costH = MeasureLabelHeight(_connectCostLabel, width) + 2f;
        float gapH = MeasureLabelHeight(_connectGapLabel, width) + 2f;
        float actionsH = _connectSubmitButton.GetCombinedMinimumSize().Y;
        _connectTitleLabel.CustomMinimumSize = new Vector2(0, MathF.Ceiling(titleH));
        if (hintVisible) _connectHintLabel.CustomMinimumSize = new Vector2(0, MathF.Ceiling(hintH));
        _connectCostLabel.CustomMinimumSize = new Vector2(0, MathF.Ceiling(costH));
        _connectGapLabel.CustomMinimumSize = new Vector2(0, MathF.Ceiling(gapH));

        // 一个完整最长多行候选 pair 的实际高度：按钮文本按列表内容宽（滚动条 + 左右内边距 12+12）折行，
        // 再加按钮上下内边距 6+6；同样量宽略窄，只可能高不裁。
        float rowTextWidth = MathF.Max(40f, width - barAllowance - 28f);
        float longestRow = 0f;
        foreach (var (_, button) in _connectRows)
            longestRow = MathF.Max(longestRow, MathF.Ceiling(MeasureTextHeight(button, button.Text, rowTextWidth)) + 14f);
        if (longestRow <= 0f) longestRow = 48f * (float)_uiScale; // 无候选行时给稳定保底，布局不抖

        int coreChildren = 1 + (hintVisible ? 1 : 0) + 1 + 1 + (_connectReasonScroll.Visible ? 1 : 0) + 1;
        float essential = panelPadV + titleH + hintH + costH + gapH + actionsH + boxSep * coreChildren;
        float remaining = MathF.Max(0f, height - essential);

        // 拒因块：完整文本按内容宽（滚动时再让出滚动条）度量；最少保一行可读，超出预算就区内滚动。
        float reasonMeasured = _connectReasonScroll.Visible
            ? MeasureLabelHeight(_connectReasonLabel, MathF.Max(40f, width - barAllowance)) + 2f : 0f;
        float reasonLine = _connectReasonLabel.GetThemeFont("font").GetHeight(_connectReasonLabel.GetThemeFontSize("font_size"));
        float reasonMin = reasonLine + 6f;
        float reasonBudget = MathF.Max(reasonMin, remaining - longestRow); // 列表优先保一个完整最长 pair
        float reasonH = _connectReasonScroll.Visible ? MathF.Min(MathF.Max(reasonMin, reasonMeasured), reasonBudget) : 0f;
        if (_connectReasonScroll.Visible)
            _connectReasonScroll.CustomMinimumSize = new Vector2(0, MathF.Ceiling(reasonH));

        // 列表拿其余全部（expand 拿满剩余空间；封顶 480 防长视口拉得过高）；剩余不足时收缩的是列表，
        // 不为凑固定保底顶破外层遮罩（原生 7098/N001 教训）。
        float listMin = Mathf.Clamp(remaining - reasonH, 0f, 480f);
        _connectListScroll.CustomMinimumSize = new Vector2(0, MathF.Ceiling(listMin));
    }

    private static float MeasureLabelHeight(Label label, float width) => MeasureTextHeight(label, label.Text, width);

    // 真实字体度量：给定宽度折行后的文本高度。先按字体度量折行总高推回行数，再按控件自身
    // line_separation 重建行距（与 Label/Button 实际排版同公式）。量宽比实际再窄 4px：无论哪种
    // 换行规则都只会更早折行，行数只可能多不会少，度量结果不小于真实布局高度（宁可略高不裁）。
    private static float MeasureTextHeight(Control control, string text, float width)
    {
        if (text.Length == 0) return 0f;
        var font = control.GetThemeFont("font");
        int size = control.GetThemeFontSize("font_size");
        float lineHeight = MathF.Max(1f, font.GetHeight(size));
        float lineSep = control.GetThemeConstant("line_separation");
        var extent = font.GetMultilineStringSize(text, HorizontalAlignment.Left, MathF.Max(1f, width - 4f), size);
        // 实测：折行总高 extent.Y 的每行行进比 fontH 略大（如 27/26、47/23），不能向下取整反推行数
        // （会把 1 行计成 2 行、把预算翻倍压没列表）。行进只略大于 fontH，按 fontH 四舍五入还原行数，
        // 结果与 extent.Y 取大并补 line_separation，方向上只可能略高不裁。
        int lines = Math.Max(1, (int)MathF.Round(extent.Y / lineHeight, MidpointRounding.AwayFromZero));
        return MathF.Max(extent.Y, lines * lineHeight + (lines - 1) * lineSep);
    }

    private string _connectSignature = "";

    private string ConnectSignature() => string.Join("|", _connectOptions.Select(o =>
        o.FacilityId + ">" + o.SourceId + ":" + o.ObservedVersion + ":" + o.CanSubmit + ":" + o.CandidateReason + ":" + o.SubmitReason));

    private ConnectionOptionView? CurrentConnectOption()
        => _connectFacilityId is { } facility && _connectSourceId is { } source
            ? Array.Find(_connectOptions, o => o.FacilityId == facility && o.SourceId == source)
            : null;

    // 预选：保留当前有效选择；否则用教学给出的真实目标；再退到第一个可提交项。
    private void EnsureConnectionSelection()
    {
        if (CurrentConnectOption() != null) return;
        var candidate = _tutorial is { } t && t.FacilityId is { } wantedFacility
            ? (t.SourceId is { } wantedSource
                ? Array.Find(_connectOptions, o => o.FacilityId == wantedFacility && o.SourceId == wantedSource) : null)
                ?? Array.Find(_connectOptions, o => o.FacilityId == wantedFacility)
            : null;
        candidate ??= Array.Find(_connectOptions, o => o.CanSubmit);
        candidate ??= _connectOptions.Length > 0 ? _connectOptions[0] : null;
        if (candidate is { } chosen)
        {
            _connectFacilityId = chosen.FacilityId;
            _connectSourceId = chosen.SourceId;
        }
    }

    private void RebuildConnectRows()
    {
        foreach (var (_, button) in _connectRows)
        {
            _connectListBox.RemoveChild(button);
            button.QueueFree();
        }
        _connectRows.Clear();
        foreach (var option in _connectOptions)
        {
            var button = MakeConnectRow(option);
            _connectListBox.AddChild(button);
            _connectRows.Add((option, button));
        }
        RefreshConnectRowStyles();
    }

    private Button MakeConnectRow(ConnectionOptionView option)
    {
        var button = new Button
        {
            Name = "ConnectRow_" + option.FacilityId + "_" + option.SourceId,
            Text = option.FacilityName + " ← " + option.SourceName + " · " + option.DistanceM.ToString("0.#", Inv) + "m/" + option.RangeM.ToString("0.#", Inv) + "m" +
                (option.CanSubmit ? " · 可提交" : option.CandidateLegal ? " · " + option.SubmitReason : " · " + option.CandidateReason),
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.All, // 键盘可选择（接线面板打开时方向键让位给列表）
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
        };
        _sizeTargets.Add((button, SizeSmall));
        StyleButton(button, accent: false);
        var cost = "成本 " + CostText(option.Cost) + " · 净缺口 " + GapText(option.Gap) + (option.Recovery ? " · 恢复路径" : "") + (option.SupplyId is { } supply ? " · 供料 " + supply : "");
        button.TooltipText = cost;
        button.Pressed += () =>
        {
            _connectFacilityId = option.FacilityId;
            _connectSourceId = option.SourceId;
            RefreshConnectRowStyles();
        };
        return button;
    }

    private void RefreshConnectRowStyles()
    {
        foreach (var (option, button) in _connectRows)
        {
            bool selected = option.FacilityId == _connectFacilityId && option.SourceId == _connectSourceId;
            button.AddThemeStyleboxOverride("normal", selected
                ? ButtonStyle(Palette.PanelBg.Lightened(0.06f), Palette.Accent, 2)
                : ButtonStyle(Palette.PanelBg));
            button.AddThemeStyleboxOverride("hover", ButtonStyle(Palette.PanelBg.Lightened(0.1f)));
            button.AddThemeColorOverride("font_color", Palette.Text);
            button.AddThemeColorOverride("font_hover_color", Palette.Text);
            button.AddThemeColorOverride("font_disabled_color", Palette.TextDim);
        }
    }

    private void SubmitConnectionSelection()
    {
        var option = CurrentConnectOption();
        if (option is not { CanSubmit: true } ||
            !GateAction("connection:" + option.FacilityId + ">" + option.SourceId)) return;
        // 明确 facility/source 与观察版本原样提交；执行端再复验。
        _world.QueueConnection(option.FacilityId, option.SourceId, option.ObservedVersion);
    }

    private void QueueTutorial(string action)
    {
        if (!GateAction("tutorial:" + action)) return;
        _world.QueueTutorialAction(action);
    }
}
