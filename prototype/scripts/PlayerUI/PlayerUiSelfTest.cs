#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Yudian.PlayerUI;

/// <summary>
/// E01 自测入口（headless）：加载真实 Main 场景（玩家模式默认入口），挂 PlayerController，
/// 经公开合同与合成输入事件驱动并断言。不改共享场景、不白盒读 Main 私有状态。
/// 运行：
///   Godot --headless --path prototype --script res://scripts/PlayerUI/PlayerUiSelfTest.cs
/// 输出 PLAYER_UI_SELFTEST PASS/FAIL 行，退出码 0=全部通过，1=有失败或超时。
/// </summary>
public partial class PlayerUiSelfTest : SceneTree
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly List<string> _results = [];
    private int _failures;
    private Main _world = null!;
    private PlayerController _ui = null!;
    private Camera3D _camera = null!;

    public override void _Initialize()
    {
        System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST", "1");
        System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yudian-ui-test-" + Guid.NewGuid() + ".json"));
        GD.Print("PLAYER_UI_SELFTEST begin");
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")
            ?? throw new InvalidOperationException("无法加载 res://scenes/Main.tscn");
        _world = (Main)scene.Instantiate();
        Root.AddChild(_world);
        Run();
    }

    private async void Run()
    {
        try
        {
            await WaitFor(() => _world.ReadPlayerState().Ready, 60, "世界就绪");
            Root.Size = new Vector2I(1280, 800);
            GD.Print("PLAYER_UI_SELFTEST viewport=" + Root.Size);
            _camera = Root.GetCamera3D() ?? throw new InvalidOperationException("主场景没有当前相机");
            _ui = (PlayerController)_world.FindChild("PlayerController", true, false)!;
            await Frames(3);

            // CAMERA-01：设 YUDIAN_CAMERA_DIAG_ONLY=1 时跳过完整 UI 流程，只跑镜头验收与诊断（快速重跑入口）。
            if (System.Environment.GetEnvironmentVariable("YUDIAN_CAMERA_DIAG_ONLY") == "1")
            {
                GD.Print("PLAYER_UI_SELFTEST mode=camera-diag-only");
                await CameraUiChecks();
                await CameraDiag();
                Report();
                Quit(_failures == 0 ? 0 : 1);
                return;
            }

            Check(_ui.FindChild("ConfirmButton", true, false) is Button, "HUD 建立确认按钮");
            Check(_ui.FindChild("SelectionLabel", true, false) is Label, "HUD 建立选择标签");
            Check(_world.ReadPlayerRobots().Length == 12, "普通场景 12 台机器人");

            // 预览：合法点（动态寻找，机器人停驻位随种子变化）/ 设施重叠 / 场外 / 望山不能当筑垒
            var legal = FindLegalSite();
            Check(legal.Legal, "找到合法预览点：" + legal.Reason);
            Check(!_world.PreviewLevel(_world.ReadBootstrap().Facilities.FirstOrDefault()?.Position ?? new Vector3(14, 0, 0)).Legal, "设施重叠预览非法");
            Check(!_world.PreviewLevel(new Vector3(28, 0, 28)).Legal, "场外预览非法");
            Check(!_world.PreviewLevel(legal.Center, "Robot_Wangshan_1").Legal, "望山不能当筑垒执行");

            // UI 地面点选 → 已选位置 → 确认按钮启用
            Click(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
            await Frames(3);
            var selection = (Label)_ui.FindChild("SelectionLabel", true, false)!;
            Check(selection.Text.Contains("已选位置"), "地面点选后显示已选位置：" + selection.Text);
            var confirm = (Button)_ui.FindChild("ConfirmButton", true, false)!;
            await WaitFor(() => !confirm.Disabled, 5, "确认按钮启用");

            // 面板点击不穿透：点标题区域，选择保持
            Click(new Vector2(120, 60));
            await Frames(2);
            Check(selection.Text.Contains("已选位置"), "点击 HUD 面板不穿透（选择保持）");

            // 空点（天空）不下命令且清除选择
            Click(new Vector2(Root.GetVisibleRect().Size.X / 2f, 15));
            await Frames(2);
            Check(!selection.Text.Contains("已选位置"), "空点清除选择且不下命令");
            Check(_world.ReadPlayerState().Job == null, "空点后仍无任务");

            // 机器人点选（选一台投影在画面内的）
            string? clickedRobot = null;
            foreach (var robot in _world.ReadPlayerRobots())
            {
                var pos = _camera.UnprojectPosition(robot.Position + Vector3.Up * 0.45f);
                var rect = Root.GetVisibleRect();
                if (pos.X > 80 && pos.Y > 80 && pos.X < rect.Size.X - 80 && pos.Y < rect.Size.Y - 160)
                {
                    clickedRobot = robot.Id;
                    Click(pos);
                    break;
                }
            }
            await Frames(3);
            if (clickedRobot != null)
                Check(selection.Text.Contains("已选"), "机器人点选生效：" + selection.Text);
            else
                NotRun("机器人点选（无机器人投影在画面内）");

            // 确认按钮双击只提交一次（同一点位重选）
            Click(_camera.UnprojectPosition(new Vector3(legal.Center.X, 0.2f, legal.Center.Z)));
            await WaitFor(() => !confirm.Disabled, 5, "确认按钮再次启用");
            var center = confirm.GetGlobalRect().GetCenter();
            Click(center);
            Click(center);
            await Frames(6);
            var job = _world.ReadPlayerState().Job;
            Check(job is { Active: true }, "确认后任务活动");
            Check(job?.Id == "level-1", "双击只产生一个任务（level-1）：" + job?.Id);
            Check(job?.WorkerId.StartsWith("Robot_Zhulei_", StringComparison.Ordinal) == true,
                "执行者是筑垒：" + job?.WorkerId);
            Check(job?.Stage == "前往现场", "阶段文本：" + job?.Stage);

            // 取消（等待命令冷却过期，headless 帧率极高不能按帧数计时）
            var cancel = (Button)_ui.FindChild("CancelButton", true, false)!;
            await CommandClick(cancel);
            await WaitFor(() => _world.ReadPlayerState().Job is not { Active: true }, 10, "取消生效");
            Check((_world.ReadPlayerState().Job?.Active ?? false) == false, "取消后无活动任务");

            // 暂停：模拟时间冻结，但相机与 UI 仍可操作
            var pause = (Button)_ui.FindChild("PauseButton", true, false)!;
            await CommandClick(pause);
            await WaitFor(() => _world.ReadPlayerState().Paused, 10, "暂停生效");
            await Frames(2); // 等控制器 _Process 刷新按钮文本（process_frame 先于节点回调）
            Check(((Button)_ui.FindChild("PauseButton", true, false)!).Text == "继续", "暂停后按钮显示继续");
            double frozen = _world.ReadPlayerState().TimeSeconds;
            await Frames(30);
            Check(Math.Abs(_world.ReadPlayerState().TimeSeconds - frozen) < 1e-9, "暂停时模拟时间不推进");

            // CAMERA-01：暂停期间镜头按钮仍可用
            if (_ui.FindChild("CameraRotateRightButton", true, false) is Button camPause)
            {
                var basisAtPause = _camera.GlobalBasis.Z;
                Click(camPause.GetGlobalRect().GetCenter());
                await Frames(2);
                Check(basisAtPause.AngleTo(_camera.GlobalBasis.Z) > 0.1f, "暂停期间镜头按钮旋转可用");
            }
            else
                NotRun("暂停期间镜头按钮（按钮未建立）");

            var camBefore = _camera.GlobalPosition;
            PressKey(Key.W, true);
            await RealSeconds(0.25);
            PressKey(Key.W, false);
            await Frames(2);
            Check(_camera.GlobalPosition.DistanceTo(camBefore) > 1e-5, "暂停期间 WASD 平移可用");

            var distBefore = _camera.GlobalPosition.Length();
            Wheel(true);
            Wheel(true);
            await Frames(2);
            Check(_camera.GlobalPosition.Length() < distBefore - 1e-4, "滚轮缩放可用");

            var grab = Root.GetVisibleRect().Size / 2f;
            Vector3 Plane(Vector2 pixel)
            {
                var origin = _camera.ProjectRayOrigin(pixel); var ray = _camera.ProjectRayNormal(pixel);
                return origin - ray * (origin.Y / ray.Y);
            }
            var dragBefore = _camera.GlobalPosition;
            var expectedDrag = Plane(grab) - Plane(grab + new Vector2(30, 0));
            Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = grab });
            for (int i = 1; i <= 3; i++) Root.PushInput(new InputEventMouseMotion { Position = grab + new Vector2(i * 10, 0), Relative = new Vector2(10, 0), ButtonMask = MouseButtonMask.Right });
            await Frames(2);
            Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = grab + new Vector2(30, 0) });
            Check((_camera.GlobalPosition - dragBefore).DistanceTo(expectedDrag) < .001, "连续拖动只累计实际总位移");
            var beforeBasis = _camera.GlobalBasis;
            PressKey(Key.Q, true); await RealSeconds(.2); PressKey(Key.Q, false); await Frames(2);
            Check(_camera.GlobalBasis.Z.DistanceTo(beforeBasis.Z) > .05, "暂停期间镜头旋转可用");
            PressKey(Key.Escape, true); PressKey(Key.Escape, false); await Frames(2);
            Check(!selection.Text.Contains("已选") && confirm.Disabled, "Esc 清除预览而不重建旧选区");

            await CommandClick(pause);
            await WaitFor(() => !_world.ReadPlayerState().Paused, 10, "继续生效");

            // 非法命令被拒
            _world.QueuePlayerAction("dig");
            await Frames(3);
            Check(_world.ReadPlayerState().Notice.Contains("未知"), "未知操作被拒：" + _world.ReadPlayerState().Notice);

            // 保存与读取（存档文件跨运行持久，等通知文本而非 SaveExists 标志）
            _world.QueuePlayerAction("save");
            await WaitFor(() => _world.ReadPlayerState().Notice.Contains("已保存"), 10, "保存成功提示");
            Check(_world.ReadPlayerState().SaveExists, "保存成功可见");
            _world.QueuePlayerAction("load");
            await WaitFor(() => _world.ReadPlayerState().Notice.Contains("读取完成"), 60, "读取后世界恢复");
            Check(_world.ReadPlayerState().Ready, "读取后世界就绪");
            await Frames(5);
            Check(_ui.FindChild("ConfirmButton", true, false) is Button, "读取完成后 UI 仍可用");

            // —— D1.1 建设菜单：仅 bootstrap 启用时检查（Enabled=false 时界面与 D1.0 一致）——
            var boot = _world.ReadBootstrap();
            if (boot.Enabled)
            {
                Check(_ui.FindChild("ModeLevelButton", true, false) is Button, "整平模式按钮建立");
                Check(boot.Blueprints.Length == 5 && boot.Blueprints.All(b => _ui.FindChild("ModeButton_" + b.Id, true, false) is Button),
                    "五种建设模式按钮建立（来自真实蓝图）");
                Check(((Label)_ui.FindChild("StockLabel", true, false)!).Text.Contains("铁料"),
                    "库存与电力显示真实着陆器数据：" + ((Label)_ui.FindChild("StockLabel", true, false)!).Text);
                Check(_ui.FindChild("ConnectButton", true, false) is Button && _ui.FindChild("RetryButton", true, false) is Button,
                    "铺电缆与重试入口建立");

                var rotate = (Button)_ui.FindChild("RotateButton", true, false)!;
                await CommandClick(rotate);
                await Frames(2);
                Check(rotate.Text.Contains("90"), "90度朝向控件生效：" + rotate.Text);
                await CommandClick(rotate);
                await CommandClick(rotate);
                await CommandClick(rotate);
                await Frames(2);
                Check(rotate.Text.Contains("0"), "朝向 270° 回绕到 0°：" + rotate.Text);

                // 真实下单：选充电桩 → 扫描合法建设点 → 点地面 → 确认 → 建设任务 → 取消
                var charger = (Button)_ui.FindChild("ModeButton_charger", true, false)!;
                await CommandClick(charger);
                await Frames(3);
                var buildSite = FindLegalBuildSite("charger");
                Check(buildSite.Legal, "建设模式找到合法预览点：" + buildSite.Reason);
                Click(_camera.UnprojectPosition(new Vector3(buildSite.Center.X, 0.2f, buildSite.Center.Z)));
                await Frames(3);
                Check(selection.Text.Contains("已选位置"), "建设模式点选地面显示已选位置");
                Check(selection.Text.Contains("成本"), "已选位置显示材料成本：" + selection.Text);
                await WaitFor(() => !confirm.Disabled, 5, "建设确认按钮启用");
                await CommandClick(confirm);
                await WaitFor(() => _world.ReadPlayerState().Job is { Active: true }, 10, "建设任务活动");
                var buildJob = _world.ReadPlayerState().Job;
                Check(buildJob?.Id.StartsWith("build-", StringComparison.Ordinal) == true,
                    "确认建设产生真实建设任务：" + buildJob?.Id + " · " + buildJob?.Stage);
                await CommandClick(cancel);
                await WaitFor(() => _world.ReadPlayerState().Job is not { Active: true }, 10, "取消建设生效");
            }
            else
            {
                NotRun("D1.1 建设菜单（bootstrap 未启用）");
            }

            // —— CAMERA-01 二阶段：真实相机按钮验收（读档完成后跑，覆盖读档后相机可用）——
            await CameraUiChecks();
            // —— CAMERA-01 镜头输入诊断：同帧/跨帧短按、长按对照、滚轮、触控板手势、HUD释放残留 ——
            await CameraDiag();

            Report();
            Quit(_failures == 0 ? 0 : 1);
        }
        catch (Exception e)
        {
            GD.Print("PLAYER_UI_SELFTEST FAIL 异常 " + e.GetType().Name + ": " + e.Message);
            Quit(1);
        }
    }

    // ---- 帮助 ----

    // 网格扫描一个合法整平点：机器人停驻位随种子变化，不能写死坐标。
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

    // 网格扫描一个合法建设点（走真实 PreviewBuild 合同）。
    private PlayerSitePreview FindLegalBuildSite(string type)
    {
        for (float x = -20f; x <= 20f; x += 4f)
            for (float z = -20f; z <= 20f; z += 4f)
            {
                var preview = _world.PreviewBuild(type, new Vector3(x, 0, z));
                if (preview.Legal) return preview;
            }
        throw new InvalidOperationException("全场扫描不到合法建设点");
    }

    private void Check(bool condition, string label)
    {
        _results.Add((condition ? "PASS " : "FAIL ") + label);
        if (!condition) _failures++;
        GD.Print("PLAYER_UI_SELFTEST " + (condition ? "PASS " : "FAIL ") + label);
    }

    private void NotRun(string label)
    {
        _results.Add("NOT_RUN " + label);
        GD.Print("PLAYER_UI_SELFTEST NOT_RUN " + label);
    }

    private void Report()
    {
        GD.Print("PLAYER_UI_SELFTEST summary checks=" + _results.Count + " failures=" + _failures);
        foreach (var line in _results)
            GD.Print("PLAYER_UI_SELFTEST result " + line);
    }

    // 合成点击驱动主场景控制器；最终包真实鼠标另验。
    private async System.Threading.Tasks.Task CameraUiChecks()
    {
        GD.Print("PLAYER_UI_SELFTEST CAMUI begin");
        Button? Cam(string name) => _ui.FindChild(name, true, false) as Button;
        var forward = Cam("CameraForwardButton");
        var back = Cam("CameraBackButton");
        var left = Cam("CameraLeftButton");
        var right = Cam("CameraRightButton");
        var zoomIn = Cam("CameraZoomInButton");
        var zoomOut = Cam("CameraZoomOutButton");
        var rotL = Cam("CameraRotateLeftButton");
        var rotR = Cam("CameraRotateRightButton");
        var reset = Cam("CameraResetButton");
        var buttons = new[] { forward, back, left, right, zoomIn, zoomOut, rotL, rotR, reset };
        Check(buttons.All(b => b != null), "九个镜头按钮建立（前后左右/拉近拉远/左右转/复位）");
        if (buttons.Any(b => b == null))
        {
            NotRun("镜头按钮操作（按钮未建立）");
            return;
        }
        Check(forward!.FocusMode != Control.FocusModeEnum.None, "镜头按钮保留键盘焦点可访问");

        // 基准机位：复位到初始焦点/距离/朝向
        Click(reset!.GetGlobalRect().GetCenter());
        await Frames(2);
        var baseOrigin = _camera.GlobalPosition;
        var baseBasisZ = _camera.GlobalBasis.Z;

        // 布局：镜头行与按钮在1280x800测试窗口内。
        var view = Root.GetVisibleRect().Size;
        bool InView(Control c)
        {
            var r = c.GetGlobalRect();
            return r.Position.Y >= 0f && r.End.Y <= view.Y && r.Position.X >= 0f && r.End.X <= view.X;
        }
        Check(forward.GetParent() is HBoxContainer camRow && InView(camRow)
            && buttons.All(b => InView(b!)), "镜头行与全部按钮在视口内不裁切");

        Vector3 FlatForward()
        {
            var f = -_camera.GlobalBasis.Z;
            f.Y = 0f;
            return f.Normalized();
        }
        Vector2 Xz(Vector3 v) => new(v.X, v.Z);

        // 『前』沿当前朝向明显平移
        var want = Xz(FlatForward()).Normalized();
        var o = _camera.GlobalPosition;
        Click(forward.GetGlobalRect().GetCenter());
        await Frames(2);
        var moved = Xz(_camera.GlobalPosition - o);
        Check(moved.Length() > 2f && moved.Normalized().Dot(want) > 0.95f,
            "『前』按钮沿当前朝向明显平移: " + F(moved.Length()) + "m");

        // 『右转』单次约15度
        var b0 = _camera.GlobalBasis.Z;
        Click(rotR!.GetGlobalRect().GetCenter());
        await Frames(2);
        float rot = MathF.Abs(Xz(b0).AngleTo(Xz(_camera.GlobalBasis.Z)));
        Check(MathF.Abs(rot - Mathf.Pi / 12f) < 0.01f, "『右转』单次15度: " + F(rot) + "rad");

        // 旋转后『前』沿新朝向平移
        want = Xz(FlatForward()).Normalized();
        o = _camera.GlobalPosition;
        Click(forward.GetGlobalRect().GetCenter());
        await Frames(2);
        moved = Xz(_camera.GlobalPosition - o);
        Check(moved.Length() > 2f && moved.Normalized().Dot(want) > 0.95f, "旋转后『前』沿新朝向平移");

        // 连点三次『右转』约45度：不受工程冷却（若吞第二三下只转15度则红）
        b0 = _camera.GlobalBasis.Z;
        for (int i = 0; i < 3; i++) Click(rotR.GetGlobalRect().GetCenter());
        await Frames(2);
        rot = MathF.Abs(Xz(b0).AngleTo(Xz(_camera.GlobalBasis.Z)));
        Check(MathF.Abs(rot - Mathf.Pi / 4f) < 0.01f, "连点三次『右转』45度不受工程冷却: " + F(rot) + "rad");

        // 缩放两侧边界
        Click(reset.GetGlobalRect().GetCenter());
        await Frames(2);
        for (int i = 0; i < 22; i++) Click(zoomIn!.GetGlobalRect().GetCenter());
        await Frames(2);
        o = _camera.GlobalPosition;
        Click(zoomIn!.GetGlobalRect().GetCenter());
        await Frames(2);
        Check(o.DistanceTo(_camera.GlobalPosition) < 0.05f, "连续拉近到下限后不再变化");
        for (int i = 0; i < 30; i++) Click(zoomOut!.GetGlobalRect().GetCenter());
        await Frames(2);
        o = _camera.GlobalPosition;
        Click(zoomOut!.GetGlobalRect().GetCenter());
        await Frames(2);
        Check(o.DistanceTo(_camera.GlobalPosition) < 0.05f, "连续拉远到上限后不再变化");

        // 平移两侧边界
        Click(reset.GetGlobalRect().GetCenter());
        await Frames(2);
        for (int i = 0; i < 40; i++) Click(forward.GetGlobalRect().GetCenter());
        await Frames(2);
        o = _camera.GlobalPosition;
        Click(forward.GetGlobalRect().GetCenter());
        await Frames(2);
        Check(Xz(o - _camera.GlobalPosition).Length() < 0.05f, "平移到前限后不再变化");
        for (int i = 0; i < 80; i++) Click(back!.GetGlobalRect().GetCenter());
        await Frames(2);
        o = _camera.GlobalPosition;
        Click(back!.GetGlobalRect().GetCenter());
        await Frames(2);
        Check(Xz(o - _camera.GlobalPosition).Length() < 0.05f, "平移到后限后不再变化");

        // 乱操作后复位精确恢复初始机位
        Click(rotL!.GetGlobalRect().GetCenter());
        Click(zoomIn.GetGlobalRect().GetCenter());
        Click(forward.GetGlobalRect().GetCenter());
        await Frames(2);
        Click(reset.GetGlobalRect().GetCenter());
        await Frames(2);
        Check(baseBasisZ.AngleTo(_camera.GlobalBasis.Z) < 1e-3f && baseOrigin.DistanceTo(_camera.GlobalPosition) < 0.01f,
            "复位恢复初始焦点/距离/朝向");

        // 镜头按钮不占用工程冷却：先等前序工程按钮（取消建设）的冷却过期，
        // 再点镜头按钮后立即保存——若镜头按钮错误设置冷却，保存会被吞而超时。
        await RealSeconds(0.5);
        Click(rotR.GetGlobalRect().GetCenter());
        var save = (Button)_ui.FindChild("SaveButton", true, false)!;
        Click(save.GetGlobalRect().GetCenter());
        await WaitFor(() => _world.ReadPlayerState().Notice.Contains("已保存"), 10, "镜头按钮后保存立即执行");
        Check(_world.ReadPlayerState().SaveExists, "保存成功（冷却未被镜头按钮占用）");

        // 镜头操作不下达任务
        Check(_world.ReadPlayerState().Job is not { Active: true }, "镜头操作不下达任务");
        GD.Print("PLAYER_UI_SELFTEST CAMUI end");
    }

    // 同帧按下/释放用于诊断自动化边界，不作为玩家失效证据。
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

        // 同帧短按 Q：press+release 在同一帧窗口解析，下一帧 flush 时先按下后释放，轮询应看不见
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

        // 同帧短按 Right（平移走同一条轮询路径）
        {
            var originBefore = _camera.GlobalPosition;
            bool preParse = Input.IsKeyPressed(Key.Right);
            PressKey(Key.Right, true);
            bool afterPress = Input.IsKeyPressed(Key.Right);
            PressKey(Key.Right, false);
            bool afterRelease = Input.IsKeyPressed(Key.Right);
            await Frames(3);
            Diag("same-frame Right move=" + F(_camera.GlobalPosition.DistanceTo(originBefore)) + "m" +
                " originAfter=" + Fmt(_camera.GlobalPosition) +
                " IsKeyPressed(preParse/afterPress/afterRelease)=" + preParse + "/" + afterPress + "/" + afterRelease);
        }

        // 跨帧短按 Q（press → 2帧 → release，真实按键的最小形态）
        {
            var basisBefore = _camera.GlobalBasis;
            PressKey(Key.Q, true);
            await Frames(2);
            bool polled = Input.IsKeyPressed(Key.Q);
            PressKey(Key.Q, false);
            await Frames(2);
            float rotation = basisBefore.Z.AngleTo(_camera.GlobalBasis.Z);
            Diag("frame-span Q(2帧) rotation=" + F(rotation) + "rad polledDuringPress=" + polled);
            Check(rotation > 1e-6f, "跨帧短按Q被轮询到(旋转非零): " + F(rotation) + "rad polled=" + polled);
        }

        // 跨帧短按 Right
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

        // 人级短按 Q（60ms，所有者报告的操作量级）
        {
            var basisBefore = _camera.GlobalBasis;
            PressKey(Key.Q, true);
            await RealSeconds(0.06);
            bool polled = Input.IsKeyPressed(Key.Q);
            PressKey(Key.Q, false);
            await Frames(2);
            float rotation = basisBefore.Z.AngleTo(_camera.GlobalBasis.Z);
            Diag("short Q(60ms) rotation=" + F(rotation) + "rad polled=" + polled);
            Check(rotation > 0.02f, "人级短按Q(60ms)旋转可见(期望约0.06rad): " + F(rotation));
        }

        // 长按对照（轮询接缝阳性对照，diag-only 模式下自含）
        {
            var basisBefore = _camera.GlobalBasis;
            PressKey(Key.Q, true);
            await RealSeconds(0.3);
            PressKey(Key.Q, false);
            await Frames(2);
            Check(basisBefore.Z.AngleTo(_camera.GlobalBasis.Z) > 0.15f, "长按Q(0.3s)旋转显著");
        }
        {
            var originBefore = _camera.GlobalPosition;
            PressKey(Key.Right, true);
            await RealSeconds(0.3);
            PressKey(Key.Right, false);
            await Frames(2);
            Check(originBefore.DistanceTo(_camera.GlobalPosition) > 0.5f, "长按Right(0.3s)平移显著");
        }

        // 滚轮（事件路径，PushInput 立即传播到 _UnhandledInput）
        {
            var originBefore = _camera.GlobalPosition;
            Wheel(true);
            await Frames(2);
            float moved = originBefore.DistanceTo(_camera.GlobalPosition);
            Diag("wheel-up x1 originΔ=" + F(moved) + "m");
            Check(moved > 0.5f, "滚轮上滚拉近可见: " + F(moved) + "m");
        }

        // 触控板手势：记录控制器实际反应
        {
            var before = _camera.GlobalTransform;
            Root.PushInput(new InputEventMagnifyGesture { Position = Root.GetVisibleRect().Size / 2f, Factor = 1.25f });
            await Frames(2);
            var after = _camera.GlobalTransform;
            Diag("MagnifyGesture factor=1.25 originΔ=" + F(before.Origin.DistanceTo(after.Origin)) + "m" +
                " basisΔ=" + F(before.Basis.Z.AngleTo(after.Basis.Z)) + "rad");
        }
        {
            var before = _camera.GlobalTransform;
            Root.PushInput(new InputEventPanGesture { Position = Root.GetVisibleRect().Size / 2f, Delta = new Vector2(240, 0) });
            await Frames(2);
            var after = _camera.GlobalTransform;
            Diag("PanGesture delta=(240,0) originΔ=" + F(before.Origin.DistanceTo(after.Origin)) + "m" +
                " basisΔ=" + F(before.Basis.Z.AngleTo(after.Basis.Z)) + "rad");
        }

        // REQUIRE_GESTURES（CAMERA-01 二阶段验收）：触控板捏合/双指手势必须驱动相机
        {
            var before = _camera.GlobalPosition;
            Root.PushInput(new InputEventMagnifyGesture { Position = Root.GetVisibleRect().Size / 2f, Factor = 1.6f });
            await Frames(2);
            Check(before.DistanceTo(_camera.GlobalPosition) > 0.5f,
                "MagnifyGesture 捏合缩放驱动相机: Δ=" + F(before.DistanceTo(_camera.GlobalPosition)) + "m");
        }
        {
            var before = _camera.GlobalPosition;
            Root.PushInput(new InputEventPanGesture { Position = Root.GetVisibleRect().Size / 2f, Delta = new Vector2(120, 0) });
            await Frames(2);
            Check(before.DistanceTo(_camera.GlobalPosition) > 0.1f,
                "PanGesture 双指平移驱动相机: Δ=" + F(before.DistanceTo(_camera.GlobalPosition)) + "m");
        }

        // 右键拖动；随后在 HUD 面板(MouseFilter=Stop)上释放，再无按键移动鼠标测残留拖动
        {
            var pressPos = new Vector2(900, 600);
            var originBefore = _camera.GlobalPosition;
            Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = pressPos });
            for (int i = 1; i <= 3; i++)
                Root.PushInput(new InputEventMouseMotion { Position = pressPos + new Vector2(i * 10, 0), Relative = new Vector2(10, 0), ButtonMask = MouseButtonMask.Right });
            await Frames(2);
            float dragged = originBefore.DistanceTo(_camera.GlobalPosition);
            Check(dragged > 0.05f, "右键拖动平移可见: " + F(dragged) + "m");

            var panelPos = new Vector2(120, 200); // InfoPanel(MouseFilter=Stop) 内
            Root.PushInput(new InputEventMouseMotion { Position = panelPos, Relative = new Vector2(10, 0), ButtonMask = MouseButtonMask.Right });
            Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = panelPos });
            await Frames(2);
            var afterRelease = _camera.GlobalPosition;
            for (int i = 1; i <= 3; i++) // 无任何按键的普通移动（用户已松开）
                Root.PushInput(new InputEventMouseMotion { Position = new Vector2(640 + i * 20, 500), Relative = new Vector2(20, 0) });
            await Frames(2);
            float residual = afterRelease.DistanceTo(_camera.GlobalPosition);
            Diag("右键拖动=" + F(dragged) + "m HUD面板释放后无键移动Δ=" + F(residual) + "m" +
                (residual > 0.01f ? " → 残留拖动（释放事件被HUD吞掉，_panGrab未清）" : " → 无残留"));
        }
        GD.Print("PLAYER_UI_SELFTEST CAMERADIAG end");
    }

    private void Diag(string message) => GD.Print("PLAYER_UI_SELFTEST CAMERADIAG " + message);
    private static string F(float v) => v.ToString("G4", Inv);
    private static string Fmt(Vector3 v)
        => v.X.ToString("F2", Inv) + "," + v.Y.ToString("F2", Inv) + "," + v.Z.ToString("F2", Inv);

    private void Click(Vector2 pos)
    {
        Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = pos });
        Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = pos });
    }

    // 点命令按钮：先等上一条命令的冷却过期（headless 帧率极高，不能按帧数计时），再点击。
    private async System.Threading.Tasks.Task CommandClick(Button button)
    {
        await RealSeconds(0.5);
        Click(button.GetGlobalRect().GetCenter());
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
        });
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
