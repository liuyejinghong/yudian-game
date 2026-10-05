# ART-U02-GEO · 当前低保真几何首样

GLM-5.3-Flash经真实Bridge task_5c5b0d5c76交303f0bf，主控集成4036dc6。首版源与检查在6894a0e保留；architect发现盖板缺实体铰座，主控仅补HoodHingeMount/轴两件并重建。当前数字与hash均为该局部返修后，不冒记GLM交付。

- Blender4.5.14 LTS，米制/+Y上/-Z前。build_sample.py用--background --factory-startup --python-exit-code 1；支持--output、--check、--verify B --compare G。
- 现役U01六轮/Rocker/Bogie完整子树只读复用，输入63c82349bb2c834ba33266c1741b95709dbb43566ab1402dd980f9d634827fd9。旧货台/服务头/接口/动画移除；无Cargo，无游戏能力新增。
- 三静态快照各8416tri/50mesh/66surface/5socket；六角色内只用body_light/frame_dark/accent_warm和继承轮组rubber。源与运行副本三GLB字节一致。
- idle肩55°/肘相对-155°、盖0、足Y.30；work肩-20°/肘5°、足Y.03；maintenance保留idle臂/足、盖70°。候选包络/实际顶点/socket在geometry-facts.json。
- 新实体铰座底Y.76接后墙顶、轴心Y.80/Z.68接盖后缘；装配接触有意保留。支撑为空套四壁、孔.056/杆.045，杆足相接。

| 文件 | bytes | sha256前16 |
|---|---|---|
| zhulei-r1.blend | 1307048 | 6d985fe2db16e318 |
| zhulei-r1.glb | 325992 | 3f75fb7ebb2c2c29 |
| zhulei-work.glb | 325972 | 626fd98b6b5a7205 |
| zhulei-maintenance.glb | 326048 | 76e28c66ab9c8bce |

主控独立检查：源重开结构/索引一致，最大浮点差1.1920929e-7≤1e-6，GLB字节有差异；实际零字节输出被拒；实际Godot三导入、5接口/包络/杆足与铰座连接、132有限姿态采样、18新图＋6参照图通过。正常镜头小工具端尚不可确认可读。

源重开/采样/截图等真实证据见docs/art/production/evidence/u02-r1/README.md；固定相机与M01覆盖，全部Godot Metal实机静态姿态，不是离线渲染或七态动画。GLM原异常5项与CLI检查记录见原任务结果；主控未重复全套旧用例。

NOT_RUN：七态动画/往返复位、建设/维修/电力/通行/保存、连续净空/航天工程认证、LOD/UV/烘焙、整场性能、所有者最终视觉与低保真整批验收。GEO REVIEW，STATE另DRAFT。
