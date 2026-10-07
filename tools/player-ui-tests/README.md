# E01 Player UI 自测（tools/player-ui-tests）

E01 交付物 `Yudian.PlayerUI.PlayerController` 的自测入口说明。共享场景未改动；
自测脚本本体在 `prototype/scripts/PlayerUI/PlayerUiSelfTest.cs`（C# `--script`
入口必须位于被编译的程序集内，故放在独占的 PlayerUI 目录），本目录只放运行
说明与脚本。

## 运行

```sh
cd /Users/ethan/yudian-game/d1-player-ui-r1
./tools/player-ui-tests/run-selftest.sh
```

等价手打（DOTNET_ROOT=/Users/ethan/.dotnet，Godot 4.7.2）：

```sh
cd prototype
/Users/ethan/.dotnet/dotnet build
/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot --headless --path . \
  --script res://scripts/PlayerUI/PlayerUiSelfTest.cs
```

## 行为

- 加载真实 `res://scenes/Main.tscn`（默认即玩家模式），等 `ReadPlayerState().Ready`。
- `AddChild` 后调用 `Initialize(Main, Camera3D)`（与主控接入顺序一致）。
- 断言：HUD 六按钮/标签建立；原点预览合法、设施重叠与场外非法、望山不能当筑垒；
  地面点选→确认启用→按钮双击只产生一个 `level-1` 任务且执行者是筑垒；取消；
  暂停时模拟时间冻结但 WASD/滚轮/UI 可用；未知命令被拒；保存可见成功；读取恢复；
  HUD 面板点击不穿透、空点清除选择且不下命令。
- 每项输出 `PLAYER_UI_SELFTEST PASS/FAIL <描述>`，末行
  `PLAYER_UI_SELFTEST summary checks=N failures=M`；退出码 0=全过，1=有失败或超时。
- 个别依赖投影位置的检查（机器人点选）在画面内找不到目标时输出 `NOT_RUN`，不计失败。
