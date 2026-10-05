# Main真实地形与物理巡逻检查

默认场景保持旧灰模负载；`--live-terrain`显式进入权威地形/CharacterBody模式，禁止与任何游戏benchmark触发方式组合。手动整平/挖低不代表建设工序或寻路完成。

在仓库根目录，使用已有离线SDK/Godot（不安装依赖）：

```sh
export DOTNET_ROOT="$HOME/.dotnet"
"$DOTNET_ROOT/dotnet" restore prototype/Yudian.csproj --source /private/tmp/yudian-empty-nuget -p:NuGetAudit=false
"$DOTNET_ROOT/dotnet" build prototype/Yudian.csproj --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
YUDIAN_LIVE_SELF_TEST=1 /Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot --headless --path prototype -- --live-terrain
```

`YUDIAN_LIVE_SELF_TEST=1`仅在live模式有效，使用默认s_small。真实Main检查：12机贴地移动；相邻cell/胶囊边缘占用；设施/机器人拒绝不换资源且恢复旧巡逻；土坡整平/矿点挖低后续帧射线；绑定与再次恢复失败保持暂停/原因；Current恢复不增加版本，随后真实移动。900物理帧超时或任一断言失败非0退出；成功包含`MAIN_GROUND_SELF_TEST PASS`，且无引擎ERROR。

前台试玩去掉`--headless`和自测环境变量，检查三类灰模实际移动、土坡/矿点按钮、版本与占用拒绝，真实关闭窗口。`YUDIAN_CAPTURE_PNG=/绝对路径.png`沿用已有viewport截图入口；headless不作为画面/性能证据。

独立坡面/墙/暂停检查见[GroundPatrol](../ground-patrol-tests/README.md)。本票不接受机器人相互/设施避让、完整导航、经济/电耗、保存或正式美术。
