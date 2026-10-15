# R2 Main authority 合同运行器

`run.py` 只启动现有 `Main.ManagementTests.cs` 的 R2 无画面工程合同。先由主控单独完成 Godot .NET 项目构建与资源导入；运行器不 build、import、export，不启动 GUI，不改模拟步长、TimeScale、库存、健康或路线。它不能代签原生操作、布局、玩家体验或 D001。

在 macOS / POSIX 环境用 Python 3.9+ 标准库执行，无新增依赖。`--engine` 指向现有 Godot .NET 可执行文件，`--project` 指向已经构建、导入的 `prototype`，`--output` 必须是明确的绝对私有路径且从未存在。三个路径都不接受相对路径或 `user://` / `res://`；输出解析符号链接后必须位于当前 worktree、共享仓库、项目和正式 Godot 用户数据目录之外，也不能包含这些目录。重复运行须换新的输出目录，不删除或覆盖旧证据。

```sh
# 先设置现有 .NET 运行环境；此处不会安装或构建任何内容。
export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$PATH"

python3 tools/r2-authority-tests/run.py \
  --engine /absolute/path/to/Godot.app/Contents/MacOS/Godot \
  --project /absolute/path/to/worktree/prototype \
  --output /absolute/private/path/to/r2-authority-run-01
```

`--timeout` 是正整数墙钟秒数，默认 `1800`；不会改变固定 60 FPS 的模拟时序。已有 600 秒和 1800 秒两輪均超时（exit 124、ERROR 0），第二轮末尾在 step 32、约 718.4 秒模拟时间仍执行真实运输；另进程的原完整 storage 教学已自然通过（约 2489.6 秒、12 次采矿、18 次配方，prepare/resume 均 exit 0）。原日志须保留，这些结果分别证明未完成的 R2 合同与可完成的原教学链，不能相互代签。

此次重计划处理测试本身的重复开销：物资账本、嵌套 DTO 与路线计划深核由实际意图版本、world epoch、测试步骤、目标／生产任务／建设工段阶段（含真实实例、paid 与 trip）变化触发；状态未变化时最多间隔 60 个物理帧兜底，不再在等待分支每帧重复深核。每个真实建设预留、载货交接和付费批次都必须被观察并验证，完成前核对覆盖；关键取消、运输、加工、保存和恢复边界立即深核。管理 hold 首尾保留完整 fingerprint，直接 tick／drive 和每条非法命令仍验证零变更。进度只在现有 R2 测试路径每 600 帧或步骤变化输出。生产 tick、delta 和数值规则不变，断言失败仍 exit 1；优先节流，无新 fixture、无提高默认超时。

新增权威断言包括建设／接线执行至终态的稳定 `build-N`、reservation 经真实 TaskId 对应发展 GoalId、旧暂停载货等待的 Unknown 投影与读取零变更、损坏档校验失败递增尝试修订号且保留旧 epoch／世界／原字节，以及未确认覆盖不递增。`WorldTransitionRevision` 是临时 UI 草案失效信号，不持久化、不回滚、不改变旧 schema 或配置哈希。新运行仍用全新输出目录，实际是否在 1800 秒内完成由主控最终构建后独立实跑判断。

每次运行在新输出目录内新建空 `saves/`，清除继承环境的全部 `YUDIAN_*`，仅设置：

- `YUDIAN_R2_SELF_TEST=1`
- `YUDIAN_PLAYER_GUI_TEST=1`（私有测试隔离标志，进程仍为 headless）
- `YUDIAN_TUTORIAL_TEST_ROOT=<output 的绝对路径>/saves`

现有 `.NET` 等非 `YUDIAN_*` 环境保留。引擎以参数列表直接启动 `--headless --fixed-fps 60 --path <project> --log-file <output>/engine.log --`，不经过 shell。实际 stdout/stderr 合并保留在 `stdout.log`，Godot 原生日志保留在 `engine.log`，私有槽和设置留在 `saves/`。

只有引擎退出码为 `0`、stdout 有完整行标记 `R2_AUTHORITY PASS`（可带后续合同名称），且两份日志均可读、均无 `ERROR` 或 `Unhandled exception` 时，运行器才返回 `0` 并输出 `R2_AUTHORITY_RUN exit=0 timeout=… evidence=…`。非零引擎退出码、缺日志、缺标记或错误日志均为非零；参数或目录拒绝返回 `2`。超时保留全部原件并返回 `124`，键盘中断返回 `130`；先向本次新建的引擎进程组发送 TERM，最多等 5 秒后必要时 KILL，并等待直属子进程退出，不扫描或终止其他 Godot 进程、父进程或服务。超时后的日志仅是部分证据。

本工具自身的语法、参数拒绝和微型私有替身检查不等于真实 authority 通过。真实 `Main` 合同由主控独立运行并读取实际输出；GUI、原生构建、导出和玩家体验须分别记录。
