# 地形静态碰撞与候选联调 r1

2026-10-04，main991b78e，主控冻结；所有者授权持续产研与美术联调、合格交付直接合并。前置TerrainData/两个CPU生成器/ArrayMesh适配已受限接受。只做独立候选的静态三角碰撞、显示同源和材质接口，不实现完整T3、动态物理角色、导航、世界提交、保存或收益。

## GLM单票 T3-COLLISION

新增静态类`Yudian.Terrain.TerrainCollisionAdapter`，接口`ConcavePolygonShape3D Create(TerrainMesh mesh)`。调用者须在已启动Godot主线程调用，接收者拥有返回shape及释放责任；仅用于StaticBody3D静态地形，凹三角壳不等于实体体积或动态机器人碰撞。

实现直接重用已验收`TerrainMeshAdapter.Create(mesh)`的全部顶点float/三角/单位法线验证与绕序，临时ArrayMesh经`CreateTrimeshShape()`生成全新ConcavePolygonShape3D，设置`BackfaceCollision=false`；临时ArrayMesh无论成功失败均释放，shape不得依赖其存活。native空结果或不合法face数量明确InvalidOperationException，不返回半合法资源；异常时也释放已产生但未返回shape。非法CPU/null继续ArgumentException带原路径，不改原adapter契约、不重写转换/法线算法。shape.GetFaces长度=mesh.Indices.Count，位置与ArrayMesh的发出顶点顺序同值（本轮读回金样1e-4m）；无节点、变换、材质、缓存、mask选择或新数据类型。

唯一可新增目录：`prototype/scripts/TerrainCollision/`（adapter与Tests原生脚本）、`prototype/scenes/terrain-collision-tests/`（CollisionTests.tscn）、`tools/terrain-collision-tests/`（README）。禁改既有文件、CPU/Rendering/Probe、project/Main、艺术资源/文档、主控合同/TODO/PLAN。无新依赖/安装/下载/付费API/GUI；新.uid允许，缓存/日志不入库。

### 工人验证

现有SDK10.0.401、Godot4.7.2，空本地NuGet源restore、Debug单节点禁共享编译build；显式DOTNET_ROOT=$HOME/.dotnet，实际Godot headless执行CollisionTests.tscn，打印CLR/当前Debug文件SHA及loaded MVID==文件PE MVID，Location空如实记录；不能声称加载字节SHA比对。保留初次失败日志，每项PASS/FAIL与失败exit1/成功exit0。两次同一步失败保留诊断并缩票，不改标准或金样蒙混过关。

少量命名用例：flat2×2、非对称2×2斜坡、两个生成器的同patch，核GetFaces与已适配render发出位置/数量、输入不变、同输入返回独立RID、false backface、临时mesh释放后shape可用；null/未引用顶点溢出/量化退化/负绕序拒绝（原render其他矩阵不重复）。最大合法两个生成器输入各做一次shape数量/端点核验，不作为性能结论。

真正物理用例在新Node3D世界中两个StaticBody位置相隔，layer分别1与2，CollisionMask=0；查询mask明确1或2，CollideWithBodies=true/Areas=false、HitBackFaces=false、HitFromInside=false，从已知上方往下、下方往上、地形XZ之外分别测。只在_PhysicsProcess读DirectSpaceState，节点/shape设置后至少等下一物理查询帧；记录实际`physics/3d/physics_engine`和命中对象/世界位置/正常朝上normal。非顶点斜坡金样：2×2origin(0,0),spacing2,heights[0,2,4,8]，局部(x=.5,z=.25)的Stage平面应Y=1.25、Heightfield细分平面应Y=1.125（按各自真实三角平面，不按原bilinear面）；上方命中、下方/外部不命中、错mask不命中。若实测与金样冲突先报告，不改expected值。

依据：[官方ConcavePolygonShape3D](https://docs.godotengine.org/en/stable/classes/class_concavepolygonshape3d.html)、[RayQuery](https://docs.godotengine.org/en/stable/classes/class_physicsrayqueryparameters3d.html)，实际4.7.2 GodotSharp XML已核API；物理后端/单面及更新时序由实测确认。

## 主控探针更新与接受

主控唯一修改已接受TerrainProbe及其自身新增测试/工具、工程文档；默认无碰撞入口保持旧行为。新增`--terrain-collision`，两个StaticBody与显示mesh完全相同的平移、不同layer；`--terrain-collision-self-test`启用碰撞和分帧独立检查。

GUI请求进入单槽pending，当前物理回合仅消费最新请求。同帧level→dig只执行dig；base当前时level→base须撤销先前pending而保持base/no-op；level→非法最新请求必须清掉早先pending，拒绝后不得后续帧突然显示旧候选。同一个pending重复请求no-op；当前状态重复请求在排队被消费后no-op。它只是未提交候选预览请求，不是游戏订单。

在_PhysicsProcess内先准备两个CPU/ArrayMesh与两个shape，全部成功才绑定四资源并修改mode/count；失败保留旧四资源/mode/count，清空此次pending并释放全部半成资源。交换后释放旧shape/mesh，退出先脱离节点再释放。四属性赋值只能证明逻辑替换完成；至少下一物理帧查询命中当前新body位置/高度后记录physics_synced，不能把赋值帧当native同步完成。完整重建且CollisionAdapter临时重复ArrayMesh构建成本留档，不称GPU增量更新。

主控覆盖三种排队序列、重复、真实stale/current/perimeter拒绝、第二shape失败注入、旧四资源与半成资源释放、下一物理帧斜坡/中心/外部射线、冷重开base/version不变、真实GUI与固定镜头PNG；性能/导航/保存未运行不签通过。

## 美术联调归属与缺口

美术会话独占docs/art与materials，工程不争写。M01已交付但等待该组主控接受；接受后固定其commit与SHA，只读soil_mars，`--terrain-material res://...soil_mars.tres`按surface0绑定两份网格，整件MaterialOverride必须为空。缺资源/错类型/双面或透明材质显式拒绝，无替代材质；同样几何/灯光/镜头比较debug与M01，记录材质SHA和resource RID。不签最终颜色或美术视觉接受。

正式U01/土坡T01/矿点T02未交付与正式预览实现缺口记工程TODO并链接美术任务；对应美术状态由美术组维护。只有实际交付源、GLB、manifest与逐surface角色/节点/尺度、Godot真实导入和异常检查等证据后解除相应联调阻塞，校准件与概念图不能充当正式素材。
