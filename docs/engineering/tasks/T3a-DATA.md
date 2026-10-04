# T3a-DATA · 冻结高度数据接缝

批次terrain-data-r1；主控合同/验收Codex，GLM-Eng-A执行。基线合并main `707258f12e400508c880166cd10e608f137d16d1`；输入为主控已提交的[合同r1](../contracts/terrain-patch-r1.md)。派单前记录实际合同提交、工作树/分支和Bridge session/task。目标只有数据，不做完整地形/游戏保存。

只可新增`prototype/scripts/TerrainData/*.cs`及`tools/terrain-data-tests/`（csproj、C#测试、少量测试fixture、README），不改任何既有文件。代码消费net8标准库；测试用现有.NET环境、无包依赖，独立console返回0/非0，链接该目录C#，不启动Godot。测试csproj设TargetFrameworks为net8.0;net10.0：两者编译、只在已有net10运行时运行；编译兼容与执行结果分开记录。测试必须按冻结接口调用并覆盖金样/每类异常/输入保持，不只测“解析未报错”。文件可按类型拆为三件，不建框架/泛型校验器/插件。

主控提供实际.NET入口`$HOME/.dotnet/dotnet`（派单给绝对路径）；SDK10.0.401、独立runtime10.0.12，net8仅做编译（Godot导出包中的net8不能冒充独立dotnet运行时）。net8引用包8.0.31已在本机NuGet缓存。先在工作树外建空本地包源，执行`"$HOME/.dotnet/dotnet" restore tools/terrain-data-tests/TerrainData.Tests.csproj --source <空本地目录> -p:NuGetAudit=false`；再`"$HOME/.dotnet/dotnet" build tools/terrain-data-tests/TerrainData.Tests.csproj -c Release --no-restore`；最后`"$HOME/.dotnet/dotnet" run --project tools/terrain-data-tests/TerrainData.Tests.csproj -c Release -f net10.0 --no-build --no-restore`。不得联网取新依赖/安装；本机缓存缺失则报告，不自行下载。

禁改Main/fixture/桥接/项目/依赖/公共合同/TODO/旧证据；不增加Apply/Commit权威层、库存/矿物/任务状态机、碰撞/导航/保存文件写入、GUI或网络。源码无个人绝对路径；临时输出与bin/obj不提交。源文件归属唯一，其他工人不得写这个目录。

验收由主控：查看真实diff、自己运行新测试；按两个金样和异常输入独立复核，net8编译兼容；必要时独立reviewer审数据/持久化接缝。工人不得写ACCEPTED、不push/merge、不领取下一票。五行报告：实现范围、命令/真实结果、缺项、可见用量、commit。失败保留日志，同接口两轮失败先停并缩票，不静默改变合同。
