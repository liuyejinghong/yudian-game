# 地形网格生成探针 r1

2026-10-04，Codex冻结；起点main3999d30，前置[数据r1](terrain-patch-r1.md)已接受。本轮只构建两种纯CPU网格，不改变旧Main/fixture或世界状态，不宣称完成T3b/T3c的阶段更新与高度场系统比较。完整Issue6的碰撞/导航/保存/并发/成本仍未验。

## 接口与所有权

命名空间Yudian.Terrain，仅net8标准库。主控提供共享TerrainVertex(double X,Y,Z)值类型和TerrainMesh：GridRows/GridColumns、不可变Vertices/Indices；构造防御复制、拒绝非有限顶点/错误长度/越界索引，索引访问统一ArgumentException。只提供位置/三角索引，不含法线、UV、材质、Godot资源或权威版本。两个工人不修改共享类型/数据合同。

两个独立静态类分别StageMeshBuilder与HeightfieldMeshBuilder，接口相同：
- `TerrainMesh Build(TerrainSnapshot snapshot)`：原快照高度。
- `TerrainMesh Build(TerrainPatch patch, TerrainSnapshot current)`：先调用TerrainDataCodec.ValidateAgainst(patch,current)，再用patch候选高度和base布局。只版本一致仍须拒绝内容不同。失败没有输出或副作用。

null或非法参数统一ArgumentException带参数路径；不绕过数据解析入口构造Snapshot/Patch，不修改输入、版本或世界。不增加可变缓存、增量更新、采样API、Apply/Commit或新包。

## 固定几何

坐标双精度米制/Y向上、row+Z/column+X；输出全部世界坐标。顶点按row-major，cell遍历同样row-major。每cell四角索引a=左上、b=右上、c=左下、d=右下；顺序固定`a,d,b,a,c,d`，XZ投影叉积Y为正。2×2输出索引金样`[0,3,1,0,2,3]`。这是CPU绕序合同，Godot实际背面剔除另由适配票验证，未运行不冒称通过。

T3b-MESH：保留原rows×columns节点，不改变高度；每原cell两三角，固定a-d对角线。顶点数rows*columns，索引数6*(rows-1)*(columns-1)。这是阶段网格候选的生成子件，不是已经实现阶段管理/局部更新策略。

T3c-MESH：固定每原cell沿X/Z各细分2份（没有可配置LOD）；输出(2*rows-1)×(2*columns-1)节点。原节点必须直接拷贝原高度；水平/垂直中点取相邻节点均值，cell中心取四角均值。它们等价于双线性在0/.5/1处采样。坐标是origin + (fineIndex/2.0)*spacing。每fine cell仍按a-d对角线生成平面三角。双线性只用于生成顶点，三角面内部不等于精确双线性曲面；不是已完成高度场更新/碰撞实现。

允许2×N/N×2及最大257×257合法输入；T3c最大513×513=263169顶点、1572864索引。所有输入高度仍按数据r1范围，无拓扑折叠的XZ三角；不能把最大尺寸通过当作性能预算。完全不变patch合法且几何与base一致。

## 独立金样与边界

1. 数据r1的2×3非对称sample-a：T3b6顶点/12索引；T3c3×5=15顶点/48索引。原(0,2)坐标(-.5,4,2)；fine(1,1)为(-1.25,6.75,2.25)。防止行列转置。
2. sample-b的3×4候选内部改0/-1：T3b12/36，T3c5×7=35/144；所有原节点应与候选相同，输入base保持不变。
3. 扭曲2×2cell高度[0,0,0,4]，origin=(0,0)、spacing1：T3b对角中心(.5,.5)表面高2；T3c中心顶点高1。在(.75,.25)处T3c三角面高1，而精确双线性高.75。主控独立按三角重心复核，不能用顶点通过冒称整个曲面双线性。
4. 相邻两个3×3区域：left origin(0,0)，right origin(2,0)，spacing1，left右边与right左边沿Z为[3,5,9]，非零边界。对应原节点/中点，以及边内参数.25/.75，比较两候选实际三角边的世界XYZ；绝对误差<=1e-9米。允许顶点数量不同，按同一世界位置取值，不比较整数组相同。

外围插值为原节点之间piecewise-linear；细分与粗网格高度连续。但多顶点可能形成T-junction；不同候选之间未实现焊接、邻块法线一致性或LOD拼接。边界通过只说明位置/高度连续，不解除ART-T01/T02正式接入阻塞。

必测两种CultureInfo、计数/全部索引/正Y绕序/有限/确定性、非对称原节点与候选、扭曲cell、邻区边、最大/最小尺寸、null/过期/错误region/同版内容不符、输入和输出不可变（包括ICollection.SyncRoot）。独立Godot适配、图形/碰撞/导航/保存/更新成本留后续票，不在本轮伪造截图或性能结论。
