# Design and architecture

The prototype is a single-player, isometric woodland survival and production game. It is rendered in 3D with code-built low-poly models: natural woodland, modern DIY buildings and no fantasy styling. The 2D art comparison boards in `docs/art/` are historical samples; only the interface icons still use that illustrated art.

## Player loop

The intended fresh-start loop is to gather loose iron and nearby wood, build storage and a basic furnace, smelt iron and forge a pickaxe, mine iron and coal, then build and operate a reinforced furnace to make steel tools. Sell a steel tool through the village signpost and use the proceeds to hire and assign a worker. The 10–15 minute duration is a pacing target, subject to hands-on UI and exported-build review.

Food, water, the day/night cycle, shelter, wages, and wolf encounters support the production loop. Campfire light repels wolves within its radius. At half health or less, an unprotected player can be pinned by a leaping wolf; the pinned state (`SaveState.Mauled`) blocks movement and actions, drains health while the pack feeds, and makes the following death strip food from the recovery box. Killed wolves leave a `wolf_corpse` resource that yields raw meat, which the stone furnace cooks into edible meat. Sleeping in a cabin advances time toward dawn. Death moves carried items to a recovery box and respawns the player at the clearing after a 30-second timer. Recovery boxes are never automation storage and disappear once empty.

## Implemented rules

- The basic furnace accepts wood or mined coal for fuel. The reinforced furnace uses coal only, and steel production needs the furnace to stay hot through bellows pumping.
- Steel recipes consume iron bars and mined coal. Charcoal production is not implemented.
- Inventories are slot grids with stacks of 64; a worker carries one stack of up to 5.
- Selling lowers prices unit by unit and they recover over time; buying costs a fixed 1.25× the base price. A cart trip takes 30 seconds; purchases and sale proceeds settle on return.
- Workers are tracked individually. Resource assignments gather and deliver materials; building assignments haul recipe inputs and outputs and can operate furnace bellows. Two workers assigned to a reinforced furnace split bellows and hauling work.
- Chests have separate item allowlists for input and output. Empty lists allow all items. Automated deposits prefer a chest with a matching input filter, then a nearby unrestricted chest. Automated withdrawals respect output filters. Manual transfers ignore those filters.
- Pollution, vampires, multiplayer, and technology beyond steel are outside this POC.

## Code and data

The project targets Godot 4.7.2 .NET with C#. The `World` simulation and `SaveState` are in `src/World.cs`; inventories and their filter-aware transfers are in `src/Inventory.cs`; tunable items, buildings, recipes, trade values, and time values are loaded from `data/game.json` by `src/GameData.cs`. `src/Game.cs` owns UI, input, save/load, and the main game loop. `src/Grid.cs` draws the icon-cell grids, `src/ItemSlot.cs` carries drag and drop between them, `src/Machine.cs` draws station input and output slots, `src/Needs.cs` the HUD hunger and thirst icons, `src/Ending.cs` the one-time ending screen, and `src/Hotbar.cs` implements nine saved item/plan references, assignment, icons and use. Hunger and thirst are HUD icons; the other player stats appear in the inventory panel. `src/WorldView.cs` owns the 3D scene, orthographic camera, picking, lighting and world animation; `src/WorldScene.cs` adds ground dressing, particles and the animation review; `src/WorldModels.cs`, `src/ActorRig.cs` and `src/ModelKit.cs` build the models. `scenes/Main.tscn` is the project entry point and references `src/Game.cs`.

Keep balance and recipe values in `data/game.json` so they can be adjusted without changing simulation code. Keep rendering and input separate from state transitions so headless checks can exercise gameplay rules without relying on screenshots. Simulation checks live in `tests/SimulationChecks.cs` and cover filters, worker routing, furnace work, trade, wages, recovery after death, saves, and a material-driven progression run.

Every world object is a low-poly model assembled from Godot primitive meshes in code; meshes and materials are cached by parameters so repeated objects share resources. One tile is one world unit and an adult is about 1.65 units tall. The camera is orthographic with a 30° pitch, matching the old 2:1 isometric footprint and actor scale. Picking, the hover ring and canopy fading use the same per-kind vertical hit cylinders (`WorldModels.Hits`), so selection matches what is drawn. Trees use three variants chosen by object id, so saves need no new fields. Fading swaps in cached alpha-blended copies of a model's materials, because the Compatibility renderer ignores per-instance transparency. `assets/` keeps only the illustrated icon atlases and SVG toolbar icons.

Actor animation is driven by gameplay state. `ActorAnimation` maps state to an action and returns keyframed joint angles (`Pose`) for any time; `ActorRig` applies them to a hierarchy of hips, torso, head, shoulders, elbows, thighs and knees, lifting the hips so the lower foot stays on the ground. The player's carried tool selects its matching action and the tool shown in hand; workers animate according to movement and an explicit transient `WorkerWorkState`, not localized or presentation status text. The transient state is excluded from saves and recalculated on the next simulation tick. Manual bellows pumping and the unarmed punch trigger one loop of their action. Each work action has an impact moment that shakes its target and spawns chips, leaves, splashes or sparks. Pausing freezes animation and effects.

## Development and verification

Use the repository launcher from its root:

```sh
./build.py play
./build.py test
./build.py build
```

`play` sets up Godot 4.7.2 .NET and the .NET 10 SDK locally when missing. `test` runs the simulation checks with a 120-second limit and requires `SELF-TEST PASS`, followed by Godot input/UI checks requiring `UI-TEST PASS`. `build` runs both, exports a Linux executable, smoke-tests it, and packages the executable, game pack and runtime into a tar archive. Tool downloads and build output remain under ignored project-local directories.

The project targets Linux x86_64 and Godot's Compatibility renderer. First-time setup requires internet access; interactive play requires a supported graphics driver. The export is a runnable prototype. The world is a fixed small map, and workers use grid detours around water and buildings.
