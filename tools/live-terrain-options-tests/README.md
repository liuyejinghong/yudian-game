# live-terrain 选项独立检查

LIVE-TERRAIN-OPTIONS 包A：`--live-terrain` 旗标识别（user/engine/重复）与 benchmark 最终判定互斥、旧默认与 fixture 优先级不变。net10 console，仅编译 `prototype/scripts/Configuration/BenchmarkOptions.cs`，无 NuGet 依赖，不启动 Godot。

在工作树根目录，使用空本地 NuGet 源：

```sh
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" restore tools/live-terrain-options-tests/LiveTerrainOptions.Tests.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" build tools/live-terrain-options-tests/LiveTerrainOptions.Tests.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" run --project tools/live-terrain-options-tests/LiveTerrainOptions.Tests.csproj --no-build
```

全部 PASS 且退出 0 才通过；日志保留 `/private/tmp/live-terrain-options-worker-20261005`。
