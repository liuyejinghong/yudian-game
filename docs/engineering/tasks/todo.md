# 产研 TODO

更新：2026-10-08；主控Codex。历史PR33/34的Main地形与固定整平接受保留；本批以main259a8bf冻结D1.0，普通镜头/选择/真实整平/低保真与最小磁盘保存已在新默认包接通。D1.1自举建设现从main718905d进入隔离实施；导航/账本/建设/保障与schema2已实现并独立复验；975916d修复包九进程、同一真实存档的鼠标保存/重开/继续和正常入口通过；根应用已更新为D1.1。当前默认r5已完成镜头鼠标/按钮/滚轮入口复验；物理触控板与持续按键手感未签。D1.2仅做接缝预检，未派实施；真实采矿/加工/有后果的后续选择与本地候选尚未交付。稳定性能继续未完成。

按[联合里程碑](../../roadmap/milestones.md)细化[九模块与D1—D3](../module-roadmap.md)。PR38/39/40是此前文档/TODO/编排整理；随后所有者明确批准D1.0实施和新美术项目会话，本轮已运行、重新导出并更新本机应用。实际证据及边界见[D1.0复验](../reports/2026-10-07-d1-player-verification.md)。

本批已实现并集成 **D1.0 可操作场地**：普通镜头/选择/合法区域预览、真实整平与取消、所需低保真接入、统一暂停和最小磁盘保存，最后由 Codex 在同一默认应用验收。GAME-* 仍是完整模块父票；下列 C01/C02/E01—E04 的 D1.0 行是其已执行子片，不代表整包 READY。路线 DT-* 是规划 ID，正式状态只在本表；后续包映射见表后。

| ID | 优先级 | 状态 | 依赖/解除条件 | 拟执行槽 | 交付与接受条件 | 来源/证据 |
|---|---|---|---|---|---|---|
| CAMERA-01 | P0 | ACCEPTED | r5实际鼠标按钮/滚动、暂停/边界复位、保存关窗重开后镜头通过；正常入口已切r5，原档不变；仅接受鼠标入口，触控板/真人按住键盘手感未签 | GLM UI；Codex复现/集成/实际输入验收 | 缩放/旋转/平移可见，新档/暂停/读档后可用；不误下工程命令，操作可发现；原设备根因不因合成手势通过而结论化 | [返修票](CAMERA-01.md) / [复验](../reports/2026-10-07-camera-verification.md) / GAME-UX / DT-E01 |
| C01-D1.1 | P0 | ACCEPTED | 975916d规则与恢复独立审查；原生与最终包真实窗口通过 | Codex | 冻结有限自举账、设施实例、路径/共享预约、分离健康、schema2；首套投入安全证明，数值为可调探针 | [执行合同](../contracts/d11-bootstrap-r1.md) |
| C02-D1.1 | P0 | ACCEPTED | r3同包九进程/签名与真实鼠标保存/重开/继续通过；默认入口已更新，仅签工程子片 | Codex | 从仅着陆器实建首套保障/加工，真实运输/两轮工作保障/保存重开；未测项明确，不以子件通过结项 | [本批证据](../reports/2026-10-07-d11-bootstrap-verification.md) |
| E05-D1.1 | P1 | ACCEPTED | 14项账本、真实运输守恒与同包中断恢复通过；C02工程收口 | Codex账本 | 离散库存/载荷/现场/消耗守恒，预约不扣料，自己取消，坏档拒绝；当批保存 | [执行合同](../contracts/d11-bootstrap-r1.md) |
| E06-D1.1 | P1 | ACCEPTED | GLM78项纯检查；Main绕行/围挡阻塞释放、读档与撤障重试通过；双机竞争另验 | GLM导航；Codex原生/占用 | 代理在场内、绕障/窄口/坡度拒绝、地形/设施版本失效，真实到站，共用工位及有界冲突 | [导航检查](../../../tools/navigation-tests/README.md) |
| E07-E09-D1.1 | P0 | ACCEPTED | 单活动工程/有限出力/真实供电保障与两轮阈值样本通过；未签自然平衡 | Codex规则；GLM UI | 实取卸货/设施能力/付费电缆/能量耐久分离；取消重试、夜间安全等待、两轮保障、坏档不破坏当前世界 | [执行合同](../contracts/d11-bootstrap-r1.md) |
| E12-D1.1 | P0 | ACCEPTED | 同包四类跨进程恢复及真实窗口通过；角点误拒绝修复，禁用碰撞体反例仍拒绝 | Codex | 取货后/落成前/维修中跨进程恢复，无重复交接/耗料，保存保上一有效档；所有新增事实同批交付 | [执行合同](../contracts/d11-bootstrap-r1.md) |
| PLAYER-SLICE | P0 | IN_PROGRESS | 先 C01-D1.0 与本批应用范围；保留 LEVEL-JOB/MAIN-GROUND/APP-START | Codex集成/实机；GLM模块实现 | D1.0 先交可操作场地；D1.1 自举建设，D1.2 缺料经营与至少两种有后果的发展选择；D1/D2 按版本单独验收，不以固定整平签经营首玩 | [模块/版本框架](../module-roadmap.md) / [产品§14](../../product/product-definition.md#144-内部验证顺序) |
| GAME-UX | P0 | IN_PROGRESS | D1玩家命令/世界查询与资源/目标反馈合同 | GLM UI/输入；Codex接入 | 镜头/选择/预览/目标看板与干预，显示真实原因；主场景与实际应用可操作，不只UI样板 | [模块框架](../module-roadmap.md) |
| GAME-WORLD | P0 | IN_PROGRESS | D1.0 复用已接受单区域；C01/E02 冻结合法范围与已知视图 | Codex权威状态；GLM确定子件 | D1.0 有限选区，D1.2 已知矿源；D2 获准实际勘探，D3 有限区域扩张；矿点空间/情报归 WORLD，矿量归 RESOURCES | [模块框架](../module-roadmap.md) / [产品§5](../../product/product-definition.md) |
| GAME-RESOURCES | P1 | IN_PROGRESS | D1.1 C01 物料/归属/交易合同；D1.0 不需先实现账本 | Codex账/提交；GLM冻结规则子件 | E05/E07/E10 真实库存/载荷/预留/在途与净缺口；建造/维修同账结算，幂等守恒；D1.1 起新增事实当批保存 | [模块框架](../module-roadmap.md) |
| GAME-BUILD | P0 | IN_PROGRESS | D1选址/成本/目标/执行接缝，已有LEVEL-JOB | Codex工程状态；GLM适配 | 玩家设施建设、必要整平、材料与工序、建成能力真实改变；取消保留已发生结果 | [模块框架](../module-roadmap.md) |
| GAME-ROBOTS | P1 | IN_PROGRESS | D1.0 原生订单/到场复用；D1.1 E06 有限路线/工位合同 | GLM执行子件；Codex协作/接入 | D1.0 有限合法站位与直线受阻拒绝；D1.1 路线、运输和服务位共用真实运动；D2 竞争/变化，不以固定巡逻代替调度 | [模块框架](../module-roadmap.md) |
| GAME-SUPPORT | P1 | IN_PROGRESS | D1.1 物料/供能/服务位接缝；v0.4；救援未定只停对应子片 | Codex关键状态；GLM确定子件 | E09 从自举即纳入耗电/磨损/供电/回充/维修；D2 E17 动态预算与真实送回；充电不修耐久，维修不赠电 | [模块框架](../module-roadmap.md) / [保障](../../product/energy-return-and-rescue.md) |
| GAME-GOALS | P1 | DRAFT | D1.2 E11 有界真实目标链；E14 本地候选从 D1.1 合同试验起步 | Codex计划/权限；GLM纯适配/harness | 净缺口/合法前置/保护与授权同源；本地候选实际参与解释/选择/重规划，采用后接真实任务；D2 正式本地入口和晚到结果重验 | [模块框架](../module-roadmap.md) / [T4 #7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| GAME-SAVE | P0 | IN_PROGRESS | C01-D1.0/E02-D1.0 的权威事实与暂停/恢复边界 | Codex持久化契约；GLM冻结codec/工具 | E04 从 D1.0 实际磁盘保存/跨进程恢复；E12 每批保存新增经营事实，E19 故障恢复；加载/投影屏障不解除用户暂停，无离线收益 | [模块框架](../module-roadmap.md) |
| GAME-DELIVERY | P0 | IN_PROGRESS | 复用APP-START/QA/LIFE；C02 每个检查点具名集成 | Codex实机/默认包；GLM适配/工具；美术供资产 | 每批更新 Main/默认应用并核包身份、实际玩家行为与保存重开；E20 D2.2 联合性能，E22 R1 独立发行门槛；旧包不会随源码更新 | [模块框架](../module-roadmap.md) / ART-LINK/PERF |
| C01-D1.0 | P0 | ACCEPTED | 本批运行源d631a24；合同71cd485；真实应用/原生检查/reviewer与最终architect通过，限定D1.0 | Codex | 本批合同与真实接入一起交；重复/非法命令无副作用，UI/模型不拥有事实；下一检查点另冻 C01 子片 | [D1.0复验](../reports/2026-10-07-d1-player-verification.md) / [合同](../contracts/d1-player-r1.md) |
| E01-D1.0 | P0 | ACCEPTED | 历史d631a24/合同71cd485的限定接受保留；现役镜头实际操作验收重开为CAMERA-01，其他有效成果保留 | GLM-Eng-A；Codex接入 | 普通镜头、对象选择、固定尺寸合法位置预览、下达/取消及真实目标卡；点击不穿透/重复不重提交/暂停仍可操作 | [D1.0复验](../reports/2026-10-07-d1-player-verification.md) / [合同](../contracts/d1-player-r1.md) / [镜头返修](CAMERA-01.md) |
| E02-D1.0 | P0 | ACCEPTED | 本批运行源d631a24；合同71cd485；真实应用/原生检查/reviewer与最终architect通过，限定D1.0 | Codex | 固定整平改为有限可选区域，真实到场/有效工作/版本重验/物理确认；取消保留已改造，无效位置不提交；绕障未支持须明示拒绝 | [D1.0复验](../reports/2026-10-07-d1-player-verification.md) / [合同](../contracts/d1-player-r1.md) |
| E03-D1.0 | P0 | ACCEPTED | 本批运行源d631a24；合同71cd485；真实应用/原生检查/reviewer与最终architect通过，限定D1.0 | GLM-Eng-B；Codex代理/注册 | 模型置于实体视觉子节点，六键由真实位移/工段驱动；保持根 identity/动作极值；合法切换先校验再复位，非法输入保留旧有效状态；未用 U01 不强行接运输 | [D1.0复验](../reports/2026-10-07-d1-player-verification.md) / [合同](../contracts/d1-player-r1.md) |
| E04-D1.0 | P0 | ACCEPTED | 本批运行源d631a24；合同71cd485；真实应用/原生检查/reviewer与最终architect通过，限定D1.0 | Codex；GLM可做冻结codec | 版本化磁盘快照保存地形/实体/时间/单任务；工作中及完成后退出重开一致，不重复完成；坏档/写失败保原档，物理屏障后恢复执行 | [D1.0复验](../reports/2026-10-07-d1-player-verification.md) / [合同](../contracts/d1-player-r1.md) |
| C02-D1.0 | P0 | ACCEPTED | 本批运行源d631a24；合同71cd485；真实应用/原生检查/reviewer与最终architect通过，限定D1.0 | Codex集成/独立验收 | 同一普通应用完成选择→真实整平→取消/完成→保存重开；来源commit/包hash/素材/设备证据齐全；只验可操作场地，不签 D1/G1；后续 C02 重复执行 | [D1.0复验](../reports/2026-10-07-d1-player-verification.md) / [合同](../contracts/d1-player-r1.md) |
| QA-01 | P0 | ACCEPTED | QA-03/QA-04/QA-05本轮底座 | Codex（上批实际） | 旧原件复算、新三轮Metal/有效内存可获取；性能稳定性转PERF-01 | [复验](../reports/2026-10-04-takeover-verification.md) / [#15](https://github.com/liuyejinghong/yudian-game/issues/15) |
| QA-02 | P0 | ACCEPTED | 冻结输入合同 | GLM（上批实际） | 109合同用例+实际Godot异常矩阵；限定commit已集成并随PR16合并 | [交付票](QA-02.md) |
| QA-03 | P0 | ACCEPTED | QA-02 | Codex（上批实际） | 只读移包/不同cwd/默认输出/引擎中断/失败；GUI关窗另列LIFE-01 | [复验](../reports/2026-10-04-takeover-verification.md) |
| QA-04 | P0 | ACCEPTED | QA-03/QA-05 | Codex（上批实际） | 实际Forward+/Metal、Mobile/Metal、运行身份；尺寸明确estimated | [复验](../reports/2026-10-04-takeover-verification.md) |
| QA-05 | P0 | ACCEPTED | 既有工具、原始模板 | Codex（上批实际） | 干净检出构建/启动、模板重复hash；新系统验证另列ENV-01 | [构建证据](../evidence/2026-10-04-takeover/clean-build-manifest.json) |
| QA-06 | P0 | ACCEPTED | QA-02 / ART-I00 | Codex（上批实际） | 纯输入/统计组件与独立视觉接缝；场景仍是探针，不是核心模拟 | [复验](../reports/2026-10-04-takeover-verification.md) |
| PERF-01 | P1 | REWORK | 六轮观察与复算保留；PERF-01-CONTROLLED 固定后台焦点失败，未完成新一轮受控复验 | GLM离线复算；Codex实机/裁决 | 受控采样/归因与稳定预算仍未接受；本轮仅纠正父票状态；不阻塞 D1.0 功能整理，相关联合性能由 E20 实测 | [实机复验](../reports/2026-10-05-runtime-verification.md) / [#5](https://github.com/liuyejinghong/yudian-game/issues/5) / [#15](https://github.com/liuyejinghong/yudian-game/issues/15) |
| PERF-01-COMPARE | P0 | ACCEPTED | 原ebfb877；GLM e1df119/440b686/3926518；独立review与集成复验通过 | Codex编排GLM；Codex review/验收 | 50项unit+9主控复验+3独立解析回归；只接受多轮身份分组/复算工具，不签性能稳定 | [任务包](../packages/PERF-01-COMPARE.md) / [复验](../reports/2026-10-04-perf-compare-verification.md) |
| LIFE-01-AUDIT | P0 | ACCEPTED | 原7f72c19；GLM a0c2f19/93ef427，主控3493ddb；独立review/集成复验通过 | Codex编排GLM；Codex review/验收 | 56项unit+11主控检查；只接受退出/摘要/CSV一致与缺证审计，不签GUI关窗/恢复 | [任务包](../packages/LIFE-01-AUDIT.md) / [复验](../reports/2026-10-04-lifecycle-audit-verification.md) |
| LIFE-01 | P0 | ACCEPTED | 固定历史包真实GUI关窗/默认输出/同包重开已实测 | Codex | 限定进程与产物：exit0/130/0、三独立run-id、旧文件/包SHA不变；不签存档恢复、公证发行或硬杀测试 | [实机复验](../reports/2026-10-05-runtime-verification.md) |
| ENV-01 | P2 | BLOCKED | 合法可用的无SDK隔离系统/设备；禁止为本票擅装系统 | GLM-Eng-A清单/脚本；Codex环境与裁决 | 同一包在无SDK环境启动，真实runtime/身份；未有环境不影响合同与独立样件 | [复验边界](../reports/2026-10-04-takeover-verification.md) |
| T3a | P1 | ACCEPTED | r1矩形数据接缝/边界/版本与提交取消职责已冻结；实际世界提交系统在T3d | Codex | 接受r1数据合同设计：绝对高度/row-major、不可变base与candidate、外围同值/版本重验和取消保留已发生结果；完整世界/邻区接入未实现，不直接开全套T3 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3a-DATA | P1 | ACCEPTED | T3a-r1冻结2a37f4b；GLM原提交d1f7cd6/修正1a0ec1c，主控独立验收 | GLM-Eng-A | 388工人检查+39主控独立检查通过，reviewer12项复核关闭两缺陷；net8/net10编译0警告0错误，net10执行；只接受数据子件，不批准世界提交/游戏保存 | [复验](../reports/2026-10-04-terrain-data-verification.md) / [合同r1](../contracts/terrain-patch-r1.md) / [GLM票](T3a-DATA.md) / [#19](https://github.com/liuyejinghong/yudian-game/issues/19) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3b | P2 | DRAFT | T3b-MESH已接受，T3-GODOT/PROBE受限复验完成；阶段管理/增量更新与正式接入仍待冻结；按真实接入/性能疑点再冻结，不作 D1.0 总门禁 | GLM-Eng-A | 局部阶段网格子件，边界连续、同输入输出；不把网格变化当持久世界已成立 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3c | P2 | DRAFT | T3c-MESH已接受，T3-GODOT/PROBE受限复验完成；高度场更新与正式接入仍待冻结；按真实接入/性能疑点再冻结，不作 D1.0 总门禁 | GLM-Eng-B | 第二候选子件与边界/局部更新证据；不和T3b争写公共状态 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3b-MESH | P1 | ACCEPTED | T3a-DATA；网格合同r1、共享输出/金样/独占目录已冻结；base8c3e117；实际GLM交付/主控独立验收/reviewer复核已完成 | GLM-Eng-A | 66工人检查+89共用主控检查通过；固定对角线、候选先验current；只接受纯网格，不做阶段管理/更新 | [复验](../reports/2026-10-04-terrain-mesh-verification.md) / [#21](https://github.com/liuyejinghong/yudian-game/issues/21) / [执行票](T3b-MESH.md) / [合同](../contracts/terrain-mesh-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3c-MESH | P1 | ACCEPTED | T3a-DATA；网格合同r1、共享输出/金样/独占目录已冻结；base8c3e117；实际GLM交付/主控独立验收/reviewer复核已完成 | GLM-Eng-B | 82工人检查+89共用主控检查通过；2×2细分采样顶点、面内仍平面；不做高度场更新系统 | [复验](../reports/2026-10-04-terrain-mesh-verification.md) / [#22](https://github.com/liuyejinghong/yudian-game/issues/22) / [执行票](T3c-MESH.md) / [合同](../contracts/terrain-mesh-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3-GODOT | P1 | ACCEPTED | base702e66b；Bridge sess_2cb78af554 / task_35d0705185；返工task_75e6ea5407；原提交ac457be+修复80b84d0；主控复验/reviewer/最终architect通过 | GLM-Eng-A | 10引擎命名检查通过；未引用顶点溢出实际先失败后拒绝；只接受ArrayMesh适配，不做世界状态 | [复验](../reports/2026-10-04-terrain-render-verification.md) / [#24](https://github.com/liuyejinghong/yudian-game/issues/24) / [执行票](T3-GODOT.md) / [合同](../contracts/terrain-render-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3-PROBE | P1 | ACCEPTED | 主控独立探针/fixtures；35日志检查与Metal实际GUI、三态PNG/back-cull通过；最终architect通过 | Codex | 真实重复/拒绝/重开/资源释放；全重建后替换及提交耗时，只接受未提交候选显示，不签增量/稳定性能或正式美术 | [复验](../reports/2026-10-04-terrain-render-verification.md) / [#24](https://github.com/liuyejinghong/yudian-game/issues/24) / [合同](../contracts/terrain-render-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3-COLLISION | P1 | ACCEPTED | basecd61ec4；ZCode sess_14ebc7d603/task_4016f0d285，原生GLM-5.3-Flash已确认 | GLM-Eng-A | 12项独立引擎复验通过；Stage1.25/Fine1.125真实坡面射线，单面/mask/拒绝/最大输入/独立资源；reviewer无剩缺陷；只接受静态适配 | [复验](../reports/2026-10-04-terrain-collision-verification.md) / [#26](https://github.com/liuyejinghong/yudian-game/issues/26) / [执行票](T3-COLLISION.md) / [合同](../contracts/terrain-collision-r1.md) |
| T3-COLLISION-PROBE | P1 | ACCEPTED | T3-COLLISION实现；主控独占TerrainProbe | Codex | 118条分帧检查+36条默认回归、自身PASS/exit0；Metal真实GUI/冷开/三态PNG；实际GodotPhysicsDirectSpaceState3D，reviewer无剩缺陷；只接受候选 | [复验](../reports/2026-10-04-terrain-collision-verification.md) / [#26](https://github.com/liuyejinghong/yudian-game/issues/26) / [合同](../contracts/terrain-collision-r1.md) |
| ART-LINK-M01 | P1 | ACCEPTED | 美术主控2401b6b技术接受；原1b91467限定材质，soil SHA5f8aa3e4 | Codex联调；美术组材质所有者 | soil_mars两网格surface0绑定，MaterialOverride为空；缺资源/错类型/双面透明拒绝，同镜头三态实机PNG；不签最终视觉 | [美术TODO](../../art/production/todo.md) / [首批需求](../../art/production/requirements/first-assets-r1.md) |
| ART-LINK-U01 | P1 | BLOCKED | U01 rev3 源/GLB/canonical manifest 已交；待局部载荷修正及 E03/E07 实际消费/代理合同 | 美术局部返修；Codex联调 | 正式 Godot 状态包装已具独立证据，Main 真实运输/货物驱动未接；不重复索要源，不阻塞只消费筑垒的 D1.0 子片 | [美术TODO](../../art/production/todo.md) |
| ART-LINK-T01-T02 | P0 | BLOCKED | D1.0 A03整平表面已按权威高度/mask接入并核保存；正式T01所有者视觉与E10开挖/矿量接缝仍待验 | 美术表面/标记；Codex权威地形接入 | 三版 GLB 是已有参考；按本批实际消费边界接权威高度场表现、碰撞和保存，不等整个 T3，也不直接用参考 GLB 替换世界 | [美术TODO](../../art/production/todo.md) |
| T3d-MEM | P1 | ACCEPTED | 单区域单写者内存入口；28主控独立例PASS，双TFM及实际Godot项目编译通过；reviewer无剩余问题 | Codex | 内存版本/取消保留/语义去重，失败与NoChange不消费请求；不签渲染/物理同步或存档 | [复验](../reports/2026-10-04-terrain-commit-verification.md) / [合同](../contracts/terrain-commit-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3d-COMMIT-POLICY | P1 | ACCEPTED | base6554495；Bridge sess_7697b4c358/task_76fc5bf2a2；原bf6da0a，原生GLM-5.3-Flash已确认，主控集成验证/reviewer完成 | GLM-Eng-A | 37例×2文化=74检查；仅纯判定；费用未知，net8仅编译、net10执行 | [复验](../reports/2026-10-04-terrain-commit-verification.md) / [任务包](../packages/T3d-COMMIT-POLICY.md) |
| LIFE-01-GUI | P0 | ACCEPTED | 主控自行启动固定包，Cua真实关闭自有窗口 | Codex | 默认user://三轮审计PASS、exit0/130/0、重开新目录；不签游戏保存 | [实机复验](../reports/2026-10-05-runtime-verification.md) / [用例](../contracts/runtime-controlled-r1.md) |
| PERF-01-CONTROLLED | P0 | REWORK | 六轮完成，后台Finder焦点漂移；需工程补固定后台/负载条件，不等待美术回执 | Codex | 原始六轮与两组复算保留；仅接受观察，严格受控NOT_ACCEPTED；温度未测、归因/稳定预算未完成 | [实机复验](../reports/2026-10-05-runtime-verification.md) / [用例](../contracts/runtime-controlled-r1.md) |
| T3d-PROJECTION-RESOURCES | P1 | ACCEPTED | 冻结905458c；Bridge sess_dd53b2aefe，原939c79d→返工0516dda；主控f3b08bf与8项native检查/reviewer通过 | GLM-Eng-A | 单对资源束、失败清理/幂等释放；首轮引擎ERROR已修，最终无ERROR；费用未知 | [复验](../reports/2026-10-05-terrain-view-verification.md) / [合同](../contracts/terrain-view-r1.md) |
| T3d-VIEW | P1 | ACCEPTED | 单区域Stage完整重建；29内存例/7步真实引擎检查、Metal GUI修改/拒绝/恢复/关窗、reviewer通过 | Codex | 接受独立权威场景、后续帧射线、重放/故障Current恢复；旧Main机器人/导航/save未接入，完整T3d不扩签 | [复验](../reports/2026-10-05-terrain-view-verification.md) / [合同](../contracts/terrain-view-r1.md) |
| LIVE-TERRAIN-OPTIONS | P1 | ACCEPTED | 冻结89cb3fb；原68390afa/集成0efd005，独立reviewer与主控21项复验通过 | GLM-Eng-A | 仅解析：新模式与benchmark互斥；旧109项工人回归通过；不签Main/native | [合同](../contracts/main-ground-r1.md) |
| GROUND-PATROL | P1 | ACCEPTED | 冻结89cb3fb；原58e99d4→7e64c97→c1300cb；主控最终10项native/reviewer通过 | GLM-Eng-B | 原生CharacterBody固定巡逻、真实坡往返/陡坡阻挡/空中暂停落地；无导航/避让，费用UNKNOWN | [复验](../reports/2026-10-05-main-ground-verification.md) / [合同](../contracts/main-ground-r1.md) |
| MAIN-GROUND | P1 | ACCEPTED | e2dbef1运行源码；Main七步/11项兼容、Metal真实GUI与close0，reviewer缺陷已修 | Codex | 显式live模式12机贴地、cell占用拒绝、后续帧版本同步与故障Current恢复；不签完整导航/建设/save/正式美术 | [复验](../reports/2026-10-05-main-ground-verification.md) / [合同](../contracts/main-ground-r1.md) |
| GROUND-ORDER | P1 | ACCEPTED | 冻结4cbdf3c；session sess_a371c1f521，原生GLM-5.3-Flash确认 | GLM-Eng-A | 单目标native7/旧10通过；37个同XZ空中帧false/落地true，清除保Paused/巡逻；不签导航 | [合同](../contracts/level-job-r1.md) |
| WORK-METER | P1 | ACCEPTED | 冻结4cbdf3c；session sess_54983ddecd，原生GLM-5.3-Flash确认 | GLM-Eng-B | 纯计时两文化36PASS；累计/中断清零/封顶/非法无副作用，不签任务状态 | [合同](../contracts/level-job-r1.md) |
| LEVEL-JOB | P1 | ACCEPTED | GROUND-ORDER/WORK-METER；主控合同与集成 | Codex | 一名筑垒真实到场与连续作业；Main15/旧七步/Metal取消与完成通过，取消/重复/过期/占用/故障复验，限定接受 | [合同](../contracts/level-job-r1.md) |
| APP-START | P1 | ACCEPTED | 既有离线导出/自包含runtime | Codex | 本机双击进入整平；签名/路径移动/实际GUI，非发行验收 | [启动说明](../../../prototype/README.md) / [复验](../reports/2026-10-05-desktop-start-verification.md) |
| T3d | P1 | DRAFT | T3d-MEM/VIEW与MAIN-GROUND限定接受；灰模Main原生通行已接，完整导航/经营状态保存/建设未完成 | Codex合同/集成；GLM确定适配子件 | 几何/碰撞/导航/重载与取消同源；邻工程并发、无过期提交/重复收益，跨模块独立reviewer | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3e | P2 | BLOCKED | PERF-01可用对照；T3d；ART-T01/T02仅为视觉对照需接口与样件；按真实接入/性能疑点再冻结，不作 D1.0 总门禁 | GLM-Eng-B采集工具/复算；Codex实机/选择 | 两候选同条件更新范围/主线程峰值/导航耗时/保存增长/视觉成本对比；所有者签视觉，主控选技术 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T4a | P1 | DRAFT | D1.1 C01 受限动作/授权与 E05 归属样本；E14 后续需 E11 真实任务 | Codex | T4 输入合同沿用并按 E14 冻结当前子片；规则参考加至多两种合适本地候选，真实完成/拦截/回退分计，不另造旁路规划器 | [#7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| T4-HARNESS | P1 | BLOCKED | T4a 输入/合法动作与提供者合同待冻结；E14 D1.1 先合同试验、D1.2 接真实任务 | GLM冻结评估工具/适配；Codex采纳与验收 | 复用受限世界输入，保留非法/超时/晚到结果；不自动下载权重或调用付费 API；独立 harness 不结清 T4/G1 | [#7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| T5-SAMPLE | P2 | BLOCKED | PERF-01；支持路径预检；同资源/镜头/输出/回退合同 | GLM-Eng-A采样与统计；Codex后端/画质裁决 | 原生/已支持超分模式真实数据与回退；不可用标缺证，不伪造集成，所有者判画质 | [#8](https://github.com/liuyejinghong/yudian-game/issues/8) |

## 2026-10-06 重评结果与后续归属

2026-10-07 所有者批准D1.0后，六子片已在同一普通应用实现并独立复验，六子片限定ACCEPTED；最终architect通过；完整模块父票只进IN_PROGRESS。两GLM交独占UI/筑垒适配，Codex交真实规则/保存/应用；美术专门会话交A01/A03候选，实际消费在本批应用。原生UI28、视觉12、Main22步和同包5独立进程通过，真实Metal鼠标与访达双击保存/重开/续接已核；来源及未测项见[D1.0复验](../reports/2026-10-07-d1-player-verification.md)。下一批仅按D1.1冻结资源/导航/运输/建设/保障及增量保存，本轮未派发。

旧探针的 ACCEPTED 范围与证据原样保留；T3b/T3c/T3e 远期比较和 PERF 受控返工不作为 D1.0 整体门禁。有真实投影/运动回归或联合负载疑点时再推进相应留项，不为改变路线重跑旧底座。ART-LINK-U01 的“源未交付”已纠正为局部修正/实际运输接入缺口；地表接缝分别按整平和开挖消费冻结。

后续规划包挂到现有父票，**这里只列归属，不增加第二套状态**：

- D1.1：E05 → GAME-RESOURCES；E06/E07 → GAME-ROBOTS（交接经资源账本）；E08 → GAME-BUILD；E09 → GAME-SUPPORT；E12 本阶段子片 → GAME-SAVE。正式新档用着陆器和有限套件，先核安全自举成本；基础保障不等救援方式冻结。
- D1.2：E10 → GAME-WORLD/GAME-RESOURCES；E11 → GAME-GOALS；E12 → GAME-SAVE；E13 → GAME-UX。真实缺料链、保障与保存贯通，玩家有不同后果的后续发展选择。
- E14 → GAME-GOALS/T4：D1.1 起试验，D1.2 在真实任务复验；候选承担实际决策责任，失败只调整候选，不阻塞账本/建设。
- D2.0：E15 → GAME-GOALS（资源/机器人/保障共用承诺）；E16 → GAME-WORLD（已知情报/实际勘探）；D2.1：E17 → GAME-SUPPORT/GAME-ROBOTS，E18 → GAME-GOALS/GAME-UX，E19 → GAME-SAVE/GAME-DELIVERY。零电恢复方式待定只阻塞 E17b，零耐久需其他机器人送回维修已确定。
- D2.2：E20 → GAME-DELIVERY/PERF/ENV；D3.0：E21 → GAME-WORLD/GAME-BUILD/GAME-GOALS；R1：E22 → GAME-DELIVERY。联合性能、弱配置、真实 API、Windows、发行按各自条件取证，未测记 NOT_RUN，不外推已通过。
- C01/C02 随每个检查点冻结/集成；本批 D1.0 接受不覆盖后续。P1 高保真由美术线按低保真/真实用途/所有者授权门槛推进，不替代经营或 G1 验收。

每个实际执行票分别记录验证、实现、接入和版本内验收，真人理解/持续经营意愿、所有者视觉独立记录；测试规格 FX-* 仍拟建/NOT_RUN。详细步骤用[产研 D1](../../roadmap/engineering-d1.md)、[后续产研](../../roadmap/engineering-d2-d3.md)和[验收矩阵](../../roadmap/acceptance.md)，不在 TODO 重抄整个规格。

## 历史已交批次（原验收范围保留）

MAIN-GROUND本波已合并[PR33](https://github.com/liuyejinghong/yudian-game/pull/33)，精确head8e1ddde、mergef041e3e；真实main重新编译与窄烟测通过。完成包不重复派发，当时建议下一冻结建设或导航小票，当前顺序以上述重评为准；两GLM会话已结束，费用UNKNOWN。

2026-10-05 LEVEL-JOB三子票限定ACCEPTED：[复验](../reports/2026-10-05-level-job-verification.md)。GLM同冻结基线两包并行，主控关键状态/实机/集成；当时下一任务建议为导航与避让或建设资源接缝，当前按 D1.0 重评冻结；不重复派单本批；无电耗/经济/存档与正式美术扩签。

PR34本波已合并，精确head b79860a、merge 8bcefb1；主工作树重新编译0警告0错误与15步窄烟测exit0/12贴地，GLM两会话已结束。父#6与PERF-01保持未完成。

APP-START已合并[PR35](https://github.com/liuyejinghong/yudian-game/pull/35)：head8a2c986、merge115f1c7，main同步；本机余电.app入口可双击，固定试玩包保留。

2026-10-07 D1.1工程、同包原生与实际窗口复验已完成；六工程子片限定ACCEPTED，根入口已切到975916d修复包。下一批D1.2缺料生产与有后果的发展选择；详细限定与失败修复见[D1.1复验](../reports/2026-10-07-d11-bootstrap-verification.md)。不扩签全部模块、自然平衡或联合视觉。
