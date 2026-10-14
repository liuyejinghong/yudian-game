# E01 / tutorial-mvp Player UI 自测（tools/player-ui-tests）

`Yudian.PlayerUI.PlayerController`（tutorial-mvp r1 新 UI）的自测入口。自测脚本本体在
`prototype/scripts/PlayerUI/PlayerUiSelfTest.cs`（C# `--script` 入口必须位于被编译的程序集内，故放在独占的 PlayerUI 目录），本目录只放运行说明与脚本。

## 运行

```sh
cd /path/to/yudian-game
./tools/player-ui-tests/run-selftest.sh      # 完整自测
./tools/player-ui-tests/run-camera-diag.sh   # 只跑相机验收与输入诊断
```

运行前必须给出**私有产物根**（repo 外；脚本与自测都不写 repo、不碰正式存档）：

- 显式 `YUDIAN_UI_TEST_ROOT`（绝对路径）→ 直接使用全新目录；已有 saves 或同名日志会拒绝，绝不删除旧证据；
- 否则给出 `CLAUDE_JOB_DIR` → 在 `$CLAUDE_JOB_DIR/tmp/player-ui-tests` 下派生唯一 run 目录
  （`run-<时间戳>-<pid>`，不覆盖旧 run 产物）；runner 与自测使用同一 root；
- 两者都无 → fail-fast 并提示传入参数，绝不回落 repo 或正式存档目录。

工具定位不猜个人安装：Godot 默认 `<repo 同级>/tools-bin/Godot.app/Contents/MacOS/Godot`
（已核存在），或显式 `YUDIAN_GODOT`；dotnet 默认 `$HOME/.dotnet/dotnet` 或显式 `YUDIAN_DOTNET`；
任一缺失即失败退出（码 2）。

## 输出

- 构建完整输出写 `$YUDIAN_UI_TEST_ROOT/build.log`，运行完整输出写 `selftest.log`（camera-diag 写 `camera-diag.log`），退出码保留实际进程值。
- 自测进入场景前清理继承的 `YUDIAN_*` 标记，只有自己的 `GUI_TEST` 与 `saves/player-ui-test-save.json` 生效；不继承其他工程 case 的教学根、资产跳过或自测模式。
- headless 用 `--fixed-fps 60` 的正常 1/60 delta 加快模拟，没有 `Engine.TimeScale`、健康/资源/设施注入。相机按帧时长测量；命令防双击仍按真实时间等待。
- 原生布局独立入口：`YUDIAN_UI_LAYOUT_CAPTURE_ONLY=1` 后运行同一 C# script，真实按钮切建造与 ray/机器人排除扫描的现场地面选点，分别截图正常草案和完整非法拒因。需要定向复验已建世界时，`YUDIAN_UI_LAYOUT_RESUME_SLOT` 只接受同一次修复私有根内正常保存的槽副本，按字节复制到全新根后公开 `QueueLifecycle(load)`；不编辑 JSON，不发设施/资源。此模式最后实际点保存并返回主页；不包含未跑的首套/相机用例，summary 只计本次执行项。
- `YUDIAN_UI_CONNECTION_CAPTURE_ONLY=1` 配合正常私有 `YUDIAN_UI_LAYOUT_RESUME_SLOT`，只核接线100/150%的面板、提示、成本/净缺口、完整来源行/距范围、拒因与确认关闭同时位于外层真实 viewport 和内层 scroll mask，输出两张连接图并保存返回主页。用于独立收口具体连接布局失败，不重放已通过的付费建设与相机。
- native 图只从本次 `Root` viewport 捕获，写 `captures`，不读桌面；逻辑尺寸乘屏幕 DPI 后设置物理窗口，合成输入显式 `inLocalCoords:true`，百分比缩放单独作用于 UI。
- 脚本退出码即 Godot 真实退出码：0=全过，1=有失败，3=无失败但存在 BLOCKED
  （`BLOCKED` 表示因权威协议 stub 未实现而无法真实执行的检查，不是通过）。

## 覆盖

- 主页（`EntryOverlay`）：标题居中、五按钮（新游戏/继续游戏/存档/设置/退出）、初始焦点、
  世界输入不穿透、1920 与大窗布局、标题字号基数 ≥ 40；「继续游戏」目标槽策略
  （优先所选可载入槽，否则最近 SavedAtUtc 的可载入槽，全无则禁用，并按目标槽显示标注）——
  合成用例纯逻辑断言，不依赖世界；槽摘要显示本地时间与目标名称，不显示 raw ISO/内部阶段 id。
- 生命周期（`ReadLifecycle`/`QueueLifecycle`）：newgame / slots / settings / pause / leave
  流程与键盘焦点、Esc 返回语义；权威协议 stub 未实现时输出 `BLOCKED`（不是通过），
  退化为旧兼容命令只为继续跑相机/布局检查。
- 教学上报：始终读真实 `ReadTutorial`；相机操作与真实机器人选择（真实 ID）在实际发生的那一刻
  按（世界 × 阶段 × 操作）去重上报 `QueueTutorialAction`——覆盖冻结的 `observe-team`
  认识镜头/选择阶段及后续阶段，每世界首次即可；页面出现、自测接缝都不会伪报。
- Esc：局内 → 暂停菜单（不再“只清除选区”，选区保持）；菜单/确认页 → back；
  接线面板打开时先关闭面板。
- 局内 HUD：目标标题来自 `ReadTutorial`（≥24）、必要指令、下一步/进度、功率、typed Available 摘要与非零在途/在制、真实作业同屏。完整原指令/为什么/覆盖、7种四字段库存、发展/设施/按键可在低频详情区回看，展开详情只滚明细。建造方案保完整成本/净缺口/拒因、选定供电来源与距离、供料路线和后果；全库存与备选来源回看。显式接线位于常用操作栏，候选逐项列出、Esc 先关闭。
- 设置：从 `ReadSettings` 读真实值并实际生效（100/125/150% → 正文字号 18/23/27，
  镜头速度影响平移速率，精简/详细切换提示详略）；`QueueSettings` 写回。
- 接线独立真实用例：有限正常初始库存，`QueueBuild` 建太阳能(-3,-14)与充电桩(-3,-3)；真实常用接线按钮展开面板，候选行点击明确来源、确认双击；真实运输施工完成才断言 `ServiceConnected`、目标来源、教学推进与私有保存账本 `spent.cable` 增4。开局只有登陆器时，空候选用已建太阳能＋用电设施判断，不报告伪 BLOCKED。
- 设置失败恢复：只在本次私有根制造配置写入失败，等待 UI 草案回显超时，再只改镜头速度；断言不携带失败缩放/提示值，收尾恢复旧设置。
- 拖动归属：右键拖动→Esc→菜单内释放→继续→无键实际 mouse motion；HUD 内释放后继续实际 motion；两条都断言无残留平移。
- 相机：九按钮、平移/旋转/缩放/复位、手势、右键拖动残留、短按/长按轮询诊断；先等 UI 的 game 页与可见复位按钮完成布局，再核配置初始焦点/距离/basis，方向 dot≥0.95 的原严格断言保持。
- 尺寸与缩放：1280×800 与1860×939逻辑点，100/125/150%；1920×1200仅headless扩展。默认低频明细折叠、滚动位置为0，逐项核目标/指令/下一步/资源/作业、完整成本/缺口/拒因/电源/供料在所属滚动遮罩内，且与确认同时可见。遮罩外即FAIL。打印 `PX`/`CORE_RECT` 和 native viewport PNG；dummy 渲染的图为 `CAPTURE NOT_RUN`，不冒充视觉通过。
- 保存/读取兼容路径（权威旧接口）只使用私有测试档（断言为非 repo 的绝对路径）；
- 收尾按生命周期 `home`→`leave-save`→主页核私有槽可读，再由测试树打印完整 summary 并退出；返回主页不应被误测为退出应用。
