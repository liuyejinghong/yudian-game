# D1.2 可持续经营首玩合同 r1

2026-10-08｜基线51c9b632｜已有授权实施、GLM外包、隔离/原生/GUI验收、合格PR集成。Codex持有Main、权威账/提交/恢复/配置/默认应用；工程独占eng-d12-20261008。本合同仅冻结当前消费者，D2/G1/真人与发行不扩签。

## 玩家循环与授权

新档仅着陆器/12机。d12配置iron17、copper5、parts21、cable20、kit8；其余0。首套太阳能、充电、维修（含现场4结构件储备）、加工及3段连接可由有限启动物资建起。加工前保护尚缺首套及连接预算；提前重复/非必要建设不挪用保护库存。kit没有配方，不自产。d11配置文件保留供旧schema2及旧检查使用。

五种现有蓝图仍由玩家选位置/朝向，单活动目标。确认授权有限已知矿点采集、实际运输、四配方加工、目标整平/建设及正常保障；缺前置设施明确请求玩家选址建设/连接，禁止无权限替选位置或绕过缺口。已接受不保证完成；模型/搜索失败不判基地死局。提供者显示“本地规则 r1”，E14模型未提供时NOT_RUN。无通用规划器、多目标竞争或新资源。

两个后续方向：仓储（iron4、parts4、kit1）增加100单位实物容量且可作真实取货源；第二太阳能（iron4、parts4、cable2、kit1）增加日照4/s供能，连接仍限12m/付费线缆，位置影响保障和搬运。「连接电缆」对已选真实设施/太阳能展开同一净缺口生产链，后续缺线缆不能停在旧库存不足分支；目标保存稳定facility/source引用，前置生产后才冻结接线工段。首套后着陆器无铁/铜/结构件/线缆；仓储须24铁矿→12铁→4结构件+4建筑铁，太阳能另需2铜矿→1铜→2线缆。只生产净缺口，维修站现场储备不参与扩张；已产/在途货物保留。补维修耗材入口把结构件真实送至既有维修站，不赠资源。

## 工程消费 API / 单写者

GLM-A独占prototype/scripts/Goals/ProductionPlan.cs、tools/production-plan-tests/**，纯.NET，namespace Yudian.Goals。
`ProductionStep(string Kind,string Material,int Quantity)`（mine数量=离散矿单位，recipe数量=配方批次）；`ProductionPlanResult(bool Feasible,string Reason,ProductionStep[] Steps)`；`ProductionPlan.Create(Dictionary<string,int> demand,Dictionary<string,int> available,RecipeDefinition[] recipes,Dictionary<string,int> remainingOre)`。
输入MaterialLedger.Materials，数量0..100000，配方四个且无kit产出。消费可用库存/有归属在途（调用者先合并，另一目标在途不合并），多输入按每批投入/输出向上取整；完整模拟预验有限矿量/无来源/循环，任何不可行返回空Steps和中文原因，不部分执行。成功有限拓扑顺序，复用中间余料，不重复承诺；单返回最多128步骤，不需通用图框架。每种最终需求净缺口为max(0,demand-available)。Main每次真实交易后重读再规划，分批矿<=8、配方<=4；GLM不动世界或账本。

GLM-B独占PlayerUI/**、tools/player-ui-tests/**。主控新增`DevelopmentReadModel(bool Enabled,string Provider,string Goal,string Stage,string Need,string Reason,string Choices,string Mines)`，public `ReadDevelopment()`。UI仅展示该DTO；原QueueBuild/PreviewBuild仍权威处理缺口。public QueuePlayerAction新增`restock`（真实补维修结构件）。不得计算成本/矿量或改保存。建设按钮文案表达有限缺料前置授权；两方向收益/净缺口/等待原因可见。1280x800可操作，镜头/旧UI保留，空选择/双确认/暂停/取消/读取验证。暂时可编译的DTO由主控提供。

## 权威交易和保存

MaterialLedger新增显式MiningReceipt（稳定operation/site/material/quantity/baseVersion/appliedVersion）与RecipeReceipt（operation/recipe/configVersion/batches/input/output）。同ID不同负载拒绝；转移/转换/采集均先全量校验副本，矿量与现场货物和逻辑地形在同一simulation线程采纳。地形Commit后即使Bind失败，不能重新发矿；逻辑回执/账本采纳后只能从Current恢复投影。暂停/加载物理屏障继续沿用，所有执行端重验权限/取消/版本/贴地和真实取卸站。

账本对账严格`Totals = Initial + mining receipts + recipe output - recipe input`；spent仍仅建筑/电缆/维修；配置/配方版本/有限矿初值与矿剩余/receipt一致。加工输入先进稳定batch容器（物理端点=加工设施），无电/容量不得有效作业或产出；共享太阳能按既有保障优先后给加工，真实功率预算不重复分配。取消不开始新前置，保输入/载荷/完成产物；重试同批不重扣。加工成果需驮运取出送库，库/现场/货舱只有同一账本事实。

生产开始前退役旧终态build/level对象（保留设施/历史交易）；建设patch在前置结束后才冻结。schema3新槽d12-player-v3.json完整保存目标/授权/生产阶段/矿量/批次/事务/现场/载荷/健康/供电/进度。schema2旧槽可显式读取，候选配置校验不先更改当前世界，采纳/回滚同带配置身份；另存schema3不覆盖旧档。原schema1模式保留。新增验证跨进程矿提交后、实运中、加工投入后、产出后、建设/保障中；重复读取无新增账/量；坏档保当前世界与有效档。

## 美术冻结接口（无需等工程完工）

六资源稳定ID：iron_ore铁矿、copper_ore铜矿、iron铁料、copper铜料、parts结构件、cable线缆；kit仅有限套件沿用当前表示，不在六资源自产链。数量单位为离散物料单位；单件模型表示1单位，批量模型/堆表示可视聚合而非新增数量。权威items字典给出每种数量/total/capacity；不能从模型数、动画或箱子取模反推库存。

消费约定：素材候选可放`prototype/assets/resources/d12-r1/{ID}.glb`与manifest.json（源/正式素材由美术树持有；工程只拷贝消费）。缺素材先用明确颜色/中文数量临时表示。米制、Y-up，模型本地原点底面中心，前向-Z保持既有tuoyun/processor manifest和源轴约定，不擅旋设施世界朝向。
- Cargo：驮运同账本cargo:Robot_Tuoyun_N，总容量8。既有loaded/empty只作状态，新增六资源具体表示放车辆局部货台约x±0.85、z±0.75、y0.55..1.35；不得超车体/遮车头。混载按items显示，最多4个视觉聚合，数量由UI明示。具体资源显示时隐藏旧封闭CargoBox，empty保持无货；不改车辆实体、容量或卸货端点。
- 仓储：现有storage实例容器100，不预装免费货；堆包络在设施半径内、避开四个真实外部取卸站（radius+bodyRadius+1.2m），状态empty/stocked/full来自items与capacity。
- 加工：batch容器属于稳定processor实例，输入/输出均权威账；出料展示锚点采用现F02 output_socket局部(0,0,2.2)，锚点相对包络x±0.6、z±0.4、y0..0.8；该锚点只做展示，工程不硬贴装饰端口作卸货点。idle/working/no_power/waiting_input/output_full由真实阶段/功率/库存，保存恢复重新绑定，不凭动画结算。
- 矿点：已知mine-iron(-14,10)、mine-copper(1,17)，各初始96单位；开挖半径2m，每最多8单位有效6秒，深度随累计数量从当前初始高度下降最多1.2m。机器人施工站在改动包络外；现场产物同矿容器100。完整枯竭/阶段/产物据权威receipt；不能只换贴花。

真实消费样本：首套后lander{kit:3}，维修现场{parts:4}；首次采铁8后mine-iron{iron_ore:8}；取货后cargo{iron_ore:8}且矿现场0；加工4批铁投入batch{iron_ore:8}→完成batch{iron:4}，再实运回库；4批parts投入iron8→parts4；铜1批投入2矿→copper1，再cable1批→cable2。任何资源都有同ID但不同物理归属，不以外观猜地点。
