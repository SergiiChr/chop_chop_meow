---
name: playtest
description: Play the real rendered game window with keyboard and mouse events and take screenshots, without OS input tools. Use for a manual playthrough, checking a visual or input change in a normal game, or reproducing a player-reported problem.
---

# Playtest

There is no xdotool or ydotool on this Wayland machine, so the game drives itself: a temporary `ReviewDrive` node reads commands from a file and sends them through `Input.ParseInputEvent`, like real input.

## Set up

1. Copy the driver in and hook it into `Game._Ready`, right after `StartPerformanceRun();`:

   ```sh
   cp .claude/skills/playtest/ReviewDrive.cs.txt src/ReviewDrive.cs
   ```

   ```csharp
   if (LaunchValue("--drive") is string drive) AddChild(new ReviewDrive { Folder = drive });
   ```

2. Start the game in the background with a folder in your scratchpad: `./build.py play -- --drive <dir>`.
   Add `--showcase` and its flags for a late-game fixture instead of a fresh start.
3. When done, remove both: `git checkout src/Game.cs && rm -f src/ReviewDrive.cs src/ReviewDrive.cs.uid`.
   Never commit the driver.

## Drive

Write commands, one per line, to `<dir>/cmd.txt`.
The driver runs them, renames the file to `cmd-N.done`, appends `ok …` or `failed …` per line to `log.txt`, and sets `status.txt` to `ready` when idle.
Wait for `ready` before writing the next `cmd.txt`.

| Command | Does |
| --- | --- |
| `key W [down\|up]` | Taps, presses or releases a key by Godot `Key` name (`W`, `Escape`, `Tab`, `Space`, `Shift`, `Ctrl`); digits `1`–`9` work as is |
| `hold W 1.5` | Holds a key for real seconds |
| `move X Y` | Moves the cursor (hover, placement ghost) |
| `click X Y [right] [ctrl] [shift] [double]` | Clicks at screen pixels |
| `down X Y [right]` / `up X Y [right]` | Presses or releases a button, e.g. hold right mouse to gather |
| `wheel X Y up\|down [steps]` | Zooms |
| `wait 2` | Waits real seconds, whatever the time scale |
| `scale 10` | Sets `Engine.TimeScale` to skip game time |
| `shot name` | Saves `<dir>/name.png` from the viewport |
| `quit` | Closes the game |

Coordinates are window pixels at the game's resolution; screenshots use the same pixels, so read positions off the last shot.
Look at every screenshot before deciding the next step.

## Pitfalls

- Stop the game by PID from `pgrep -a Godot`; `pkill -f` with a pattern in the same shell command kills that shell.
- Saves written by the game in this sandbox may not persist across restarts; don't rely on reloading them.
- A run driven by `scale` or a showcase fixture is not a fair timing or full-playthrough result; say what was skipped.
- Treat Godot errors in the game output as failures.
