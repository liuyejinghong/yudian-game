# D1.0 真实 Main 保存与恢复检查

在已还原现役 Godot.NET SDK/NuGet 缓存后运行；没有新增依赖。必须使用新的输出目录，保留失败轮次。

```sh
python3 tools/player-world-tests/run.py --godot /Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot --dotnet /Users/ethan/.dotnet/dotnet --output /private/tmp/yudian-player-world-new-run
```

22 步检查消费真实 Main 命令/preview/权威提交，涵盖暂停不推进、每台筑垒可达、两处选址、工作中保存恢复、异常档（落点/跳工段/范围外patch/站位）、不可写目录保有效档、完成和取消重读、加载投影故障回退。另起原生进程验证工作中退出续接、完成后和提交但未验物理的快照均不重放提交。fixed-fps 仅控制测试推进；不作性能、普通鼠标交互或真人可玩性证据。用户存档不会被这些测试覆盖；测试槽仅在显式 SELF_TEST 时启用。Windows 权限夹具未测不外推。
