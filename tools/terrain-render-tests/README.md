# tools/terrain-render-tests

T3-GODOT（terrain-render-r1 冻结 TerrainMeshAdapter）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/TerrainRendering/TerrainMeshAdapter.cs`，测试脚本
`prototype/scripts/TerrainRendering/Tests/AdapterTests.cs`，场景
`prototype/scenes/terrain-render-tests/AdapterTests.tscn`。native API 须在 Godot 进程内执行，
不直接 dotnet 跑 DLL；无新增 NuGet 包、无新工程。

## 运行（在仓库工作树根目录执行）

```sh
mkdir -p <空本地NuGet源目录>
DOTNET=/Users/ethan/.dotnet/dotnet
GODOT=/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot

"$DOTNET" restore prototype/Yudian.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$DOTNET" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT=/Users/ethan/.dotnet "$GODOT" --headless --path prototype res://scenes/terrain-render-tests/AdapterTests.tscn
```

退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL，另有 SUMMARY 行）。

## 启动身份核对

测试第一条检查打印实际 CLR 版本、Assembly.FullName、AppDomain.BaseDirectory 与刚构建
`prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 SHA256，并把加载程序集的 MVID 与该文件的
MVID 比对（Godot 4.7 经加载上下文装载，Assembly.Location 为空，MVID 是确定性构建的字节身份等价物）；
不一致或文件缺失按失败退出。

## 覆盖范围（命名用例，非 CPU 数据 r1 全量 JSON 用例）

- flat 2×2（Stage 生成器保留原分辨率）：CPU 固定 a,d,b,a,c,d 索引；展开 6 顶点完整 i0,i2,i1 发出顺序与 Up 法线。
- sample-a 非对称：展开数 = CPU Indices.Count = 48；全部 48 槽位的 float 世界 XYZ 发出映射与逐三角平面法线。
- patch-b：StageMeshBuilder 与 HeightfieldMeshBuilder 两个生成器各适配一次；候选内部原节点 0/-1 的 readback Y；
  patch/base 序列化前后不变。
- surface 形状：单 surface、适配器只提供 Vertex/Normal 两通道、无索引/UV/颜色/材质、PrimitiveType.Triangles。
  实测 Godot 在有 Normal 无 Tangent 时由引擎自动生成切线通道（slot 2，非适配器输出），测试按此断言并留档。
- 独立资源：同输入两次构建 RID 不同（无缓存）且内容一致。
- 输入保持：Create 后 CPU 顶点/索引缓冲与快照序列化不变。
- 拒绝：null、double 超 float 范围、float 量化退化、负绕序、叉积溢出、有限叉积但长度平方溢出、
  次正规长度平方下溢为零法线——均 ArgumentException 且消息含 vertices[i]/triangle[i] 路径。
- 最大合法尺寸 513×513：展开 1572864 顶点/法线全有限且朝上、首末端点与远角精确。

## 限制

headless 覆盖资源构建与 readback；真实 back-cull 渲染观感由主控独立可视探针验收，不在本测试范围。
