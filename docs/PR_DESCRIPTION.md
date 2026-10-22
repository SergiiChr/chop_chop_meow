# Add dedicated player and worker animations

## Summary

Adds thirteen four-direction, eight-frame animation atlases for player and worker movement and actions, including iron and steel tools, hammering, bellows, and crate carrying. The player animation follows the relevant carried tool, with an appropriate axe or pick selected when needed; worker animation follows actual movement and work status. Pausing freezes the animation, while attacks and manual bellows pumping play their own action cycles.

Adds a developer animation review fixture with frame captures for visual inspection. Also blocks tool and bellows actions while paused and gives steel tools the full pickaxe yield when gathering stone.

## Validation

- `./build.py lint` — passed.
- `./build.py build` — simulation, all 13 animation assets, real Godot input/UI checks, Linux export and exported smoke test passed.
- Exported executable rendered 96 review frames covering all four directions; checked poses and boot anchors. See [animation preview](screenshots/animations.gif).

The review fixture is developer-only and is not a human playthrough or a measurement of the 10–15 minute pacing target.

## Limitations

The prototype still uses a compact fixed map and simple worker pathing, and its gameplay progression ends at steel. Later technology, pollution, vampires, multiplayer, and a measured first-playthrough duration remain out of scope.
