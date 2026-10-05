# tools/ground-patrol-tests

main-ground-r1 GLM包B（GROUND-PATROL）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/TerrainWorld/Actors/GroundPatrol.cs`，测试脚本
`prototype/scripts/TerrainWorld/Actors/Tests/GroundPatrolTests.cs`，场景
`prototype/scenes/ground-patrol-tests/GroundPatrolTests.tscn`。运动行为须在 Godot 进程内
以真实 Velocity+MoveAndSlide 验证，不直接 dotnet 跑 DLL；无新增 NuGet 包、无新工程、
不修改已接受的 TerrainMeshAdapter/TerrainCollisionAdapter/TerrainRegionView。

## 运行（在仓库工作树根目录执行；$PROJECT_ROOT 是协调项目目录，含 tools-bin）

```sh
mkdir -p /private/tmp/yudian-empty-nuget
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" restore prototype/Yudian.csproj \
  --source /private/tmp/yudian-empty-nuget -p:NuGetAudit=false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" build prototype/Yudian.csproj \
  -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot" --headless \
  --path prototype \
  res://scenes/ground-patrol-tests/GroundPatrolTests.tscn \
  > /private/tmp/ground-patrol-worker-20261005/ground-patrol-native.log 2>&1
echo "exit=$?"
```

退出码 0 = 全部检查通过；非 0 = 存在失败（逐行打印 PASS/FAIL，另有 SUMMARY 行）。注意
PASS/FAIL/SUMMARY 经 Console.WriteLine 走 **stdout，不进 Godot `--log-file`**（那里只有引擎
自身日志），验收必须捕获完整 stdout+stderr 并保留退出码，不得用管道截断掩盖 exit。测试内部
每个物理场景带 deadline 帧超时（外加全局帧上限），超时按失败计；调用方应另设进程看门狗
（如 subprocess timeout 240s），挂起按非0处理。失败日志保留不覆盖。

## 启动身份核对

与 terrain-collision-tests 相同约定：打印实际 CLR 版本、实际 `physics/3d/physics_engine`、
Assembly.FullName 与刚构建 `prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 SHA256，
并把加载程序集 MVID 与该文件 PE MVID 比对；Godot 4.7 内存加载下 Assembly.Location 为空，
如实记录，不宣称对已加载字节做过 SHA 比对。不一致或文件缺失按失败退出。

## 覆盖范围（全部headless；地面判定均来自原生物理）

纯逻辑（_Ready）：

- 合同常量与默认态：TargetIndex 初始0、Paused 默认 false、TravelledM/Blocked 初始 0/false；
  CollisionLayer=2、CollisionMask=1、MotionMode=Grounded、UpDirection=Up、
  FloorMaxAngle=35°、FloorSnapLength=0.5、FloorStopOnSlope/FloorBlockOnWall=true；
  唯一 CollisionShape3D：胶囊 r=0.35m、h=1.1m、Position.Y=0.55（脚底为节点原点）。
- 生命周期：非法输入抛 ArgumentException 且不建任何碰撞子节点，被拒调用不消费一次性配额，
  之后可成功初始化一次；成功后重复 Initialize 抛 InvalidOperationException。
- 拒绝矩阵：null、少于2点、非有限坐标、|坐标|>10000、相邻XZ相同（含末点→首点）、
  speed 非 (0,20]（0、负、NaN、∞、>20）；speed=20 边界接受。

真实 MoveAndSlide（_PhysicsProcess，静态box世界，六个互不重叠XZ区域）：

- 平面闭环：落地贴地（脚底Y≈0、持续 IsOnFloor）、逐tick实际XZ位移 ≤ speed·dt
  （min(speed, distance/delta) 不越步长）、到点XZ容差0.25m内才换点、TravelledM 与实际路程
  一致、开放平地 Blocked 保持 false；初始化后篡改外部航点数组仍走向原目标（防御复制）。
- 20°可走坡（<35°）：上坡到顶后**真实下坡折返回低端**（第二次回到低端航点，非仅一次到顶），
  全程贴地、不滑落、不穿入，到点判定同上；判收时刻用 terrain-mask(1) 真实向下 ray 取支撑面，
  按胶囊半径（合同0.35m）与命中法线推期望脚底高 `hit.Y + 0.35·(1/normal.Y − 1)`，
  要求 |脚底Y − 期望| ≤ 0.05 且 IsOnFloor。这是测试 oracle，组件不含 sampler、不手设Y。
- 50°陡坡（>35° FloorMaxAngle，墙语义）：接近后被挡，IsOnWall/Blocked 置位，X 不越过
  接触点、不爬坡不穿过，Y 保持地面高度，停滞120帧后判收。
- Paused：暂停帧内 XZ/TravelledM/TargetIndex 冻结，仍 IsOnFloor 且 Y 稳定（重力/地面仍
  处理）；恢复后继续前进 ≥1.5m。
- 空中已Paused（防IsOnFloor缓存假通过）：空中暂停的机器人经真实重力下落并落地（下落量
  ≥1m、落地Y≈0、**连续**贴地稳定≥60帧，离地帧即归零重计），XZ全程冻结——证明Paused分支
  不靠缓存的地面状态直接跳过。
- 微小航点差（1mm XZ，合法相邻差）：目标持续轮换 ≥20 次无停摆，位置/速度/TravelledM
  全程有限（无 NaN）。

## 限制

- Blocked 只表示"本帧希望水平移动但实际XZ<1e-4且被墙/陡坡阻挡"，不构成可达性算法或导航
  承诺；机器人彼此/设施避让、寻路、建设/经济均不在本票，由主控包处理。
- 胶囊尺寸、速度、35°/0.5m/9.8 等是本工程验证常量，不是正式机器人平衡或最终外形碰撞。
- headless 覆盖物理与输入验证；三个灰模外观由 Main 制作，渲染/美术、保存、性能均不在本票。
