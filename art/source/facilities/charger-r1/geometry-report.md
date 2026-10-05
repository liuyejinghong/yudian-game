# charger-r1 · geometry-report（ART-F04-GEO）

由 `generate_charger_r1.py` 生成；本报告为末次真实运行的实录。

## 冻结范围与来源

- 米制，+Y 上，前 -Z，原点 = 地基底面中心；根节点 ChargingStation；落地 Y=0。
  单泊位仅首样比较，不是玩法容量/停靠合同；主体全部固定，无动画/漂浮/自转，无游戏状态。
- 包络 X[-.90,1.30] / Y[0,1.45] / Z[-1.10,1.10]。九个固定部件：深框 Foundation(1.80×.02×2.20)、
  CabinetFoot、ArmStem(X.71… .88)、PowerInGuard(柜后，前Z=-.66)、ServicePanel(贴柜X1.245)；
  浅色 Cabinet(底Y.08/顶1.40)、CabinetCap(顶1.45，不用大遮棚)；暖色 BayMark(X±.78 导向线+
  Z±1.0 端标，Y .020… .024)与 ContactHead(center .68/.57/-.35，受保护接触面 X=.65)。
- 接口回缩：未使用旧票候选头部面 X=.52；面 X=.65 与候选演示车 Charge 点(.48,.57,-.35，
  机器人根(0,.02,0))相距 .17m。仅安全待接静态示意，不宣称插合；开盖顶点净空由主控按
  现役 U01 开盖 charge 姿态实测。
- 两个 socket 为纯标记节点（无网格）：Socket_Dock=(.65,.57,-.35) forward -X/up+Y；
  Socket_PowerIn=(1.055,.18,-.66) forward -Z/up+Y。真实负载/电网/补能归技术线。
- 三角色 M01（body_light/frame_dark/accent_warm）只读同源线性占位 PBR；盒体逐面平法线。
  原创程序化几何（python 标准库），无第三方/版权/图像生成资产；F03 最小导出辅助已复制入本目录，
  无公共框架。

## 节点、角色与面数

节点 12 个：根 ChargingStation + 九个部件节点（Foundation/BayMark/CabinetFoot/
Cabinet/CabinetCap/ArmStem/ContactHead/PowerInGuard/ServicePanel）+ 两个 socket 空节点；
mesh 9，surface 9，三角面合计 144；
GLB 材质数 3（正式接线用共享 art-r1 材质）。逐 surface 角色明细与
socket 变换（glTF 四元数 xyzw，前向=局部 -Z 参考轴）见 geometry-facts.json。

## 包络与确定性（源/GLB 一致）

- 实测 AABB min [-0.9, 0.0, -1.1] / max [1.2999999999999998, 1.45, 1.1]，与冻结包络逐轴一致，min Y=0。
- SHA256：gltf `a59e46b6d7630874…`、bin `aca4b293daa6cef3…`、GLB（源目录）`84c4849bda02625c…`、
  prototype 私有副本 GLB `84c4849bda02625c…`（与源字节一致）。
- 确定性：同一运行内二次重建 GLB 字节一致（facts rebuilt_glb_identical）；跨目录重跑 hash 一致；
  无时间戳/随机数进 GLB。
- 独立回读：`--readback` 解析 GLB 原始字节（header/JSON/BIN 块），核根/节点、两个 socket 的
  平移与朝向（-X/-Z）、逐 surface 顶点 AABB、包络、面数、平法线轴向与材质数，与 facts 一致。

## 自检（本机 stdlib 实测）

- 写前自查：非有限数/负尺寸/未知角色/越界索引/翻绕序/超包络/缺或坏 socket 方向 → 具体异常
  非零退出，不落地任何文件；`--selftest` 六类负例全部被拒绝（PASS 明细见运行输出）。

## 本生成器未运行（NOT_RUN）

- Godot 4 导入、同镜头 normal/close/yaw0/90 空泊位图、U01(含开盖 charge 姿态)静态对位与
  顶点净空、引擎世界几何/角色核验：主控执行。机械插合、正式电网/补能/维修/导航/保存、STATE、
  占用逻辑、性能、所有者最终视觉：未制作。

重跑方式：`cd <任意目录> && python3 art/source/facilities/charger-r1/generate_charger_r1.py`
（重写本目录五件并同步 prototype 私有 GLB 副本）。本轮耗时 0.003s。
