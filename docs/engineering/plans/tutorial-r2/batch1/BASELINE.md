# tutorial-r2 批1 · 完整教学 r1 基线（机械导入记录）

2026-10-14 由基线导入 worker 从原工作区只读复制。以下为导入时记录；主控随后已独立通过编译、资源导入、原生主页、导航和生命周期工程合同，实际结果见 [VERIFICATION.md](VERIFICATION.md)。本文件记录来源身份、delta 与指纹，r2实现与玩家试玩验收仍未执行。

## 来源身份

- 源：原共享 checkout `prototype/`（git_head `953573f`，working_tree_dirty=true，教学源码未提交）。本导入用文件哈希固定，不依赖 git。
- 源 `build-info.json` 与候选包 `试玩包/教学 MVP 2026-10-12 候选 r1/build-manifest.json` 的 `source_files_sha256`（各 207 项）完全相同；逐项与当前源文件哈希比对：0 不符、0 缺失。
- 聚合指纹精确复现（按 `tools/build_macos.py` 规则重算）：`source_snapshot_sha256=b76c2fa2dd46553280a7b9bf83b39274e0efb2bdc6339ee5ca8d5d5ebae8bedc`、`assets_snapshot_sha256=0174c5ddcfe1251bb9dbbefcf42372765c0822547e5215d42f857f327e87a98d`，与两份清单记录一致。这证明清单覆盖的 207 项与旧包构建记录一致；未入旧清单的 PNG、UID 等 87 项本轮单独固定 raw 哈希，不能据旧清单追证它们的旧包来源，也不宣称本次重新构建会复现旧 PCK 字节。
- 候选包 PCK 实测 SHA256 `8cf854790304c9ceaa7e0901c00fd13082df9e04c3a53de0676dea889346ca86`，与 manifest `pck_sha256` 及主控预期一致。
- 工具链身份（记录自清单，未在本树实测）：Godot `4.7.2.stable.mono.official.ed1daf0bf`，.NET SDK `10.0.401`（根 `global.json`），TFM `net8.0`，`build_script_sha256=d7750413fed58a4c6de93d3aa37e974fa142ad689a3e78aa524babf1d85a102f`。

## 导入内容与 delta

- 导入范围：`scripts/ scenes/ config/ assets/ fixtures/` 全部，加 `project.godot、Yudian.csproj、Yudian.sln、.gitignore、export_presets.cfg、README.md`。排除任意路径分量 `.godot/ export/ benchmarks/ bin/ obj/`；`build-info.json` 只读作来源证据，未复制。源内无 symlink；除 `.gitignore` 外无隐藏文件（export 内 .DS_Store 属排除范围）。共 294 文件。
- 本树导入前 prototype 有 178 文件，无独有文件被删除或覆盖丢失：
  - 162 文件与源已字节一致；
  - 16 文件被源版本覆盖（教学 r1 未提交改动，均已备份）：`scenes/Main.tscn`、`scripts/Main.cs`、`Main.Bootstrap.cs`、`Main.BootstrapSave.cs`、`Main.Development.cs`、`Main.DevelopmentSave.cs`、`Main.DevelopmentTests.cs`、`Main.HaulerVisuals.cs`、`Main.LiveTerrain.cs`、`Main.Player.cs`、`Main.PlayerSave.cs`、`Main.PlayerVisuals.cs`、`scripts/Navigation/BoundedRoute.cs`、`scripts/PlayerContracts.cs`、`scripts/PlayerUI/PlayerController.cs`、`scripts/PlayerUI/PlayerUiSelfTest.cs`；
  - 新增 116 文件：69 `.uid`、29 `.import`、8 `.cs`、3 `.png`、2 `.glb`、2 `.gd`、2 `.tscn`、1 `.tres`（新目录 `assets/environment/tutorial-mars-r1/`、`assets/units/tuoyun-hf-mvp-r1/` 及配套场景/测试）。
- 备份：覆盖前 178 文件全部备份于主控任务 tmp `jobs/78a0dc70/tmp/baseline-import-r1-20261014/backup/`（含备份哈希校验），不入库。
- 新增资产类别与出处已核对：`mars-outcrop-hifi-r2.*`（GLB+PNG 贴图）见 `docs/art/production/mars-hifi-r2/`，`tuoyun-hf-mvp-r1` 见 `docs/art/production/tuoyun-hf-mvp-r1/`（含 receipt、binary-identity），`tutorial-mars-r1` 有 consumer-receipt。均为项目内制作/回执资产；本记录不宣称任何第三方可公开性，公开性判断归主控。

## 完整性与指纹

- 源全树快照（1338 文件，含 .godot/export/benchmarks）复制前后 SHA256 全等：源期间未变化。
- 目标 294 文件逐文件 SHA256 与源一致，0 不符；无计划外非忽略文件。主控验证产生的 .godot 与测试 bin/obj 缓存被忽略，未纳入基线清单或暂存区。
- 本基线清单：`baseline-files.json`（294 项，仓库相对路径 `prototype/...`→sha256），聚合指纹 `aggregate_sha256=dd0181b1ff4a3f6cdd2961ed0e3bb25bccaa90d4f7420733fb62ae05273746a2`。聚合规则为对路径→sha256字典调用 `json.dumps(files, sort_keys=True)`，将其 UTF-8 字节取 SHA256；不含清单其他元字段。
- 全 assets 独立哈希（121 文件，不受清单后缀白名单限制，含 PNG/UID）：聚合 `ce318aa28855fc41731294b981be105c568352f3fc2550b00e79259bcf30c7d0`。
- 清单覆盖差（仅记录，不修）：源 294 文件中 87 项不在 207 项清单内 = 79 `.uid` + 3 `.md`（README、assets/materials/art-r1、scenes/art_preview_r1）+ 3 `.png` + `.gitignore` + `.sln`，与 build 脚本后缀白名单一致，非内容差异。

## 导入时的构建 / 启动提示（历史记录）

以下保留 worker 导入时的提示，当时未执行。主控随后已验证构建、导入与原生主页，准确且使用私有槽的命令见 [VERIFICATION.md](VERIFICATION.md)。下面的普通启动命令未隔离正式槽，不用于本任务验证。工具路径来自已有安装：

```bash
export YUDIAN_GODOT="$HOME/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot"
export YUDIAN_DOTNET="$HOME/.dotnet/dotnet"
export DOTNET_ROOT="$(dirname "$YUDIAN_DOTNET")"; export PATH="$DOTNET_ROOT:$PATH"

"$YUDIAN_DOTNET" build prototype/Yudian.csproj --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false   # 首次可能需先 restore
"$YUDIAN_GODOT" --path prototype                                             # 原生启动（Main 场景）
tools/run_godot.sh --path prototype                                          # 需桌面 SDK 环境时的入口
```

版本组合须与来源身份一致（Godot 4.7.2 mono / SDK 10.0.401 / net8.0）；导出采用既有 `python3 tools/build_macos.py ...`（输出换新目录，不覆盖旧包）。导入时构建、启动和截图尚未执行；后续结果在 VERIFICATION.md 单独记录，r2试玩验收仍 NOT_RUN。

## 补充导入（2026-10-14 第二批：验证入口与来源合同）

主控确认基线独立编译成功、旧包清单 207 项独立 0 差后，指派补导入同份 r1 的必要验证入口与来源合同。同源（原共享 checkout，git_head `953573f` + 未提交工作区），仍为机械快照，未构建未自测。

- 复制 10 文件：`tools/tutorial-world-tests/run.py`、`tools/tutorial-mars-tests/verify_tutorial_mars_landmark.py`、`tools/navigation-tests/{Navigation.Tests.csproj,NavigationContractTests.cs,Program.cs,README.md}`、`tools/player-ui-tests/{README.md,run-camera-diag.sh,run-selftest.sh}`、`docs/engineering/contracts/tutorial-mvp-r1.md`。均为 raw 源码/脚本/文档；`bin/ obj/ __pycache__`、日志、证据、应用、存档、符号链接、机密均未复制（范围内亦无 symlink/密钥模式命中）。
- delta：目标既有 7 文件被覆盖（4 文件内容实变：`navigation-tests/NavigationContractTests.cs`、`player-ui-tests/{README.md,run-camera-diag.sh,run-selftest.sh}`；另 3 已同字节），新增 3 路径（两个 tutorial 测试目录、tutorial-mvp-r1.md 合同）。prototype 本批未触碰。
- 备份：覆盖前 7 文件备份于主控任务 tmp `jobs/78a0dc70/tmp/baseline-import-r1-tools-20261014/backup/`（含备份哈希校验），不入库。
- 完整性：源 copy-set 10 文件复制前后 SHA256 全等（范围内目录含 bin/obj 全量 52 文件 0 漂移）；目标 10 文件逐项与源字节一致。清单：`additional-baseline-files.json`（10 项，`aggregate_sha256=394f1c3556c75fbcb637848fde174496248f6751bac66fd570a52eaefb1c130a`）。
- 依赖提示（仅记录）：`verify_tutorial_mars_landmark.py` 引用 `art/source/environment/mars-outcrop-hifi-r2/` 与 `docs/art/production/mars-hifi-r2/evidence/`，前者在本树已存在；`navigation-tests` csproj 链接 `prototype/scripts/{TerrainData,Navigation}`（第一批已导入）。运行仍属主控职责，本批 NOT_RUN。

## 主控待办（本 worker 未做）

- 主控随后已完成实际版本、构建、导入、原生主页与合同检查，见 VERIFICATION.md；普通键鼠进入新游戏未验证，不读取/改写正式存档。
- 原工作区 tools 中仍未随导入复制的候选：`tools/hauler-hf-tests/`（capture/reopen/identity/state 合同）。第二批范围外的其余 tools 差异以第一批记录为准；是否指派复制由主控决定。
- 新增资产（mars-hifi-r2、tuoyun-hf-mvp-r1）公开性/版权口径：主控已确认按项目原创/源复制回执核实，无需重复调查。
