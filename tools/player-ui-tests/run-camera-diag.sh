#!/bin/sh
# CAMERA-01 镜头验收与诊断：只跑相机按钮验收 + 镜头输入诊断 phase（快速可重跑）。
# 完整 UI 自测仍走 run-selftest.sh（同一私有产物根合同）。
# 私有产物根（必需，repo 外）：YUDIAN_UI_TEST_ROOT 绝对路径，或 CLAUDE_JOB_DIR（在其
# tmp/player-ui-tests 下派生唯一 run 目录）；两者都无 fail-fast，绝不回落 repo/正式目录。
# 工具：Godot 默认 repo 同级 tools-bin（或 YUDIAN_GODOT）；dotnet 默认 $HOME/.dotnet/dotnet（或 YUDIAN_DOTNET）。
# 输出：完整日志写 $YUDIAN_UI_TEST_ROOT/camera-diag.log；脚本退出码即 Godot 真实退出码。
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
if [ -e "$test_root/camera-diag.log" ] || [ -e "$test_root/build.log" ] || [ -e "$test_root/saves" ]; then
  echo "需全新 root，既有日志/存档不会删除或覆盖：$test_root" >&2
  exit 2
fi
mkdir -p "$test_root"
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$DOTNET")}"
export PATH="$(dirname "$DOTNET"):$PATH"
export YUDIAN_UI_TEST_ROOT="$test_root"
export YUDIAN_CAMERA_DIAG_ONLY=1
unset YUDIAN_UI_LAYOUT_CAPTURE_ONLY YUDIAN_UI_CONNECTION_CAPTURE_ONLY YUDIAN_UI_LAYOUT_RESUME_SLOT
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
"$godot" --headless --fixed-fps 60 --path . --script res://scripts/PlayerUI/PlayerUiSelfTest.cs >"$test_root/camera-diag.log" 2>&1
status=$?
set -e
grep -E "PLAYER_UI_SELFTEST" "$test_root/camera-diag.log" || true
echo "PLAYER_UI_SELFTEST log=$test_root/camera-diag.log exit=$status"
exit $status
