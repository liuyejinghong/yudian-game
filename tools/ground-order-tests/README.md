# tools/ground-order-tests

level-job-r1 GLM包A（GROUND-ORDER）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/TerrainWorld/Actors/GroundPatrol.cs`（本包唯一修改的源文件），
测试脚本 `prototype/scripts/TerrainWorld/Actors/Tests/GroundOrderTests.cs`，场景
`prototype/scenes/ground-order-tests/GroundOrderTests.tscn`。SetOrder/ClearOrder 的运动行为
须在 Godot 进程内以真实 Velocity+MoveAndSlide 验证，不直接 dotnet 跑 DLL；无新增 NuGet 包、
无新工程、不寻路、不加碰撞层、不传送；不修改旧 GroundPatrolTests 或 Main/合同/TODO。

## 运行（在仓库工作树根目录执行；$PROJECT_ROOT 是协调项目目录，含 tools-bin）

```sh
mkdir -p /private/tmp/yudian-empty-nuget
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" restore prototype/Yudian.csproj \
  --source /private/tmp/yudian-empty-nuget -p:NuGetAudit=false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" build prototype/Yudian.csproj \
  -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot" --headless \
  --path prototype \
  res://scenes/ground-order-tests/GroundOrderTests.tscn \
  > /private/tmp/ground-order-worker-20261005/ground-order-native.log 2>&1
echo "exit=$?"
```

退出码 0 = 全部检查通过；非 0 = 存在失败（逐行打印 PASS/FAIL，另有 SUMMARY 行）。注意
PASS/FAIL/SUMMARY 经 Console.WriteLine 走 **stdout，不进 Godot `--log-file`**（那里只有引擎
自身日志），验收必须捕获完整 stdout+stderr 并保留真实退出码，不得用管道截断掩盖 exit；日志
中出现 ERROR 也按失败处理。测试内部每个物理场景带 deadline 帧超时（外加全局帧上限），超时按
失败计；调用方应另设进程看门狗（如 timeout 240s），挂起按非0处理。失败日志保留不覆盖。

因本包修改了 `GroundPatrol.cs` 共享运动路径，回归必须同时跑旧场景
`res://scenes/ground-patrol-tests/GroundPatrolTests.tscn`（同法构建，日志另存），
旧测试全过才算保留原巡逻/胶囊/速度/重力/Paused 合同。

## 启动身份核对

与 ground-patrol-tests 相同约定：打印实际 CLR 版本、实际 `physics/3d/physics_engine`、
Assembly.FullName，并把加载程序集 MVID 与刚构建
`prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 PE MVID 比对，文件 SHA256 另行记录；
Godot 4.7 内存加载下 Assembly.Location 为空，如实记录，不宣称对已加载字节做过 SHA 比对。
不一致或文件缺失按失败退出。

## 覆盖范围（全部headless；地面/到场判定均来自原生物理）

纯逻辑（_Ready）：

- 未 Initialize 时 SetOrder 抛 InvalidOperationException，且不建订单、不消费一次性
  Initialize 配额（随后可成功初始化）。
- 默认态：HasOrder/OrderReached 均 false，巡逻 TargetIndex 仍从 0 开始。
- 非法 target 拒绝矩阵：各轴 NaN、±∞、|坐标|>10000，抛 ArgumentException，不建订单；
  边界 |坐标|=10000 接受。
- ClearOrder 幂等：清订单、不重置 TargetIndex/TravelledM、不置 Paused。

帧驱动物理场景（各自deadline帧超时）：

- order flow：巡逻先跑起来，SetOrder 覆盖单目标；TargetIndex 全程冻结；只经
  MoveAndSlide 直线进场（逐帧位移方向与目标方向点积>0.95，XZ距离单调不增）；
  XZ<=0.25 到点保持90帧（IsOnFloor、位置不漂、OrderReached 恒真）；期间非法 SetOrder
  被拒；Paused=true 下 ClearOrder 两次不解除 Paused、不重置 TargetIndex/TravelledM；
  清除后从保留索引恢复巡逻并推进航点。
- invalid SetOrder keeps old target：进场20帧后连续3次非法 SetOrder（NaN/超限/∞）被拒，
  机器人继续奔原目标并真实到点。
- blocked order：订单目标在 50° 陡坡墙后，机器人被墙挡住：X 不越过接触点+0.15、不穿墙
  （X<=104.2）、180帧内永不 OrderReached、Blocked/IsOnWall 置位、TargetIndex 冻结。
- paused airborne over target：下单目标为出生点正下方（XZ距离0）+Paused 首帧前悬空：真实
  重力下落≥1m，≥10个空中物理帧逐帧断言 OrderReached=false（XZ<=.25 但 IsOnFloor=false，
  杀掉移除 IsOnFloor 保护的变异）；XZ冻结、TargetIndex不推进；稳定贴地30帧后断言
  OrderReached=true；随后 SetOrder 覆盖为远目标（立即变回 false），解除 Paused 后真实
  到场到点。
- replace order mid-chase：奔向第一目标途中（d1<=5m）SetOrder 覆盖：改道（对旧目标距离
  转增、对新目标单调减）并真实到点，TargetIndex 全程冻结。

## 合同边界

只验证组件级合同：单目标覆盖、到点保持、清除回巡逻、非法拒绝保留旧目标、无寻路/新碰撞层/
传送。任务编排（到场失败60s、占用等待15s、版本核验、故障绑定）属主控 LEVEL-JOB 范围，
不在本包。
