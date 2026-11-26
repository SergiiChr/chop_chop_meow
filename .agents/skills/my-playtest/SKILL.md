---
name: my-playtest
description: Prepare a playable copy of the user's latest Chop Chop Meow save and settings for testing a change. Put a chest beside their player with full stacks of the direct supplies and enough ingredients and tools to make any underlying parts.
---

# My playtest

Use the worktree that contains the change being tested. Keep the original save and settings untouched.

## Bring the player's game over

- Find the main checkout's actual game data in `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/`.
  Pick the most recently written of `autosave.json`, `settlement.json`, `settlement-2.json` and `settlement-3.json`.
  Ignore backups, fixtures and prefixed slots. Copy its `settings.cfg` too, including the machine key that selects the saved player.
- In the target worktree's `build/xdg-data/godot/app_userdata/Chop Chop Meow/`, give both copies the same unused
  `my-playtest-<feature>-` prefix. Put the save in slot 1 as `<prefix>settlement.json` and the settings in
  `<prefix>settings.cfg`. The launcher refreshes ordinary saves and settings from main, but leaves these prefixed files alone.
- Compare the copied save's version with the target's `SaveState.Version`.
  If they differ, use the target's save validation and the intervening changes to work out what must change.
  Port only the copy by hand, including any changed fields, IDs or item formats. Do not just change the version number
  without checking those differences. If the target cannot load the copy, report why instead of starting an empty game.

## Stock the chest

- Place a chest on open ground next to the player selected by the copied settings, within reach and off any doorway or path.
  Use the target build's chest and inventory rules and a fresh thing ID. Close both worker filters so supplies stay put.
- Read the changed recipes, construction costs and use rules in the target build.
  Stock one full in-game stack of each direct resource, using that item's real stack limit, and one of each required tool.
- For a complex item, follow the recipes for its parts down to the raw supplies. Alongside the ready-made parts, include
  enough of each underlying ingredient, tool, fuel and consumable to make one complete set of parts, counting shared
  ingredients and batch yields. Make sure the required stations in the copied settlement are available for that chain.
  If the supplies do not fit, keep the direct supplies together where possible and put the rest in an adjoining chest.
  Use the target's item state for things like live firebugs. Don't put in the finished item as a substitute for its inputs.
- Verify in the target build that the copy loads as the saved player, the chest is within reach, and its contents match
  the test. Keep the prepared files in ignored game data and tell the user what to try first and what is in the chest.

Launch from the target worktree and choose **Continue**.

```sh
./build.py play -- --file-prefix my-playtest-<feature>-
```

Replace `<feature>` with the actual name before running the command.
Do not use `--drive` to launch this copy. Driven play erases the selected settings file at startup.
Use the [playtest skill](../playtest/SKILL.md) for separate input-driven checks that do not need the player's settings.
