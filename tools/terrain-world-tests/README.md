# 单区域权威提交场景

被测`TerrainRegionView`复用内存提交、Stage生成器和native资源束。独立场景是实际权威提交入口，旧Main的机器人、导航、存档和正式美术尚未接入。工程自行启动与验证，无需美术回执。

在工作树根目录，使用已有工具与空本地NuGet源：

```sh
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" restore prototype/Yudian.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" build prototype/Yudian.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
DOTNET_ROOT="$HOME/.dotnet" "$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot" --headless --path prototype res://scenes/terrain-world/TerrainWorldDemo.tscn -- --terrain-world-self-test
```

去掉`--headless`和自测参数可交互运行：平整、挖低、重放、取消、提交旧版、Current投影恢复。操作只在物理写帧执行；中心高程和权威/绑定/物理验证版本分别显示。材质为明确标注的工程调试材质。

自测退出0且`TERRAIN_WORLD_SELF_TEST PASS`才通过：真实9→10→11、历史回执重放不回退、拒绝与准备失败保留状态、准备后取消/权限撤销/版本变化重验、绑定中途合成异常保留已提交13、下一帧旧碰撞仍有效、Current恢复仍13、真实后续物理帧射线/全mesh-shape角同源、替换/退出释放。合成绑定异常与GLM资源束中途异常不表示真实native资源耗尽；无跨线程/原生全局原子事务承诺。启动核对加载程序集MVID与当前Debug文件MVID，打印构建文件SHA，未声称加载字节SHA。
