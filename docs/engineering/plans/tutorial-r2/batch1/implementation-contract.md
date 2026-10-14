# tutorial-r2 批1 · 实施合同

2026-10-14｜所有者已授权按既定方案修复、创建任务、独立分支提交推送及 draft PR；原生自动化由 Codex 执行。本合同固定本轮公共协议与文件所有权，补充上级 remediation-plan.md / hud-spec.md；不重写其历史状态。集成后已证局部缺口按 [integration-fixes.md](integration-fixes.md) 补充稳定build任务身份与WorldTransitionRevision；两轮实际UI失败后的布局决策见 [ui-layout-replan.md](ui-layout-replan.md)。

## 目标与交付边界

新玩家能从矿点、设施和机器人读到真实事实，预览并授权一件缺料工程，看见采矿→加工→运输→施工→建成接线。工程验收交付候选；无提示真人 G001 未执行前不关闭 D001。

保留原 7 物资、4 配方、5 建筑的数值、稳定身份、配置文件原字节和配置 hash，保单活动工程、真实账本/预留/货物/地形/保存。批2地形规则扩展、下一计划、批3科研/kit配方、地图/背包、升级/拆除不并入。B002 只补可靠可见原因，正常 UI 复现与同目标恢复交 Codex；取得根因前不改导航、坡角、碰撞或搬移机器人。

实施基线为本分支 `a4745ba`（完整 r1 已由 `9c85b98` 固定），不是 main 的历史文档状态。

## 1. 统一只读定义目录

在 `Resources/Core` 建立最小 `DefinitionCatalog`，定义包含 stableID、显示名、说明、类别、单位、视觉引用；配方包含真实输入/输出、时间/功率，建筑包含真实成本、半径/工时。定义 ID 与实例、容器、工程、交易 ID 分开。

- 七种物资身份只在目录登记；`MaterialLedger.Materials` 从目录取得不可修改的 ID 列表，保既有消费接口的读操作。
- 物资定义采用不可修改值；配方/建筑数值从已加载 `BootstrapConfig` 复制到只读目录，不让目录或 UI 修改 config/ledger。名称/说明等缺失元数据在目录补充，不改旧 JSON 或配置 hash。
- 保留现有五种建筑/四种基础配方为必要能力，以成员、唯一 ID、非空、合法引用校验代替精确数量 5/4 闸门；planner 同步去掉精确 4 配方闸门，仍拒 null/空、重复 ID、重复产出、非法数量、非法材料、kit 自产与不支持的多产出。必要启动保障计算只包括既有基础保障设施，不能因新增目录条目自动收费。
- 不宣称加一个条目就获得新矿点、设施行为、视觉接入或旧档迁移。目录扩展与玩法执行能力是两个边界；本批不添加实际新定义或新行为。
- 校验配方闭环/引用与计量；真实采矿仍仅 iron_ore/copper_ore。保账本幂等、守恒、载荷和预留的既有合同。
- 沿用 `tools/material-ledger-tests`、`tools/production-plan-tests`，补目录引用、重复身份、只读/复制隔离和非固定数量的反例，不建立重复测试框架。

Core 接口冻结：`MaterialDefinition(Id, Name, Description, Category, Unit, VisualRef)`；`RecipeCatalogDefinition(Id, Name, Description, Category, Unit, VisualRef, Input, Output, WorkSeconds, Power)`；`BuildingCatalogDefinition(Id, Name, Description, Category, Unit, VisualRef, Cost, Radius, WorkSeconds)`，数量映射为真正只读的 `IReadOnlyDictionary<string,int>`。`DefinitionCatalog` 提供静态只读 `MaterialIds`、`Material(string id)`，构造器 `DefinitionCatalog(BootstrapConfig config)`，实例只读 `Materials`、`Recipes`、`Buildings`。所有集合有只读包装且从 config 拷贝；权威者只从这个 API 转 DTO。本批元数据不参与旧数值配置 hash，旧配置文件不修改。

## 2. 公共读模型（整合者独写）

保留 r1 调用需要的 DTO，新增字段可放末尾并有兼容默认值。旧 `PlayerJobView.Stage` 必须恢复阶段语义，不能再装 Reason。下列是 UI 的固定入口；嵌套 record 可按本节命名建立，纯值数组均为脱离权威的新副本。

### 定义

`Main.ReadDefinitions() -> PlayerDefinitionsView`，包含 `Materials`、`Recipes`、`Buildings` 三类 detached 数组。

- `MaterialDefinitionView(Id, Name, Description, Category, Unit, VisualRef)`。
- `RecipeDefinitionView(Id, Name, Description, Category, Unit, VisualRef, Input: MaterialAmountView[], Output: MaterialAmountView[], WorkSeconds, Power)`。
- `BuildingDefinitionView(Id, Name, Description, Category, Unit, VisualRef, Cost: MaterialAmountView[], Radius, WorkSeconds)`。

视觉引用必须指向现有项目资源或明确的已有呈现方式；不凭空引用资产。主控验存在性。

### 世界对象

`WorldObjectKind = Robot | Mine | Facility`。

`WorldObjectView(WorldEpoch, Kind, Id, DefinitionId, Name, Position, Radius)`。

`Main.ReadWorldObjects() -> WorldObjectView[]`；`Main.ReadObjectDetail(kind, id, observedEpoch) -> PlayerObjectDetailView?`。旧 epoch 或消失对象返回 null；每次重新解析权威，不把节点/ledger引用交给 UI。

`PlayerObjectDetailView(Object, Description, Mine?, Robot?, Facility?)`：

- Mine：`MaterialId, Remaining, Products: MaterialAmountView[], Prerequisite`。剩余量来自 mine，产物从真实配方链；要求真实加工/保障条件，不教成手动逐机采矿。
- Robot：`Energy, Durability, Capacity, Role, State, Reason, GoalId?, TaskId?, Cargo: MaterialAmountView[]`。健康、实际分工和携带现货均取真实状态。
- Facility：`Type, Built, Connected, Powered, SourceId?, OperatingState, Stored: MaterialAmountView[]`。建成、接线和昼夜运行分开；没有真实连接语义的仓储/着陆器不标“缺电”。夜间已接线显示等待日照，不显示重新付费接线。

身份使用已有 robot Name、mine-iron/mine-copper、lander/build-N。地形 Y 从当前权威地形重新投影，设施半径沿现值，矿点沿现保护半径。

### 工程

`Main.ReadWork() -> PlayerWorkView?`；`Main.ReadWorkHistory() -> PlayerWorkView[]`。

`PlayerWorkView(WorldEpoch, GoalId, TaskId?, Kind, Stage, Status, Name, ReasonCode, Reason, ProgressBasis, Progress?, WorkerIds: string[], Center, Active, ObservedVersion)`。

- Kind 枚举：Build、Connection、Restock、Mine、Recipe、Haul、Level。
- Stage 枚举：Authorized、PreparingMaterials、Travelling、Mining、Processing、Transporting、WaitingForRoute、Levelling、AwaitingPhysics、Constructing、Completed、Cancelled、Blocked、Unknown。
- Status 枚举：Running、Waiting、Blocked、Completed、Cancelled、Failed。
- ProgressBasis 枚举：None、MiningWork、RecipeWork、ConstructionWork、LevellingWork。运输、走路与全目标不能凭空百分比；只有有真实分母时 Progress 非 null。采矿 Work/6，加工 Work/(WorkSeconds*batches)，施工/整平按各自当前工段分母，夹在 0..1。
- ReasonCode 至少含 None、WaitingForWorker、WaitingForStation、WaitingForRoute、WaitingForPower、WaitingForProjection、Unknown、Completed、Cancelled、Blocked。由实际控制流/结构状态决定；未知原因用 Unknown，不从中文字符串反解析或推测坡度/具体障碍。
- CargoWaiting/取货工位失败等必须刷新真实反馈，不能沿用“正在前往着陆器取货”的旧成功理由；不改变路线或重试规则。只证明找不到当前工位候选时，不声称已证明具体地形障碍。
- 历史仅来自当前世界已有生产任务、教学授权回执及仍持有的终态任务，按实际稳定 ID 去重；不制造一个“完整永久工程历史”或新持久化系统。UI 对没有记录的旧工程明确显示已有记录范围。

### 物资位置/预留/加工

`Main.ReadMaterialsDetail() -> PlayerMaterialsView`：`Summary: MaterialStockView[], Locations: MaterialContainerView[], Reservations: MaterialReservationView[], Batches: ProductionBatchView[], ObservedVersion, WorldEpoch`。

- `MaterialContainerKind = Warehouse | RobotCargo | Mine | Worksite | Processing | Consumed | Other`。
- `MaterialLocationAmountView(Id, Name, Quantity, Reserved, Unreserved)`；三个数量仅在同一实际容器内成立，Quantity≥Reserved。Unreserved 为该容器现物未预留量，不代表对某计划可达。
- `MaterialContainerView(Id, Name, Kind, OwnerId?, Position?, Capacity, Materials: MaterialLocationAmountView[])`。
- `MaterialReservationView(GoalId, ContainerId, MaterialId, Name, Quantity)`，取真实 reservation，不能把 goal 的所有物料当预留。
- `ProductionBatchView(TaskId, GoalId, RecipeId, FacilityId, Paid, Stage, Input: MaterialAmountView[], ExpectedOutput: MaterialAmountView[], Progress?)`。预计输出仅未完成且已付生产，不能作为现货相加；已消耗容器不列入库存。
- 顶栏 Stored 为建成 lander/storage 中真实现货（含预留子集）；Available 原字段是 PlanningAvailable，只在明确解释的详情中显示；InTransit 标“机器人携带”；InProcess 标“加工预计产出”。不跨口径求总库存。
- `BuildPlanView` 末尾增加 `EligibleStock: MaterialAmountView[]?` 与 `WorldEpoch`；建设卡的“此计划已有/还需补”由 authority 以提交相同的材料来源资格计算，Gap 同口径。配方预计产出不抵已有，路由未证不冒称全局可达。UI 不自己减预留或递归配方。

## 3. 管理暂停、命令重验与生命周期

`Main.SetPlayerManagement(open: bool, observedEpoch: long) -> bool`：同步单模拟线程调用，只接受当前 game/非 busy 的 epoch；幂等、不翻转手动暂停。`PlayerReadModel` 末尾增加 `ManagementPaused` 与 `WorldEpoch`。

- 管理层打开立即持有独立临停原因。门禁覆盖模拟时间、bootstrap/level工作、健康/电耗、路线等待计时、机器人驱动/物理移动；恢复投影必要重力不被误冻结。不使用 SceneTree.Paused。
- `ProcessPlayerCommand` 分派 tutorial/connection 等早分支前统一门禁：管理层期间拒绝世界操作。管理入口自身及合法生命周期返回不因 gate 死锁。不要把面板关闭实现成 toggle user pause。
- 排队世界意图捕获 worldEpoch；执行时在已有版本/权限检查之外重验 epoch。关闭覆盖层后主按钮重取 authority，双击只落一个目标。
- `Main.QueueCancelWork(goalId, observedVersion, observedEpoch)` 用于明确后果确认。只取消该当前目标，旧上下文/旧 epoch/旧版本拒绝；取消仍沿旧真实释放/保已交付规则。
- 生命周期开始 load/newgame/home/quit 时清管理临停；UI 一旦观察 loading/busy 清临时预览，即使读取失败回旧 epoch 也不复用旧草案。成功 epoch 改变关闭覆盖层、丢弃 transient context；对象按 stableID 重新解析或清选。保既有故障回滚，不把管理页面/选择/镜头写存档。
- 同 epoch 的普通暂停菜单返回保选择/预览；管理关闭与退出 placement 恢复进入前镜头、有效对象和输入模式，按 epoch+stableID 重验。

## 4. 分层 HUD 与真实教学因果

PlayerUI 仅消费上述接口及已有 lifecycle/settings/planning/tutorial 合同，不改模拟或存档。

- 顶部紧凑库内现货/发电每秒/昼夜/真实重要警报/菜单；顶部物资与底部基地物资打开同一管理层。
- 教学目标卡：目标、为什么、下一步/完成条件，可折叠；无教学目标时隐藏。无 pin 系统时不造 pin。
- 浏览默认无空对象板；选机器人/矿点/设施出现真实对象侧栏。建设、整地显式模式互斥于对象卡；左键仅选址，不选机或提交。整地可由 authority 自动选择有能力的筑垒，不要求先选机器人；不改整地数值/规则，也不宣称整地是所有建设必经。
- 建设菜单按既有五蓝图；建设卡主按钮、阻塞反馈固定可见，长成本/路线说明可滚动。显示完整真实成本、资格量/缺口、收益、场址拒因和必要供电。明确“机器人自动准备缺料”。
- 独立活动工程一行摘要，选中/建设/接线改变不使它消失；无活动隐藏。点击进工程管理，详情与取消不常驻整排工具。
- 底部建设/整地/基地物资/工程，科研隐藏。移除九个镜头方向按钮，保实际键鼠映射并在帮助正确说明；不加 M/B。
- 单管理覆盖层，物资/工程标签切换；重复入口关闭同视图。库存按实际物理位置与预留显示，可按物资/位置筛选，关联工程在同层展示；只显示真实已有历史。空态、长列表正常。
- 遮罩 MouseFilter.Stop；键盘门禁包括每帧相机 Input.IsKeyPressed；只顶层接受焦点。管理期间世界禁止选址/提交，内部定位动作先关闭面板再定位。
- Esc 每次只退一层：确认/子详情→管理→预览→局内暂停；Esc 不取消正式工程。普通预览取消不确认；工程取消显示已发生工作和已交付物料保留的后果，并重验目标/版本/epoch。
- 教学说明与帮助按对象→目标→机器人工作→现场/仓库结果解释，不把自动前置工序做成新按钮或调试阶段名称。接线针对具体合法未连接 target 与来源，夜间0/s不重接。
- 用 Control/Container/anchor 与既有 Theme；revision 或节流刷新，不每帧重建控件树。1280×800、1920×1080、1920×1200及100/125/150%交 Codex 实机覆盖；紧凑布局保阻塞与主按钮，镜头世界可选址。

## 5. 所有权与任务图

| 任务 | 实施者 | 独占文件 | 依赖/证明 |
|---|---|---|---|
| A 定义目录与纯合同 | GLM-5.3 | Resources/Core、新目录元数据、Goals/ProductionPlan、material-ledger-tests、production-plan-tests | 本合同；真实 console 合同输出，原配置原字节/数值不变 |
| B 权威投影与管理门禁 | Sol | PlayerContracts.cs、Main*.cs（含新的 Main.PlayerViews/Main.ManagementTests partial） | 公共协议已固定；与 A 分开写；真实 authority 合同与既有生命周期 |
| C HUD/对象/管理层 | GLM-5.3-Flash | PlayerUI/*.cs、tools/player-ui-tests 内必要适配 | B 的 DTO 文件落地且主控核对后开写；A/B 只读；真实 headless UI 检查，图形/原生输入另列 |
| D 可复跑构建/指纹 | Haiku-5.5 | tools/build_macos.py、其局部测试（如需） | 与 A/B 并行；原 TPZ 路径保留，增加本机已安装同版模板的固定指纹入口，不下载依赖 |
| E 集成、验收与交接 | 主控 + 一次独立 Sol 审查 | 本批文档、分支与构建输出；源码修复再次委托对应 owner | A/B/C/D 集成后，最小实际合同/build/import；新候选/draft PR/Codex prompt |

A 与 B 可并行：B 使用本合同 API，不碰 Core/Goals。B 若定义接口需调整先向主控报告；主控冻结后 C 开写，不让 C 修改 PlayerContracts。构建 D 与产品源码完全分文件；不让多个 agent 同时触发 Godot import/build 或覆盖同一个 bin/obj/evidence目录。

D 的安装模板入口验证 `version.txt=4.7.2.stable.mono`、本机已实测 macos.zip SHA256 `e6120d492650ad677c3a50dfded836928671049552e09c41e0850beb425ace8c`，只复制并提取 arm64到本树缓存，不改安装模板。不把此入口冒称原 TPZ 哈希核验。指纹保旧清单兼容，另固定所有实际源/资产（含 PNG、UID、工具/launcher），排除 .git/.godot/bin/obj/export/benchmarks、generated build-info 和私人数据；manifest记录完整身份并在预置临改后恢复原字节。

## 6. 验收与停止规则

主控独立执行，不由实施者自批：

1. 规格：只批1，7/4/5原数值、身份与配置 hash保留；无新依赖/新机制；全部 UI 字段有事实来源。
2. 质量：detached/只读目录、无中文拒因反解析、无错误资源合计/伪进度/夜间重接、无 TODO/debug残留；重要交易/保存/恢复由集成后的独立 Sol 直接审，不调用 review skill。
3. 类型/构建：固定 SDK/Godot，实际 dotnet build 与 headless import；日志 ERROR/异常必须读并报告。
4. 纯合同：沿既有 ledger/planner/native navigation 必要项目，目录有效/非法/扩展计数、守恒/幂等/复制隔离。
5. authority：打开关闭管理时 userPaused=false/true 均保值，管理期间 time/positions/health/ledger/work/waiting不推进，直接 world 命令拒绝；epoch/取消重验、预览零副作用/双提交一次；DTO改副本不改世界，库存/预留/预计产出正确；夜间已接线状态。
6. 回归：既有 tutorial boundaries 与 full-storage（含冷启动续接）实际输出；恢复验证只使用已授权私有副本，未执行不填 PASS。
7. Headless UI 仅证明输入分派/控件状态，dummy图形项 BLOCKED/NOT_RUN准确保留；不得换成“玩家体验通过”。
8. 导出：新输出目录、新app、codesign/arm64/PCK/runtime与源/资源manifest一致，旧包与默认入口不覆盖。GUI与 B002复现交 Codex；D001单列真人无提示轨。

同一步两次失败先保原输出、诊断并重新计划，不无限盲改。所有私有日志/存档/脚本留 job tmp 或新的私有验收目录，入公开仓库仅脱敏证据摘要。交接必须固定commit/app/hash、私有启动入口、真实步骤、预期/失败登记和哪些项未跑。
