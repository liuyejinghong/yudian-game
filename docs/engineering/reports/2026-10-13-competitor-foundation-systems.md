# 《余电》物资、地形、建造与科技：竞品基础框架研究

2026-10-13｜所有者授权保存前轮结论并研究竞品系统。本文为一手资料与公开源码研究，没有新试玩、没有修改游戏、没有批准新机制实施。

## 结论

四项问题应该围绕同一组世界事实解决：**内容定义说明“是什么”，世界状态记录“现在怎样”，规则与命令决定“能做什么”，执行把计划变成真实变化，保存恢复这些关系。** 这是本文依据多项来源提出的《余电》候选结构，不声称所有竞品用了完全相同的内部框架。

- 物资：借 Factorio 的原型定义与库存实例分离，补统一物资目录；复用现有守恒账本，不重建所有物流。
- 地形：借 OpenTTD 的高度、坡度、地块属性与建设命令关系，以及 Surviving Mars 的整平工程代价；保留现有连续高度，把建设和通行的规则补清楚。
- 建造：借 Factorio 的建造意图与实体区别、Anno 的规划与实际建造区别；保留真实供料施工，把工程生命周期和建成后的运营条件分开。
- 科技：借 Factorio 的前置、成本、效果定义与研究状态，以及 Surviving Mars 的研究路线、探索与能力回报；研究进度独立于教学进度，解锁必须改变后续可执行行动。

前两轮分类与系统核对已保存在[结论归档](2026-10-13-tutorial-r1-module-and-system-triage.md)。原审核确认设计 P0=0/P1=1/P2=0；四项系统能力缺口不因竞品有该能力就自动新增确认 P0。基础建设优先级与已发生体验损害的严重度分别记录。

## 1. 问题、范围与版本

本轮研究的是统一物资定义和管理、权威地形及可预测规则、建造机制完整性、科技研究与成长解锁。此前 HUD、术语与目标委托的理解问题仍保留，但本轮进一步查结构和系统连接，不以改善排版代替基础能力建设。

| 对象 | 本轮固定范围 | 证据能证明什么 |
|---|---|---|
| Factorio | 官方 API 文档 **2.1.21**，固定版本 URL，访问 2026-10-13 | 内容原型与运行时公开契约、数据生命周期；不能证明闭源 C++ 引擎的全部内部组织 |
| OpenTTD | 官方 tag **15.3**，commit `14ec60f248547d4d062a1160f0fc26d742319888` | 固定版本具体高度、坡度、地形命令与地图保存实现；不是《余电》的玩法或 3D 技术路线 |
| Surviving Mars | Relaunched 的 2025 官方物流/科研日记；2019 原版 Armstrong 整平公告 | 官方机制声明；原版整平规则不外推为新版全部实现 |
| Anno | 1800 的 2018 PC 发布前开发博客，以及 2023 Console Edition 官方说明 | 规划、岛内库存共享、运营条件；不是 Anno 2205 或现行 PC 补丁的实测 |
| 《余电》 | 源码基线 `953573f` 与原教学 r1 未提交工作树的只读核对 | 现有底座及明确限制；源码不等于固定 r1 包运行证据，详见归档 |

OpenTTD tag 与 SHA 已由[官方 GitHub ref](https://api.github.com/repos/OpenTTD/OpenTTD/git/ref/tags/15.3)独立核对。Factorio 引用页头确认为 2.1.21，未把版本号从项目日期推算出来。

证据分三类：**公开实现**（固定源码）、**公开契约/机制**（官方 API 和开发者说明）、**本文建议**（对《余电》的结构推导）。闭源游戏内部是否有某个 `ItemManager`、ECS、事件溯源或事务平台，没有证据就不下结论。

## 2. 竞品怎样组织这四类系统

| 模块 | 已核一手事实 | 对《余电》的直接启示 |
|---|---|---|
| 物资与配方 | Factorio 的 ItemPrototype 给名称、堆叠等定义，RecipePrototype 按物资名声明投入和产出；LuaInventory 是持有实际堆叠的运行对象。SM 的 depot 有位置和期望存量，运输有服务范围。 | 类型定义与库存事实分开；全局总量不能代替位置、预留、在途和运输条件。 |
| 地形与规则 | OpenTTD 存角点层高，由邻格推导坡形；类型与高度分列，改地检查邻接、地图边缘和受影响对象。2019 SM 整平先评估土方，再执行搬运；坡道有通行用途。 | 高度只是输入，还需要地形属性、建造/通行能力和改造代价。 |
| 规划与建造 | Factorio ghost 引用拟建原型，与真实实体转换有明确接口；Anno 蓝图不运行、规划零成本，之后才实建。SM 普通设施有建设/供能要求，depot 免费即时放置是明确例外。 | 计划、已授权工程、已建成设施和正在运行设施不能混成同一个状态。 |
| 科技与成长 | Factorio 定义前置、研究成本/触发条件、效果，运行层另存研究完成与进度。SM 有研究路线、成本、队列，探索能提供研究点或揭示科技，能力如班车连接科研。 | 科技是独立规则系统，产出要影响配方、设施或真实能力；教学完成不是研究完成。 |

### 2.1 物资：定义目录与库存实例是两层

Factorio [ItemPrototype](https://lua-api.factorio.com/2.1.21/prototypes/ItemPrototype.html)的公开定义包括 `name`、`stack_size`、`place_result` 等。`stack_size` 明确是一个库存格可存的同名物品数量；`place_result` 引用可用此物品建造的实体原型。这里可证明的是公开内容引用用名称，不据此猜内部是否还有数字索引。

[RecipePrototype](https://lua-api.factorio.com/2.1.21/prototypes/RecipePrototype.html)用 `ingredients` 与 `results` 声明投入/产出，例如物品或流体的类型、名称与数量。配方定义不是一堆具体物品实例。[LuaInventory](https://lua-api.factorio.com/2.1.21/classes/LuaInventory.html)提供计数、查询堆叠、插入、移除等运行接口，操作返回实际数量；库存可属于实体、玩家等，不能从一个可空的 `entity_owner` 字段推断所有库存的内部组织。

[数据生命周期](https://lua-api.factorio.com/2.1.21/auxiliary/data-lifecycle.html)区分定义阶段和控制阶段：mod 在 `data.lua`、`data-updates.lua`、`data-final-fixes.lua` 登记或修改原型；进入控制阶段后原型不可再修改。公开运行接口的 `insert` 是脚本改库存能力，本身不证明物资通过采矿或制造获得，更不提供《余电》跨容器守恒的替代方案。

SM:Relaunched 的[物流与资源日记](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/logistics-and-resources)补上世界约束：depot 可设置期望资源数量，无人机接受指挥器派单并受服务区限制；班车在全地图尺度运输，但需供能、燃料和相应设施。物资存在于哪里，与统计总共有多少，是不同问题。该日记不公开物资注册表或库存类实现。

### 2.2 地形：基准高度、坡形与地表类型不能混为一个整数

OpenTTD 的[TileHeight](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.h#L29-L33)存每格北角高度，[地图结构](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/map_func.h#L28-L40)将类型和高度分列。[GetTileSlopeZ](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.cpp#L23-L68)读取邻格角点，计算最低角与坡形。因此两个格子自身基准高相同，邻角不同就可有不同坡形；同高也可以有不同地块类型。

它采用非负整数绝对层高，terraform 另有相邻角高差约束。这不否定所有者用 `0/-1/1` 表达相对基准高程的要求，也不说明《余电》必须改用 OpenTTD 的层级或坡形规则。现有米制连续高度同样可以提供清楚的基准、坡度与高差。

[TerraformTileHeight / CmdTerraformLand](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L99-L303)先建立候选高度，计算邻接调整和受影响地块，检查高度上限、边缘、桥隧等条件，累计角点改造与地块清理成本；真实写入由执行分支控制。其成本不是完整土方体积物理模型。[CmdLevelLand](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L315-L387)还可能部分完成，不能把 test/execute 理解成自动整图回滚。

2019 SM 的[官方 Armstrong 公告](https://store.steampowered.com/news/posts/?appgroupname=Surviving+Mars&appids=464920&enddate=1559147206)则说明了改地的经营意义：整平建立施工全息项目，评估需要或多余土方，由无人机送入或移走废石；Ramp 连接两块不同高程的平地形成可步行地面。补丁说明 Flatten/Ramp 消耗大量无人机时间并可能需要或产出废石。本轮已直接读取该公告正文；这是 **2019 原版**依据，新版 RC Dozer 的全部更新规则未核。

### 2.3 建造：意图、执行和运营各有事实

Factorio [LuaEntity](https://lua-api.factorio.com/2.1.21/classes/LuaEntity.html)公开 ghost 对目标原型的引用，以及 `revive` 将其变成实体的接口。`revive` 是脚本 API，不证明一定经过正常机器人供料；`item_requests` 也不能当作建筑本体成本清单。其 `order_deconstruction` 与 `cancel_deconstruction` 可说明“安排拆除”和“取消尚未完成的安排”有区别，不能据此声称已拆建筑可无成本复原。

Anno 1800 的[2018 官方开发博客](https://www.anno-union.com/x-marks-the-spot/)标题是《This belongs in a museum!》，正文同时介绍博物馆与蓝图，URL 和标题不同不代表是另一份证据。蓝图显示为轮廓、放置不耗资源或钱、实际建设前不运行，并只允许当时已通过自然进程解锁的建筑。该资料属于发布前说明；[2023 主机版官方提示](https://news.ubisoft.com/en-us/article/6z7wAll2mXTdQtp79h2B2x/anno-1800-console-edition-4-tips-for-getting-started)再次说明蓝图不生产、不耗资源或人力。运营人力不足会使生产放慢、经营成本上升，属于建成后的规则。

SM 的免费即时 depot 是[官方明确的类型例外](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/logistics-and-resources)，不能将“点一下就出现”一概判为坏设计。对《余电》应检查的是该类设施承诺的成本和作用是否真实兑现。

### 2.4 科技：定义依赖与研究状态分离

Factorio [TechnologyPrototype](https://lua-api.factorio.com/2.1.21/prototypes/TechnologyPrototype.html)的 `prerequisites` 给前置科技，`unit` 给物资/时间成本，`research_trigger` 提供其他研究触发方式，`effects` 可按配方名称 `unlock-recipe`。[LuaTechnology](https://lua-api.factorio.com/2.1.21/classes/LuaTechnology.html)持研究完成、等级、累计进度等状态；[LuaForce](https://lua-api.factorio.com/2.1.21/classes/LuaForce.html)持当前研究、研究进度与队列。公开 API 可以证明定义和状态分离，不能据此宣称其内部研究图算法或存档数据库结构。

SM:Relaunched 的[研究与探索日记](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/research-and-exploration)说明七条研究路线、每项研究点成本、最多五项队列；科技位置每局可重排，但有早晚阶段限制。研究通常开放路线下一项，异常探索可能揭示更深科技；研究点来自赞助、里程碑、探险、异常、实验室或地球外包。这是一种不同于固定前置图的成长组织，不能把两款都概括成同一个静态科技树。

科技价值体现在能力改变：物流日记明确班车可由特定开局条件获得，否则经物理科技树解锁。研究没有仅停留在 UI 节点。

## 3. 《余电》建议的最小框架

以下均为建议，不是已实施或最终冻结 API。不增加独立服务器、数据库、庞大 ECS 或通用工作流平台。先用现有 C# 数据和规则形成清楚的职责。

| 层 | 主要事实 | 边界 |
|---|---|---|
| 内容目录 | 物资、配方、建筑、地表规则、科技的稳定类型 ID 和定义版本 | 载入时检查重复、引用、数值和前置；之后供查询。开发期增删改查定义不等于运行中任意热删除。 |
| 世界状态 | 容器、矿量、货物、地形区域、工程、设施、研究状态的实例 ID | 模拟拥有真实数量、位置、阶段与归属；避免 UI/模型各复制一份可写经济。 |
| 规则查询与命令 | 预览、授权、执行、取消、失败原因、相关版本 | 共用规则；预览不改变世界，执行前重验，真实效果只结算一次。 |
| 执行与表现 | 机器人任务、场景实体、HUD/详情 | 机器人实际工作；场景/UI解释事实，不凭动画发矿或扣健康。 |
| 保存 | 定义版本、稳定实例引用、已发生与未完成事实 | 恢复跨模块关系，不重复取货、生产、消费或科技效果。 |

候选内容关系为：**物资定义被配方、建筑成本和科研成本引用；科技效果开放配方/建筑/能力；建筑实例通过工程和运营规则使用物资与能源；地形规则约束工程和路线。** 实际需要的关系才加入，不先为未来功能预建空接口。

### 3.1 物资目录：补定义层，保账本

现有 [MaterialLedger](../../../prototype/scripts/Resources/Core/MaterialLedger.cs)已经持容器、预留、交易与守恒；[BootstrapConfig](../../../prototype/scripts/Resources/Core/BootstrapConfig.cs)已有建筑和配方定义，但验证器固定物资集合、建筑数量和四条配方。[ProductionPlan](../../../prototype/scripts/Goals/ProductionPlan.cs)也限定配方数量、单输出和每种产出唯一来源。扩展不能只改一张名单，还要一起核对这些消费方。

建议最小物资定义字段：稳定 `MaterialId`、名称/说明、类别、实际采用的载荷或容量计量、视觉引用。堆叠、质量、危险品或不同品质只在对应玩法有需要时加入。配方另有稳定 `RecipeId`、投入/产出物资与数量、工时、设备能力与耗能要求。

定义通过目录查找，库存持物资 ID 与数量；货物批次/容器/交易另有实例身份。无需给每一块可互换矿石独立 UUID，也不能用物资类型 ID 代替货物归属。

管理器最小操作是定义的登记、查询、修改前验证、删除引用检查，以及目录载入验证。定义变更涉及已有档引用时需要迁移或明确拒绝加载，保持旧有效档；运行库存仍只能经真实采集、配方、转运、消费或明确获授权的测试操作变化。

### 3.2 地形规则：保连续高度，补用途与代价

复用 [TerrainSnapshot](../../../prototype/scripts/TerrainData/TerrainSnapshot.cs)的米制连续高度和 [TerrainRegionState](../../../prototype/scripts/TerrainCommit/World/TerrainRegionState.cs)的版本提交。显示可用相对基准高度/坡度，但权威值、渲染、碰撞、导航和建设必须一致。

建议规则查询回答四件事：这里是什么地表、机器人怎样通过、建筑需要怎样的基底、修改地形会影响什么。高度、坡度、矿点/设施/保护范围是不同维度，不能一个数字同时代替所有属性。

整平计划应选目标基底高程，评估影响范围、高差、清理对象和工作量。先证明“地形差异会改变允许行动或真实作业成本”，不继续用全场统一高程 0 与固定工段代替不同场址的工程含义。土方体积可用现有网格做简化估算；是否加入可搬运土方资源，是另一个玩法决定，本轮不自动采用 SM 的废石系统。

自动整平可以作为已授权建设前置；独立整平应有可解释用途。允许某些坡地自动处理、某些设施适应坡地，不为了保留按钮而强迫所有地块先手动整平。

### 3.3 建造：工程状态与运营状态分开

现有零承诺预览、确认预留、真实补料/运料/施工、取消保地形和货物应保留。[自举合同](../contracts/d11-bootstrap-r1.md)已定义这些底线。不能把本轮建议写成需要重新发明这条链。

建议最小工程事实：`ConstructionId`、建筑类型、地点/朝向、所用地形与经济版本、授权范围、所需/已预约/在途/现场/已消费物资、前置工序、执行者、阶段与结构化阻塞原因。阻塞要记录具体当前工序原因；不要将下一份草案的拒因当成正在执行工程的原因（对应原 B002）。

两个状态轴分别维护：工程从规划/授权/供料/场地准备到施工、落成或取消；设施从已建成但未接通，到满足运营条件、运行、维护或停机。研究门控、选址许可和材料可得是不同条件，不把“解锁了”当作“立即可建”。

持久规划、多工程队列、优先级、升级和拆除按玩家任务逐项定义。可以暂保单活动工程并保存下一份计划；只有实际需要并行建设才扩充资源竞争与调度。拆除若采用，要说明成本、产物、连线和地形如何处理，真实回收运输，不瞬间退回全部投入。

### 3.4 科技：研究数据驱动能力，教学只做教学

建议固定前置图作为首个可验证方案，暂不复制 SM 随机研究位置。最小科技定义：`TechnologyId`、前置 ID、研究成本/条件、完成效果；世界另存正在研究、进度和已完成集合。效果用受支持类型引用配方、建筑或设备能力，不运行任意内容脚本。

研究投入需要本游戏可获得的来源和真实执行者；不直接照搬殖民者实验室、地球赞助或免费点击。先选一条能通过机器人勘测/生产/实验等已设计行为完成的短研究链，再决定内容规模。这里列的是候选方向，不新增机器人能力或资源来源承诺。

研究完成后可获得的新配方/建筑应能实际使用，并改变下一次经营选择。教学步骤可以引导使用它，但不能给研究完成集合直接填值；教学跳过、重看与科研进度是两个概念。

科技前置应检查缺引用和循环；经济/配方图要区分不可自举前置与合法循环工艺，不能对未来所有配方循环一律判死锁。有限 kit 若仍无新来源，科技界面也不能假装解决扩张：必须有可取得材料路径和真实建设回报（关联 D003）。

## 4. 四模块连接后的最小验收链

研究结果应进入同一个小玩家流程，而不是做四个互不连接的管理器演示。以下是未来实施/复验条件，本轮没有执行：

| 验收 | 要证明的结果 |
|---|---|
| 新内容接入 | 在定义层新增一种实际需要的物资、一条配方、一类设施和一项科技后，引用校验有效，库存/计划/UI能识别；普通新增无需修改各处名称分支。实际新增内容需另行设计。 |
| 科研改变能力 | 初始不可用的配方/设施有清楚原因；合法研究真实消耗投入，完成后可用于下一工程，不是只点亮科技节点。 |
| 地形改变决策 | 可比的两个场址具有不同基底条件/改造代价；改地确实改善一种建设或通行条件，预览、执行和结果一致。 |
| 缺料目标贯通 | 下达目标后，矿→加工→运输→场地工作→施工→接通/运行用同一世界状态，成本与能力变化可核。 |
| 中断与取消 | 在研究、运输或施工中保存/恢复，进度和货物不重复结算；取消释放尚未执行承诺，保已发生改地与现场货物，并可正常再利用。 |
| 人能理解 | 新执行者无外部答案，能说清矿/库存用途、场址限制、研究回报、谁执行及为什么阻塞。工程测试不能关闭 D001。 |

这些条件保护原 L001–L004 成果，不将“复制竞品实现”当验收，也不把自动化改成所有工序都由玩家逐机点击。

## 5. 建议的推进顺序与取舍

1. **先冻结最小内容目录和跨模块引用。** 覆盖现有七类物资、四配方、五建筑；保留全部数值与守恒，再明确新增内容如何接入。目录和保存版本是建造与科技的共同输入。
2. **地形规则与建造生命周期同批贯通。** 明确场址条件、目标高程、工作代价和失败反馈，在真实工程中验证，不单独交一个高度管理器。
3. **接一条有实际回报的科研链。** 与新生产/建设能力共同交付，同时解释 kit 和后续目标来源；主游戏成长不能由一张科技面板代签。

这不是“先写完所有底层再玩”。每批都应接进现有主游戏，以玩家选择和世界后果验证。保持目前有效的账本、改地提交、实际机器人工作和成果保护；只修改真实阻碍扩展或破坏玩法的部分。

## 6. 未核、取证限制与交付检查

- 未试玩 Factorio、OpenTTD、Surviving Mars 或 Anno，文档声明不证明手感、节奏和真实玩家理解。
- Factorio 公开 mod API 不等于公开整个引擎源码；脚本 `insert/revive` 也不证明正常玩法的物料链。
- OpenTTD 只核本报告涉及的命令与地形路径，不对整个引擎作“绝无回滚”之类全局否定。
- SM 新旧版、Anno PC 发布前与主机版分列。新版 RC Dozer 全部规则、Anno 2205 底层、闭源注册表和序列化格式未核。
- 本研究没有实现物资目录、地形新规则、建造扩展或科技树，也没有改变原审核问题状态、正式任务表、默认入口和正式存档。
- 原审核与教学 r1 源码还在原工作区；本次归档保留路径和文件指纹，不上传私有存档或整个证据目录。

主控已独立读取 Factorio 物资/配方/科技/库存/ghost/生命周期，OpenTTD tag/坡形/terraform/地图保存，SM 物流/科研与 2019 整平，Anno 蓝图原始 HTML 与 2023 官方说明。文档检查结果在[本轮计划](../research/2026-10-13-foundation-systems/PLAN.md)记录；不是游戏验收。

## 7. 阅读入口与主要来源

本地分专题报告：[Factorio](2026-10-13-factorio-foundation-research.md)、[OpenTTD](2026-10-13-openttd-terrain-command-research.md)、[SM 与 Anno](2026-10-13-mars-anno-system-research.md)。主报告对具体证据的范围限定优先，分报告不得将脚本能力当正常玩法材料来源。

主要一手来源（访问 2026-10-13）：

- [Factorio 2.1.21 数据生命周期](https://lua-api.factorio.com/2.1.21/auxiliary/data-lifecycle.html)
- [ItemPrototype](https://lua-api.factorio.com/2.1.21/prototypes/ItemPrototype.html) / [RecipePrototype](https://lua-api.factorio.com/2.1.21/prototypes/RecipePrototype.html) / [TechnologyPrototype](https://lua-api.factorio.com/2.1.21/prototypes/TechnologyPrototype.html)
- [LuaInventory](https://lua-api.factorio.com/2.1.21/classes/LuaInventory.html) / [LuaEntity](https://lua-api.factorio.com/2.1.21/classes/LuaEntity.html) / [LuaTechnology](https://lua-api.factorio.com/2.1.21/classes/LuaTechnology.html) / [LuaForce](https://lua-api.factorio.com/2.1.21/classes/LuaForce.html)
- [OpenTTD 固定源码：角点坡形](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.cpp#L23-L68) / [地形命令](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L99-L387) / [地图保存](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/saveload/map_sl.cpp#L24-L121)
- [SM:Relaunched 物流与资源](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/logistics-and-resources) / [科研与探索](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/research-and-exploration)
- [2019 SM Armstrong 官方公告与 Dev Diary #5](https://store.steampowered.com/news/posts/?appgroupname=Surviving+Mars&appids=464920&enddate=1559147206)
- [Anno 1800 2018 官方蓝图介绍](https://www.anno-union.com/x-marks-the-spot/) / [2023 Console Edition 官方玩法提示](https://news.ubisoft.com/en-us/article/6z7wAll2mXTdQtp79h2B2x/anno-1800-console-edition-4-tips-for-getting-started)
