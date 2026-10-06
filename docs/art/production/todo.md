# 《余电》美术待办清单

更新日期：2026-10-06
初版：GLM按TODO-ART-01整理，基线`06f70c9`；维护与接受：Codex。历史批次art-brief-r1基线`9a89601`；历史需求补齐从main `3999d30`独立分支推进。2026-10-06本轮依据已合并 PR #38重评待办，基线`0662c126cd9d966c8e085ed00a0ff72880841063`；仅文档重评，不制作新素材、不启动派单、不进行新的游戏或视觉验收。当前重评输入见[美术执行包](../../roadmap/art-work-packages.md)；历史需求输入见[补齐计划](PLAN.md)与[本轮记录](evidence/art-requirements-2026-10-04.md)。

## 1. 本轮范围与边界

2026-10-06仅重评下文任务；以下保留2026-10-05制作轮的授权与证据边界，不视作本轮已执行或新派单。

- 机器人／设施等已冻结模型子批已完成独立制作／技术验证并进入REVIEW；游戏素材仍不齐，环境／资源／标识缺口见第10节。整批看样见[交付页](evidence/lowfi-batch-r1/README.md)。最新选择是先低保真，检查后由Astra做高保真；本轮不继续HF，不实现正式地形或游戏规则。
- 首批制作已获授权；有实际执行才写IN_PROGRESS；GLM必须有真实Bridge任务，主控制作另记执行者。记录独立工作树、文件归属、提交和运行证据。
- 不创造成本／预算事实：三件实际面数已记录；游戏性能预算未实测；工单代理体尺寸只是旧代码事实。[首批需求](requirements/first-assets-r1.md)的配色/比例是可修改的样件起点，不是已通过视觉验收的生产规范。
- 美术不决定产矿、通行、发电、救援等模拟规则；连接点与动作名的存在不代表能力、载荷或回收方式已获准。

## 2. 状态与优先级

允许状态：`DRAFT / READY / BLOCKED / IN_PROGRESS / SUBMITTED / REVIEW / REWORK / ACCEPTED`。是否获准执行、是否合并另行说明；未真实派单不写IN_PROGRESS。优先级与[总TODO](../../../TODO.md)统一使用P0/P1/P2。

## 3. 已接受事实与未验边界（2026-10-05证据保留）

- **ART-I00 = ACCEPTED**：仅技术校准通过；PR #16已合并；视觉验收未做（NOT_RUN）。旧清单的「待核定」不覆盖本结论；校准件是原创尺度标尺，不是驮运／设施正式模型。
- **ART-T01／ART-T02 = BLOCKED**：当前造型参考已交，不能作为正式阶段资产。历史只读核至84371c3：PR34单机到场／持续作业／整平提交与后帧验证，以及此前T3a数据、T3b/c纯网格、Godot适配／候选预览、静态碰撞／候选联调和T3d单区域内存提交均仅限定接受。解除条件改为[合同](../../roadmap/contracts.md)与[DT-A03](../../roadmap/art-work-packages.md)的实际接缝：T01须冻结D1.0／C01／E02的区域边界、采样、阶段和版本同步；T02须冻结E10开挖阶段、暴露层及相同边界／版本接缝。A03按这些数据接入表面／标记，E04（开挖后E12）保存同源事实。无需笼统等待整个T3、所有导航或经济完成；正式阶段／增量投影、碰撞／通行／保存一致性仍须工程实测，未验项不据文档解除。
- M01六资源与PREVIEW A/B已接受；U01/F01/F02真实源、GLB、包装和canonical manifest已制作并独立运行核验，旧版技术核验仍有效；所有者反馈后，三件本轮火星结构已重制/独立核验，父票均 **REVIEW**，等待完整视觉与所有者接受。详见[本轮火星优化](evidence/art-mars-models-r2-2026-10-05.md)与[原首样交付](evidence/art-first-samples-2026-10-05.md)。所有者已给出“效果还不错”的局部反馈，完整所有者验收仍NOT_RUN；当前新授权下P2按实际子片冻结，不再全体等待首批最终接受。
- ART-TOOLS-01原预检已接受，报告当时未找到Blender；本轮已独立安装/核SHA并实测Blender4.5.14，U01实验源能重开/导出/导入。完整高保真生产流水线尚未通过。
- ART-BRIEF-01接受范围是需求文档，本轮补齐r1不代表资产或视觉已通过；ART-PREVIEW-01已定光照/接线/样件输入，A/B工具技术与图形已独立接受。它们不是新增游戏资产种类。

**最新局部优化：** F01 revision3已实际制作/复验，检修盖抬起后close0/90可见内罩；392姿态/10非法保持/16维护Basis及111角净空通过，10张新实机PNG。主控直接完成这一视觉局部调整，本轮无新GLM派单。[证据](evidence/art-solar-maintenance-r3-2026-10-05.md)。三父票仍REVIEW，所有者“效果不错”只记录为总体局部反馈，不代签完整验收。

**最新质量反馈（2026-10-05）：** 所有者认可风格，但明确指出模型精度/细节偏低保真。三件现阶段属于程序化结构/动作首样，不是高精度成品；未制作高模、未做UV贴图生产/高低模烘焙。父票继续REVIEW，下一关包含精度提升与验收，不能描述成仅剩看样。所有者已确认U01精度样板与低保真批次并行并授权执行；精度样板已交可编辑Blender几何试片，尚未达到最终高保真目标。见当前双线子票，旧首样未代签接受。

### 火星适应性返修（现有父票的切片，不新增资产种类）

[约束r2](requirements/mars-equipment-r2.md)与[切片](tasks/ART-MARS-R2.md)已执行。新版三件真实源/GLB/manifest、539姿态/30非法保持/24轮姿态及33张实机PNG已完成；源和STATE技术已接受，父票均REVIEW。两GLM真实源票无交件取消，主控完成全部本轮制作。F01随后已局部更新revision3，内罩在固定close0/90可见，normal三方向成对图已复看；工程仿真/完整盲比/所有者新版接受NOT_RUN。[证据](evidence/art-mars-models-r2-2026-10-05.md)。

## 4. 拟执行槽（将来角色，非已启动 agent）

| 槽 | 持有任务行 | 说明 |
|---|---|---|
| GLM-Art-A | ART-U01、U02、U03、P01；首批U01后可接F02 | 同一槽按票串行，独立单位/设施目录 |
| GLM-Art-B | ART-F01、F03…F06 | F01可与另一个工人的F02并行 |
| GLM-Art-T | ART-T01、T02 | 地形样件 |
| GLM-Material | ART-M01 | 共享材质单一写者 |
| Codex（主控） | ART-I00公共合同／集成、ART-BRIEF-01需求 | 技术验收Codex；视觉效果验收所有者；ART-PREVIEW-01拟给GLM工具槽 |

实际派单要求：独立工作树、单票冻结目录；不得两个agent同时编辑同一源、场景入口或资源注册表。ART-TOOLS-01已由GLM-Art-A做只读预检；不由其自行领取材质或模型下一票。

## 5. 原始任务表（17行；当前执行子票另列）

原始 14 行清单见[清单@171c610][14csv]；其旧状态列按本轮实证更新，不覆盖当前任务行。P0记录已验接口，P1为首批样件/工具前置，P2为后续资产队列；当前授权按逐片真实前置排批，不统一卡在首批最终视觉接受。

| ID | 任务 | 优先级 | 状态 | 前置 | 拟执行槽 | 交付 / 验收 | 来源 |
|---|---|---|---|---|---|---|---|
|ART-I00|模型接入约定（技术校准）|P0|ACCEPTED|无（已完成技术验收）|Codex（公共合同／集成）|已交付可编辑 glTF+bin 源、生成脚本、GLB 校准件、manifest 与独立预览场景；技术验收已通过，视觉验收未做（NOT_RUN）；PR #16已合并。校准件不是驮运／设施正式模型|[接口合同][i00]、[接管复验][takeover]、[PR #16][pr16]|
|ART-T01|整平土坡样件（原坡／施工中／整平后三阶段）|P1|BLOCKED|ART-TOOLS-01／I00技术接口；D1.0的C01／E02区域边界、表面采样、阶段与版本接缝待冻结；A01正常相机、A03表面映射|GLM-Art-T|三阶段同一局部边界、无裂缝；源文件＋阶段 GLB＋同镜头对照图；不实现矿物结算。参考造型可先行；冻结上述接缝后按E02接入、E04重载复验；不等整个T3，未代签导航／碰撞／保存|[工单][wo]|
|ART-T02|开挖矿点样件（未开挖／部分／已开挖三阶段）|P1|BLOCKED|I00及T01同源边界／采样；E10开挖阶段、暴露层与版本接缝待冻结，A03按真实数据映射|GLM-Art-T|三阶段可辨截面与矿层、统一边界；新增前后状态不自动成为可挖储量，E10绑定真实几何／矿量，E12保存；碰撞／通行／重载由工程另验，不等所有导航／经济完成|[工单][wo]|
|ART-U01|驮运首样|P1|REVIEW|M01/PREVIEW已接受；新版源与STATE技术通过|主控本轮制作；GLM源票取消无交件|7312三角面/109面/4socket；六金属轮/摇臂/保护模块，98姿态+24轮姿态+10非法保持通过；完整视觉/所有者NOT_RUN|[U01票](tasks/ART-U01.md)、[本轮证据](evidence/art-mars-models-r2-2026-10-05.md)|
|ART-F01|太阳能首样|P1|REVIEW|尺度、M01/PREVIEW已接受；新版源与STATE技术通过|主控制作；GLM源票取消无交件|888三角面/74面/2socket；四建设阶段×七态，392姿态+10非法保持通过；revision3维护近景内罩可见；完整视觉/所有者未接受|[F01票](tasks/ART-F01.md)、[本轮证据](evidence/art-mars-models-r2-2026-10-05.md)|
|ART-F02|加工首样|P1|REVIEW|同F01；独立processor-r1目录|原r1 GLM源；本轮主控制作|1224三角面/33面/4socket；新增料口遮护/内罩/外露板，49姿态+10非法保持通过；offline仅disabled；完整视觉/所有者未接受|[F02票](tasks/ART-F02.md)、[本轮证据](evidence/art-mars-models-r2-2026-10-05.md)|
|ART-M01|共享基础材质组|P1|ACCEPTED|六角色/参数/资源目录/验收已冻结，已装Godot可用；不依赖T3或模型|GLM-Material（单一写者）|六个可编辑.tres和参数/引用说明；主控独立加载与核参；不要求GLB。Bridge真实提交1b91467，集成6b6198b，独立加载/核参通过；主控独立白昼试板PASS，所有者最终配色NOT_RUN；[生产记录](evidence/art-production-r1-2026-10-04.md)|[M01票](tasks/ART-M01.md)、[首批需求](requirements/first-assets-r1.md)|
|ART-U02|筑垒机器人|P2|REVIEW|当前Blender增补／五clip／五接口已实际验证|真实GLM源＋主控包装／独立验收|8584tri；七态含静态候选towed，work臂／足及维护／充电盖可复位；所有者最终NOT_RUN|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-U03|望山机器人|P2|REVIEW|新Blender源／五clip／五接口及实机图已齐|真实GLM-A源＋主控独立验收|7544tri；桅杆、封闭横头、六轮与扫描／低头／维护状态候选；不产生地图信息|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-F03|仓储设施|P2|REVIEW|源增补／两clip／三接口与12箱显隐已验证|真实GLM-B源＋主控独立验收|1312tri；向外折门避箱，work静态开放取放面；未接真实库存|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-F04|充电站|P2|REVIEW|源增补／三clip／两接口及有限净空已验证|真实GLM-B源＋主控独立验收|824tri；工作板／停机挡板／维护盖；接口仍.17m待接，不代签补能|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-F05|维修站|P2|REVIEW|新源／三clip／三接口与维修关系图已齐|真实GLM-A源＋主控独立验收|1348tri；开放工位／工具挂轨／维护与停机；维修不补电|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-F06|着陆器|P2|REVIEW|新源／三clip／三接口与货箱显隐已齐|真实GLM-A源＋主控独立验收|2444tri；四足／封闭主舱／前货舱；不增加起降或人口模拟|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-P01|货箱与回收连接件|P2|REVIEW|Blender静态源与独立图已交；正式拖救契约仍BLOCKED|真实GLM-B源＋主控独立验收|48tri货箱／132tri候选回收杆；两端接口不批准载荷／谁可拖谁|[整批交付](evidence/lowfi-batch-r1/README.md)|
|ART-TOOLS-01|本机可编辑工具链预检|P1|ACCEPTED|主控独立核验两文件/版本/源/hash，已修路径误判|GLM-Art-A（实际）|Python3.14.3/Godot4.7.2/.NET SDK10.0.401；已查路径未找到Blender；源/生成器/GLB存在且hash匹配。接受的是预检报告，生产流水线未核验|[报告](evidence/art-tools-2026-10-04.md)、[工具票](tasks/ART-TOOLS-01.md)|
|ART-BRIEF-01|首批需求与外包边界补齐|P0|ACCEPTED|当前明确需求授权；独立审查与文档检查见本轮记录|Codex主控；GLM仅清单核对|保留r1角色/镜头/候选尺度，补形体状态/设施尺寸/预览合同/视觉标准/小票；接受文档，资产/视觉NOT_RUN|[首批需求](requirements/first-assets-r1.md)、[验收方法](requirements/visual-acceptance-r1.md)、[记录](evidence/art-requirements-2026-10-04.md)|
|ART-PREVIEW-01|首批独立预览（A→B串行）|P1|ACCEPTED|M01真实接受；光照、材质surface接线、样件输入/目录已冻结|GLM工具槽；Codex图形验收|A加载/绑定/复位与异常已接受（149项、独立8项与672姿态）；B ed4f856+主控来源校验返修，228项/原8项/672姿态/真实Metal采集与原生窗口操作通过；只接受工具，probe非正式资产、所有者视觉NOT_RUN|[PREVIEW票](tasks/ART-PREVIEW-01.md)、[合同](requirements/preview-r1.md)|

## 6. 历史批次与排批说明

- 第一批（服务 T3 与资源接入）：ART-U01、F01、F02、M01 优先（冻结事实点名）；ART-T01、T02 同属第一批范围，但正式接入被当时T3接缝阻断（BLOCKED）；当前具体解除条件见第3／5节。
- 2026-10-05已交低保真整批：[ART-LF-BATCH-01](tasks/ART-LF-BATCH-01.md)已冻结模型子批候选并实际派单：A制作U03/F05/F06，B补U02/F03/F04源动作、P01与三版地表参考；主控包装/manifest/固定实机比较。既有三首样保留；正式游戏与T3接入另验。
- 原批次顺序见[历史派单索引](tasks/dispatch-r1.md)；当时顺序/目录/验收见[双线任务包](tasks/ART-DUAL-R1.md)。首批三件仍REVIEW；本轮后续优先顺序见第10节，不启动历史队列。

## 7. 每行交付与验收基线

模型类按工单交可编辑源、真实GLB、清单、同镜头证据、两档细节或明确简化说明与实际验证记录；清单记录ID/revision、单位、导入AABB、原点/前向、适用连接点/动作/阶段、材质/面数/纹理、hash与来源。M01仅交材质资源与参数/引用说明，工具/文档票按自身范围；不要求所有资产都有机器人接口。主控独立技术/可读性核查，所有者最终视觉效果确认；仅导入成功不等于效果通过，未运行写NOT_RUN。预算待实际整场测量固定。

文档外包交付与独立验收记录见[TODO-ART-01](../../workflow/tasks/TODO-ART-01.md)。当前状态只在任务表凭证据更新，不把本轮映射永久冻结。

[wo]: https://github.com/liuyejinghong/yudian-game/blob/171c610/docs/art/production/2026-10-04-t2-asset-work-order.md
[14csv]: https://github.com/liuyejinghong/yudian-game/blob/171c610/docs/art/production/2026-10-04-asset-tasks.csv
[i00]: art-i00-interface.md
[artdir]: ../art-direction.md
[takeover]: ../../engineering/reports/2026-10-04-takeover-verification.md
[pr16]: https://github.com/liuyejinghong/yudian-game/pull/16


## 8. 双线历史子片 · 2026-10-05

此节保留整批收齐前的历史切片状态；其中STATE DRAFT与“仍需制作”均是当时描述，不是当前待制队列。已交低保真制作／STATE结果以第9节及整批交付为准；未完成的高保真范围保留，本轮不启动。

**当时优先级：先低保真，检查通过后由Astra做高保真。** HF成果保留，本轮不继续HF/MAT/烘焙，不启动Astra制作；低保真整批验收/交接门槛见[当前计划](PLAN.md)。

**当时制作工具：两线统一Blender。** 新模型及视觉返修交.blend＋Blender Python脚本＋GLB；低保真/精细线区别为细节预算。保留已交源，按实际返修迁移；GLM下一票先实际核Blender CLI/bpy/导出。工具可用不代签模型品质，详见[双线任务包](tasks/ART-DUAL-R1.md)。

只读工程main7f6667e，单区显示/静态碰撞同步受限接受；正式通行/导航/save未实现。旧ART-LINK-U01描述仍是工程线历史接入待办，不覆盖美术分支已经存在的源交付。

| 子票 | 状态 | 当前执行者/前置 | 完成边界 |
|---|---|---|---|
| [U01-HF-BOOT](tasks/ART-U01-HF-BOOT.md) | REVIEW | 主控；历史r2工具试片16504tri/28mesh/4socket/16图通过；当前形体r3见HF-SHAPE | 工具与静态几何试片已交，正常镜头提升有限；整车高保真未接受 |
| [P01-CRATE-GEO](tasks/ART-P01-CRATE-GEO.md) | REVIEW | ZCode原生GLM-5.3-Flash；task_94dc7ef663 / sess_796e07dcb4；独立工作树实际派单 | 仅共用货箱静态几何，不包括回收件 |
| [F03-GEO](tasks/ART-F03-GEO.md) | REVIEW | GLM源eb024eb/e14a907；主控修共面/独立Godot756tri/12箱位/8实机图 | 仅固定仓储/示意货物，不是库存系统 |
| [U01-HF-SHAPE](tasks/ART-U01-HF-SHAPE.md) | REVIEW | 主控r3：19504tri/46mesh/4socket/16旧新实机图 | 近景层次更清楚，normal收益有限；完整高保真尚未通过 |
| U01-HF-MAT/STATE/QUALITY | DRAFT | A1实机看样后冻结UV/材质接线/完整动作与质量标准 | 最终高保真尚未制作/接受 |
| [F04-GEO](tasks/ART-F04-GEO.md)/STATE | REVIEW / DRAFT | GLM源83f7987/f41478a；144tri/9mesh/2socket/12实机图，61开盖采样无穿插 | 安全待接位置；插合/STATE未制作，补能/停靠由技术线验证 |
| [U02-GEO](tasks/ART-U02-GEO.md)/STATE | REVIEW / DRAFT | GLM303f0bf/集成4036dc6；主控补铰座；8416tri/50mesh/66surface/5socket/132采样/18新图＋6参照 | GEO不代签七态或建设/维修能力 |
| F05-GEO/STATE | DRAFT | 主控冻结拖入与维护包络 | 建设/维修能力由技术线拥有 |

已验证：旧三件技术/图形、HF实验源重开/GLB导入/4接点/16张旧新实机图，以及货箱48tri/4张新图、仓储756tri/12箱实际落位/8张新图；已冻结候选：货箱/仓储包络与形体。NOT_RUN：整车高保真UV/烘焙/完整动画、最终配色/完整所有者接受/游戏集成/整场性能/同任务两工具速度比较。后续以真实派单、提交和独立检查更新，不能以此计划替代资产交付。

第一片独立取证（历史）：U01几何试片16旧新对照图＋货箱4图＋仓储8图。仓储首版遮挡、r2货箱共面伪影均保留失败版本；主控局部修正后normal0/90空有差异可见，近景箱顶伪影消失。当前GEO均REVIEW，需完整视觉确认；P01连接件/设施STATE与正式玩法接入未交。作者和版本见[双线证据](evidence/dual-r1/README.md)。

第二片实际执行：主控[U01-HF-SHAPE](tasks/ART-U01-HF-SHAPE.md) REVIEW（r3源/真实导入/16图已核），GLM[F04-GEO](tasks/ART-F04-GEO.md) REVIEW（静态源/实际导入/61开盖采样/12图已核）。[当前证据](evidence/dual-r2/README.md)，GLM会话已结束。不将之前所有者局部认可写成最终视觉接受；完整HF与游戏接入保持未完成。

当前新增低保真U02：GEO REVIEW，完整STATE DRAFT；[实际证据](evidence/u02-r1/README.md)。正常镜头仅确认大轮廓差别，工具端/最终视觉未接受。仍需F05维修、U03望山、F06着陆器几何，P01回收连接件及F03/F04/U02等必要状态、关系预览与所有者整批看样；高保真后续Astra未开始。

## 9. 已交低保真模型子批 · 2026-10-05

[整批票](tasks/ART-LF-BATCH-01.md) **REVIEW：独立制作与技术检查齐套，等待所有者整批视觉确认。** 保留旧U01/F01/F02，完成11份新增／增补源与21真实clip，该模型子批票面项目已交，不覆盖环境／资源／标识缺口。[整批看样／证据](evidence/lowfi-batch-r1/README.md)，[技术交接](evidence/lowfi-batch-r1/technical-handoff.md)。

| 子包 | 真实派单／提交 | 交付及接受依据 |
|---|---|---|
| A | Agent Bridge / ZCode原生GLM-5.3-Flash；task_01db3e6d19前置→task_23fb772900制源→task_9090cf51f7返修→task_390ccb5412交件；sess_2a8c5b12d7；0d14aa0→7252fdd | U03／F05／F06.blend、真实clip与GLB；主控独立源重开、导入与实机验证 |
| B | 同原生GLM；task_2add7873a6制源→task_c5350f9518返修→task_4ad95558a9交件；sess_fedb9781d9；69b5f4a→ee91dbd | U02／F03／F04增补、P01两件、三版地表参考；源与运行副本一致 |
| 主控 | Codex；独立美术分支art/production-r1-20261004 | 包装／11manifest／完整Godot导入参数、637姿态＋110非法＋12缺源保持、108选定净空、284单体＋3关系实机PNG；architect审查无新增阻断 |

**本批已验证**：11 Blender源真实重开／导出，21源clip和根静止／角色／地表公式；无缓存Godot导入、冻结姿态／接口／显隐复位；同档镜头与M01接线、当前SHA和实际图一致。

**候选仍需整批看样**：正常距离大岗位轮廓可分，但小盖／接口／微动作辨识有限；固定close大设施会裁切，完整形体以normal三方向核。配色、尺寸、动作节奏不是最终视觉规范或机械载荷认证。

**尚未完成**：所有者最终视觉NOT_RUN；正式Main接入／联调／整场性能与LOD／物理导航／保存NOT_RUN；正式T01/T02、回收载荷／规则BLOCKED，三版地表仅参考。高保真／UV／贴图烘焙／Astra制作NOT_RUN。该模型子批票面没有未交源文件，游戏级素材覆盖仍缺环境／资源／标识，不能把被技术接缝阻断的正式资产改成ACCEPTED。

工程main只读核至84371c3且干净，单机整平闭环已限定接受；美术未修改工程、根PLAN/TODO、Main或公共fixture。后续技术线按第10节具体前置和交接选择正式注册／阶段网格／动画驱动接入，再按实际游戏镜头验证；正式接入不代签整批所有者视觉接受，Astra高保真另获授权。

## 10. 当前重评任务表 · 2026-10-06

**素材尚未齐全。** 第9节“已齐”只指模型子批。soil_mars仍是纯色材质，三版地表仅参考；通用箱和三块深色岩石不能代替六类资源、独立地标和操作标记。依据[覆盖盘点](requirements/asset-coverage-2026-10-05.md)、[独立效果审查](review/2026-10-05-independent-review.md)、[美术执行包](../../roadmap/art-work-packages.md)和[接口合同](../../roadmap/contracts.md)重评；以下七行是这些新增／缺口任务的唯一权威状态记录，执行包只作范围与接缝说明。

正常游戏视距先于近景；制作、独立技术检查、正式接入、版本内验收四维证据分记，所有者视觉接受单独记录。现有normal只是参考镜头；本轮没有新素材、游戏运行或新的视觉验收，七票均DRAFT，不能因路线已写好自动READY。READY前须冻结具体写入路径、输入、消费者和验收样本，实际派单后才记IN_PROGRESS。

| ID／任务 | 优先级／状态 | 真实前置与用途 | 拟写入边界 | 工程接入者／包与版本内变化 | 来源 |
|---|---|---|---|---|---|
| ART-CAMERA-01 实际工作相机 | P0／DRAFT | DT-A01；C01／E01初始视口与现役manifest；先定位尺度、可点击性、遮挡及地表可读性，可与UI骨架并行 | 尚待冻结专属参考记录／独立对照场景；主场景相机、灯光、UI和共享材质由Codex单写 | Codex，D1.0／E01／E03／C02；普通游戏viewport消费正常／总览／近景配置，记录窗口像素、UI占用与版本，所有者看样 | [DT-A01](../../roadmap/art-work-packages.md)、[审查](review/2026-10-05-independent-review.md) |
| ART-ENV-SURFACE-01 地表成果表面 | P0／DRAFT | DT-A03；A01镜头与C01／E02边界、采样、阶段／版本；自然土、作业面、压实面帮助读懂真实整平 | 尚待冻结独立表面资源／映射目录；形式按用途选简单材质、顶点／世界坐标或必要纹理；不改权威高度场、M01或Main | Codex，D1.0／E02／E03／C02与E04保存；同地点原始→作业→完成→取消保留成果可读，投影与重载同源；E10／E12再补开挖暴露层 | [覆盖盘点](requirements/asset-coverage-2026-10-05.md)、[DT-A03](../../roadmap/art-work-packages.md) |
| ART-MARKER-01 选择与工程标记 | P0／DRAFT | DT-A03；A01视距，E01选择／命令及E02合法范围、提交／取消／阻塞语义；与表面并行 | 尚待冻结独立标记资源目录；贴地、非红绿唯一通道；不改规则或共享注册表 | Codex，D1.0／E01／E02／E03／C02，E04保存相关权威事实；真实选择、合法／非法／已提交范围及阻塞反馈进入游戏，取消不抹成果 | [覆盖盘点](requirements/asset-coverage-2026-10-05.md)、[DT-A03](../../roadmap/art-work-packages.md) |
| ART-U01-CARGO-FIX-01 内置载荷局部候选返修 | P0／DRAFT | DT-A02a；当前canonical源／审查支持共面解释，未运行复现；只修箱身或重复顶面，保留外包络、Cargo socket、轴与状态 | 已有[源生成器](../../../art/source/units/tuoyun-r1/generate_tuoyun_r1.py)296–301行；完整源／运行GLB／manifest／包装写入清单尚待冻结。不得扩大到独立P01-CRATE或重做整车 | Codex，E03／C02；同相机正常空／载、载货近景90／180及维护回归。D1.0若未消费仅记制作；后续E07实际载货接入才记版本效果 | [局部发现](review/2026-10-05-independent-review.md)、[DT-A02](../../roadmap/art-work-packages.md) |
| ART-F02-READABILITY-01 加工形态与朝向 | P1／DRAFT | DT-A02b；F02 rev2、A01正常0／90／180；E10加工用途与工作／停机状态明确后强化进出料和一个工艺特征，保持防护逻辑 | 已有[源目录](../../../art/source/facilities/processor-r1/)；局部源／GLB／manifest／包装写入清单尚待冻结，不改M01或新增大型工厂 | Codex，D1.1／D1.2的E03／E10／C02，与A07状态反馈联合；实际加工时朝向、进出料及工作／停机可读，不因未到加工阶段额外阻塞D1.0 | [逐项审查](review/2026-10-05-independent-review.md)、[DT-A02](../../roadmap/art-work-packages.md) |
| ART-RESOURCE-01 六类资源表示 | P1／DRAFT | DT-A04；E05资源ID、单件／批量视觉语义、载荷／箱位／出料包络和A01视距；铁矿、铜矿、铁料、铜料、结构件、线缆 | 尚待冻结独立资源源／GLB／必要材质图标／manifest目录；容器复用P01-CRATE，不增加第七条工业链 | Codex，D1.1子片／D1.2完整，E07／E10／E13／C02；真实取送加工中类别一致，数量仍由账本UI给出，不用数箱代替库存 | [覆盖盘点](requirements/asset-coverage-2026-10-05.md)、[DT-A04](../../roadmap/art-work-packages.md) |
| ART-ENV-PROP-01 有限自然定位物 | P2／DRAFT | DT-A08；E16未知／已知映射、地面接合和允许区域尺度；少量岩块／露头与可辨自然轮廓，定义明确可先行 | 尚待冻结独立环境源／GLB／manifest／标识目录；不新增遗迹、水冰、生物等玩法地点 | Codex，D2.0／E16、D3.0／E21与各批C02；两处可操作场地能定位，普通岩石不误报矿点、未知信息不泄漏，装饰关闭不改真实障碍 | [覆盖盘点](requirements/asset-coverage-2026-10-05.md)、[DT-A08](../../roadmap/art-work-packages.md) |

优先前移A01／A03：D1.0先由E01／E02／E03／C02消费相机、成果表面和操作标记，E04同时保存新增事实；U01局部返修可独立冻结。六类资源和F02随D1.1／D1.2实际取送／加工进入版本，有限环境定位物服务D2。正式T01／T02仍BLOCKED，具体解除接缝见第3／5节，不笼统等整个T3或所有导航／经济。

其余执行包映射到已有父票，不另维护动态状态：DT-A05→F01／F02／F03／F04／F05的实际建设子片（F06仅初始出货）；DT-A06→F04／F05服务子片及P01／被服务单位的救援子片，服务与救援分别冻结；DT-A07→U01／U02／U03现有状态及真实原因反馈；DT-A08→ART-ENV-PROP-01。DT-A09须正常镜头、低保真轮廓／尺寸／接口认可、关键局部问题关闭、相关真实行为成立与高保真启动授权；DT-A10须生产切片结果和已批准内容清单。Astra高保真不在本轮启动。

保留已交模型、原ACCEPTED／REVIEW限定范围与全部证据；完整低保真素材里程碑尚未达成。本表是待办重评，不代替写入合同、派单、资产交付或所有者验收。

## 11. 美术目录公开发布 · 2026-10-05

所有者已授权上传并合并美术文件夹。公开副本只带美术变更，技术目录保持main版本；[发布核验](evidence/publication-r1/README.md)记录路径清理、13份Blender重开、独立导入／228自测和历史证据hash边界。素材仍REVIEW，完整低保真素材里程碑仍未达成。
