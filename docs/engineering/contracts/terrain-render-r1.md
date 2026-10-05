# 地形 Godot 适配与候选预览 r1

2026-10-04，Codex冻结；起点main0a1e54e，前置[CPU网格r1](terrain-mesh-r1.md)已合并。只把两种CPU网格变为可显示的Godot资源并做独立候选预览；不实现阶段/高度场增量更新、世界提交、碰撞导航、保存或收益。

## GLM单票接口与归属

`Yudian.Terrain.TerrainMeshAdapter`静态类：`ArrayMesh Create(TerrainMesh mesh)`，调用者负责在已启动Godot的主线程调用和资源生命周期。源码只新增`prototype/scripts/TerrainRendering/`；实际引擎测试脚本可在该目录Tests子目录；测试场景只新增`prototype/scenes/terrain-render-tests/`；`tools/terrain-render-tests/README.md`记录命令。不修改CPU数据/生成/共享类型、Main、项目/依赖、美术、主控probe或公共合同/TODO。

Create返回一个全新ArrayMesh，单surface、PrimitiveType.Triangles、调用方只提供Vertex/Normal两个通道（Godot读回自动补Tangent，见运行澄清），无索引/UV/颜色/材质/LOD/压缩选择/缓存。展开顶点数=CPU Indices.Count，三角数=Indices.Count/3。每CPU三角(i0,i1,i2)按**i0,i2,i1**发出位置；它是Godot顺时针front-face与CPU正Y叉积之间的显式适配，不修改原CPU索引。禁止以双面材质/关闭剔除掩盖错误。

全部位置由double转当前Godot Vector3的float分量；先检查转换后有限。法线按原CPU三角的三个float位置计算 `(p1-p0).Cross(p2-p0)` 并规范化，同组三顶点同一平面法线。要求叉积有限、Y>0、长度非零且归一化有限、单位长度误差<=1e-4；float量化退化、溢出或错误绕序拒绝，不能补默认法线。只有全部转换/校验成功后才创建native ArrayMesh，避免拒绝时半成资源。null/非法参数统一ArgumentException，消息含mesh/vertices[i]/triangle[i]路径，无输入变化或可见资源输出。

Godot SurfaceGetArrays读回位置与float转换目标误差<=1e-4米（此探针金样）；法线单位长度/方向可允许Godot打包误差<=.005。不把CPU1e-9边界承诺当作全世界float精度保证。对大坐标的测试只比较明确float转换目标；公开TerrainMesh允许超出数据域的有限double，适配器仍须拒绝float溢出和量化退化。

依据已安装GodotSharp4.7.2 XML和[官方ArrayMesh说明](https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html)。官方顺时针是约定依据，仍由主控在真实渲染器以back-cull上/下表面对照复验；未运行不能声称通过。

## GLM测试与工具

用现有SDK10.0.401和Godot4.7.2 .NET。不建另一游戏工程、不增加NuGet包；离线空NuGet源restore `prototype/Yudian.csproj`，Debug单节点/UseSharedCompilation=false/nodeReuse:false build；显式DOTNET_ROOT指向现有$HOME/.dotnet，实际启动时核对Debug程序集身份；Godot `--headless --path <工作树/prototype> res://scenes/terrain-render-tests/AdapterTests.tscn` 真正执行，不直接dotnet运行依赖native的测试DLL。

测试启动打印实际CLR、Assembly.Location、当前工作树Debug文件SHA256，并比对已加载程序集MVID与该文件PE元数据MVID。Godot4.7.2内存加载导致Location为空，不能声称已对加载字节做SHA比对；以MVID核对当前Debug构建身份，不以外部旧缓存作为通过。

少量命名检查足够：2×2平面完整发出顺序/Up法线；sample-a非对称count/世界XYZ；patch的两个生成器与候选原节点/readback；每个surface的通道/无材质与索引/独立资源重复构建；输入序列化/CPU缓冲保持；null、float溢出、float量化退化、负绕序、法线叉积溢出与有限叉积但长度平方溢出/零单位法线拒绝；最大合法尺寸各一次并核对count/有限/端点。不是重复数据r1全部JSON用例。每次明确PASS/FAIL与失败exit，保留原失败日志。工人只运行headless，不操作GUI、截图或生产美术。

## 主控独立可视探针与更新语义

主控唯一写者：`prototype/scripts/TerrainProbe/`、`prototype/scenes/terrain-probe/`、`prototype/fixtures/terrain-probe-r1/`、`tools/terrain-probe/`、工程需求/证据/TODO/PLAN。两种算法并排显示同一7×7输入（origin -6/-6，spacing2），base、内部整平、内部开挖三状态，外围不变。主控编写固定镜头/白昼/调试材质，不引用或改共享美术材质。这只是产研形变对照，不是最终美术要求。

选中同一状态重复请求为no-op，不增展示计数。只有两份新CPU/ArrayMesh全构建成功后才替换旧显示，并增加一次展示计数；任一步拒绝必须保留旧mesh、当前选择和计数，新临时资源及时释放。替换后释放旧资源，退出也清理。基准快照/版本不变，不存在world Apply/Commit。

“撤销候选预览”只切回未提交基准，不叫取消世界改造；未来真正取消必须保留已发生结果。失败注入为过期current或外围非法patch，不能用假错误提示代替真实Codec/ValidateAgainst拒绝。

更新实现为**完整CPU重建+全新ArrayMesh替换**，记录两算法CPU节点数、发出顶点/三角数、生成+适配提交耗时；后者不包括GPU完成或导航/保存，不叫增量更新，不设未测性能门槛。float/平面法线/展开缓冲额外成本和接缝限制明确留档。

主控验收实际headless拒绝/重复/切换/资源替换与退出、GUI按钮/键切换、真实back-cull上面可见/下面被剔除、同镜头三状态PNG及渲染器身份、冷重开基准。图片是探针实际图形，视觉效果最终验收仍由所有者；headless不能替代图形证明。完整T3与正式地形美术接入仍未完成。

## 运行事实澄清（主控，2026-10-04）

首轮实际headless暴露两项冻结时未识别的引擎行为，保留失败日志，不由工人自行改验收：Godot内存加载Assembly.Location为空，采用上述MVID与构建文件SHA证据；仅提供Vertex/Normal仍由引擎读回自动生成Tangent。调用方不得主动提供Tangent，读回允许原生PackedFloat32Array且长度为展开顶点数×4；其他额外通道仍拒绝。这里澄清实际引擎输出，不增加压缩选择或业务纹理接口。
