#!/bin/zsh
set -e
lookdev_dir="$(cd "$(dirname "$0")" && pwd)"
repo_dir="$(cd "$lookdev_dir/../../.." && pwd)"
workspace_dir="$(dirname "$repo_dir")"
godot_bin="$workspace_dir/tools-bin/Godot.app/Contents/MacOS/Godot"
if [[ ! -x "$godot_bin" ]]; then
  print -u2 "Godot not found in workspace tools-bin. Open project.godot with the installed Godot."
  exit 1
fi
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
"$godot_bin" --headless --editor --path "$lookdev_dir" --import --quit
exec "$godot_bin" --path "$lookdev_dir"
