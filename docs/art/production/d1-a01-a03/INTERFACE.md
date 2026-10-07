# A01／A03 接缝 r1

2026-10-07，美术候选；制作与技术样例，尚未接入普通 D1.0 应用，所有者视觉未验。

## A01 给 E01／C02

机器输入：[camera.json](../../../../art/d1/camera.json)。正常／总览／近景是 absolute position + target + FOV；实际工程使用平移／缩放控制时保留各档相对方向与距离即可。透视 keep_height；模型1m单位、scale1。候选留白左320px、上56px、下112px，UI 1x；工程UI实际占用改变后同viewport重新核验。近景聚焦机器人，不要求同时容纳整个基地；主线运行相机代码仍工程持有。

A01 独立脚本读取实际 Main 场景、原生地形与光照，再隐藏旧灰模、停其更新，在同一个1920×1200 root viewport 放置三机器人和四设施做对照；其模型位置是美术校准布局。actual-main-v0.png 是原 Main，其他图明确标 art overlay；不能当默认应用接入图。reference_normal 沿用旧 preview 的方向／距离并平移到同一候选取景目标，另有 actual Main 原始镜头。

## A03 给 E01／E02／C02

收到工程工作树 `docs/engineering/contracts/d1-player-r1.md`（base259a8bf、2026-10-07工作中合同）：半径2m，网格65×65、spacing=.9375m，范围[-30,30]；margin=2+2*spacing=3.875m 是工程合法性边界，不是标记半径。选区依据 PreviewLevel.Legal 与 Reason；下单、权限与采样版本由工程重验。

`art/d1/markers.gd`：`build(center: Vector2, radius: float, kind: String, sample_height: Callable) -> MeshInstance3D`。center 是世界x/z；sample(x,z)->height 应来自当前已验证权威投影。返回节点顶点已经在世界米制坐标，挂在 identity 的视觉根；不要再加center位移。kind=selected/hover/legal/illegal/committed；非法返回null。圆环、虚线加叉、双圆边互补颜色；每顶点高度+.025m，段长最多.25m。选中机器人半径由工程真实包络提供；工程范围固定2m。

`art/d1/surface.gdshader`：传 `stage`(0自然土／1碎石作业面／2压实面)、`region_center`、`region_radius`；仅改ALBEDO/ROUGHNESS，无几何位移。世界坐标颗粒无需UV或贴图；单次绑定仅表示一处区域候选。原状/作业阶段由工程实际执行读模型给出；stage2只能依据实际已提交事实，不能仅看进度1或“已完成”文字。

**尚缺接缝：**当前公开 PlayerJobView 只有当前任务的 Stage/Progress/Center/Active，没有已提交版本／历史表面区域事实。要在取消、改派新任务、保存重开后保留全部压实成果，工程须供给持久化的已提交区域／版本或等价映射；美术不创建第二套地形状态，也不从截图猜阶段。工程给同地点原状、工作、提交、取消样例后，再做正式阶段映射对照。现阶段 surface-style-* 是同一真实v0几何上的参数小样，**不是渐进整平或已提交成果**；T01正式阶段未解锁，T02/E10未进入本批。

采样与碰撞/通行/保存一致性、鼠标选择、版本门禁、低配置性能由工程验收；本包只检查图形源、尺度、贴地和可读候选。
