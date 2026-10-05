# ART-I00 · 接入合同 r1

范围：模型导入和表现接缝，不是正式生产美术或救援玩法。依据 PR #14 固定 171c610，沿用已定画风，无新风格投票。

Godot 内 1 单位=1米，Y-up，视觉前方 -Z。地面原点=(0,0,0)，视觉几何最低接触点Y=0；EntityRoot定位/航向由技术线给出，VisualRoot不带额外世界位移、缩放或root motion。Blender作者坐标需在导入后核验，不重复添加90°补偿。

包装结构：`EntityRoot / VisualRoot / <imported mesh parts, Socket_*>`。连接点为局部米制：Socket_Cargo、Socket_TowFront、Socket_TowRear、Socket_Charge；具体位置各资产manifest记录。连接点存在不代表能力、载荷或回收方式已经获准。

动作名：idle/move/work/charge/disabled/towed/maintenance；不适用记录N/A。VisualStateBridge只接受以上外部状态，不计算电量、货物、矿物、地形、任务和建设完成。AnimationPlayer仅驱动模型子件，禁止修改EntityRoot。预览替代动作明确标Preview，不能当作游戏进度。

本轮校准件：`art/source/calibration/calibration.gltf` + `.bin`为可编辑源，`tools/make_art_calibration.py`可复现，实际GLB在`prototype/assets/calibration/art_i00.glb`。是原创非对称尺度标尺，不冒充驮运/设施；无纹理和第三方资源，无Blender安装。本轮未交生产模型、绑定动画或商业许可决定。

独立场景：`prototype/scenes/asset_preview/ArtI00.tscn`。三件同模型按0/90/180°摆放，橙色鼻标指局部-Z；底部贴Y=0。按1–7切换纯预览状态，work仅转WorkPart。manifest记录4连接点、单位和GLB哈希；检查导入AABB/原点/轴向/节点层次，再看正常指挥距离截图。

旧gray-r1场景及fixture的几何、相机路径和占位动作不因样件改变；新样件不能混入旧约120FPS比较。土坡/矿点可先做造型参考；T3区域、边界高程、权威状态与通行/产矿合同未定，ART-T01/T02正式接入仍BLOCKED。

首批顺序：ART-U01驮运→ART-F01太阳能→ART-F02加工；共享材质ART-M01单一写者；每件单独源/GLB/manifest/图形证据。面数/材质/贴图预算待实测，尚不量产全城。

验证状态以工程接手复验报告为准；仅导入成功不等于美术效果获所有者确认。
