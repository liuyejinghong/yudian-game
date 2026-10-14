#!/bin/sh
# E01 Player UI headless 自测：构建后在真实主场景上驱动 PlayerController。
# 私有产物根（必需，repo 外，脚本与自测都不写 repo、不碰正式存档）：
#   YUDIAN_UI_TEST_ROOT 绝对路径 -> 直接使用；
#   否则给出 CLAUDE_JOB_DIR -> 在其 tmp/player-ui-tests 下派生唯一 run 目录（不覆盖旧产物）；
#   两者都无 -> fail-fast 提示传入参数，绝不回落 repo/正式目录。
# 工具：Godot 默认 repo 同级 tools-bin（或 YUDIAN_GODOT）；dotnet 默认 $HOME/.dotnet/dotnet（或 YUDIAN_DOTNET）。
# 输出：完整日志写 $YUDIAN_UI_TEST_ROOT/selftest.log，stdout 回放 PLAYER_UI_SELFTEST 行；
# 脚本退出码即 Godot 真实退出码（0=全过，1=有失败，3=无失败但有 BLOCKED）。
set -eu
repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
cd "$repo_root/prototype"
DOTNET="${YUDIAN_DOTNET:-$HOME/.dotnet/dotnet}"
godot="${YUDIAN_GODOT:-$(dirname "$repo_root")/tools-bin/Godot.app/Contents/MacOS/Godot}"
if [ -n "${YUDIAN_UI_TEST_ROOT:-}" ]; then
  test_root="$YUDIAN_UI_TEST_ROOT"
elif [ -n "${CLAUDE_JOB_DIR:-}" ]; then
  test_root="$CLAUDE_JOB_DIR/tmp/player-ui-tests/run-$(date +%Y%m%dT%H%M%S)-$$"
else
  echo "需私有产物根：设置 YUDIAN_UI_TEST_ROOT（绝对路径）或 CLAUDE_JOB_DIR（产物写入其 tmp/player-ui-tests）" >&2
  exit 2
fi
[ -x "$DOTNET" ] || { echo "未找到 dotnet：设置 YUDIAN_DOTNET（当前默认 $DOTNET）" >&2; exit 2; }
[ -x "$godot" ] || { echo "未找到 Godot：设置 YUDIAN_GODOT（当前默认 $godot）" >&2; exit 2; }
case "$test_root" in
  /*) ;;
  *) echo "YUDIAN_UI_TEST_ROOT 必须是绝对路径" >&2; exit 2 ;;
esac
if [ -e "$test_root/selftest.log" ] || [ -e "$test_root/build.log" ] || [ -e "$test_root/saves" ]; then
  echo "需全新 root，既有日志/存档不会删除或覆盖：$test_root" >&2
  exit 2
fi
mkdir -p "$test_root"
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$DOTNET")}"
export PATH="$(dirname "$DOTNET"):$PATH"
export YUDIAN_UI_TEST_ROOT="$test_root"   # 自测启动时清所有遗留 YUDIAN flags，只设置自己的 GUI_TEST/私有 save
unset YUDIAN_CAMERA_DIAG_ONLY YUDIAN_UI_LAYOUT_CAPTURE_ONLY YUDIAN_UI_CONNECTION_CAPTURE_ONLY YUDIAN_UI_LAYOUT_RESUME_SLOT
set +e
"$DOTNET" build >"$test_root/build.log" 2>&1
build_status=$?
set -e
if [ "$build_status" -ne 0 ]; then
  grep -E "error|warning|失败|错误" "$test_root/build.log" || true
  echo "PLAYER_UI_SELFTEST build_log=$test_root/build.log exit=$build_status"
  exit "$build_status"
fi
grep -E "Warning|Error|警告|错误|Time Elapsed|已用时间" "$test_root/build.log" || true
set +e
"$godot" --headless --fixed-fps 60 --path . --script res://scripts/PlayerUI/PlayerUiSelfTest.cs >"$test_root/selftest.log" 2>&1
status=$?
set -e
grep -E "PLAYER_UI_SELFTEST" "$test_root/selftest.log" || true
echo "PLAYER_UI_SELFTEST log=$test_root/selftest.log exit=$status"
exit $status
