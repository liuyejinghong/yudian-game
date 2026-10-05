# 产研 TODO

更新：2026-10-05；主控Codex。PR33已合并：单区域权威地形、真正Main原生通行与手动改造限定接受。PR34已合并，单机器人整平任务闭环限定接受；完整导航、建设/经济、保存与稳定性能仍未完成，正式素材按ART-LINK跟踪。

| ID | 优先级 | 状态 | 依赖/解除条件 | 拟执行槽 | 交付与接受条件 | 来源/证据 |
|---|---|---|---|---|---|---|
| PLAYER-SLICE | P0 | DRAFT | 先明确并验证经营/目标委托假设；已有地形/移动/施工复用，太阳能场景待具体冻结 | Codex玩法/合同与集成；GLM独占UI/确定子件 | 玩家发展与保障选择→机器人补齐前置并执行→资源/供能/维护与基地能力改变→下一次有效决策；功能UI可解释成本/执行者/阻塞/边界并允许干预；建设动作链只是子检查，基本耗电/耐久/维修不整体后置；未冻结未派单，不冒称完整G1 | [流程调研](../reports/2026-10-05-simulation-development-research.md) / [产品§14](../../product/product-definition.md#144-内部验证顺序) |
| QA-01 | P0 | ACCEPTED | QA-03/QA-04/QA-05本轮底座 | Codex（上批实际） | 旧原件复算、新三轮Metal/有效内存可获取；性能稳定性转PERF-01 | [复验](../reports/2026-10-04-takeover-verification.md) / [#15](https://github.com/liuyejinghong/yudian-game/issues/15) |
| QA-02 | P0 | ACCEPTED | 冻结输入合同 | GLM（上批实际） | 109合同用例+实际Godot异常矩阵；限定commit已集成并随PR16合并 | [交付票](QA-02.md) |
| QA-03 | P0 | ACCEPTED | QA-02 | Codex（上批实际） | 只读移包/不同cwd/默认输出/引擎中断/失败；GUI关窗另列LIFE-01 | [复验](../reports/2026-10-04-takeover-verification.md) |
| QA-04 | P0 | ACCEPTED | QA-03/QA-05 | Codex（上批实际） | 实际Forward+/Metal、Mobile/Metal、运行身份；尺寸明确estimated | [复验](../reports/2026-10-04-takeover-verification.md) |
| QA-05 | P0 | ACCEPTED | 既有工具、原始模板 | Codex（上批实际） | 干净检出构建/启动、模板重复hash；新系统验证另列ENV-01 | [构建证据](../evidence/2026-10-04-takeover/clean-build-manifest.json) |
| QA-06 | P0 | ACCEPTED | QA-02 / ART-I00 | Codex（上批实际） | 纯输入/统计组件与独立视觉接缝；场景仍是探针，不是核心模拟 | [复验](../reports/2026-10-04-takeover-verification.md) |
| PERF-01 | P0 | IN_PROGRESS | 已有六轮实际观察；尚缺固定后台焦点/负载对照与profiler归因 | GLM离线复算；Codex实机/裁决 | 启动尖刺全部保留；本轮前台75–77FPS/后台波动只作观察，原因与稳定预算未接受 | [实机复验](../reports/2026-10-05-runtime-verification.md) / [#5](https://github.com/liuyejinghong/yudian-game/issues/5) / [#15](https://github.com/liuyejinghong/yudian-game/issues/15) |
| PERF-01-COMPARE | P0 | ACCEPTED | 原ebfb877；GLM e1df119/440b686/3926518；独立review与集成复验通过 | Codex编排GLM；Codex review/验收 | 50项unit+9主控复验+3独立解析回归；只接受多轮身份分组/复算工具，不签性能稳定 | [任务包](../packages/PERF-01-COMPARE.md) / [复验](../reports/2026-10-04-perf-compare-verification.md) |
| LIFE-01-AUDIT | P0 | ACCEPTED | 原7f72c19；GLM a0c2f19/93ef427，主控3493ddb；独立review/集成复验通过 | Codex编排GLM；Codex review/验收 | 56项unit+11主控检查；只接受退出/摘要/CSV一致与缺证审计，不签GUI关窗/恢复 | [任务包](../packages/LIFE-01-AUDIT.md) / [复验](../reports/2026-10-04-lifecycle-audit-verification.md) |
| LIFE-01 | P0 | ACCEPTED | 固定历史包真实GUI关窗/默认输出/同包重开已实测 | Codex | 限定进程与产物：exit0/130/0、三独立run-id、旧文件/包SHA不变；不签存档恢复、公证发行或硬杀测试 | [实机复验](../reports/2026-10-05-runtime-verification.md) |
| ENV-01 | P2 | BLOCKED | 合法可用的无SDK隔离系统/设备；禁止为本票擅装系统 | GLM-Eng-A清单/脚本；Codex环境与裁决 | 同一包在无SDK环境启动，真实runtime/身份；未有环境不影响合同与独立样件 | [复验边界](../reports/2026-10-04-takeover-verification.md) |
| T3a | P1 | ACCEPTED | r1矩形数据接缝/边界/版本与提交取消职责已冻结；实际世界提交系统在T3d | Codex | 接受r1数据合同设计：绝对高度/row-major、不可变base与candidate、外围同值/版本重验和取消保留已发生结果；完整世界/邻区接入未实现，不直接开全套T3 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3a-DATA | P1 | ACCEPTED | T3a-r1冻结2a37f4b；GLM原提交d1f7cd6/修正1a0ec1c，主控独立验收 | GLM-Eng-A | 388工人检查+39主控独立检查通过，reviewer12项复核关闭两缺陷；net8/net10编译0警告0错误，net10执行；只接受数据子件，不批准世界提交/游戏保存 | [复验](../reports/2026-10-04-terrain-data-verification.md) / [合同r1](../contracts/terrain-patch-r1.md) / [GLM票](T3a-DATA.md) / [#19](https://github.com/liuyejinghong/yudian-game/issues/19) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3b | P1 | DRAFT | T3b-MESH已接受，T3-GODOT/PROBE受限复验完成；阶段管理/增量更新与正式接入仍待冻结 | GLM-Eng-A | 局部阶段网格子件，边界连续、同输入输出；不把网格变化当持久世界已成立 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3c | P1 | DRAFT | T3c-MESH已接受，T3-GODOT/PROBE受限复验完成；高度场更新与正式接入仍待冻结 | GLM-Eng-B | 第二候选子件与边界/局部更新证据；不和T3b争写公共状态 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3b-MESH | P1 | ACCEPTED | T3a-DATA；网格合同r1、共享输出/金样/独占目录已冻结；base8c3e117；实际GLM交付/主控独立验收/reviewer复核已完成 | GLM-Eng-A | 66工人检查+89共用主控检查通过；固定对角线、候选先验current；只接受纯网格，不做阶段管理/更新 | [复验](../reports/2026-10-04-terrain-mesh-verification.md) / [#21](https://github.com/liuyejinghong/yudian-game/issues/21) / [执行票](T3b-MESH.md) / [合同](../contracts/terrain-mesh-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3c-MESH | P1 | ACCEPTED | T3a-DATA；网格合同r1、共享输出/金样/独占目录已冻结；base8c3e117；实际GLM交付/主控独立验收/reviewer复核已完成 | GLM-Eng-B | 82工人检查+89共用主控检查通过；2×2细分采样顶点、面内仍平面；不做高度场更新系统 | [复验](../reports/2026-10-04-terrain-mesh-verification.md) / [#22](https://github.com/liuyejinghong/yudian-game/issues/22) / [执行票](T3c-MESH.md) / [合同](../contracts/terrain-mesh-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3-GODOT | P1 | ACCEPTED | base702e66b；Bridge sess_2cb78af554 / task_35d0705185；返工task_75e6ea5407；原提交ac457be+修复80b84d0；主控复验/reviewer/最终architect通过 | GLM-Eng-A | 10引擎命名检查通过；未引用顶点溢出实际先失败后拒绝；只接受ArrayMesh适配，不做世界状态 | [复验](../reports/2026-10-04-terrain-render-verification.md) / [#24](https://github.com/liuyejinghong/yudian-game/issues/24) / [执行票](T3-GODOT.md) / [合同](../contracts/terrain-render-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3-PROBE | P1 | ACCEPTED | 主控独立探针/fixtures；35日志检查与Metal实际GUI、三态PNG/back-cull通过；最终architect通过 | Codex | 真实重复/拒绝/重开/资源释放；全重建后替换及提交耗时，只接受未提交候选显示，不签增量/稳定性能或正式美术 | [复验](../reports/2026-10-04-terrain-render-verification.md) / [#24](https://github.com/liuyejinghong/yudian-game/issues/24) / [合同](../contracts/terrain-render-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3-COLLISION | P1 | ACCEPTED | basecd61ec4；ZCode sess_14ebc7d603/task_4016f0d285，原生GLM-5.3-Flash已确认 | GLM-Eng-A | 12项独立引擎复验通过；Stage1.25/Fine1.125真实坡面射线，单面/mask/拒绝/最大输入/独立资源；reviewer无剩缺陷；只接受静态适配 | [复验](../reports/2026-10-04-terrain-collision-verification.md) / [#26](https://github.com/liuyejinghong/yudian-game/issues/26) / [执行票](T3-COLLISION.md) / [合同](../contracts/terrain-collision-r1.md) |
| T3-COLLISION-PROBE | P1 | ACCEPTED | T3-COLLISION实现；主控独占TerrainProbe | Codex | 118条分帧检查+36条默认回归、自身PASS/exit0；Metal真实GUI/冷开/三态PNG；实际GodotPhysicsDirectSpaceState3D，reviewer无剩缺陷；只接受候选 | [复验](../reports/2026-10-04-terrain-collision-verification.md) / [#26](https://github.com/liuyejinghong/yudian-game/issues/26) / [合同](../contracts/terrain-collision-r1.md) |
| ART-LINK-M01 | P1 | ACCEPTED | 美术主控2401b6b技术接受；原1b91467限定材质，soil SHA5f8aa3e4 | Codex联调；美术组材质所有者 | soil_mars两网格surface0绑定，MaterialOverride为空；缺资源/错类型/双面透明拒绝，同镜头三态实机PNG；不签最终视觉 | [美术TODO](../../art/production/todo.md) / [首批需求](../../art/production/requirements/first-assets-r1.md) |
| ART-LINK-U01 | P1 | BLOCKED | 正式U01源/GLB/manifest/逐surface角色与scale未交付 | 美术组制作；Codex联调 | 正式机器人实际Godot导入/绑定与状态复位验证；I00校准件不能替代 | [美术TODO](../../art/production/todo.md) |
| ART-LINK-T01-T02 | P1 | BLOCKED | 正式土坡/矿点三态素材与接入说明未交付；正式素材绑定尚未实现 | 美术组制作；Codex联调 | 模型/源/manifest/尺度及逐surface绑定齐备后冻结正式接入票；当前工程网格不充当正式美术 | [美术TODO](../../art/production/todo.md) |
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
| T3d | P1 | DRAFT | T3d-MEM/VIEW与MAIN-GROUND限定接受；灰模Main原生通行已接，完整导航/保存/建设未完成 | Codex合同/集成；GLM确定适配子件 | 几何/碰撞/导航/重载与取消同源；邻工程并发、无过期提交/重复收益，跨模块独立reviewer | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3e | P1 | BLOCKED | PERF-01可用对照；T3d；ART-T01/T02仅为视觉对照需接口与样件 | GLM-Eng-B采集工具/复算；Codex实机/选择 | 两候选同条件更新范围/主线程峰值/导航耗时/保存增长/视觉成本对比；所有者签视觉，主控选技术 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T4a | P2 | DRAFT | 现役产品/共同场景与权限案例，明确本轮候选输入范围 | Codex | 冻结世界事实/行动/权限输入；将原T4大票拆成领域基线、校验器、候选适配与统计小票；真实完成与拦截分计，模型非权威 | [#7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| T4-HARNESS | P2 | BLOCKED | T4a；领域计划/错误分类/模拟提供者接口 | GLM-Eng-A / GLM-Eng-B分目录 | 纯harness/适配/测试优先外包；主控握权限与提交；不自动下载权重或开付费API | [#7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| T5-SAMPLE | P2 | BLOCKED | PERF-01；支持路径预检；同资源/镜头/输出/回退合同 | GLM-Eng-A采样与统计；Codex后端/画质裁决 | 原生/已支持超分模式真实数据与回退；不可用标缺证，不伪造集成，所有者判画质 | [#8](https://github.com/liuyejinghong/yudian-game/issues/8) |

## 进入下一执行批次时

当前交付定位是工程验证包，尚非玩家可玩版本。下一批优先PLAYER-SLICE，但先按[流程调研](../reports/2026-10-05-simulation-development-research.md)明确并验证玩家的经营选择、目标委托与持续循环；太阳能建设保留为候选场景，不把建成单设施当玩法验收。产品§14.4的足料/缺料/变化/竞争仍是实施检查点，基本耗电、耐久与维修不整体后置。验证目的明确后再将UI、资源和必要导航范围拆成GLM独占包，关键事实/权限/取消由主控负责。接受条件应包括玩家理解目标、选择产生真实且可解释的后果、能形成下一决策；趣味性仍待真人试玩，未冻结不派单、不扩签完整G1。PERF-01继续未完成，必要受控复验保留；已接受的LIFE/原生运动/投影不重复派单。正式美术不是灰模玩法开测前置。

T4与T5沿用已存在Issue范围，不重新创建宏观路线；目标委托、机器人保障和生产存档仍未完成，本轮也不排成假装已经READY的大包。低配/Windows/发行是后续范围，当前不因缺设备宣布最低配置，也不把新系统留项扩成购买或部署任务。

MAIN-GROUND本波已合并[PR33](https://github.com/liuyejinghong/yudian-game/pull/33)，精确head8e1ddde、mergef041e3e；真实main重新编译与窄烟测通过。完成包不重复派发，下一冻结建设或导航小票；两GLM会话已结束，费用UNKNOWN。

2026-10-05 LEVEL-JOB三子票限定ACCEPTED：[复验](../reports/2026-10-05-level-job-verification.md)。GLM同冻结基线两包并行，主控关键状态/实机/集成；下一任务是导航与避让或建设资源接缝新冻结，不重复派单本批；无电耗/经济/存档与正式美术扩签。

PR34本波已合并，精确head b79860a、merge 8bcefb1；主工作树重新编译0警告0错误与15步窄烟测exit0/12贴地，GLM两会话已结束。父#6与PERF-01保持未完成。

APP-START已合并[PR35](https://github.com/liuyejinghong/yudian-game/pull/35)：head8a2c986、merge115f1c7，main同步；本机余电.app入口可双击，固定试玩包保留。
