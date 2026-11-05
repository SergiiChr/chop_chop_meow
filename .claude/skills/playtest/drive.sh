#!/bin/bash
# Usage: drive.sh <dir> "command" ... — waits for the driver, sends the commands, waits until they ran, prints their log lines.
dir=$1; shift
ready() { [ "$(cat "$dir/status.txt" 2>/dev/null)" = ready ] && [ ! -f "$dir/cmd.txt" ]; }
for _ in $(seq 120); do ready && break; sleep .5; done
printf '%s\n' "$@" > "$dir/cmd.tmp" && mv "$dir/cmd.tmp" "$dir/cmd.txt"
sleep 1
for _ in $(seq 240); do ready && break; sleep .5; done
tail -n $# "$dir/log.txt"
