# 主控独立数据验收

依据terrain-patch-r1冻结接口和两个非对称金样，在GLM实现提交前由主控准备；与工人测试独立。只测输入/值保持/拒绝/只读接缝，不启动Godot、读写世界或证明游戏保存。

使用现有SDK，向空本地目录restore（NuGetAudit=false），不下载新包。`dotnet build tools/terrain-data-acceptance/Independent.csproj -c Release --no-restore`分别编译net8/net10；`dotnet run --project tools/terrain-data-acceptance/Independent.csproj -c Release -f net10.0 --no-build --no-restore`实际执行。具体命令/输出在工程证据目录记录；未运行不标通过。
