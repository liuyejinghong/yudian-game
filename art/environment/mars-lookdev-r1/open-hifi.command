#!/bin/zsh
set -e
export YUDIAN_MARS_HIFI=1
exec "$(dirname "$0")/open-lookdev.command"
