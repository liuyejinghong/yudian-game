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
- Esc：局内逐层（取消确认子层 → 关联工程子详情 → 管理层 → 显式接线 → 选址预览 → 暂停菜单），Esc 不取消正式工程；
  菜单/确认页 → back；菜单页 Esc 不被局内覆盖层状态截走。
- 局内 HUD：顶栏常驻库内现货（Stored 口径 chip，点击打开基地物资并按该物资过滤）、发电/s、昼夜与真实警报
  （警报取 typed Work.Status/ReasonCode 与设施接线事实，不用中文关键字解析通知）；
  教学目标卡可整卡收成一行（标题保留，展开核心含为什么），无教学目标（含 freeplay 结尾确认后）整卡隐藏；
  typed 对象卡（机器人/矿点/设施，按 epoch 经 ReadObjectDetail 重解析，消失即清选，无空对象板；画面外目标先把相机对准再用真实 input 点击；
  目标投影被可见语境卡遮挡时，先在无 HUD 挡世界的一点真实右键清选（生产行为）再重试，被遮挡面板会吞掉其上的任何点击）；
  选址准备卡（建设/整地左键仅选址不选机；固定操作区＝标题/场址朝向/状态/完整拒因小滚动块/开始与退出按钮/自动备料提示，拒因不截断不折叠、超长区内滚动；
  开始/退出先于多行成本与来源；成本/供料/后果处于默认可见的内部成本滚动区（完整需/已有 EligibleStock/缺口顶部可见，超长区内滚动可查），电源/备选/库存长详情折叠滚动；
  第一次建设未选蓝图：明示选择蓝图、禁提交、不整地 fallback）；
  独立活动工程一行（有活动才显示，点击进工程管理）；底部建设/整地/基地物资/工程工具组 + 常规「补维修耗材」经营入口，
  无科研/地图/背包入口；显式接线位于工具组，候选逐项列出、Esc 先关闭；
  诊断折叠（读取旧档/故障恢复/重试/自动接最近电缆）只在 `YUDIAN_PLAYER_GUI_TEST=1` 或 `YUDIAN_PLAYER_ENG_DIAG=1` 下显示，普通包隐藏。
- 管理层：SetPlayerManagement 同步成功才开、遮罩在所有局内控件之上（含底部工具与教学结尾卡）并 Stop 门禁键盘/每帧镜头轮询、世界命令被拒、
  同层物资/工程标签互切、被遮罩覆盖的重复入口不可达（关闭走关闭按钮/Esc）、用户手动暂停保值、取消工程后果确认为独占顶层（指针/焦点独占，Esc 只退该层）
  并在确认后同步关闭管理且单次提交；物资按定义说明/物理位置/预留/携带/加工预计分口径显示并过滤，
  预约按 GoalId/TaskId 解析并显示真实「查看关联工程」按钮进入同层详情（Esc 先退子详情；文案不露内部目标 ID），历史只显示真实已有记录；
  管理面板 760×520 基数随缩放并 clamp 到视口，内部滚动不把关闭/确认挤出；离开 game（生命周期）或权威临停解除时同步丢弃覆盖层状态，取消退出再继续不复活管理。
- placement returncontext：进入选址保存镜头与 typed 选中（记录当时 epoch），Esc 退出按 epoch+stableID 重解析恢复；
  load/newgame 尝试轮次（`WorldTransitionRevision`，含校验失败的尝试）变化即清临时选择/选址/接线 context，同 epoch 失败也不复用草案；
  普通 menu 同 epoch 返回保 context。
- 尝试轮次用例：真实私槽保存后损坏私档文件再确认读取，断言 revision 递增、epoch 不变、选址/整地草案被清。
- 设置：从 `ReadSettings` 读真实值并实际生效（100/125/150% → 正文字号 18/23/27，
  镜头速度影响平移速率，精简/详细切换提示详略）；`QueueSettings` 写回。
- 接线独立真实用例：有限正常初始库存，`QueueBuild` 建太阳能(-3,-14)与充电桩(-3,-3)；真实常用接线按钮展开面板，候选行点击明确来源、确认双击；真实运输施工完成才断言 `ServiceConnected`、目标来源、教学推进与私有保存账本 `spent.cable` 增4。开局只有登陆器时，空候选用已建太阳能＋用电设施判断，不报告伪 BLOCKED。
  首次真实太阳能建设授权过程内（不注入材料）：等真实物资预约出现后打开基地物资，断言预约 Goal 对应当前工程（TaskId/GoalId），点行内「查看关联工程」实际跳同层工程详情，Esc 退回物资标签，关闭管理层释放临停让建设继续；预约未按权威时机出现则如实 BLOCKED。
- 设置失败恢复：只在本次私有根制造配置写入失败，等待 UI 草案回显超时，再只改镜头速度；断言不携带失败缩放/提示值，收尾恢复旧设置。
- 拖动归属：右键拖动→Esc→菜单内释放→继续→无键实际 mouse motion；HUD 内释放后继续实际 motion；两条都断言无残留平移。
- 相机：九个镜头方向按钮已移除；用真实 engine input（Q/E 旋转、W 平移、滚轮缩放）与状态合同验收，
  帮助文本与真实映射一致；右键拖动残留、短按/长按轮询诊断保持；先等 UI 的 game 页与底部工具组完成布局。
- 尺寸与缩放：当期三尺寸 1280×800、1920×1080、1920×1200（headless）各跑 100/125/150%；1860×939 仅 legacy 额外样本，不冒充 1080。
  默认低频明细折叠、滚动位置为0，逐项核目标/指令/下一步/资源/作业；建设卡布局合同区分固定操作区与内部成本滚动区的准确 mask：
  固定区（标题/场址/状态/拒因/按钮/自动前置）完整在视口与外层遮罩内且不被命令栏遮挡，拒因块内滚动可查全；
  成本行顶部默认可见，供料/后果区内滚动可达（不截断不删除），开始/退出位于成本区之前。固定区出屏即 FAIL，不把失败改成 BLOCKED。
  布局用例前经控制器 testhook 把镜头恢复到进世界初姿（前序相机诊断会真实移动镜头），扫描诊断打印 focus/distance 与逐类拒因计数，不放宽 picker；
  150% 下另核管理面板视口 clamp 与关闭按钮同屏。打印 `PX`/`CORE_RECT`/`REASON_RECT`/`COST_RECT` 和 native viewport PNG；dummy 渲染的图为 `CAPTURE NOT_RUN`，不冒充视觉通过。
- 保存/读取兼容路径（权威旧接口）只使用私有测试档（断言为非 repo 的绝对路径）；
- 收尾按生命周期 `home`→`leave-save`→主页核私有槽可读，再由测试树打印完整 summary 并退出；返回主页不应被误测为退出应用。
