# tools/player-visual-tests

D1.0 E03（GLM-B，筑垒视觉适配）的实际引擎测试入口与说明。
被测实现 `prototype/scripts/Presentation/ZhuleiVisual.cs`，测试脚本
`prototype/scripts/Presentation/Tests/ZhuleiVisualTests.cs`，测试场景（独占入口）
`prototype/scripts/Presentation/Tests/ZhuleiVisualTests.tscn`。视觉适配必须在 Godot
进程内以真实 canonical 资产 `res://assets/lowfi-batch-r1/models/zhulei.glb` 验证，
不直接 dotnet 跑 DLL；无新增 NuGet 包、无新工程、不复制/不改模型与导入设置、
不修改 Main/shared/canonical GLB/manifest/全局材质。

## 运行（在仓库工作树根目录执行；tools-bin 取协调项目根）

```sh
mkdir -p /private/tmp/yudian-empty-nuget
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" restore prototype/Yudian.csproj \
  --source /private/tmp/yudian-empty-nuget -p:NuGetAudit=false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" build prototype/Yudian.csproj \
  -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$PWD/../tools-bin/Godot.app/Contents/MacOS/Godot" --headless \
  --path prototype --import > /tmp/player-visual-import.log 2>&1
DOTNET_ROOT="$HOME/.dotnet" "$PWD/../tools-bin/Godot.app/Contents/MacOS/Godot" --headless \
  --path prototype \
  res://scripts/Presentation/Tests/ZhuleiVisualTests.tscn \
  > /tmp/player-visual-native.log 2>&1
echo "exit=$?"
```

（`--import` 只需在 .godot 缓存不存在时执行一次，用于 canonical GLB 的既有导入；
测试场景入口在 `res://scripts/Presentation/Tests/`，属本包独占目录。）

退出码 0 = 全部检查通过；非 0 = 存在失败（逐行打印 PASS/FAIL，另有 SUMMARY 行）。
PASS/FAIL/SUMMARY 经 Console.WriteLine 走 **stdout**，不进 Godot `--log-file`；验收必须
捕获完整 stdout+stderr 并保留真实退出码，不得用管道截断掩盖 exit；日志出现 ERROR 也按
失败处理。本测试全部在 `_Ready` 一次跑完（不依赖物理帧推进），建议调用方仍设进程看门狗
（如 timeout 240s），挂起按非0处理。失败日志保留不覆盖。

## 启动身份核对

与 ground-patrol-tests 相同约定：打印实际 CLR 版本、实际 `physics/3d/physics_engine`、
Assembly.FullName，并把加载程序集 MVID 与刚构建
`prototype/.godot/mono/temp/bin/Debug/Yudian.dll` 的 PE MVID 比对，文件 SHA256 另行记录；
Godot 4.7 内存加载下 Assembly.Location 为空，如实记录。不一致或文件缺失按失败退出。

## 覆盖范围（全部 headless，真资产 zhulei.glb + 既有导入/材质）

- 生命周期：Initialize 前 Apply 抛 InvalidOperationException；Initialize 仅成功一次，
  重复调用抛 InvalidOperationException；初始 State=idle、StateTime=0。
- canonical 资产：模型实例化为子节点 Model；子树内恰一个 AnimationPlayer；五个源 clip
  齐全且时长等于 manifest rev1（move 1.0s、work 2.0s、charge/disabled/maintenance 1.0s）。
- 根 identity/单位/脚底原点：适配器根 transform 恒 identity；模型根 ≈ identity；
  静态包络按每个 Mesh `SurfaceGetArrays` 实际顶点经链式 transform 变到 Model 根坐标
  （manifest bounds 的"实际顶点"口径，逐轴记录来源 mesh），对照 manifest bounds.static
  ±0.01——实测逐轴吻合到 1e-4：min=(-0.87, 0.0000, -0.8106) max=(0.87, 1.1034, 0.80)。
  不用 `Mesh.GetAabb` 八角变换：旋转轮等局部 AABB 空角会虚假扩大包络（曾把 minY 虚降到
  -0.044，属测量算法假偏差，非导入差异）。
- 六键路由：idle/move/work/charge/disabled/maintenance 全部接受并路由到 manifest clip；
  manifest 有而 D1.0 合同没有的 `towed` 按非法键拒绝且不改 State。
- 循环回绕：move 轮子真实转动，t=1.25 pose == t=0.25（1.0s 回绕）；work t=2.5 == t=0.5
  （2.0s 回绕）。
- 单次动作：charge/disabled/maintenance 按 clip 长度钳制定格（CurrentAnimationPosition
  == length），继续喂 delta pose 不再变。
- paused：StateTime/playback position/pose 全冻结（不伪造动作）；恢复后从冻结时刻继续；
  paused 期间切换状态键立即生效但定格新 clip t0，解除后才推进。
- 非法拒绝先验后改：未知键（bogus/null/空串/towed/大小写不符）与非法 delta
  （NaN/±Inf/负/超 DeltaLimit=10s）逐一拒绝，State/StateTime/pose 保持原有效状态；
  拒绝后合法调用继续推进旧状态。delta=0 与 DeltaLimit 边界接受（10s 在 1.0s 循环上
  回绕回同 pose）。
- 合法切换复位防串扰：按 manifest changed_targets 逐名核验——work 动
  Shoulder/Elbow/SupportSlideL/SupportSlideR，切到 charge 后四者必须回基线而
  ChargePivot 可见移动；charge→maintenance 后 ChargePivot 回基线而 HoodLid 移动；
  maintenance→disabled 后 HoodLid 回基线而 Shoulder 移动；回 idle 全子树逐节点回基线。
- 父节点不动：非 identity 父节点（物理 actor 替身）transform 恒不变，适配器根恒 identity。

## 合同边界

只验证适配器：状态键消费、delta 驱动时间、paused 语义、复位防串扰、根/父不动。
主控如何把真实位移/工段映射为 move/work 键、BodyRadius=.9 选区 offset、A01 视口联调、
渲染表现、充电/维修能力、运输与救援均不在本票；D1.0 不伪造充电维修，服务状态仅测适配。
