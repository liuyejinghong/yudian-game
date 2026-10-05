# 余电双线 TODO

更新：2026-10-05。[GitHub总索引 #17](https://github.com/liuyejinghong/yudian-game/issues/17) / [文档PR #18](https://github.com/liuyejinghong/yudian-game/pull/18) / [数据PR #20](https://github.com/liuyejinghong/yudian-game/pull/20) / [网格PR #23](https://github.com/liuyejinghong/yudian-game/pull/23) / [显示PR #25](https://github.com/liuyejinghong/yudian-game/pull/25) / [碰撞PR #27](https://github.com/liuyejinghong/yudian-game/pull/27) / [PERF PR #28](https://github.com/liuyejinghong/yudian-game/pull/28) / [LIFE PR #29](https://github.com/liuyejinghong/yudian-game/pull/29)。本轮执行范围：**推进GLM产研子票，主控独立复验并按授权合并合格交付**。完整游戏功能未完成；首批美术制作已由所有者在美术会话启动。排入表格、指定拟执行槽或依赖齐备，都不等于已派单。后续由主控按所有者阶段指令排批，不逐项追问常规技术细节。

| 线 | 权威任务表 | 当前事实 | 后续首要队列（未启动） |
|---|---|---|---|
| 产研 | [产研 TODO](docs/engineering/tasks/todo.md) | QA受限技术验收已完成，PR #16已合并；性能稳定性仍待复核 | 数据与纯网格子件已接受；Godot适配/候选探针已受限接受；静态碰撞与候选联调已受限接受，PR #27已合并、子票#26已关闭；两个离线工具包均限定ACCEPTED，PR #28/#29已分别合并；单区域内存提交与GLM纯判定已限定接受；阶段/同步/导航保存和性能留项 |
| 美术 | [美术 TODO](docs/art/production/todo.md) | ART-I00技术校准已验；[首批需求](docs/art/production/requirements/first-assets-r1.md)由主控编写；正式资产与视觉效果未验 | M01材质已由美术组技术接受，soil_mars工程逐surface联调通过；预览工具正在制作，正式模型/地形素材仍缺；工程缺口见ART-LINK各行 |

2026-10-04所有者授权后，PR [#16](https://github.com/liuyejinghong/yudian-game/pull/16)、[#18](https://github.com/liuyejinghong/yudian-game/pull/18)及[#20](https://github.com/liuyejinghong/yudian-game/pull/20)已合并，本网格批次从main `3999d30`起步。PR [#13](https://github.com/liuyejinghong/yudian-game/pull/13)/[#14](https://github.com/liuyejinghong/yudian-game/pull/14)保留902a662历史评审/工单，已补后续权威入口，不重复派发其中旧任务；历史结果见[接管复验](docs/engineering/reports/2026-10-04-takeover-verification.md)。原Issue [#5](https://github.com/liuyejinghong/yudian-game/issues/5)/[#6](https://github.com/liuyejinghong/yudian-game/issues/6)/[#7](https://github.com/liuyejinghong/yudian-game/issues/7)/[#8](https://github.com/liuyejinghong/yudian-game/issues/8)分别关联T2/T3/T4/T5；旧ready-for-agent标签不能替代新子票的冻结接口和本轮范围。

## 分工与外包默认

| 角色/执行槽 | 工作 | 权限边界 |
|---|---|---|
| Codex主控 | 产品/技术合同、拆票、关键状态/持久化、实机调度、审查、验收、集成 | 公共合同、Main/项目/依赖、共用fixture和注册表唯一写者；可以冻结子票让GLM实现 |
| GLM-Eng-A / GLM-Eng-B | 纯数据/网格候选、工具、测试、清单等已定接口子件 | 每次一张结果票，各自工作树和目录；不批准提交/取消语义或最终性能 |
| GLM-Art-A / GLM-Art-B / GLM-Art-T | 单件机器人/设施/地形参考和确定边界下样件 | 独立源、GLB、manifest；不写库存/电量/通行/产矿规则 |
| GLM-Material | 共享材质实现 | 主控冻结材质接口后，指定其为共享材质目录唯一写者；其他工人只引用 |
| 独立reviewer / architect | 关键或跨模块变更只读审查 | 不编辑、不代替主控验收；按风险使用，不每张机械小票都调用 |
| 所有者 | 视觉/产品效果与越界决定 | 不承担算法选型和逐工序技术审批 |

这些GLM槽是拟执行角色，不是已启动会话；同一GLM渠道可以承担不同票。默认通过Agent Bridge调用ZCode原生GLM，先确认模型；渠道不可用则报告，不静默换模型。优先把重复实现/测试/清单/展示工作交给GLM，主控不因工人额度充足而放大票，也不重做已验收成果。记录真实耗时、返工、工具可见用量；未知成本写未知，不给虚构排名。

## 状态、证据与更新

- `DRAFT`：已入待办，未冻结可执行票；`READY`：依赖、范围、文件归属与验收方式齐备。设计票冻结问题/输入范围/验收案例，领域合同是其产出；下游实现票才要求领域接口已经冻结。两者均不自动开工。
- `IN_PROGRESS`：实际派单后才设置，记录session/task和分支；`SUBMITTED`→`REVIEW`→`ACCEPTED`由主控独立证据推进。`REWORK`是具体不合格项；`BLOCKED`须写真实阻塞与解除条件。
- `ACCEPTED`只覆盖任务行写明的验收范围；合并、图形验证、视觉确认分别记录。旧批次表是历史快照，当前状态只在对应线的TODO维护。
- 每行有ID、优先级、状态、依赖、拟执行者、结果/验收及来源。P0=当前整理或证据留项；P1=首批底座/样件；P2=后续队列，不是日期承诺。
- 只在派单时展开一张完整票：固定base/分支、最小资料、可写与禁写、冻结接口/单位/错误、正常异常例、释放/中断、实际检查命令、提交/限制与升级条件。实际示例：[QA-02](docs/engineering/tasks/QA-02.md)、[TODO-ART-01](docs/workflow/tasks/TODO-ART-01.md)。
- 依赖就绪且当期执行范围覆盖，主控才将READY票派出；工人不自动领取下一张。独立提交→主控查看diff和检查→接受→PR集成，不自验自签。两轮同接口失败缩票/定位，保留失败材料。
- 更新任务时只改本线权威行并附证据；根表和GitHub总索引只放入口与摘要，不维护第二套逐行状态。旧Issue保留讨论和原范围，需要执行时再创建/关联子票，不一次生成全部远期Issue。

## 两线依赖与并行

性能留项阻塞“将基线当作稳定对照”，不阻塞T3a合同讨论或独立美术造型。土坡/矿点正式接入必须等区域、边界高程、阶段/版本合同；图片和造型不能反推通行/矿物。共享材质只设一名写者；单位与设施可分别实现，但导入由主控串行核查公共接线。

后续可并行：GLM-Eng-A实现冻结的网格候选子件，GLM-Art-A做驮运，GLM-Art-B做太阳能；文件不交叠。不能并行争写Main、共用fixture、公共材质或同一模型。新资产单独预览/基准，不混旧灰模成绩；正式验收仍需正常指挥距离效果、真实导入与实际成本。

当前编排：美术需求由专门会话按美术权威TODO推进；产研继续由本会话握合同与集成。数据和两个CPU网格子件已经接受并合并PR #20/#23；本轮GLM完成T3-GODOT（含一次限定返工），主控完成独立T3-PROBE，真实Godot复验、reviewer与最终architect通过；仅接受资源适配/未提交候选预览。[本轮复验](docs/engineering/reports/2026-10-04-terrain-render-verification.md) / [子票#24](https://github.com/liuyejinghong/yudian-game/issues/24)。

更新只采用完整CPU重建+新ArrayMesh替换。完整T3、阶段/高度场增量更新、完整世界、旧Main接线与机器人通行/导航、保存与最终美术继续留项；下一批先由主控冻结最小子票，再将确定实现/测试交GLM。费用未知如实记录。各行状态以产研/美术权威表为准。

本批证据工具：PERF-01-COMPARE与LIFE-01-AUDIT均限定ACCEPTED，原交付与返工分别保留。[PERF复验](docs/engineering/reports/2026-10-04-perf-compare-verification.md) / [LIFE复验](docs/engineering/reports/2026-10-04-lifecycle-audit-verification.md)。LIFE真实GUI/默认目录重开现已限定接受；PERF受控条件/原因/稳定预算继续未完成。外包可用原生子代理，主控保留最终审查/集成职责；各行以产研权威表为准。

本批T3d-MEM与GLM纯判定子件限定接受，已合并[PR #30](https://github.com/liuyejinghong/yudian-game/pull/30)：[内存提交复验](docs/engineering/reports/2026-10-04-terrain-commit-verification.md)。2026-10-05主控自行完成LIFE真实关窗与同包重开并限定接受，六轮PERF完成但后台焦点失守，受控项REWORK、原因与稳定预算未完成。[实机复验](docs/engineering/reports/2026-10-05-runtime-verification.md)。工程负责游戏测试，美术负责建模，不以美术回执为启动前提。

2026-10-05旧PR清理完成：[#1](https://github.com/liuyejinghong/yudian-game/pull/1)、[#2](https://github.com/liuyejinghong/yudian-game/pull/2)、[#3](https://github.com/liuyejinghong/yudian-game/pull/3)、[#13](https://github.com/liuyejinghong/yudian-game/pull/13)、[#14](https://github.com/liuyejinghong/yudian-game/pull/14)已全部合并，查询时open队列为0。[限定复验](docs/engineering/reports/2026-10-05-pr-cleanup-verification.md)：历史说明/现役入口已同步，参考图仅归档，游戏代码与生产资产未改；现行PERF/T3/正式美术留项不扩签。


2026-10-05 T3d-PROJECTION-RESOURCES / T3d-VIEW限定接受：GLM资源束经一次返工，主控实际单区域权威提交场景已串起内存/画面/静态碰撞；29内存检查、8 native资源检查、7步引擎与Metal GUI通过。[复验](docs/engineering/reports/2026-10-05-terrain-view-verification.md)。完整T3d、旧Main机器人通行/导航/存档/建设闭环、正式美术与性能留项未完成，下一张按真实接入接口再冻结。

本批已合并[PR #32](https://github.com/liuyejinghong/yudian-game/pull/32)，merge `8b693447e4e466f1baf0ad2deec710e642c28895`；主仓库main已同步，完整父票保持未完成。
