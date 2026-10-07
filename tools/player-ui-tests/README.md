# E01 Player UI 自测（tools/player-ui-tests）

E01 交付物 `Yudian.PlayerUI.PlayerController` 的自测入口说明。共享场景未改动；
自测脚本本体在 `prototype/scripts/PlayerUI/PlayerUiSelfTest.cs`（C# `--script`
入口必须位于被编译的程序集内，故放在独占的 PlayerUI 目录），本目录只放运行
说明与脚本。

## 运行

```sh
cd /path/to/yudian-game
./tools/player-ui-tests/run-selftest.sh
```

环境变量（可选，默认 `$HOME` 下常见安装位置）：

- `YUDIAN_GODOT`：Godot 4 可执行文件，默认 `$HOME/Applications/Godot.app/Contents/MacOS/Godot`
- `YUDIAN_DOTNET`：dotnet 可执行文件，默认 `$HOME/.dotnet/dotnet`；`DOTNET_ROOT` 取其目录

等价手打（`YUDIAN_GODOT=<godot>`、`YUDIAN_DOTNET=<dotnet>`，Godot 4.x）：

```sh
cd prototype
"$YUDIAN_DOTNET" build
"$YUDIAN_GODOT" --headless --path . --script res://scripts/PlayerUI/PlayerUiSelfTest.cs
```

## 行为

- 加载真实 `res://scenes/Main.tscn`（默认即玩家模式），等 `ReadPlayerState().Ready`。
- 复用 Main 默认注册的 PlayerController；显式 GUI_TEST 和独立临时保存槽，不覆盖用户档。
- 断言：HUD 六按钮/标签建立；动态寻找合法预览点、设施重叠与场外非法、望山不能当筑垒；
  地面点选→确认启用→按钮双击只产生一个 `level-1` 任务且执行者是筑垒；取消；
  暂停时模拟时间冻结但 WASD/滚轮/旋转/UI 可用；连续拖动不重复累计，Esc清除预览；未知命令被拒；保存可见成功；读取恢复；
  HUD 面板点击不穿透、空点清除选择且不下命令。
- D1.1 追加（仅 `ReadBootstrap().Enabled` 时检查，否则输出 `NOT_RUN` 不计失败）：
  整平+五种蓝图模式按钮来自真实 `BootstrapReadModel`；库存/电力显示真实着陆器数据（含"铁料"）；
  连接电缆与重试入口建立；90度朝向按钮 0°→90°→…→0° 回绕；
  建设模式走真实合同：选充电桩→`PreviewBuild` 扫描合法点→点地面显示已选位置与材料成本→
  确认产生真实 `build-*` 建设任务→取消恢复。
- 每项输出 `PLAYER_UI_SELFTEST PASS/FAIL <描述>`，末行
  `PLAYER_UI_SELFTEST summary checks=N failures=M`；退出码 0=全过，1=有失败或超时。
- 个别依赖投影位置的检查（机器人点选）在画面内找不到目标时输出 `NOT_RUN`，不计失败。

## CAMERA-01 镜头验收与诊断（快速重跑）

```sh
cd /path/to/yudian-game
./tools/player-ui-tests/run-camera-diag.sh   # 相机按钮验收 + 镜头诊断 phase
./tools/player-ui-tests/run-selftest.sh      # 完整自测（原 38 项 + 末尾同一验收/诊断 phase）
```

输出行前缀 `PLAYER_UI_SELFTEST CAMUI`（按钮验收）与 `PLAYER_UI_SELFTEST CAMERADIAG`
（输入诊断），在 Main 场景和现有 PlayerController 上驱动合成点击/输入事件。
两个入口均先构建，避免验证旧程序集。

- 相机按钮验收（CAMUI）：九按钮（前/后/左/右、拉近/拉远、左转/右转、复位）建立与
  视口内不裁切；单次点击沿当前朝向明显平移/约15度旋转；连点不受工程冷却且镜头按钮
  紧跟的保存立即执行；缩放/平移两侧到限幅后不再变化；复位精确恢复初始机位；
  镜头操作不下达任务；保留键盘焦点。
- REQUIRE_GESTURES：`MagnifyGesture`（捏合缩放）与 `PanGesture`（双指平移）必须驱动
  相机（合成手势只作接缝验证，不替代物理触控板验收）。
- 同帧 press+release 短按 Q/Right：自动化边界，只记录前后
  origin/basis 与 `Input.IsKeyPressed` 采样，不判红。
- 跨帧短按（press → 2帧 → release）与人级短按 Q（60ms）、跨帧短按 Right：轮询路径
  （`UpdateCamera` 逐帧 `Input.IsKeyPressed`），丢失则红。
- 长按 0.3s Q/Right 阳性对照；滚轮上滚（事件路径 `PushInput`）。
- 右键拖动；在 HUD 面板（`MouseFilter=Stop`）上释放后无按键移动，测 `_panGrab` 残留拖动。
