# work-meter-tests

level-job-r1 GLM包B `Yudian.Construction.WorkMeter` 纯计时合同自测：独立 console、零外部包、无测试框架，非 0 退出码表示有失败检查。

- 被测：`prototype/scripts/Construction/WorkMeter.cs`（sealed，单构造，仅 `ElapsedSeconds`/`Fraction`/`IsComplete`/`Advance`），源码直接链接编译进本程序集；无 Godot、无任务/权限/世界状态、无重置。
- 覆盖：构造边界（最小正/3600 上沿/0/负/超上沿/NaN/±Inf）；Advance 边界（0/3600 上沿/负/超上沿/NaN/±Inf）；eligible 累计；未完成 eligible=false 中断清零并可从零重累计；超量累计封顶恰为 required、Fraction 恒 [0,1] 且完成时恰为 1；完成后 eligible=true/false 均保持完成（不清零）；非法输入抛 ArgumentException 且状态不变、之后可继续正常推进；两实例零共享（新任务新 meter）。
- 命令（空本地 NuGet 源、完全离线；`<empty-dir>` 为任意空目录）：
  - `dotnet restore WorkMeter.Tests.csproj --source <empty-dir> -p:NuGetAudit=false`
  - `dotnet build WorkMeter.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`
  - `dotnet run -f net10.0 -c Release --no-build --no-restore`
- 结果（本机 SDK 10.0.401，实际运行记录）：
  - 离线 restore 与 no-restore 构建均通过：0 警告 0 错误。
  - net10.0 实跑通过：InvariantCulture 与 de-DE 各 18 例，共 `checks run: 36, failed: 0`，`ALL CHECKS PASSED`，退出码 0。
  - 边界注记：`Advance(0, ·)` 对 `requiredSeconds=double.Epsilon` 的 meter 是无操作（累计 0 秒）；最小正需求须以正增量完成，已在 `ctor_accepts_smallest_positive` 中按此断言。
- 全量检查在 InvariantCulture 与 de-DE 两种 CultureInfo 下各跑一遍。本工具只测纯计时合同；施工到场判定、暂停/取消与任务编排另有独立验收，不在本工具范围。
