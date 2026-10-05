# T3-GODOT · ArrayMesh适配子件

状态与本轮证据见[产研TODO](todo.md)的T3-GODOT行；主控冻结[合同r1](../contracts/terrain-render-r1.md)，GLM执行，主控独立验收。仅做TerrainMeshAdapter.Create、实际headless测试和简短README。父票T3b/T3c仍未通过完整阶段/高度场比较。

唯一可新增目录：prototype/scripts/TerrainRendering/（适配器+Tests引擎脚本）、prototype/scenes/terrain-render-tests/（AdapterTests.tscn）、tools/terrain-render-tests/（README/少量测试支持文件）。禁改所有既有文件、共享CPU类型、Main/项目/依赖、主控probe、材质/美术、公共合同/TODO/PLAN。新脚本.uid如引擎生成可以保留；不得提交.godot/bin/obj/临时日志。

实际入口由派单明确：`$HOME/.dotnet/dotnet`与`$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot`，PROJECT_ROOT是协调项目目录不是工作树；派单给绝对路径。空本地NuGet源restore当前prototype/Yudian.csproj（NuGetAudit=false），Debug单节点禁共享编译build，显式DOTNET_ROOT=$HOME/.dotnet，实际Godot headless启动测试场景退出0/非0；打印CLR、程序集位置/SHA，匹配刚构建的当前工作树Debug/Yudian.dll。缓存缺失报告，不下载/安装。无需另建net10console测试，native API须在Godot进程中执行。

完成条件：限定目录实现与命名用例通过、清楚失败日志/缺项、干净限定commit；五行报告范围/命令与检查数量/限制/可见用量/commit。不push/merge、自写ACCEPTED、操作GUI或领取下一票。合同已由所有者授权继续产研并由主控冻结，直接实现，不再请求开工确认。两轮同接口失败停下给出诊断，不更改合同绕过问题。

实际派单：base702e66b；session sess_2cb78af554；模型确认task_f212cbe147原生GLM-5.3-Flash，observed_model未知；执行task_35d0705185→一次限定返工task_75e6ea5407；原提交ac457be/修复80b84d0，执行会话已结束。运行身份与原生自动Tangent澄清由主控写入合同，[复验](../reports/2026-10-04-terrain-render-verification.md)与[#24](https://github.com/liuyejinghong/yudian-game/issues/24)记录受限接受依据。
