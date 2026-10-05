# tools/terrain-collision-tests

T3-COLLISION（terrain-collision-r1 冻结 TerrainCollisionAdapter）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/TerrainCollision/TerrainCollisionAdapter.cs`，测试脚本
`prototype/scripts/TerrainCollision/Tests/CollisionTests.cs`，场景
`prototype/scenes/terrain-collision-tests/CollisionTests.tscn`。native API 须在 Godot 进程内
执行，不直接 dotnet 跑 DLL；无新增 NuGet 包、无新工程。
验收状态由[产研TODO](../../docs/engineering/tasks/todo.md)的T3-COLLISION行维护。

## 运行（在仓库工作树根目录执行；$PROJECT_ROOT 是协调项目目录，含 tools-bin 与各工作树）

```sh
mkdir -p <空本地NuGet源目录>
DOTNET="$HOME/.dotnet/dotnet"
GODOT="$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot"

"$DOTNET" restore prototype/Yudian.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$DOTNET" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$GODOT" --headless --path prototype \
  --log-file /private/tmp/yudian-collision-worker-native.log \
  res://scenes/terrain-collision-tests/CollisionTests.tscn
```

退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL，另有 SUMMARY 行）。

## 启动身份核对

测试第一条检查打印实际 CLR 版本、实际 `physics/3d/physics_engine`、Assembly.FullName 与刚构建
`prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 SHA256，并把加载程序集的 MVID 与该文件的
PE MVID 比对（Godot 4.7 内存加载下 Assembly.Location 为空，如实记录，合同允许
loaded MVID == current Debug PE MVID + 记录文件 SHA256）。不宣称对已加载字节做过 SHA 比对。
不一致或文件缺失按失败退出。

## 覆盖范围（命名用例）

- stage flat 2×2：shape GetFaces 数量 = CPU Indices.Count = 6，位置与 ArrayMesh 发出顶点顺序
  逐槽 1e-4 m 同值，BackfaceCollision=false。
- sample-a 非对称 2×3（Heightfield）：GetFaces 48 == Indices.Count，与发出顶点逐槽 1e-4 同值，
  首槽端点 (-1.5,1,2)。
- 独立资源：同输入两次 Create 返回不同 shape RID（无缓存）、内容一致；临时 ArrayMesh 已在
  Create 内无论成功失败均释放，返回 shape 释放后仍可完整查询。
- 输入保持：collision Create 后 CPU 顶点/索引缓冲不变。
- 拒绝（原 render 其他矩阵不重复）：null mesh、未被索引引用顶点 float 溢出、float 量化退化、
  负绕序——均 ArgumentException 且消息含原路径（vertices[i]/triangle[i]）。
- 最大合法输入经两个生成器实际路径各一次（仅数量/端点核验，不作为性能结论）：
  257×257 snapshot -> Stage shape 393216 角、Heightfield shape 1572864 角，首角 (0,0,0)、
  含远角 (256,0,256)。
- 物理世界：新 Node3D 下两个 StaticBody 位置相隔（Stage 斜坡 layer 1、Heightfield 斜坡 layer 2），
  CollisionMask=0；查询 CollideWithBodies=true、Areas=false、HitBackFaces=false、
  HitFromInside=false，仅 _PhysicsProcess 读 DirectSpaceState，节点/shape 设置后至少等下一物理
  查询帧。金样斜坡 2×2 origin(0,0) spacing 2 heights[0,2,4,8]：非顶点局部 (x=.5,z=.25) 从上方
  往下，mask=1 命中 Stage 世界 Y=1.25、mask=2 命中 Heightfield（body 本地 1.125，世界 6.125），
  均按各自真实三角平面而非原 bilinear 面，命中点/朝上 normal 逐项打印；从下方往上不命中
  （单面）；地形 XZ 之外不命中；错 mask 不命中。
- 独立记录首次失败日志（--log-file），不覆盖。

## 限制

- InvalidOperationException 路径（native 空结果或 face 数不符）是公开合法输入下不可注入的
  防御分支：不修改被测 adapter 无法注入失败；该分支由代码审阅与全量 PASS 的 face 数核验共同
  背书，无独立失败注入用例。
- headless 覆盖 shape 构建与射线查询；真实渲染/美术材质、动态角色碰撞、导航、世界提交、保存
  均不在本票范围。
