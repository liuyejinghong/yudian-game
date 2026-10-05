# 灰模探针运行与验证

候选组合：Godot 4.7.2 .NET，SDK 10.0.401（根 global.json），游戏项目 TFM net8.0。没有把 SDK10 当作运行目标迁移；导出 runtimeconfig 与实际 CLR 各自记录。

设置本机现有工具路径（不自动安装/升级）：

```bash
export YUDIAN_GODOT="$HOME/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot"
export YUDIAN_DOTNET="$HOME/.dotnet/dotnet"
export YUDIAN_TEMPLATES="$HOME/Downloads/godot-templates/Godot_v4.7.2-stable_mono_export_templates.tpz"
export DOTNET_ROOT="$(dirname "$YUDIAN_DOTNET")"
export PATH="$DOTNET_ROOT:$PATH"
```

以下从仓库根执行。输入/统计无需启动三维：

```bash
"$YUDIAN_DOTNET" run --project tools/qa02-tests/Qa02.Tests.csproj -- "$PWD/prototype/fixtures/s_small.json"
"$YUDIAN_DOTNET" run --project tools/benchmark-tests/Benchmark.Tests.csproj
```

构建到从未使用的输出目录（原始模板SHA校验、复制出本地arm64模板、导出与ad-hoc签名；不修改全局模板）：

```bash
python3 tools/build_macos.py --godot "$YUDIAN_GODOT" --dotnet "$YUDIAN_DOTNET" \
  --templates "$YUDIAN_TEMPLATES" --output prototype/export/local-r1
```

已有同名.app时失败并保留；再次构建换新目录。SDK与Godot须精确匹配；仍需本机原生Xcode命令工具、已缓存/可恢复的.NET包。没有公证或发行。

合法S档/异常矩阵与Metal独立档（45秒×3，不包含AI/导航/保存/动态地形）：

```bash
python3 tools/verify_probe.py --app prototype/export/local-r1/Yudian.app --output /private/tmp/yudian-input-r1
python3 tools/measure_baseline.py --app prototype/export/local-r1/Yudian.app --output /private/tmp/yudian-metal-r1
python3 tools/measure_baseline.py --recompute /private/tmp/yudian-metal-r1/run-1/frames.csv
```

直接启动新包：

```bash
prototype/export/local-r1/Yudian.app/Contents/MacOS/Yudian --rendering-driver metal -- --benchmark --duration 45
```

默认输出为 `user://benchmarks/<UTC+随机run-id>/`，不写应用包；fixture旧输出字段只提供文件名。显式 `--frames-csv`/`--summary-json` 接受绝对路径、当前工作目录相对路径或user://；拒绝res://、相同文件、覆盖旧文件。fixture输入可为res://相对资源、资源URI或外部绝对路径，导出资源使用Godot.FileAccess读取。

用户选项放在`--`之后：--benchmark、--yudian-benchmark、--duration、--fixture、--frames-csv、--summary-json。裸--benchmark是Godot引擎计时参数；兼容旧的裸--duration/--yudian-benchmark但新调用使用分隔符。用户选项未知、重复、缺值即错误。fixture所有已知字段必需，未知/重复/null拒绝；三类数量合同robots_total=3*robots_per_type。浮点语法合法后，主控另检查实际地形/路线/相机派生量，防止极小正数下溢/溢出。

完成status=completed且exit0；用户关窗/引擎提前退出status=interrupted且exit130；输入/IO失败exit1且无completed summary。硬杀/崩溃可只留下部分CSV，不声称完整运行；测量器超时会终止本次进程组。CSV采用单调时钟，汇总最近秩包含全部帧与启动尖刺。GPU/内存采集另附原文；headless永远不计图形性能，内部3D尺寸当前明确标estimated。

独立ART-I00校准（不是正式驮运美术）：

```bash
"$YUDIAN_GODOT" --path prototype res://scenes/asset_preview/ArtI00.tscn
"$YUDIAN_GODOT" --headless --path prototype res://scenes/asset_preview/ArtI00.tscn -- --art-self-test
```

按1–7选预览状态；仅work活动件旋转，不改变EntityRoot或世界事实。可编辑源、真实GLB、连接点和导入约定见[ART-I00](../docs/art/production/art-i00-interface.md)。默认Main仍为旧灰模几何与镜头。`YUDIAN_CAPTURE_PNG`仅用于独立画面取证，截图的GPU读回会影响采样，不在正式性能运行中设置。
