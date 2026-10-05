# 余电 · 低保真模型子批交付

2026-10-05：**冻结模型子批的独立制作已齐，进入 REVIEW，等待所有者整批视觉确认。** 原有驮运／太阳能／加工三件保留；本轮交11份可编辑Blender源、11个GLB、21段真实源clip、逐件包装与manifest，287张Godot实机图。游戏素材尚未齐全：地表贴图、环境／地标、铁／铜资源外形与图标未制作，见[覆盖盘点](../../requirements/asset-coverage-2026-10-05.md)。正式地形、Main接入和高保真不在本次完成范围。

先看[整批看样页](gallery.html)或[正常指挥镜头关系样板](relation/terrain-original_normal.png)。看样页可切对象、载荷／动作时刻和方向；全部PNG保留1920×1200原图。**概念图：本轮未生成；Blender离线渲染：本轮未生成；这里的画面全部是Godot实机截图。** 关系样板只是独立摆放，不证明库存、充电、维修或拖救规则运行。

| 资产 | 本轮交付 | 实测三角面 | 当前范围 |
|---|---|---:|---|
| U03望山 | 新几何、五源clip、六轮／横向传感头、五接口 | 7544 | 扫描与停机候选，不产生地图或矿点 |
| F05维修 | 开放工位、工具滑座、维护／停机，三clip／三接口 | 1348 | 维修位置与动作示意，不补电／恢复数值 |
| F06着陆器 | 四足、封闭主舱、可开前货舱、一箱显隐，三clip／三接口 | 2444 | 已着陆货物设施候选，不含起降或自动发货 |
| U02筑垒 | 保留已交臂／底盘／铰座，新增五clip与充电盖 | 8584 | 施工／收拢／维护；不自行增加能力 |
| F03仓储 | 向外折门、后维护盖、12箱显隐，两clip／三接口 | 1312 | work为静态开放取放面，无自动搬箱 |
| F04充电 | 顶部状态板、停机挡板、真实维护铰轴，三clip／两接口 | 824 | 与U01仍有.17m待接间隙，未完成插合 |
| P01货箱 | 现役形体转Blender源，静态 | 48 | 一种箱，无库存数或开盖功能 |
| P01回收杆 | 双端接点的原创静态候选 | 132 | 无正式销孔、载荷或救援规则认证 |
| 原坡／整平／开挖参考 | 三份9×9m同拓扑网格与岩块 | 各4740 | 49×49地表顶点、4608地表三角面；其余为岩块，不是正式T3数据 |

实测面数来自Model本体，不含包装单独附加的回收杆。新源位于`art/source/lowfi-batch-r1/{new,existing}/`，运行资源位于`prototype/assets/lowfi-batch-r1/`，逐件身份／源SHA／接口／实际包络／材质绑定／状态模式见`art/manifests/lowfi-batch-r1/`。旧三件源与canonical manifest保持原路径；本批不覆盖旧GEO证据。

## 可比较的验收结果

- **PASS，独立核验**：[11源与21clip](source-checks.json)的真实GLB字节、角色、时长、有限值、作者根静止；源与runtime逐字节一致。地表三版精确高度公式与同XZ／拓扑／零边界通过。
- **PASS，Blender真实重开**：[11份源重开与再导出](source-reopen.json)，结构／索引一致，最大浮点差1.7881393e-7（容差1e-6）。GLB字节是否一致逐件如实记录，不把时间戳差异当几何不同。
- **PASS，Godot干净导入**：[无缓存导入日志](import-clean.log)，Godot4.7.2；六件有动画资产的冻结动作端点、接口位置／朝向与候选包络通过。637姿态采样、110非法输入及12缺Model／缺clip保持测试通过；相同绝对时间、fresh实例与跨态复位一致。各`*-audit.json`和`*-state-clean.log`保存实际轨道与结果。
- **PASS，有限净空检查**：[108组部件对与采样时刻](clearance.json)，U03头／叉、F03门／箱、F06箱／主舱、F05工具／U01、F04新增挡板／状态板／U01均有正分离间距。只认证这些选定姿态，不能当全模型连续碰撞认证。
- **PASS，实际图形取证**：[采集计划](capture-plan.json)54组姿态／284张单体图（原230图＋统一补54张前向close180），另[三张关系图](relation/relation.json)。Metal4 / Forward+ / Apple M3 Pro，逐surface绑定M01，不改对象缩放，不按单件自动fit镜头；所有图SHA与当前GLB一致。正常／近景的实际镜头、曝光、环境、灯光和viewport记录在每组captures.json，跨资产同档参数一致。
- **PASS，局部看样页操作**：浏览器真实切换着陆器work_loaded、连续重复选择、normal180与实际close90注记、仓储disabled_loaded和刷新恢复；所示图片真实加载。页面没有自由文本或提交表单，空输入测试N/A。

主控实际看过关系normal与新件关键normal／close。用途大轮廓、开放工位、空有载和主要开闭姿态可比较；**正常距离的小接口／检修盖／细微扫描动作辨识仍有限**，不能只以近景签完整视觉。固定close会裁切仓储／着陆器等大设施，完整轮廓以normal三方向验；近景实际方向可能与normal选择不同，图下注记明确展示。

## 候选与尚未完成

比例、六角色配色、源动作节奏与机构均为低保真候选。未定的视觉问题是：整场单位大小／缩放提示、微动作是否需要技术状态提示、地表参考与权威地形的实际形体差异；它们分别影响远距离辨识、运行态辨识和正式地表接入，不阻断当前源交付。

所有者最终视觉接受 **NOT_RUN**；正式Main注册／游戏联调、整场性能与LOD预算、物理碰撞／导航／保存 **NOT_RUN**；T01／T02正式阶段接入与拖救契约 **BLOCKED**。高保真、UV／贴图／烘焙、Astra制作 **NOT_RUN**。几何存在、接口存在、动画存在不批准玩法能力。

技术线main已只读核至`84371c3`且干净；PR34已有限接受单机器人到场／连续作业／整平提交／后帧验证，完整建设、导航、经济／能源、存档和正式美术仍未接受。美术没有修改它；具体接入所需输入见[技术交接](technical-handoff.md)。

## 实际作者与派单

GLM通过Agent Bridge技能（本机）派至ZCode独立会话；原生模型切换确认是GLM-5.3-Flash，Bridge observed_model为空，不能宣称平台自动证明了模型。主控负责冻结造型、具体返修判断、包装／manifest、Godot验证、图形与最终独立检查；没有把OpenAI子代理记作GLM。

| 子包 | 真实任务／会话 | 源提交 → 主控集成 | 结果 |
|---|---|---|---|
| A，U03/F05/F06 | task_01db3e6d19前置 → task_23fb772900制源 → task_9090cf51f7返修 → **task_390ccb5412**；sess_2a8c5b12d7 | 0d14aa0 → 7252fdd | 最终已交；[完整结果](lfA-delivery-result.json) |
| B，8件 | task_2add7873a6制源 → task_c5350f9518返修 → **task_4ad95558a9**；sess_fedb9781d9 | 69b5f4a → ee91dbd | 最终已交；[完整结果](lfB-delivery-result.json) |

原任务取消只中止turn，已落源保留再续；A最初30分钟只完成动画API前置，未当模型交付。工人自己的Godot烟测未成立／SIGABRT不当PASS，主控用原生引擎独立补齐。Bridge跨工作树files_changed扫描不作为作者归属，真实提交diff仅限票面源与运行GLB。两工人会话已真实结束（[dead回执](sessions-ended.json)），源／分支保留。费用 **UNKNOWN**；结果中的usage/context或cache-read不是计费金额，不能相加成成本。

architect独立核11源／21clip、计数、SHA、manifest与实际关键图，未发现新增交付阻断；同意小动作辨识和close裁切需保留说明，不代签所有者视觉。无缓存复验结果另见[收口审查](review.md)。

## 复现与失败证据

在美术工作树根运行：

```sh
python3 docs/art/production/evidence/lowfi-batch-r1/check_sources.py
python3 docs/art/production/evidence/lowfi-batch-r1/check_delivery.py
python3 docs/art/production/evidence/lowfi-batch-r1/prepare_preview.py
```

prepare逐字节复制交付的完整.glb.import；Godot高级导入的`_subresources.nodes["PATH:AnimationPlayer"]["optimizer/enabled"]=false`保留作者动作极值，依据[官方导入器源码](https://github.com/godotengine/godot/blob/master/editor/import/3d/resource_importer_scene.cpp#L1221)并以本机真实端点复验。不能写在逐clip的animations层级。默认优化时扫描头35°变30.625°、维修X.85变.862903、肩-20°变-15.55555°，关优化后恢复，见两份imported-track日志。

实际无缓存验证使用独立`prototype/.godot/art-lowfi-batch-r1-clean`，不依赖正式project/C#编译。以已有`evidence/dual-r1/run_godot.py`传`--headless --editor --path <private-project> --import`；然后`--headless --path <private-project> --script res://validate.gd -- <asset> res://assets/lowfi-batch-r1/<asset>.tscn <absolute-audit-path>`。Godot实机图必须去掉headless，`res://capture.tscn -- <plan-json> <new-output-dir>`；关系图为`res://relation.tscn -- <new-output-dir>`。make_capture_plan.py复现54姿态／284单体视图，make_gallery.py只索引真实图；新输出目录不得覆盖失败证据。Blender CLI `--background --python .../reopen_sources.py`可复现源重开。

失败日志完整保留，均不计PASS：leading-zero tscn／GDScript解析、默认优化器极值损失、旧helper断言未传播（repair-audit-rejected.json明确拒绝）、世界yaw轴框假重叠（改共同局部坐标后108组通过）、Godot将动画0时长夹到最小值导致无效负例（zero-duration-test-rejected，删该错误注入）、prepare首行改多行参数会残留内容（删重写，提交完整sidecar并无缓存重验）。验收同时检查日志无SCRIPT ERROR／ERROR／TIMEOUT、成功标记和当前SHA，不能只看引擎exit0。
