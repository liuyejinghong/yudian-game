# ART-F04-GEO · 充电站单泊位静态首样

状态：REVIEW（真实GLM交件83f7987，主控集成f41478a；实际Godot导入/61开盖采样/12实机图已核）。根ChargingStation；米制、+Y上、前-Z、地基底面中心原点。单泊位只用于首样比较，不代表玩法容量。保留明亮/专业/模块化火星设备方向，不做地球油枪、人用厂房/空气风扇或漂浮三柱。

## 目标与冻结输入

只读M01三角色body_light/frame_dark/accent_warm，现役U01及art/manifests/tuoyun-r1.json。U01空载原包络宽1.26m/长1.6m，Socket_Charge=(.48,.55,-.35)forward+X。本样以机器人根(0,.02,0)作候选演示，接口高度变为.57；只是静态对位，不批准电网、机械插接或停靠姿态。

| 部件/节点 | 冻结几何（米） | 角色/目的 |
|---|---|---|
| Foundation | center(0,.01,0), size(1.80,.02,2.20) | 深框，地基Y0… .02 |
| BayMark | X±.78两导向线，Z±1.0端标；线宽.035，Y.020… .024；不挡入口 | 暖色取放方向标记，不是导航目标 |
| CabinetFoot | center(1.055,.04,-.35), size(.49,.08,.62) | 深框，落地且与地基接合 |
| Cabinet | center(1.055,.74,-.35), size(.38,1.32,.50) | 浅色封闭电气柜，底Y.08/顶1.40 |
| CabinetCap | center(1.055,1.425,-.35), size(.42,.05,.54) | 浅色保护顶，不用大遮棚挡泊位 |
| ArmStem | center(.795,.57,-.35), size(.17,.08,.10) | 深框固定导向，接入柜体 |
| ContactHead | center(.68,.57,-.35), size(.06,.12,.16) | 暖色受保护接口，面X=.65；不画裸露闪电/插入车身 |
| PowerInGuard | center(1.055,.18,-.615), size(.12,.12,.09) | 深色柜后外置接口护块，贴柜Z=-.60，前面Z=-.66 |
| ServicePanel | center(1.254,.81,-.35), size(.018,.60,.34) | 深色外侧检修板，内面贴柜X1.245 |

包络X[-.90,1.30],Y[0,1.45],Z[-1.10,1.10]。Socket_Dock=(.65,.57,-.35),forward-X/up+Y；Socket_PowerIn=(1.055,.18,-.66),forward-Z/up+Y，位于落地脚后服务面；两个点仅视觉标记，真实负载/电网由技术线拥有。architect发现旧候选接口头与现役U01 charge开盖+60°的实体穿插，已将接口回缩。候选示意中Dock与车Charge点相距.17m；仅安全待接位置，不宣称插合。验收同时加载现役开盖charge姿态，核实际顶点净空，不只看idle。固定柜体不移动/转圈；无线缆贯穿车体。

## 归属、交付与禁写

独占art/source/facilities/charger-r1/和prototype/assets/facilities/charger-r1/。只交Python标准库生成器、可编辑glTF/bin、同源GLB、geometry-facts.json/geometry-report.md（≤60行）。事实列节点/surface角色、两个socket变换、包络/面数/hash/原创来源；GLB运行副本字节一致。可复制已有F03最小导出辅助到独占目录，不造公共框架或依赖其私人输出路径；box使用逐面平法线，避免共面表面覆盖。

禁写U01/HF/P01/F03、共享M01/预览/manifest公共表、根PLAN/TODO/AGENTS、Main/project/fixture与产品/工程合同。禁装依赖、运行GUI、派下一票、push/merge。主控独立做对位示意、Godot导入、同镜头normal/close/yaw0/90空泊位与U01演示图，工人不代签。

## 案例、验收与依赖

正常：Y0落地，柜/脚/杆有明确连接、接口不进机器人；裸泊位可辨，三角色分区，源重跑GLBhash一致。异常：非有限数、负尺寸、未知角色、坏索引、超包络、缺/坏socket方向具体拒绝；留一个可运行自检覆盖上述非法输入。主控读实际GLB并核Socket_Dock朝-X、PowerIn朝-Z、引擎世界几何/角色/包络/演示净空与实际PNG。

前置：现役U01/M01及冻结候选已就绪；不等整个游戏或U01最终高保真。STATE/停电/充电/占用/活动插接、正式电网/补能/维修/导航/保存、所有者完整视觉、性能均NOT_RUN，下一片另票DRAFT。此GEO不自带U01资源副本，不把示意占用等同模拟事实。

本轮证据：[交付与独立验收](../evidence/dual-r2/README.md)。144tri/9mesh/9surface/2socket，三角色；61采样中非地面件AABB均分离，最小分离轴间距.0412564m。近景柜体/泊位明确，normal空有差异可见、接口细节弱；仅静态GEO进入REVIEW，不代签完整视觉或STATE。
