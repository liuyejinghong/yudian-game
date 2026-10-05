# ART-U02-GEO · 筑垒 Blender 低保真首样

状态：REVIEW（真实GLM task_5c5b0d5c76交303f0bf，主控补实体铰座并重开/导入/132姿态/24图复核；STATE另DRAFT）。本轮所有者要求低保真优先，精细阶段留给Astra。本票只做静态可编辑形体与三种姿态快照；不把快照当七态动画或建设/维修能力实现。

## 目标与依据

沿用明亮专业模块化火星设备；与驮运的开放货台区别：筑垒有受保护后舱、前置折叠两段工具臂、两侧支撑足。参考art-direction中的施工/维护分工，具体机构是主控首样候选，不是航天承载/越障认证，不画炮口、挖斗、大风扇或人用驾驶室。

只读现役art/source/units/tuoyun-r1/tuoyun-r1.glb与其manifest、M01六角色、已验HF Blender脚本。保留原六轮/Rocker/Bogie完整子树、实际轴套连接与局部变换，用它作本票底盘候选；不要仅复制轮子并删悬挂。不继承旧动画PASS。导入后清所有原动画，删除货台、服务头、旧接口和其他原Body网格，保留Wheels子树及所需祖先空节点。重命名根为Zhulei，Model保持作者组织节点，导出后无额外root位移/补偿旋转，+Y上/前-Z、米制、Y0接地。

## 冻结几何与姿态（米制首样候选）

下列位置均在导出glTF模型坐标；Blender作者坐标映射复用已验world_position，不重复加90°。

| 分件 | 位置/尺寸 | 分区与连接 |
|---|---|---|
| FrameBed | center(0,.37,.10),size(.90,.14,1.25) | 深框；支承原摇臂pivot及新壳 |
| BodyShell | center(0,.48,.10),size(.90,.08,1.22) | 浅壳，下沿Y.44与frame顶面接合 |
| RearFloor | center(0,.57,.34),size(.74,.10,.70) | 深框，接BodyShell顶Y.52 |
| RearWallL/R | center(±.34,.69,.34),size(.06,.14,.70) | 浅壳；不把维修舱做整块实心盒 |
| RearWallFront/Back | center(0,.69,.02/.66),size(.62,.14,.06) | 浅壳；与侧墙接合 |
| ProtectedModule | center(0,.68,.34),size(.54,.12,.46) | 深框封闭内模块，底Y.62接RearFloor；不是裸露电子板 |
| HoodLid | pivot(0,.80,.68)，local center(0,0,-.35),size(.74,.03,.70) | 浅壳，闭盖下沿.785；maintenance_demo绕局部X+70° |
| ShoulderMount | center(0,.56,-.43),size(.38,.08,.22) | 深框；下沿接BodyShell，肩轴连装配座 |
| Shoulder/Elbow/Wrist | 肩pivot(0,.62,-.45)，elbow local(0,0,-.50)，wrist local(0,0,-.40) | X轴关节，肩半径.09/轴长.34，肘.075/.36，腕.06/.34；连接不同X车道的两杆 |
| UpperArm | 肩local center(-.09,0,-.25),size(.10,.10,.50) | 浅壳；沿局部-Z，偏X-.09 |
| LowerArm | 肘local center(.09,0,-.20),size(.10,.10,.40) | 浅壳；偏X+.09，折叠与上杆不共面穿插 |
| WorkPad | 腕local center(0,0,-.08),size(.34,.10,.16) | 暖标+深框接触块，平整施工接触意向，不定义新工艺/采矿/抓取能力 |
| SupportBeamL/R | center(±.585,.48,-.40),size(.31,.10,.10) | 深框，内端|X|.43接FrameBed，外端|X|.74止于套外面，不穿入滑杆孔 |
| SupportSleeveL/R | 空节点center(±.78,.57,-.40)，外截面.08×.08、高.42 | 四浅壳壁：localX±.034,size(.012,.42,.08)；localZ±.034,size(.056,.42,.012)，内孔.056×.056；底.36/顶.78。上端盖local center(0,.225,0),size(.08,.03,.08)，下沿接套顶，不封下端 |
| SupportSlideL/R | pivot(±.78,.30,-.40) | 子rod local center(0,.23,0),size(.045,.40,.045)，深框；子pad local0,size(.18,.06,.22)，暖标；work_demo pivotY=.03，足底Y0，杆仍插入套内 |
| ChargeHousing/Cap | housing center(-.455,.55,.34),size(.05,.16,.16)，cap center(-.484,.55,.34),size(.008,.12,.12) | 左后受保护服务面，housing内面贴BodyShell；避开前支撑，不假宣称适配F04现役对位 |
| TowFrontMount/Ear | mount center(0,.28,-.605),size(.18,.12,.23)，ear center(0,.24,-.76),size(.20,.06,.08) | 深框＋暖标；实体连续接frame前端与接点 |
| TowRearMount | center(0,.28,.715),size(.18,.12,.17) | 深框；连续接frame后端 |

原六轮最高Y.41/最外|X|.63；支撑足最内|X|.69，梁最低Y.43，避轮齿。FrameBed收窄到.90避免切入轮内环；足pad顶localY.03与rod底localY.03真实相接，空套内杆每侧留.0055m名义间隙。关节/套杆/舱壁有意装配接触与无关件穿插分开。

idle：肩X+55°、肘相对X-155°，盖0，足pivotY.30。work_demo：肩X-20°、肘相对X+5°，盖0，足Y.03。maintenance_demo：臂/足保持idle，盖X+70°，受保护内模块仍完整。WorkPad不是小枪头，正常镜头用折叠/前伸大形体区分工作准备。三姿态为确定性展示快照，root不动，不显示资源增减。

候选静态包络X±.90、Y0…1.14、Z±.84；demo总包络X±.90、Y0…1.52、Z[-1.60,.84]。实际超界或穿插须具体报主控，不擅自删部件/删检查。BOX可小倒角.003单段，避免微螺丝/装饰粒子/新纹理，保留粗大关节/分件。角色只用body_light/frame_dark/accent_warm，继承轮组可保留既有rubber防护角色；不把rubber覆盖金属轮。

连接点：Socket_TowFront=(0,.24,-.80)forward-Z，Socket_TowRear=(0,.24,.80)forward+Z，Socket_Charge=(-.48,.55,.34)forward-X，Socket_Service=(0,.69,.695)forward+Z；up均+Y。Socket_Work为腕子节点local(0,0,-.16)，forward局部-Z/up+Y，随示意姿态变。无Cargo接口：该GEO无货物区，不批准装载能力；未来接入需求另交技术线，不争写I00合同。

## 独占交付、禁写与案例

仅写art/source/units/zhulei-r1/、prototype/assets/units/zhulei-r1/。交build_sample.py、可编辑zhulei-r1.blend、idle zhulei-r1.glb、zhulei-work.glb、zhulei-maintenance.glb、geometry-facts.json和≤60行geometry-report.md；三GLB私有运行副本源字节一致。脚本支持--output在指定独占目录重建，不导入私人源绝对路径；默认只定位repo现役U01。facts列真实三姿态角色/层级/面数/包络/socket/hash及原创来源。

GLM先实际核Blender4.5.14 CLI、bpy与导出，日志记实际结果；二次重开.blend导出结构/索引/浮点≤1e-6核对，GLB字节差异如实记。可以复制HF已有小helper，不建公共框架。Blender CLI可用，禁止GUI/Godot图形/整游戏编译、联网安装插件/依赖。禁写现役U01/HF/F01/F02/P01/F03/F04、M01/公共预览/manifest公共表、根PLAN/TODO/AGENTS、Main/project/fixture和工程/产品合同；不得push/merge/领取下一票或让自检代签视觉。

正常：六轮及悬挂完整、足/臂/舱有实体连接，idle/work_demo/maintenance_demo包络和角色正确；源重开可编辑。异常：缺输入、缺Wheels/六Wheel、未知角色、非有限网格、空输出具体失败；留一份最小可运行检查，不能回退旧件。主控独立核实际Godot导入/5接口/三姿态及固定normal/close×yaw0/90图；再采样工作/开盖变化核具体非连接件净空。完整七态/复位/动画、游戏建设/维修/电力/通行/保存、工程可靠性、整场性能和所有者完整视觉均NOT_RUN，STATE另DRAFT；GEO REVIEW不完成母票。

architect前置：已修frame轮内环穿插候选、杆/足1cm间隙、支撑套实心问题，冻结肩座。实际顶点仍须制作后核，未以计算候选代签实机。

收口返修冻结：HoodHingeMount center(0,.78,.68),size(.28,.04,.06)，frame_dark，底接RearWallBack顶Y.76；HoodHingeAxle center(0,.80,.68)，X轴radius.022/length.36，frame_dark；两件parent Model，轴与盖板后缘相接。此装配接触有意保留，盖板与其它舱体仍做开盖采样。首版盖与舱壁25mm间隙缺承载为真实缺陷，不以空pivot代替铰座。主控执行该确定性两件补建，保留GLM原提交，不改原任务作者。

实际交件：8416tri/50mesh/66surface/5socket；18张U02＋6张U01参照实机图。源与运行副本/hash相符；正常距离工具端仍不可确认可读，完整视觉未接受。证据与作者见[本轮记录](../evidence/u02-r1/README.md)。
