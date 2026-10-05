# ART-PREVIEW-01 · 独立预览工具（A+B）

日期：2026-10-04。执行：GLM 工具槽（B，继承已接受 A）。合同：`docs/art/production/requirements/preview-r1.md`（冻结）。
本目录含 A（加载/材质试板/自测）与 B（真实窗口渲染、GUI、evidence/blind 采集与 JSON 记录）。
Godot 4.7.2 .NET 实测（`--version`: `4.7.2.stable.mono.official.ed1daf0bf`），实际适配器 Apple M3 Pro (Apple9)、
`rendering_method_actual=forward_plus`、`rendering_driver_actual=metal`（由真实窗口采集 JSON 回读，非启动参数冒充）。

## 文件

| 文件 | 作用 |
|---|---|
| `PreviewR1.tscn` + `preview_r1.gd` | 唯一入口；CLI 解析/共用校验 `_typed_params`、manifest 加载、材质绑定、试板、B 自测用例、交互与采集调度 |
| `preview_view_r1.gd` | 冻结视图栈：1920×1200 SubViewport（own world、MSAA4、比例 1、无 FXAA/TAA）、normal/close/board 相机、方向光、环境、`RenderingServer.directional_shadow_atlas_set_size(4096,true)`、只读回读快照 |
| `preview_overlay_r1.gd` | SubViewport 内标注层：evidence 信息块、电池/扳手/双图标/回充箭头、socket 辅助圈；blind 全隐藏；PingFang SC 系统 SystemFont |
| `preview_panel_r1.gd` | SubViewport 之外的 GUI 面板：相机/yaw/七态/phase/进度/货箱/原因/时间/labels/Reset，走同一 `_typed_params` |
| `preview_capture_r1.gd` | 采集 IO：目录守卫（文件/目录撞名均拒）、evidence 编码名与 blind 中性序号名、PNG/JSON/capture_hashes 写入、git HEAD+dirty、GLB/manifest/源/六材质/预览配置实际 SHA256 |
| `probe_sample_r1.tscn` + `sample_wrapper.gd` + `probe_model_r1.glb` + `probe_manifest_r1.json` | test-only 探针（A 已接受）；manifest 新增 `source.files` 逐项源哈希（B 采集记录逐项核对实际值） |
| `gui_probe_r1.tscn` + `gui_probe_r1.gd` | TEST-ONLY 程序化 GUI 回归：向真实控件注入信号（非人手/非 nativeOS 输入，不得冒充） |

引擎生成的 `*.import`、`*.uid`、`prototype/.godot/` 是缓存，不提交。

## 用法（本机实测命令）

```sh
GODOT=<project-root>/tools-bin/Godot.app/Contents/MacOS/Godot
export DOTNET_ROOT=<user-home>/.dotnet PATH="<user-home>/.dotnet:$PATH"
PROJ=<工作树>/prototype

# 一次性导入（新增/替换 GLB 后）
$GODOT --headless --editor --path $PROJ --quit

# 自测（221 项；成功标记 ART_PREVIEW_R1_SELFTEST_PASS，exit 0）
$GODOT --headless --path $PROJ res://scenes/art_preview_r1/PreviewR1.tscn \
  -- --self-test --manifest $PROJ/scenes/art_preview_r1/probe_manifest_r1.json

# headless 校验模式（参数/导入/绑定/姿态；无渲染输出）
$GODOT --headless --path $PROJ res://scenes/art_preview_r1/PreviewR1.tscn -- --manifest <manifest> [选项…]
$GODOT --headless --path $PROJ res://scenes/art_preview_r1/PreviewR1.tscn -- --material-board

# 交互 GUI（真实窗口，金属驱动；窗口标题含“余电 ART-PREVIEW-R1 B”便于主控 CUA）
$GODOT --path $PROJ --rendering-driver metal res://scenes/art_preview_r1/PreviewR1.tscn \
  -- [--manifest <manifest> | --material-board] [选项…]

# 采集（必须真实窗口；headless 明确拒绝 exit 1）
$GODOT --path $PROJ --rendering-driver metal res://scenes/art_preview_r1/PreviewR1.tscn -- \
  --manifest <manifest> --capture-dir <不存在的新目录> [选项…]

# 程序化 GUI 回归（TEST-ONLY，真实窗口）
$GODOT --path $PROJ --rendering-driver metal res://scenes/art_preview_r1/gui_probe_r1.tscn
```

CLI 选项（合同冻结）：`--manifest <path>`/`--material-board` 二选一、`--camera normal|close`、`--yaw 0|90|180`、
`--state <七态>`（offline 拒绝）、`--phase`、`--phase-t 0..1`、`--cargo empty|loaded`、`--reason <五值>`、
`--time 秒`、`--labels evidence|blind`、`--capture-dir <新目录>`、`--self-test`。
未知/重复/缺值/空值、非法枚举、NaN/inf、非法 reason 配对：exit 1 不回落默认。试板模式只允许默认对象态。

## B 行为

- **视图**：3D 内容全部在 1920×1200 SubViewport（独立 world）；`RenderingServer.directional_shadow_atlas_set_size(4096,true)`
  运行时请求（无 getter，JSON 记 requested 并注明）；阴影滤波无光对象 getter，JSON 记
  `rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality` 项目默认（本机 2）并注明未覆盖；
  真实 method/driver 用 `RenderingServer.get_current_rendering_method/get_current_rendering_driver_name` 回读。
- **无 capture（窗口）**：构建内容+GUI 后保持窗口打开，不自动退出；headless 无 capture 走 A 的一次性校验后退出。
- **采集**：目录必须不存在（文件/目录撞名均拒，不覆盖）；精确等待 2 个 `frame_post_draw` 后取 SubViewport 原图；
  姿态由请求时刻绝对寻址（无“等第 120 帧”）；PNG+同名 JSON+capture_hashes.json 三件；写失败 exit 1 无 PASS。
  `--rendering-driver metal` 下 16 例实测 PNG 全部 1920×1200、15 个唯一哈希、同参数重跑字节级同哈希。
- **JSON 记录**：schema/asset/revision、请求与实际 applied mode/NA reason、实际 viewport/相机/光/环境回读、
  git HEAD+dirty、GLB/manifest/`source.files` 逐项/六材质/七个预览配置文件的实际 SHA256（manifest 自身哈希只在采集侧）。
- **blind**：无信息块/无原因图标/无 socket 辅助；文件名中性序号 `blind_0001.png`，答案只在 JSON。
- **GUI**：面板在 SubViewport 之外（不进采集图）；所有变更走 CLI 同一 `_typed_params`；空/非法输入保持上一合法姿态
  并回退控件+报错；重复选择幂等；Reset 恢复默认 preset+镜头+yaw（slider 用 `set_value_no_signal` 防重入）；
  关闭重开回到默认（每个进程独立，程序化用例 `gui_initial_defaults` 验证）。程序化输入=信号注入，不冒充人眼/nativeOS。
- **A 行为保留**：149 项原自测名称保留（含 CLI 负例、试板参数/共享资源、manifest 校验、wrapper 契约及4项活动包络回归）；完整672姿态扫描是主控外部独立检查，并非该149项自测的一部分；A 的 `--capture-dir` 占位拒绝例按主控允许换成 B 语义（显式空串仍拒绝）。

## 实测结果（2026-10-04，本机 M3 Pro）

| 检查 | 命令形态 | 结果 |
|---|---|---|
| check-only 全部 7 个 .gd | `--check-only --script`（python 25s 硬超时，读 stderr） | 7/7 OK，0 SCRIPT ERROR（注意：Godot check-only 出错时 exit 仍 0，以日志为准） |
| 自测 | headless `--self-test` | `ART_PREVIEW_R1_SELFTEST_PASS 221/221`，exit 0，0 SCRIPT ERROR |
| A 回归 | 基线 149 PASS 名单 diff | 移除 0 项 |
| GUI 程序化（真实窗口 metal） | `gui_probe_r1.tscn` | `ART_PREVIEW_R1_GUIPROBE_PASS 22/22`，exit 0，0 SCRIPT ERROR |
| 采集矩阵（真实窗口 metal） | 16 例：board e/b、probe normal/close×yaw0/90/180、七态抽样、三故障原因图标、回充箭头、blind、deploying .5、move t=.25/1、work t=.5、loaded | 全部 `CAPTURE_OK` exit 0；PNG 全 1920×1200；15 唯一哈希 |
| 确定性 | 同参数重跑 | PNG sha256 字节级相同（`54ad6782…`） |
| 负例（真实命令） | headless capture 拒绝/目录撞名/文件撞名/只读父目录/offline/未知键/NaN/重复键/空 manifest/显式空 capture-dir | 10/10 exit 1、明确报错、不覆盖旧内容、无 SCRIPT ERROR |
| headless 常规模式 | sample 默认/全参、board | `SAMPLE_OK`×2、`BOARD_OK`，exit 0 |

证据目录：`<project-root>/art-preview-evidence/preview-b-worker-20261004/`
（`run1-gui-probe/`、`run1-capture-matrix/`（16 子目录+日志+`matrix-summary.json`）、
`run2-negatives/`（10 日志+`negatives-summary.json`）、`run3-headless-modes/`）。
采集 JSON 的 `git.head=b3cd9340d315…`、`dirty=true`（提交前工作树，诚实记录）。

## NOT_RUN（B 不签）

- 所有者本人交互（主控CUA原生输入已补验，见docs/art/production/evidence/production-r1-receipts/preview-b-accepted/native-gui-review.md）
- 所有者视觉接受、盲比看图、可读性判断（主控职责）
- 性能/FPS 预算（本工具不输出性能结论）
- 正式样件（U01/F01/F02）与三件组合场景采集（下游票）
- 黄昏/天气/最终游戏相机（合同外）

probe 仍是 test-only；672 姿态包络与 A 补验结论不变；正式资产地面接触不因探针测试性下沉放宽。

主控补验：source.files存在时验证数组/对象/path与64位sha256格式，损坏元素[0]拒绝exit1且不写证据；可省略该可选字段。新增7项，自测228/228。主控另跑原8合同与672姿态、Metal采集及CUA原生输入；工具接受，probe非资产。
