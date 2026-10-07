#!/bin/sh
# E01 Player UI headless 自测：构建后在真实主场景上驱动 PlayerController。
# Godot/dotnet 可用环境变量覆盖：YUDIAN_GODOT、YUDIAN_DOTNET（默认 $HOME 下常见安装位置）。
set -e
cd "$(dirname "$0")/../../prototype"
DOTNET="${YUDIAN_DOTNET:-$HOME/.dotnet/dotnet}"
GODOT="${YUDIAN_GODOT:-$HOME/Applications/Godot.app/Contents/MacOS/Godot}"
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$DOTNET")}"
export PATH="$(dirname "$DOTNET"):$PATH"
"$DOTNET" build
exec "$GODOT" --headless --path . \
  --script res://scripts/PlayerUI/PlayerUiSelfTest.cs
