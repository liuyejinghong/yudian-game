# terrain-commit-policy-tests

T3d-COMMIT-POLICY `TerrainCommitPolicy.Evaluate` 纯判定自测：独立 console、零外部包、无测试框架，非 0 退出码表示有失败检查。

- 被测：`prototype/scripts/TerrainCommit/Validation/`（枚举 + 静态判定），与共享 `TerrainData/` codec 源码链接编译进本程序集；输入全部经严格 codec 创建，不复制判定实现作期望值。
- 覆盖：null 优先与参数名；四种（取消,权限）组合在 Ready/StaleBase/NoChange/VersionLimit 输入上的优先级矩阵；旧 base 全字段（region/version/rows/columns/origin_x/origin_z/spacing/同版高度）；NoChange 含 ±0；版本上限无变化/有变化；2×N 无内部（合法候选必同值，codec 拒绝任何改动）；不同 patch_id 不影响纯判定；每例判定前后 Serialize 比对证明对象未变。
- 命令（空本地 NuGet 源、完全离线；`<empty-dir>` 为任意空目录）：
  - `dotnet restore TerrainCommitPolicy.Tests.csproj --source <empty-dir> -p:NuGetAudit=false`
  - `dotnet build TerrainCommitPolicy.Tests.csproj -c Release --no-restore`
  - `dotnet run -f net10.0 -c Release --no-build --no-restore`
- 结果（本机 SDK 10.0.401，实际运行记录）：
  - restore/build：net8.0 与 net10.0 双 TFM 离线编译均通过。
  - net10.0 实跑通过：runtime .NET 10.0.12，InvariantCulture 与 de-DE 各 37 例，共 `checks run: 74, failed: 0`，退出码 0。
  - net8.0 执行 NOT_RUN：本机仅装 net10 runtime，运行报缺少 `Microsoft.NETCore.App 8.0.0`，退出码 150；未安装工具链，不作假称。
- 全量检查在 InvariantCulture 与 de-DE 两种 CultureInfo 下各跑一遍。本工具只测纯判定，不含权威提交/世界写入/持久化（Codex 另有独立验收）。
