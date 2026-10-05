# tools/terrain-projection-tests

terrain-view-r1 合同 GLM资源束章节的实际引擎测试入口。被测实现
`prototype/scripts/TerrainProjection/TerrainProjectionResources.cs`（IDisposable 资源束，
复用已验收 TerrainMeshAdapter/TerrainCollisionAdapter，mesh→shape 顺序创建，任一阶段抛异常
释放已创建资源，成功由接收者持有整个 bundle，重复 Dispose 幂等）。测试脚本
`prototype/scripts/TerrainProjection/Tests/ProjectionResourcesTests.cs`，场景
`prototype/scenes/terrain-projection-tests/ProjectionResourcesTests.tscn`。
运行现有 Godot 工程，无新 csproj、无新增 NuGet 包。

## 运行（在仓库工作树根目录执行；$PROJECT_ROOT 是协调项目目录，含 tools-bin 与各工作树）

```sh
DOTNET="$HOME/.dotnet/dotnet"
GODOT="$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot"

"$DOTNET" restore prototype/Yudian.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$DOTNET" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$GODOT" --headless --path prototype \
  --log-file <日志文件> \
  res://scenes/terrain-projection-tests/ProjectionResourcesTests.tscn
```

退出码 0 = 8 项命名检查全过；每项逐行打印 PASS/FAIL，另有 SUMMARY 行。首轮启动核对加载
Yudian.dll MVID == 当前 Debug 构建 PE MVID，并打印该文件 SHA256（不比对加载字节 SHA）。

## 所有权与覆盖

bundle 的 mesh/shape 归接收者持有，替换/退出后 Dispose。用例覆盖：mesh+shape 与 CPU 发射序
全角逐一等值（flat/Stage、sample-a/Heightfield）、双束 mesh/shape RID 独立且内容一致、
Create/Dispose 后 CPU 输入不变、null/负绕序 ArgumentException 透传、internal
failBeforeShape 合成中途失败（保留已释放 mesh 实例，原实例 IsInstanceValid + 已知活实例
阳性对照证明释放）、Dispose 幂等且释放后属性访问抛 ObjectDisposedException。

## 真实限制

- failBeforeShape 是合成注入（公开 Create 默认 false），不冒称真实 native 分配失败；真实
  shape 阶段防御分支在公开合法输入下不可注入，由 collision adapter 既有验收背书。
- 释放证明用原实例 IsInstanceValid（合同允许路径）；不查询失效 RID，本测试输出要求
  无 ERROR/FAIL 行。
- native 测试限 headless；GUI/性能不在范围。Create 复用两个已验收 adapter 导致 mesh 构建
  两次，是合同要求的固有代价，数字不作性能结论。
