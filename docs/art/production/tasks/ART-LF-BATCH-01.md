# ART-LF-BATCH-01 · 当前低保真批次收齐

状态：REVIEW。所有者2026-10-05要求持续完成当前低保真批次；11份新增／增补Blender源、21真实局部clip、逐件包装／manifest与287张统一Godot实机图已齐，独立技术验证通过。39e9618与旧三首样保留；完成制作不代签所有者视觉、正式游戏／T3接入或高保真。实际派单、来源、计数与限制见[交付记录](../evidence/lowfi-batch-r1/README.md)。

米制，+Y上/前-Z；模型作者根静止、Y0接地。所有后续建模用已安装Blender4.5.14，交.blend/构建脚本/真实GLB/facts。M01六角色沿用，不造纹理/粒子/空气风扇/司机舱/武器。配色、比例和机构是首样候选，不是工程载荷认证。U01/F01/F02既有canonical源、动作与manifest保留；本批转换/增补新目录，不覆盖它们或共享fixture。

## 子包A：三件新增几何与Blender源动作

独占 `art/source/lowfi-batch-r1/new/` 与 `prototype/assets/lowfi-batch-r1/models/` 下 wangshan/repair/lander三个同名GLB。仅源目录内build_samples.py及三个子目录.blend/.glb/facts/report；复制已验小helper，无新公共框架。只读U01/U02源与现有Blender脚本；禁写其它模型、包装/manifest、公共M01/预览、Main/project/根PLAN/TODO/AGENTS/产研合同。可在临时无C#工程做headless导入/动画核验，不运行GUI、不push/merge、不自签视觉。

### U03 望山（wangshan）

来源：art-direction§5传感器分工、product-definition§5勘测；本票只表现扫描准备，不产生矿点发现/地图信息。复用现役U01 Wheels/Rocker/Bogie及祖先变换，移除旧Body/货箱/头/接口/旧动画，与U02一样保存完整六轮金属底盘。

| 部件 | glTF坐标米制候选 | 连接/角色 |
|---|---|---|
| FrameBed | center(0,.37,.10),size(.90,.14,1.25) | frame_dark，避轮内圈 |
| BodyShell | center(0,.53,.10),size(.90,.22,1.22) | body_light，下沿接frame顶.44 |
| SensorBase | center(0,.69,-.08),size(.32,.10,.32) | frame_dark，下沿接壳顶.64 |
| Mast | center(0,1.005,-.08),size(.12,.53,.12) | body_light，底.74接Base顶.74 |
| MastHeadMount | center(0,1.28,-.08),size(.24,.04,.22) | frame_dark，接Mast顶1.27 |
| SensorYaw | pivot(0,1.30,-.08) | 绕Y扫描，root不动 |
| SensorPitch | SensorYaw子pivot local(0,.32,0) | X倾角，轴radius.045/length.76与支承叉相连 |
| SensorHead | pitch localcenter(0,0,-.08),size(.60,.22,.30) | body_light；宽横向封闭传感器头，不做人头/雷达锅 |
| SensorFace | pitch localcenter(0,0,-.237),size(.48,.12,.014) | frame_dark，贴头前面-Z的封闭深色传感窗 |
| HeadFork | yaw子左右X±.35,Y.20,Z0；size(.05,.40,.10)，底桥localcenter(0,.03,0),size(.76,.06,.16) | frame_dark，底桥接HeadMount，两支架接pitch轴 |
| RearModule | center(0,.75,.42),size(.58,.22,.42) | frame_dark封闭模块，底.64接壳 |
| ServiceLid | pivot(0,.86,.64),panel localcenter(0,0,-.22),size(.64,.025,.46) | body_light；X+70维护，补实体hinge mount/轴接模块与盖 |
| ChargeHousing | center(.455,.55,-.32),size(.05,.16,.16) | frame_dark，内面接壳；盖top铰点(.484,.61,-.32)，closed中心(.484,.55,-.32),size(.008,.12,.12) |
| TowFront/Rear支座 | 沿用U02实体耳/支座尺寸与位置 | frame_dark/accent_warm，不批准拖救能力 |

Socket_TowFront(0,.24,-.80)fwd-Z；TowRear(0,.24,.80)fwd+Z；Charge(.48,.55,-.32)fwd+X；Service(0,.75,.65)fwd+Z；Sensor(0,0,-.245)为SensorPitch子随头指向的标记，fwd局部-Z/up+Y。无Cargo。idle宽横头水平、yaw0；move六轮X滚转一秒循环，头不扫描；work yaw从-35到+35再回-35，两秒循环，SensorPitch保持0；disabled yaw0/pitchX-35°低头；charge右盖绕Z+60°向外，头不扫；maintenance后盖X70°，传感头保idle；towed复用disabled定格，候选牵引附件由包装单独显隐。源内至少move/work/charge/disabled/maintenance五真实clip，子件驱动。静态候选X±.65/Z±.84/Y0…1.80，维护总Y≤1.85/拖杆Z≥-1.15。

### F05 维修站（repair）

依据：维修不赠电，零耐久需他机送回；设备是开放维修工位，不是雷达或地球汽车车库。单工位与工具机构只用于候选比较，不代表游戏容量/机械工艺。U01以root(0,.04,0)作关系示例，其盖开70°仍须留净空。

| 部件 | 米制候选 | 连接/角色 |
|---|---|---|
| Foundation | center(0,.02,0),size(3.4,.04,3.2) | frame_dark，Y0落地 |
| PortalPosts | X±1.45/Z±.90，centerY1.13,size(.12,2.18,.12) | frame_dark，底.04接地基；内净宽2.78 |
| PortalCrossbars | Z±.90，centerY2.25,size(3.02,.12,.14) | body_light，下沿接柱顶2.22，交叠仅装配 |
| SideRails | X±1.45,Y2.25,Z0，size(.12,.12,1.80) | body_light；无整片屋顶遮机器人 |
| BayGuides | X±1.0,Y.045,Z0，size(.035,.01,2.4) | accent_warm，入口-Z；不生成导航/碰撞规则 |
| CabinetFoot/Body | center(-1.60,.05,.90),size(.50,.10,.70)；body center(-1.60,.75,.90),size(.40,1.30,.60) | frame_dark脚/body_light柜，底相接，不用地球风扇 |
| CabinetCap | center(-1.60,1.425,.90),size(.44,.05,.64) | body_light，接柜顶1.40 |
| ToolTrack | center(1.05,1.61,-.35),size(.80,.12,.16) | frame_dark，外端接TrackHanger；新增hanger center(1.45,1.93,-.35),size(.12,.64,.12)，上下接Track与SideRails，不悬空 |
| ToolSlide | pivot(1.05,1.60,-.35)；横滑X1.05… .85 | 下挂杆localcenter(0,-.30,0),size(.10,.60,.10)，工具块localcenter(0,-.65,0),size(.30,.10,.20)，accent_warm+frame_dark |
| ServiceCover | 柜+X面，pivot(-1.395,1.25,.90)，panel localcenter(0,-.35,0),size(.018,.70,.40) | frame_dark，Z+70°外翻；有实体铰座/轴，打开仍为封闭模块 |
| StopGate | pivot(0,.045,-1.52)，panel localcenter(0,.17,0),size(2.0,.34,.035) | accent_warm；idle X-90平放外侧/disabled X0竖起；两个落地轴座+X轴承，不漂浮 |

Socket_Bay(0,.04,0)fwd-Z/up+Y是机器人根示意；PowerIn(-1.60,.18,1.255)fwd+Z（封闭外护块接柜后）；Service(-1.385,.90,.90)fwd+X（服务面）。不定义拖运姿态/恢复值。work ToolSlide X=1.05-.20*sin²(pi*t/2)，Y保持1.60，工具下挂杆不脱离滑座，0/1/2秒X=1.05/.85/1.05分段线性，2秒循环；端头最内X≥.70，与现役U01外轮.63仍分离，靠近候选工作区不假装已修数值。idle工具停靠，disabled停工具+竖StopGate；maintenance柜盖外翻70；move/charge/towed N/A固定维修设施，充电功能不擅加。clips work/disabled/maintenance。外包络候选X[-1.85,1.70],Z[-1.90,1.60],Y[0,2.32]，service打开可至X-1.10但避候选机器人。

### F06 着陆器（lander）

依据：art-direction§6小基地组成、产品建设/运输用途；此票为已着陆封闭设备与货物舱候选，不追加起降模拟、人口/穹顶或自动发物资。

主舱CoreBus center(0,1.55,.25),size(1.60,1.60,1.40)，body_light，倒角.05/2segments；底部承架center(0,.70,.25),size(1.40,.10,1.20)，frame_dark，顶.75接主舱底.75。四足中心(X,Z)=(±1.40,±1.40),Y.05,size(.40,.10,.40)，frame_dark/accent_warm；斜撑从(±.65,1.15,±.65)到(±1.40,.15,±1.40)，截面/直径.12，用两端连接实际主舱与足座，补下轴座到足顶.10。不把姿态/落地动作用纯空节点顶替结构。

前货物区-Z：Floor center(0,.82,-.78),size(1.20,.08,.90)，背面Z-.33接舱前面Z-.45（结构装配重叠）；左右墙center(±.62,1.17,-.78),size(.08,.62,.90)，顶部梁center(0,1.50,-.78),size(1.28,.06,.90)，frame_dark/body_light。LanderPayloadDemo子下一个现役P01箱，底(0,.86,-.83)，尺寸.64×.38×.70，Z[-1.18,-.48]；与CoreBus前面-.45有.03m间隙，货舱地板Z[-1.23,-.33]承托完整箱。盖为top pivot(0,1.54,-1.265),localcenter(0,-.34,0),size(1.22,.68,.04)，实体X铰轴radius.025/length1.28；前横梁center(0,1.50,-1.22),size(1.28,.06,.08)接侧墙和铰轴。work X+90°抬盖，露货物区，不当斜坡、不演凭空卸货。四斜撑上端更精确：front=(±.65,1.15,-.40)、rear=(±.65,1.15,.90)，都在主舱截面内，不以薄货舱外墙承载；足端对应Z±1.40。

RearModule center(0,1.30,1.015),size(.70,.50,.15)，frame_dark，贴主舱后Z.95；ServiceLid top pivot(0,1.55,1.10),panel local(0,-.25,0),size(.68,.50,.025)，body_light，X-70°向后外翻；补实际铰座。StopPlate pivot(0,2.35,.25),localcenter(0,.12,0),size(.55,.24,.025)，accent_warm；idle X-90折平顶，disabled X0立起，轴接主舱。work货盖0→90一秒非循环；maintenance后盖-70；disabled关货盖/升StopPlate，其他不动；move/charge/towed N/A已着陆固定设备。clip work/disabled/maintenance，cargo empty/loaded只包装显隐，不源动画货物数量。Socket_Cargo=(0,.86,-.83)fwd-Z、Service(0,1.3,1.1125)fwd+Z、Handling(0,.70,1.0)fwd+Z（后承架加耳center(0,.70,.925),size(.20,.10,.15)，Z.85接承架、Z1.0接点），均up+Y；存在不批准牵引/停靠。静态及动作候选X±1.65，Z[-2.02,1.65]，Y0…2.60。源码测真实foot/body/frame连接、箱净空与开盖包络；有冲突具体报主控。

## 子包B：已有几何增补／道具／地表参考

独占 `art/source/lowfi-batch-r1/existing/` 与runtime/models下 zhulei/storage/charger/crate/recovery/terrain-original/terrain-flat/terrain-dug八GLB。只读旧U02 .blend、F03/F04/P01 GLB；新目录保存Blender源，原源保留。禁止写A三个文件及包装/manifest/公共资源/工程合同。可临时headless核源动作与接口。

- **U02**：从39e9618的当前.blend读，不从初版缺铰座GLB读。保持六轮/臂/足/后舱/铰座全部实物。新增ChargePivot=( -.484,.61,.34)，现ChargeCap保持世界姿态并做其子，charge绕Z-60°向左外翻，补顶边小实体铰座接ChargeHousing。五clip：move轮X一秒整圈；work在2秒内idle→已交work→idle（三参数肩55→-20→55、肘-155→5→-155、足.30→.03→.30）；charge盖0→-60一秒；disabled肩55→85/肘保持-155一秒收拢停机候选；maintenance后盖0→70。towed disabled定格＋包装候选杆。不动画root/socketstatic/材质/资源。Socket_Work随腕，其余原位置保持，无Cargo。候选三态/动作总包络在已有范围基础上必要实测回报，不能静默放宽。
- **F03**：导入现役storage-r1完整几何/12箱，保留r3可读性。新增Gate pivot(0,1.90,-1.52)，panel localcenter(0,-.72,0),size(4.12,1.44,.06)，body_light；闭X0挡取放面，idle X+90向前外侧水平（Z至-2.96），计入板厚所有开闭角度Z≤-1.49，仍在货箱前至少.44m，避免扫进前排箱。GateAxle沿X长4.32、radius.04，接前柱；暖色标条localcenter(0,-.66,-.04),size(3.9,.10,.02)，贴面。disabled clip开放→关闭一秒；idle/work Gate保持开放，work静态“取放待接”，不让箱自动进出。新增后封闭ServicePod center(0,.85,1.62),size(.80,.60,.20)，底通过支座center(0,.335,1.58),size(.50,.43,.20)接后地基顶.12和pod底.55；ServiceCover pivot(0,1.15,1.725),panel localcenter(0,-.30,0),size(.78,.60,.03)，X-70外翻维护，轴/铰座实体接pod。maintenance clip，Gate仍开。Payload父RackPayloadDemo显隐由包装控制。固定设施move/charge/towed N/A。示意Socket_Input=(0,.20,-1.60)fwd-Z，Output=(0,.20,1.78)fwd+Z（后另贴实体服务标记，不承诺机器人停靠），Service=(0,.85,1.7425)fwd+Z。外包络候选X±2.25/Z[-3.02,2.35]/Y0…2.20，pod开盖若超此具体报告。
- **F04**：导入旧充电GEO全件，保持Dock/PowerIn/socket；ContactHead仍安全待接，与车接口.17m空隙，禁止擅自插入。新增StatusPaddle pivot(1.055,1.45,-.35)，localcenter(0,.13,0),size(.35,.26,.035)，body_light/accent_warm；idle X-90平放柜顶，work X=15*sin(pi*t)两秒循环，明确0/.5/1/1.5/2秒为0/+15/0/-15/0°（不能只采0/1/2造成静态）。实体轴座接CabinetCap。StopGate pivot(0,.04,-1.0)，localcenter(0,.17,0),size(1.55,.34,.035)，accent_warm，idle X-90向外平放（Z≥-1.34），disabled X0竖起，地基上的双轴座接X轴。ServicePanel旧件改为top hinge(1.254,1.11,-.35)子件、世界位置保持；maintenance Z+70°向+X外翻；补可见铰座。work/disabled/maintenance clips；move/charge/towed N/A固定供电设施，机器人受充电态由外部U01表达。候选动作X[-.9,1.85],Z[-1.40,1.1],Y0…1.74；主控再核U01 charge开盖净空。
- **P01货箱**：只读现役crate-r1 GLB，导入保存.blend/再导出，保持.64×.38×.70/r3无共面伪影，不新增动画、额外盖开启能力或库存数。
- **P01回收连接件候选**：root RecoveryLink，中心原点，Z两端±.175，X±.10、Y±.04。深色杆长.35/截面.06；端耳centerZ±.15，size(.20,.08,.05)，accent_warm/metal frame。Socket_EndA=(0,0,-.175)fwd-Z，EndB=(0,0,.175)fwd+Z/up+Y。与既有U01 .35m牵引示意一致，仅可比较原创连接附件；没有正式销孔/机械连接/载荷/谁可拖谁规则。在独立关系图按两个当前机器人Tow接口摆放，标明视觉候选，不签救援系统。静态源/无clip。
- **T01/T02造型参考**：独立9×9米地表参考，49×49顶点规则网格，+Y上，边界高度0，非T3区域/产品权威数据。b=max(0,1-(max(|x|,|z|)/4.5)^6)，h0=.35*b；hill=.85*exp(-((x+2)^2/1.1²+(z-1)^2/1.25²))*b；ore=.50*exp(-((x-2)^2/1.0²+(z+1)^2/1.1²))*b。original=h0+hill+ore；flat=h0+ore；dug=h0-.30*exp(-((x-2)^2/1.0²+(z+1)^2/1.1²))*b。三网格同外边/拓扑，分别写真实.blend/GLB/facts；soil_mars，大矿点形体用frame_dark的3个原创低段岩块，均按各版本实际地表放置，参考形体不发矿/不记库存/不当变形系统。成图明确“造型参考”，正式地形接入仍BLOCKED。边缘h0、原点Y0，内部挖坑高≥0，法线/三角形朝上，不做贴花假坡。

## 源动作和交付格式

A/B每个资产保存idle可编辑.blend，真实GLB及单独facts/report（≤60行）。仅动作对象创建Blender真实动作/NLA，按约定clip同名汇总导出，不靠Python手写二进制拼GLB冒充Blender源动画。帧率30；一秒动作0/1、二秒动作0/1/2秒端点，对循环实际两帧区分，F04必须加.5/1.5秒关键帧。move为轮局部X；直线keyframe，关节分段线性，source动画root不动。各clip对所有可动件提供基线，避免旧动作残留；只写子件变换，无几何缩放/材质/资源数值动画。

facts记录源SHA/输出SHA、真实三角面/mesh/每surface角色/包络/socket/clip时长与轨道、原创/继承来源、实际Blender版本。Blender二次重开/再导出对几何结构/索引/浮点≤1e-6；GLB字节差异如实记。GLM确认真实CLI/工具再制作；缺输入、未知角色、缺动作件、非有限坐标、空输出具体失败，留一份可运行小检查。输出格式不是技术规则/最终审美或性能认证。

## 主控包装与整批验收

A/B源交付完成后，主控补models下逐件.glb.import（保留源动作极值的真实导入参数），不改交付GLB；主控独占 `prototype/assets/lowfi-batch-r1/` 下preview包装、`art/manifests/lowfi-batch-r1/`、`docs/art/production/evidence/lowfi-batch-r1/`、任务/TODO。沿用已有六键apply_preview(state/phase/phase_t/cargo/reason/time_s)与完整先验证再复位规则，七态外不新增offline第八态。新设施只completed；固定设施move/charge/towed N/A有依据；机器人七态都有真实局部clip/静态停机与候选拖杆。包装只暂停/seek真实源动作与显隐明确候选货物/拖杆，不写资源/电/耐久/矿物/导航/保存。

按实际导入层级生成asset-specific manifest、surface接线、socket/AABB和源hash；复用既有正常/近景灯镜头/截图记录，不改公共M01或旧preview。正常/异常验证：全states×货物模式×代表时刻；maintenance→idle、disabled→move、loaded→empty、N/A返回基线；相同绝对time往返一致、root不动、非法枚举/NaN/缺键/越界维持上一合法姿态；缺文件/角色/源clip不给fallback旧件。

实机取证：逐件normal/close、yaw0/90/180，关键工作/停机/维护/空有差异；同条件小基地关系图（单位/设施/运行与部署阵列/货架空有/维修与充电示意/地表前后），T01/T02和回收件标造型候选。记录真实顶点/源轨道/选定非连接件净空有限采样，遇到可达的穿插才局部修，不能以大AABB误报扩建任意碰撞框架。

子包A/B技术交付后主控独立检查与architect收口；作品进入REVIEW并给所有者整批看样，不能擅自标最终ACCEPTED。来源/工具与未核验项单列，所有者已批准低保真制作，无需逐件问风格。真正技术接缝列具体需求，不自行实现；正式Main、性能/碰撞/导航/保存、地形接入均NOT_RUN/BLOCKED而非假PASS。高保真/UV/贴图/烘焙与Astra制作本轮不执行。

## 实际收口 · 2026-10-05

A task_390ccb5412 / 0d14aa0→7252fdd，B task_4ad95558a9 / 69b5f4a→ee91dbd，均真实ZCode GLM-5.3-Flash会话。主控独立补包装与验收：11源重开、21clip、Godot无缓存导入与637姿态／110非法／12缺源保持、108选定净空、284单体图＋3关系图通过；失败记录保留并明确拒绝。

制作与独立技术检查已完成；所有者最终视觉NOT_RUN、T01/T02正式地形与拖救契约BLOCKED、Main／整场性能／高保真／Astra NOT_RUN。正常镜头小接口／微动作辨识有限、固定近景大设施会裁切，已在看样页展示限制。
