# A01／A03 接缝 r2 · committed mask

2026-10-07，美术候选；制作与技术样例，尚未接入普通 D1.0 应用，所有者视觉未验。

## A01 给 E01／C02

机器输入：[camera.json](../../../../art/d1/camera.json)。正常／总览／近景是 absolute position + target + FOV；实际工程使用平移／缩放控制时保留各档相对方向与距离即可。透视 keep_height；模型1m单位、scale1。候选留白左320px、上56px、下112px，UI 1x；工程UI实际占用改变后同viewport重新核验。近景聚焦机器人，不要求同时容纳整个基地；主线运行相机代码仍工程持有。

A01 独立脚本读取实际 Main 场景、原生地形与光照，再隐藏旧灰模、停其更新，在同一个1920×1200 root viewport 放置三机器人和四设施做对照；其模型位置是美术校准布局。actual-main-v0.png 是原 Main，其他图明确标 art overlay；不能当默认应用接入图。reference_normal 沿用旧 preview 的方向／距离并平移到同一候选取景目标，另有 actual Main 原始镜头。

## A03 给 E01／E02／C02

收到工程工作树 `docs/engineering/contracts/d1-player-r1.md`（base259a8bf、2026-10-07工作中合同）：半径2m，网格65×65、spacing=.9375m，范围[-30,30]；margin=2+2*spacing=3.875m 是工程合法性边界，不是标记半径。选区依据 PreviewLevel.Legal 与 Reason；下单、权限与采样版本由工程重验。

`art/d1/markers.gd`：`build(center: Vector2, radius: float, kind: String, sample_height: Callable) -> MeshInstance3D`。center 是世界x/z；sample(x,z)->height 应来自当前已验证权威投影。返回节点顶点已经在世界米制坐标，挂在 identity 的视觉根；不要再加center位移。kind=selected/hover/legal/illegal/committed；非法返回null。圆环、虚线加叉、双圆边互补颜色；每顶点高度+.025m，段长最多.25m。选中机器人半径由工程真实包络提供；工程范围固定2m。

`art/d1/surface.gdshader`：传 `stage`(0自然土／1碎石作业面／2旧单区域压实小样)、`region_center`、`region_radius`；仅改ALBEDO/ROUGHNESS，无几何位移。默认 `use_committed_mask=false`，无需mask纹理，保留原有单区域0/1/2小样。

**工程mask接缝已确认（2026-10-07）：**工程按当前权威高度与同fixture初始高度派生65×65 committed_mask，Changed点=1、其余=0；随world version重建，保存权威高度后即可重载派生。不新增美术区域账本；mask构造、变化判定、纹理生命周期／刷新与状态来源均归工程。

| 参数 | 消费约定 |
|---|---|
| `committed_mask` | 工程ImageTexture；线性红通道0/1；65×65，无sRGB解码；nearest、repeat disabled，shader已声明采样方式 |
| `use_committed_mask` | 默认false；工程有有效mask时置true |
| `world_origin` / `world_span` | 世界XZ最小端[-30,-30]／正跨度[60,60]；不是节点数或采样间距 |
| 像素轴向 | column递增=X递增；row递增=Z递增；不要上下翻转 |
| 节点采样 | uv=(worldXZ-origin)/span；texelUV=(uv*(textureSize-1)+.5)/textureSize，世界两端映射首末texel中心；范围外不采mask |
| 覆盖优先级 | mask1 → 压实面；其余且stage1／当前圆内 → 碎石；其他 → 自然土 |
| `stage=1` | 仅工程实际Working／WaitingForSpace时传入，不覆盖mask1；取消或离开工段由工程撤除临时碎石表现 |
| `stage=2` | mask开启时不把当前圆假设为已提交；只有mask1压实。mask关闭时保留旧单区域小样 |

参数样例（region仅示例当前土坡；纹理必须工程生成）：

```json
{"use_committed_mask":true,"world_origin":[-30,-30],"world_span":[60,60],"stage":1,"region_center":[12,-10],"region_radius":2}
```

机器可读配置见 `art/d1/palette.json` 的 committed_mask_input。`art/d1/check_mask.gd`用合成平面和65×65非对称mask验证采样、首末节点、压实优先、远处历史、mask外不串色及无纹理旧模式；它不创建或验证真实区域事实。r1 shader/配色在evidence/source-r1按原提交字节保留，旧11图和输入hash不冒称新shader截图；mask扩展有独立检查记录。

**仍未验：**普通D1.0实际消费、同地点原状→工作→提交→取消和跨进程重载画面、所有者视觉。原surface-style-*仍是同一真实v0几何的参数小样，不是渐进整平或已提交成果；T01正式阶段待工程同viewport收口，T02/E10未进入本批。

采样与碰撞/通行/保存一致性、鼠标选择、版本门禁、低配置性能由工程验收；本包只检查图形源、尺度、贴地和可读候选。
