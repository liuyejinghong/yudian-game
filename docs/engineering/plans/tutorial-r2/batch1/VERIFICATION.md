# tutorial-r2 批1 · r1 基线独立验证

2026-10-14｜本轮完成源码基线准备与必要验证。尚未实施 r2 批1；D001 未关闭。基线来源与文件清单见 [BASELINE.md](BASELINE.md)。

## 已实际通过

| 检查 | 实际结果 | 证据边界 |
|---|---|---|
| 工具版本 | Godot 4.7.2.stable.mono.official.ed1daf0bf；SDK 10.0.401；本机共享 CLR 10.0.12 | 游戏工程 TFM 仍 net8.0；没有升级或安装依赖 |
| 旧包身份 | PCK SHA256 8cf854790304c9ceaa7e0901c00fd13082df9e04c3a53de0676dea889346ca86，与原审核记录一致 | 原包仅独立测试启动，未修改 |
| 源码对应 | 旧包 source_files_sha256 覆盖的 207 项与隔离源逐项一致，0 缺失/不符 | 旧清单未覆盖的 PNG/UID 等不能由其追证；本轮另固定 raw 哈希 |
| raw 完整性 | prototype 294/294，验证工具与合同 10/10，独立哈希检查 0 不符；编译/导入后再次核对 0 不符 | 原工作区只读，复制前后源快照稳定；覆盖前本树文件已备份 |
| C# 编译 | exit0，Build succeeded，0 Warning(s)，0 Error(s) | .csproj 依赖声明未修改；首次构建执行了现有工程 restore |
| Godot 资源导入 | 第二次 exit0，日志 0 ERROR | 首次 SDK PATH 错误原件保留，见下文 |
| 原生启动 | Metal 4.0 / Forward+ / Apple M3 Pro，真实主页截图；MAIN_GROUND_READY robots=12 facilities=1 | 1280×800 逻辑窗口、物理2560×1600、屏幕倍率2；不是新r2界面验收或性能结论 |
| 运行身份 | 本树Debug程序集 SHA256 91ea0cf7f7471227ee900e9eb61a28750f9714d7e5d6c19e278a4385350e9145，MVID 64157721-4498-4839-9016-bd963083fc49，CLR10.0.12，display=macOS | 原旧导出实际CLR8.0.31；源码相同不等于重新构建二进制/PCK字节相同 |
| 导航合同 | net10.0，exit0，两个culture共92项 PASS，结尾 ALL CHECKS PASSED | 纯函数合同，不代签B002原目标恢复 |
| 生命周期合同 | exit0，TUTORIAL_BOUNDARIES PASS，TUTORIAL_WORLD_TESTS PASS processes=1，日志ERROR=0 | 真实Main无画面工程驱动，经过首套建设与接线；不是玩家键鼠体验 |
| 美术接入静态合同 | tutorial-mars-r1 landmark static verification — 46/46 PASS | 本轮未修改美术；静态检查不证明审美或性能 |
| 文档与补丁格式 | verify_documents.py PASS；git diff --check 无错误 | 文档工具的NOT_RUN提示仅表示它不运行游戏，不覆盖本表实际独立运行 |

生命周期实际覆盖：menu_pause、settings、duplicate_fields、projection_rollback、save_failure、newgame_reset、double_submit、close_confirmation、detached_dto。日志同时记录 PLAYER_SAVE_OK、PLAYER_LOAD_ROLLBACK、PLAYER_LOAD_READY；私有目录写入拒绝为测试主动制造的保存失败，正确保留原档和世界，不是系统权限故障。

## 失败、回退与未验证项

- 导入第一轮设置 DOTNET_ROOT 但未把现有 dotnet 加进 PATH，Godot编辑器报 `.NET Sdk not found` 和 Microsoft.Build 缺失。独立 dotnet build 已通过；第二轮沿现有 tools/run_godot.sh 设置本次子进程环境后 exit0，无ERROR。未安装、更换SDK，失败日志未覆盖。
- 导航第一轮没指定多目标框架，入口拒绝运行；按已安装运行时指定 `--framework net10.0` 后 exit0。游戏TFM未变。
- 生命周期第一轮外部90s时限截断在真实首套建设，未出现合同失败；核对现有driver默认与历史记录后，重新调用时传入 `--timeout 1200`，新输出目录完整运行exit0。runner源码、物理步长、TimeScale、健康和库存未改。
- 正常键鼠进入新游戏未取得证据：AX仅返回主页文字，Peekaboo定向定位超时，agent-desktop焦点未settle、鼠标返回 delivered_unverified / Physical pointer did not reach requested position。回读仍为主页，未继续盲点或切宿主、改权限。Accessibility/Screen Recording当时均granted，不据此指责用户未授权或断言游戏输入故障。
- 原旧包以 --quit-after 正常结束exit0。隔离源码GUI主页检查后由主控主动停止自有任务，进程退出；不宣称正常菜单退出通过。无画面生命周期合同的正常退出另有exit0证据。
- B002正常UI重现、根因与同目标恢复：NOT_RUN。本轮仅整理已有对照与调用链，不调坡、不放宽导航、不瞬移。
- r2批1实现、覆盖面板、完整缺料建设新GUI、无提示新玩家轨、发布包/draft PR：NOT_RUN。

## 可重复命令

从本隔离worktree根执行，工具环境只对本次进程生效。临时输出必须使用当前任务的私有目录，示例中的 `TEST_ROOT` 必须指向新建的绝对路径，不能取正式 user://saves；不得覆盖既有证据目录。最后一条 GUI 命令用于原生主页探针，未证明正常键鼠进入新游戏或完整操作链。

```bash
export YUDIAN_GODOT="$HOME/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot"
export YUDIAN_DOTNET="$HOME/.dotnet/dotnet"
export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"

"$YUDIAN_DOTNET" build prototype/Yudian.csproj -m:1 -p:UseSharedCompilation=false -nodeReuse:false -p:RestoreIgnoreFailedSources=true
tools/run_godot.sh --headless --path "$PWD/prototype" --import
"$YUDIAN_DOTNET" run --project tools/navigation-tests/Navigation.Tests.csproj --framework net10.0
python3 -I tools/tutorial-mars-tests/verify_tutorial_mars_landmark.py
python3 -I tools/tutorial-world-tests/run.py --engine "$PWD/tools/run_godot.sh" --project "$PWD/prototype" --output "$TEST_ROOT/boundaries-new" --phase boundaries --timeout 1200

YUDIAN_PLAYER_GUI_TEST=1 YUDIAN_PLAYER_GUI_SIZE=1280x800 \
YUDIAN_TUTORIAL_TEST_ROOT="$TEST_ROOT/gui-slots" \
YUDIAN_PLAYER_TEST_SAVE="$TEST_ROOT/gui-slots/d13-tutorial-v4-slot-1.json" \
tools/run_godot.sh --path "$PWD/prototype" --rendering-driver metal --accessibility always --max-fps 60 --log-file "$TEST_ROOT/gui-native.log"
```

本轮原始日志、失败日志与定向游戏截图位于任务私有证据目录，未提交玩家存档或私人路径。[实际输出摘录](evidence/contract-output.txt)保存构建和合同结果，[原生主页截图](evidence/source-home.png)仅包含本项目测试窗口；不发布整个桌面、私有槽或工具缓存。

## 下一阶段阻断

用户已授权规格、任务、实施和draft PR。to-spec 的 SKILL.md frontmatter 为 `disable-model-invocation: true`；Skill 工具拒绝调用，原文为「Ask the user to run /mattpocock-skills:to-spec themselves」及「Do not replicate this skill's workflow by other means」。直接调用与禁止复刻来自工具返回，不是产品方向待确认。基线准备独立完成；没有代为写实施规格、发布规格issue或派r2实现。解锁入口后从本工作树及这份基线继续，不重新访谈已确认方向，不重建已通过基线。
