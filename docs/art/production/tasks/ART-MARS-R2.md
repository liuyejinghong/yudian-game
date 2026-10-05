# 火星适应性返修 · r2切片

2026-10-05。优化前父票U01设计REWORK，F01/F02仍REVIEW；r9及旧设施技术成果/图像保留。输入：[火星设备约束](../requirements/mars-equipment-r2.md)、[首样基线](../evidence/art-first-samples-2026-10-05.md)、[预览合同](../requirements/preview-r1.md)。以下首表保留冻结前DRAFT记录；当前执行结果见票尾。

| 切片 | 目标与独占目录 | 状态 / 缺少前置 |
|---|---|---|
| U01-MARS-GEO | 重新设计金属轮/抓地面、六轮候选的悬挂/轮拱、受保护服务头；保留货台参照。仅`art/source/units/tuoyun-r1/`与对应prototype GLB | DRAFT：主控尚未冻结轮位/摇臂pivot/轮面截面/底盘局部让位和温控舱分件；不让GLM替主控做选型 |
| U01-MARS-STATE | 在接受的新源上补全轮组move轨/动作复位、manifest和真实预览 | DRAFT：等GEO接受及目录释放；同资产源/包装/manifest目录串行独占 |
| F01-MARS-DETAIL | 核电器箱、铰链接线防护与面板维护可达空间；保持双折和阶段/状态。仅solar-r1源/包装/manifest | DRAFT：服务路径与防护分件需主控冻结；清扫工具/动作未获定义，不自动新增 |
| F02-MARS-DETAIL | 受保护工艺/电气模块、料口防尘与保温/散热路径；保持可见加工件。仅processor-r1源/包装/manifest | DRAFT：模块用途、遮护边界和分件未冻结；不默认高压舱/冶炼/制氧工艺 |

禁写：其他资产、公共M01/预览工具、Main、工程规则/合同/fixture、根PLAN/TODO/AGENTS。不新增温度/积尘/能耗/载荷/维修/导航或保存计算；不安装依赖、购买资产、付费调用或推送合并。

GEO交可编辑生成器+glTF/bin/自包含GLB、分件/角色表、静态/有载/指定活动包络与结构说明。STATE/DETAIL另交同步manifest/私有包装、源clips实测与同镜头PNG；每片仅SUBMITTED，主控独立检查再释放目录。记录真实Bridge session/task/base/commit，不能冒领旧任务。

正常案例：货台/箱体接口未被轮组侵入，受保护模块与开放作业区有清楚边界，源新增轮轨与实际节点一一对应，四phase/七态/空有载重复切换无残留。异常案例：仅加轮却穿底盘、摇臂不接轮心、轮轨只转旧四轮、密封外罩挡住装卸/状态件、维护打开后把电气当裸空腔、自动除尘/空气冷却/加压工艺缺解释，均返修；不得放宽检查来通过。

验收：先导入/面绑定/源轨/包络/socket与有限规定姿态空间，再看正常镜头识别与近景结构，并逐项回答火星约束。未验证的轮轨/工程性能/清尘效果/所有者视觉写NOT_RUN；DRAFT不为了排期写READY。新的实际参数冻结后才提供完整可派小票。

## 本轮实际制作入口

所有者已授权继续优化，参数见[制作冻结](../requirements/mars-equipment-r2.md#2026-10-05-制作冻结--本轮候选结构)。旧DRAFT表是冻结前记录；本轮执行片如下，源片不等待GUI。源完成仅SUBMITTED，不能自签视觉。

- **U01-MARS-SOURCE：ACCEPTED（源技术）**。包含GEO与已确定的六轮源move轨；独占`art/source/units/tuoyun-r1/`与对应私有GLB。保留其余四源clip/socket/货箱，更新针对新增结构的自检和`mars-r2-report.md`，交glTF/bin/GLB/facts。主控按新源做STATE包装/manifest验收，工人不写该manifest或公共预览。
- **F01/F02-MARS-SOURCE：ACCEPTED（源技术）**。同一设施工人按F01→F02串行；独占两个设施各自source及对应私有GLB。只加冻结保护分件，不改四phase/七态/clip名/箱体或服务盖pivot。各交`mars-r2-report.md`与源/GLB/facts，主控更新manifest及取证。保留生成器必要断言，不改公用检查。
- **STATE：ACCEPTED（技术）；视觉：REVIEW**。新版实际导入、逐surface绑定、539姿态/30非法保持及24轮姿态已通过，33张同条件实机PNG已归档；完整视觉/所有者接受仍NOT_RUN；F01随后revision3已解决指定近景遮挡，最新记录见票尾。

禁写范围同上；本轮运行仅Python源生成/离线自检，GLM不得运行Godot GUI、导入编译、性能测试或修改美术TODO。完成后仅提交自己获配路径，报告commit与未跑检查并停止。

真实派单：[Bridge记录](../evidence/mars-r2-receipts/bridge.json)。两会话均先切GLM-5.3-Flash成功，再派冻结源片；未用原生OpenAI代理冒充GLM。

## 本轮收口

本轮三件火星结构优化已实际制作；源/GLB/manifest与独立Godot检查完成，父票均REVIEW，完整视觉/所有者接受仍NOT_RUN。 [证据](../evidence/art-mars-models-r2-2026-10-05.md)。两真实GLM源票只推演无交件，807/591秒后取消并结束会话；没有GLM新版commit。主控完成全部冻结源/manifest与独立运行，不用历史交件冒领本轮成果。源目录已释放，当前无活动GLM任务；不自动开始P2。

## F01维护可见性 r3 · 已执行局部片

目标：外盖打开后在冻结近景看清内罩。输入230342f/F01 revision2；独占solar-r1源/私有GLB/canonical manifest，主控直接修改（视觉角度选择），没有新GLM派单。禁写其他模型、公共M01/预览、Main/工程规则/fixture及根PLAN/TODO；没有开始P2。交源码/GLB/manifest新hash、111角净空、392姿态与16维护源Basis、idle/maintenance成对normal/close实机PNG。

源终点X -110°、pivot保留；活动最大Z采样1.84021695，候选上限1.85。正常案例：close0/90内罩显露，四phase/七state与复位保持。异常：盖板/把手碰撞指示机构、越旧包络而不记录、只改包装动作不改源、单件改光/相机或冒用旧hash，均拒绝。依赖：F01 r2真实源与冻结预览已经具备；技术与指定近景检查通过，父票REVIEW。完整盲比/所有者、工程可靠性和性能仍NOT_RUN。

实际结果与边界见[本轮证据](../evidence/art-solar-maintenance-r3-2026-10-05.md)；上一节遮挡结论是r2历史版本，r3指定近景已解决。
