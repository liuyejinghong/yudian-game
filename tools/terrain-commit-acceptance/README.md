# 单区域内存提交独立验收

由主控维护，对照 terrain-commit-r1 使用严格 codec 创建输入，检验真实权威状态与回执。GLM纯判定自测另在 `tools/terrain-commit-policy-tests/`。

离线编译与执行（本机已有SDK及framework reference packs）：

```sh
mkdir -p /tmp/yudian-empty-nuget-source
dotnet restore tools/terrain-commit-acceptance/Independent.csproj --source /tmp/yudian-empty-nuget-source
dotnet build tools/terrain-commit-acceptance/Independent.csproj --no-restore -f net8.0
dotnet build tools/terrain-commit-acceptance/Independent.csproj --no-restore -f net10.0
dotnet tools/terrain-commit-acceptance/bin/Debug/net10.0/Independent.dll
```

实际命令、运行时与结果见本轮工程复验报告；缺少net8 runtime时只签net8编译。测试不连接引擎、导航、资源结算或保存；单写者顺序验证不能被当作多线程测试。
