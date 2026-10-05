# terrain-stage-tests

T3b-MESH `StageMeshBuilder` 合同测试：独立 console、零外部包、无测试框架，非 0 退出码表示有失败检查。

- 被测：`prototype/scripts/TerrainCandidates/StageGrid/StageMeshBuilder.cs`，与共享 `TerrainData/`、`TerrainGeometry/` 源码链接编译进本程序集。
- 命令（空本地 NuGet 源、完全离线）：
  - `dotnet restore Tests.csproj --source <empty-dir> -p:NuGetAudit=false`
  - `dotnet build Tests.csproj -c Release --no-restore`
  - `dotnet run -f net10.0 -c Release --no-build --no-restore`
- net8.0 按任务仅编译（NOT_RUN），不执行。全量检查在 InvariantCulture 与 de-DE 两种 CultureInfo 下各跑一遍。
