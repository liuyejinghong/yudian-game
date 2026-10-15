# tools/native-acceptance

tutorial-r2 批1 的原生验收私有入口准备工具。只生成私有验收包，不启动 app、不启动 GUI、不运行 selftest driver。

## 用法

```
python3 tools/native-acceptance/prepare.py --candidate <已签候选 Yudian.app 绝对路径> \
  --output <新的绝对私有目录，必须不存在> --slot-root <绝对私有槽根> --size 1280x800|1920x1080|1920x1200
```

- 候选所在目录必须有 `build-manifest.json`；引擎、PCK、runtime 与其中哈希不一致则拒绝。候选的 codesign 严格校验与 arm64 检查先于克隆。
- 槽根同时作设置根。拒绝相对路径、`user://`、仓库与 worktree、`~/Library/Caches`、`~/.cache`、Godot 应用数据（含正式 `user://saves`），也拒绝包含正式路径的上级目录。
- 输出目录内容：`YudianAcceptance.app`（新 Info.plist、包装器 `Contents/MacOS/YudianAcceptance`，仅外层重签，引擎与 Resources 字节不变）、`acceptance-manifest.json`（配置与哈希）。`native.log` 与 `captures/` 由引擎运行时生成。
- 包装器清除继承的全部 `YUDIAN_*` 变量，只设置 `YUDIAN_PLAYER_GUI_TEST=1`、`YUDIAN_TUTORIAL_TEST_ROOT`、`YUDIAN_PLAYER_TEST_SAVE`、`YUDIAN_PLAYER_GUI_SIZE`、`YUDIAN_PLAYER_CAPTURE_DIR`，然后以 `--rendering-driver metal --accessibility always --max-fps 60 --log-file <output>/native.log` exec 引擎。

## 已知边界

- `Main.ApplyWindowAndQuality` 的 `YUDIAN_PLAYER_GUI_SIZE` 白名单为 1280x800、1920x1080、1920x1200，与 `SOURCE_GUI_SIZES` 一致（`test_prepare.py` 静态核对）。`gui_size_supported_by_candidate_source` 只表示工具常量，不证明候选二进制由含该白名单的源码构建；须核对 `build-manifest.json` 的 `git_head` 含本次改动。
- 本工具不证明启动、焦点、后台隔离或图形输入；这些属于 Codex 原生验收。

## 局部测试

```
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/native-acceptance -p 'test_prepare.py' -v
```
