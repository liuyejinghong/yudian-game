#nullable enable
using System;
using System.Linq;
using Godot;

namespace Yudian.PlayerUI;

// 局内常驻 HUD：目标/教学、资源与功率、当前作业、建造方案（只读草案）、显式接线、底部常用操作。
// 关键内容默认同屏；低频明细（里程碑/发展方向/设施/诊断）折叠；宽度容器换行，不用整屏滚动塞内容。
public partial class PlayerController
{
    private Control _hudRoot = null!;
    private Control _commandBar = null!;
    private PanelContainer _commandPanel = null!;
    private ScrollContainer _infoScroll = null!, _planScroll = null!, _connectScroll = null!;
    private ScrollContainer _infoDetailsScroll = null!, _planDetailsScroll = null!;
    private VBoxContainer _infoCore = null!, _planCore = null!;
    private Button _infoDetailsToggle = null!, _planDetailsToggle = null!;
    private HFlowContainer _rowModes = null!;
    private HBoxContainer _confirmRow = null!;
    private HFlowContainer _cameraRow = null!;
    private HFlowContainer _diagRow = null!;
    private ButtonGroup _modeGroup = null!;
    private Button? _rotateButton;
    private Button _confirmButton = null!, _cancelButton = null!, _pauseButton = null!, _diagToggle = null!;
    private Button _restockButton = null!, _legacyButton = null!, _recoverButton = null!, _retryButton = null!, _connectNextButton = null!, _connectOpenButton = null!;

    private PanelContainer _infoPanel = null!;
    private Label _statusLabel = null!;
    private Label _goalStageLabel = null!, _goalTitleLabel = null!, _goalInstructionLabel = null!, _goalWhyLabel = null!,
        _goalActionLabel = null!, _goalCoverageLabel = null!, _goalProgressLabel = null!;
    private Button _milestonesToggle = null!;
    private Label _milestonesLabel = null!;
    private Label _powerLabel = null!, _stockLabel = null!, _stockDetailsLabel = null!, _noticeDetailsLabel = null!, _instructionDetailsLabel = null!, _jobLabel = null!, _supportLabel = null!;
    private Label _devTitleLabel = null!, _devBodyLabel = null!, _devDirectionsLabel = null!;
    private Button _devDirectionsToggle = null!, _facilityToggle = null!;
    private Label _facilityLabel = null!;

    private PanelContainer _planPanel = null!;
    private Label _planTitleLabel = null!, _selectionLabel = null!, _planTagLabel = null!, _planReasonLabel = null!,
        _planCostLabel = null!, _planGapLabel = null!, _planStockLabel = null!, _planSupplyLabel = null!,
        _planPowerLabel = null!, _planConsequenceLabel = null!, _planRecoveryLabel = null!, _worldLabel = null!;

    private PanelContainer _connectPanel = null!;
    private Label _connectHintLabel = null!, _connectCostLabel = null!, _connectGapLabel = null!, _connectReasonLabel = null!;
    private ScrollContainer _connectListScroll = null!;
    private VBoxContainer _connectListBox = null!;
    private Button _connectSubmitButton = null!, _connectCloseButton = null!;

    private Label _hintsLabel = null!;
    private Control _conclusionCard = null!;
    private Label _conclusionListLabel = null!;
    private Button _conclusionContinueButton = null!;

    private string? _hintedModeType;

    private void BuildHud()
    {
        var hud = new CanvasLayer { Name = "PlayerHud" };
        AddChild(hud);
        var root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(root);
        _hudRoot = root;

        // —— 左列：目标/资源/作业/低频明细（高度按底部命令栏封顶；常用行默认同屏，仅低频明细超出时滚动） ——
        var infoScroll = new ScrollContainer
        {
            Name = "InfoScroll",
            Position = new Vector2(16, 16),
            Size = new Vector2(420, 600),
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
        _infoCore = new VBoxContainer { Name = "InfoCore" };
        _infoCore.AddThemeConstantOverride("separation", 4);
        infoBox.AddChild(_infoCore);

        _statusLabel = InfoLabel("StatusLabel", SizeSmall, Palette.Warn);
        _infoCore.AddChild(_statusLabel);

        _goalStageLabel = InfoLabel("GoalStageLabel", SizeTag, Palette.TextDim);
        _goalTitleLabel = InfoLabel("GoalTitleLabel", SizeHeading, Palette.Text);
        _goalInstructionLabel = InfoLabel("GoalInstructionLabel", SizeBody, Palette.Text);
        _goalWhyLabel = InfoLabel("GoalWhyLabel", SizeBody, Palette.TextDim);
        _goalActionLabel = InfoLabel("GoalActionLabel", SizeBody, Palette.Accent);
        _goalCoverageLabel = InfoLabel("GoalCoverageLabel", SizeSmall, Palette.Hint);
        _goalProgressLabel = InfoLabel("GoalProgressLabel", SizeTag, Palette.TextDim);
        _infoCore.AddChild(_goalStageLabel);
        _infoCore.AddChild(_goalTitleLabel);
        _infoCore.AddChild(_goalInstructionLabel);
        _infoCore.AddChild(_goalActionLabel);

        _powerLabel = InfoLabel("PowerLabel", SizeBody, Palette.Text);
        _stockLabel = InfoLabel("StockLabel", SizeBody, Palette.Text);
        _stockDetailsLabel = InfoLabel("StockDetailsLabel", SizeBody, Palette.Text);
        _noticeDetailsLabel = InfoLabel("NoticeDetailsLabel", SizeSmall, Palette.TextDim);
        _instructionDetailsLabel = InfoLabel("InstructionDetailsLabel", SizeBody, Palette.Text);
        _infoCore.AddChild(_powerLabel);
        _infoCore.AddChild(_stockLabel);

        _jobLabel = InfoLabel("JobLabel", SizeBody, Palette.Text);
        _supportLabel = InfoLabel("SupportLabel", SizeSmall, Palette.Accent);
        _infoCore.AddChild(_jobLabel);
        _infoCore.AddChild(_supportLabel);

        _infoDetailsScroll = new ScrollContainer
        {
            Name = "InfoDetailsScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 180),
        };
        _infoDetailsToggle = FoldToggle("InfoDetailsToggle", "目标、资源与操作详情", _infoDetailsScroll);
        infoBox.AddChild(_infoDetailsToggle);
        infoBox.AddChild(_infoDetailsScroll);
        var infoDetails = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        infoDetails.AddThemeConstantOverride("separation", 6);
        _infoDetailsScroll.AddChild(infoDetails);
        infoDetails.AddChild(_noticeDetailsLabel);
        infoDetails.AddChild(_instructionDetailsLabel);
        infoDetails.AddChild(_goalProgressLabel);
        infoDetails.AddChild(_goalWhyLabel);
        infoDetails.AddChild(_goalCoverageLabel);
        infoDetails.AddChild(_stockDetailsLabel);
        _milestonesToggle = FoldToggle("MilestonesToggle", "教学进度明细", _milestonesLabel = InfoLabel("MilestonesLabel", SizeSmall, Palette.TextDim));
        infoDetails.AddChild(_milestonesToggle);
        infoDetails.AddChild(_milestonesLabel);

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
        _hintsLabel = TrackLabel("Hints", "WASD／方向键平移 · 右键拖动或双指平移 · 滚轮／捏合缩放 · Q/E 旋转 · F 定位 · 左键选择 · 右键取消 · Esc 菜单", SizeSmall, Palette.TextDim);
        infoDetails.AddChild(_hintsLabel);

        // —— 右列：选点/建造方案（草案只读）/ 显式接线（高度同样按命令栏封顶） ——
        var planScroll = new ScrollContainer
        {
            Name = "PlanScroll",
            Size = new Vector2(380, 600),
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
        _planGapLabel = InfoLabel("PlanGapLabel", SizeBody, Palette.Text);
        _planStockLabel = InfoLabel("PlanStockLabel", SizeSmall, Palette.TextDim);
        _planSupplyLabel = InfoLabel("PlanSupplyLabel", SizeSmall, Palette.Text);
        _planPowerLabel = InfoLabel("PlanPowerLabel", SizeSmall, Palette.Text);
        _planConsequenceLabel = InfoLabel("PlanConsequenceLabel", SizeSmall, Palette.TextDim);
        _planRecoveryLabel = InfoLabel("PlanRecoveryLabel", SizeSmall, Palette.Hint);
        _worldLabel = InfoLabel("WorldLabel", SizeTag, Palette.TextDim);
        _planCore.AddChild(_planTitleLabel);
        _planCore.AddChild(_selectionLabel);
        _planCore.AddChild(_planTagLabel);
        _planCore.AddChild(_planReasonLabel);
        _planCore.AddChild(_planCostLabel);
        _planCore.AddChild(_planGapLabel);
        _planCore.AddChild(_planSupplyLabel);
        _planCore.AddChild(_planPowerLabel);
        _planCore.AddChild(_planConsequenceLabel);
        _planCore.AddChild(_planRecoveryLabel);
        _planDetailsScroll = new ScrollContainer
        {
            Name = "PlanDetailsScroll", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 120),
        };
        _planDetailsToggle = FoldToggle("PlanDetailsToggle", "物料与世界详情", _planDetailsScroll);
        planBox.AddChild(_planDetailsToggle);
        planBox.AddChild(_planDetailsScroll);
        var planDetails = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _planDetailsScroll.AddChild(planDetails);
        planDetails.AddChild(_planStockLabel);
        planDetails.AddChild(_worldLabel);

        var connectWrap = new ScrollContainer
        {
            Name = "ConnectScroll",
            Size = new Vector2(380, 600),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
            Visible = false,
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
        var connectTitle = TrackLabel("ConnectTitleLabel", "接线", SizeHeading, Palette.Text);
        _connectHintLabel = InfoLabel("ConnectHintLabel", SizeSmall, Palette.TextDim);
        _connectCostLabel = InfoLabel("ConnectCostLabel", SizeBody, Palette.Text);
        _connectGapLabel = InfoLabel("ConnectGapLabel", SizeBody, Palette.Text);
        _connectReasonLabel = InfoLabel("ConnectReasonLabel", SizeSmall, Palette.Warn);
        connectBox.AddChild(connectTitle);
        connectBox.AddChild(_connectHintLabel);
        connectBox.AddChild(_connectCostLabel);
        connectBox.AddChild(_connectGapLabel);
        connectBox.AddChild(_connectReasonLabel);
        var connectScroll = new ScrollContainer
        {
            Name = "ConnectList",
            CustomMinimumSize = new Vector2(0, 140),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _connectListScroll = connectScroll;
        connectBox.AddChild(connectScroll);
        _connectListBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _connectListBox.AddThemeConstantOverride("separation", 4);
        connectScroll.AddChild(_connectListBox);
        var connectActions = new HBoxContainer();
        connectActions.AddThemeConstantOverride("separation", 8);
        _connectSubmitButton = PanelActionButton("ConnectSubmitButton", "确认接线", SubmitConnectionSelection, accent: true);
        _connectCloseButton = PanelActionButton("ConnectCloseButton", "关闭", CloseConnectPanel);
        connectActions.AddChild(_connectSubmitButton);
        connectActions.AddChild(_connectCloseButton);
        connectBox.AddChild(connectActions);

        // —— 底部：提示 + 常用操作（模式/确认/取消/暂停）+ 镜头 + 低频诊断折叠 ——
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
        // 宽度下限保证 1280 逻辑点上模式/确认/镜头行不换行堆高（150% 时 1260 仍不裁切）。
        barPanel.CustomMinimumSize = new Vector2(840, 0);
        bar.AddChild(barPanel);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        barPanel.AddChild(stack);

        _rowModes = new HFlowContainer { Name = "BuildBar", Visible = false };
        _rowModes.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(_rowModes);

        _confirmRow = new HBoxContainer { Name = "ConfirmRow" };
        _confirmRow.AddThemeConstantOverride("separation", 10);
        stack.AddChild(_confirmRow);
        _confirmButton = MakeButton("ConfirmButton", "确认整平", DoConfirm, accent: true);
        // 固定宽度：整平/建设两态文案长度不同，避免按钮文字切换时整行移动。
        _confirmButton.CustomMinimumSize = new Vector2(176, 0);
        _minSizeTargets.Add((_confirmButton, new Vector2(176, 0)));
        _cancelButton = MakeButton("CancelButton", "取消任务", () => _world.QueuePlayerAction("cancel"));
        _pauseButton = MakeButton("PauseButton", "暂停", () => _world.QueuePlayerAction("pause"));
        _diagToggle = MakeButton("DiagToggle", "诊断 ▸", ToggleDiagnostics, commandCooldown: false);
        _diagToggle.FocusMode = Control.FocusModeEnum.All;
        _connectOpenButton = MakeButton("ExplicitConnectButton", "接线…", OpenConnectPanel);
        _connectOpenButton.FocusMode = Control.FocusModeEnum.All;
        var menuButton = MakeButton("GameMenuButton", "菜单", () => QueueLifecycle("pause"), commandCooldown: false);
        menuButton.FocusMode = Control.FocusModeEnum.All;
        _confirmRow.AddChild(_confirmButton);
        _confirmRow.AddChild(_cancelButton);
        _confirmRow.AddChild(_pauseButton);
        _confirmRow.AddChild(_connectOpenButton);
        _confirmRow.AddChild(menuButton);
        _confirmRow.AddChild(_diagToggle);

        _cameraRow = new HFlowContainer { Name = "CameraRow" };
        _cameraRow.AddThemeConstantOverride("h_separation", 6);
        stack.AddChild(_cameraRow);
        AddCamButton(_cameraRow, "CameraForwardButton", "前", () => PanCamera(0f, 1f));
        AddCamButton(_cameraRow, "CameraBackButton", "后", () => PanCamera(0f, -1f));
        AddCamButton(_cameraRow, "CameraLeftButton", "左", () => PanCamera(-1f, 0f));
        AddCamButton(_cameraRow, "CameraRightButton", "右", () => PanCamera(1f, 0f));
        AddCamButton(_cameraRow, "CameraZoomInButton", "拉近", () => ZoomCamera(0.8f));
        AddCamButton(_cameraRow, "CameraZoomOutButton", "拉远", () => ZoomCamera(1.25f));
        AddCamButton(_cameraRow, "CameraRotateLeftButton", "左转", () => RotateCamera(-Mathf.Pi / 12f));
        AddCamButton(_cameraRow, "CameraRotateRightButton", "右转", () => RotateCamera(Mathf.Pi / 12f));
        AddCamButton(_cameraRow, "CameraResetButton", "复位", ResetCamera);

        _diagRow = new HFlowContainer { Name = "DiagRow", Visible = false };
        _diagRow.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(_diagRow);
        _restockButton = MakeButton("RestockButton", "补维修耗材", () => _world.QueuePlayerAction("restock"));
        _legacyButton = MakeButton("LegacyLoadButton", "读取旧档", () => _world.QueuePlayerAction("legacy"));
        _recoverButton = MakeButton("RecoverButton", "故障恢复", () => _world.QueuePlayerAction("recover"));
        _retryButton = MakeButton("RetryButton", "重试工程/保障", () => _world.QueuePlayerAction("retry"));
        _connectNextButton = MakeButton("ConnectButton", "自动接最近电缆", () => _world.QueuePlayerAction("connect"));
        _diagRow.AddChild(_restockButton);
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

        BuildScreens(hud, root);
    }

    // 面板内动作按钮：保留键盘焦点，宽度适配 380 面板；冷却由命令自身负责，避免双重门禁。
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

    // 两列高度按底部命令栏封顶：常用行默认同屏；只有低频明细超出时才出现滚动，任何缩放都不叠挡命令栏。
    private void UpdateColumnGeometry()
    {
        var viewport = _camera.GetViewport().GetVisibleRect().Size;
        _commandPanel.CustomMinimumSize = new Vector2(MathF.Min(960f, viewport.X - 32f), 0);
        _commandBar.OffsetTop = -_commandPanel.GetCombinedMinimumSize().Y - 12f;
        float barTop = _commandBar.GetGlobalRect().Position.Y;
        float height = MathF.Max(220f, (barTop > 0f ? barTop : viewport.Y) - 28f);
        float leftWidth = MathF.Min(450f, (viewport.X - 160f) * 0.4f);
        float rightWidth = MathF.Min(450f, (viewport.X - 160f) * 0.4f);
        _infoScroll.Position = new Vector2(16f, 16f);
        _infoScroll.Size = new Vector2(leftWidth, height);
        _planScroll.Position = new Vector2(viewport.X - rightWidth - 16f, 16f);
        _planScroll.Size = new Vector2(rightWidth, height);
        _connectScroll.Position = _planScroll.Position;
        _connectScroll.Size = _planScroll.Size;
        // 核心不会随详情滚走；低频区域只使用核心剩下的高度。
        _infoDetailsScroll.CustomMinimumSize = new Vector2(0, MathF.Max(80f, height - _infoCore.GetCombinedMinimumSize().Y - _infoDetailsToggle.Size.Y - 32f));
        _planDetailsScroll.CustomMinimumSize = new Vector2(0, MathF.Max(80f, height - _planCore.GetCombinedMinimumSize().Y - _planDetailsToggle.Size.Y - 32f));
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
        UpdateColumnGeometry();

        _economyTimer -= dt;
        if (_economyTimer <= 0f || _economy == null)
        {
            _economy = _world.ReadEconomy();
            _economyTimer = EconomyIntervalS;
        }
        _tutorial = _world.ReadTutorial();
        if (_tutorial.StageId != _learnedStage) _learnedStage = _tutorial.StageId;

        RefreshGoalPanel(state);
        RefreshEconomyPanel();
        RefreshJobPanel(state);
        RefreshDevPanel();
        RefreshPlanPanel(state);
        RefreshConnectPanel(dt);
        RefreshBottomBar(state);

        _conclusionCard.Visible = _tutorial is { Enabled: true, ShowConclusion: true, Completed: true };
        if (_conclusionCard.Visible && _tutorial is { } done)
            _conclusionListLabel.Text = "已完成 " + done.Achieved + "/" + done.Total + " 项，世界继续运行；可继续自主经营，或保存后返回主页。";
    }

    private void RefreshGoalPanel(PlayerReadModel state)
    {
        _noticeDetailsLabel.Text = state.Notice.Length > 0 ? state.Notice : _lifecycle?.Notice ?? "";
        _noticeDetailsLabel.Visible = _noticeDetailsLabel.Text.Length > 0;
        _statusLabel.Text = _noticeDetailsLabel.Text;
        _statusLabel.Visible = IsProblemNotice(_statusLabel.Text);

        var tutorial = _tutorial;
        bool teaching = tutorial is { Enabled: true } && !_tutorial!.Completed;
        _goalStageLabel.Visible = true;
        _goalTitleLabel.Visible = true;
        _goalInstructionLabel.Visible = true;
        _goalActionLabel.Visible = true;
        _goalProgressLabel.Visible = true;
        _milestonesToggle.Visible = tutorial is { Enabled: true };
        if (tutorial is not { Enabled: true } t2)
        {
            _goalStageLabel.Text = "目标";
            _goalTitleLabel.Text = "自主经营";
            _goalInstructionLabel.Text = "选择位置与目标，机器人代劳执行。";
            _instructionDetailsLabel.Text = "按当前任务与资源推进，机器人代劳执行。";
            _goalWhyLabel.Visible = false;
            _goalCoverageLabel.Visible = false;
            _goalActionLabel.Text = "下一步：" + (state.Job is { Active: true } ? "等待当前施工完成" : "选择整平或建设位置");
            _goalProgressLabel.Visible = false;
            _milestonesLabel.Visible = false;
            return;
        }

        _goalStageLabel.Visible = false; // 标题与下一步已给阶段语义，避免重复占行。
        _goalTitleLabel.Text = tutorial.Title;
        _goalInstructionLabel.Text = CompactTutorialInstruction(tutorial);
        _instructionDetailsLabel.Text = tutorial.Instruction;
        _goalWhyLabel.Text = "为什么：" + tutorial.Why;
        _goalWhyLabel.Visible = _helpDetailed && tutorial.Why.Length > 0;
        _goalActionLabel.Text = "下一步：" + NextActionDisplay(tutorial.NextAction, tutorial.NeededType) + " · " + tutorial.Achieved + "/" + tutorial.Total;
        _goalProgressLabel.Text = "教学进度 " + tutorial.Achieved + "/" + tutorial.Total;
        _goalCoverageLabel.Text = CoverageHint(tutorial);
        _goalCoverageLabel.Visible = _helpDetailed && _goalCoverageLabel.Text.Length > 0;

        var milestones = tutorial.Milestones;
        _milestonesLabel.Text = milestones.Length == 0
            ? "（暂无可核对的里程碑条目）"
            : string.Join("\n", milestones.Select(m => (m.Achieved ? "✓ " : "· ") + m.Title));
        _milestonesToggle.Text = (_milestonesLabel.Visible ? "教学进度明细 ▾" : "教学进度明细 ▸");
        _milestonesToggle.Visible = true;

        ApplyModeHint(teaching && ShowButtonHints(tutorial) ? tutorial.NeededType : null);
    }

    private static bool IsProblemNotice(string text) => new[] { "失败", "故障", "未执行", "未知", "停机", "阻塞", "超出", "不可", "需要处理" }
        .Any(word => text.Contains(word, StringComparison.Ordinal));

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
                "solar" => "首个太阳能阵列的 12m 覆盖决定后续设施能从哪里取电；建设方案面板显示实际覆盖距离。",
                "charger" or "repair" or "processor" => "用电设施必须落在已建太阳能的 12m 覆盖内；方案面板显示每座太阳能的距离与范围。",
                "storage" => "仓储位置影响运输距离；方案面板显示各仓库路线距离。",
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

    private void RefreshEconomyPanel()
    {
        var economy = _economy ?? _world.ReadEconomy();
        _powerLabel.Text = (economy.Daylight ? "日照" : "夜间") + " · 发电 " + economy.GenerationPerSecond.ToString("0.##", Inv) + "/s · 范围 " + economy.ConnectionRangeM.ToString("0.#", Inv) + "m";
        var stock = economy.Materials;
        if (stock.Length == 0)
        {
            _stockLabel.Text = "库存数据准备中";
            _stockDetailsLabel.Text = "";
            return;
        }
        string Available(string id) => Array.Find(stock, m => m.Id == id) is { } material ? material.Name + " " + material.Available : "";
        var lines = new System.Collections.Generic.List<string>
        {
            "可用 " + string.Join(" · ", new[] { "iron", "copper", "parts" }.Select(Available)),
            string.Join(" · ", new[] { "cable", "kit" }.Select(Available)),
        };
        var ores = stock.Where(m => m.Id is "iron_ore" or "copper_ore" && m.Available > 0).Select(m => m.Name + " " + m.Available).ToArray();
        var pending = stock.Where(m => m.InTransit > 0 || m.InProcess > 0)
            .Select(m => m.Name + (m.InTransit > 0 ? "途" + m.InTransit : "") + (m.InProcess > 0 ? "制" + m.InProcess : "")).ToArray();
        if (ores.Length > 0 || pending.Length > 0) lines.Add(string.Join(" · ", ores.Concat(pending)));
        _stockLabel.Text = string.Join("\n", lines);
        _stockDetailsLabel.Text = string.Join("\n", stock.Select(m => m.Name + " 存" + m.Stored + " · 可用" + m.Available + " · 在途" + m.InTransit + " · 在制" + m.InProcess));
    }

    private void RefreshJobPanel(PlayerReadModel state)
    {
        var job = state.Job;
        _jobLabel.Text = job is not { } current
            ? "当前作业：无 · 选择位置后确认授权"
            : string.Format(Inv, "作业 {0} · 执行者 {1}{2}",
                StageDisplay(current.Stage), WorkerDisplay(current.WorkerId),
                current.Progress > 0 ? string.Format(Inv, " · {0:P0}", current.Progress) : "");

        bool boot = BootstrapOn;
        bool robotDetail = boot && _selectedRobotId is { } robotId && Array.Find(_bootstrap!.Robots, r => r.Id == robotId) is not null;
        if (robotDetail)
        {
            var support = Array.Find(_bootstrap!.Robots, r => r.Id == _selectedRobotId)!;
            _supportLabel.Visible = true;
            _supportLabel.Text = $"电量 {support.Energy:0.#}/{support.Capacity:0} · 耐久 {support.Durability:0.#}/{support.Capacity:0} · 载货 {support.Cargo}" +
                "\n" + support.State + (support.Reason.Length > 0 ? "：" + support.Reason : "");
        }
        else
        {
            // 未选机时的首屏保障汇总：按权威 State 原文分组计数，不推导电力/预算阈值。
            _supportLabel.Visible = boot;
            if (boot) _supportLabel.Text = "保障 " + string.Join("／", _bootstrap!.Robots.GroupBy(r => r.State).Select(g => g.Key + g.Count()));
        }
        // 玩家可见脚注：运行时长/当前槽/存档状态；不露地形版本等内部技术代号。
        var elapsed = TimeSpan.FromSeconds(Math.Max(0, state.TimeSeconds));
        _worldLabel.Text = string.Format(Inv, "{0}分{1:00}秒 · 槽 {2} · 存档 {3} · {4}{5}",
            (int)elapsed.TotalMinutes, elapsed.Seconds, _lifecycle?.ActiveSlot ?? 0, state.SaveExists ? "已有" : "无",
            state.Paused ? "已暂停" : "运行中", state.Ready ? "" : " · 世界恢复中");
    }

    private void RefreshDevPanel()
    {
        // 设施折叠：开关随 bootstrap 显隐；开关收起时明细不可见（与 dev 启用状态无关）。
        _facilityToggle.Visible = BootstrapOn;
        if (!_facilityToggle.Visible) _facilityLabel.Visible = false;
        if (_bootstrap is { } boot)
            _facilityLabel.Text = boot.Facilities.Length == 0 ? "暂无设施" :
                string.Join("\n", boot.Facilities.Select(f =>
                    $"{f.Name} · {(f.Built ? "已建成" : "建设中")}{(f.ServiceConnected ? " · 已接线" : f.Powered ? " · 供电" : "")}" +
                    (f.Source is { } source ? " · 电缆←" + FacilityName(boot, source) : "")));

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
        var detail = new System.Collections.Generic.List<string>();
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

    private void RefreshPlanPanel(PlayerReadModel state)
    {
        bool connectOpen = _connectPanelOpen;
        _planScroll.Visible = !connectOpen;
        if (connectOpen) return;

        bool building = Building;
        _planTitleLabel.Text = building ? "建造方案" : "选点";
        string? site = _selectedSite is { } p ? string.Format(Inv, "已选位置 ({0:F1}, {1:F1})", p.X, p.Z) : null;
        string? robot = _selectedRobotId is { } id && Array.Find(_robots, r => r.Id == id) is { } picked ? "已选 " + picked.Name : null;

        if (!building)
        {
            _selectionLabel.Text = site is not null
                ? site + "\n" + (_preview?.Reason ?? "预览中…")
                : robot is not null ? robot + (SelectedWorkerId() != null ? " · 再点地面指定整平位置" : " · 整平由筑垒执行")
                : "左键点选机器人与地面；右键取消选择";
            _planTagLabel.Visible = false;
            _planReasonLabel.Visible = false;
            _planCostLabel.Visible = false;
            _planGapLabel.Visible = false;
            _planStockLabel.Visible = false;
            _planSupplyLabel.Visible = false;
            _planPowerLabel.Visible = false;
            _planConsequenceLabel.Visible = false;
            _planRecoveryLabel.Visible = false;
            return;
        }

        _selectionLabel.Text = (site ?? robot ?? "瞄准地面") + string.Format(Inv, " · 朝向 {0}°", _yawSteps * 90);
        var plan = _plan;
        if (plan is not { } draft)
        {
            _planTagLabel.Text = "草案，无承诺";
            _planTagLabel.Visible = true;
            _planReasonLabel.Text = _preview?.Reason ?? "选择位置后显示完整成本、缺口与后果";
            _planReasonLabel.Visible = true;
            _planCostLabel.Visible = false;
            _planGapLabel.Visible = false;
            _planStockLabel.Visible = false;
            _planSupplyLabel.Visible = false;
            _planPowerLabel.Visible = false;
            _planConsequenceLabel.Visible = false;
            _planRecoveryLabel.Visible = false;
            return;
        }

        _planTagLabel.Text = "草案，无承诺 · " + (draft.CanSubmit ? "可提交" : "不可提交");
        _planTagLabel.Visible = true;
        var reasons = new System.Collections.Generic.List<string>();
        if (!draft.CandidateLegal && draft.CandidateReason.Length > 0) reasons.Add(draft.CandidateReason);
        if (!draft.CanSubmit && draft.SubmitReason.Length > 0 && draft.SubmitReason != draft.CandidateReason) reasons.Add(draft.SubmitReason);
        _planReasonLabel.Text = string.Join("；", reasons);
        _planReasonLabel.Visible = reasons.Count > 0;

        _planCostLabel.Text = "成本 " + CostText(draft.Cost);
        _planGapLabel.Text = "净缺口 " + GapText(draft.Gap);
        _planCostLabel.Visible = true;
        _planGapLabel.Visible = true;
        _planStockLabel.Text = draft.Stock.Length == 0 ? "" :
            "可用 " + string.Join(" · ", draft.Stock.Select(s => s.Name + " " + s.Available + "（在途" + s.InTransit + " 在制" + s.InProcess + "）"));
        _planStockLabel.Visible = _planStockLabel.Text.Length > 0;

        var supplyLines = new System.Collections.Generic.List<string>();
        var chosenRoute = draft.SupplyId is { } supplyId ? Array.Find(draft.SupplyRoutes, r => r.SupplyId == supplyId) : null;
        if (chosenRoute is { } route)
            supplyLines.Add("供料 " + route.Name + " 距离 " + route.RouteDistanceM.ToString("0.#", Inv) + "m · " + (route.RouteFound ? "路线可达" : "路线不可达"));
        else if (draft.SupplyId is { } supplyText)
            supplyLines.Add("供料 " + supplyText);
        if (draft.SupplyRoutes.Length > 1) _planStockLabel.Text += "\n" + string.Join("\n", draft.SupplyRoutes.Where(r => r != chosenRoute)
            .Select(r => "备选 " + r.Name + " " + r.RouteDistanceM.ToString("0.#", Inv) + "m · " + (r.RouteFound ? "可达" : "不可达")));
        _planStockLabel.Visible = _planStockLabel.Text.Length > 0;
        _planSupplyLabel.Text = string.Join("\n", supplyLines);
        _planSupplyLabel.Visible = supplyLines.Count > 0;

        var powerLines = new System.Collections.Generic.List<string>();
        if (draft.SourceId is { } sourceId && Array.Find(draft.PowerSources, s => s.SourceId == sourceId) is { } source)
            powerLines.Add("电源 " + source.Name + " " + source.DistanceM.ToString("0.#", Inv) + "/" + source.RangeM.ToString("0.#", Inv) + "m · " +
                (source.InRange ? "覆盖" : "超出范围"));
        else if (draft.PowerDistanceM is { } pd)
            powerLines.Add("电源 " + pd.ToString("0.#", Inv) + "/" + draft.PowerRangeM.ToString("0.#", Inv) + "m" +
                (pd <= draft.PowerRangeM ? " · 覆盖" : " · 超出范围"));
        else if (draft.PowerSources.Length == 0)
            powerLines.Add("尚无已建太阳能提供覆盖");
        else if (draft.PowerSources.OrderBy(s => s.DistanceM).First() is { } nearest)
            powerLines.Add("最近 " + nearest.Name + " " + nearest.DistanceM.ToString("0.#", Inv) + "/" + nearest.RangeM.ToString("0.#", Inv) + "m · " +
                (nearest.InRange ? "覆盖" : "超出范围"));
        foreach (var extra in draft.PowerSources)
        {
            if (extra.SourceId == draft.SourceId || powerLines.Count >= 3) continue;
            _planStockLabel.Text += "\n太阳能 " + extra.Name + " " + extra.DistanceM.ToString("0.#", Inv) + "m/" + extra.RangeM.ToString("0.#", Inv) + "m" + (extra.InRange ? " · 覆盖" : " · 超范围");
        }
        _planPowerLabel.Text = string.Join("\n", powerLines);
        _planPowerLabel.Visible = powerLines.Count > 0;

        _planConsequenceLabel.Text = draft.Consequence;
        _planConsequenceLabel.Visible = draft.Consequence.Length > 0;
        _planStockLabel.Visible = _planStockLabel.Text.Length > 0;
        _planRecoveryLabel.Text = draft.Recovery ? "恢复路径：为既有保障设施补覆盖/接线" : "";
        _planRecoveryLabel.Visible = draft.Recovery;
    }

    private void RefreshBottomBar(PlayerReadModel state)
    {
        bool boot = BootstrapOn;
        if (boot) EnsureBootstrapControls(_bootstrap!);
        _rowModes.Visible = boot;
        _hintsLabel.Text = _helpDetailed
            ? "WASD／方向键平移 · 右键拖动或双指平移 · 滚轮／捏合缩放 · Q/E 旋转 · F 定位 · 左键选择 · 右键取消 · Esc 菜单"
            : "WASD 平移 · 滚轮缩放 · 左键选择 · Esc 菜单";

        bool jobActive = state.Job is { Active: true };
        _confirmButton.Text = Building ? "建设·含前置授权" : "确认整平";
        bool canConfirm = Building
            ? (_plan is { } plan ? plan.CanSubmit : _preview is { Legal: true }) && state.Ready
            : _preview is { Legal: true } && !jobActive && state.Ready;
        _confirmButton.Disabled = !canConfirm || _commandCooldown > 0f;
        _cancelButton.Disabled = !jobActive;
        _pauseButton.Text = state.Paused ? "继续" : "暂停";

        _connectOpenButton.Visible = boot;
        _retryButton.Visible = boot;
        _connectNextButton.Visible = boot;
        var dev = _world.ReadDevelopment();
        _restockButton.Visible = dev.Enabled;
        _legacyButton.Visible = dev.Enabled;
        _legacyButton.Disabled = !state.Ready;
    }

    // ---- 显式接线 ----

    private void OpenConnectPanel()
    {
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
        else _connectHintLabel.Text = "按已建设施逐个选择；未选择供电源不会自动接线。";

        var selected = CurrentConnectOption();
        _connectCostLabel.Text = selected is { } selectedCost ? "成本 " + CostText(selectedCost.Cost) : "成本：先选择设施与供电源";
        _connectGapLabel.Text = selected is { } selectedGap ? "净缺口 " + GapText(selectedGap.Gap) : "净缺口：先选择设施与供电源";
        _connectReasonLabel.Text = selected is { CanSubmit: false } refused
            ? !refused.CandidateLegal ? refused.CandidateReason : refused.SubmitReason : "";
        _connectReasonLabel.Visible = _connectReasonLabel.Text.Length > 0;
        bool canSubmit = selected is { CanSubmit: true } && !(_lifecycle?.Busy ?? false) && _commandCooldown <= 0f;
        _connectSubmitButton.Disabled = !canSubmit;
        _connectSubmitButton.Text = selected is not null ? "确认接线" : "先选来源";
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
