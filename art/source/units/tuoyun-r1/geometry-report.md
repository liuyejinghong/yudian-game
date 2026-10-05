# ART-U01-GEO · 驮运机器人 r1 静态几何报告

**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 3与本轮实测见[mars-r2-report](mars-r2-report.md)。

**版本提示：本页保留r4/r5历史检查；2026-10-05当前r9资产、hash/计数/视觉边界见[state-report](state-report.md)。**

日期：2026-10-04。执行：GLM几何工人，主控局部后壁修复；独立接受：主控。
状态：**GEO ACCEPTED（主控r4技术范围；STATE/视觉仍NOT_RUN）**——只交本票源/GLB/报告，不写 ACCEPTED，不接 STATE。

> **r2 修订**（主控复核后）：检修腔真实内壁修正为 |X|=0.38（r1 报告曾把外墙 0.375 误写作
> 满足"≥0.38"）。冻结修法落地：外白壳 x 0.395..0.41（厚 0.015，中心 ±0.4025，头总宽仍 ±0.41），
> 深色内衬 x 0.38..0.395（厚 0.015，中心 ±0.3875），背衬宽 0.76（X±0.38）；墙衬贴合面 0.395 无共面穿插。
>
> **r3 修订**（第三/最后局部返修，主控实测两处缺陷）：
> 1. **GLB 不自包含**：export 曾把带 `buffers[0].uri` 的 .gltf JSON 原样写入 GLB chunk，主控全新工程
>    只放 GLB 时导入失败（"glTF Binary file notfound"），工人源目录旁置 bin 掩盖了该失败。
>    已修：GLB 用专用 JSON 副本**删除 uri**（自包含二进制块），.gltf 源保留 uri；未把 bin 复制到 prototype。
>    全新小工程（仅 GLB+project+脚本）重测：import 日志 0 error，audit PASS（证据 glb-only-*-r3.log）。
> 2. **SAT 失效**：旧 `sat_gap` 对每轴 `max(...,0)` 截断，永不返回负数——**r1/r2 报告中所有
>    SAT PASS / no-penetration 结论作废**，以本轮有符号 SAT 为准。重写为完整凸体 SAT（双方面法线 ∪
>    双方边×边轴，去重），三例自检（分离 2.0 / 接触 0.0 / 重叠 -0.5）证明可判负后，重跑全部静态配对
>    与盖板 1° 离散采样；采样暴露**真实穿透**（盖下折切入鼻块，最深 -0.0295），已按局部修几何（见下节），
>    未放宽任何断言。

## 交付物

| 文件 | 说明 | SHA256 |
|---|---|---|
| `generate_tuoyun_r1.py` | 原创生成器（Python 3.14.3 标准库，无第三方依赖） | `50cc2ebe7591252ed6ad3c883c9e93bb397587b3fd2c12c0843b92ac86f5f357` |
| `tuoyun-r1.gltf` + `tuoyun-r1.bin` | 可编辑分离源（glTF 2.0，bin 仅随 .gltf 使用） | `f4dec3458b448d28611b02eecc2ab7d76c0bd7d5f3afe0057d7e28237ca4a857` / `944a1dd21e4bd0ddb67a9b9b06e1c74729aad2d0dda360a8a37c8fc47320832d` |
| `tuoyun-r1.glb` | 真实 GLB（**自包含**，同步复制到 `prototype/assets/units/tuoyun-r1/`） | `dce2eb0bed0b6443e7ededefb59a48b09c9000566d8ae92e3cd85741bc06ccfc` |
| `geometry-facts.json` | 生成期实测（bbox/有符号SAT/1°采样/surface 表） | `d3394f175627f5ee82132db05205c31a3831cb7c66769605335c1577eb013b42` |

完整 hash 见证据目录 `source-hashes.txt`（r1）、`source-hashes-rework.txt`（r2）、`source-hashes-r3.txt`（r3，历史；当前主控r4以u01-geo-accepted/source-hashes.json为准）。
可复现性：连续两次运行输出**字节级一致**（diff 为空，已验证）；生成器不读任何共享源码，不自改。

## 坐标与变换

米制，+Y 上，前方 **-Z**，原点 = 空载静止最低接地点（轮缘 Y=0.0，Godot 实测 static AABB ymin=0.0）。
全部节点仅平移：Godot 导入后 `BAD_TRANSFORMS = []`（无旋转/缩放/镜像 basis，无负 scale）。
轮几何圆柱轴 = 节点局部 X（"圆轴局部 X、绕局部 X 滚转"约定），STATE 可直接绕各自局部 X 轴转轮。
三角形绕向：glTF 规范 CCW 正面 = 外向法线；生成器对**每个** box/prism/环面基元做符号体积断言
（外向 CCW 闭合凸体必为正），失败即拒绝产出。未用双面材质修反面（四占位材质均默认单面）。

## 冻结分区落实（需求表 → 实际）

| 分区 | 需求 | 实际 |
|---|---|---|
| 静态空载闭盖车体 | ≤1.2W×1.6L×0.9H，X±0.6 Z±0.8 Y0..0.9 | **1.172×1.60×0.72**（X±0.586 为轮毂缘、Z±0.80 保险杠/牵引端面、Y0.0..0.72 护栏顶）。未假称占满候选空间 |
| 四轮 | r0.22 宽0.16，心(±0.50,0.22,±0.50) | 24 边环形胎（外0.22/内0.155）+ 12 角星形宽毂（缘 0.1549，宽0.172 两面各凸 0.006）；轮心逐点相符；接地 Y=0.0 精确 |
| 轮上间隙 | ≥0.06 | 后轮上方护栏后段底 0.52（SAT 0.08）；前轮上方充电罩底 0.51（SAT 0.0798）；轮毂与侧伸板 SAT 0.012 |
| 前服务头 | Z-0.8..-0.25 高≤0.65，宽约0.85，顶部前下收 | Z-0.78..-0.25（前保险杠到 -0.80），高 0.65（=闭盖顶），宽 0.82；鼻封板 X±0.375、z-0.78..-0.76 薄前脸 + 35° 斜顶（0.5545→0.565，r3 后退让出盖下折扫掠）；暖鼻标带 Y0.48..0.525；深色格栅 ×2 |
| 检修腔 | 地板≤0.31，侧墙内 |X|≥0.38，禁白块填满 | 地板=头基座顶面 0.31；**真实内壁=深色内衬内沿 X±0.38**（衬 0.38..0.395、外白壳 0.395..0.41，实测顶点范围）；腔体 X±0.38、Z-0.72..-0.30（后衬面 -0.30）、Y0.31..0.58 中空无填充；背衬宽 0.76 匹配 |
| 开放货台 | X±0.42，台 Y0.48，Z-0.10..0.70，护栏≤0.72，净宽/净长≥0.80 | 台板 X±0.42 顶 0.48；护栏最高 0.72；净宽 0.84（护栏内沿 X±0.42）、净长 0.80（前块后面 -0.10 至后横梁前面 0.70）；无顶盖 |
| 货箱 | 0.64×0.70×0.38，底心(0,0.48,0.28)，顶 0.86 | 独立 `CargoBox` 节点（含深框+浅面+沿口三 surface），节点原点=底面中心；有载顶 0.86（Godot loaded AABB ymax=0.86）；与护栏/锁扣/头 SAT 净空 0.02..0.17，移除后车身完整（static AABB 由车体自身构成） |
| 后连接/锁扣 | Z0.70..0.80，独立锁扣+牵引耳 | `RearLock` 独立节点（accent 横杆 + 支撑耳）；前后牵引耳端面 Z±0.80 恰在静态边界内，接触面含 socket |
| 前顶盖 | pivot(0,0.64,-0.25) X 轴，0.70×0.02×0.50 沿-Z，闭顶 0.65 | `HoodLid` 节点 pivot 即规范值，闭合 Y0.63..0.65 / Z-0.75..-0.25；鼻封板后沿-.76，与闭盖前缘分离；实际GLB盖板-35…+70°共106个整数角样本已独立核无穿透（含后壁），端点Y约.345/1.1133；动画属STATE未交 |
| 侧充电盖 | — | `ChargeCap` 独立节点（浅色盖板贴充电罩外面 X0.465..0.495，罩住 Socket_Charge） |
| 轮毂分节 | t=0.25/0.75 相差 180° 画面须有区别 | 12 角星形毂半径 0.009..0.155 起伏 + 环形胎开口，绕 X 转 180° 后星形指向反转（几何上可辨；图形实录属 STATE） |

四 socket 为不渲染 Node3D（Godot 实测 children=0、无 MeshInstance），挂在 `Model` 下：

| 节点 | 需求位置 | Godot 实测 | delta |
|---|---|---|---|
| Socket_Cargo | (0,0.48,0.28) | (0.0,0.48,0.28) | 0.0 |
| Socket_TowFront | (0,0.24,-0.80) | (0.0,0.24,-0.8) | 0.0 |
| Socket_TowRear | (0,0.24,0.80) | (0.0,0.24,0.8) | 0.0 |
| Socket_Charge | (0.48,0.55,-0.35) | (0.48,0.55,-0.35) | 0.0 |

朝向：Cargo/TowFront forward -Z（Node3D 默认 -Z 即合同方向）；TowRear +Z、Charge +X 的
forward/up 记录在 glTF `extras` 与 geometry-facts.json，节点本身恒等旋转（STATE 按 forward 重定向，
本票不旋转节点以保 identity 约定）。牵引示意杆（0.35m）属 STATE，本票未新增任何游戏能力。

## node/surface 角色表

结构：`tuoyun-r1 → Model → {Chassis, Head, Body, HoodLid, RearLock, ChargeCap, Wheels→4×Wheel_*, CargoBox} + 4×Socket_*`。
几何节点 11 个（含分组节点共 18 Node3D + 4 socket），**55 个 surface**，每 surface 恰一个
四角色占位材质（Godot 实测无 null、无越界；逐行表见 `geometry-facts.json` 与证据
`godot-check-glb-gltf.log` 的 110 行 SURF 记录，GLB 与 .gltf 各 55）。

| 节点 | surface 数 | 角色 |
|---|---|---|
| Chassis | 16 | frame_dark ×16 |
| Head | 11 | body_light ×4（墙/鼻块）、frame_dark ×5（衬/格栅）、accent_warm ×2（鼻标/充电罩） |
| Body | 11 | body_light ×4、frame_dark ×7（台板/护栏） |
| HoodLid | 2 | body_light、accent_warm |
| RearLock | 3 | accent_warm、frame_dark ×2 |
| ChargeCap | 1 | body_light |
| Wheel_*×4 | 2 each | rubber（胎 192 tris）、frame_dark（星毂 44 tris） |
| CargoBox | 3 | frame_dark ×2、body_light |

占位材质仅承载角色分面（名称=角色名，PBR 参数近 M01 值）；正式接线由 preview 按
`res://assets/materials/art-r1/<role>.tres` 逐 surface 覆盖（M01 已接受，本票未动公共材质）。

## 生成期与导入期双实测

- 生成器自检（两轮均过）：全部基元正符号体积；18 组 SAT 净空对 gap≥0.005（最小
  cargo_vs_rail_front 0.02、wheelFL_vs_plinth 0.01）、2 组就位接触对无穿透；冻结尺寸断言 11 条全过
  （含净宽 0.84≥0.80、净长 0.80≥0.80、loaded 顶 0.86、接地 0.0、**检修腔内壁 |X|≥0.38**）。

## 盖板活动采样（r3，有符号 SAT，离散 1°）

在生成器自检内对 HoodLid 闭合位顶点绕 pivot(0,0.64,-0.25) X 轴做**离散 1° 采样**（fold 0..-35° 共 36
样本、open 0..+70° 共 71 样本；这是离散样本，**不是连续扫掠**，角度间未采样），每样本对
鼻/双侧墙/双侧衬/腔地板/前 transition 块做有符号 SAT，负间隙即失败。

| 方向 | 全样本最小间隙（出现角） | 端点 bbox |
|---|---|---|
| fold 0..-35° | **+0.0064**（-35°） | X±0.35, Y 0.345..0.648, Z -0.665..-0.244 |
| open 0..+70° | **+0.0100**（0°） | X±0.35, Y 0.637..1.1133, Z -0.430..-0.241 |

下折端点最低 Y0.345、上开端点最高 Y1.1133 与需求冻结论证一致；逐角度数值在 geometry-facts.json
`lid_sample_fold_1deg` / `lid_sample_open_1deg`。

**采样暴露的真实几何冲突与修正**（旧 SAT 因 max(...,0) 掩盖）：

- 现象：盖下折 -1°..-21° 前缘切入鼻块顶，最深 **-0.0295**（约 -4°，r2 鼻几何）。
- 根因：盖前缘绕 pivot 的扫掠圆弧（半径 ≈0.5001）在 z=-0.72 处已降至 y≈0.469——鼻块只要占据
  z≥-0.72 且顶高于 ≈0.47 就必然被扫到，降鼻顶不可行。
- 修正（局部，不动冻结 pivot/盖尺寸/整车包络）：鼻封板后沿从 z=-0.72 退到 **z=-0.76**（圆弧最远
  z=-0.7501，分离 ≥0.0099），鼻顶 0.565；同时鼻块改窄 X±0.375（消除与侧墙 X0.395..0.41 的立体
  共面重叠），鼻标带下移至 Y0.48..0.525（保持贴在斜面以下）。
- 视觉后果（如实记录，供主控/所有者复核）：闭合时盖前缘（底 Y0.63）悬于鼻封板顶（0.565）之上，
  前缘下有约 0.065 的可见台阶缝；正常镜头效果未验（NOT_RUN）。

静态配对（同一有符号 SAT 重跑，19 对全部真分离）：cargo 系 0.02..0.17、lid 系 0.01..0.065、
wheel 系 0.01..0.134、sat_lid_seated_on_walls 0.045（详 geometry-facts.json）。
SAT 三例自检实录：separate=+2.0、touch=0.0、overlap=**-0.5**（可判负证明）。

## 实际命令与结果

```
# 生成（幂等，两次 hash 相同）
python3 art/source/units/tuoyun-r1/generate_tuoyun_r1.py
→ OK static=[[-0.586,0.0,-0.8],[0.586,0.72,0.8]] loaded=[[-0.586,0.0,-0.8],[0.586,0.86,0.8]] tris=1512 bin=70992B

# r3：全新工程只含单 GLB（/private/tmp/yudian-u01-geo-gltonly-r3，无 bin 无旧缓存）
… --headless --path <gltonly工程> --import   # exit 0，import.log 中 error 计数 0（旧 "glTF Binary file notfound" 消失）
… --headless --path <gltonly工程> --script res://audit_geo.gd  # exit 0，AUDIT_RESULT PASS

# 导入 + 检查（临时空工程 /private/tmp/yudian-u01-geo-check1，GDScript-only，不碰仓库工程）
DOTNET_ROOT=<user-home>/.dotnet PATH="<user-home>/.dotnet:$PATH" \
  <project-root>/tools-bin/Godot.app/Contents/MacOS/Godot \
  --headless --path /private/tmp/yudian-u01-geo-check1 --import        # exit 0
… --headless --path … --script res://check_geo.gd                     # exit 0，双 PASS
```

检查脚本自带 30 秒自管超时（只终止自己启动的 PID）；全程 check-only，未下单/未改配置。
工具版本：Python 3.14.3；Godot `4.7.2.stable.mono.official.ed1daf0bf`。

## 成本与简化建议

- 三角面 **1512**；材质 **4**（占位角色，实际接线复用 M01 共享资源，GLB 自身 0 纹理 0 采样）；
  二进制 70,992 B。
- 简化方案（供 LOD/量产评估，本票未实施）：24 边胎→12 边（-96 tris/轮）、星毂 12 角→8 角
  （-16/轮）、检修腔内衬与格栅并面、护栏四段合并为整条。预计整件可到 ~900 tris，
  货箱/鼻标/开盖差异保留。

## 来源

本几何为项目原创：由本仓需求冻结表逐参数推导，用 Python 标准库（struct/json/math）程序化生成；
无第三方模型、无下载素材、无生成式服务、无旧 I00/校准件复制。

## NOT_RUN（本票范围外，未制作不冒充）

- 动画/七态/滚轮两帧实录、0°/90°/180° 方向拍摄（STATE 票，铰链 pivot 已预留）。
- 正常/近景镜头真实渲染、所有者视觉确认、可读性（PREVIEW-B + STATE 后）。
- preview wrapper、最终 canonical manifest、M01 材质真实接线（STATE）。
- 游戏接入、导航/碰撞/拖救/充电能力、性能预算与 LOD 实测。
- 盖板为**离散 1° 样本**（0..-35/0..+70，共 107 样本），非连续扫掠；逐帧动画、锁扣 25°/充电盖 60°/滚轮实录仍属 STATE，未制作不冒充。

## 冲突与门槛

无候选比例冲突需要放宽：全部冻结分区按原值落地，静态包络在候选内。若主控复核发现局部不符，
按票规走局部 REWORK，不自行改共同比例。


## 主控 r4 独立接受范围（当前事实覆盖旧r3采样范围）

工人6243ad4修正GLB自包含与signed SAT，但遗漏后壁。主控直接读取成品GLB顶点/三角面做完整SAT，实际35个负角样本与Head surface2后壁穿插（-35°最深约.02188m）。第三次返修后不再整票外包，主控局部把后白壁顶面从.63降到.60（中心Y.455/高.29），并将后壁/后衬加入生成期检查；盖/轮/货台/候选包络不变。鼻坡注释35°的数字笔误同步修为.005m水平差/.0035m高差，范围不变。

独立原始GLB位置/索引检查：signed SAT面法线与三角边叉积；分离/接触/重叠三控制能分别判正/零/负；-35到+70共106个整数角样本对全部Head surfaces、腔地板和前块，零失败。是离散采样，不声称连续扫掠。原始报告与检查脚本存 docs/art/production/evidence/production-r1-receipts/u01-geo-accepted/。

真正无旁置bin的全新Godot小工程导入/读取非空网格：11mesh、55surface、1512tri、4socket位置、空载±.586/Y0….72/Z±.8、有载顶.86通过，日志零ERROR/SCRIPT ERROR，成品GLB自包含。source两次生成字节级相同。当前hash以同目录source-hashes.json为准，旧r1/r2/r3短hash仅为历史。GEO接受只包含源/静态结构/这些有限采样；socket后方/+X旋转需STATE烘入源；动画/正式包装/材质接线/正常镜头/所有者仍NOT_RUN。

## STATE-A r5 修订（2026-10-04，本票 ART-U01-STATE-A：仅源动画与局部几何调整）

GEO r4 已接受的静态形体保留（后壁顶 Y0.60、检修腔真实内壁 |X|=0.38、盖 pivot (0,0.64,-0.25)、
轮参数、底盘/护栏/鼻部全部不动）。本节只记录 STATE-A 引入的源事实变化；wrapper/manifest/材质
接线/图形证据属 STATE-B，本片未做（见 state-source-report.md NOT_RUN）。

### 几何局部改动（仅服务冻结动作/示意杆/固定支撑）

| 项 | r4 | r5 | 原因 |
|---|---|---|---|
| CargoBox 总体外沿 | 0.66W×0.72L | **0.64W×0.70L×0.38H**（三层同心缩比） | 冻结值收敛；底心仍 (0,0.48,0.28)、顶 0.86 |
| RearLock | 横杆+双侧支撑耳整体可转 | 仅暖色横杆可转（accent_warm） | 冻结"只暖横杆动" |
| 锁扣固定支撑 | 随 RearLock 旋转 | **导向柱×2 移入 Body 静态**（X±0.21..0.25, Y0.50..0.65, Z0.76..0.80） | 让开横杆 25° 绕X扫掠；横杆转轴是X、x 范围不变，柱在 x 向分离 gap 0.01 |
| 横杆高度 | Y0.58..0.65（0.07） | **Y0.615..0.65（0.035）** | 25° 端点与后横梁（y≤0.60）/后护栏净空≥0.005；暖色可读性保留 |
| ChargeCap 厚度 | X0.465..0.495（0.03，嵌罩面） | **X0.48..0.495（0.015，内侧面=铰链平面）** | 开 60° 时全部角点 x≥0.48，铰链角原位接触不穿充电罩（GEO 闭位本就与罩共面贴合） |
| TowRod | — | **新增**（Z-0.80..-1.15, Y0.225..0.255, 0.03², frame_dark） | towed 私有示意杆沿前 socket -Z 0.35m；extras.visible=false（glTF 核心无 node 可见性，**Godot 导入实测 visible=true**，默认隐藏由 STATE-B wrapper 控制） |
| Socket_TowRear | identity, forward -Z | **rotation Y180° 烘入**，forward +Z | 冻结指令；audit 实测 fwd=(0,0,1) |
| Socket_Charge | identity, forward -Z | **rotation Y-90° 烘入**，forward +X | 冻结指令；audit 实测 fwd=(1,0,0) |

静态/有载包络不变：static ±0.586 / Y0..0.72 / Z±0.80，loaded 顶 0.86（TowRod 按"包络口径补清"
不计入静态/有载，杆属 towed 活动件）。面数 1512→**1524**（+柱24 −耳24 +杆12），surface 55→**56**。

### 新增 SAT（有符号，含有限运动端点）

净空对（≥0.005）：bar_closed/ear 0.01、bar_open25/ear 0.01、bar_open25/beam_mid **0.018768**、
bar_open25/rear_rail 0.017082、rod/bumper 0.095、rod/wheel 0.410383；
接触对（≥-1e-6）：ear↔beam 0.0、rod↔前牵引横杆 0.0、cap 闭/开60° ↔充电罩 0.0/-0.0、
bar_closed↔beam 0.015。货箱系净空 0.10..0.18。**端点姿态检查只覆盖 t=1（25°/60°）离散位形，
不称连续扫掠证明**；盖板 106 角离散采样（0..-35/0..+70，含后壁）原样保留且重跑通过。

### 动画（真实导出，六 clip，LINEAR 四元数轨）

| clip | 轨道 | keyframes | loop |
|---|---|---|---|
| move | 4×Wheel 绕局部X | t 0/.25/.5/.75/1 → 0/90/180/270/**360(双覆盖 q=(0,0,0,-1))** | 设计循环（GLB 无 loop 标志，见下） |
| work | RearLock 绕局部X | t0→0°, t1→+25° | 非循环 t1 定格 |
| charge | ChargeCap 绕局部Z（顶铰链 .48,.58,-.35） | t0→0°, t1→+60° 向+X | 非循环 t1 定格 |
| disabled | HoodLid 绕局部X | t0→0°, t1→-35° | 非循环；**towed 复用本 clip** |
| maintenance | HoodLid 绕局部X | t0→0°, t1→+70° | 非循环 t1 定格 |

idle = 源闭合静姿（无 clip）。**首尾四元数刻意不同**（q(360°)=(0,0,0,-1)≠identity）：LINEAR
插值据此才会真实转动——首尾同值会让插值恒为 identity，这是冻结"分段 quat 防 0↔360 同端点"的
根因；生成器自检含"旋转测试向量逐档核对 + 相邻档 dot>0.7 短弧"断言。

### r5 当前 SHA256（取代上表 r1/r3 历史值）

| 文件 | SHA256 |
|---|---|
| generate_tuoyun_r1.py | `41fccc7bbcb4294186f4e685dfec65b3064b3dd52e70afa1dcc494187bb945eb` |
| tuoyun-r1.gltf / .bin | `d49d638885f3fded2caec274d622591820911ef5c7847bebf7c854c614957ecf` / `caa7c3c41fd856a49ed5466b8d0d1e043c07e5ffddc10d2528381b9822edebcd` |
| tuoyun-r1.glb（=prototype 同步件） | `506c02ae21baff90cdfb446b7cb19a4a05e4456c65e4c36dcb02f4bbdd775922` |
| geometry-facts.json | `f1deb6a8f09f34bc0758863634e0c5d625bc26a476f8222f8454213150e55628` |

两次重跑 diff 为空（字节级一致，证据 source-hashes.txt）。GLB JSON buffer 无 uri；.gltf 源保留 bin。
真实 Godot 4.7.2 导入/clip 实测、命令与退出码见 state-source-report.md。


## 主控r6 · 当前源技术接受（2026-10-04）

2b2abd6原STATE-A提交已实际读取（1181秒，上下文used161065非计费；工人估40分钟不采用）。主控局部修轮胎144个逆向橡胶三角（端环96/内孔48），补按实体面方向独立判定；顶点/车体尺度未改。恢复后轮vs后侧伸板的正确轮位断言，原错误写成前轮。真实glTF move命名`move-loop`，按[Godot导入命名惯例](https://godotengine.org/article/importing-3d-assets-blender-gamedevtv/)并实际在4.7.2单GLB新工程导入核为`move` loop_mode=1，非循环四clip=0。没有只用loop_intent假声明。

主控bounded空工程导入/网格/clip读取三步exit0、无ERROR/SCRIPT ERROR，12 mesh/56 surface/1524 triangles/4 socket。实际5clip均1s、move quarter姿态不同/根固定；另t1四动作basis、实际货箱.64×.70×.38/socket位置方向通过。源重跑byte一致；轮面192三角物理朝向全部通过。固定close Metal原PNG实际看轮胎侧环闭合、没有旧扇瓣缺面。记录见 docs/art/production/evidence/production-r1-receipts/u01-state-a-accepted-r6/ 。

仅源/局部动作技术接受。wrapper/七态复位/非法保持/最终manifest与active包络、完整normal视觉/所有者效果/游戏/性能仍NOT_RUN，不能用static-only临时包装代签七态。原r1/r3/r5与工人45项报告为历史，当前hash如下。

| 当前文件 | SHA256 |
|---|---|
|generate_tuoyun_r1.py|`61f9e895a0aac0ee0fef4bea4b9c9c7f1aa5a64a9d9ee6ecb579fad244a92f38`|
|tuoyun-r1.gltf|`179a98f5f2c1a425f8729e1eaa3cef09dc2d97749d85fc9839c4baf5f90562f3`|
|tuoyun-r1.bin|`fc70ad7e3ec404abbdb2945aaf0e2fcedc15f95c62f59969870011f928821c90`|
|tuoyun-r1.glb|`d28c38b02ca298e427dc8562ecdcdd0d4785018b3aa6dc54e7136c523a18dac5`|
|geometry-facts.json|`b4e81faf07bfa8484a295fec3a66494f4666c9cc61ddb19d43f1917be8274086`|
