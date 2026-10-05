# 首样独立预览合同 r1

日期：2026-10-04。**比较输入已冻结；本合同定义时实现与图形NOT_RUN，当前制作/接受状态以[美术TODO](../todo.md)为准。**适用ART-PREVIEW-01与M01/U01/F01/F02，独立于游戏规则；需求轮只写合同；后续实际生产授权与证据在TODO中追踪。现有I00是校准件，`VisualStateBridge.Preview()`在work强制转WorkPart且切态仅复位WorkPart，不能直接复用作新样件控制器。七个状态字符串保留，旧桥不修改。

## 固定画面设置

| 项 | 固定值/动作 |
|---|---|
| 引擎与后端 | 当前候选Godot 4.7.2 .NET，Forward+ / Metal；每份证据记录实际版本/后端，不把启动请求当实际身份 |
| Viewport | 输出PNG实际1920×1200；3D渲染比例1，MSAA4，FXAA/TAA/超分关闭；不随窗口尺寸或Retina比例更改内部尺寸。不符合则本次不能公平比较 |
| 相机 | 透视，KEEP_HEIGHT，near=0.05、far=200；normal=(18.3848,28,18.3848)→(0,0,0)，FOV60；close=(3,3,4)→(0,0.4,0)，FOV50 |
| 朝向 | EntityRoot原点，Y轴yaw=0/90/180°；VisualRoot与SampleRoot身份变换。只转物件，不移动光/相机 |
| 主光 | 单个DirectionalLight3D，rotation_degrees=(-55,-30,0)，light_color=#FFFFFF、light_energy=1.0，shadow_enabled=true，directional_shadow_max_distance=80；阴影图4096（独立进程内调用RenderingServer.directional_shadow_atlas_set_size(4096, true)，不改project.godot），阴影滤波使用引擎默认并记录 |
| 环境 | Background COLOR #AEB8BC、background_energy_multiplier=1；Ambient source COLOR #D5DBDF、energy=0.60、sky_contribution=0；reflected_light_source=Environment.REFLECTION_SOURCE_DISABLED，不装Sky/ReflectionProbe/GI |
| 映射与后处理 | tonemap LINEAR、exposure=1；无CameraAttributes/自动曝光、调色LUT、亮度/对比度调整、雾/体积雾、辉光、SSAO/SSIL/SSR、景深和装饰粒子 |
| 地面 | 20×20m Plane，Y=0，引用soil_mars；不是地形施工状态，不做贴花。地面不自动缩放/变色适配资产 |
| 材质试板 | 同一光/环境；6组球（直径0.6）+立方体（边长0.6），组中心X=-1.2/0/1.2、Z=-0.8/+0.8，最低Y=0；每组球/方块沿Z分别偏移-0.35/+0.35（X不偏移，组间不穿插）；固定试板相机(4,4,6)→(0,0.4,0)、FOV50。表格依次body_light/frame_dark/rubber/accent_warm/solar_face/soil_mars |

以上是主控推荐的首样白昼设置，不是量产光照验收结果。第一次实机若过曝/欠曝/关键状态不可读，先查接线与曝光是否符合；需要修设置时升preview revision、所有资产一起重拍，禁止单件改光/缩放或裁图赢比较。近景大设施超框仅用于局部，不作为整件失败或擅改相机的理由。无标签图保留同样的原始尺寸，不靠放大辨认。

Camera的FOV与KEEP_HEIGHT、Environment映射/曝光、材质颜色属性分别以[Godot Camera3D](https://docs.godotengine.org/en/stable/classes/class_camera3d.html)、[Environment](https://docs.godotengine.org/en/stable/classes/class_environment.html)、[BaseMaterial3D](https://docs.godotengine.org/en/stable/classes/class_basematerial3d.html)为API依据（2026-10-04查阅，stable网页不能替代本机4.7.2属性加载检查）；具体美术数值是本项目候选。

## 最小样件包装与材质引用

独立预览场景拥有`EntityRoot / VisualRoot / SampleRoot`。样件交`preview.tscn`（根SampleRoot）、`preview.gd`和一个真实GLB实例，节点名`Model`。动画和socket都在样件子树内，不修改公共桥或注册表。独立预览调用根方法`apply_preview(preset: Dictionary) -> String`：空字符串成功，非空为具体错误；不得给游戏权威写入口。

- GLB保留各面稳定的占位材质角色名，模型按结构分surface，不能用一层material_override遮掉所有分区。
- canonical manifest在`art/manifests/<sample>.json`；CLI读仓库外路径/绝对路径或cwd相对路径，不要求导出游戏读取它。本轮不新建通用schema平台。
- `material_bindings`每行是`{"node":"Model/<实际Mesh节点>","surface":0,"role":"body_light"}`，node相对SampleRoot，surface从0起。示例路径是占位，工人必须填导入后的真实节点路径；遍历所有MeshInstance3D，连隐藏货箱/状态件每个surface也恰好绑定一次。
- 预览查稳定六角色字典，加载`res://assets/materials/art-r1/<role>.tres`后在该surface设override；同角色复用同一资源，不按颜色猜角色，不修改GLB导入器、不生成第二套公共材质、不set整个Mesh的material_override。
- 缺.tres/节点/角色、索引越界、重复绑定、遗漏surface或wrapper加载失败：停止本样件、退出1/GUI明确错误，不输出PASS截图，不回退旧I00或灰模。几何遮盖颜色不等于材质已接通。
- 实录GLB、源、manifest、六资源及预览配置SHA256；manifest不记录自己的hash避免循环，采集记录另列其hash。先核对manifest声明的GLB hash再导入。

manifest至少记录：`id`、正整数`revision`、`unit="meter"`、`up="+Y"`、`forward="-Z"`、`preview_scene`、`glb`（后两者res路径）、`glb_sha256`、`bounds`（bounds.static / bounds.loaded / bounds.active，分别空载闭合、idle有载和局部活动包络，各min/max XYZ；按实际可见Mesh顶点转换到SampleRoot局部空间测量，隐藏状态件不计当前包络，仍参与材质覆盖检查）、`material_bindings`、`sockets`（node/position/forward/up）、`states`（七键，每项mode=static/animated/na、reason、clip、duration_s、loop）、`phases`、`cargo_modes`、`source`、`triangles`、`textures`。clip无动画为null、duration_s为0；phase仅F01四项，其余`["completed"]`；cargo_modes仅U01为empty/loaded，其余`["empty"]`。来源记录生成器/源路径、工具版本、原创/依赖与hash。F01另交四阶段×七态`phase_state_modes`表（键phase→state→mode/reason）：主states表描述completed基准，非completed的work为static；实际呈现mode也写进采集JSON，不能把无动作的组合标成animated。字段都写真实值，不用null冒充已测包络。复核数值允许误差1e-4m；造型违反候选包络须回报主控，不放宽来签通过。

## 输入与完整复位

preset固定键：`state`、`phase`、`phase_t`、`cargo`、`reason`、`time_s`。状态仅idle/move/work/charge/disabled/towed/maintenance，大小写精确；offline在F02展示标签中映射disabled，输入offline必须拒绝。

- F01 phase仅packed/installed/deploying/completed；phase_t∈[0,1]只对deploying有意义，其余必须0。部署4秒曲线由phase_t寻址，不靠动画播完改phase，不混成七态之一。运行循环用time_s，完整复位后按绝对时间seek；非循环姿态到达终点后保持。
- U01/F02 phase仅completed、phase_t=0。U01 cargo=empty/loaded，移除货箱保留Socket_Cargo；F01/F02 cargo=empty。F02进/出料箱为固定私有示意摆件，不能误称loaded。
- reason仅none/return_charge/no_power/mechanical/both（**预览原因注记，不是新增模拟enum**）：return_charge用于move；no_power/mechanical/both用于disabled/towed/maintenance；其他组合只用none。模型动作不从原因反推数值。零电/机械/两者用“电池轮廓／扳手／两者”图标+中文短说明；return_charge用回充箭头说明。
- time_s有限非负秒。yaw、camera和labels由预览工具拥有，样件不接收世界移动；灯光与材质不能从preset改变。N/A态返回成功但UI/采集记录明确N/A原因，姿态回到当前phase的静态基准，不沿用上一态运动。
- wrapper每次先Stop所有动画，重置每个活动节点transform/visible、货箱和示意杆，再seek到请求时刻；同输入同结果，不按调用次数累计旋转。非法请求不得部分改变已显示对象，验证后再复位施加。

CLI合同（未来实现入口，不是本轮已可执行命令）：显式场景`res://scenes/art_preview_r1/PreviewR1.tscn`；`--`后接受`--manifest <path>`或`--material-board`二选一，`--camera normal|close`、`--yaw 0|90|180`、`--state <七态>`、`--phase <phase>`、`--phase-t <0…1>`、`--cargo empty|loaded`、`--reason <reason>`、`--time <秒>`、`--labels evidence|blind`、可选`--capture-dir <新目录>`、`--self-test`。默认normal/yaw0/idle/completed/0/empty/none/time0/evidence；试板模式只允许默认对象状态，使用试板相机。未知/重复/缺值/空manifest/非法枚举/NaN等先拒绝且退出1，不回落默认；有效加载/采集结束退出0。

GUI仅需相机、方向、七态、phase/进度、货箱、原因、时间、标签和Reset控制；与CLI共用同一校验。Reset恢复默认preset和镜头方向；重复选择同状态不得叠加动画，关闭重开不保留上次姿态。不做库存UI或完整关卡。

## 公平证据与验收

带标注evidence PNG及同名JSON记录asset/revision、Git commit、sample/材质/配置hash、实际Viewport/后端、镜头/yaw、state/phase/phase_t/cargo/reason/time、N/A与错误。blind PNG同画面但隐藏资产名/状态/原因/图标和socket辅助线；blind文件名只用随机/序号中性ID，答案留JSON由主控持有，不向看图者同时透露。一张单件图一份记录，正常镜头不可截取主体放大；图集只用等比例缩小排版。

采集目录须不存在，创建独立run目录；撞名拒绝而不覆盖旧证据。无窗口只查参数/导入/变换，图形证据必须实际窗口渲染后取Viewport原始PNG，不把headless空图计通过。截图前完成导入/材质接线，至少2帧渲染稳定；动作在指定时刻暂停，不靠“等到第120帧”随机抓活动位置。性能测量不与截图读回混跑。

单件入口足以验每个模型；三件比例联验等资产齐套后，由主控在临时组合场景复用本合同设置取证，不在本工具扩多资产编辑器，也不是U01开设施生产的循环前置。全表见[视觉验收](visual-acceptance-r1.md)。空路径、重复切态、缺资源、未知状态/phase、错误surface、关闭重开、disabled→move、maintenance→idle、loaded→empty、completed→packed→completed、idle→N/A→idle都属于必要正常/异常用例。预览工具可用自身目录内无规则探针验证接口，但必须标test-only，不能冒充正式样件或视觉接受。
