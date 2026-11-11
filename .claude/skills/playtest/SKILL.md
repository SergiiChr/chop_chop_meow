---
name: playtest
description: Play the real rendered game window with keyboard and mouse events and take screenshots, without OS input tools. Use for a manual playthrough, checking a visual or input change in a normal game, or reproducing a player-reported problem.
---

# Playtest

There is no xdotool or ydotool on this Wayland machine, so the game drives itself.
Started with `--drive <dir>`, its `ReviewDrive` node (`src/ReviewDrive.cs`) reads commands from a file in that folder and
sends them through `Input.ParseInputEvent`, like real input.

## Set up

1. Keep the player's files out of it with `--file-prefix playtest-`. The saves and settings live in
   `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/` in the main checkout, which every worktree reaches through its
   `.tools` symlink, and the prefix makes the game read and write `playtest-*` files there instead. To test with one of
   the player's saves, copy it to a `playtest-` name first.
2. Start the game in the background with a folder in your scratchpad:

   ```sh
   env -u WAYLAND_DISPLAY ./build.py play -- --file-prefix playtest- --drive <dir>
   ```

   On native Wayland the window runs at about one frame a second. Add `--showcase` and its flags for a late-game fixture
   instead of a fresh start.
3. When done, send `quit` and delete the `playtest-*` files.

## Drive

The game opens with the intro film over the start menu: send `key Escape` and take a `shot` before anything else.

Write commands, one per line, to `<dir>/cmd.txt`.
The driver runs them, renames the file to `cmd-N.done`, appends `ok …` or `failed …` per line to `log.txt`, and sets `status.txt` to `ready` when idle.
Wait for `ready` before writing the next `cmd.txt`.
`.claude/skills/playtest/drive.sh <dir> "cmd" …` does both: it waits for `ready`, sends the commands, waits until they ran and prints their log lines.

| Command | Does |
| --- | --- |
| `key W [down\|up]` | Taps, presses or releases a key by Godot `Key` name (`W`, `Escape`, `Tab`, `Space`, `Shift`, `Ctrl`); digits `1`–`9` work as is |
| `hold W 1.5` | Holds a key for real seconds |
| `move X Y` | Moves the cursor (hover, placement ghost) |
| `click X Y [right] [ctrl] [shift] [double]` | Clicks at screen pixels. Code that reads `Input.IsKeyPressed`, such as Ctrl item moves, needs `key Ctrl down` first instead of `ctrl` |
| `down X Y [right]` / `up X Y [right]` | Presses or releases a button, e.g. hold right mouse to gather |
| `wheel X Y up\|down [steps]` | Zooms |
| `wait 2` | Waits real seconds, whatever the time scale |
| `scale 10` | Sets `Engine.TimeScale` to skip game time |
| `shot name` | Saves `<dir>/name.png` from the viewport |
| `quit` | Closes the game |

Coordinates are window pixels at the game's resolution; screenshots use the same pixels, so read positions off the last shot. A showcase runs at 1440x900 and a normal game at the saved resolution, so they differ.
Look at every screenshot before deciding the next step.

## Pitfalls

- A `key Escape` tap can do nothing, so a menu may not have opened or closed. Take a `shot` after it before the next
  step, and prefer clicking the Resume or Back button.
- `shot` lines in a row without a `wait` between them all capture the same frame: put `wait 0.05` between them for a
  sequence of a motion.
- Stop the game by PID from `pgrep -a ChopChopMeow`, the exported game's name.
  `pkill -f` and `pgrep -f` match the calling shell's own command line: `pkill -f` kills that shell, and a wait loop
  on `pgrep -f` never ends.
- A showcase switches panels and pages on its own, so a click there can land on a different page than the last
  screenshot showed. Take a `shot` right before clicking, or play a normal game for input checks.
- A playtest game left running holds the game's network port, and `./build.py check` or `test net` then fails or
  crawls. Quit the game before running checks.
- A run driven by `scale` or a showcase fixture is not a fair timing or full-playthrough result; say what was skipped.
- Treat Godot errors in the game output as failures, except the release-build engine bug AGENTS.md describes:
  `Attempt to disconnect a nonexistent connection` for `tree_exited` or `focus_entered` when a game ends or a dropdown
  closes.
