#!/bin/sh
set -eu
repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
dotnet_bin="${YUDIAN_DOTNET:-$HOME/.dotnet/dotnet}"
godot_bin="${YUDIAN_GODOT:-$(dirname "$repo_root")/tools-bin/Godot.app/Contents/MacOS/Godot}"
if [ ! -x "$dotnet_bin" ] || [ ! -x "$godot_bin" ]; then
    echo '未找到已安装工具，请设置 YUDIAN_DOTNET 与 YUDIAN_GODOT。' >&2
    exit 1
fi
export DOTNET_ROOT="$(dirname "$dotnet_bin")"
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$PATH"
exec "$godot_bin" "$@"
