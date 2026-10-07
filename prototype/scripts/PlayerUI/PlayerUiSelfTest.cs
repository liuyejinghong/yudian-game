#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
            _camera = Root.GetCamera3D() ?? throw new InvalidOperationException("主场景没有当前相机");
            _ui = new PlayerController();
            Root.AddChild(_ui);
            _ui.Initialize(_world, _camera);
            await Frames(3);

            Check(_ui.FindChild("ConfirmButton", true, false) is Button, "HUD 建立确认按钮");
            Check(_ui.FindChild("SelectionLabel", true, false) is Label, "HUD 建立选择标签");
            Check(_world.ReadPlayerRobots().Length == 12, "普通场景 12 台机器人");

            // 预览：合法点（动态寻找，机器人停驻位随种子变化）/ 设施重叠 / 场外 / 望山不能当筑垒
            var legal = FindLegalSite();
            Check(legal.Legal, "找到合法预览点：" + legal.Reason);
            Check(!_world.PreviewLevel(new Vector3(14, 0, 0)).Legal, "设施重叠预览非法");
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
