# tools/terrain-render-tests

T3-GODOT（terrain-render-r1 冻结 TerrainMeshAdapter）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/TerrainRendering/TerrainMeshAdapter.cs`，测试脚本
`prototype/scripts/TerrainRendering/Tests/AdapterTests.cs`，场景
`prototype/scenes/terrain-render-tests/AdapterTests.tscn`。native API 须在 Godot 进程内执行，
不直接 dotnet 跑 DLL；无新增 NuGet 包、无新工程。
验收状态由[产研TODO](../../docs/engineering/tasks/todo.md)的T3-GODOT行维护。

## 运行（在仓库工作树根目录执行；$PROJECT_ROOT 是协调项目目录，含 tools-bin 与各工作树）

```sh
mkdir -p <空本地NuGet源目录>
DOTNET="$HOME/.dotnet/dotnet"
GODOT="$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot"

"$DOTNET" restore prototype/Yudian.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$DOTNET" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$GODOT" --headless --path prototype res://scenes/terrain-render-tests/AdapterTests.tscn
```

退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL，另有 SUMMARY 行）。

## 启动身份核对

测试第一条检查打印实际 CLR 版本、Assembly.FullName、AppDomain.BaseDirectory 与刚构建
`prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 SHA256，并把加载程序集的 MVID 与该文件的
PE MVID 比对（Godot 4.7 内存加载下 Assembly.Location 为空，合同运行澄清允许
loaded MVID == current Debug PE MVID，并记录该文件 SHA256）。不宣称对已加载字节做过 SHA 比对，
也不宣称 MVID 保证任意字节相同；不一致或文件缺失按失败退出。

## 覆盖范围（命名用例，非 CPU 数据 r1 全量 JSON 用例）

- flat 2×2（Stage 生成器保留原分辨率）：CPU 固定 a,d,b,a,c,d 索引；展开 6 顶点完整 i0,i2,i1 发出顺序与 Up 法线。
- sample-a 非对称：展开数 = CPU Indices.Count = 48；全部 48 槽位的 float 世界 XYZ 发出映射与逐三角平面法线。
- patch-b：StageMeshBuilder 与 HeightfieldMeshBuilder 两个生成器各适配一次；候选内部原节点 0/-1 的 readback Y；
  patch/base 序列化前后不变。
- surface 形状：单 surface、适配器只提供 Vertex/Normal 两通道、无索引/UV/颜色/材质、PrimitiveType.Triangles。
  实测 Godot 在有 Normal 无 Tangent 时由引擎自动生成切线通道（slot 2，非适配器输出），测试断言其长度 =
  发出顶点数 × 4（PackedFloat32Array 每顶点 4 分量）并留档。
- 独立资源：同输入两次构建 RID 不同（无缓存）且内容一致。
- 输入保持：Create 后 CPU 顶点/索引缓冲与快照序列化不变（含未被索引引用的顶点）。
- 拒绝：null、double 超 float 范围、float 量化退化、负绕序、叉积溢出、有限叉积但长度平方溢出、
  次正规长度平方下溢为零法线、未被索引引用顶点的 float 溢出——均 ArgumentException 且消息含
  vertices[i]/triangle[i] 路径。
- 最大合法输入经两个 Build 实际路径各一次：257×257 snapshot -> StageMeshBuilder 257×257 mesh
  （展开 393216）与 HeightfieldMeshBuilder 513×513 mesh（展开 1572864），核展开 count、全有限、
  单位朝上法线、原点与远角 (256,0,256)。
- 最大合法 Raw 513×513 TerrainMesh：展开 1572864 顶点/法线全有限且朝上、首末端点与远角精确。

## 限制

headless 覆盖资源构建与 readback；真实 back-cull 渲染观感由主控独立可视探针验收，不在本测试范围。
