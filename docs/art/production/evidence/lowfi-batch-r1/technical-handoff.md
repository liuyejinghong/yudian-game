# 低保真批次的技术接入需求

2026-10-05；本文件是接口交接，不批准玩法规则。本批独立美术制作/技术验证齐套，状态REVIEW，所有者视觉接受NOT_RUN；实际资产清单与hash见manifest和审计记录。只读工程main 84371c3且干净；本轮不写Main、工程合同或游戏系统。

- **导入与呈现**：新源在`art/source/lowfi-batch-r1/`，运行GLB/场景在`prototype/assets/lowfi-batch-r1/`，独立manifest在`art/manifests/lowfi-batch-r1/`。米制、+Y上、前-Z。实例位置/朝向由外部EntityRoot持有，VisualRoot/SampleRoot/Model根保持identity；源clip只驱动局部件。技术线自行选择正式资源注册与LOD/碰撞方案，旧三首样保持原路径。
- **源动作导入**：本批交付完整.glb.import，AnimationPlayer高级节点设置`_subresources.nodes["PATH:AnimationPlayer"]["optimizer/enabled"]=false`，不可仅复制GLB后用默认优化参数。默认优化会削去本批扫描／工具／关节转折极值；主控新无缓存工程导入与真实端点已复验。动作节奏是美术候选，轮滚动／扫描time_s须由技术线按实际位移／工作进度提供，不反推速度或资源规则。
- **状态调用**：独立包装使用已有六键`state/phase/phase_t/cargo/reason/time_s`，先验证再复位并seek真实clip。七态固定，F02的offline继续映射disabled。新设施仅completed；固定设施move/charge/towed明确N/A。绝对展示时间由技术线提供，不从动作反推生产率、耗电或维修进度。
- **货物**：U01原有empty/loaded、F03的RackPayloadDemo、F06的LanderPayloadDemo只是视觉显隐。真实库存数量、箱位分配、产物/运输命令由技术线拥有；F03 work为静态开放取放面，不伪造自动搬箱，F06开盖不自动出货。
- **充电**：F04 Socket_Dock保留既有位置，接触头与U01接口仍有.17m安全待接间隙。需要技术线冻结停靠姿态、接触机构驱动与状态来源后再做正式插合；不能以候选摆放宣称补能已接。
- **维修/拖救**：F05 Socket_Bay是root摆放示意，工作滑座只表现候选工具区。需要技术线给出实际可维修对象、送回/停靠命令、维修进度来源；维修不补电。P01回收杆及TowFront/TowRear只是候选视觉接点，谁拖谁、载荷、允许连接、断开/路径/故障规则未冻结，模型不代签救援能力。
- **地形**：三个9×9m同拓扑网格只用于原坡/整平/开挖形体比较，不能替代权威T3数据。正式接入需区域边界/采样尺度、阶段或增量更新接口，以及显示/碰撞/通行/保存同步契约；矿物储量/结算与地表变化绑定仍由技术线决定。

工程PR34已有限接受单机器人到场／连续有效作业／整平提交与下一帧验证；正式美术仍是工程ART-LINK待接内容。美术未实现或代签这些玩法；参考地表不能替换权威高度场。

美术验证只覆盖源重开/导出、真实导入、材质surface、接口与有限局部动作、状态复位和同镜头可读性。正式Main集成、整场性能/LOD预算、物理碰撞/导航/保存与所有者最终视觉接受均另验。后续发现实际可达穿插时针对局部部件修正，不要求技术线重做已核合同。
