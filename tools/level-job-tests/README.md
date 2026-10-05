# 单机器人整平任务检查

仓库根目录，以已有离线SDK和Godot编译运行。PROJECT_ROOT为含tools-bin的协调目录，不安装依赖。

```sh
export DOTNET_ROOT="$HOME/.dotnet"
"$DOTNET_ROOT/dotnet" restore prototype/Yudian.csproj --source /private/tmp/yudian-empty-nuget -p:NuGetAudit=false
"$DOTNET_ROOT/dotnet" build prototype/Yudian.csproj --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
YUDIAN_LEVEL_SELF_TEST=1 "$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot" --headless --path prototype -- --live-terrain
```

自测用真正Main、默认s_small，不能与YUDIAN_LIVE_SELF_TEST同时开启。7200物理帧超时，断言失败退出非0。15步覆盖实际派工到场、赶路不计时、暂停不计时、施工取消/重复、未到场超时阈值、权限与过期、另一机器人实际进入施工区导致占用等待、释放后提交、绑定故障保留权威、稳定request重放、Current恢复后下一帧才完成、NoChange、释放回巡逻、提交后取消保地形。

超时阈值例仅注入任务计时60秒，机器人目标改为另一点但不传送，验证失败分支；不声称实跑60秒阻挡。占用者实际走到土坡，成功任务工人实际到施工站并连续作业3物理秒。测试重造坡面仅为后续取消例准备；没有资源/电量产出。

旧接缝回归沿用 [Main地形检查](../main-ground-tests/README.md)。单目标独立原生与纯计时检查见 [GroundOrder](../ground-order-tests/README.md) / [WorkMeter](../work-meter-tests/README.md)。

GUI去掉headless和自测变量，显式--live-terrain：下达整平→观察筑垒到场/进度/地形版本与完成→再下单明确无需改造；施工中取消保地形；真实关窗。工程“直接整平”与“矿点挖低”在活动任务期间拒绝，避免改写任务base。Current恢复可随时请求，作业条件失效时未完成连续计时清零。仍未接完整导航、经济/能源、设施建设、生产存档或正式模型。
