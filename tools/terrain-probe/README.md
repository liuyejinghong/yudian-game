# 候选显示工程探针

独立场景 `res://scenes/terrain-probe/TerrainProbe.tscn`，不改变 Main。

使用现有 .NET SDK / Godot 4.7.2，先离线 restore 和 Debug build：

```sh
mkdir -p /private/tmp/yudian-empty-nuget
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" restore prototype/Yudian.csproj --source /private/tmp/yudian-empty-nuget -p:NuGetAudit=false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" /path/to/Godot --headless --path prototype res://scenes/terrain-probe/TerrainProbe.tscn -- --terrain-probe-self-test
DOTNET_ROOT="$HOME/.dotnet" /path/to/Godot --path prototype res://scenes/terrain-probe/TerrainProbe.tscn
```

按钮或键1/2/3切基准/整平/开挖；键4/5注入真实过期/非法外围拒绝；Esc退出。撤销只撤销未提交的预览。没有文本输入。

图形截图：添加 `-- --terrain-capture-dir <新的绝对目录>` 自动保存同镜头 base/level/dig PNG 后退出。额外 `--terrain-cull-check` 保存无UI的 top/bottom 对照，材质开启back-cull。捕获目录必须尚不存在，避免覆盖证据。headless只作逻辑检验，不能代替图形检查。

打印当前Debug程序集路径/SHA256、CLR、显示后端、显卡；每次替换打印CPU节点数、展开顶点、三角数及CPU生成+资源提交耗时。耗时不含GPU完成，不是稳定性能结论。完整重建增加展开缓冲成本，未实现增量、碰撞、导航、保存与接缝焊接。

## 可选静态碰撞与M01材质

加`--terrain-collision`启用两个StaticBody（layer1/2、mask0）；输入只排最新请求，在物理回合全准备显示/碰撞四资源再换，下一物理查询帧才记录`PROBE_PHYSICS_SYNCED`。重复/撤销/非法最新请求不会留下旧pending。它仍是未提交候选；完整重建及adapter额外临时ArrayMesh，不代表动态角色、导航、持久提交或性能优化。

独立headless验证用`--terrain-collision-self-test`，不与`--terrain-probe-self-test`混用；用原README的Debug构建和实际引擎命令，启动Scene保持TerrainProbe.tscn。检查会跨物理帧运行，exit0且自身PASS才算成功。默认无碰撞路径保持旧渲染自测。

加`--terrain-material res://assets/materials/art-r1/soil_mars.tres`加载美术组技术接受的M01，两个节点只绑定surface0，MaterialOverride为空。缺资源、错类型、透明/双面材质拒绝；不回退代用品。可同时用碰撞/截图，但不能与cull-check混用。材质技术/绑定成功不代表所有者最终视觉接受，正式U01/T01/T02仍缺。
