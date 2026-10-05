# main-ground-r1 · 主场景权威地形与物理巡逻

2026-10-05冻结，基线7f6667e9da070b9bb7dbc2ee7f836c5e3cafb930。复用Main场景/设施/灰模，不再创建只有地形的展示场景。显式`--live-terrain`启用新路径；默认与历史benchmark仍为原灰模负载。新模式不签完整导航、动态避让、经济/建设工序、保存或正式美术。工程独立启动/验收。

## GLM包A · LIVE-TERRAIN-OPTIONS

归属只有`prototype/scripts/Configuration/BenchmarkOptions.cs`与新`tools/live-terrain-options-tests/`。新增bool LiveTerrain，默认false；无值旗标`--live-terrain`在user与engine参数识别，同scope重复拒绝，未知user依旧拒绝。与Benchmark最终判定结果互斥：包含user --benchmark、任一 --yudian-benchmark、--duration、环境开关；同时为true抛ArgumentException，消息同时含live-terrain与benchmark。先沿用已有扫描/值优先级，不新建参数解析器，不收窄FixtureConfig。冻结独立console检查flag/user/engine/重复/组合/未知、旧默认与fixture优先级；net10标准库工程只引用BenchmarkOptions，无NuGet依赖。既有109例QA-02由主控复验。

## GLM包B · GROUND-PATROL

归属只有`prototype/scripts/TerrainWorld/Actors/GroundPatrol.cs`、`prototype/scripts/TerrainWorld/Actors/Tests/GroundPatrolTests.cs`、`prototype/scenes/ground-patrol-tests/GroundPatrolTests.tscn`、`tools/ground-patrol-tests/README.md`。

`Yudian.Terrain.GroundPatrol : CharacterBody3D`，`Initialize(Vector3[] waypoints,float speed)`仅一次，先验证后建collision child；复制输入，不依赖外部修改。>=2点，每点全部坐标有限且绝对值<=10000；相邻点（包括末点→首点）XZ须不同；speed有限(0,20]。非法输入ArgumentException，不建半个collider；重复初始化InvalidOperationException。

脚底为Node原点；胶囊radius0.35m、height1.1m，CollisionShape3D.Position.Y=.55m。CollisionLayer=2、CollisionMask=1（仅本票静态地形；机器人彼此/设施避让未实现）。Godot Grounded mode，UpDirection=Up；FloorMaxAngle35度、FloorSnapLength.5m、FloorStopOnSlope/FloorBlockOnWall=true，gravity9.8m/s²。三个灰模外观继续由Main制作，不引入模型。

所有移动只在_PhysicsProcess，经Velocity+MoveAndSlide；不得直接设Y、传送越墙、补TerrainHeight或做双线性高度预测。朝当前目标XZ走，速度不超过speed且不越过近目标（min(speed,distance/delta)）；到点XZ容差.25m后换下一点，初始目标索引0。重力/FloorSnap由原生负责。公开bool Paused（默认false），暂停时不推水平运动/航点，仍处理重力/地面；只读TargetIndex、TravelledM（累加实际XZ位移）、Blocked（本帧希望移动但实际XZ<1e-4且有墙/陡坡阻挡）用于主控验收/调试，不能以它宣称可达性算法。未Initialize时不移动；两个目标差异/小delta不得NaN。

native自测只headless，最小实际覆盖：平面贴地前進、可走斜坡、>35度陡坡或静态墙阻挡不穿过、Paused不前进/恢复、输入拒绝/防御复制与一次初始化。使用静态地形或box primitive，不修改已接受资源束/RegionView来伪造通行；至少等后续物理帧再判地面状态，设置检查timeout并非0失败。不得只手动调用C#函数来冒称真实MoveAndSlide。测试以无ERROR/FAIL、exit0为门槛；原失败日志保留。数值是本工程验证常量，不称正式机器人平衡/最终外形碰撞。

## 主控包 · MAIN-GROUND

主控独占Main.cs、Main.LiveTerrain.cs、Main真实自测入口/任务跟踪/合同，GLM不修改。新模式建场景前生成并用现有codec校验r1 snapshot（rows=segments+1、version0、区域原点-size/2、spacing=size/segments、高斯旧函数采样）；超出r1范围或场内布局拒绝，旧默认/benchmark原fixture范围不变。禁与benchmark组合，无静默降采样。

复用TerrainRegionView持有权威；等待物理同步后用terrain mask真实射线初始化设施与机器人脚底，不写StageTerrainSampler。设施/机器人物理位置使用当前shape；运动交原生CharacterBody。Main用现有三类灰模、seed航点与设施，同一入口可操作土坡整平/矿点挖低/Current投影恢复；不能将手动地形操作叫完整建设工序。

提交前暂停机器人；改一个顶点会影响周边cell，检查所有受影响cell世界XZ矩形与机器人胶囊足迹（radius .35）、设施保守占地相交（含已有自转几何的最大半径）。有占用拒绝，不动权威/投影；已验证旧物理状态可恢复原运动，让占用对象离开，不制造死锁。失败或投影不一致保持暂停并显示原因。成功提交后至少等下一PhysicsProcess，核对mesh/shape全部角与受影响节点真实射线高度，再恢复运动；绑定故障从Current恢复，不撤销版本/回执。无改造可见但机器人仍用旧高斯高度的路径。

主控验收：参数与旧QA回归；真正Main非基准默认与短benchmark兼容；live前台GUI/原生物理，12机器人贴地移动，改造前后版本/画面/射线一致，陡坡/墙停滞（由GLM原生用例），设施与机器人占用拒绝、无人区改造后继续运动、投影故障暂停/恢复；拒绝不发资源，无经济代码。所有运行身份/原日志明确，本票不作性能结论。正式资产仍缺则挂已有ART-LINK票，不让美术执行工程试玩。
