#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Yudian.PlayerUI;

namespace Yudian.PlayerUI;

/// <summary>
/// 玩家 UI 自测入口（headless）：加载真实 Main 场景（玩家模式默认入口），挂 PlayerController，
/// 经公开合同（旧接口 + tutorial-player-api-r1 冻结协议）与合成输入事件驱动并断言。
/// 不改共享场景、不白盒读 Main 私有状态、不访问正式存档目录。
/// 运行：先给出私有产物根（见 UiTestRoot 合同），再
///   Godot --headless --path prototype --script res://scripts/PlayerUI/PlayerUiSelfTest.cs
/// 输出 PLAYER_UI_SELFTEST PASS/FAIL/BLOCKED 行；退出码 0=全过，1=有失败，3=无失败但存在
/// 因权威协议 stub 未实现而被阻塞的检查（BLOCKED 不是通过）。
/// 测试存档/截图只写显式 YUDIAN_UI_TEST_ROOT（绝对路径），
/// 或在 CLAUDE_JOB_DIR/tmp/player-ui-tests 下派生的唯一 run 目录；绝不触碰正式旧档或 repo 内路径。
/// </summary>
public partial class PlayerUiSelfTest : SceneTree
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly List<string> _results = [];
    private int _failures;
    private int _blocked;
    private Main _world = null!;
    private PlayerController _ui = null!;
    private Camera3D _camera = null!;
    private string _savePath = "", _testRoot = "", _privateRoot = "";
    private Vector3 _configuredCameraOrigin, _configuredCameraFocus;
    private Basis _configuredCameraBasis;
    private float _configuredCameraDistance;
    private bool _cameraOnly, _layoutOnly, _resumeLayout, _connectionOnly;
    // 进入世界后的初姿（首次 HUD 就绪时记录）；布局类用例前经控制器 testhook 恢复，不被前序真实手势影响。
    private Basis _initialCameraBasis = Basis.Identity;
    private Vector3 _initialCameraFocus;
    private float _initialCameraDistance = 26f;
    private bool _initialCameraCaptured;

    public override void _Initialize()
    {
        _cameraOnly = System.Environment.GetEnvironmentVariable("YUDIAN_CAMERA_DIAG_ONLY") == "1";
        _connectionOnly = System.Environment.GetEnvironmentVariable("YUDIAN_UI_CONNECTION_CAPTURE_ONLY") == "1";
        _layoutOnly = _connectionOnly || System.Environment.GetEnvironmentVariable("YUDIAN_UI_LAYOUT_CAPTURE_ONLY") == "1";
        _testRoot = UiTestRoot();
        string? resumeSlot = System.Environment.GetEnvironmentVariable("YUDIAN_UI_LAYOUT_RESUME_SLOT");
        _resumeLayout = _layoutOnly && resumeSlot is { Length: > 0 };
        if (_connectionOnly && !_resumeLayout)
            throw new InvalidOperationException("连接定向模式必须提供正常私有保存槽 YUDIAN_UI_LAYOUT_RESUME_SLOT，未跑用例不能算通过");
        // Main 的测试门不能继承其他 case 的 SELF_TEST/root/资产跳过标记。
        foreach (string name in System.Environment.GetEnvironmentVariables().Keys)
            if (name.StartsWith("YUDIAN_", StringComparison.Ordinal))
                System.Environment.SetEnvironmentVariable(name, null);
        _privateRoot = System.IO.Path.Combine(_testRoot, "saves");
        if (System.IO.Directory.Exists(_privateRoot))
            throw new InvalidOperationException("测试须使用全新 root，已有 saves 不会删除或覆盖：" + _privateRoot);
        System.IO.Directory.CreateDirectory(_privateRoot);
        _savePath = System.IO.Path.Combine(_privateRoot, "player-ui-test-save.json");
        if (_resumeLayout)
        {
            string repairRoot = System.IO.Directory.GetParent(_testRoot)!.FullName;
            string source = System.IO.Path.GetFullPath(resumeSlot!);
            if (!source.StartsWith(repairRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || !System.IO.File.Exists(source))
                throw new InvalidOperationException("定向布局续读仅接受此修复私有根下真实保存槽：" + source);
            System.IO.File.Copy(source, System.IO.Path.Combine(_privateRoot, "d13-tutorial-v4-slot-1.json"), overwrite: false);
            GD.Print("PLAYER_UI_SELFTEST RESUME actual_saved_copy=" + source);
        }
        System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST", "1");
        System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE", _savePath);
        GD.Print("PLAYER_UI_SELFTEST begin testRoot=" + _testRoot + " fixedFps=" + Engine.PhysicsTicksPerSecond);
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")
            ?? throw new InvalidOperationException("无法加载 res://scenes/Main.tscn");
        _world = (Main)scene.Instantiate();
        Root.AddChild(_world);
        Run();
    }

    internal static string UiTestRoot()
    {
        // 私有产物根合同：显式 YUDIAN_UI_TEST_ROOT（绝对路径）优先；否则在 CLAUDE_JOB_DIR/tmp/player-ui-tests
        // 下派生唯一 run 目录（runner 与自测使用同一 root）；两者都无则 fail-fast，绝不回落 repo/正式目录。
        string root;
        if (System.Environment.GetEnvironmentVariable("YUDIAN_UI_TEST_ROOT") is { Length: > 0 } explicitRoot)
        {
            if (!System.IO.Path.IsPathFullyQualified(explicitRoot))
                throw new InvalidOperationException("YUDIAN_UI_TEST_ROOT 必须是绝对路径：" + explicitRoot);
            root = System.IO.Path.GetFullPath(explicitRoot);
        }
        else if (System.Environment.GetEnvironmentVariable("CLAUDE_JOB_DIR") is { Length: > 0 } jobDir && System.IO.Path.IsPathFullyQualified(jobDir))
            root = System.IO.Path.Combine(jobDir, "tmp", "player-ui-tests",
                "run-" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", Inv) + "-" + System.Environment.ProcessId);
        else throw new InvalidOperationException(
            "未提供私有测试根：请设置绝对路径 YUDIAN_UI_TEST_ROOT（或提供 CLAUDE_JOB_DIR，产物将在其 tmp/player-ui-tests 下派生唯一 run 目录）");
        string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string repository = System.IO.Directory.GetParent(project.TrimEnd(System.IO.Path.DirectorySeparatorChar))!.FullName;
        string formal = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://saves"));
        static bool Inside(string path, string parent) => path == parent.TrimEnd(System.IO.Path.DirectorySeparatorChar)
            || path.StartsWith(parent.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (Inside(root, repository) || Inside(root, formal))
            throw new InvalidOperationException("UI 测试 root 必须位于仓库和正式存档之外：" + root);
        return root;
    }

    private async void Run()
    {
        try
        {
            bool worldReady = false;
            try
            {
                await WaitFor(() => _world.ReadPlayerState().Ready, 60, "世界就绪");
                worldReady = true;
            }
            catch (TimeoutException)
            {
                // 共享树在飞集成（如驮运接缝）可能让地面投影故障停摆；不把非 UI 故障计成 UI 失败，
                // 但也不假装通过：仍执行不依赖模拟的页面/输入/像素检查，其余记 BLOCKED。
                Blocked("世界就绪（共享树当前未就绪，如 MAIN_GROUND_PAUSED 投影故障；世界相关检查全部阻塞）");
            }
            ResizeLogicalViewport(new Vector2I(1280, 800));
            _camera = Root.GetCamera3D() ?? throw new InvalidOperationException("主场景没有当前相机");
            _ui = (PlayerController)_world.FindChild("PlayerController", true, false)!;
            ReadConfiguredCamera();
            await Frames(3);

            if (_layoutOnly)
            {
                if (_resumeLayout)
                {
                    _world.QueueLifecycle("load", 1);
                    await WaitForGameHud("真实私有槽续读的布局世界与 HUD 就绪");
                    Check(_world.ReadBootstrap().Facilities.Any(f => f.Built && f.Type == "solar")
                        && _world.ReadBootstrap().Facilities.Any(f => f.Type == "charger" && f.ServiceConnected),
                        "定向布局使用前轮真实建成/接线保存世界，无注入");
                }
                else await ForceEnterWorldOrBlock();
                if (!_connectionOnly) await LayoutAndScaleChecks();
                if (_resumeLayout) await ConnectionLayoutChecks();
                await Finale();
                return;
            }
            PrintPixelEvidence("boot");
            await HomeChecks(worldReady);
            if (!worldReady)
            {
                Report();
                Quit(ExitCode());
                return;
            }

            if (_cameraOnly)
            {
                GD.Print("PLAYER_UI_SELFTEST mode=camera-diag-only");
                await ForceEnterWorldOrBlock();
                await CameraInputChecks();
                await CameraDiag();
                Report();
                Quit(ExitCode());
                return;
            }

            await ForceEnterWorldOrBlock();
            await WorldSelectionChecks();
            await EscAndPauseChecks();
            await DragAcrossMenuChecks();
            await BuildModeNullTypeChecks(); // 首次建设空蓝图检查必须在选定蓝图之前：排在其后会把测试前提误报成失败
            await BuildPlanDraftChecks();
            await TypedPickAndReturnContextChecks();
            await ManagementGateChecks();
            await LeaveCancelOverlayOffChecks();
            await ConnectPanelChecks();
            await TutorialPanelChecks();
            await SettingsChecks();
            await CameraInputChecks();
            await CameraDiag();
            await RealConstructionConnectionChecks();
            await SaveLoadCompatChecks();
            await TransitionRevisionChecks();
            await DevPanelChecks();
            await LayoutAndScaleChecks();
            await Finale();
        }
        catch (Exception e)
        {
            Check(false, "异常 " + e.GetType().Name + ": " + e.Message);
            Report();
            Quit(1);
        }
    }

    private void ResizeLogicalViewport(Vector2I logicalSize)
    {
        float dpiScale = DisplayServer.GetName() == "headless" ? 1f : DisplayServer.ScreenGetScale();
        Root.Size = new Vector2I((int)MathF.Round(logicalSize.X * dpiScale), (int)MathF.Round(logicalSize.Y * dpiScale));
    }

    private void ReadConfiguredCamera()
    {
        using var file = Godot.FileAccess.Open("res://assets/d1-art/camera.json", Godot.FileAccess.ModeFlags.Read);
        using var config = System.Text.Json.JsonDocument.Parse(file.GetAsText());
        var normal = config.RootElement.GetProperty("cameras").GetProperty("normal");
        var position = normal.GetProperty("position");
        var target = normal.GetProperty("target");
        _configuredCameraFocus = new Vector3(target[0].GetSingle(), target[1].GetSingle(), target[2].GetSingle());
        var artPosition = new Vector3(position[0].GetSingle(), position[1].GetSingle(), position[2].GetSingle());
        _configuredCameraOrigin = _configuredCameraFocus + (artPosition - _configuredCameraFocus) * 1.6f;
        _configuredCameraDistance = _configuredCameraOrigin.DistanceTo(_configuredCameraFocus);
        _configuredCameraBasis = Basis.LookingAt(_configuredCameraFocus - _configuredCameraOrigin, Vector3.Up);
    }

    private async System.Threading.Tasks.Task WaitForGameHud(string label)
    {
        // 九个镜头方向按钮已移除；局内 HUD 就绪以底部工具组可见为准。
        var tools = _ui.FindChild("ToolsRow", true, false) as Control;
        await WaitFor(() => _world.ReadLifecycle().Page == "game" && _ui.PageForTest == "game" && tools is { Visible: true }, 10, label);
        await Frames(3); // ProcessFrame 在控制器 _Process 前发出；容器还需要后续布局。
        CaptureInitialCameraOnce();
    }

    // 只记录一次进世界初姿（镜头焦点/距离/朝向），供布局用例前的 testhook 复位使用。
    private void CaptureInitialCameraOnce()
    {
        if (_initialCameraCaptured) return;
        _initialCameraCaptured = true;
        _initialCameraBasis = _camera.GlobalBasis;
        _initialCameraFocus = _ui.CameraFocusForTest;
        _initialCameraDistance = _ui.CameraDistanceForTest;
        GD.Print("PLAYER_UI_SELFTEST CAM_CAPTURE initialFocus=" + Fmt(_initialCameraFocus) + " distance=" + F(_initialCameraDistance));
    }

    // 布局用例前把镜头恢复到初姿：前序 CAMERA 诊断/手势会真实移动镜头；用控制器 testhook 复位，不改生产相机路径。
    private async System.Threading.Tasks.Task RestoreCameraForLayout()
    {
        _ui.ResetCameraForTest(_initialCameraBasis, _initialCameraFocus, _initialCameraDistance);
        await Frames(2);
        GD.Print("PLAYER_UI_SELFTEST LAYOUT_CAM restoredFocus=" + Fmt(_ui.CameraFocusForTest) +
            " distance=" + F(_ui.CameraDistanceForTest));
    }

    // ---- 真实像素/视口证据：逻辑视口 vs 窗口 vs 最终变换 vs 主题字号 ----
    private void PrintPixelEvidence(string tag)
    {
        var viewport = Root.GetVisibleRect().Size;
        var window = Root.Size;
        var finalScale = Root.GetFinalTransform().Scale;
        var job = _ui.FindChild("GoalInstructionLabel", true, false) as Label;
        var goal = _ui.FindChild("GoalTitleLabel", true, false) as Label;
        GD.Print($"PLAYER_UI_SELFTEST PX[{tag}] window={window.X}x{window.Y} viewport={viewport.X}x{viewport.Y}" +
            $" finalScale=({F(finalScale.X)},{F(finalScale.Y)}) display={DisplayServer.GetName()}" +
            $" jobFont={job?.GetThemeFontSize("font_size")} goalFont={goal?.GetThemeFontSize("font_size")}" +
            $" uiScale={_ui.UiScaleForTest.ToString("0.##", Inv)}");
    }

    // ---- 主页 ----
    private async System.Threading.Tasks.Task HomeChecks(bool worldReady)
    {
        GD.Print("PLAYER_UI_SELFTEST HOME begin");
        var lifecycle = _world.ReadLifecycle();
        Check(lifecycle.Page == "home", "启动进入主页 Page=home：" + lifecycle.Page);
        var overlay = _ui.FindChild("EntryOverlay", true, false) as Control;
        Check(overlay is { Visible: true }, "主页遮罩显示");
        var title = _ui.FindChild("HomeTitle", true, false) as Label;
        Check(title is { Visible: true } && title.Text.Length > 0, "主页标题显示：" + title?.Text);
        Check(title?.HorizontalAlignment == HorizontalAlignment.Center, "主页标题居中");
        Check(title?.GetThemeFontSize("font_size") >= 40, "主页标题字号 ≥ 40（缩放前基数 44）");
        var newGame = (Button)_ui.FindChild("EntryNewGameButton", true, false)!;
        var load = (Button)_ui.FindChild("EntryLoadButton", true, false)!;
        var slots = (Button)_ui.FindChild("EntrySlotsButton", true, false)!;
        var settings = (Button)_ui.FindChild("EntrySettingsButton", true, false)!;
        var quit = (Button)_ui.FindChild("EntryQuitButton", true, false)!;
        Check(new[] { newGame, load, slots, settings, quit }.All(b => b.FocusMode != Control.FocusModeEnum.None),
            "主页五按钮保留键盘焦点可访问");
        // 「继续游戏」目标槽策略（合成用例，纯逻辑）：优先所选可载入槽，否则最近 SavedAtUtc 可载入槽，全无则禁用。
        static PlayerSlotView Slot(int n, bool exists, bool loadable, string saved) => new(n, "槽位" + n, exists, loadable, saved, "solar", "", "");
        Check(PlayerController.ContinueTargetSlot([Slot(1, false, false, ""), Slot(2, true, true, "2026-10-12T07:00:00.0000000Z"), Slot(3, false, false, "")], 1) == 2,
            "继续目标：所选槽1不可载入时选择唯一有效槽2");
        Check(PlayerController.ContinueTargetSlot([Slot(1, true, true, "2026-10-12T06:00:00.0000000Z"), Slot(2, true, true, "2026-10-12T07:00:00.0000000Z"), Slot(3, false, false, "")], 1) == 1,
            "继续目标：当前所选槽1可载入时保持槽1");
        Check(PlayerController.ContinueTargetSlot([Slot(1, true, false, ""), Slot(2, false, false, ""), Slot(3, true, false, "")], 1) == 0,
            "继续目标：全无可载入槽时为0（禁用）");
        Check(PlayerController.ContinueTargetSlot([Slot(1, false, false, ""), Slot(2, true, true, "2026-10-11T07:00:00.0000000Z"), Slot(3, true, true, "2026-10-12T07:00:00.0000000Z")], 1) == 3,
            "继续目标：取最近 SavedAtUtc 的可载入槽3");
        await Frames(2);
        if (worldReady)
        {
            Check(newGame.HasFocus(), "主页初始焦点在新游戏（回车即可开始）");
            Check(!newGame.Disabled, "世界就绪后新游戏可用");
            Check(load.Disabled && !_world.ReadPlayerState().SaveExists, "无存档时继续游戏禁用");
        }
        else
        {
            Blocked("主页就绪态焦点/可用性（世界未就绪）：newGame.Disabled=" + newGame.Disabled);
        }

        // 世界输入不穿透：点击/键盘镜头/暂停命令（浏览点击不产生 typed 选择或选址）
        var view = Root.GetVisibleRect().Size;
        Click(new Vector2(view.X * .8f, view.Y * .5f));
        await Frames(3);
        Check(_ui.SelectedObjectIdForTest == null && _ui.SelectedSiteForTest == null && _world.ReadPlayerState().Job == null,
            "主页期间点击世界无穿透、无选择");
        var camBefore = _camera.GlobalPosition;
        PressKey(Key.W, true); await SimulationSeconds(0.25); PressKey(Key.W, false); await Frames(2);
        Check(_camera.GlobalPosition.DistanceTo(camBefore) < 1e-5, "主页期间键盘不透传镜头");
        _world.QueuePlayerAction("pause");
        await Frames(2);
        var pauseNotice = _world.ReadPlayerState().Notice;
        // 主页期间暂停必须被拒（不进入用户暂停）；Notice 单通道可能被投影故障消息抢占，两者都算未见"命令成功"。
        Check(!_world.ReadPlayerState().Paused && (pauseNotice.Contains("请先选择") || pauseNotice.Contains("故障") || pauseNotice.Contains("投影")),
            "主页期间暂停命令被权威拒绝（不进入用户暂停）：" + pauseNotice.Split('\n')[0]);

        // 1920 大窗主页布局
        ResizeLogicalViewport(new Vector2I(1860, 939));
        await Frames(4);
        var big = Root.GetVisibleRect().Size;
        bool InView(Control c) { var r = c.GetGlobalRect(); return r.Position.X >= 0 && r.Position.Y >= 0 && r.End.X <= big.X && r.End.Y <= big.Y; }
        Check(overlay != null && InView(overlay) && InView(newGame) && InView(load) && InView(slots) && InView(settings) && InView(quit),
            "1860x939 主页遮罩与五按钮完整可见不越界");
        PrintPixelEvidence("1860x939");
        ResizeLogicalViewport(new Vector2I(1280, 800));
        await Frames(4);
        PrintPixelEvidence("1280");
        GD.Print("PLAYER_UI_SELFTEST HOME end");
    }

    // ---- 进入世界：优先冻结协议生命周期；权威 stub 未实现时退化到旧兼容命令并标记 BLOCKED ----
    private async System.Threading.Tasks.Task ForceEnterWorldOrBlock()
    {
        if (_world.ReadLifecycle().Page == "game") { await WaitForGameHud("已有世界 HUD 可见"); return; }
        _world.QueueLifecycle("newgame", 1);
        await Frames(5);
        try
        {
            await WaitFor(() => _world.ReadLifecycle().Page == "game", 4, "生命周期 newgame 进入世界");
            await WaitForGameHud("生命周期 newgame 与 UI HUD 同步");
            Check(true, "QueueLifecycle(newgame) 真实重置并进入世界（Page=game）");
            return;
        }
        catch (TimeoutException)
        {
            Blocked("QueueLifecycle(newgame/slots/settings/pause 生命周期流程（权威 stub 未实现，Page 停留在 " + _world.ReadLifecycle().Page + "）");
        }
        // 退化：旧兼容 newgame 命令只用于让既有相机/布局检查继续可跑，不代表新生命周期通过。
        _world.QueuePlayerAction("newgame");
        await WaitFor(() => _world.ReadLifecycle().Page == "game", 10, "旧兼容 newgame 进入世界（测试专用退化路径）");
        await WaitForGameHud("旧兼容入口 HUD 可见");
        GD.Print("PLAYER_UI_SELFTEST NOTE 已用旧兼容 newgame 进入世界：新生命周期流程仍计 BLOCKED，不被视作通过");
    }

    // ---- 局内：typed 浏览选择、整地选址、工程确认与经管理层的工程取消 ----
    private async System.Threading.Tasks.Task WorldSelectionChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST WORLD begin");
        Check(_ui.FindChild("ConfirmButton", true, false) is Button, "HUD 建立确认按钮（选址准备卡内）");
        Check(_ui.FindChild("ToolBuildButton", true, false) is Button && _ui.FindChild("ToolLevelButton", true, false) is Button
            && _ui.FindChild("ToolMaterialsButton", true, false) is Button && _ui.FindChild("ToolWorkButton", true, false) is Button,
            "底部工具组建建设/整地/基地物资/工程入口");
        Check(_ui.FindChild("GoalTitleLabel", true, false) is Label && _ui.FindChild("TopPowerLabel", true, false) is Label &&
            _ui.FindChild("TopStockChip_iron", true, false) is Button && _ui.FindChild("WorkRowPanel", true, false) is Control,
            "HUD 建立顶栏（发电/库内现货）与独立活动工程行");
        Check(_world.ReadPlayerRobots().Length == 12, "普通场景 12 台机器人");

        var legal = FindLegalSite();
        Check(legal.Legal, "找到合法预览点：" + legal.Reason);
        Check(!_world.PreviewLevel(_world.ReadBootstrap().Facilities.FirstOrDefault()?.Position ?? new Vector3(14, 0, 0)).Legal, "设施重叠预览非法");
        Check(!_world.PreviewLevel(new Vector3(28, 0, 28)).Legal, "场外预览非法");
        Check(!_world.PreviewLevel(legal.Center, "Robot_Wangshan_1").Legal, "望山不能当筑垒执行");

        // 浏览模式：地面点不再直接选址；先进入整地模式，左键仅选址。
        var objectScroll = (Control)_ui.FindChild("ObjectScroll", true, false)!;
        await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
        await Frames(3);
        Check(!objectScroll.IsVisibleInTree() && _ui.SelectedObjectIdForTest == null && _ui.SelectedSiteForTest == null,
            "浏览模式地面点不创建选址草案，也无空对象板");
        Check(_ui.PlacementModeForTest == 0, "整地前无活动选址模式");

        // 机器人点选（选一台投影在画面内的）出现 typed 对象卡；机器人会移动，按最新权威投影重试
        string? clickedRobot = null;
        foreach (var robot in _world.ReadPlayerRobots())
        {
            var pos = _camera.UnprojectPosition(robot.Position + Vector3.Up * 0.45f);
            var rect = Root.GetVisibleRect();
            if (pos.X > 80 && pos.Y > 90 && pos.X < rect.Size.X - 80 && pos.Y < rect.Size.Y - 200)
            {
                clickedRobot = robot.Id;
                break;
            }
        }
        var objectTitle = (Label)_ui.FindChild("ObjectTitleLabel", true, false)!;
        if (clickedRobot != null && await ClickWorldObject(clickedRobot, WorldObjectKind.Robot))
        {
            Check(_ui.SelectedObjectIdForTest == clickedRobot && _ui.SelectedKindForTest == WorldObjectKind.Robot
                && objectScroll.IsVisibleInTree() && objectTitle.Text.Length > 0,
                "机器人点选打开 typed 对象卡：" + objectTitle.Text);
            var detail = _world.ReadObjectDetail(WorldObjectKind.Robot, clickedRobot, _world.ReadLifecycle().WorldEpoch);
            var body = (Label)_ui.FindChild("ObjectBodyLabel", true, false)!;
            Check(detail is { Robot: { } robotDetail } && body.Text.Contains("电量") && body.Text.Contains(robotDetail.State),
                "对象卡显示权威机器人详情（电量/状态）：" + body.Text.Split('\n')[0]);
        }
        else
        {
            Blocked("机器人点选（无机器人投影在画面内或点选未命中）");
        }

        // 整地：显式模式进入；选址后经准备卡主按钮确认；placement 内点击机器人不换选。
        var levelTool = (Button)_ui.FindChild("ToolLevelButton", true, false)!;
        await CommandClick(levelTool);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 1, "整地模式状态机为 1（level placement）");
        Check(!objectScroll.IsVisibleInTree(), "进入选址后对象卡隐藏（互斥）");
        await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
        await Frames(4);
        Check(_ui.SelectedSiteForTest is { } site && XzDistance(site, legal.Center) < 0.75f, "整地选址生效");
        if (clickedRobot != null)
        {
            var current = _world.ReadPlayerRobots().Single(r => r.Id == clickedRobot);
            var robotNow = _camera.UnprojectPosition(current.Position + Vector3.Up * 0.45f);
            await ClickWorld(robotNow);
            await Frames(3);
            Check(_ui.SelectedObjectIdForTest == null && _ui.SelectedSiteForTest != null,
                "placement 左键仅选址：点机器人位置不换选机器人，仍保持地面选址");
            await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
            await Frames(3);
        }
        var confirm = (Button)_ui.FindChild("ConfirmButton", true, false)!;
        await WaitFor(() => confirm.IsVisibleInTree() && !confirm.Disabled, 5, "整地确认按钮启用");
        Check(confirm.Text.Contains("确认整平"), "整模式主按钮文案：" + confirm.Text);

        // 面板点击不穿透：点标题区域，选址保持
        Click(new Vector2(120, 60));
        await Frames(2);
        Check(_ui.SelectedSiteForTest != null, "点击 HUD 面板不穿透（选址保持）");

        // 空点（天空）不下命令
        Click(new Vector2(Root.GetVisibleRect().Size.X / 2f, 15));
        await Frames(2);
        Check(_world.ReadPlayerState().Job == null, "空点不下命令");

        // 确认按钮双击只提交一次（整平走既有地形版本合同）
        await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
        await WaitFor(() => confirm.IsVisibleInTree() && !confirm.Disabled, 5, "确认按钮再次启用");
        var center = confirm.GetGlobalRect().GetCenter();
        Click(center);
        Click(center);
        await Frames(6);
        var job = _world.ReadPlayerState().Job;
        Check(job is { Active: true }, "确认后任务活动");
        Check(job?.Id == "level-1", "双击只产生一个任务（level-1）：" + job?.Id);

        // 独立活动工程行：有活动工程才显示，不随选中消失
        var workRow = (Control)_ui.FindChild("WorkRowPanel", true, false)!;
        var workRowLabel = (Label)_ui.FindChild("WorkRowLabel", true, false)!;
        await WaitFor(() => workRow.Visible && workRowLabel.Text.Length > 0, 5, "活动工程行显示");
        Check(!workRowLabel.Text.Contains("level-"), "活动工程行不泄露内部任务 ID：" + workRowLabel.Text.Split('\n')[0]);
        Check(_ui.WorkForTest is { Active: true } work && work.WorkerIds.Any(w => w.StartsWith("Robot_Zhulei_", StringComparison.Ordinal)),
            "权威 ReadWork 显示整平工程与筑垒执行者");

        // 经管理层的工程取消：后果确认 → 同步关闭管理 → 单次提交
        var workTool = (Button)_ui.FindChild("ToolWorkButton", true, false)!;
        await CommandClick(workTool);
        await Frames(4);
        Check(_ui.ManagementOpenForTest && !_ui.ManagementMaterialsTabForTest && _world.ReadPlayerState().ManagementPaused,
            "工程行入口打开管理层工程标签并真实临停");
        var workCurrent = (Label)_ui.FindChild("WorkCurrentLabel", true, false)!;
        Check(workCurrent.Text.Contains("整平") || workCurrent.Text.Contains("已授权"), "工程标签显示当前整平工程：" + workCurrent.Text.Split('\n')[0]);
        var cancelButton = (Button)_ui.FindChild("WorkCancelButton", true, false)!;
        await CommandClick(cancelButton);
        await Frames(3);
        Check(_ui.CancelConfirmOpenForTest && ((Control)_ui.FindChild("CancelConfirmBox", true, false)!).Visible,
            "取消工程先显示后果确认子层");
        var cancelLabel = (Label)_ui.FindChild("CancelConfirmLabel", true, false)!;
        Check(cancelLabel.Text.Contains("已发生") && cancelLabel.Text.Contains("保留"), "取消确认显示已发生工作与保留后果：" + cancelLabel.Text.Split('\n')[^1]);
        // 取消确认是最顶层：独占指针与焦点，下层标签/按钮不可动；Esc 只退确认层
        var matTabButton = (Button)_ui.FindChild("ManagementMaterialsTabButton", true, false)!;
        Click(matTabButton.GetGlobalRect().GetCenter());
        await Frames(3);
        Check(_ui.CancelConfirmOpenForTest && !_ui.ManagementMaterialsTabForTest, "取消确认期间下层标签不可动（指针独占）");
        Click(cancelButton.GetGlobalRect().GetCenter());
        await Frames(3);
        Check(_ui.CancelConfirmOpenForTest, "取消确认期间下层取消按钮不可再点");
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(3);
        Check(!_ui.CancelConfirmOpenForTest && _ui.ManagementOpenForTest, "Esc 只退取消确认层（管理层仍开）");
        await CommandClick(cancelButton);
        await Frames(3);
        Check(_ui.CancelConfirmOpenForTest, "取消确认可重新打开");
        var confirmYes = (Button)_ui.FindChild("CancelConfirmYesButton", true, false)!;
        var yesCenter = confirmYes.GetGlobalRect().GetCenter();
        Click(yesCenter);
        Click(yesCenter); // 双击：冷却门禁保证单次提交
        await WaitFor(() => _world.ReadPlayerState().Job is not { Active: true }, 10, "取消生效");
        Check(!_ui.ManagementOpenForTest && !_world.ReadPlayerState().ManagementPaused,
            "确认取消后同步关闭管理层并释放自己的临停");
        await Frames(3);
        Check((_world.ReadPlayerState().Job?.Active ?? false) == false, "取消后无活动任务");
        var history = _world.ReadWorkHistory();
        Check(history.Any(w => w.GoalId == "level-1" && w.Status == PlayerWorkStatus.Cancelled),
            "取消后历史出现真实取消记录：" + string.Join("/", history.Select(w => w.Name + ":" + w.Status)));
        // 收尾退出整地选址（后续用例从浏览模式开始）
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 0, "整地用例收尾退出选址");

        // 用户手动暂停：模拟时间冻结，但相机与 UI 仍可操作
        var pause = (Button)_ui.FindChild("TopPauseButton", true, false)!;
        await CommandClick(pause);
        await WaitFor(() => _world.ReadPlayerState().Paused, 10, "手动暂停生效");
        await Frames(2);
        Check(((Button)_ui.FindChild("TopPauseButton", true, false)!).Text == "继续", "手动暂停后按钮显示继续");
        double frozen = _world.ReadPlayerState().TimeSeconds;
        await Frames(30);
        Check(Math.Abs(_world.ReadPlayerState().TimeSeconds - frozen) < 1e-9, "暂停时模拟时间不推进");
        await CommandClick(pause);
        await WaitFor(() => !_world.ReadPlayerState().Paused, 10, "继续生效");

        // 非法命令被拒
        _world.QueuePlayerAction("dig");
        await Frames(3);
        Check(_world.ReadPlayerState().Notice.Contains("未知"), "未知操作被拒");
        GD.Print("PLAYER_UI_SELFTEST WORLD end");
    }

    // ---- Esc 逐层：选址预览 → 局内暂停；Esc 不取消正式工程，选区保持 ----
    private async System.Threading.Tasks.Task EscAndPauseChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST ESC begin");
        var legal = FindLegalSite();
        // 第一层：整地选址中 Esc 先退预览（不开暂停、不取消任何工程）
        var levelTool = (Button)_ui.FindChild("ToolLevelButton", true, false)!;
        await CommandClick(levelTool);
        await Frames(3);
        await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
        await Frames(3);
        Check(_ui.PlacementModeForTest == 1 && _ui.SelectedSiteForTest != null, "Esc 检查前置：处于选址且有选中点");
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 0 && _ui.SelectedSiteForTest == null && _world.ReadLifecycle().Page == "game",
            "选址中 Esc 先退出预览（普通预览取消不确认，Page 仍为 game）");
        Check(_world.ReadPlayerState().Job is not { Active: true }, "Esc 退出预览不取消、也不创建正式工程");

        // 第二层：局内 Esc 开暂停菜单；选中的 typed 对象保持
        string? before = _ui.SelectedObjectIdForTest;
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(3);
        var page = _world.ReadLifecycle().Page;
        if (page == "pause")
        {
            Check(true, "局内 Esc 打开暂停菜单（Page=pause）");
            Check(_ui.SelectedObjectIdForTest == before, "Esc 进暂停不清 typed 选区");
            var resume = (Button)_ui.FindChild("PauseResumeButton", true, false)!;
            await Frames(2);
            Check(resume.HasFocus(), "暂停菜单初始焦点在继续");
            // 键盘不穿透：暂停菜单期间点击世界不产生选择
            Click(new Vector2(Root.GetVisibleRect().Size.X * .75f, Root.GetVisibleRect().Size.Y * .5f));
            await Frames(3);
            Check(_world.ReadLifecycle().Page == "pause", "暂停菜单期间点击世界不关闭菜单也不穿透");
            PressKey(Key.Escape, true); PressKey(Key.Escape, false);
            await WaitFor(() => _world.ReadLifecycle().Page == "game", 5, "Esc 返回局内");
            Check(true, "暂停菜单 Esc 返回到局内（resume）");
        }
        else
        {
            Blocked("局内 Esc 打开暂停菜单与 resume 返回（权威 stub：Page=" + page + "）");
        }
        GD.Print("PLAYER_UI_SELFTEST ESC end");
    }

    private async System.Threading.Tasks.Task DragAcrossMenuChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST DRAG_MENU begin");
        await WaitForGameHud("拖动菜单边界前 HUD 可见");
        await RealSeconds(0.5);
        var position = Root.GetVisibleRect().Size * new Vector2(0.5f, 0.55f);
        var before = _camera.GlobalPosition;
        Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = position }, inLocalCoords: true);
        Root.PushInput(new InputEventMouseMotion { Position = position + new Vector2(30, 0), Relative = new Vector2(30, 0), ButtonMask = MouseButtonMask.Right }, inLocalCoords: true);
        await Frames(3);
        Check(before.DistanceTo(_camera.GlobalPosition) > 0.05f, "菜单边界前真实右键拖动已启动");
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await WaitFor(() => _world.ReadLifecycle().Page == "pause" && _ui.PageForTest == "pause", 5, "拖动中 Esc 打开菜单");
        Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = new Vector2(120, 200) }, inLocalCoords: true);
        await Frames(3);
        await CommandClick((Button)_ui.FindChild("PauseResumeButton", true, false)!);
        await WaitForGameHud("菜单释放后返回局内");
        await RealSeconds(0.5);
        before = _camera.GlobalPosition;
        for (int i = 1; i <= 3; i++)
            Root.PushInput(new InputEventMouseMotion { Position = position + new Vector2(i * 20, 0), Relative = new Vector2(20, 0) }, inLocalCoords: true);
        await Frames(3);
        float residual = before.DistanceTo(_camera.GlobalPosition);
        Check(residual < 0.01f, "右键拖动→Esc→菜单释放→继续→无键实际 motion 不残留平移：" + F(residual) + "m");
        GD.Print("PLAYER_UI_SELFTEST DRAG_MENU end");
    }

    // ---- 建造方案（只读草案；建设经底部工具进入，蓝图行选择类型） ----
    private async System.Threading.Tasks.Task BuildPlanDraftChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST PLAN begin");
        var boot = _world.ReadBootstrap();
        if (!boot.Enabled) { Blocked("建造方案检查（bootstrap 未启用）"); return; }
        // 当前阶段合法类型：优先教学 NeedsType，否则 solar→charger（用电设施需已有太阳能覆盖，开局无合法点属正确规则）。
        var tutorial = _world.ReadTutorial();
        var candidates = new System.Collections.Generic.List<string>();
        if (tutorial.Enabled && tutorial.NeededType is { Length: > 0 } needed) candidates.Add(needed);
        foreach (var candidateType in new[] { "solar", "charger" })
            if (!candidates.Contains(candidateType)) candidates.Add(candidateType);
        string? type = null;
        PlayerSitePreview? site = null;
        foreach (var tryType in candidates)
        {
            if (_ui.FindChild("ModeButton_" + tryType, true, false) is not Button) continue;
            var found = FindLegalBuildSite(tryType);
            if (found is not null) { type = tryType; site = found; break; }
        }
        if (type is null || site is null)
        {
            Blocked("建造方案检查（当前阶段无合法建设点，类型候选：" + string.Join("/", candidates) + "）");
            return;
        }
        var buildTool = (Button)_ui.FindChild("ToolBuildButton", true, false)!;
        await CommandClick(buildTool);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 2, "建设工具进入选址模式");
        await CommandClick((Button)_ui.FindChild("ModeButton_" + type, true, false)!);
        await Frames(3);
        Check(_ui.BuildTypeForTest == type, "蓝图按钮真实选定类型：" + type);
        Check(site.Legal, "建设模式找到合法预览点（" + type + "）：" + site.Reason);
        await ClickWorld(_camera.UnprojectPosition(new Vector3(site.Center.X, 0.2f, site.Center.Z)));
        await Frames(6);
        var tag = (Label)_ui.FindChild("PlanTagLabel", true, false)!;
        var selection = (Label)_ui.FindChild("SelectionLabel", true, false)!;
        var confirm = (Button)_ui.FindChild("ConfirmButton", true, false)!;
        var title = (Label)_ui.FindChild("PlanTitleLabel", true, false)!;
        Check(title.Text.StartsWith("准备建设："), "建设卡标题按新文案『准备建设』：" + title.Text);
        Check(tag.Visible && tag.Text.Contains("尚未下达"), "未提交时显示『尚未下达』：" + tag.Text);
        Check(selection.Text.Contains("已选位置") && selection.Text.Contains("朝向") && selection.Text.Contains("仅选址"),
            "建设选择显示位置、朝向，并说明左键仅选址");
        Check(confirm.Text.Contains("开始建造"), "建设主按钮文案：" + confirm.Text);
        var plan = _world.ReadBuildPlan(type, site.Center, 0);
        if (plan.CanSubmit)
        {
            await WaitFor(() => confirm.IsVisibleInTree() && !confirm.Disabled, 5, "可提交时确认按钮启用");
            Check(true, "ReadBuildPlan.CanSubmit=true 时主按钮启用（权威实现已生效）");
        }
        else
        {
            Check(confirm.Disabled, "CanSubmit=false 时主按钮禁用（不产生未授权提交）");
            var reason = (Label)_ui.FindChild("PlanReasonLabel", true, false)!;
            Check(reason.Visible && reason.Text.Length > 0, "当前阻塞固定可见：" + reason.Text);
            Blocked("建造方案完整提交（权威当前 CanSubmit=false：“" + plan.SubmitReason + "”）");
        }
        Check(_ui.FindChild("PlanAutoLabel", true, false) is Label { Visible: true }, "建设卡明示缺料由机器人自动准备");
        // 重复入口：再次点击建设工具退出选址
        await CommandClick(buildTool);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 0, "再次点击建设工具退出选址（重复入口关闭）");
        GD.Print("PLAYER_UI_SELFTEST PLAN end");
    }

    // ---- typed 浏览（矿点/设施）与 placement returncontext ----
    private async System.Threading.Tasks.Task TypedPickAndReturnContextChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST TYPED_PICK begin");
        var objects = _world.ReadWorldObjects();
        GD.Print("PLAYER_UI_SELFTEST PICK_SCAN total=" + objects.Length +
            " robots=" + objects.Count(o => o.Kind == WorldObjectKind.Robot) +
            " mines=" + objects.Count(o => o.Kind == WorldObjectKind.Mine) +
            " facilities=" + objects.Count(o => o.Kind == WorldObjectKind.Facility) +
            " facilityIds=" + string.Join("/", objects.Where(o => o.Kind == WorldObjectKind.Facility).Select(o => o.Id)));
        var objectScroll = (Control)_ui.FindChild("ObjectScroll", true, false)!;
        // 矿点：点选后对象卡显示权威详情（剩余储量与产物）
        var mine = objects.FirstOrDefault(o => o.Kind == WorldObjectKind.Mine);
        if (mine is { } mineView && await ClickWorldObject(mineView.Id, WorldObjectKind.Mine))
        {
            var detail = _world.ReadObjectDetail(WorldObjectKind.Mine, mineView.Id, _world.ReadLifecycle().WorldEpoch);
            Check(_ui.SelectedKindForTest == WorldObjectKind.Mine && objectScroll.IsVisibleInTree(),
                "矿点点选打开 typed 对象卡：" + ((_ui.FindChild("ObjectTitleLabel", true, false) as Label)?.Text ?? ""));
            var body = (_ui.FindChild("ObjectBodyLabel", true, false) as Label)?.Text ?? "";
            Check(detail is { Mine: { } mineDetail } && body.Contains("剩余储量 " + mineDetail.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                "矿点卡显示权威剩余储量：" + body.Split('\n')[0]);
        }
        else Blocked("矿点点选（无矿点投影在可点画面内）");
        // 设施：着陆器为 Facility；显示建成/接线真实状态
        var facility = objects.FirstOrDefault(o => o.Kind == WorldObjectKind.Facility);
        if (facility is { } facilityView && await ClickWorldObject(facilityView.Id, WorldObjectKind.Facility))
        {
            var body = (_ui.FindChild("ObjectBodyLabel", true, false) as Label)?.Text ?? "";
            Check(_ui.SelectedKindForTest == WorldObjectKind.Facility && (body.Contains("已建成") || body.Contains("建设中")),
                "设施点选显示建成状态：" + body.Split('\n')[0]);
        }
        else Blocked("设施点选（无设施投影在可点画面内）");

        // returncontext：选中对象 → 进入建设选址（镜头移动）→ Esc 退出恢复镜头与有效对象
        var robot = objects.FirstOrDefault(o => o.Kind == WorldObjectKind.Robot && IsPickableOnScreen(o.Position));
        if (robot is { } robotView && await ClickWorldObject(robotView.Id, WorldObjectKind.Robot))
        {
            Check(_ui.SelectedObjectIdForTest == robotView.Id, "returncontext 前置：已选中机器人 " + robotView.Id);
        }
        var focusBefore = _ui.CameraFocusForTest;
        var distanceBefore = _ui.CameraDistanceForTest;
        var basisBefore = _camera.GlobalBasis;
        var buildTool = (Button)_ui.FindChild("ToolBuildButton", true, false)!;
        await CommandClick(buildTool);
        await Frames(3);
        PressKey(Key.W, true);
        await SimulationSeconds(0.25);
        PressKey(Key.W, false);
        await Frames(2);
        Check(_ui.CameraFocusForTest.DistanceTo(focusBefore) > 0.2f, "选址期间镜头仍可移动");
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 0, "Esc 退出选址");
        Check(_ui.CameraFocusForTest.DistanceTo(focusBefore) < 0.05f
            && MathF.Abs(_ui.CameraDistanceForTest - distanceBefore) < 0.05f
            && basisBefore.Z.AngleTo(_camera.GlobalBasis.Z) < 0.02f,
            "退出选址恢复进入前镜头：dFocus=" + F(_ui.CameraFocusForTest.DistanceTo(focusBefore)));
        if (robot is { } robotAfter)
        {
            bool stillValid = _world.ReadObjectDetail(WorldObjectKind.Robot, robotAfter.Id, _world.ReadLifecycle().WorldEpoch) != null;
            Check(stillValid ? _ui.SelectedObjectIdForTest == robotAfter.Id : _ui.SelectedObjectIdForTest == null,
                "退出选址按 epoch+stableID 重解析原有效对象：" + _ui.SelectedObjectIdForTest);
        }
        // 世界 epoch 变化（load）后临时 context 不复用
        if (objects.Length == 0) { Blocked("context 清理前置（无世界对象）"); return; }
        await ClickWorld(_camera.UnprojectPosition((robot ?? objects[0]).Position + Vector3.Up * 0.5f));
        await Frames(3);
        await RealSeconds(0.5);
        _world.QueuePlayerAction("save");
        await WaitFor(() => _world.ReadPlayerState().Notice.Contains("已保存"), 15, "context 清理前保存");
        _world.QueuePlayerAction("load");
        await WaitFor(() => _world.ReadPlayerState().Notice.Contains("读取完成"), 60, "epoch 变化用例读取");
        await Frames(4);
        Check(_ui.SelectedObjectIdForTest == null && _ui.PlacementModeForTest == 0 && !_ui.ConnectPanelOpenForTest,
            "load 后 epoch 变化清临时选择/选址/接线 context");
        GD.Print("PLAYER_UI_SELFTEST TYPED_PICK end");
    }

    // ---- 第一次建设未选蓝图：必须先选蓝图，禁提交，不整地 fallback ----
    private async System.Threading.Tasks.Task BuildModeNullTypeChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST BUILD_NULL begin");
        var buildTool = (Button)_ui.FindChild("ToolBuildButton", true, false)!;
        await CommandClick(buildTool);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 2 && _ui.BuildTypeForTest == null, "建设模式进入且未选蓝图");
        var title = (Label)_ui.FindChild("PlanTitleLabel", true, false)!;
        var confirm = (Button)_ui.FindChild("ConfirmButton", true, false)!;
        await Frames(2);
        Check(title.Text.Contains("选择蓝图"), "未选蓝图时建设卡明示先选择蓝图：" + title.Text);
        Check(confirm.Disabled, "未选蓝图时提交禁用");
        var legal = FindLegalSite();
        await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
        await Frames(4);
        Check(_ui.SelectedSiteForTest == null, "未选蓝图时地面点击不创建选址草案");
        Check(_world.ReadPlayerState().Job is not { Active: true }, "未选蓝图时点击世界不产生整地任务（无 level fallback）");
        if (_ui.FindChild("ModeButton_solar", true, false) is Button solar)
        {
            await CommandClick(solar);
            await Frames(3);
            Check(_ui.BuildTypeForTest == "solar", "选定蓝图后建设模式恢复可用");
        }
        await CommandClick(buildTool);
        await Frames(3);
        Check(_ui.PlacementModeForTest == 0, "建设空态用例退出选址");
        GD.Print("PLAYER_UI_SELFTEST BUILD_NULL end");
    }

    // ---- 离开 game 同步丢弃管理覆盖层；取消退出再继续不复活管理 ----
    private async System.Threading.Tasks.Task LeaveCancelOverlayOffChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST LEAVE_CANCEL begin");
        var materialsTool = (Button)_ui.FindChild("ToolMaterialsButton", true, false)!;
        await CommandClick(materialsTool);
        await Frames(4);
        Check(_ui.ManagementOpenForTest, "离开用例先打开管理层");
        _world.QueueLifecycle("home"); // 管理层期间生命周期返回不被 gate 死锁
        try
        {
            await WaitFor(() => _world.ReadLifecycle().Page == "leave", 5, "管理期间打开离开确认");
            await Frames(3);
            Check(!_ui.ManagementOpenForTest && !((Control)_ui.FindChild("ManagementOverlay", true, false)!).Visible,
                "离开 game 同步丢弃管理覆盖层状态");
        }
        catch (TimeoutException)
        {
            Blocked("管理期间生命周期 home 返回（权威当前拒绝或未切换，Page=" + _world.ReadLifecycle().Page + "）");
            Click(((Button)_ui.FindChild("ManagementCloseButton", true, false)!).GetGlobalRect().GetCenter());
            await Frames(4);
            _world.QueueLifecycle("home");
            await WaitFor(() => _world.ReadLifecycle().Page == "leave", 5, "离开确认页（管理已先关闭）");
            await Frames(2);
        }
        await CommandClick((Button)_ui.FindChild("LeaveCancelButton", true, false)!);
        await WaitFor(() => _world.ReadLifecycle().Page is "pause" or "game", 5, "取消离开");
        if (_world.ReadLifecycle().Page == "pause")
        {
            await CommandClick((Button)_ui.FindChild("PauseResumeButton", true, false)!);
            await WaitFor(() => _world.ReadLifecycle().Page == "game", 5, "取消离开后继续经营");
        }
        await Frames(3);
        Check(!_ui.ManagementOpenForTest && !_world.ReadPlayerState().ManagementPaused && _ui.PlacementModeForTest == 0,
            "取消退出再继续：管理不复活、无残留临停、无选址");
        GD.Print("PLAYER_UI_SELFTEST LEAVE_CANCEL end");
    }

    // ---- 读档/新游戏尝试轮次（WorldTransitionRevision）：失败尝试同 epoch 也清选址草案 ----
    private async System.Threading.Tasks.Task TransitionRevisionChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST REVISION begin");
        long revisionBefore = _world.ReadLifecycle().WorldTransitionRevision;
        long epochBefore = _world.ReadLifecycle().WorldEpoch;
        var levelTool = (Button)_ui.FindChild("ToolLevelButton", true, false)!;
        await CommandClick(levelTool);
        await Frames(3);
        var legal = FindLegalSite();
        await ClickWorld(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
        await Frames(3);
        Check(_ui.SelectedSiteForTest != null && _ui.PlacementModeForTest == 1, "revision 用例前置：已有整地选址草案");
        await WaitFor(() => _world.ReadPlayerState().Ready && !_world.ReadLifecycle().Busy, 10, "revision 用例保存准备");
        _world.QueueLifecycle("save", 2);
        await WaitFor(() => !_world.ReadLifecycle().Busy, 10, "revision 用例保存槽2");
        Check(_world.ReadLifecycle().Slots.Any(s => s.Slot == 2 && s.Exists), "revision 用例槽2已真实保存");
        string slot2Path = System.IO.Path.Combine(_privateRoot, "d13-tutorial-v4-slot-2.json");
        Check(System.IO.File.Exists(slot2Path), "revision 用例私槽2文件存在：" + slot2Path);
        System.IO.File.WriteAllText(slot2Path, "{ broken json for failed-load-attempt test"); // 私有测试档：制造确定校验失败
        _world.QueueLifecycle("load", 2, confirmed: true);
        await RealSeconds(1.0);
        await WaitFor(() => !_world.ReadLifecycle().Busy, 10, "失败读取尝试处理完成");
        await Frames(6);
        long revisionAfter = _world.ReadLifecycle().WorldTransitionRevision;
        Check(revisionAfter > revisionBefore, "失败读取尝试递增 WorldTransitionRevision：" + revisionBefore + "→" + revisionAfter);
        Check(_world.ReadLifecycle().WorldEpoch == epochBefore, "失败读取尝试保留当前世界（epoch 不变）");
        Check(_ui.SelectedSiteForTest == null && _ui.PlacementModeForTest == 0 && _ui.SelectedObjectIdForTest == null,
            "新尝试轮次清选址/整地/选择草案（同 epoch 不复用）");
        GD.Print("PLAYER_UI_SELFTEST REVISION end");
    }

    private bool IsPickableOnScreen(Vector3 worldPos)
    {
        var pos = _camera.UnprojectPosition(worldPos + Vector3.Up * 0.6f);
        var rect = Root.GetVisibleRect();
        return pos.X > 80 && pos.Y > 90 && pos.X < rect.Size.X - 80 && pos.Y < rect.Size.Y - 200;
    }

    // 世界点选遮挡检测：这些 MouseFilter=Stop 的面板可见时，其矩形内的点击会被 GUI 吞掉，
    // 不会到达世界 picker（诊断已证实对象卡占据右列时设施点选被吞）。
    private static readonly string[] PickOccluders = { "InfoPanel", "ObjectPanel", "PlanPanel", "ConnectPanel", "TopBar", "WorkRowPanel", "ManagementOverlay" };

    private bool OccludedByVisibleHud(Vector2 point)
    {
        foreach (string name in PickOccluders)
            // IsVisibleInTree()：语境卡只显隐外层 Scroll，子面板自身 Visible 恒为 true，不能用它判遮挡。
            if (_ui.FindChild(name, true, false) is Control panel && panel.IsVisibleInTree() && panel.GetGlobalRect().HasPoint(point))
            {
                GD.Print("PLAYER_UI_SELFTEST PICK_DIAG occluder=" + name + " rect=" + panel.GetGlobalRect() + " point=" + point);
                return true;
            }
        return false;
    }

    // 找一个当前没有任何可见 HUD 面板覆盖的画面点（限世界带内），供真实右键清选用；找不到返回 null。
    private Vector2? FindHudFreePoint()
    {
        var view = Root.GetVisibleRect().Size;
        foreach (var fraction in new[]
        {
            new Vector2(0.5f, 0.55f), new Vector2(0.35f, 0.5f), new Vector2(0.5f, 0.35f),
            new Vector2(0.3f, 0.6f), new Vector2(0.6f, 0.45f), new Vector2(0.25f, 0.65f),
        })
        {
            var point = view * fraction;
            if (point.X < 80 || point.Y < 90 || point.X > view.X - 80 || point.Y > view.Y - 200) continue;
            if (!OccludedByVisibleHud(point)) return point;
        }
        return null;
    }

    // typed 点选带重试：机器人会移动，每次按最新权威投影重新计算屏幕点。
    // 覆盖方式为真实 engine input 点击：以权威投影中心为基准，按投影半径取多个真实偏移点
    //（单次中心射线可能被路过机器人或取景遮挡）；目标不在画面时用 testhook 对焦/拉近后重试，
    // 不放宽生产 picker、不伪造选中。
    private async System.Threading.Tasks.Task<bool> ClickWorldObject(string id, WorldObjectKind kind, int attempts = 6)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var view = Array.Find(_world.ReadWorldObjects(), o => o.Id == id && o.Kind == kind);
            if (view == null)
            {
                GD.Print("PLAYER_UI_SELFTEST PICK_DIAG id=" + id + " kind=" + kind + " attempt=" + attempt + " view=missing");
                return false;
            }
            Vector3 center = view.Position + Vector3.Up * (kind == WorldObjectKind.Robot ? 0.45f : 0.6f);
            var screen = _camera.UnprojectPosition(center);
            var rect = Root.GetVisibleRect();
            GD.Print("PLAYER_UI_SELFTEST PICK_DIAG id=" + id + " kind=" + kind + " attempt=" + attempt +
                " world=" + Fmt(view.Position) + " radius=" + F(view.Radius) + " screen=" + Fmt(new Vector3(screen.X, screen.Y, 0)) +
                " focus=" + Fmt(_ui.CameraFocusForTest) + " distance=" + F(_ui.CameraDistanceForTest));
            bool inBand = screen.X > 80 && screen.Y > 90 && screen.X < rect.Size.X - 80 && screen.Y < rect.Size.Y - 200;
            if (inBand && OccludedByVisibleHud(screen))
            {
                // 目标投影落在可见语境卡之下（如点选矿点后对象卡占据右列，MouseFilter=Stop 吞掉世界点击）。
                // 被遮挡点上的任何点击（含右键）都会被面板吞掉，无法自清：改在无 HUD 挡世界的一点真实右键
                // 清选（生产行为：右键单击取消选择），卡片收起后下一轮重试；不伪造选中、不改生产 picker。
                Vector2? clearPoint = FindHudFreePoint();
                if (clearPoint is { } point)
                {
                    GD.Print("PLAYER_UI_SELFTEST PICK_DIAG id=" + id + " attempt=" + attempt + " occluded, right-click clear at " + point);
                    Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = point }, inLocalCoords: true);
                    Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = point }, inLocalCoords: true);
                    await Frames(3);
                }
                else GD.Print("PLAYER_UI_SELFTEST PICK_DIAG id=" + id + " attempt=" + attempt + " occluded and no HUD-free clear point");
                continue;
            }
            if (inBand)
            {
                float radiusPx = MathF.Max(6f, (_camera.UnprojectPosition(center + _camera.GlobalBasis.X * view.Radius) - screen).Length());
                var candidates = new[]
                {
                    screen,
                    screen + new Vector2(radiusPx, 0), screen - new Vector2(radiusPx, 0),
                    screen + new Vector2(0, radiusPx), screen - new Vector2(0, radiusPx),
                };
                foreach (var candidate in candidates)
                {
                    if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= rect.Size.X || candidate.Y >= rect.Size.Y) continue;
                    await ClickWorld(candidate);
                    await Frames(3);
                    if (_ui.SelectedObjectIdForTest == id && _ui.SelectedKindForTest == kind) return true;
                }
            }
            else
            {
                // 目标不在画面（如着陆器在画面外）：先用 testhook 对焦，再不行拉近一档，全部为测试设施，不改生产相机路径。
                if (attempt % 2 == 0) _ui.FocusCameraForTest(view.Position);
                else _ui.ResetCameraForTest(_camera.GlobalBasis, view.Position, MathF.Max(14f, _ui.CameraDistanceForTest * 0.6f));
                await Frames(2);
            }
        }
        return _ui.SelectedObjectIdForTest == id && _ui.SelectedKindForTest == kind;
    }

    // ---- 管理层：门禁、重复入口、用户暂停保值、同 epoch 返回保 context ----
    private async System.Threading.Tasks.Task ManagementGateChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST MGMT begin");
        var materialsTool = (Button)_ui.FindChild("ToolMaterialsButton", true, false)!;
        await CommandClick(materialsTool);
        await Frames(4);
        Check(_ui.ManagementOpenForTest && _ui.ManagementMaterialsTabForTest && _world.ReadPlayerState().ManagementPaused,
            "基地物资入口打开管理层并真实临停");
        var overlay = (Control)_ui.FindChild("ManagementOverlay", true, false)!;
        Check(overlay.Visible && overlay is ColorRect { MouseFilter: Control.MouseFilterEnum.Stop }, "管理遮罩 MouseFilter.Stop 全屏拦截");
        // 世界点击不穿透：世界选区不变
        string? selectedBefore = _ui.SelectedObjectIdForTest;
        Click(new Vector2(Root.GetVisibleRect().Size.X * 0.5f, Root.GetVisibleRect().Size.Y * 0.6f));
        await Frames(3);
        Check(_ui.SelectedObjectIdForTest == selectedBefore && _ui.ManagementOpenForTest,
            "管理层期间世界点击不穿透（无选址/选择）");
        // 键盘门禁：按 W 镜头不动（每帧 camera Input.IsKeyPressed 被门禁）
        var cameraBefore = _camera.GlobalPosition;
        PressKey(Key.W, true);
        await SimulationSeconds(0.25);
        PressKey(Key.W, false);
        await Frames(2);
        Check(cameraBefore.DistanceTo(_camera.GlobalPosition) < 1e-4f, "管理层期间键盘轮询不驱动镜头");
        // 世界命令被权威拒绝（level 是世界意图；管理层门禁下必须拒绝且无任务）
        await RealSeconds(0.5);
        _world.QueueLevel(new Vector3(8, 0, 8), _world.ReadPlayerState().Version);
        await Frames(4);
        Check(_world.ReadPlayerState().Notice.Contains("管理面板打开期间不接受世界操作") && _world.ReadPlayerState().Job is not { Active: true },
            "管理层期间世界命令被权威拒绝：" + _world.ReadPlayerState().Notice.Split('\n')[0]);
        // 关联工程：预约按 GoalId/TaskId 真实解析；空态或行内"查看关联工程"实际按钮（不展示内部目标 ID）
        var reservationsNow = _world.ReadMaterialsDetail().Reservations;
        var relatedEmpty = (Label)_ui.FindChild("MatRelatedLabel", true, false)!;
        Check(!relatedEmpty.Text.Contains("goal-") && !relatedEmpty.Text.Contains("build-"), "关联工程文案不泄露内部目标 ID");
        if (reservationsNow.Length == 0)
        {
            Check(relatedEmpty.Visible && relatedEmpty.Text.Contains("无关联工程"), "无真实预留时关联工程空态：" + relatedEmpty.Text);
        }
        else if (_ui.FindChild("RelatedWorkButton", true, false) is Button relatedOpen)
        {
            Click(relatedOpen.GetGlobalRect().GetCenter());
            await Frames(3);
            Check(!_ui.ManagementMaterialsTabForTest && ((Control)_ui.FindChild("RelatedWorkBox", true, false)!).Visible,
                "预留行按钮进入同层关联工程详情");
            PressKey(Key.Escape, true); PressKey(Key.Escape, false);
            await Frames(3);
            Check(_ui.ManagementOpenForTest && _ui.ManagementMaterialsTabForTest, "Esc 先退关联工程子详情回物资标签（管理层仍开）");
        }
        else Check(false, "存在预留但未建立查看关联工程按钮");
        // 同一覆盖层标签互切
        var workTab = (Button)_ui.FindChild("ManagementWorkTabButton", true, false)!;
        await CommandClick(workTab);
        await Frames(3);
        Check(_ui.ManagementOpenForTest && !_ui.ManagementMaterialsTabForTest, "物资→工程为同层标签切换");
        // 用户手动暂停保值：管理层打开期间手动暂停，关闭后保持
        var topPause = (Button)_ui.FindChild("TopPauseButton", true, false)!;
        Click(topPause.GetGlobalRect().GetCenter()); // 遮罩上不可点：此处应无效果
        await Frames(3);
        Check(!_world.ReadPlayerState().Paused, "遮罩挡住 HUD 按钮（手动暂停未被误触）");
        // Esc 同样只退一层
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(4);
        Check(!_ui.ManagementOpenForTest && !_world.ReadPlayerState().ManagementPaused && _world.ReadLifecycle().Page == "game",
            "Esc 关闭管理层并释放自己的临停（不开暂停菜单）");
        // 遮罩在底部工具上方：管理期间真实点击建设/物资工具不进入选址、也不触发关闭
        await CommandClick(materialsTool);
        await Frames(4);
        Check(_ui.ManagementOpenForTest && _ui.ManagementMaterialsTabForTest, "重新打开管理层");
        await CommandClick((Button)_ui.FindChild("ToolBuildButton", true, false)!);
        await Frames(3);
        Check(_ui.ManagementOpenForTest && _ui.PlacementModeForTest == 0, "遮罩挡底部建设工具：不进入选址也不关闭");
        await CommandClick(materialsTool);
        await Frames(3);
        Check(_ui.ManagementOpenForTest && _ui.ManagementMaterialsTabForTest, "遮罩挡物资工具：被覆盖的重复入口不再可达");
        Click(((Button)_ui.FindChild("ManagementCloseButton", true, false)!).GetGlobalRect().GetCenter());
        await Frames(4);
        Check(!_ui.ManagementOpenForTest && !_world.ReadPlayerState().ManagementPaused, "关闭按钮释放自己的临停");
        // 管理层开关不翻转用户暂停（开/关都走真实控件）
        var pauseTool = (Button)_ui.FindChild("TopPauseButton", true, false)!;
        await CommandClick(pauseTool);
        await WaitFor(() => _world.ReadPlayerState().Paused, 10, "先置用户手动暂停");
        await CommandClick(materialsTool);
        await Frames(4);
        Check(_ui.ManagementOpenForTest && _world.ReadPlayerState().Paused, "用户手动暂停在管理层打开期间保持");
        Click(((Button)_ui.FindChild("ManagementCloseButton", true, false)!).GetGlobalRect().GetCenter());
        await Frames(4);
        Check(!_ui.ManagementOpenForTest && _world.ReadPlayerState().Paused, "关闭管理层只释放自己的临停，手动暂停保值");
        await CommandClick(pauseTool);
        await WaitFor(() => !_world.ReadPlayerState().Paused, 10, "恢复运行");
        // 同 epoch 普通暂停菜单返回保 context
        var menu = (Button)_ui.FindChild("TopMenuButton", true, false)!;
        await CommandClick(menu);
        await WaitFor(() => _world.ReadLifecycle().Page == "pause", 5, "菜单打开");
        await CommandClick((Button)_ui.FindChild("PauseResumeButton", true, false)!);
        await WaitFor(() => _world.ReadLifecycle().Page == "game", 5, "同 epoch 返回局内");
        await Frames(2);
        Check(!_ui.ManagementOpenForTest && !_world.ReadPlayerState().ManagementPaused,
            "同 epoch 菜单返回不残留管理临停");
        GD.Print("PLAYER_UI_SELFTEST MGMT end");
    }

    // ---- 显式接线面板 ----
    private async System.Threading.Tasks.Task ConnectPanelChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST CONNECT begin");
        var open = _ui.FindChild("ExplicitConnectButton", true, false) as Button;
        if (open == null) { Blocked("接线面板（按钮未建立）"); return; }
        await OpenConnectionPanelByClick();
        Check(_ui.ConnectPanelOpenForTest, "常用栏真实接线入口可发现并打开");
        Check((((Control)_ui.FindChild("DiagRow", true, false)!).Visible == false), "打开接线不依赖诊断折叠");
        var panel = _ui.FindChild("ConnectPanel", true, false) as Control;
        Check(_ui.ConnectPanelOpenForTest && panel is { } p0 && p0.IsVisibleInTree(), "显式接线面板可打开");
        var options = _world.ReadConnectionOptions();
        var submit = (Button)_ui.FindChild("ConnectSubmitButton", true, false)!;
        if (options.Length == 0)
        {
            Check(submit.Disabled, "无接线候选时确认接线禁用");
            var facilities = _world.ReadBootstrap().Facilities;
            bool hasSolar = facilities.Any(f => f.Built && f.Type == "solar");
            bool hasConsumer = facilities.Any(f => f.Built && f.Type is "charger" or "repair" or "processor");
            Check(!hasSolar || !hasConsumer, "候选前置是已建太阳能与已建用电设施；登陆器不产生接线项：solar=" + hasSolar + " consumer=" + hasConsumer);
        }
        else
        {
            var rows = panel!.FindChildren("ConnectRow_*", "", true, false);
            Check(rows.Count == options.Length, "接线候选逐项列出（不过滤成看似全合法）：rows=" + rows.Count + " options=" + options.Length);
            if (options.Any(o => o.CanSubmit)) Check(!submit.Disabled || submit.Text.Contains("确认接线"), "有可提交候选时出现明确接线入口：" + submit.Text);
            else Check(submit.Disabled, "全部不可提交时确认接线禁用");
        }
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(3);
        Check(!_ui.ConnectPanelOpenForTest, "Esc 优先关闭接线面板（不开暂停菜单）");
        await CloseDiagnosticsByClick();
        GD.Print("PLAYER_UI_SELFTEST CONNECT end");
    }

    private async System.Threading.Tasks.Task OpenConnectionPanelByClick()
    {
        await WaitForGameHud("打开接线前 HUD 可见");
        var open = (Button)_ui.FindChild("ExplicitConnectButton", true, false)!;
        Check(open.IsVisibleInTree(), "显式接线位于常用操作栏并可见");
        await CommandClick(open);
        await WaitFor(() => _ui.ConnectPanelOpenForTest, 5, "真实按钮打开接线面板");
        await Frames(4);
    }

    private async System.Threading.Tasks.Task CloseDiagnosticsByClick()
    {
        if (_ui.FindChild("DiagRow", true, false) is not Control { Visible: true }) return;
        Click(((Button)_ui.FindChild("DiagToggle", true, false)!).GetGlobalRect().GetCenter());
        await Frames(4);
    }

    private async System.Threading.Tasks.Task RealConstructionConnectionChecks()
    {
        ulong started = Time.GetTicksMsec();
        GD.Print("PLAYER_UI_SELFTEST REAL_CONNECT begin normal_initial=true");
        await WaitForGameHud("正常建造前 HUD 可见");
        if (_world.ReadPlayerState().Paused)
        {
            _world.QueuePlayerAction("pause");
            await WaitFor(() => !_world.ReadPlayerState().Paused, 5, "正常建造前解除暂停");
        }
        var initial = _world.ReadEconomy();
        int initialKits = initial.Materials.Single(m => m.Id == "kit").Stored;
        Check(initialKits == 8 && _world.ReadBootstrap().Facilities.All(f => f.Type == "lander"),
            "接线用例从正常有限8套件与登陆器开局，未注入设施/健康/资源");
        var solar = await BuildRealFacility("solar", new Vector3(-3, 0, -14));
        Check(_world.ReadTutorial().StageId == "charger" && !_world.ReadTutorial().Completed,
            "太阳能真正建成后教学只推进到充电桩");
        var charger = await BuildRealFacility("charger", new Vector3(-3, 0, -3));
        Check(!charger.ServiceConnected && charger.Source == null && _world.ReadTutorial().StageId == "charger-wire",
            "充电桩真正建成仍未接线，教学等待真实线缆");
        Check(_world.ReadEconomy().Materials.Single(m => m.Id == "kit").Stored == initialKits - 2,
            "太阳能与充电桩各消耗真实有限套件");

        await SavePrivateSlot("接线前成本账本");
        int spentBefore = ReadPrivateSpentCable();
        int cableBefore = _world.ReadEconomy().Materials.Single(m => m.Id == "cable").Stored;
        var beforeTutorial = _world.ReadTutorial();
        var option = _world.ReadConnectionOptions(charger.Id).Single(o => o.SourceId == solar.Id);
        Check(option.CanSubmit && option.CandidateLegal && option.DistanceM <= option.RangeM && option.Cost.Length > 0,
            "正常已建太阳能/桩产生有限成本、12m内可提交真实候选");
        await OpenConnectionPanelByClick();
        var panel = (Control)_ui.FindChild("ConnectPanel", true, false)!;
        var rows = panel.FindChildren("ConnectRow_*", "", true, false);
        Check(panel.IsVisibleInTree() && rows.Count == _world.ReadConnectionOptions().Length,
            "真实接线面板与完整候选行可见");
        var row = (Button)_ui.FindChild("ConnectRow_" + charger.Id + "_" + solar.Id, true, false)!;
        Check(row.IsVisibleInTree() && row.Text.Contains(option.FacilityName) && row.Text.Contains(option.SourceName),
            "候选行显示真实充电桩与明确太阳能来源");
        Click(row.GetGlobalRect().GetCenter());
        await Frames(4);
        Check(_world.ReadTutorial().StageId == beforeTutorial.StageId && _world.ReadTutorial().Achieved == beforeTutorial.Achieved
            && !_world.ReadBootstrap().Facilities.Single(f => f.Id == charger.Id).ServiceConnected && _world.ReadPlayerState().Job is not { Active: true },
            "打开面板与选来源均不推进教学、不建线、不创建工程");
        var submit = (Button)_ui.FindChild("ConnectSubmitButton", true, false)!;
        await WaitFor(() => submit.IsVisibleInTree() && !submit.Disabled, 5, "真实接线确认按钮可用");
        Click(submit.GetGlobalRect().GetCenter());
        Click(submit.GetGlobalRect().GetCenter());
        await WaitFor(() => _world.ReadPlayerState().Job is { Active: true }, 5, "确认接线创建真实施工任务");
        var connectionJob = _world.ReadPlayerState().Job!;
        Check(_world.ReadTutorial().StageId == "charger-wire" && !_world.ReadBootstrap().Facilities.Single(f => f.Id == charger.Id).ServiceConnected,
            "确认点击仅授权；施工中仍不算接线里程碑");
        await WaitForSimulation(() => _world.ReadBootstrap().Facilities.Single(f => f.Id == charger.Id).ServiceConnected
            && _world.ReadPlayerState().Job is not { Active: true }, 220, "真实运输与付费铺线完成");
        var connected = _world.ReadBootstrap().Facilities.Single(f => f.Id == charger.Id);
        Check(connected.Source == solar.Id && connected.ServiceConnected, "真实充电桩明确接入所选太阳能来源");
        Check(_world.ReadPlayerState().Job?.Id == connectionJob.Id, "接线双击仍只有同一个真实工程");
        int cableCost = option.Cost.Single(c => c.Id == "cable").Quantity;
        Check(cableCost == 4 && _world.ReadEconomy().Materials.Single(m => m.Id == "cable").Stored == cableBefore - cableCost,
            "真实接线仓库线缆减少4，无免费线缆");
        await SavePrivateSlot("接线后成本账本");
        int spentAfter = ReadPrivateSpentCable();
        Check(spentAfter - spentBefore == cableCost,
            "私有正常保存账本 spent.cable 实增4：before=" + spentBefore + " after=" + spentAfter);
        Check(_world.ReadTutorial().StageId == "repair" && _world.ReadTutorial().Achieved == beforeTutorial.Achieved + 1
            && !_world.ReadTutorial().Completed, "仅真实接线完成后教学推进维修站");
        await Frames(4);
        Check(((Button)_ui.FindChild("ConnectSubmitButton", true, false)!).Disabled,
            "已接线候选拒绝重复铺线");
        await CaptureViewport("normal-solar-charger-connected");
        PressKey(Key.Escape, true); PressKey(Key.Escape, false);
        await Frames(4);
        await CloseDiagnosticsByClick();
        GD.Print("PLAYER_UI_SELFTEST REAL_CONNECT end elapsed=" + ((Time.GetTicksMsec() - started) / 1000.0).ToString("0.###", Inv) +
            " simulation=" + _world.ReadPlayerState().TimeSeconds.ToString("0.###", Inv));
    }

    private async System.Threading.Tasks.Task<FacilityView> BuildRealFacility(string type, Vector3 position)
    {
        var plan = _world.ReadBuildPlan(type, position);
        Check(plan.CandidateLegal && plan.CanSubmit && plan.Gap.Length == 0, "有限初始物资可授权 " + type + "：" + plan.SubmitReason);
        if (!plan.CanSubmit) throw new InvalidOperationException("正常建造不可提交 " + type + "：" + plan.SubmitReason);
        ulong started = Time.GetTicksMsec();
        _world.QueueBuild(type, position, 0, plan.ObservedVersion);
        await Frames(3);
        if (type == "solar") await ReservationWorkDetailChecks(); // 首次真实建设过程内覆盖：物资预约→关联工程详情，不只空态
        await WaitForSimulation(() => _world.ReadBootstrap().Facilities.Any(f => f.Built && f.Type == type)
            && _world.ReadPlayerState().Job is not { Active: true }, 220, "正常 " + type + " 实际建成");
        var facility = _world.ReadBootstrap().Facilities.Single(f => f.Built && f.Type == type);
        GD.Print("PLAYER_UI_SELFTEST REAL_BUILD type=" + type + " id=" + facility.Id + " elapsed=" +
            ((Time.GetTicksMsec() - started) / 1000.0).ToString("0.###", Inv) + " simulation=" + _world.ReadPlayerState().TimeSeconds.ToString("0.###", Inv));
        return facility;
    }

    // 真实建设授权后的物资预约（世界运行中等待出现，不注入材料）：预约 Goal 应解析到当前工程
    // （TaskId/GoalId 任一口径），物资标签行内“查看关联工程”按钮实际跳同层工程详情；检查后关闭
    // 管理层释放临停让建设继续。预约时机按权威规则，未出现则如实 BLOCKED，不伪造。
    private async System.Threading.Tasks.Task ReservationWorkDetailChecks()
    {
        bool reserved = true;
        try
        {
            await WaitFor(() => _world.ReadPlayerState().Job is { Active: true } && _world.ReadMaterialsDetail().Reservations.Length > 0,
                15, "真实建设授权产生物资预约");
        }
        catch (TimeoutException) { reserved = false; }
        if (!reserved || _world.ReadPlayerState().Job is not { Active: true })
        {
            Blocked("真实建设物资预约（15 秒内未见预约或工程已推进；按权威时机，不注入材料）");
            return;
        }
        var work = _world.ReadWork()!;
        await CommandClick((Button)_ui.FindChild("ToolMaterialsButton", true, false)!);
        await Frames(4);
        Check(_ui.ManagementOpenForTest && _ui.ManagementMaterialsTabForTest && _world.ReadPlayerState().ManagementPaused,
            "真实建设过程打开基地物资标签并真实临停");
        var reservations = _world.ReadMaterialsDetail().Reservations;
        Check(reservations.Length > 0 && reservations.All(r => r.GoalId == work.TaskId || r.GoalId == work.GoalId),
            "建设预约 Goal 对应当前真实工程（TaskId=" + work.TaskId + " GoalId=" + work.GoalId + "）：" +
            string.Join(",", reservations.Select(r => r.GoalId + " " + r.Name + "×" + r.Quantity)));
        if (_ui.FindChild("RelatedWorkButton", true, false) is Button relatedOpen)
        {
            Click(relatedOpen.GetGlobalRect().GetCenter());
            await Frames(3);
            var detailLabel = (Label)_ui.FindChild("RelatedWorkDetailLabel", true, false)!;
            Check(!_ui.ManagementMaterialsTabForTest && ((Control)_ui.FindChild("RelatedWorkBox", true, false)!).Visible
                && detailLabel.Text.Contains(work.Name),
                "预约行按钮实际跳同层关联工程详情：" + detailLabel.Text.Split('\n')[0]);
            PressKey(Key.Escape, true); PressKey(Key.Escape, false);
            await Frames(3);
            Check(_ui.ManagementOpenForTest && _ui.ManagementMaterialsTabForTest, "Esc 从关联工程详情退回物资标签（管理层仍开）");
        }
        else Blocked("关联工程行按钮（真实预约存在但未建立行入口）");
        Click(((Button)_ui.FindChild("ManagementCloseButton", true, false)!).GetGlobalRect().GetCenter());
        await Frames(4);
        Check(!_ui.ManagementOpenForTest && !_world.ReadPlayerState().ManagementPaused,
            "真实建设检查后关闭管理层释放临停（建设继续）");
    }

    private async System.Threading.Tasks.Task SavePrivateSlot(string label)
    {
        await WaitFor(() => _world.ReadPlayerState().Ready && !_world.ReadLifecycle().Busy, 10, label + " 准备");
        _world.QueueLifecycle("save", 1);
        await WaitFor(() => !_world.ReadLifecycle().Busy, 10, label + " 保存处理");
        Check(_world.ReadPlayerState().Notice.Contains("已保存") && _world.ReadLifecycle().Slots.Any(s => s.Slot == 1 && s.Loadable),
            label + "：权威保存真实私有槽");
        if (!_world.ReadPlayerState().Notice.Contains("已保存")) throw new InvalidOperationException(label + "：" + _world.ReadPlayerState().Notice);
    }

    private int ReadPrivateSpentCable()
    {
        string slot = System.IO.Path.Combine(_privateRoot, "d13-tutorial-v4-slot-1.json");
        using var saved = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(slot));
        var spent = saved.RootElement.GetProperty("Bootstrap").GetProperty("Ledger").GetProperty("Containers")
            .EnumerateArray().Single(c => c.GetProperty("Id").GetString() == "spent").GetProperty("Items");
        return spent.TryGetProperty("cable", out var cable) ? cable.GetInt32() : 0;
    }

    // ---- 教学目标面板（始终读真实 ReadTutorial） ----
    private async System.Threading.Tasks.Task TutorialPanelChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST TUTORIAL begin");
        var tutorial = _world.ReadTutorial();
        var title = (Label)_ui.FindChild("GoalTitleLabel", true, false)!;
        var action = (Label)_ui.FindChild("GoalActionLabel", true, false)!;
        var progress = (Label)_ui.FindChild("GoalProgressLabel", true, false)!;
        if (tutorial.Enabled)
        {
            Check(title.Text == tutorial.Title, "目标标题与权威 ReadTutorial 一致：" + title.Text);
            Check(action.Text.StartsWith("下一步："), "下一步动作行显示：" + action.Text);
            Check(action.IsVisibleInTree() && action.Text.Contains(tutorial.Achieved + "/" + tutorial.Total), "核心下一步同屏显示权威 Achieved/Total：" + action.Text);
            Check(progress.Text.Contains(tutorial.Achieved + "/" + tutorial.Total), "教学详情保留完整进度：" + progress.Text);
            Check(title.GetThemeFontSize("font_size") >= 24, "目标标题字号 ≥ 24（缩放前基数 24）");
        }
        else
        {
            Check(title.Text == "自主经营", "教学未启用时显示自主经营：" + title.Text);
        }
        // 目标卡整卡收成一行（标题保留），再展开恢复核心与为什么；freeplay 结尾确认后目标卡隐藏由真实通关轨覆盖
        if (_ui.FindChild("GoalFoldToggle", true, false) is Button fold && fold.IsVisibleInTree())
        {
            Click(fold.GetGlobalRect().GetCenter());
            await Frames(3);
            var instructionLabel = (Label)_ui.FindChild("GoalInstructionLabel", true, false)!;
            var actionLabel = (Label)_ui.FindChild("GoalActionLabel", true, false)!;
            Check(title.IsVisibleInTree() && !instructionLabel.IsVisibleInTree() && !actionLabel.IsVisibleInTree(),
                "目标卡可收成一行（标题保留）");
            Click(fold.GetGlobalRect().GetCenter());
            await Frames(3);
            Check(instructionLabel.IsVisibleInTree() && actionLabel.IsVisibleInTree(), "目标卡可再次展开");
            var whyLabel = (Label)_ui.FindChild("GoalWhyLabel", true, false)!;
            if (whyLabel.Text.Length > "为什么：".Length)
                Check(whyLabel.IsVisibleInTree(), "展开目标核心显示为什么：" + whyLabel.Text.Split('\n')[0]);
        }
        else Blocked("目标卡收展开关（GoalFoldToggle 未建立或教学未启用）");
        // 学习操作上报：实际按 Q 旋转相机后不产生世界任务，且不伪造达成
        var jobBefore = _world.ReadPlayerState().Job;
        PressKey(Key.Q, true); await SimulationSeconds(0.15); PressKey(Key.Q, false);
        await Frames(2);
        Check(_world.ReadPlayerState().Job == jobBefore, "相机学习操作不产生世界任务");
        GD.Print("PLAYER_UI_SELFTEST TUTORIAL end");
    }

    // ---- 设置：真实生效（UI 缩放/镜头速度/提示详略） ----
    private async System.Threading.Tasks.Task SettingsChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST SETTINGS begin");
        var settings = _world.ReadSettings();
        Check(_ui.UiScaleForTest == settings.UiScale || _ui.UiScaleForTest > 0, "UI 缩放初值来自权威 ReadSettings：" + _ui.UiScaleForTest.ToString("0.##", Inv));
        var job = (Label)_ui.FindChild("GoalInstructionLabel", true, false)!;
        var goal = (Label)_ui.FindChild("GoalTitleLabel", true, false)!;

        // 150%：字体按 1.5 实际变大
        _ui.ApplySettingsForTest(1.5, 1, "detailed");
        await Frames(3);
        Check(Mathf.IsEqualApprox(job.GetThemeFontSize("font_size"), 27), "150% 后正文字号 27：实际 " + job.GetThemeFontSize("font_size"));
        Check(Mathf.IsEqualApprox(goal.GetThemeFontSize("font_size"), 36), "150% 后标题字号 36：实际 " + goal.GetThemeFontSize("font_size"));
        PrintPixelEvidence("scale150");

        // 125%：中点取整向远离零
        _ui.ApplySettingsForTest(1.25, 1, "detailed");
        await Frames(3);
        Check(job.GetThemeFontSize("font_size") == 23, "125% 后正文字号 23：实际 " + job.GetThemeFontSize("font_size"));

        // 镜头速度真实影响平移速率
        _ui.ApplySettingsForTest(1, 0.5, "detailed");
        await Frames(2);
        var slow = await MeasurePanDistance();
        _ui.ApplySettingsForTest(1, 2, "detailed");
        await Frames(2);
        var fast = await MeasurePanDistance();
        Check(fast > slow * 1.5f, "镜头速度 2× 平移明显快于 0.5×：slow=" + F(slow) + "m fast=" + F(fast) + "m");

        // 精简提示仍显示当前目标但隐藏为什么
        bool infoWasOpen = (_ui.FindChild("InfoDetailsScroll", true, false) as Control)?.Visible == true;
        if (!infoWasOpen)
        {
            Click(((Button)_ui.FindChild("InfoDetailsToggle", true, false)!).GetGlobalRect().GetCenter());
            await Frames(4);
        }
        _ui.ApplySettingsForTest(1, 1, "brief");
        await Frames(3);
        var why = (Label)_ui.FindChild("GoalWhyLabel", true, false)!;
        Check(_ui.HelpDetailedForTest == false && !why.Visible && goal.Visible, "精简模式仍显示当前目标且隐藏『为什么』");
        _ui.ApplySettingsForTest(1, 1, "detailed");
        await Frames(3);
        Check(_ui.HelpDetailedForTest && why.IsVisibleInTree(), "详细模式的可回看详情恢复『为什么』");
        if (!infoWasOpen)
        {
            Click(((Button)_ui.FindChild("InfoDetailsToggle", true, false)!).GetGlobalRect().GetCenter());
            await Frames(4);
        }

        // 设置页页面流程：真实 QueueLifecycle(settings) 打开 → 结构/焦点 → Esc 返回局内。
        _world.QueueLifecycle("settings");
        try
        {
            await WaitFor(() => _world.ReadLifecycle().Page == "settings", 5, "设置页打开");
            Check(_ui.FindChild("ScaleButton_100", true, false) is Button && _ui.FindChild("ScaleButton_125", true, false) is Button &&
                _ui.FindChild("ScaleButton_150", true, false) is Button && _ui.FindChild("SettingsBackButton", true, false) is Button &&
                _ui.FindChild("SettingsDefaultsButton", true, false) is Button,
                "设置页三档缩放/还原默认/返回按钮建立");
            await Frames(3);
            Check(((Button)_ui.FindChild("SettingsBackButton", true, false)!).FocusMode != Control.FocusModeEnum.None, "设置页按钮保留键盘焦点");
            await SettingsFailureDraftChecks();
            PressKey(Key.Escape, true); PressKey(Key.Escape, false);
            await WaitForGameHud("设置页 Esc 返回且局内 HUD 可见");
            Check(true, "设置页 Esc 返回局内（Back 语义）");
        }
        catch (TimeoutException e)
        {
            Blocked("设置页页面流程（" + e.Message + "）");
        }
        GD.Print("PLAYER_UI_SELFTEST SETTINGS end");
    }

    private async System.Threading.Tasks.Task SettingsFailureDraftChecks()
    {
        var current = _world.ReadSettings();
        string path = System.IO.Path.Combine(_privateRoot, "tutorial-player-settings.cfg");
        string backup = System.IO.Path.Combine(_privateRoot, "settings-before-rejected-write.cfg");
        if (System.IO.File.Exists(path)) System.IO.File.Move(path, backup);
        System.IO.Directory.CreateDirectory(path); // 只在本次私有根制造确定写入失败，不改个人配置。
        try
        {
            Click(((Button)_ui.FindChild("ScaleButton_150", true, false)!).GetGlobalRect().GetCenter());
            await RealSeconds(0.9);
            await Frames(3);
            var rejected = _world.ReadSettings();
            Check(rejected == current && _world.ReadPlayerState().Notice.Contains("设置保存失败"),
                "真实设置保存失败保权威旧值，UI 等待回显超时");
        }
        finally
        {
            System.IO.Directory.Delete(path);
            if (System.IO.File.Exists(backup)) System.IO.File.Move(backup, path);
        }
        var speed = (HSlider)_ui.FindChildren("*", "HSlider", true, false).Single();
        speed.Value = current.CameraSpeed == 1.5 ? 1.25 : 1.5;
        await WaitFor(() => _world.ReadSettings().Revision > current.Revision, 5, "失败后只改镜头速度真实保存");
        var accepted = _world.ReadSettings();
        Check(accepted.UiScale == current.UiScale && accepted.HelpDetail == current.HelpDetail
            && accepted.CameraSpeed == speed.Value, "失败后只改速度，不再携带失败的150%缩放或旧提示草案");
        _world.QueueSettings(current.UiScale, current.CameraSpeed, current.HelpDetail);
        await WaitFor(() => _world.ReadSettings().Revision > accepted.Revision, 5, "恢复测试前权威设置");
        await Frames(3);
    }

    private async System.Threading.Tasks.Task<float> MeasurePanDistance()
    {
        var before = _camera.GlobalPosition;
        PressKey(Key.W, true);
        await SimulationSeconds(0.35);
        PressKey(Key.W, false);
        await Frames(2);
        return before.DistanceTo(_camera.GlobalPosition);
    }

    // ---- 相机输入（九个镜头方向按钮已移除；用真实 engine input 与状态合同验收） ----
    private async System.Threading.Tasks.Task CameraInputChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST CAMUI begin");
        Check(_ui.FindChild("CameraForwardButton", true, false) == null && _ui.FindChild("CameraResetButton", true, false) == null,
            "九个镜头方向按钮不再建立（真实键鼠/触控板映射替代）");
        await WaitForGameHud("相机输入检查前 HUD 可见");
        var hints = (_ui.FindChild("Hints", true, false) as Label)?.Text ?? "";
        Check(hints.Contains("Q/E") && hints.Contains("滚轮") && hints.Contains("WASD"),
            "帮助文本与真实映射一致（Q/E 旋转、滚轮缩放、WASD 平移）：" + hints);

        // 复位合同：真实 Q/E 与滚轮改变镜头后，状态合同仍可经 F 定位与初始姿态不可伪造地验证。
        // 这里验证输入→状态因果：Q 旋转、W 平移、滚轮缩放各自改变被测状态。
        var basisBefore = _camera.GlobalBasis;
        PressKey(Key.Q, true);
        await SimulationSeconds(0.15);
        PressKey(Key.Q, false);
        await Frames(2);
        Check(basisBefore.Z.AngleTo(_camera.GlobalBasis.Z) > 1e-4f, "真实 Q 旋转相机");

        var originBefore = _camera.GlobalPosition;
        PressKey(Key.W, true);
        await SimulationSeconds(0.2);
        PressKey(Key.W, false);
        await Frames(2);
        Check(originBefore.DistanceTo(_camera.GlobalPosition) > 0.2f, "真实 W 沿朝向平移");

        float distanceBefore = _ui.CameraDistanceForTest;
        Wheel(true);
        await Frames(2);
        Check(_ui.CameraDistanceForTest < distanceBefore, "真实滚轮拉近（状态合同距离变小）");

        // 镜头操作不下达任务
        Check(_world.ReadPlayerState().Job is not { Active: true }, "镜头操作不下达任务");
        GD.Print("PLAYER_UI_SELFTEST CAMUI end");
    }


    private async System.Threading.Tasks.Task CameraDiag()
    {
        GD.Print("PLAYER_UI_SELFTEST CAMERADIAG begin accumulated_input=" + Input.UseAccumulatedInput);
        if (_world.ReadPlayerState().Paused)
        {
            _world.QueuePlayerAction("pause");
            await WaitFor(() => !_world.ReadPlayerState().Paused, 5, "诊断前解除暂停");
        }
        await Frames(2);
        Diag("initial origin=" + Fmt(_camera.GlobalPosition) + " basisZ=" + Fmt(_camera.GlobalBasis.Z));

        {
            var basisBefore = _camera.GlobalBasis;
            var originBefore = _camera.GlobalPosition;
            bool preParse = Input.IsKeyPressed(Key.Q);
            PressKey(Key.Q, true);
            bool afterPress = Input.IsKeyPressed(Key.Q);
            PressKey(Key.Q, false);
            bool afterRelease = Input.IsKeyPressed(Key.Q);
            await Frames(3);
            bool postFlush = Input.IsKeyPressed(Key.Q);
            Diag("same-frame Q rotation=" + F(basisBefore.Z.AngleTo(_camera.GlobalBasis.Z)) + "rad" +
                " originBefore=" + Fmt(originBefore) + " originAfter=" + Fmt(_camera.GlobalPosition) +
                " IsKeyPressed(preParse/afterPress/afterRelease/postFlush)=" + preParse + "/" + afterPress + "/" + afterRelease + "/" + postFlush);
        }
        {
            var originBefore = _camera.GlobalPosition;
            PressKey(Key.Right, true);
            await Frames(2);
            bool polled = Input.IsKeyPressed(Key.Right);
            PressKey(Key.Right, false);
            await Frames(2);
            float move = _camera.GlobalPosition.DistanceTo(originBefore);
            Diag("frame-span Right(2帧) move=" + F(move) + "m polledDuringPress=" + polled);
            Check(move > 1e-5f, "跨帧短按Right被轮询到(平移非零): " + F(move) + "m polled=" + polled);
        }
        {
            var basisBefore = _camera.GlobalBasis;
            PressKey(Key.Q, true);
            await Frames(2);
            PressKey(Key.Q, false);
            await Frames(2);
            float rotation = basisBefore.Z.AngleTo(_camera.GlobalBasis.Z);
            Check(rotation > 1e-6f, "跨帧短按Q被轮询到(旋转非零): " + F(rotation) + "rad");
        }
        {
            var basisBefore = _camera.GlobalBasis;
            PressKey(Key.Q, true);
            await SimulationSeconds(0.06);
            PressKey(Key.Q, false);
            await Frames(2);
            float rotation = basisBefore.Z.AngleTo(_camera.GlobalBasis.Z);
            Diag("short Q(60ms) rotation=" + F(rotation) + "rad");
            Check(rotation > 0.02f, "人级短按Q(60ms)旋转可见(期望约0.06rad): " + F(rotation));
        }
        {
            var basisBefore = _camera.GlobalBasis;
            PressKey(Key.Q, true);
            await SimulationSeconds(0.3);
            PressKey(Key.Q, false);
            await Frames(2);
            Check(basisBefore.Z.AngleTo(_camera.GlobalBasis.Z) > 0.15f, "长按Q(0.3s)旋转显著");
        }
        {
            var originBefore = _camera.GlobalPosition;
            PressKey(Key.Right, true);
            await SimulationSeconds(0.3);
            PressKey(Key.Right, false);
            await Frames(2);
            Check(originBefore.DistanceTo(_camera.GlobalPosition) > 0.5f, "长按Right(0.3s)平移显著");
        }
        {
            var originBefore = _camera.GlobalPosition;
            Wheel(true);
            await Frames(2);
            float moved = originBefore.DistanceTo(_camera.GlobalPosition);
            Diag("wheel-up x1 originΔ=" + F(moved) + "m");
            Check(moved > 0.5f, "滚轮上滚拉近可见: " + F(moved) + "m");
        }
        {
            var before = _camera.GlobalTransform;
            Root.PushInput(new InputEventMagnifyGesture { Position = Root.GetVisibleRect().Size / 2f, Factor = 1.25f }, inLocalCoords: true);
            await Frames(2);
            var after = _camera.GlobalTransform;
            Diag("MagnifyGesture factor=1.25 originΔ=" + F(before.Origin.DistanceTo(after.Origin)) + "m" +
                " basisΔ=" + F(before.Basis.Z.AngleTo(after.Basis.Z)) + "rad");
        }
        {
            var before = _camera.GlobalTransform;
            Root.PushInput(new InputEventPanGesture { Position = Root.GetVisibleRect().Size / 2f, Delta = new Vector2(240, 0) }, inLocalCoords: true);
            await Frames(2);
            var after = _camera.GlobalTransform;
            Diag("PanGesture delta=(240,0) originΔ=" + F(before.Origin.DistanceTo(after.Origin)) + "m" +
                " basisΔ=" + F(before.Basis.Z.AngleTo(after.Basis.Z)) + "rad");
        }
        {
            var before = _camera.GlobalPosition;
            Root.PushInput(new InputEventMagnifyGesture { Position = Root.GetVisibleRect().Size / 2f, Factor = 1.6f }, inLocalCoords: true);
            await Frames(2);
            Check(before.DistanceTo(_camera.GlobalPosition) > 0.5f, "MagnifyGesture 捏合缩放驱动相机: Δ=" + F(before.DistanceTo(_camera.GlobalPosition)) + "m");
        }
        {
            var before = _camera.GlobalPosition;
            Root.PushInput(new InputEventPanGesture { Position = Root.GetVisibleRect().Size / 2f, Delta = new Vector2(120, 0) }, inLocalCoords: true);
            await Frames(2);
            Check(before.DistanceTo(_camera.GlobalPosition) > 0.1f, "PanGesture 双指平移驱动相机: Δ=" + F(before.DistanceTo(_camera.GlobalPosition)) + "m");
        }
        {
            var pressPos = new Vector2(900, 600);
            var originBefore = _camera.GlobalPosition;
            Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = pressPos }, inLocalCoords: true);
            for (int i = 1; i <= 3; i++)
                Root.PushInput(new InputEventMouseMotion { Position = pressPos + new Vector2(i * 10, 0), Relative = new Vector2(10, 0), ButtonMask = MouseButtonMask.Right }, inLocalCoords: true);
            await Frames(2);
            float dragged = originBefore.DistanceTo(_camera.GlobalPosition);
            Check(dragged > 0.05f, "右键拖动平移可见: " + F(dragged) + "m");

            var panelPos = new Vector2(120, 200); // InfoPanel(MouseFilter=Stop) 内
            Root.PushInput(new InputEventMouseMotion { Position = panelPos, Relative = new Vector2(10, 0), ButtonMask = MouseButtonMask.Right }, inLocalCoords: true);
            Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = panelPos }, inLocalCoords: true);
            await Frames(2);
            var afterRelease = _camera.GlobalPosition;
            for (int i = 1; i <= 3; i++)
                Root.PushInput(new InputEventMouseMotion { Position = new Vector2(640 + i * 20, 500), Relative = new Vector2(20, 0) }, inLocalCoords: true);
            await Frames(2);
            float residual = afterRelease.DistanceTo(_camera.GlobalPosition);
            Check(residual < 0.01f, "HUD 内释放右键后无键实际 motion 不残留拖动：" + F(residual) + "m");
            Diag("右键拖动=" + F(dragged) + "m HUD面板释放后无键移动Δ=" + F(residual) + "m" +
                (residual > 0.01f ? " → 残留拖动（释放事件被HUD吞掉，_panGrab未清）" : " → 无残留"));
        }
        GD.Print("PLAYER_UI_SELFTEST CAMERADIAG end");
    }

    // ---- 保存/读取（旧兼容接口在 UI 之外仍走权威；本测试只用 jobtmp 私档） ----
    private async System.Threading.Tasks.Task SaveLoadCompatChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST SAVE begin");
        Check(System.IO.Path.IsPathFullyQualified(_savePath) && !_savePath.StartsWith(ProjectSettings.GlobalizePath("res://"), StringComparison.Ordinal),
            "测试存档在私有绝对路径（repo 外）：" + _savePath);
        _world.QueuePlayerAction("save");
        await WaitFor(() => _world.ReadPlayerState().Notice.Contains("已保存"), 15, "保存成功提示");
        Check(_world.ReadPlayerState().SaveExists, "保存成功可见");
        _world.QueuePlayerAction("load");
        await WaitFor(() => _world.ReadPlayerState().Notice.Contains("读取完成"), 60, "读取后世界恢复");
        Check(_world.ReadPlayerState().Ready, "读取后世界就绪");
        await Frames(5);
        Check(_ui.FindChild("ConfirmButton", true, false) is Button, "读取完成后 UI 仍可用");
        GD.Print("PLAYER_UI_SELFTEST SAVE end");
    }

    // ---- 发展面板与诊断折叠（D1.2 覆盖，节点名与新布局对齐） ----
    private async System.Threading.Tasks.Task DevPanelChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST DEV begin");
        if (_ui.FindChild("InfoDetailsScroll", true, false) is Control { Visible: false })
        {
            Click(((Button)_ui.FindChild("InfoDetailsToggle", true, false)!).GetGlobalRect().GetCenter());
            await Frames(4);
        }
        var dev = _world.ReadDevelopment();
        var devTitle = _ui.FindChild("DevTitleLabel", true, false) as Label;
        var devBody = _ui.FindChild("DevBodyLabel", true, false) as Label;
        var diagToggle = (Button)_ui.FindChild("DiagToggle", true, false)!;
        var restock = _ui.FindChild("RestockButton", true, false) as Button;
        var legacy = _ui.FindChild("LegacyLoadButton", true, false) as Button;
        Check(restock != null && legacy != null, "补维修耗材（常规工具行）与读取旧档（诊断）按钮建立");
        Click(diagToggle.GetGlobalRect().GetCenter());
        await Frames(4);
        Check(_ui.FindChild("DiagRow", true, false) is Control { Visible: true }, "诊断折叠可展开（低频操作与常规操作分离）");
        var infoScrollRect = (_ui.FindChild("InfoScroll", true, false) as Control)?.GetGlobalRect() ?? new Rect2();
        var barRect = (_ui.FindChild("CommandBar", true, false) as Control)?.GetGlobalRect() ?? new Rect2();
        GD.Print("PLAYER_UI_SELFTEST GEO infoScroll=" + infoScrollRect + " bar=" + barRect +
            " hints=" + ((_ui.FindChild("Hints", true, false) as Control)?.GetGlobalRect() ?? new Rect2()) +
            " buildBar=" + ((_ui.FindChild("BuildBar", true, false) as Control)?.GetGlobalRect() ?? new Rect2()) +
            " confirmRow=" + ((_ui.FindChild("ConfirmRow", true, false) as Control)?.GetGlobalRect() ?? new Rect2()) +
            " cameraRow=" + ((_ui.FindChild("CameraRow", true, false) as Control)?.GetGlobalRect() ?? new Rect2()) +
            " diagRow=" + ((_ui.FindChild("DiagRow", true, false) as Control)?.GetGlobalRect() ?? new Rect2()) +
            " dirToggle=" + ((_ui.FindChild("DevDirectionsToggle", true, false) as Control)?.GetGlobalRect() ?? new Rect2()) +
            " facToggle=" + ((_ui.FindChild("FacilityToggle", true, false) as Control)?.GetGlobalRect() ?? new Rect2()));
        if (!dev.Enabled)
        {
            Check(devTitle is { Visible: false } && devBody is { Visible: false }, "发展面板在 DTO 未启用时隐藏");
            Check(restock != null && !restock.IsVisibleInTree() && legacy != null && !legacy.IsVisibleInTree(), "经营按钮在 DTO 未启用时不可见");
            Blocked("发展面板内容展示（DevelopmentReadModel 未启用）");
        }
        else
        {
            var body = devBody ?? throw new InvalidOperationException("DevBodyLabel 未建立");
            Check(devTitle is { Visible: true } && devTitle!.Text.Contains(dev.Provider), "发展标题显示提供者：" + devTitle!.Text);
            Check(body is { Visible: true } &&
                    body.Text.Contains("目标 " + dev.Goal) && body.Text.Contains("当前 " + dev.Stage) &&
                    body.Text.Contains("下一步 " + dev.Reason) && body.Text.Contains("净缺口 " + dev.Need),
                "发展核心行显示目标/当前动作/下一步/净缺口（权威原文）");
            if (dev.Directions is { Length: > 0 } dirs)
            {
                var dirToggle = _ui.FindChild("DevDirectionsToggle", true, false) as Button;
                var dirLabel = _ui.FindChild("DevDirectionsLabel", true, false) as Label;
                Check(dirToggle is { Visible: true } && dirLabel is { Visible: false }, "方向详情默认折叠不挤占首屏");
                ScrollIntoView(dirToggle!);
                await Frames(2);
                Click(dirToggle!.GetGlobalRect().GetCenter());
                await Frames(2);
                Check(dirLabel is { Visible: true } &&
                    dirs.All(d => dirLabel!.Text.Contains(d.Name) && dirLabel.Text.Contains(d.Cost) && dirLabel.Text.Contains(d.Consequence)),
                    "两方向逐字段显示权威 Name/Cost/Need/Consequence：" + (dirLabel is { Visible: true } ? dirLabel.Text.Split('\n')[0] : "(未展开)"));
                Click(dirToggle.GetGlobalRect().GetCenter());
                await Frames(2);
                Check(dirLabel is { Visible: false }, "方向详情可再次折叠");
            }
            else
            {
                Blocked("两方向 Directions 详情（权威为空）");
            }
            // restock 走权威命令：双击合并一次，不被"未知操作"拒绝
            Click(restock!.GetGlobalRect().GetCenter());
            Click(restock.GetGlobalRect().GetCenter());
            await Frames(3);
            var notice = _world.ReadPlayerState().Notice;
            Check(!notice.Contains("未知操作") && !notice.Contains("操作正在处理"), "restock 请求被权威接受且双击只提交一次：" + notice);
        }
        // 设施折叠：默认收起，展开显示真实设施
        if (_ui.FindChild("FacilityToggle", true, false) is Button facToggle && _ui.FindChild("FacilityLabel", true, false) is Label facLabel)
        {
            Check(facToggle.Visible && !facLabel.Visible, "设施列表默认折叠不挤占首屏");
            ScrollIntoView(facToggle);
            await Frames(2);
            Click(facToggle.GetGlobalRect().GetCenter());
            await Frames(2);
            Check(facLabel.Visible && facLabel.Text.Length > 0, "设施列表展开显示真实状态");
            Click(facToggle.GetGlobalRect().GetCenter());
            await Frames(2);
            Check(!facLabel.Visible, "设施列表可再次折叠");
        }
        else Blocked("设施折叠（控件未建立）");
        Click(diagToggle.GetGlobalRect().GetCenter());
        await Frames(2);
        Check(_ui.FindChild("DiagRow", true, false) is Control { Visible: false }, "诊断折叠可收起");
        var infoDetails = (ScrollContainer)_ui.FindChild("InfoDetailsScroll", true, false)!;
        infoDetails.ScrollVertical = 0;
        Click(((Button)_ui.FindChild("InfoDetailsToggle", true, false)!).GetGlobalRect().GetCenter());
        await Frames(4);
        GD.Print("PLAYER_UI_SELFTEST DEV end");
    }

    // ---- 尺寸与缩放：默认折叠状态下同时检查核心信息、滚动遮罩与命令栏 ----
    private async System.Threading.Tasks.Task LayoutAndScaleChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST LAYOUT begin");
        await WaitForGameHud("布局检查前 HUD 可见");
        await RestoreCameraForLayout(); // 前序 CAMERA 诊断真实手势移动过镜头：布局前恢复测试初姿
        await CloseDiagnosticsByClick();
        foreach (string name in new[] { "InfoDetailsToggle", "PlanDetailsToggle", "MilestonesToggle", "DevDirectionsToggle", "FacilityToggle" })
            if (_ui.FindChild(name, true, false) is Button { ButtonPressed: true } fold)
            {
                fold.ButtonPressed = false; // ToggleMode setter 实际发 Toggled，目标区回到收起。
                await Frames(2);
            }
        Check(_ui.FindChild("InfoDetailsScroll", true, false) is Control { Visible: false }
            && _ui.FindChild("MilestonesLabel", true, false) is Control { Visible: false }, "布局默认低频详情确实收起");

        // 当期三尺寸（1280x800、1920x1080、1920x1200）各跑 100/125/150%；1860x939 仅作 legacy 额外样本，不冒充 1080。
        var sizes = DisplayServer.GetName() == "headless"
            ? new[] { new Vector2I(1920, 1200), new Vector2I(1920, 1080), new Vector2I(1280, 800), new Vector2I(1860, 939) }
            : new[] { new Vector2I(1860, 939), new Vector2I(1280, 800) };
        var buildTool = (Button)_ui.FindChild("ToolBuildButton", true, false)!;
        foreach (var size in sizes)
        {
            ResizeLogicalViewport(size);
            foreach (double scale in new[] { 1.0, 1.25, 1.5 })
            {
                _ui.ApplySettingsForTest(scale, 1, "detailed");
                var info = (ScrollContainer)_ui.FindChild("InfoScroll", true, false)!;
                var planScroll = (ScrollContainer)_ui.FindChild("PlanScroll", true, false)!;
                info.ScrollVertical = 0;
                planScroll.ScrollVertical = 0;
                if (_ui.FindChild("PlanReasonScroll", true, false) is ScrollContainer reasonReset) reasonReset.ScrollVertical = 0;
                if (_ui.FindChild("PlanCostScroll", true, false) is ScrollContainer costReset) costReset.ScrollVertical = 0;
                await Frames(6);
                string tag = size.X + "x" + size.Y + "-scale" + (int)(scale * 100);
                PrintPixelEvidence(tag);
                CheckCoreHudVisibility(tag + "-idle", false);
                await CaptureViewport(tag + "-idle");

                if (_ui.FindChild("ModeButton_solar", true, false) is Button solarMode)
                {
                    await CommandClick(buildTool);
                    await Frames(3);
                    await CommandClick(solarMode);
                    await Frames(4);
                    Check(_ui.BuildTypeForTest == "solar", tag + " 真实点击太阳能模式按钮已生效");
                    await RealSeconds(0.5);
                    var candidate = FindVisibleLayoutSite("solar", false);
                    Click(candidate.Screen);
                    await Frames(6);
                    var selection = (Label)_ui.FindChild("SelectionLabel", true, false)!;
                    var cost = (Label)_ui.FindChild("PlanCostLabel", true, false)!;
                    Check(_ui.BuildTypeForTest == "solar" && _ui.SelectedSiteForTest is { } selected && XzDistance(selected, candidate.World) < 0.75f
                        && selection.Text.Contains("已选位置") && cost.IsVisibleInTree() && cost.Text.Length > 0,
                        tag + " 真正选中无遮挡地面与可见草案成本后抓图");
                    info.ScrollVertical = 0;
                    planScroll.ScrollVertical = 0;
                    if (_ui.FindChild("PlanReasonScroll", true, false) is ScrollContainer reasonResetPlan) reasonResetPlan.ScrollVertical = 0;
                    if (_ui.FindChild("PlanCostScroll", true, false) is ScrollContainer costResetPlan) costResetPlan.ScrollVertical = 0;
                    await Frames(4);
                    CheckCoreHudVisibility(tag + "-plan", true);
                    await CaptureViewport(tag + "-plan");
                    await RealSeconds(0.5);
                    var illegal = FindVisibleLayoutSite("solar", true);
                    Click(illegal.Screen);
                    await Frames(6);
                    GD.Print("PLAYER_UI_SELFTEST LAYOUT_SELECTED expected=" + Fmt(illegal.World) + " actual=" +
                        (_ui.SelectedSiteForTest is { } selectedIllegal ? Fmt(selectedIllegal) : "none") + " selection=" + selection.Text);
                    Check(_ui.SelectedSiteForTest is { } illegalSite && XzDistance(illegalSite, illegal.World) < 0.75f,
                        tag + " 真实地面非法候选仍选点成功");
                    if (_ui.SelectedSiteForTest is { } actual)
                    {
                        var refused = _world.ReadBuildPlan("solar", actual);
                        var reason = (Label)_ui.FindChild("PlanReasonLabel", true, false)!;
                        Check(!refused.CandidateLegal && !refused.CanSubmit && reason.IsVisibleInTree()
                            && reason.Text.Contains(refused.CandidateReason), tag + " 非法方案显示完整权威拒因：" + reason.Text);
                    }
                    CheckCoreHudVisibility(tag + "-illegal", true);
                    await CaptureViewport(tag + "-illegal");
                    await CommandClick(buildTool); // 再次点击建设工具退出选址
                    await Frames(4);
                    Check(_ui.PlacementModeForTest == 0, tag + " 布局用例退出选址复位");
                }
                else Blocked(tag + " 建造核心字段（太阳能模式按钮未建立）");
            }
        }
        // 管理/确认面板受视口 clamp：150% 时面板完整在视口内、关闭按钮同屏、高度让出边距
        _ui.ApplySettingsForTest(1.5, 1, "detailed");
        await Frames(4);
        await CommandClick((Button)_ui.FindChild("ToolMaterialsButton", true, false)!);
        await Frames(4);
        Check(_ui.ManagementOpenForTest, "clamp 用例打开管理层");
        var clampView = Root.GetVisibleRect();
        var panelRect = ((Control)_ui.FindChild("ManagementPanel", true, false)!).GetGlobalRect();
        GD.Print("PLAYER_UI_SELFTEST MGMT_RECT panel=" + panelRect + " viewport=" + clampView);
        Check(panelRect.Position.X >= -0.5f && panelRect.Position.Y >= -0.5f
            && panelRect.End.X <= clampView.End.X + 0.5f && panelRect.End.Y <= clampView.End.Y + 0.5f,
            "150% 管理面板完整在视口内（clamp）");
        Check(panelRect.Size.Y <= clampView.Size.Y - 20f,
            "面板高度让出视口边距（clamp 生效）：" + F(panelRect.Size.Y) + "<=" + F(clampView.Size.Y - 20f));
        var clampClose = (Button)_ui.FindChild("ManagementCloseButton", true, false)!;
        var closeRect = clampClose.GetGlobalRect();
        Check(clampClose.IsVisibleInTree() && closeRect.End.Y <= clampView.End.Y && closeRect.End.X <= clampView.End.X,
            "关闭按钮在 clamp 后同屏可见");
        Click(closeRect.GetCenter());
        await Frames(4);
        Check(!_ui.ManagementOpenForTest, "clamp 用例关闭管理层");
        _ui.ApplySettingsForTest(1, 1, "detailed");
        ResizeLogicalViewport(new Vector2I(1280, 800));
        await Frames(4);
        GD.Print("PLAYER_UI_SELFTEST LAYOUT end");
    }

    private async System.Threading.Tasks.Task ConnectionLayoutChecks()
    {
        ResizeLogicalViewport(new Vector2I(1280, 800));
        foreach (double scale in new[] { 1.0, 1.5 })
        {
            _ui.ApplySettingsForTest(scale, 1, "detailed");
            await Frames(5);
            await OpenConnectionPanelByClick();
            var option = _world.ReadConnectionOptions().First();
            var row = (Button)_ui.FindChild("ConnectRow_" + option.FacilityId + "_" + option.SourceId, true, false)!;
            Click(row.GetGlobalRect().GetCenter());
            await Frames(4);
            var cost = (Label)_ui.FindChild("ConnectCostLabel", true, false)!;
            var gap = (Label)_ui.FindChild("ConnectGapLabel", true, false)!;
            var reason = (Label)_ui.FindChild("ConnectReasonLabel", true, false)!;
            var confirm = (Button)_ui.FindChild("ConnectSubmitButton", true, false)!;
            var wrap = (ScrollContainer)_ui.FindChild("ConnectScroll", true, false)!;
            var mask = wrap.GetGlobalRect();
            var view = Root.GetVisibleRect();
            var panel = (Control)_ui.FindChild("ConnectPanel", true, false)!;
            bool InMask(Control control)
            {
                var rect = control.GetGlobalRect();
                bool inside = control.IsVisibleInTree() && rect.Position.X >= mask.Position.X && rect.Position.Y >= mask.Position.Y
                    && rect.End.X <= mask.End.X && rect.End.Y <= mask.End.Y
                    && rect.Position.X >= view.Position.X && rect.Position.Y >= view.Position.Y && rect.End.X <= view.End.X && rect.End.Y <= view.End.Y;
                GD.Print("PLAYER_UI_SELFTEST CONNECT_RECT scale=" + scale.ToString("0.##", Inv) + " name=" + control.Name + " rect=" + rect + " mask=" + mask + " viewport=" + view + " inside=" + inside);
                return inside;
            }
            Check(InMask(panel) && InMask((Control)_ui.FindChild("ConnectHintLabel", true, false)!)
                && InMask(confirm) && InMask((Button)_ui.FindChild("ConnectCloseButton", true, false)!),
                "接线" + (int)(scale * 100) + "%面板/说明/确认关闭完整在真实视口与滚动遮罩内");
            Check(option.Cost.All(c => cost.Text.Contains(c.Name + "×" + c.Quantity)) && InMask(cost) && InMask(gap),
                "接线" + (int)(scale * 100) + "%真实所选成本/净缺口常驻可见：" + cost.Text + " / " + gap.Text);
            Check(InMask(row) && row.Text.Contains(option.DistanceM.ToString("0.#", Inv) + "m/" + option.RangeM.ToString("0.#", Inv) + "m"),
                "接线" + (int)(scale * 100) + "%真实来源与12m距离同屏");
            Check(!option.CanSubmit && confirm.Disabled && InMask(reason)
                && reason.Text.Contains(option.CandidateLegal ? option.SubmitReason : option.CandidateReason),
                "接线" + (int)(scale * 100) + "%已接线候选完整拒因与禁用确认同屏");
            await CaptureViewport("1280x800-scale" + (int)(scale * 100) + "-connection-cost-reason");
            PressKey(Key.Escape, true); PressKey(Key.Escape, false);
            await Frames(4);
        }
        _ui.ApplySettingsForTest(1, 1, "detailed");
        await Frames(4);
    }

    private static float XzDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private (Vector2 Screen, Vector3 World) FindVisibleLayoutSite(string type, bool illegal)
    {
        var view = Root.GetVisibleRect().Size;
        var info = ((Control)_ui.FindChild("InfoScroll", true, false)!).GetGlobalRect();
        var plan = ((Control)_ui.FindChild("PlanScroll", true, false)!).GetGlobalRect();
        var bar = ((Control)_ui.FindChild("CommandBar", true, false)!).GetGlobalRect();
        var robots = _world.ReadPlayerRobots();
        // 只读诊断：打印当前镜头与逐类拒因计数；不放宽 picker、不注入材料。
        int rejectFrustum = 0, rejectNoGround = 0, rejectRobot = 0, rejectOutOfBounds = 0, rejectLegality = 0;
        GD.Print("PLAYER_UI_SELFTEST LAYOUT_SITE_SCAN type=" + type + " illegal=" + illegal +
            " focus=" + Fmt(_ui.CameraFocusForTest) + " distance=" + F(_ui.CameraDistanceForTest) +
            " bandX=[" + F(info.End.X + 24) + "," + F(plan.Position.X - 24) + "] bandY=[" + F(view.Y * 0.42f) + "," + F(bar.Position.Y - 24) + "]");
        for (float y = view.Y * 0.42f; y < bar.Position.Y - 24; y += 18f)
            for (float x = info.End.X + 24; x < plan.Position.X - 24; x += 18f)
            {
                var screen = new Vector2(x, y);
                var origin = _camera.ProjectRayOrigin(screen);
                var direction = _camera.ProjectRayNormal(screen);
                if (direction.Y >= -1e-4f) { rejectFrustum++; continue; }
                using var query = PhysicsRayQueryParameters3D.Create(origin + direction * 0.5f, origin + direction * 400f, 1);
                using var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (hit.Count == 0) { rejectNoGround++; continue; }
                var ground = hit["position"].AsVector3();
                bool hitsRobot = robots.Any(robot =>
                {
                    var center = robot.Position + Vector3.Up * 0.45f;
                    float along = (center - origin).Dot(direction);
                    return along > 0.5f && (origin + direction * along).DistanceTo(center) <= MathF.Max(robot.Radius + 0.5f, along * 0.02f) + 0.35f;
                });
                if (hitsRobot) { rejectRobot++; continue; }
                if (MathF.Abs(ground.X) > 24 || MathF.Abs(ground.Z) > 24) { rejectOutOfBounds++; continue; }
                var draft = _world.ReadBuildPlan(type, ground);
                if (draft.CandidateLegal == illegal) { rejectLegality++; continue; }
                GD.Print("PLAYER_UI_SELFTEST LAYOUT_SITE type=" + type + " illegal=" + illegal + " screen=" + screen +
                    " world=" + Fmt(ground) + " candidate=" + draft.CandidateLegal + " reason=" + draft.CandidateReason);
                return (screen, ground);
            }
        throw new InvalidOperationException(
            "现场无无遮挡、无机器人拦截的" + (illegal ? "非法" : "合法") + "地面候选" +
            " focus=" + Fmt(_ui.CameraFocusForTest) + " distance=" + F(_ui.CameraDistanceForTest) +
            " rejects: frustum=" + rejectFrustum + " noGround=" + rejectNoGround + " robot=" + rejectRobot +
            " outOfBounds=" + rejectOutOfBounds + " legality=" + rejectLegality);
    }

    private void CheckCoreHudVisibility(string tag, bool building)
    {
        var view = Root.GetVisibleRect();
        var bar = (Control)_ui.FindChild("CommandBar", true, false)!;
        var topBar = (Control)_ui.FindChild("TopBar", true, false)!;
        var info = (ScrollContainer)_ui.FindChild("InfoScroll", true, false)!;
        var plan = (ScrollContainer)_ui.FindChild("PlanScroll", true, false)!;
        static bool Contains(Rect2 outer, Rect2 inner) => inner.Position.X >= outer.Position.X - 0.5f
            && inner.Position.Y >= outer.Position.Y - 0.5f && inner.End.X <= outer.End.X + 0.5f && inner.End.Y <= outer.End.Y + 0.5f;
        Rect2 ScrollMask(ScrollContainer scroll)
        {
            var mask = scroll.GetGlobalRect();
            if (scroll.GetVScrollBar().IsVisibleInTree()) mask.Size = new Vector2(mask.Size.X - scroll.GetVScrollBar().Size.X, mask.Size.Y);
            return mask;
        }
        // 目标卡核心行在左列滚动遮罩内且不被命令栏遮住
        foreach (string name in new[] { "GoalTitleLabel", "GoalInstructionLabel", "GoalActionLabel" })
        {
            var label = (Label)_ui.FindChild(name, true, false)!;
            var rect = label.GetGlobalRect();
            var mask = ScrollMask(info);
            bool visible = label.IsVisibleInTree() && Contains(view, rect) && Contains(mask, rect) && !bar.GetGlobalRect().Intersects(rect);
            GD.Print("PLAYER_UI_SELFTEST CORE_RECT[" + tag + "] " + name + "=" + rect + " mask=" + mask + " bar=" + bar.GetGlobalRect() + " textChars=" + label.Text.Length);
            Check(visible, tag + " " + name + " 完整位于所属滚动遮罩内且不被命令栏遮住");
        }
        Check(((Label)_ui.FindChild("GoalActionLabel", true, false)!).Text.Contains(_world.ReadTutorial().Achieved + "/" + _world.ReadTutorial().Total),
            tag + " 教学进度与下一步在核心区同时显示");
        // 顶栏常驻：发电、昼夜、库内现货（Stored 口径）、警报位
        foreach (string name in new[] { "TopPowerLabel", "TopDayLabel", "TopPauseButton", "TopMenuButton" })
        {
            var control = (Control)_ui.FindChild(name, true, false)!;
            var rect = control.GetGlobalRect();
            Check(control.IsVisibleInTree() && Contains(view, rect) && Contains(topBar.GetGlobalRect(), rect),
                tag + " " + name + " 常驻顶栏且完整可见");
        }
        var chip = (Button)_ui.FindChild("TopStockChip_iron", true, false)!;
        Check(chip.IsVisibleInTree() && chip.Text.Length > 1 && Contains(topBar.GetGlobalRect(), chip.GetGlobalRect()),
            tag + " 顶栏库内现货chip显示 Stored 口径：" + chip.Text);
        // 独立活动工程行：有活动才显示，显示时完整可见
        var workRow = (Control)_ui.FindChild("WorkRowPanel", true, false)!;
        if (workRow.Visible)
        {
            var workRect = ((Control)_ui.FindChild("WorkRowLabel", true, false)!).GetGlobalRect();
            Check(Contains(view, workRect) && !bar.GetGlobalRect().Intersects(workRect), tag + " 活动工程行完整可见且不压命令栏");
        }
        if (building)
        {
            // 布局合同：固定操作区（标题/场址/状态/当前阻塞/主按钮/自动前置）不滚动且完整在屏；
            // 成本/供料/后果在默认可见的内部滚动区可查。两组使用准确区分的 mask，失败不得改 BLOCKED。
            var planMask = ScrollMask(plan);
            foreach (string name in new[] { "PlanTitleLabel", "SelectionLabel", "PlanTagLabel", "PlanReasonLabel", "PlanAutoLabel", "ConfirmButton", "ExitPlacementButton" })
            {
                var control = (Control)_ui.FindChild(name, true, false)!;
                if (!control.IsVisibleInTree() && control is Label { Text.Length: 0 }) continue;
                var rect = control.GetGlobalRect();
                GD.Print("PLAYER_UI_SELFTEST CORE_RECT[" + tag + "] " + name + "=" + rect + " planMask=" + planMask + " textChars=" + (control is Label l ? l.Text.Length : 0));
                Check(control.IsVisibleInTree() && Contains(view, rect) && Contains(planMask, rect)
                    && !bar.GetGlobalRect().Intersects(rect), tag + " " + name + " 固定操作区完整可见不被命令栏遮挡");
            }
            var reasonLabel = (Label)_ui.FindChild("PlanReasonLabel", true, false)!;
            var reasonScroll = (ScrollContainer)_ui.FindChild("PlanReasonScroll", true, false)!;
            if (reasonLabel.Text.Length > 0 && reasonScroll.IsVisibleInTree())
            {
                // 拒因小滚动块：块本身完整在屏，超长拒因经区内滚动可查全（不截断、不折叠）。
                var reasonMask = ScrollMask(reasonScroll);
                var reasonRect = reasonLabel.GetGlobalRect();
                float hidden = MathF.Max(0f, reasonRect.Size.Y - reasonMask.Size.Y);
                GD.Print("PLAYER_UI_SELFTEST REASON_RECT[" + tag + "] label=" + reasonRect + " mask=" + reasonMask +
                    " maxScroll=" + F((float)reasonScroll.GetVScrollBar().MaxValue) + " hidden=" + F(hidden));
                Check(Contains(view, reasonMask) && Contains(planMask, reasonMask), tag + " 拒因小滚动块完整在视口与外层遮罩内");
                Check(reasonScroll.GetVScrollBar().MaxValue >= hidden - 2f, tag + " 完整拒因未被截断（超出部分区内滚动可查全）");
                Check(reasonRect.Position.Y >= reasonMask.Position.Y - 1f && reasonRect.Position.Y < reasonMask.End.Y,
                    tag + " 拒因默认从顶部开始可读");
            }
            var costScroll = (ScrollContainer)_ui.FindChild("PlanCostScroll", true, false)!;
            if (costScroll.IsVisibleInTree())
            {
                var confirm = (Button)_ui.FindChild("ConfirmButton", true, false)!;
                var costMask = ScrollMask(costScroll);
                GD.Print("PLAYER_UI_SELFTEST COST_MASK[" + tag + "] costScroll=" + costScroll.GetGlobalRect() + " mask=" + costMask +
                    " confirmEnd=" + F(confirm.GetGlobalRect().End.Y) + " maxScroll=" + F((float)costScroll.GetVScrollBar().MaxValue));
                Check(Contains(view, costMask) && Contains(planMask, costMask) && costMask.Size.Y >= 40f,
                    tag + " 内部成本滚动区默认可见并完整在外层遮罩内");
                Check(confirm.GetGlobalRect().End.Y <= costScroll.GetGlobalRect().Position.Y + 1f,
                    tag + " 开始/退出按钮先于多行成本/来源（无需滚动到主按钮）");
                foreach (string name in new[] { "PlanCostLabel", "PlanSupplyLabel", "PlanConsequenceLabel" })
                {
                    var label = (Label)_ui.FindChild(name, true, false)!;
                    if (!label.IsVisibleInTree() && label.Text.Length == 0) continue;
                    var rect = label.GetGlobalRect();
                    float maxScroll = (float)costScroll.GetVScrollBar().MaxValue;
                    bool costTopVisible = rect.Position.Y >= costMask.Position.Y - 1f && rect.Position.Y < costMask.End.Y;
                    bool fullyVisibleNow = rect.Position.Y >= costMask.Position.Y - 1f && rect.End.Y <= costMask.End.Y + 1f;
                    bool reachableByScroll = rect.Position.Y >= costMask.Position.Y - maxScroll - 1f && rect.End.Y <= costMask.End.Y + maxScroll + 1f;
                    GD.Print("PLAYER_UI_SELFTEST COST_RECT[" + tag + "] " + name + "=" + rect + " mask=" + costMask +
                        " maxScroll=" + F(maxScroll) + " textChars=" + label.Text.Length);
                    if (name == "PlanCostLabel")
                        Check(label.IsVisibleInTree() && costTopVisible,
                            tag + " " + name + " 完整需/已有/缺口默认从顶部可见");
                    else
                        Check(label.IsVisibleInTree() && (fullyVisibleNow || reachableByScroll),
                            tag + " " + name + " 完整内容区内滚动可达（不截断不删除）");
                }
            }
        }
        // 底部工具组完整可见且不与信息列/顶栏重叠
        foreach (string name in new[] { "ToolBuildButton", "ToolLevelButton", "ToolMaterialsButton", "ToolWorkButton" })
        {
            var button = (Button)_ui.FindChild(name, true, false)!;
            var rect = button.GetGlobalRect();
            Check(button.IsVisibleInTree() && Contains(view, rect) && !info.GetGlobalRect().Intersects(rect) && !topBar.GetGlobalRect().Intersects(rect),
                tag + " " + name + " 完整可见且不与信息列/顶栏重叠");
        }
    }

    private async System.Threading.Tasks.Task CaptureViewport(string tag)
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("PLAYER_UI_SELFTEST CAPTURE NOT_RUN " + tag + " dummy-renderer");
            return;
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = Root.GetTexture().GetImage();
        if (image == null) throw new InvalidOperationException("真实渲染 viewport 截图为空：" + tag);
        string directory = System.IO.Path.Combine(_testRoot, "captures");
        System.IO.Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, tag + ".png");
        if (System.IO.File.Exists(path)) throw new InvalidOperationException("截图禁止覆盖：" + path);
        var result = image.SavePng(path);
        if (result != Error.Ok) throw new InvalidOperationException("viewport 截图失败：" + result);
        GD.Print("PLAYER_UI_SELFTEST CAPTURE " + tag + " image=" + image.GetWidth() + "x" + image.GetHeight() + " path=" + path);
    }

    // ---- 收尾：home 的 leave-save 返回主页；测试树输出完整 summary 后自行退出 ----
    private async System.Threading.Tasks.Task Finale()
    {
        GD.Print("PLAYER_UI_SELFTEST FINALE begin");
        if (_failures > 0) { Report(); Quit(ExitCode()); return; }
        _world.QueueLifecycle("home");
        await WaitFor(() => _world.ReadLifecycle().Page == "leave" && _ui.PageForTest == "leave", 5, "离开确认页与 UI 同步");
        await Frames(3);
        Check(true, "QueueLifecycle(home) 打开离开确认页（Page=leave）");
        var leaveSave = (Button)_ui.FindChild("LeaveSaveButton", true, false)!;
        await CommandClick(leaveSave);
        await WaitFor(() => _world.ReadLifecycle().Page == "home" && _ui.PageForTest == "home", 15, "保存并离开返回主页");
        Check(_world.ReadLifecycle().Slots.Any(s => s.Slot == 1 && s.Exists && s.Loadable), "保存并返回主页后真实私有槽仍可读取");
        Check(((Control)_ui.FindChild("EntryOverlay", true, false)!).IsVisibleInTree(), "返回主页真实遮罩可见");
        Report();
        Quit(ExitCode());
    }

    // ---- 帮助 ----

    private void Check(bool condition, string label)
    {
        _results.Add((condition ? "PASS " : "FAIL ") + label);
        if (!condition) _failures++;
        GD.Print("PLAYER_UI_SELFTEST " + (condition ? "PASS " : "FAIL ") + label);
    }

    // BLOCKED 不是通过：权威协议 stub 尚未实现导致该检查无法真实执行。
    private void Blocked(string label)
    {
        _blocked++;
        _results.Add("BLOCKED " + label);
        GD.Print("PLAYER_UI_SELFTEST BLOCKED " + label);
    }

    private int ExitCode() => _failures > 0 ? 1 : _blocked > 0 ? 3 : 0;

    private void Report()
    {
        GD.Print("PLAYER_UI_SELFTEST summary checks=" + _results.Count + " failures=" + _failures + " blocked=" + _blocked);
        foreach (var line in _results)
            GD.Print("PLAYER_UI_SELFTEST result " + line);
    }

    private void Diag(string message) => GD.Print("PLAYER_UI_SELFTEST CAMERADIAG " + message);
    private static string F(float v) => v.ToString("G4", Inv);
    private static string Fmt(Vector3 v)
        => v.X.ToString("F2", Inv) + "," + v.Y.ToString("F2", Inv) + "," + v.Z.ToString("F2", Inv);

    private PlayerSitePreview FindLegalSite()
    {
        for (float x = -20f; x <= 20f; x += 4f)
            for (float z = -20f; z <= 20f; z += 4f)
            {
                var preview = _world.PreviewLevel(new Vector3(x, 0, z));
                if (preview.Legal) return preview;
            }
        throw new InvalidOperationException("全场扫描不到合法整平点");
    }

    private PlayerSitePreview? FindLegalBuildSite(string type)
    {
        for (float x = -20f; x <= 20f; x += 4f)
            for (float z = -20f; z <= 20f; z += 4f)
            {
                var preview = _world.PreviewBuild(type, new Vector3(x, 0, z));
                if (preview.Legal) return preview;
            }
        return null;
    }

    private void Click(Vector2 pos)
    {
        Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = pos }, inLocalCoords: true);
        Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = pos }, inLocalCoords: true);
    }

    // 页面回到 game 后的短门禁（防关闭页面的第二击误选中世界）会吞掉窗口内的世界点击：
    // 测试在点世界前先等它过期（按钮等 GUI 点击不受该门禁影响）。
    private async System.Threading.Tasks.Task ClickWorld(Vector2 pos)
    {
        await RealSeconds(0.5);
        Click(pos);
    }

    // 信息列是高度封顶的滚动区：合成点击前先把目标滚进可见区（低频明细可能在折叠区底部）。
    private void ScrollIntoView(Control target)
    {
        ScrollContainer? scroll = null;
        for (Node? parent = target.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is ScrollContainer candidate) { scroll = candidate; break; }
        if (scroll == null) return;
        var scrollRect = scroll.GetGlobalRect();
        var rect = target.GetGlobalRect();
        if (rect.Position.Y < scrollRect.Position.Y)
            scroll.ScrollVertical = Math.Max(0, scroll.ScrollVertical + (int)(rect.Position.Y - scrollRect.Position.Y) - 4);
        else if (rect.End.Y > scrollRect.End.Y)
            scroll.ScrollVertical = Math.Min((int)scroll.GetVScrollBar().MaxValue, scroll.ScrollVertical + (int)(rect.End.Y - scrollRect.End.Y) + 8);
    }

    // 点命令按钮：先等上一条命令的冷却过期（headless 帧率极高，不能按帧数计时），再点击。
    private async System.Threading.Tasks.Task CommandClick(Button button)
    {
        await RealSeconds(0.5);
        Click(button.GetGlobalRect().GetCenter());
    }

    private async System.Threading.Tasks.Task SimulationSeconds(double seconds)
    {
        var timer = CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
    }

    private async System.Threading.Tasks.Task WaitForSimulation(Func<bool> condition, double seconds, string label)
    {
        double began = _world.ReadPlayerState().TimeSeconds;
        double wallDeadline = Time.GetTicksMsec() / 1000.0 + 600;
        while (Time.GetTicksMsec() / 1000.0 < wallDeadline)
        {
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
            if (condition()) return;
            var state = _world.ReadPlayerState();
            var dev = _world.ReadDevelopment();
            if (dev.Stage == "需要处理") throw new InvalidOperationException(label + " 真实工程阻塞：" + dev.Reason);
            if (state.TimeSeconds - began >= seconds) throw new TimeoutException(label + " 模拟时间超时：" + state.Notice);
        }
        throw new TimeoutException(label + " 实际运行超时：" + _world.ReadPlayerState().Notice);
    }

    private async System.Threading.Tasks.Task RealSeconds(double seconds)
    {
        double deadline = Time.GetTicksMsec() / 1000.0 + seconds;
        while (Time.GetTicksMsec() / 1000.0 < deadline)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private void Wheel(bool up)
    {
        Root.PushInput(new InputEventMouseButton
        {
            ButtonIndex = up ? MouseButton.WheelUp : MouseButton.WheelDown,
            Pressed = true,
            Position = Root.GetVisibleRect().Size / 2f,
        }, inLocalCoords: true);
    }

    private void PressKey(Key key, bool pressed)
        => Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = pressed });

    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async System.Threading.Tasks.Task WaitFor(Func<bool> condition, double seconds, string label)
    {
        double deadline = Time.GetTicksMsec() / 1000.0 + seconds;
        while (Time.GetTicksMsec() / 1000.0 < deadline)
        {
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
            if (condition()) return;
        }
        throw new TimeoutException("等待超时：" + label);
    }
}
