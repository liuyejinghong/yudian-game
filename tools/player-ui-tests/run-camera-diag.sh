#!/bin/sh
# CAMERA-01 镜头验收与诊断：只跑相机按钮验收 + 镜头输入诊断 phase（快速可重跑）。
# 完整 UI 自测（原 38 项检查 + 末尾同一验收/诊断 phase）仍走 run-selftest.sh。
# Godot/dotnet 可用环境变量覆盖：YUDIAN_GODOT、YUDIAN_DOTNET（默认 $HOME 下常见安装位置）。
set -e
cd "$(dirname "$0")/../../prototype"
DOTNET="${YUDIAN_DOTNET:-$HOME/.dotnet/dotnet}"
GODOT="${YUDIAN_GODOT:-$HOME/Applications/Godot.app/Contents/MacOS/Godot}"
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$DOTNET")}"
export PATH="$(dirname "$DOTNET"):$PATH"
export YUDIAN_CAMERA_DIAG_ONLY=1
exec "$GODOT" --headless --path . \
  --script res://scripts/PlayerUI/PlayerUiSelfTest.cs \
  --log-file "/private/tmp/yudian-camera-01-diag-engine-$(date +%Y%m%d%H%M%S).log"
