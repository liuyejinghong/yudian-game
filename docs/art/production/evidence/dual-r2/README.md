# 双线第二片 · 2026-10-05

已产出：主控U01 Blender形体revision3、真实GLM的F04单泊位静态几何，均已独立导入/看实机图，REVIEW。未完成整车高保真、充电站完整状态、最终视觉或游戏集成。独立分支art/production-r1-20261004；工程main7f6667e仅只读核查。

## U01真实结果

主控在6a0028e HF基础上增加成形盖板/肩条/货台侧壳/六轮毂盖，保留六轮/摇臂/货物区及四接点。源/运行GLB19504tri、46mesh；[实际网格](hf-audit.json)、[独立检查](hf-verification.json)、[可运行验证](verify_hf.py)。[重开结果](reopen.json)结构/索引一致、最大浮点差1.1920929e-7≤1e-6，GLB字节不同。

[16张旧新真实Godot图](images-hf/captures.json)：相同normal/close、yaw0/90、空/有箱、M01、白昼、Metal Forward+。u01-prev是Git6a0028e的r2试片，u01-hf是当前r3，没有更换镜头/光照。主控复看：近景成形边/货台侧壳/毂盖层次更明显，正常尺度收益有限，不能据此写高保真品质完成。当前面数增加是几何事实，实机成本尚未测。

[近景当前](images-hf/u01-hf_close_90_empty.png)、[近景旧版](images-hf/u01-prev_close_90_empty.png)，正常镜头见同目录normal图。此片无完整动画、UV或烘焙，形体/尺度仍候选。

## F04真实交付与独立验收

[冻结票](../../tasks/ART-F04-GEO.md)。工人独立分支art/f04-worker-20261005从675defd，只写charger-r1两目录；原创程序化静态几何、可编辑glTF/bin、GLB、生成器/facts/report七件。GLM提交83f7987，主控cherry-pick为f41478a。源/运行SHA256均84c4849bda02625c0499b4dcfb2bc14ec59b6f02b429c2cde86b43e0d1c0b791。

通过Agent Bridge技能（本机SKILL.md：`<user-home>/.codex/skills/agent-bridge/SKILL.md`）真实派单。ZCode会话sess_1584816a79先原生/model GLM-5.3-Flash确认（[记录](model-confirm.json)），再[派单](F04-dispatch.json)task_0e18fb6a1d；[真实结果](F04-result.json)。Bridge observed_model为null，不伪称其监测到了模型；作者由工人提交独立核定。任务729秒；usage83098仅Bridge计数，不是费用，生成器0.003s也不是人工制作耗时。会话现已结束，无活动外包任务。

GLM自检：六类非法输入被具体ValidationError拒绝（自检整体exit0）；原始GLB回读及跨目录重建hash一致。主控独立核查：[Godot实际导入](f04-audit.json)144tri/9mesh/9surface/三角色/2socket，包络与正向均符冻结；源/运行字节一致，现役U01演示hash与原manifest一致；[可运行验证](verify_f04.py)/[结果](f04-verification.json)。使用Godot4.7.2真实Metal4/Forward+，1920×1200，六角色共享M01中按surface绑定三角色。

architect计划前发现旧head面X.52会与U01 charge盖+60°穿插，冻结回缩面X.65/Dock(.65,.57,-.35)。主控[实际开盖检查](check_f04.gd)/[记录](f04-clearance.json)：现役车根(0,.02,0)，charge片0…1秒共61采样，每次133对非地面设施/车网格按实际顶点AABB检查均分离；最小分离轴间距.0412564m（不是欧氏最近表面距离或连续扫掠证明）。被否决的虚拟旧头在50个采样与盖AABB重叠，只作失败候选复现，非交件模型。

[12张真实实机图](images-f04/captures.json)：normal/close×yaw0/90×空泊位/停车/开盖示意；[近景](images-f04/charger_close_0_charge_demo.png)、[正常空](images-f04/charger_normal_0_empty.png)、[正常有车](images-f04/charger_normal_0_idle_demo.png)。主控复看：封闭柜/固定泊位分区成立，空与停车差别在normal可见；接口小、normal细节弱，近景仍明显低保真。此GEO仅候选安全待接位置，Dock与车Charge点间距.17m，未制作真实插合/充电设施状态；演示的开盖来自现役U01，不代表设施STATE完成。柜后PowerIn护块有实体承载，但非电网认证。

## 验证边界与重跑

本轮已验证：工具源重开/GLB导入、静态形体包络/继承接口、F04候选开盖净空采样、28张实机图及其源hash。候选：新增外壳比例、单泊位尺寸、回缩接口位置。NOT_RUN：完整HF UV/材质/烘焙/七态/LOD、F04 STATE/机械插合、补能/维修/库存/导航/存档、整场性能及所有者最终视觉。现役U01/F01/F02、M01/公共预览、Main/project/fixture和工程/产品合同未改。

最小复核：运行本目录verify_hf.py与verify_f04.py；它们读取归档的实际导入/姿态数据并核当前源和PNG身份，不冒充重新运行GPU或动画。真实采集入口capture-hf.gd/capture-f04.gd、charge核查check_f04.gd复用已存在的独立临时预览；原预览工具与ViewR1未修改。日志仅替换私人路径，数值/错误不改；原始日志SHA见[清单](private-log-hashes.json)。

[Architect收口](architect-review.md)：无新增阻断问题；主控已核关键hash与真实图，接受的是本片制作与独立技术检查记录，两票仍REVIEW。
