# 《余电》美术待办清单

更新日期：2026-10-04
初版：GLM按TODO-ART-01整理，基线`06f70c9`；维护与接受：Codex。当前批次art-brief-r1，代码基线`9a89601`；新增首批需求、可派单材质票及实际工具预检记录。

## 1. 本轮范围与边界

- 本轮主控编写美术需求并安排下一批外包，GLM只执行工具预检；尚未制作正式模型、材质或纹理，也未修改游戏代码。
- 生产票本轮尚未派单，READY是可安排的票；启动仍由主控记录独立工作树、文件归属和实际Bridge任务。
- 不创造成本／预算事实：面数、贴图预算未实测；工单代理体尺寸只是旧代码事实。[首批需求](requirements/first-assets-r1.md)的配色/比例是可修改的样件起点，不是已通过视觉验收的生产规范。
- 美术不决定产矿、通行、发电、救援等模拟规则；连接点与动作名的存在不代表能力、载荷或回收方式已获准。

## 2. 状态与优先级

允许状态：`DRAFT / READY / BLOCKED / IN_PROGRESS / SUBMITTED / REVIEW / REWORK / ACCEPTED`。是否获准执行、是否合并另行说明；未真实派单不写IN_PROGRESS。优先级与[总TODO](../../../TODO.md)统一使用P0/P1/P2。

## 3. 2026-10-04 当前事实

- **ART-I00 = ACCEPTED**：仅技术校准通过；PR #16 未合并；视觉验收未做（NOT_RUN）。旧清单的「待核定」不覆盖本结论；校准件是原创尺度标尺，不是驮运／设施正式模型。
- **ART-T01／ART-T02 = BLOCKED**：正式接入等待 T3 区域／边界高程／版本合同；参考造型不必等性能复核。
- ART-M01已形成READY材质票，其余10个原ART ID仍DRAFT；未启动生产。
- ART-TOOLS-01已实际外包并接受修正报告；独立实查Python/Godot/.NET可用、Blender在已查路径未找到。工具检测不等于生产流水线通过。
- ART-BRIEF-01记录主控需求交付，ART-PREVIEW-01记录后续预览工具缺口；它们不是新增游戏资产种类。

## 4. 拟执行槽（将来角色，非已启动 agent）

| 槽 | 持有任务行 | 说明 |
|---|---|---|
| GLM-Art-A | ART-U01、U02、U03、P01；首批U01后可接F02 | 同一槽按票串行，独立单位/设施目录 |
| GLM-Art-B | ART-F01、F03…F06 | F01可与另一个工人的F02并行 |
| GLM-Art-T | ART-T01、T02 | 地形样件 |
| GLM-Material | ART-M01 | 共享材质单一写者 |
| Codex（主控） | ART-I00公共合同／集成、ART-BRIEF-01需求 | 技术验收Codex；视觉效果验收所有者；ART-PREVIEW-01拟给GLM工具槽 |

实际派单要求：独立工作树、单票冻结目录；不得两个agent同时编辑同一源、场景入口或资源注册表。ART-TOOLS-01已由GLM-Art-A做只读预检；不由其自行领取材质或模型下一票。

## 5. 任务表（17行 = 14个原始ID + 工具预检/需求/预览三项）

原始 14 行清单见[清单@171c610][14csv]；其旧状态列按本轮实证更新，不覆盖当前任务行。P0记录已验接口，P1为首批样件/工具前置，P2为第一批通过后的队列；最终排批由主控决定。

| ID | 任务 | 优先级 | 状态 | 前置 | 拟执行槽 | 交付 / 验收 | 来源 |
|---|---|---|---|---|---|---|---|
|ART-I00|模型接入约定（技术校准）|P0|ACCEPTED|无（已完成技术验收）|Codex（公共合同／集成）|已交付可编辑 glTF+bin 源、生成脚本、GLB 校准件、manifest 与独立预览场景；技术验收已通过，视觉验收未做（NOT_RUN）；PR #16 未合并。校准件不是驮运／设施正式模型|[接口合同][i00]、[接管复验][takeover]、[PR #16][pr16]|
|ART-T01|整平土坡样件（原坡／施工中／整平后三阶段）|P1|BLOCKED|ART-TOOLS-01通过；T3区域／边界高程／版本合同冻结（主控T3a）；I00技术接口|GLM-Art-T|三阶段同一局部边界、无裂缝；源文件＋阶段 GLB＋同镜头对照图；不实现矿物结算。参考造型可先行，不必等性能复核；正式接入待 T3|[工单][wo]|
|ART-T02|开挖矿点样件（未开挖／部分／已开挖三阶段）|P1|BLOCKED|同 ART-T01（T3 区域／边界高程／版本合同）|GLM-Art-T|三阶段可辨截面与矿层、统一边界；新增前后状态不自动成为可挖储量，几何／碰撞／导航／保存由 T3 绑定|[工单][wo]|
|ART-U01|驮运样件|P1|DRAFT|M01接受、预览合同补全；生产票冻结工具路径/目录/动作；I00技术原则|GLM-Art-A|低前头、开放载货区、四轮、空载/有载；四socket与逐态声明。首样候选1.2×1.6×0.9m（宽/长/高），不是旧代理体或最终比例；完整源/GLB/清单/同镜头证据|[首批需求](requirements/first-assets-r1.md)、[工单][wo]|
|ART-F01|太阳能阵列样件|P1|DRAFT|U01导入/比例复核；M01接受、预览约定补全；主控冻结设施尺寸/接口/完整生产票|GLM-Art-B|packed/installed/deploying/completed四建设阶段，独立于七运行态；面板局部驱动、基座固定，不整栋旋转；建成不等于发电|[首批需求](requirements/first-assets-r1.md)、[工单][wo]|
|ART-F02|加工设施样件|P1|DRAFT|同F01；独立目录/尺寸/适用socket集合冻结|GLM-Art-A或另一设施工人（与F01不交叠）|idle/work/offline/maintenance；本轮offline展示映射disabled，禁止未知第八状态；活动件、进出料区域与少量道具，不计算缺电/库存/维护|[首批需求](requirements/first-assets-r1.md)、[工单][wo]|
|ART-M01|共享基础材质组|P1|READY|六角色/参数/资源目录/验收已冻结，已装Godot可用；不依赖T3或模型|GLM-Material（单一写者）|六个可编辑.tres和参数/引用说明；主控独立加载与核参；不要求GLB。视觉比较NOT_RUN，未派单|[M01票](tasks/ART-M01.md)、[首批需求](requirements/first-assets-r1.md)|
|ART-U02|筑垒机器人|P2|DRAFT|第一批（U01／F01／F02／M01）导入、比例、实际成本与所有者效果通过；主控排批|GLM-Art-A|清场／施工／维护工具可辨；停机与工作姿态不同；不由外形批准额外能力|[工单][wo]|
|ART-U03|望山机器人|P2|DRAFT|同 ART-U02|GLM-Art-A|桅杆、扫描与运动；底部锚点正确，不做悬空人体|[工单][wo]|
|ART-F03|仓储设施|P2|DRAFT|同 ART-U02|GLM-Art-B|固定地基，货物进出与空位清楚；禁止整栋转圈|[工单][wo]|
|ART-F04|充电站|P2|DRAFT|同 ART-U02|GLM-Art-B|可见泊位与接口，占用／停电／充电可区别；立柱不悬浮|[工单][wo]|
|ART-F05|维修站|P2|DRAFT|同 ART-U02|GLM-Art-B|拖入通道、工具工作区与状态；维修不等于充满电；不为迎合占位强做雷达|[工单][wo]|
|ART-F06|着陆器|P2|DRAFT|同 ART-U02|GLM-Art-B|落地支脚、卸货区域与运输用途；不默认增加人口穹顶或人类模拟|[工单][wo]|
|ART-P01|货箱与回收连接件|P2|DRAFT|同 ART-U02|GLM-Art-A|一种货箱即可；拖具只按已核定能力制作，非模型决定规则|[工单][wo]|
|ART-TOOLS-01|本机可编辑工具链预检|P1|ACCEPTED|主控独立核验两文件/版本/源/hash，已修路径误判|GLM-Art-A（实际）|Python3.14.3/Godot4.7.2/.NET SDK10.0.401；已查路径未找到Blender；源/生成器/GLB存在且hash匹配。接受的是预检报告，生产流水线未核验|[报告](evidence/art-tools-2026-10-04.md)、[工具票](tasks/ART-TOOLS-01.md)|
|ART-BRIEF-01|首批美术需求与外包边界|P0|ACCEPTED|已确认画风与I00技术合同；architect审查的三项同步问题已修|Codex（实际）|六材质角色、驮运轮廓/载荷/接缝、太阳能四阶段、加工offline映射及比较条件；接受需求文档，模型/视觉效果仍NOT_RUN|[首批需求](requirements/first-assets-r1.md)|
|ART-PREVIEW-01|首批样件独立预览工具|P1|DRAFT|M01接受；主控补齐光照参数、材质接线与样件输入接口/独占目录|GLM工具槽；Codex图形验收|独立预览场景、固定正常/近景镜头、状态/建设阶段/空载有载对照；不复用I00固定AABB与四socket断言，不改Main/fixture；完整票尚未冻结|[首批需求](requirements/first-assets-r1.md)|

## 6. 批次与排批说明

- 第一批（服务 T3 与资源接入）：ART-U01、F01、F02、M01 优先（冻结事实点名）；ART-T01、T02 同属第一批范围，但正式接入被 T3 阻断（BLOCKED）。
- 第二批：ART-U02、U03、F03、F04、F05、F06、P01，须第一批导入／比例／实际成本与所有者效果通过后再展开。
- 最终排批、实际派单与单票冻结目录由主控决定；本文不启动任何生产。

## 7. 每行交付与验收基线

模型类按工单交可编辑源、真实GLB、清单、同镜头证据、两档细节或明确简化说明与实际验证记录；清单记录ID/revision、单位、导入AABB、原点/前向、适用连接点/动作/阶段、材质/面数/纹理、hash与来源。M01仅交材质资源与参数/引用说明，工具/文档票按自身范围；不要求所有资产都有机器人接口。技术验收由Codex负责，视觉效果验收由所有者负责；仅导入成功不等于效果通过，未运行写NOT_RUN。预算待实际整场测量固定。

文档外包交付与独立验收记录见[TODO-ART-01](../../workflow/tasks/TODO-ART-01.md)。当前状态只在任务表凭证据更新，不把本轮映射永久冻结。

[wo]: https://github.com/liuyejinghong/yudian-game/blob/171c610/docs/art/production/2026-10-04-t2-asset-work-order.md
[14csv]: https://github.com/liuyejinghong/yudian-game/blob/171c610/docs/art/production/2026-10-04-asset-tasks.csv
[i00]: art-i00-interface.md
[artdir]: ../art-direction.md
[takeover]: ../../engineering/reports/2026-10-04-takeover-verification.md
[pr16]: https://github.com/liuyejinghong/yudian-game/pull/16
