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
   if (Launch.Value("--drive") is string drive) AddChild(new ReviewDrive { Folder = drive });
   ```

2. Back up the saves. They are in `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/` in the main checkout, not in
   `~/.local/share/godot`: `build.py` points the game there, and every worktree reaches the same folder through its
   `.tools` symlink, so playing can overwrite the player's real saves. Copy `autosave.json`, the `settlement*.json`
   slots and `settings.cfg` out first. Press New game, not Continue, unless the test needs a save. A running game writes
   the autosave slot every 300 seconds of play, and joining from the pause menu writes it too, so compare the files when
   done and restore any that changed. Delete any `guest-*.json` the run made.
3. Start the game in the background with a folder in your scratchpad: `env -u WAYLAND_DISPLAY ./build.py play -- --drive <dir>`. On native Wayland the window runs at about one frame a second. Add `--showcase` and its flags for a late-game fixture instead of a fresh start.
4. When done, remove both: `git checkout src/Game.cs && rm -f src/ReviewDrive.cs src/ReviewDrive.cs.uid`. Never commit the driver.

## Drive

Write commands, one per line, to `<dir>/cmd.txt`.
The driver runs them, renames the file to `cmd-N.done`, appends `ok …` or `failed …` per line to `log.txt`, and sets `status.txt` to `ready` when idle.
Wait for `ready` before writing the next `cmd.txt`.

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
- Stop the game by PID from `pgrep -a Godot`. `pkill -f` and `pgrep -f` match the calling shell's own command line: `pkill -f` kills that shell, and a wait loop on `pgrep -f` never ends.
- A run driven by `scale` or a showcase fixture is not a fair timing or full-playthrough result; say what was skipped.
- Treat Godot errors in the game output as failures.
