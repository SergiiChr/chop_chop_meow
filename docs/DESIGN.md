# Design and architecture

The prototype is a single-player, isometric woodland survival and production game. Its selected visual direction is smooth illustrated sprites in a natural, readable style. The art comparison boards in `docs/art/` are samples, not screenshots or finished production assets.

## Player loop

The intended fresh-start loop is to gather loose iron and nearby wood, build storage and a basic furnace, smelt iron and forge a pickaxe, mine iron and coal, then build and operate a reinforced furnace to make steel tools. Sell a steel tool through the village signpost and use the proceeds to hire and assign a worker. The 10–15 minute duration is a pacing target, subject to hands-on UI and exported-build review.

Food, water, the day/night cycle, shelter, wages, and wolf encounters support the production loop. Campfire light repels wolves within its radius; sleeping in a hut advances time toward dawn. Death moves carried items to a recovery box and respawns the player.

## Implemented rules

- The basic furnace accepts wood or mined coal for fuel. The reinforced furnace uses coal only, and steel production needs the furnace to stay hot through bellows pumping.
- Steel recipes consume iron bars and mined coal. Charcoal production is not implemented.
- Market prices react to trade and recover over time. A cart trip takes 30 seconds; purchases and sale proceeds settle on return.
- Workers are tracked individually. Resource assignments gather and deliver materials; building assignments haul recipe inputs and outputs and can operate furnace bellows. Two workers assigned to a reinforced furnace split bellows and hauling work.
- Chests have separate item allowlists for input and output. Empty lists allow all items. Automated deposits prefer a chest with a matching input filter, then a nearby unrestricted chest. Automated withdrawals respect output filters. Manual transfers ignore those filters.
- Pollution, vampires, multiplayer, and technology beyond steel are outside this POC.

## Code and data

The project targets Godot 4.7.2 .NET with C#. The `World` simulation and `SaveState` are in `src/World.cs`; inventories and their filter-aware transfers are in `src/Inventory.cs`; tunable items, buildings, recipes, trade values, and time values are loaded from `data/game.json` by `src/GameData.cs`. `src/Game.cs` owns UI, input, save/load, and the main game loop. `src/Hotbar.cs` implements nine saved item/plan references, assignment, icons and use. Global player stats appear only in the inventory panel. `src/WorldView.cs` draws the isometric map, sprite sheets, actors, work feedback, and world interactions. `scenes/Main.tscn` is the project entry point and references `src/Game.cs`.

Keep balance and recipe values in `data/game.json` so they can be adjusted without changing simulation code. Keep rendering and input separate from state transitions so headless checks can exercise gameplay rules without relying on screenshots. Simulation checks live in `tests/SimulationChecks.cs` and cover filters, worker routing, furnace work, trade, wages, recovery after death, saves, and a material-driven progression run.

The sprite sheets under `assets/` are generated game art: `world-atlas.png` uses a 4-by-3 layout; `travel-atlas.png` contains a wolf and goods/payment carts; `environment-details.png` contains the natural pond and human-scale hut; and `item-atlas.png` supplies hotbar icons. Thirteen dedicated actor animation atlases provide four directions and eight frames for player and worker walking, tool use, and work. They cover iron axe and pick use by both actors, player steel axe and pick use, player and worker hammer and bellows work, and worker crate carrying. The scene uses a common actor scale and larger hut footprint, sparse resource groupings and a continuous grass/trail treatment. These are prototype assets and may need redraws or replacement as the game art develops.

Actor animation is driven by gameplay state. The player's held tool selects its matching animation; workers animate according to actual movement and assigned work. Pausing freezes animation, while attacks and manual bellows pumping start their corresponding action cycles.

## Development and verification

Use the repository launcher from its root:

```sh
./build.py play
./build.py test
./build.py build
```

`play` sets up Godot 4.7.2 .NET and the .NET 10 SDK locally when missing. `test` runs the simulation checks with a 120-second limit and requires `SELF-TEST PASS`, followed by Godot input/UI checks requiring `UI-TEST PASS`. `build` runs both, exports a Linux executable, smoke-tests it, and packages the executable, game pack and runtime into a tar archive. Tool downloads and build output remain under ignored project-local directories.

The project targets Linux x86_64 and Godot's Compatibility renderer. First-time setup requires internet access; interactive play requires a supported graphics driver. The export is a runnable prototype. The world is a fixed small map, and workers use grid detours around water and buildings.
