# tools/terrain-projection-tests

T-VIEW-R1（terrain-view-r1 冻结合同 GLM资源束章节）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/TerrainProjection/TerrainProjectionResources.cs`，测试脚本
`prototype/scripts/TerrainProjection/Tests/ProjectionResourcesTests.cs`，场景
`prototype/scenes/terrain-projection-tests/ProjectionResourcesTests.tscn`。native API 须在
Godot 进程内执行，不直接 dotnet 跑 DLL；运行现有 Godot 工程无新 csproj、无新增 NuGet 包。

资源束 `Yudian.Terrain.TerrainProjectionResources : IDisposable` 复用已验收
TerrainMeshAdapter.Create 与 TerrainCollisionAdapter.Create，返回全新 ArrayMesh +
ConcavePolygonShape3D；先 mesh 后 shape，任一阶段抛异常时释放已创建资源，成功由接收者
持有整个 bundle，替换/退出后 Dispose，重复 Dispose 幂等。不缓存、不持有 RegionState、
不绑定节点、不修改输入或材质。
验收状态由主控维护（本票不自验收、不签通行）。

## 运行（在仓库工作树根目录执行；$PROJECT_ROOT 是协调项目目录，含 tools-bin 与各工作树）

```sh
mkdir -p <空本地NuGet源目录>
DOTNET="$HOME/.dotnet/dotnet"
GODOT="$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot"

"$DOTNET" restore prototype/Yudian.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$DOTNET" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$GODOT" --headless --path prototype \
  --log-file /private/tmp/yudian-projection-worker-native.log \
  res://scenes/terrain-projection-tests/ProjectionResourcesTests.tscn
```

退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL，另有 SUMMARY 行）。
native 测试限 headless；不跑 GUI/性能（Create 双倍 mesh 构建成本是复用两个已验收 adapter 的
固有代价，数字不作性能结论）。

## 启动身份核对

测试第一条检查打印实际 CLR 版本、Assembly.FullName 与刚构建
`prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 SHA256，并把加载程序集的 MVID 与该文件的
PE MVID 比对（Godot 4.7 内存加载下 Assembly.Location 为空，如实记录）。不一致或文件缺失按
失败退出。

## 覆盖范围（命名用例）

- flat 2×2（Stage）与 sample-a 2×3 非对称（Heightfield）：bundle mesh 单 surface，展开顶点/
  法线按 CPU i0,i2,i1 发射映射逐角 1e-4 同值；shape GetFaces 角数 = CPU Indices.Count×3，
  位置与 mesh 发出顶点逐槽 1e-4 同值，BackfaceCollision=false。
- 独立束：同输入两次 Create 的 mesh RID 与 shape RID 均不同（无缓存），mesh/shape 内容一致；
  成功移交后实例保持有效（IsInstanceValid），活 mesh 经 RenderingServer 查询为真实表面资源，
  接收者 Dispose 前不被提前释放。
- 输入保持：Create 与 Dispose 后 CPU 顶点/索引缓冲与 snapshot 序列化均不变。
- 非法输入：null mesh、负绕序—— ArgumentException 原路径透传，无 bundle 产出，内部模拟失败
  记录不被公开路径触碰。
- 模拟中途失败：internal Create(mesh, failBeforeShape:true) 在 mesh 创建成功后、shape 前
  抛 InvalidOperationException（消息明确标注 simulated——这是合成中途异常，不冒称真实
  native 分配失败）；释放前记录的 mesh RID 用 RenderingServer 有效性查询证明确实释放。
- 重复 Dispose：幂等无异常；Dispose 后 mesh/shape 实例 IsInstanceValid=false、mesh RID 查询
  为已释放，属性访问抛 ObjectDisposedException。

## 释放证明方法与限制

- 活/释放两侧对照先行演示：已知活 mesh 查询返回 1、Dispose 后同 RID 返回 0，再对失败路径
  记录 RID 断言为 0——非资源计数猜测。
- 对已释放 mesh RID 调用 RenderingServer.MeshGetSurfaceCount 会在 stderr 打印引擎错误行
  （dummy 渲染后端为 `ERROR: Parameter "m" is null. at: mesh_get_surface_count`，返回 0），
  属查询已释放 RID 的预期现象，非测试失败。
- shape RID 无等效无噪声的 PhysicsServer3D 有效性查询；shape 释放由原实例
  GodotObject.IsInstanceValid 证明（合同允许"RenderingServer有效性或原实例IsInstanceValid"
  二选一），shape 存活期有效性另由 GetFaces 内容核验背书。
- 平台说明：RenderingServer/PhysicsServer3D 均无通用"RID 是否有效"布尔查询；本测试用
  MeshGetSurfaceCount 的 1/0 对照承载 mesh 侧有效性语义，已在上方如实记录取值含义。

## 边界

- 模拟中途失败为合成注入（内部测试钩子，公开 Create 默认 false）；真实 shape 阶段异常路径
  （native 空结果/face 数不符）是公开合法输入下不可注入的防御分支，由 collision adapter
  既有验收与代码审阅背书。
- headless 覆盖资源束创建/释放/独立/输入保持；节点绑定、权威版本、物理帧射线、GUI、导航、
  保存均不在本票范围。
