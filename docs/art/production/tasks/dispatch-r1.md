# 首样GLM派单与文件归属 r1

**F01当前版本：** revision3维护可见性片由主控直接完成，[新证据](../evidence/art-solar-maintenance-r3-2026-10-05.md)；本轮没有新GLM派单，父票仍REVIEW。


**当前入口（2026-10-05）：** 本轮三件火星结构优化已实际制作；源/GLB/manifest与独立Godot检查完成，父票均REVIEW，完整视觉/所有者接受仍NOT_RUN。 [火星优化切片](ART-MARS-R2.md)已执行；GLM无新版交件，主控完成并核验。下面是r1历史派单表，不是当前重派队列。

日期：2026-10-04。需求轮已完成；所有者随后已明确授权首批制作。下面按实际依赖与Bridge任务逐片推进，文本完整或获得授权仍不代表已经派出。权威父任务状态在[美术TODO](../todo.md)。所有票输入[首批需求](../requirements/first-assets-r1.md)、[预览合同](../requirements/preview-r1.md)、[验收表](../requirements/visual-acceptance-r1.md)和对应任务页。主控冻结视觉，GLM只制作/核对，不决定画风/规则或自写ACCEPTED。

## 顺序与最小派单切片

| 切片（每次只派一行） | 当前状态 | 输入就绪/解除条件 | 工作结果 |
|---|---|---|---|
| ART-M01 | ACCEPTED（资源） | 六角色与路径/参数；实际Godot入口已核 | 六.tres与README，资源加载核参 |
| ART-PREVIEW-01-A | ACCEPTED（技术） | M01资源接受且作为只读输入 | 独立场景、材质试板、样件加载/材质绑定/preset校验及test-only探针 |
| ART-PREVIEW-01-B | ACCEPTED | ed4f856+主控source.files/窗口局部修复；228/8/672与实机GUI通过 | 两镜头/方向/标签、GUI控制、确定时刻采集与JSON证据；真实图形验收 |
| ART-U01-GEO | ACCEPTED | M01与PREVIEW-A接受；几何不依赖B GUI | 一个驮运静态源/GLB、货箱/socket/surface表，候选比例核查 |
| ART-U01-STATE | 原r1技术通过；新版父票REVIEW | A源接受；B取消部分文件由主控完成 | 七态、包装、manifest与29张状态/方向/原因图，完整视觉待接受 |
| ART-F01-GEO | 技术通过 | 静态尺度/M01/PREVIEW已接受；主控制作 | 两次GLM仅推演已取消；原创折叠源/GLB/socket |
| ART-F01-STATE | 技术通过；父票REVIEW | 本件GEO/目录释放，主控制作 | 四阶段×七态、包装/manifest/22张图；完整视觉待接受 |
| ART-F02-GEO | ACCEPTED（技术） | GLM实际3e98c33与主控独立导入 | 腔/进出区/私有摆件/四socket |
| ART-F02-STATE | 技术通过；父票REVIEW | GLM源7f818c2→76121b5，主控包装 | 四展示态/七态声明、manifest/17张图；完整视觉待接受 |

2051d7f U01-GEO真实导入及[静态比例预核](../evidence/production-r1-receipts/u01-geo-static-proportion/review.md)已解除设施GEO前置，历史只解除GEO；当前STATE技术与三件关系图已完成，完整视觉接受另记，不回环阻塞U01。F01/F02分目录可并行；各资产GEO→STATE串行，PREVIEW-A→B串行，M01只一名写者。GEO不要求动画/最终manifest，不把未制作动作标N/A；状态票才交完整七态声明。工人完成一片后停止，不自动领取下一片。没有实际所需资源或预览依赖时，即使文本完整也不把生产片写READY。U01-GEO只做源/GLB与几何技术检查，M01/A已足够；B图形是STATE前置，此处经architect复核修正先前过重的串行约束，不代签B或样件视觉。

## 每次实际派单必须填的记录

主控把需求/本票已提交版本固定到实际base（本轮需求起点main `3999d30`，**不是未来永久base**），提供独立`worker/<slice>-<batch>`工作树绝对路径、输入commit/hash、唯一写者和目录、Bridge session/task/request ID、工具实际绝对入口、运行命令、输出证据新目录。缺实际参数不派单；执行授权另外核对，READY不会自动启动。

Bridge默认原生ZCode：在同一新会话先`/model GLM-5.3-Flash`确认，再派实际任务，不以dispatch的model字段假设生效；渠道不可用报告，不换成OpenAI子代理冒充GLM。五行结果：完成片/文件、真实命令结果、未跑/限制、commit、耗时/可见用量（未知成本写未知）。主控读diff与关键证据、独立复核后更新美术TODO；工人不改TODO或写接受。真实任务记录归本线evidence，不从历史会话冒领成果。

## 各票共同边界与交付

- 只写对应票列出的路径；禁写根PLAN/TODO、AGENTS、Main、`prototype/project.godot`/csproj/fixtures、旧I00/公共桥/注册表、`docs/engineering/`、其他资产、archive和公共材质（M01除外）。不改库存/电力/维修/通行/矿物/保存规则，不购资产、不装依赖、不联网取商业模型、不推送/合并。
- 工具：实际Python与Godot可用，Blender在已查路径未找到；默认现有Python标准库程序化生成glTF+bin/GLB，源+生成器要可编辑/重跑。若已合法可用Blender也可交.blend，但不能为票自行安装；路径与版本必须实查，不能沿用旧工具报告当当前事实。
- 模型GEO交源（.gltf+.bin+生成器或.blend）、GLB、geometry-report.md（轴向/原点、导入AABB、socket位置/朝向、实际node/surface角色表、源/GLB hash、三角面/材质/纹理数、原创来源、简化办法）。STATE保留并引用GEO源，补局部动画、`preview.tscn`/`preview.gd`、canonical manifest和真实检查记录；禁止用贴图/离线图替代GLB。
- GLB导入与材质绑定在独立工作树里运行新预览，显式场景启动，不替换Main。对已合法输入做纯接缝检查，对真实可达错误保留具体诊断；不存在的视觉证据写NOT_RUN。同接口两轮失败停止、保留失败资料，由主控缩票/修输入。
- 证据放新run目录，不覆盖；大图/视频提交与否由主控按实际大小处理，仓库至少留可复核路径/hash与文本结果，不虚构预算/识别率。所有提交只含本票路径与明确允许的报告；工人提供commit，主控决定集成。

## 给技术线的具体请求（不在本轮实施）

| 接口需求 | 依据与最低核验 | 谁决定/何时阻塞 |
|---|---|---|
| 新资产状态适配完整复位，允许自带动作；禁止I00工作旋转叠加 | 现VisualStateBridge仅复位WorkPart，Preview(work)直接RotateY；新样件有压头/顶盖/部署轴。需disabled→move、maintenance→idle与同输入幂等 | 技术线决定游戏适配方式；独立预览先行，正式接入被阻塞 |
| F01建设phase与运行state两维，局部phase_t或等价进度；F02offline仅映射disabled | 四建设阶段不等于发电状态，部署完成由权威事实给出；逐态往返不自行提交建设 | 技术线定合法组合/完成时序，不能从动画反推规则 |
| 载荷可见输入与外部原因显示 | U01 empty/loaded仅一个道具；回充move、零电/零耐久/both同七态可用原因注记；电量耐久两故障已确认 | 技术线定映射/能力，物资和健康事实只读；不新增模拟enum/阈值 |
| 机械socket与停靠/通行分开 | 本票socket是视觉点，Input/Output是料床中心；不是导航目标、载荷能力或标准接头 | U01/F01/F02真实对接时确认站点端姿态与可达空间，独立看样不等它 |
| 地形网格/边界/碰撞/导航/保存视觉接缝 | 2026-10-05主线c76b0ea：数据/纯网格/Godot候选/静态碰撞/单区内存提交受限接受；正式阶段、增量更新及渲染/物理/导航/保存同步未冻结 | T01/T02正式接入仍BLOCKED；不得重新称T3a数据合同未定 |

共享资源唯一写者由美术主控安排；公共游戏适配与注册由技术线持有。按需将此表交接，未授权不自动向另一个会话发消息。

局部依赖修正经architect复核：F GEO消费的静态尺度已实机核验，允许与U STATE分目录并行。静态比例预核并非U完整七态视觉或所有者验收；若车体/货台输入改变，重核相关F输入。

设施STATE小片进一步补清：各自GEO独立接受/固定节点pivot/目录释放即可STATE-A源动画，不消费完整U七态；STATE-B最终包装与单件/同场验收单独检查，A通过不解除或代签整体。原完整STATE行的U最终视觉条件保留在B比较门槛，不作为设施源动作的输入。

2026-10-05当前派单已结束；真实任务/提交/取消与主控完成部分见[首样记录](../evidence/art-first-samples-2026-10-05.md)。没有尚未冻结返修输入的READY票，不把视觉设计问题整包交GLM。
