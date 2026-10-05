# 当前低保真资产：独立效果审查归档

2026-10-05｜建议，不代签 ACCEPTED。基线 `16e573b01c0f7700e44224fd0fafb6e1246ea930`。本页归档此前实际看图和静态模型核查，并链接后续[美术执行包](../../../roadmap/art-work-packages.md)。本次文档 PR 没有重跑游戏、重放动画、制作或返修模型。

## 1. 看过什么、没有验证什么

此前实际审阅三张关系图、十四条当前记录的正常镜头及四十四张补充状态／角度／近景，共61张唯一PNG。[逐图登记](2026-10-05-reviewed-images.tsv)保留路径、字节hash和实际审阅方式：19张完整原图、35张原像素中心裁切、6张全图768像素宽缩略、1张完整原图加中心裁切。不是287张全部逐张审查，也不能把中心裁切说成完整边缘都看过。

此前读取14个canonical GLB的JSON／二进制、位置／索引、静态节点变换和网格／材质／动画／UV属性存在；身份与对应当前manifest匹配。没有打开Blender，没有运行连续动作、UV质量、碰撞、导航、联合性能或用户应用。[静态摘要](2026-10-05-glb-static-summary.json)不是这些运行验收。

所有画面是Godot独立预览，不是用户当时 `.app` 的游戏画面。normal是参考视距，尚非冻结的正式玩家相机。历史U01高保真实验未在本次审查范围，不评价未见效果。

## 2. 关系图与总体结论

先审阅了[原坡关系图](../evidence/lowfi-batch-r1/relation/terrain-original_normal.png)、[整平关系图](../evidence/lowfi-batch-r1/relation/terrain-flat_normal.png)、[开挖关系图](../evidence/lowfi-batch-r1/relation/terrain-dug_normal.png)。浅色外板、深色骨架和暖色强调形成一致设备家族，运输车低货台、筑垒工具架、望山桅杆以及太阳能翼／仓储架／维修门架有基本区别。现有低保真不是需要整库推倒的错误方向。

主要短板是：参考正常视距下对象偏小、细状态容易消失；地表同色和浅层形变让成果难读；“是什么设备”比“现在发生什么”容易读懂。先联合镜头、实际UI视口、材质／灯光和状态表示定位，不能把所有问题都归模型或直接放大全部设备。

下表图片相对路径以 `docs/art/production/evidence/lowfi-batch-r1/` 为公共前缀；原始索引和manifest见[未压缩审阅入口](2026-10-05-gpt6pro-assets.md)。表中的建议不会覆盖当前美术TODO的REVIEW或已有技术接受。

## 3. 逐项建议（14条记录，不是14种独立玩法资产）

| 记录／版本 | 可见问题及玩家影响 | 建议、最小修正与复看 | 已看主要证据 |
|---|---|---|---|
| U01 拖运 rev3 | 低车体／货台支持运输角色；空载／载货正常视距差别偏弱，载货近景顶面有不规则白色三角区域 | **局部返修**内置载荷重叠顶面；保留整车／外包络／socket。正常空／载、近景90/180、维护和主场景复看 | [正常](../evidence/lowfi-batch-r1/captures/set-05/tuoyun_idle_empty_normal_0.0.png)；`captures/set-05/tuoyun_idle_loaded_close_90.0.png`、`captures/set-07/tuoyun_idle_loaded_close_180.0.png` |
| U02 筑垒 rev1 | 工具架可区分角色；工作姿态变化存在但normal较弱，维护翻盖较清楚 | **可继续集成验证**；用真实作业接触／进度和状态兜底增强，不堆近景细节。实际施工站和工具包络复看 | [正常](../evidence/lowfi-batch-r1/captures/set-03/zhulei_idle_empty_normal_0.0.png)；set-03 work_0.25／work_0.75 normal；set-04 maintenance close90 |
| U03 望山 rev1 | 高桅杆／横向头辨识较好；工作方向变化可见，停机与闲置区别弱 | **可继续集成验证**；扫描由真实探索进度驱动，增加原因表示；扫描动画不自动发现资源 | [正常](../evidence/lowfi-batch-r1/captures/set-01/wangshan_idle_empty_normal_0.0.png)；同组work_0.25／work_0.75／disabled normal |
| F01 太阳能 rev3 | 双翼面板功能直观，收拢／展开轮廓差异明显；相邻阶段仍需信息帮助 | **可继续集成验证**；冻结展开空间、真实施工和出力；normal多角度与邻接设备复看 | [正常](../evidence/lowfi-batch-r1/captures/set-06/solar_idle_empty_normal_0.0.png)；同组packed／deploying／installed／work normal及normal90/180 |
| F02 处理设施 rev2 | 多角度能见输入输出，中央体仍像通用白盒；朝向和工作／停机不够读得懂 | **小改后集成**；强化进出料形体和一个工艺特征，保留防护逻辑；normal0/90/180、work／disabled复看 | [正常](../evidence/lowfi-batch-r1/captures/set-06/processor_idle_empty_normal_0.0.png)；同组normal90/180、work／disabled／maintenance normal |
| F03 仓储 rev1 | 开放框架和箱位支持识别；深载荷与底座接近，货种不清楚 | **可继续集成验证**；增强载荷／底座分离与类别表示。箱位显隐不证明真实容量 | [正常](../evidence/lowfi-batch-r1/captures/set-04/storage_idle_empty_normal_0.0.png)；同组idle_loaded／disabled_loaded／maintenance_loaded normal |
| F04 充电 rev1 | 平台和接触头有服务站轮廓，但工作截图不证明机器人插合 | **单体继续、联合证据不足**；先定实际停靠和机构，再看成对姿态；不擅挪接口消掉预留间隙 | [正常](../evidence/lowfi-batch-r1/captures/set-04/charger_idle_empty_normal_0.0.png)；work_0.5／work_1.5 normal；work_0.5 close90 |
| F05 维修 rev1 | 门架和工具区与充电有区别；空站工作变化不能说明在修谁 | **单体继续、维修关系不足**；增加真实对象入位与进度驱动，空转动画不证明修复 | [正常](../evidence/lowfi-batch-r1/captures/set-02/repair_idle_empty_normal_0.0.png)；同组work_0.25／work_0.75 normal及work_0.25 close90 |
| F06 着陆器 rev1 | 大体量箱体／支脚可识别；部分normal角度货舱和内部被遮挡 | **可继续集成验证**；改善开口／出货面和交付点，不增加起降玩法；有载／空载多角度复看 | [正常](../evidence/lowfi-batch-r1/captures/set-02/lander_idle_empty_normal_0.0.png)；work_empty normal90、work_loaded close90、set-03 maintenance normal |
| P01-CRATE 货箱 rev1 | 可作为容器，normal不能辨别内部物料 | **限容器用途继续集成**；物料形态／类别另补，不把U01内置载荷问题扩大到独立crate | [正常](../evidence/lowfi-batch-r1/captures/set-05/crate_idle_empty_normal_0.0.png)；同组close0 |
| P01-RECOVERY 回收杆 rev1 | 近景能见杆与端部，normal很弱；单件不能判断连接／转弯／载荷 | **证据不足**；保留候选，救援能力／方式冻结后看双体实际关系 | [正常](../evidence/lowfi-batch-r1/captures/set-05/recovery_idle_empty_normal_0.0.png)；同组close0 |
| T-REF-ORIGINAL／FLAT／DUG 三条均rev1 | 几何确有差异，但同色、浅凹和镜头使成果难读；岩块不说明矿种 | **返修成果表现样例**；联合形体／表面／边缘／光照，不将三个GLB直接替换权威高度场 | `captures/set-05/terrain-original_idle_empty_normal_0.0.png`、`terrain-flat_idle_empty_normal_0.0.png`、`terrain-dug_idle_empty_normal_0.0.png`；各normal90及dug close0 |

“比例大致协调”不等于活动空间已协调。太阳能展开、筑垒工具、维修入位和拖救双体仍需真实工位／通路验证。[技术交接](../evidence/lowfi-batch-r1/technical-handoff.md)保留的充电约.17m安全间隙、候选Tow接口和状态时钟，均不能被本表代签为玩法完成。

## 4. 已定位到模型层的发现

### U01内置载荷顶面共面

固定基线源 `art/source/units/tuoyun-r1/generate_tuoyun_r1.py` 的 `geom_cargo()`：浅箱身中心y=.74、高=.24，深箱盖中心y=.83、高=.06，两者顶面均y=.86。GLB CargoBox节点y位移=.48，箱身和箱盖本地顶面均=.38，重叠区域共面。对应载货近景白色三角区域支持深度竞争解释；未运行引擎开关对照，不声称已复现连续闪烁。

最小修正为缩短被盖住的箱身或删被覆盖的重复顶面，保持既定外包络／socket；DT-A02负责，不重做机器人，也不是机器路径清理造成的损坏。

### 地表参考真的不同，但表达不够

`art/source/lowfi-batch-r1/existing/build_terrain.py` 的原坡、移除小丘后的整平、局部高斯下挖形体并不相同。下挖不必低于边界0m才存在；不能凭静态AABB最低0m误判没有坑。问题在当前统一棕色／浅凹／阴影与参考镜头组合下，成果正常视距难读。DT-A03接正式高度场表现，不单加一块亮贴花。

### 材质、UV与提交开销

十四个GLB均无images／textures；部分导出件有TEXCOORD_0，所以“没有贴图生产”不等于“所有模型完全没有UV”。这里没有检查展开质量、重叠、密度、烘焙。

U01有7312三角形／109 primitives，F01有888三角形／74 primitives；提示正式场景除面数外还需测提交、材质切换和实例负载。primitive不直接等于最终draw call，不据此判性能失败。必要时只合并同材质且无需独立动作／显隐的静态部分，保留关节与接口。

## 5. 独立缺口与覆盖表核对

先看图形成缺口，再读[已有覆盖表](../requirements/asset-coverage-2026-10-05.md)。地表表面、铁铜六类资源、自然定位物和类别／工程标记与覆盖表一致；额外应前移的是实际工作相机、选择／授权／阻塞反馈、工作／停机辨识、载荷局部问题和成对服务接口。

| 状态／优先级 | 用途与最小范围 | 后续包／验收 |
|---|---|---|
| 已有参考、待冻结／P0 | 实际作业相机和UI视口，避免盲目放大模型 | A01/E01，正常档先看 |
| 未形成正式交付／P0 | 选择、合法／非法工程范围、取消与阻塞标记 | A03/E01/E02，真实命令与状态驱动 |
| 已有参考、需修正并接入／P0 | 自然土／施工面／开挖边缘和成果 | A03/E02/E10，权威几何与重载同源 |
| 未制作／P1 | 铁矿、铜矿、铁料、铜料、结构件、线缆 | A04/E05/E07/E10，类别形体和载荷／图标一致 |
| 部分已制作、施工覆盖不足／P1 | 真实材料到场、建设阶段、落成与运行 | A05/E08/E10，不能播放即产出 |
| 已有状态候选、需增强／P1 | 工作／等待／低电／低耐久／停机／维修 | A07/E09/E13，非颜色唯一通道 |
| 需先冻结接口或能力／P1 | 充电接触、维修入位、双体救援 | A06/E09/E17，游戏物理／成本由产研测 |
| 未制作独立包／P2 | 少量岩块／露头和自然定位轮廓 | A08/E16/E21，不擅增新玩法地点 |
| 按实际事件补齐／P2 | 少量作业声音、到货反馈、尘效 | A07/E03，不机械建全类别库 |

操作标记应与地表和资源并行，不等环境装饰做完。正式T01/T02的接缝仍需冻结，但不必等待父T3所有远期工作才开始当前有限区域的表现和资源准备。

## 6. 归档与字节来源

本PR将原证据附件整理为本页、逐图TSV与GLB摘要JSON，去掉重复URL和无助执行的原始AABB数列，不是原文件逐字复制；语义范围不扩大。原附件SHA256：

- `余电-双线审查证据与任务包-2026-10-05.md`：`046d98c6bca582496204147e65ae1d8952246689a08ef7a74685cb8314cd7f22`
- `余电-实际审图清单-2026-10-05.json`：`737b863fc238abbb370583ecebb47e063323d78ec3c5fdb2a0bb7229c0a5b7e2`
- `余电-GLB静态核查-2026-10-05.json`：`ef18d82da8ff214af8027102e9fd2c4bc7d4573cf6b75d6063d05bfd60d0634a`

本次归档再次核对登记中的61个PNG及14个GLB与可用基线快照字节匹配；这不是本轮又看了一遍61图或重新解析／运行全部模型。历史公开文本按publication路径清理说明理解；模型和图片身份与对应版本核对，不以清理前文本hash误报损坏。
