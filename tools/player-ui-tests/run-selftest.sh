#!/bin/sh
# E01 Player UI headless 自测：构建后在真实主场景上驱动 PlayerController。
set -e
cd "$(dirname "$0")/../../prototype"
export DOTNET_ROOT="${DOTNET_ROOT:-/Users/ethan/.dotnet}"
"$DOTNET_ROOT/dotnet" build
exec /Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot --headless --path . \
  --script res://scripts/PlayerUI/PlayerUiSelfTest.cs
