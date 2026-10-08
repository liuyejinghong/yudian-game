# 原型试玩与工程验证

本机试玩：在项目协调目录双击 **余电.app**，进入 D1.1 自举建设。选择设施或整平，再点地面预览位置并确认，观察真实到场、运料与施工；可取消、暂停、保存。重开后点击「读取」继续，关窗不会自动保存。

底栏有前后左右、拉近/拉远、左转/右转与复位按钮。也可按住WASD/方向键或右键拖动平移，滚轮缩放，按住Q/E旋转，F定位，Esc清除预览；捏合/双指平移已接入，物理触控板尚未验。当前为2026-10-07本地Mac试玩；旧包保留，源码改变需重新打包。[镜头复验](../docs/engineering/reports/2026-10-07-camera-verification.md)。

源码已进入D1.2：安全首套仍从有限着陆器库存起步，首套完成后建设缺料会展开有限采矿→实运→有电加工→实运→建设。仓储成为后续建设集料/取货源；第二太阳能扩展供电范围与功率。新档用schema3新槽，旧schema2必须点「读取旧D1.1」显式迁移副本，原档保留。「补维修耗材」真实生产并运到维修站。隔离候选`试玩包/D1.2 2026-10-08 r3/Yudian.app`原生25进程通过，实际鼠标功能链LIMITED PASS，完整HUD/普通入口仍返修，默认包继续为D1.1 r5。

本机默认试玩包为D1.1：选择太阳能、充电桩、维修站、加工设施或仓储，点合法位置后确认建设，驮运实运材料、筑垒整平施工；「连接电缆」实运并施工后供电。观察日照、电量和耐久；充电不修机械、维修不充电，耗材和套件有限。此旧默认包的加工设施只登记能力。

当前包位置 `试玩包/D1.1 2026-10-07 r5/Yudian.app`；镜头按钮/滚动、暂停、真实鼠标保存关闭重开及读档后镜头通过，根「余电.app」已切换；r3入口另保为「余电-D1.1-r3-2026-10-07.app」。D1.1规则沿用原已验实现，历史九进程属于r3，不冒充r5重跑。旧D1.0入口「余电-D1.0-2026-10-07.app」及schema1档保留；D1.1用schema2槽，关窗不自动保存。[D1.1证据与范围](../docs/engineering/reports/2026-10-07-d11-bootstrap-verification.md)。

工程构建说明如下。

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

如果Godot报`Failed to load .NET runtime / hostfxr`，已装在用户目录的SDK可能未进入桌面启动环境。本机可双击协调目录的「打开 Godot.command」；工程命令可用`tools/run_godot.sh <Godot参数>`，它只给本次进程设置已有SDK目录/PATH，不安装SDK或改系统全局环境。`YUDIAN_GODOT`和`YUDIAN_DOTNET`可覆盖路径。

```bash
"$YUDIAN_DOTNET" run --project tools/qa02-tests/Qa02.Tests.csproj -- "$PWD/prototype/fixtures/s_small.json"
"$YUDIAN_DOTNET" run --project tools/benchmark-tests/Benchmark.Tests.csproj
```

构建到从未使用的输出目录（原始模板SHA校验、复制出本地arm64模板、导出与ad-hoc签名；不修改全局模板）：

```bash
python3 tools/build_macos.py --godot "$YUDIAN_GODOT" --dotnet "$YUDIAN_DOTNET" \
  --templates "$YUDIAN_TEMPLATES" --output prototype/export/local-r1 --playable
```

`--playable`增加原生Finder启动入口，双击即整平模式；不传该选项保留旧导出行为，包内原始`Contents/MacOS/Yudian`仍供工程命令行使用。

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

主场景真实地形与物理巡逻（2026-10-05限定接受）：先在仓库根用已有SDK编译Debug，再启动明确的新模式。

```bash
"$YUDIAN_DOTNET" build prototype/Yudian.csproj --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false
"$YUDIAN_GODOT" --path prototype -- --live-terrain
```

土坡整平/矿点挖低检查受影响cell占用，投影成功后等待下一物理帧验证再继续巡逻；Current恢复不增加版本。新模式不能与benchmark、duration或环境benchmark组合，并独立拒绝超出r1与场内布局范围的fixture；旧模式输入范围不收窄。完整导航、彼此/设施避让、实际建设工序/电耗/保存与正式美术未接入。实际10项原生运动/七步Main/Metal GUI与运行身份见[复验](../docs/engineering/reports/2026-10-05-main-ground-verification.md)，可复跑入口见[Main检查](../tools/main-ground-tests/README.md)。
