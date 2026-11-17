# Design and architecture

The prototype is an isometric woodland survival and production game.
It is rendered in 3D with low-poly models: natural woodland, modern DIY buildings and no fantasy styling.
Item and building icons are rendered from the same models.

## Confirmed design decisions

These came from the user; change them only after asking.

- **Platform and launch:** Linux first, one-command launch from a fresh checkout through the Python launcher.
- **Look:** smooth isometric 3D, natural and lighthearted, with some of Terraria's approachable feel; no fantasy or industrial styling. Buildings are modern DIY, there are several tree species, and workers wear blue overalls over a cream shirt.
- **Animation:** every core interaction has an animation, with a dedicated one for every tool.
- **Progression:** the player starts with a steel axe, an empty bottle, 10 berries and 6 wood, and no pickaxe; no loose iron lies about and there are no iron tools. The first pickaxe is bought at a trading post with crates of berries.
- **Shovel:** a steel shovel is made, bought and sold like the pickaxe. It digs up the stump a felled tree leaves for 3 wood and a plain bush for 36 leaves, three oak patches' worth; both are gone for good. Stumps are part of a worker's wood gathering while a shovel is to be had, and bushes are a job of their own.
- **Mining:** coal, iron and tin deposits need a carried pickaxe; loose pieces need no tool. A pickaxe hit on rock or ore chips a loose piece off one time in twenty, so loose iron exists only from mining.
- **Fuel:** iron and tin smelt over coal or charcoal; wood only cooks food and makes charcoal. Charcoal cannot substitute for coal in steel.
- **Steel:** needs iron, coal, a reinforced furnace and active bellows heat. Two assigned workers divide bellows and hauling.
- **Worker tools:** workers take the tools their jobs need (axe for trees, pickaxe for deposits and mines, shovel for stumps and bushes, hammer for fitting a mine) from a tool station and put them back when reassigned or going home; a rake for leaves and a pickaxe for rock only speed them up. Without a needed tool they go to another spot of the job that they can work, loose pieces last, or wait and say so once.
- **Transfers:** automated transfers respect input and output allowlists, where empty means unrestricted, and prefer storage whose input filter explicitly matches. Piles set their own filters from what they hold. Manual transfers bypass automation filters.
- **Carried items:** never deleted when a worker is reassigned, fired, blocked or rerouted.
- **Survival:** food, water, day and night, wolves that fear light, sleeping in a cabin, wages, saves and recovery after death.
- **Wolves:** the same every night, never stronger or faster with the days; three or four bites bring a healthy player to half health. After the five let out at dusk, one den picked at random lets one more out every hour.
- **Canning:** meat stew and jam are canned; raw meat is not, and sells in crates as it is.
- **Saves:** a save from another version is refused, and no code converts old saves; the user's own save is fixed by hand when asked.
- **Market:** reached at a trading post; selling lowers a price, which recovers over time; deeper goods are worth more; orders settle after a visible 30-second cart trip.
- **Workers:** hired individually, paid daily, assigned, reassigned and dismissed; they carry station inputs and outputs, preferring matching filtered storage.
- **Pacing:** 10–15 minutes to the first goal is a target, not a measured playthrough.
- **Out of scope:** pollution, vampires, terraforming and technology beyond steel.

## Architecture

The project targets Godot 4.7.2 .NET with C#.
`scenes/Main.tscn` is the project entry point and references `src/Game.cs`.

### Interface parts

`Game` is a thin runner: it creates the interface's nodes once while loading, holds them and routes input and the frame to them.
Each feature is a node with a small public surface, given what it needs in its constructor.
`UiKit` carries the world, sounds and how a player action runs.
`Game` implements `IGameFlow`, what the menus call to start, load, leave or quit a game.
The interface reads the world through `World`'s read members and changes it through `World`'s actions, which go to the server; `world.S` is for what needs the real `Inventory`, such as an end of a move between grids.
`UiKit.Paused` follows which overlays are open: each one registers with `HoldWhile`, so none saves or restores another's pause.
`World.Replaced` fires whenever the whole state is replaced (a new game, a load, a player taking the server's world); each part resets itself on it, and what differs, such as which page opens next, stays where that path starts.
The side panel builds a new `PagePanel` node when a page opens; the page holds its own `LiveUpdates`, which run on each refresh, and its own state.
A page whose subject has gone (`PagePanel.Current`), such as an emptied recovery box or a building another player took down, closes on the next refresh.
A page that names another (`PagePanel.GivesWayTo`) opens that one instead: an emptied or placed carried crate's page gives way to the backpack's.
A tab or other choice on a page calls `Redraw`, which builds its body again and keeps that state; `PageMemory` keeps only what outlasts the page.
A `ThingPage` is a page about one thing in the world, with its job buttons and filters; a page calls `RedrawOnChange` beside what its layout reads, such as a mine's stage, so it draws itself again when that changes.
The side panel and the backpack drawer are `MovableWindow`s: a drag on an empty spot moves one within the screen, `Redraw` keeps it there, and opening a different page or building puts both back.
The board beside the panel moves with it, and the panel shifts right when the board would open past the screen's left edge.
Wells and boxes on the panel pass a press on their own surface on, so they move it too.
UI checks stay in `partial class Game` and reach the parts through their public members.

### Source files

Simulation and data:

| File | Owns |
| --- | --- |
| `src/World.cs` | The `World` simulation and `SaveState` |
| `src/ThingKind.cs` | `ThingKind`, what a world object is, and its ids in saves, data and hotbar slots |
| `src/ThingList.cs` | `ThingList`, the world's things, also kept by kind and by id |
| `src/Inventory.cs` | Inventories and their filter-aware transfers, and the read-only views `World`'s read members hand out: `ItemTotals`, `ListView` and `InventoryView` |
| `src/GameData.cs` | Time, pace and need values, the shapes of the game tables, and the check that they fit together |
| `src/GameTables.cs` | Items, buildings, recipes, hiring deals, the market, the mine, the farm and the goals |
| `src/Farm.cs` | Farm plots |
| `src/Mine.cs` | Mines |
| `src/Wolves.cs` | Wolf dens, the wolves they let out, patrols, smell, howls and the way home |
| `src/GatherRules.cs` | One table of how each resource is gathered: its item, the tool it needs or that helps, the by-hand factor, its chip, whether it is dug up whole and its job |
| `src/Loose.cs` | Loose stones, sticks and the chips a pickaxe knocks off rock |
| `src/Digging.cs` | Stumps and bushes: what a felled tree leaves, the valley's bushes, digging either up and building over them |
| `src/Scenery.cs` | The baked scenery spots, the ground they may stand on, and which are loose pieces or bushes |
| `src/Packing.cs` | Packing stations |
| `src/Jobs.cs` | The jobs and their table, a worker's job list with its haul groups, and adding, moving and grouping its entries |
| `src/WorkerJobs.cs` | A worker's job list read top down, the round each job is done for, deliveries, and the gather, craft, bellows and fire jobs |
| `src/Hauling.cs` | The four haul rules, what each kind of place offers a hauler, and the carry in hand |
| `src/Terrain.cs` | The fixed valley map |
| `src/Recovery.cs` | Skipping to dawn while downed |
| `src/Water.cs` | The refillable three-liter bottle and drinking at a water source |
| `src/Vessels.cs` | Pouring a vessel's liquid into a station, at any fill, and filling vessels with room from it |
| `src/Players.cs` | Per-player state, the command dispatcher, and players arriving and leaving |
| `src/Net.cs` | The ENet connection, joining by machine key, and sending and merging world updates |
| `src/Server.cs` | The headless server every game runs on: the world, its ticks and its saves |
| `src/Session.cs` | Where a window stands with the game it started |
| `src/WorldUpdates.cs` | World updates as entries, their differences and compression |
| `src/UpdateJson.cs` | How an update's entries are written: fields at their default left out |

UI and input:

| File | Owns |
| --- | --- |
| `src/Game.cs` | Creates and holds the interface's parts, routes input and runs the main game loop |
| `src/PauseMenu.cs` | The pause menu with the goal chain, and hosting |
| `src/UiKit.cs` | What the interface's parts share: `UiKit` (the world, the pause, how a player action runs, the buttons), `LiveUpdates`, `LiveText` and `MovableWindow`, a window the player drags into place |
| `src/Icons.cs` | Item and building icons, cached as they are first asked for |
| `src/Hud.cs` | The top-right corner with the clock plate and frame rate counter, and the mode and death labels |
| `src/MessageStack.cs` | The fading message stack at the top of the screen |
| `src/RecoveryPrompt.cs` | The "Skip to morning?" prompt |
| `src/MinePanel.cs` | The mine's building steps, miners and outputs |
| `src/Style.cs` | The shared enamel camp-gear look (speckled enamel boards with a cream rim, beige wells, cream slots, brick for what is active or wanted, Fredoka and Nunito) and the heat, day and cart-road dials |
| `src/SidePanel.cs` | The side panel: opening and closing its pages, selecting what it inspects, its size, and `PageMemory`, what outlasts a page |
| `src/BackpackDrawer.cs` | The backpack drawer above the hotbar |
| `src/InspectPage.cs` | Picks the page for the selected thing or worker, and `ThingPage`, the base of a page about one thing |
| `src/PagePanel.cs` | `PagePanel`, the base of every page |
| `src/ProcessRow.cs` | The process row every station shares and the fuel row under its bar |
| `src/JobRow.cs` | A place's job buttons, their worker lists and what each job is called |
| `src/FilterWindow.cs` | The filter window beside the panel |
| `src/ItemMoves.cs` | What clicking an item does: reach, refusals and the crate-in-crate warning |
| `src/Grid.cs` | Draws the icon-slot grids |
| `src/ItemSlot.cs` | The cream item slot, which tells a click, a right click and a double click apart and shakes on a refusal |
| `src/HeldCursor.cs` | `HeldCursor`, the held stack or building plan drawn by the mouse, and `SlotInput`, what each click does on an item slot |
| `src/HotbarStacks.cs` | `World`'s hotbar slots bound to backpack stacks: binding, waiting slots and reading what a slot shows |
| `src/HeldStacks.cs` | `World`'s held stacks: picking up, running commands marked "from held stack", letting go, and the lock on held units |
| `src/StationPanel.cs` | A station's page: recipe board, process row, bellows and jobs, with its tool slots in a window beside it |
| `src/StationSlots.cs` | `StationSlots`: a building's item slots and a station's process row |
| `src/CraftPanel.cs` | The crafting menu |
| `src/RecipeBoard.cs` | The recipe board the crafting menu and every station show, a recipe's hover popup, and the crafting menu's queue strip under the work |
| `src/RecipeMap.cs` | The recipe map: a row of item chains for each category, the station strip, chips that go to their item and the details strip |
| `src/PageText.cs` | How panels word durations and costs |
| `src/BlackHole.cs` | The crate-in-crate warning, collapse shader and game over screen |
| `src/MarketPanel.cs` | The trading post market, hiring offers and price sheet |
| `src/CrewPanel.cs` | The crew cards and a worker's own panel, with their hands and tool slot |
| `src/JobList.cs` | A worker's jobs as rows in order, with haul groups, dragged by their grips to another place |
| `src/StageTrack.cs` | Stages on a line, for a farm plot and a mine |
| `src/FarmPanel.cs` | A farm plot's stages and process row |
| `src/CabinPanel.cs` | A cabin: its door, sleep and the needs a night would leave |
| `src/Cabins.cs` | Cabin doors: walking round to the door, stepping in and out, following, knocking and being dragged out |
| `src/Momentum.cs` | How the player's movement answers the keys: braking, turning round and running back on a key for the other way |
| `src/CampfirePanel.cs` | A campfire |
| `src/ResourcePanel.cs` | A resource in the world |
| `src/PilePanel.cs` | A pile or compost heap |
| `src/ToolRackPanel.cs` | A tool station |
| `src/CratePanel.cs` | A placed crate, and a packed crate opened from the backpack |
| `src/RecoveryBoxPanel.cs` | A recovery box |
| `src/WelcomePage.cs`, `src/PlansPanel.cs`, `src/BackpackPage.cs` | The welcome page, the building plans and the backpack page |
| `src/StoragePanel.cs` | A chest or other plain storage |
| `src/PackerPanel.cs` | The packing station panel and its good picker |
| `src/GuidePanel.cs` | The field guide topics |
| `src/NeedsPlate.cs` | The HUD health, hunger and thirst vials, the bank and the red damage edges |
| `src/EndingScreen.cs` | The one-time ending screen |
| `src/StartMenu.cs` | The start menu with its save slot, load, options and join screens |
| `src/Saves.cs`, `src/Settings.cs`, `src/Launch.cs` | The save slots with quick save and load, the settings file and the command line |
| `src/PlayerControl.cs` | The player in the world: walking, gathering, lifting crates, placing buildings, clicking things, E and Space, and `ClickMode`, what the next click does |
| `src/Hotbar.cs` | The tool belt: the round menu badges and nine saved item/plan references, assignment and use |

Rendering and sound:

| File | Owns |
| --- | --- |
| `src/WorldView.cs` | The 3D scene, orthographic camera, picking, lighting and world animation |
| `src/WorldScene.cs` | Ground dressing, particles and the animation review |
| `src/Terrain3DView.cs` | Mountain and valley terrain through the Terrain3D plugin |
| `src/EnvironmentPlacement.cs`, `src/GroundDressing.cs` | Grass, ferns and flowers from the baked placement, cleared around buildings and paths |
| `src/WorldLook.cs`, `src/Wind.cs` | Worn footpaths, the sound listener and the wind |
| `src/LakeAppearance.cs`, `src/ShorelineAppearance.cs` | The lake and sea surfaces and their sand and stony shores |
| `src/WorldModels.cs`, `src/ActorRig.cs`, `src/ModelKit.cs`, `src/*Shape.cs` | Build the models (see [assets/README.md](../assets/README.md)) |
| `src/ActorAnimation.cs` | Picks actor actions and gives their keyframed poses |
| `src/WolfModel.cs` | Poses the wolf: its gaits, leap, howl, tug, recoil and fall |
| `src/IconRender.cs` | Renders item and building icons from the models (`./build.py icons`) |
| `src/Sounds.cs` | Plays sound effects |

Checks and review tools:

| File | Owns |
| --- | --- |
| `tests/*Checks.cs` | Simulation checks run by `./build.py check` (`SELF-TEST PASS`) |
| `tests/*Steps.cs` | Steps shared by the simulation checks |
| `src/GameChecks.cs`, `src/SurvivalUIChecks.cs` | Input/UI checks through real input events (`UI-TEST PASS`) |
| `src/RecipeReadyChecks.cs` | Every station recipe brought to the point where its next tick starts the batch, by double-click and by hand, and refused short of it with a shake and a message |
| `src/NetChecks.cs` | The host and guest network checks (`NET-HOST PASS`, `NET-GUEST PASS`) |
| `src/ServerChecks.cs` | One window playing on the real server process (`SERVER-TEST PASS`) |
| `src/InputSteps.cs`, `src/PanelSteps.cs`, `src/SessionSteps.cs` | Steps shared by the UI and network checks |
| `src/RecipeSteps.cs` | Steps that open a fresh station for a recipe, give and load its items and fuel, and expect a batch to start or be refused |
| `src/Showcase.cs` | The review scenes behind `--showcase` |
| `src/ReviewDrive.cs` | Plays the window from a command file behind `--drive`, for the `playtest` skill |
| `src/PerformanceRun.cs`, `src/PerformanceFixture.cs`, `src/PerformanceStage.cs` | The rendered performance run: the window's samples, the camp it plays and the server's half |
| `src/DoorwayReview.cs` | The `--doorway` scenes: two players and wolves at a cabin's door, moved by a script and run by the game's rules |
| `src/intro/WorldIntro.cs` | The intro film behind `--render-intro`: its shots, film camera, lighting, fades and sound cues |
| `src/intro/IntroSets.cs`, `src/intro/IntroPoses.cs` | The film's office, rented room and train car, and the poses nobody strikes in the game |
| `src/intro/IntroMovie.cs` | Plays the film over the start menu when the game opens, until it ends or Esc skips it |
| `tools/intro_film.py` | Draws the film's frames and writes its sound cue sheet |

### Simulation, view and UI

Keep balance and recipe values in `GameData` and `GameTables`, apart from the rules in the simulation code.
Keep rendering and input separate from state transitions, so headless checks can exercise gameplay rules without relying on screenshots.

- `tests/SimulationChecks.cs` covers filters, worker routing, furnace work, trade, wages, recovery after death, saves and a material-driven progression run.
- `tests/EconomyChecks.cs` measures real workers through a day and checks the economy rules.
- `tests/AllocationChecks.cs` holds ticks to their allocation budgets.
- `tests/ProductionChainChecks.cs` covers the packing station, the trading post's filter and auto-sell, and workers alone running the berry, bread, canned stew and steel pickaxe chains to a sale.

### HUD and backpack

Health, hunger and thirst are HUD icons.
Coins are a backpack item that stacks to 1000.

Every page that works with the backpack shows it in one place, a drawer above the hotbar (`PagePanel.BagGrid`).
On a page about a building or an opened crate (`PagePanel.StoresFromHotbar`), the drawer's Ctrl- and Shift-click (`BackpackDrawer.Store`) also runs for one on a hotbar slot.
The stacks that page takes are rimmed in brick and the rest are faded.
A close button hides the drawer until another page or building opens.
The side panel only shows the building and sizes itself to what it holds.

### Multiplayer

Every game runs on a `Server`, alone or together, which owns the only simulation and has no player of its own.
The window that starts a game runs it as a headless child process of the same executable (`--server`).
The server takes the first free port from `Net.DefaultPort` (24565) itself when it starts serving, since a port a window picked could be gone by the time the server came up, and writes it to a file named for the window's process.
A run of the checks starts its windows with `--default-port` and a port block of its own, which a window hands on to its server, so checks never meet a game being played or another run.
A server process logs to `user://logs/server.log`: sharing the window's log, each start would rotate the window's older logs out of the folder.
`Net.Log` prints the port the server took, connecting, joining, refusals and leaving on both ends, so a failed join can be traced from either computer's log.
The window joins it over localhost like any guest, showing a starting screen until it does.
`Session` says where a window stands with the game it started: starting it, playing it, away from it to join another, or going back to it.
The UI and network checks run the server inside the window, under a branch with its own `SceneMultiplayer`, so they can set its world up directly; the UI checks pump the line themselves (`Game.Settle`) rather than wait for it.
`src/ServerChecks.cs` and the performance run play on the real process, which plays the fixture with `PerformanceStage` driving it.
The review fixtures run their world in the window, which ticks it whenever it is not on a server.
The server ends when its owner leaves, or when nobody has joined for 30 seconds.

`SaveState.Players` holds one `PlayerState` per player playing now, and `SaveState.Absent` the rest of the world's players, each by the key of the machine they play from (`Settings.MachineKey`).
A save writes them all in `SaveState.Party` and leaves out the top-level player fields, which pass through to `SaveState.Actor`.
`SaveState.Owner` names the machine that last started the world.
The server sends everyone away on start (`World.Vacate`), then the starting machine takes the world over with the owner's player (`World.Claim`), so a save keeps its player on another computer.
`World.Arrive` brings back a machine's player, else the unclaimed one, else a newcomer, and `World.Depart` keeps a player who leaves.
A save holds `SaveState.Version`, raised whenever a change would make saves written before it load wrong, and `World.Deserialize` refuses any other version: nothing converts an old save.
`Saves.Write` writes the world beside the slot's file and renames it over, so a write that fails leaves the earlier save.

Player actions run in the acting player's context (`World.As`).
In a guest's copy, `World.Forwarded` sends each action to the host as a `Command`, named by its `Verb`, instead of changing the copy.
Guests walk locally and send their position, gather target and held item.
A host-made move such as a respawn bumps `PlayerState.Warp`, so it wins over the guest's own position.

Shared rules:

- Sleep needs every player sheltered.
- Wolves each hunt one player (`Wolf.Target`).
- Messages reach the acting player unless they concern everyone.

#### Network updates

An update holds only what changed since the last one the guest confirmed.
The host keeps the world of each recent update as one JSON entry per thing, worker, wolf, trade cart and player, plus two shared entries, and sends changed fields of changed entries.
Entries leave out fields at their type's default and write a fresh inventory as `{}` (`UpdateJson`).
A field whose new object starts elsewhere is marked to be always written, which `MultiplayerChecks` enforces.
A changed entry whose set of fields changed is sent whole.
`WorldUpdates` writes them into reused buffers and keeps the last update's array for every entry that did not change, so only changed entries allocate (`tests/AllocationChecks.cs`).
The welcome is a guest's first update, with the fog uncovered so far.

- Updates go unreliably on their own channel, so a lost one never delays the next.
- The whole world after a long stall, or a change too large for one packet, goes reliably on a third channel, so messages and actions never wait behind it.
- Guests merge entries into their copy in place, so panels and the view keep their objects, and touch only entries that differ from what they show. A change to numbers alone is set directly (`WorldUpdates.SetNumbers`); anything else reads the entry whole.
- The server writes every entry for each update and compares the bytes. With 876 things that costs 0.7 ms of its own process; marking what changed would save most of it, and one mutation missed would leave a guest's copy wrong without a sound.
- A player keeps only the updates the server can still build on: confirmations only move forward, so nothing older than the update just taken's baseline is needed.
- Each action carries where the guest stood, so the host checks reach from there rather than from a position still gliding in.
- `Net.SendEveryUpdate` turns off ENet's throttle: it read the hitch of loading a game as a slow line, and let through one update in 32 for several seconds.
- An action's result comes back with a later update. A window checks what it can before sending, and `Net.Made` names a new mine or worker so the panel opens on it. Panels refresh with every update while `Net.AwaitingAnswer` holds, and a page draws itself again when what sets its layout changes (`PagePanel.RedrawOnChange`), since drawing it right after an action would show the world before the answer.
- Leaving waits for the server to confirm before the line closes, so actions still in flight arrive; the owner's server closes instead, and the line going is the confirmation.
- Joining another game from your own autosaves it and leaves with `Game.StepAway`, which keeps its server up: a failed or cancelled join goes back to it, and after a good one it ends with nobody in it, 30 seconds later.
- A player whose line drops lets `Net.Retry` join the same address again until `Net.ReconnectLimit` runs out. A new connection gets a new id, and takes the machine's player over from the old line, which the server may not have noticed has gone. Only the same window can: each sends its process id, because two windows on one computer share a key. It goes on trying while the server says the game is not open yet, as after the host loads a save.
- Until the owner opens the game, the server refuses peers that are not on this computer. Only the owner can save; any player can hold the world, and it stands still while all of them do.
- The server's welcome says whether the player owns the game, from their machine's key, so an owner who types their own game's address into the join popup still owns it.

Factorio instead runs the same deterministic simulation on every machine and sends only inputs (lockstep).
This simulation uses floats and frame-length steps, so it is not deterministic, and with hundreds of objects the deltas stay small.

### Models and picking

Every world object is a low-poly model assembled from Godot primitive meshes in code.
Meshes and materials are cached by parameters, so repeated objects share resources.
`WorldModels.Warm` builds one model of every kind in each of its looks while the world loads, and `WorldView` draws them behind the ground for its first frames, so a building, crate, carcass or wolf that first appears mid-game builds no mesh, material, shader or pipeline; the three trade carts are built then too and reused for every trip.
One tile is one world unit and an adult is about 1.65 units tall.
The camera is orthographic with a 30° pitch, giving a 2:1 isometric footprint.

Picking, the hover ring and canopy fading use the same per-kind vertical hit cylinders (`WorldModels.Hits`), so selection matches what is drawn.
A tree crown only wins a click when nothing else is under the cursor, so objects behind a canopy stay selectable.
Trees use three variants chosen by object id, so saves need no new fields.
A tree's height comes from its id too (`WorldModels.TreeTall`): its model and hit boxes are sized by `WorldModels.TreeScale`, 0.9 to 1.5.
The bake stands each tree's lowest point on the ground, which on a spruce and one oak is a hanging branch, so their baked trunks start up to 1.8 m higher; `Tree3DShape.Add` carries such a trunk down to the ground with a stem that widens toward its foot.
A stump keeps the id of the tree that left it (`Thing.OfTree`), so its model stands where that tree's trunk did (`DugProp.Seat`) and is as thick as the trunk was at the ground (`Tree3DShape.Foot`): a birch leaves a thin stump and an oak a stout one.
A stump with no tree on record, such as a showcase's or one in an older save, is shaped as if a tree with the stump's own id had left it.
Fading swaps in cached alpha-blended copies of a model's materials, so faded trees still share a handful of materials.

### Actor animation

Actor animation is driven by gameplay state.
`ActorAnimation` maps state to an action and returns keyframed joint angles (`Pose`) for any time.
`ActorRig` applies them to a hierarchy of hips, torso, head, shoulders, elbows, thighs, knees and ankles, lifting the hips so the lower foot stays on the ground unless `Pose.Hips` sets their height.
A change of action blends from the old pose into the new one over `ActorAnimation.BlendSeconds`, while the body turns toward where it moves, so standing up from a squat into a jog is one motion; a blend into or out of a set hip height, such as a run's bounce, keeps the feet out of the ground.
Moving is built from real walking and running mechanics, checked against Muybridge's photographs and gait studies.
The player jogs (`ActorAction.Jog`) and sprints with Shift (`ActorAction.Run`); workers and the cart courier walk (`ActorAction.Walk`, `Carry` with a load).
The player's pace is a run's, not a walk's: past about two metres a second a walk turns into a run.
In all three:

- The clock moves with the ground the actor covers, `ActorAnimation.Stride` metres a cycle of two steps, so a planted foot holds its place on the ground whatever the speed.
- A planted leg is solved to keep its foot where it landed as the body passes over it, and the swinging leg follows a smooth curve that joins the planted leg's motion at push-off and landing.
- Each arm swings with the opposite thigh to balance it, the shoulders turn against the hips, the torso tips over the planted foot, and the head keeps the eyes level.

A walk:

- Each foot is down for a little over half the cycle, so both are down for a moment after each landing.
- The heel lands a quarter of a metre ahead of the hips with the toes up, and the foot rolls down flat.
- The knee gives under the weight and straightens as the body vaults over the leg; once it would have to straighten further, the heel lifts and the foot rolls onto its toe to push off.
- The hips are highest over each planted leg and lowest while both feet are down, about four centimetres apart.
- The swinging knee folds to clear the toe a centimetre or two above the ground, then straightens to land on the heel.

A sprint and a jog share their mechanics and differ in timing and form:

- Each foot is down for under a fifth of a sprint's cycle, about a tenth of a second, and about a quarter of a jog's; the rest of each step is spent in the air.
- The front of the boot lands about a third of a metre ahead of the hips; the knee gives under the weight, then the heel lifts and the foot rolls onto its toe to push off.
- The hips bounce as gravity moves them: a parabola while both feet are off the ground, and a dip on the planted leg that takes the landing speed, six to seven centimetres in all.
- The swinging heel folds up under the hip, the knee drives forward and up, and the shin reaches out and sweeps back to land; a sprint folds and drives higher.
- The feet land close to one line, and the trunk leans into the run, more in a sprint.
- The elbows stay bent, most in front, bringing the hand up toward the chin.

Players and workers turn as people do (`Turning`): the head turns toward a new heading first, about 0.2 seconds ahead of the body, which follows on a critically damped spring and covers 90% of a quarter turn in 0.3 seconds.
The body leans about the feet toward the inside of the turn, by the angle that balances turning at its speed against gravity, up to `Turning.MaxBank` (8°) in a sprint and `Turning.MaxJogBank` (1°) at a walk or jog.
A half turn swings round the way the body already turns, or the way the head looks.

The window moves its own player through `Momentum`: a key for about the opposite way brakes the run to a stop over `Momentum.BrakeSeconds`, stands `Momentum.TurnSeconds` to turn round and builds back up to full pace over `Momentum.StartSeconds`; any other change of keys turns at once, and letting go stops at once.
While it brakes, `PlayerState.Braking` is set and carried to other machines, and the view shows the `Brake` lunge: front heel planted ahead, the weight sunk onto the bent back leg, leaning back with the arms forward.

Digging (`ActorAction.Dig`) holds the shovel in both hands: the right on top of the handle and the left `ActorAnimation.ShovelGrip` down the shaft.
`ActorAnimation.DigKeys` give where the blade's tip is and how the shaft leans at each moment: driven into the ground ahead, leant on, levered up and thrown aside to the left.
Both arms are solved from there with `ActorAnimation.ArmTo`, and `ActorRig.Show` lays the shaft from the right fist through the left, so the blade goes where the keys take it.
The front boot stays flat and the back one on its toe at fixed spots, so neither slides as the hips go down and up.
The handle is kept ahead of the right side of the chest and below the shoulders: at the middle of the chest the folded right arm would lie inside the torso, and higher it would be in front of the face.

A player with something to make by hand squats over it, turning it in both hands (`ActorAction.Craft`), only while standing still.

The player's carried tool selects its matching action and the tool shown in hand.
Workers animate according to movement and an explicit transient `WorkerWorkState`, not localized or presentation status text.
The transient state is excluded from saves and recalculated on the next simulation tick.

At a packing station the packer stands behind the table (`World.PackStand`) and the `Pack` action rummages in the heap of goods, lifts a piece across and settles it into the crate, then turns back.
The player working a station they stand near packs the same way.

At a cooking table, which is only worked for the mortar's recipes and dough, the `Grind` action holds the mortar in hand (`ActorAnimation.StationAction`); kneading dough uses the `Pack` action, facing the table.
The left fist holds the bowl out in front, and the right takes the pestle twice round the bowl's floor, then lifts it and pounds.
Both arms are solved from where the hands have to be (`ActorAnimation.ArmTo`), so the pestle stays over the bowl.
`ActorRig` turns the bowl and pestle against their forearms in the resting pose (`ActorAnimation.GrindRest`), which keeps the bowl level and the pestle leaning toward its hand.
While someone grinds, the table's own mortar is off the counter (`WorldView.grinding`).

Felling a tree (`Chop`, `SteelChop`) is a stroke from the side: the axe is wound round to the left at shoulder height and swung across, almost level, into the trunk's flank.
The shoulders turn through the stroke, the grip turns the blade to cut sideways (`Pose.Grip`, which `ActorRig` applies at the right fist), and the left fist is put on the handle just past the right (`ActorAnimation.TwoHanded`).
An axe swung in the air with Space or a click (`AxeSwing`, `SteelAxeSwing`) goes up overhead and comes straight down, which is the blow that lands on a wolf.

Manual bellows pumping, the unarmed punch and the axe swing trigger one loop of their action.
Each striking work action has an impact moment that shakes its target and spawns chips, leaves, soil, splashes or sparks; packing and grinding strike nothing.
A dig strikes at `ActorAnimation.DigStrike` and throws its soil off the blade (`ActorRig.ShovelTip`) at `DigToss`.
An action's key poses are made once (`ActorAnimation.ChopKeys` and the like): every worker and player is posed every frame, so a pose is worked out without allocating.
Pausing freezes animation and effects.

### Wolf animation

`WolfShape` builds one skinned wolf mesh on a skeleton of spine, neck, head, jaw, ears, a three-bone tail and four legs of three segments and a paw; `WolfModel` poses it.
Legs are placed by two-bone IK from where each paw should stand, with the front elbow bending back and the hind stifle forward as in a real canine.
A stance moves the paw back at exactly the body's speed, so paws stay where they land.
`WolfModel.Move` picks a lateral-sequence walk, a diagonal trot or a rotary gallop from the wolf's smoothed speed; the gallop folds the spine at the gathered float and stretches it at the extended one.
A change of motion blends from the last shown pose over a short smoothstep, so standing, walking, trotting, galloping, leaping and recoiling run into each other.

`WorldView` times the wolf against the player:

- a leap arcs up and comes down jaws first on the throat (`WorldView.Throat`); contact at `WolfModel.ContactReach` knocks the player onto their back facing the wolf, and the jaws stay on the throat as they go over, so the wolf ends standing over them;
- a wolf a swing will hit lunges into it to `WolfModel.StrikeReach`; when the swing lands (`ActorAnimation.Contact`) it hops back `World.KnockBackDistance`, moving only while in the air, and ducks low before rising wary;
- a killing blow throws the wolf off its feet onto its side, covering the knockback in the air, and it lands with a bounce and lies limp.
- a howling wolf (`Wolf.Howl`) braces, lifts its chest and throws its head back, muzzle to the sky, its jaw opening as the note swells (`WolfModel.Howl`).

Wolf eyes glow faintly, so they show in the dark.

## Gameplay

### Player loop

The intended fresh-start loop starts by securing water, shelter and fire, then uses the steel axe the player carries:

1. Collect water, wood, stone and berries; build a cabin and campfire before dusk.
2. Fell trees with the starting steel axe; pick up loose stones and gather berries by hand.
3. Knap a stone chisel, then carve planks and a mallet.
4. Build a crafting bench and make crates there.
5. Build a trading post and sell crates of berries for a steel pickaxe.
6. Mine iron and coal, and smelt iron in a stone furnace.
7. Build and operate a reinforced furnace (`reinforced`) to make steel.
8. Hammer steel into steel parts at the anvil and make steel tools.
9. Pack a steel tool into a crate, sell it with another crate of berries through the trading post and use the proceeds to hire and assign a worker, with enough left for their first wage.

There are no iron tools; iron bars only go into buildings and steel.

#### Water

The player starts with one refillable bottle (`src/Water.cs`); the player-facing rules are in [README.md](../README.md#food-water-and-time).
Filling it or drinking straight from the source takes one `GatherSeconds`.
Every point on the pond shore is a collection target.
A station takes water as a liquid in liters, `Water.Liquid`, with a drawn droplet icon and no model; nothing but a station holds it, and a recipe asks for so many liters.
`GameData.Vessels` lists each refillable vessel (`VesselDef.States`, its item at each fill from empty to full, and its liquid), and `src/Vessels.cs` holds the rules for all of them:
a vessel put into a station whose recipe takes its liquid pours every liter it holds there and stays where it was, emptied (`World.PourVessels`); loading a batch pours from the least full first (`World.PourLiters`);
the liquid leaves only into vessels with room, fullest first (`World.Fill`), so taking it with none is refused (`World.FillBlock`) and the slot shakes.
Every move of a liquid or a vessel between boxes, by hand or by a worker, goes through `World.Carry`, which picks one of those or a plain transfer.
Loading a station by hand and a worker's delivery pass it what the input slot has left (`Inventory.SlotRoom`).
Haulers fetch a liquid input in the fullest vessels storage has (`World.LiquidSource`), pour them all and store them again; leftover liquid leaves in vessels with room a hauler fetches (`World.RoomySource`), and a dismantled station fills the backpack's vessels and spills the rest.
No hand recipe takes a liquid, which `GameData` checks.

During the respawn countdown, a player with no cabin anywhere in the world is offered “Skip to morning?”.
It advances normal simulation through dawn, including needs, trades, production and wages.
In multiplayer it is available only when everyone is downed; a living player prevents a shared time skip.

### Goals

The end goals:

- a working mine on every ore in `World.Cliffs`;
- `Goals.ProfitDays` consecutive days of at least `Goals.DailyProfit` profit.

`SaveState.DayRevenue` collects returned sale coins.
At dawn the wages charged are taken off and the result joins `SaveState.Profits`.
Reaching the streak sets the `profit_week` milestone, which stays reached.
The starter goal chain stays as the tutorial, and the pause menu lists both.

### Survival and needs

Food, water, the day/night cycle, shelter, wages and wolf encounters support the production loop.

- Items carry what eating or drinking one restores (`ItemDef.Hunger`, `Thirst`) and a `Food` flag; nothing eaten heals.
- Health comes back only in sleep, `World.SleepHeal` a second while hunger and thirst both stay above `World.FedNeed`; `World.HealthAtDawn` foretells it for the cabin's page.
- Food is what wolves eat and compost will take, cooked or not.
- An item that restores nothing, such as raw meat, wheat or dough, only says it needs cooking.
- `HungerHours` and `ThirstHours` in `GameData` set how many in-game hours a full bar lasts.

Sleeping in a cabin advances time toward dawn.
It is allowed from `World.SleepHour` (22:00), a little before night falls.
It is refused if hunger or thirst would run out first.

#### Sprinting

Holding Shift while moving sprints at `SprintSpeed` times the walking speed (`PlayerState.Sprinting`, sent by guests with their walk).
Hunger and thirst drain `SprintDrain` times as fast meanwhile.
Under 15% food or drink (`GameData.LowNeed`) the player cannot sprint and the screen edges pulse red.
As food falls under 20% (`GameData.GrowlBelow`), and with every further 5% gone (`GameData.GrowlEvery`), `stomach_growl` plays at the player.

#### Death and recovery boxes

Death moves carried items to a recovery box and respawns the player after a 30-second timer in the free cabin nearest to where they fell, or at the clearing without one.
Recovery boxes are never automation storage and disappear once empty.

### Sight and fog of war

Fog of war is simulation state.
`World.Look` runs every tick: it marks tiles within `SightRadius` of the player in `SaveState.Explored` and replaces `SaveState.Remembered` entries in sight with snapshots of the live things there.

`World.CollectKnown` gives what the view draws and picks: live things in sight, snapshots elsewhere, nothing never seen.
Snapshots are updated in place and keep only the look: no inventory, fire, heat or work in progress.
The view picks against what it draws but only returns things that still exist.

Only what is in sight is drawn every frame (`WorldView.SyncThings`).
A thing out of sight is drawn once as its snapshot, moved on far enough to finish whatever it was doing, and left alone until its snapshot changes, night falls or lifts, or it is seen again.
A change that did not happen in front of the player is not played when they see it (`WorldView.Watched`): a tree felled out of sight is a stump, a chest filled out of sight keeps its lid shut, and a chip or a building that appeared out of sight neither flies nor lands in dust.
A thing that leaves the world in front of the player may play its way out (`WorldView.Departs`): its model is kept until `WorldView.Depart` is done with it.
A felled tree topples and fades over the stump that took its place, and a stump or bush dug out is thrown up, tumbles and shrinks away; one a building now stands over (`World.Clears`) just goes.
While it is dug at, a stump or bush leans further over with `Thing.Progress` (`DugProp`).
Work out of sight shakes nothing and throws no chips.
It is silent too, since `SightRadius` (20 metres) lies beyond the 16 metres a sound carries ([Sound effects](#sound-effects)).

`WorldView` hides wolves, workers and carts outside sight.
It draws one screen-wide quad after everything else, placing each pixel in the world from the depth buffer: black on unexplored tiles, a grey wash on explored ones, clear inside the sight circle.

### Night and wolves

`src/Wolves.cs` runs the wolves that hunt nobody; `World.TickWolves` runs each player's hunters.
Every wolf, like players and workers, moves only onto ground `World.CanStand` allows: `World.Slide` for a step that may slide along what blocks it, and `World.Travel` for a walk to a goal that routes around it.
`Travel` swerves round anything a route's straight leg clips instead of walking through it, and after a search finds no way it waits `RouteRetrySeconds` before the next.

- **Dens.** `World.Dens` are five small caves at the foot of the ranges, drawn as a mine's outcrop and tunnel at half size.
  The outcrop is solid like a mine's (`World.InSpur`), and nothing may be built within `Den.Clearance` of a door, so the wolves always have a way out.
  From `GameData.DuskStarts` (20:24), over an hour before `World.SleepHour`, each den lets out `WolvesPerDen` wolves at its `Den.Door` once no player sees it (`SaveState.DensOut`), one den every `DenGapSeconds` so their wolves never all appear on one frame.
  After each further hour one den, picked at random among those whose door no player sees, lets one more out until the wolves head home (`SaveState.DenHours`).
- **Strength.** A wolf's health, bite and pace are `GameData.WolfHealth`, `WolfBite` and `WolfTrot`, the same every night: the fourth bite brings a healthy player to half health.
- **Patrol.** A wolf hunting nobody (`Wolf.Target` zero, `WolfMode.Patrol`) walks its `Wolf.Heading` at `PatrolPace` (half) of the trot it stalks at, a walk, and picks a new random heading it can take when the ground ahead is blocked or in campfire light.
- **Smell and howl.** A patrolling or called wolf within `SmellRadius` of a player out in the open takes them as its target.
  One that finds them beyond `WolfSightRange` howls, standing still, for as long as the howl recording plays (`LoneHowlSeconds` or `PackHowlSeconds`, `Wolf.Howl` counting down `Wolf.HowlLength`); one that finds them within sight goes for them at once.
  The night's first howl plays `wolf_howl`, a lone wolf, and later ones `wolf_howl_group` (`SaveState.Howled`).
  A howl is silent beyond `HowlRadius`, so a far pack sounds far; like every placed sound it fades with distance from the player and pans with its direction.
  The howl calls every patrolling wolf within `HowlRadius` to where it stood (`WolfMode.Called`, `Wolf.Call`); a called wolf still smells on its way and goes back to patrolling once there.
- **Approach.** After its howl a wolf trots at its player out of firelight until within `WolfSightRange`, then sprints (`Wolf.Chase` above zero) at `WolfChase` times `WalkSpeed` with one distant growl (`Wolf.Sprinted`), which data validation keeps above walking and below `SprintSpeed`.
  Within sight it closes in only to `World.StandOff`, within biting reach, and backs off to it at a walk (`World.BackOffSpeed`) from a player another wolf drags toward it (`World.Hauled`).
  Wolves at a player keep their bodies apart (`World.Body`, `World.WolfHalfWidth`): one in another's way steps aside at `World.SideStepSpeed`, unless it holds the player.
  A wolf leaping at a player in a cabin's doorway lands at their waist (`WorldView.InTheDoorway`), and one taking hold of a player lunges in to the waist rather than appearing there.
- **Home.** From `WolfRetreatStarts`, `WolfRetreatHours` before dawn, every wolf drops its hunt and runs to the nearest den's door (`WolfMode.Home`), where it goes in; dawn clears any still out.
  A player a wolf had pinned goes free as it leaves.
  Patrolling, going home or losing its player (who sheltered or left the game) clears everything a wolf was doing to them (`World.Calm`).

Campfire light repels wolves, and `CampfireHours` sets how long one log burns.
A wolf already chasing the player still follows into the light for a few seconds.
Inside a cabin (`SaveState.Sheltered`) the player is hidden: their hunters give up at once and patrol on away from it.

#### Cabin doors

`src/Cabins.cs` runs a cabin's door, which holds one player at a time.
A click on a cabin walks the player to `World.DoorPoint` along `World.TowardDoor`, round the walls when the cabin is in the way, and `World.Enter` starts there.
What a player does at a door is a `Doorway` stage on `PlayerState.Door`, timed by `PlayerState.DoorTime`; while it lasts they cannot move, swing or light a torch.

- `Entering` takes `World.EnterSeconds`, or `World.RushSeconds` for a player who sprinted there (`PlayerState.DoorRush`), then shelters them.
- A player who arrives while someone else steps in is `Following` until that one is inside, then `Knocking`: the door swings shut over `World.ShutSeconds`, they step up to it, and from `World.KnockLead` bang `World.Knocks` times over `World.KnockSeconds`, then stand free.
  At the door of a cabin someone is in or stepping out of, they knock straight away, with the same lead.
  Each arrival at a door is numbered (`SaveState.DoorArrivals`, `PlayerState.DoorQueued`), so players dragged back out go in again in the order they came.
- `World.Indoors` is what wolves go by: inside, through the second half of `Entering` and the first half of `Leaving`.
- A bite or a leap landing on a player out in the open at a door (`World.PullOut`) has that wolf take them by the waist (`Wolf.Tugging`) and drag them `World.DragDistance` back out along `World.OutOfDoor` in `World.Tugs` tugs over `World.PullSeconds` (`Pulled`, or `PulledOff` while knocking), the way `World.Dragged` paces it, with a `pulled_scream` at each tug.
  The world moves the player there when the drag ends; meanwhile the wolf stands `World.TugReach` behind them and no other wolf leaps.
  The wolf then lets go and bites no sooner than a second later; at half health or less it pins them, otherwise a pulled player steps in again at a run, and one pulled off the door stands free.
- `Leaving` puts the player on the doorstep and takes `World.LeaveSeconds`.
  Anyone waiting at that door, banging on it or standing free within `World.DoorReach`, goes `Aside` to `World.AsideSpot`, a step along `World.AsideWay`, and steps in once the leaver is through the doorway, in the order they came, while the leaver keeps `World.MakeWay` to the other side.
  The view walks them in from there up the side of the step (`WorldView.Inward`), the two turning chest to chest with their arms hanging as they pass (`WorldView.SqueezePast`), the one going in with the right palm on the door frame (`WorldView.Reaching`), the hand starting up while they wait aside and the elbow kept below it.
- A follower waits aside, out of the way, while the one ahead is dragged back out (`WorldView.Behind`).

The view walks the player through the doorway over the stage (`WorldView.AtDoor`), hurrying from the second tread, and drags a pulled one back out facing the door, bent over and stepping back with each tug (`ActorAction.Tugged`), while the wolf holds the back of their hips in its jaws, braced wide on its forelegs, and throws its weight back (`WolfModel.Tug`).
On the two treads of the step and the floor (`WorldView.StepEdges`) the body stands on the boot that bears its weight and the other knee bends to put its boot on its tread, a swinging boot rising ahead of each edge (`ActorAnimation.StepLift`, `ActorAnimation.OnSteps`), so no boot sinks into the step.
A wounded player dragged out falls backward still facing the door (`ActorAction.FallBack`, `WorldView.FallBack`), and the wolf lets go, steps round to their left (`Wolf.Beside`) and lunges onto the throat from there as they land.
A player banging on the door stands on the lower tread, with the front boot on the upper one, and steps back down off it when done.
The animation review's `player cabin` clip plays every stage at a cabin, `player jog back` and `player run back` a run braking and turning round, and `player jog turns` and `player run turns` the keys A, W, D and S pressed in quick turn.
The door opens while someone steps in or out: it swings with `hut_door_normal` after a walk, and slams shut with `hut_door_escaping` after a run.

#### Torch

A lit torch (`SaveState.Torch`, burn seconds left) burns as long as one log, only while `SaveState.TorchLit`; it stays in the backpack, burning out removes it, and a backpack with no torch left puts it out.
Lighting it takes `World.KindleSeconds` (`SaveState.TorchKindling`).
Swings share the axe cooldown and flare the light (`World.TorchFlare`).
They put the torch out after `World.WildSwings` within `World.WildWindow` seconds.

While the torch burns and fewer than `World.PackSize` wolves are near:

- an unscared wolf creeps in at a third of its speed and can bite;
- a swing aimed within about 45° of a wolf in `World.ScareRange` sets `Wolf.Scared`;
- a scared wolf keeps its bearing at `World.StalkDistance` without biting or leaping.

Once the pack is complete, swings no longer scare and the wolves attack.
A direct torch hit sets `Wolf.Flee`.

#### Mauling and wolf meat

At half health or less, an unprotected player can be pinned by a leaping wolf, which knocks them onto their back and stands over them.
The pinned state (`SaveState.Mauled`) blocks movement and actions and drains health while the pack feeds.
The death that follows strips food from the recovery box, as does a death with a hunting wolf within `LeapRange`, such as one bitten down behind a torch.

Killed wolves leave a `wolf_corpse` resource that yields raw meat, which the stone furnace cooks into edible meat.

### Resources

- Oaks drop a patch of leaves beside the trunk (`World.LeafPatch`), refilled every dawn.
- Loose stones and sticks (`loose_stone`, `stick`) sit on the baked scenery spots `Scenery.Loose` picks; `World.ScatterLoose` lays them when a world is made, and the view dresses the remaining spots with plants. Each gives 1 stone or 1 wood and does not come back.
- `World.Chip` drops a loose piece (`loose_stone`, `iron_chip`, `loose_coal`, `tin_chip`) within `GatherRules.ChipReach` of a boulder or deposit on `GatherRules.ChipChance` of pickaxe hits, a player's or a worker's; the view flies it there from the rock.
- A felled tree is taken out of the world and its stump (`stump`, `World.StumpWood`) put in its place by `World.Fell`, as a thing of its own with a new id that keeps the tree's id in `Thing.OfTree`.
- Plain bushes (`bush`, `World.BushLeaves`) grow on the scenery spots `Scenery.Bush` picks (`World.GrowBushes`), away from the clearing and the mountain feet.
- A stump or a bush is dug up whole (`GatherShape.Dug`, `Thing.IsDug`): digging adds to `Thing.Progress` at the shovel's speed until `GatherRules.DigSeconds`, then `World.Unearth` moves all it holds to the digger and removes it with a `dig_up` sound. The player's backpack must have room for all of it (`World.GatherBlock`), and a worker's hands are sized for it.
- Stumps and bushes never block building (`World.BuiltOver`): a new building, yard or crate clears away those on the ground it keeps clear of other resources (`World.ClearGround`).
- A berry bush holds `World.BushBerries` at the start and each time it fruits again, which is `BerryRegrowHours` after the first berry was picked from it, picked clean or not.
- Tin sits at the foot of a third cliff further inland. Like iron, it needs a pickaxe.
- Tin ore smelts into tin bars at the stone furnace, and a tin bar makes 4 cans at the bench.
- The stone furnace burns 2 wood into 1 charcoal.

### Tools

Tools carry a `Tool` kind (axe, pickaxe, shovel, hammer, chisel, rake, mortar), a `Speed` and a `Tier` in `GameTables`:

| `Tier` | Material |
| --- | --- |
| 0 | wood or stone |
| 2 | steel |

Tier 1 is unused, since there are no iron tools.

`World.BestTool` picks the fastest carried tool at or above a tier.
Recipes name the tool they need, the lowest `Tier` that will do, and whether they can be made by hand (`Hand`).
So a steel chisel does everything a stone chisel does, faster, while tin cans need a steel hammer and the mortar a steel chisel.

`GatherRules` holds every resource's rule in one table: the tool it needs (an axe for trees, a pickaxe for boulders and deposits, a shovel for stumps and bushes), one that only speeds gathering up (a rake for leaves, which take three times as long by hand), the loose piece a pickaxe chips off it, and the job it belongs to (`GatherRule.JobName`).
A job is every spot yielding the same item unless a rule names its own: stumps go with trees and sticks as wood, while bushes are apart from leaf patches.
Stone, leaves, berries and loose pieces need no tool.
The world's things are also kept by kind (`ThingList.Of`), so a search for one kind, such as tool stations, stores or campfires, and the tick, which runs only kinds that change by themselves, pass over trees and rocks.
Searches by place, such as what a player sees and room to build, still look at every thing.
Tool stations report changes to what they hold and to their filters (`Inventory.Changed`), so the world works out which tool kinds they give out only after a change, and a waiting worker checks that rather than every station.

Every tool and its parts stay reachable, which `CheckEverythingIsReachableFromFreshStart` verifies:

- a chisel is always 2 stone away;
- a mallet is a wood part;
- neither is sellable;
- the first pickaxe or a lost axe is bought with coins from berry crates.

### Crafting and stations

Hand recipes queue in `PlayerState.Crafting`, one `Queued` entry per recipe with its count, and take their inputs at once.
The first one waiting moves to `PlayerState.Making` when work on it starts and can no longer be cancelled; the rest give their materials back when taken off.
They run at base speed times the tool's speed while the player carries it, and only while the player has stood still for `World.CraftSettle` (`PlayerState.Still`).
The queue is written by `QueueJson`, since the generic list writer allocates on every world update.

The bench runs at `BenchSpeed` times the speed of the tool in its own chisel or hammer slot (`Thing.Tools`), and like the anvil only while someone works it.

A station counts batches of one recipe at a time: `Thing.Asked` is the recipe a player queued last and `Thing.Batches` how many of it still wait; a batch leaves the count as it starts.
`Thing.Keep` is the recipe workers keep it making once those batches are done, and a building's `Recipe` in `GameTables` names the one it starts with.
`Thing.Making` is the batch in progress, as `PlayerState.Making` is by hand.
`Thing.Recipe` is worked out, not stored: the batch in progress, else `Asked` while batches of it wait, else `Keep`; with none the station stands idle.
`World.Selected` is the recipe the station is on, whose slots its page draws and whose card is outlined: `Recipe`, else `Asked`, else the first recipe on its board.
A station is always on one: taking the mark off with no batches waiting leaves the marked recipe in `Asked`.
So a station with nothing to make keeps showing its slots, and more batches of that recipe are not a turn to another.
Saves and world updates leave out an empty mark, count and batch, so a tree or a rock carries none of them.
`World.QueueBatch` adds to the count, and for a recipe other than the selected one it first turns the station to it:
it drops the batches that waited and moves everything in `Thing.Inventory` and the fuel slot (`Thing.Burner`) to the acting player's backpack.
It always puts the mark on the recipe queued.
`World.SwitchBlock` refuses that turn while a batch is in progress or while the backpack has no room for all of it (`Inventory.Fits`).
So batches only wait for the recipe in progress when there is one, and `World.Deserialize` refuses a save where they wait for another.
`World.Unqueue` takes waiting batches off the count.
`World.SetRecipe` moves the mark, and a move to a recipe other than the selected one turns the station to it and drops the batches that waited; `World.MarkBlock` refuses that move while a batch is in progress or `Thing.Inventory` holds anything.
So a station with a mark is on its marked recipe, and `World.Deserialize` refuses a save where batches wait for another.
A station takes inputs for the selected recipe and the kept one, and haulers carry out only what neither takes.
A station starts no batch while its slots hold something that is neither such an input nor what its recipe makes (`World.Blocked`), as when a hauler delivers after the station has turned.
What no storage takes stays in the station, which goes on working until its output stack is full.
`World.SourceFor` gives out those leftovers too, so a hauler fetches an input from storage or straight from the station that made it.
Every station page shows the same `RecipeBoard` as the crafting menu; only the crafting menu has a `QueueStrip` under the work.
The board asks `World.SwitchBlock` before it queues at a station and `World.MarkBlock` before it marks, and when the answer is a refusal it shakes the slots of the station's `ProcessRow` (`ProcessRow.Shake`).
A click on a station's card marks at once, also as the first press of a double-click: queueing puts the mark on the recipe whatever that press did.
A mark the station refuses is sent and shown only after `RecipeBoard.DoubleClickSeconds`, the time a double-click's second press may take, since a double-click empties the slots in the way.
The board lists recipe cards under a heading per `RecipeGroup`; `PageMemory.ClosedGroups` keeps the groups the player closed, for every board.
Its list scrolls by itself: the board hands it to `SidePanel.ShowPageRows`, and `SidePanel.Fit` gives it the room the rest of the page leaves.
The panel keeps the list's place while the page draws again, so a click on a recipe leaves the list where it was scrolled to.
A card names the tool its recipe lacks when `World.ToolInUse` finds none slotted or carried, and `RecipePopup` says where the tool is.
A station's tool slots are drawn in `SidePanel.Aside`, a window of its own that follows the panel (`StationPanel.ToolWindow`).

`RecipeMap` covers the screen while the game goes on under it; it is drawn afresh on each `RecipeMap.Open`.
`PlayerControl.Update` moves nobody while it shows, and a `MapNode` turns to made before as soon as `World.Had` knows its item.
`RecipeMap.Place` puts an item on a grid of steps across and lines down: what is made out of it goes one step on, the first on the same line and the rest on lines below, and `MapTree` draws the lines between them.
A `MapNode` draws a filled slot for an item `World.Had` knows and a dashed one otherwise, with a chip for the tool its recipe requires and for each input after the first.
A click on a chip scrolls to that item and lights it (`MapNode.Light`); a click on a slot shows its details, whose button leaves the map through `SidePanel.ShowRecipe`.

Each recipe names its `Group` (`RecipeGroup`: tools, materials, containers or food), which the recipe boards list it under.
`SaveState.Obtained` holds every item the settlement has gathered, grown, bought or started with; with what it made, which is among `SaveState.Milestones`, those are the items the recipe map shows as known (`World.Had`).
Everything such an item is made of is known with it (`GameData.EndsUpIn`), so no ingredient looks unmade beside its product: a new settlement's steel axe and bottle make their whole chains known.
A save written before `Obtained` existed loads with it empty, so a raw resource it made nothing of shows as not made until it is gathered again.
`GameData.MadeFrom` gives the item another is made out of: its recipe's first input, or the first `From` of its entry in `GameData.Sources` when no recipe makes it.
`GameData.MapRows` names the map's rows and the items their chains start from, and `GameData.ValidateMap` checks that every item is on the map once.

#### Tool slots

`World.SlotKinds` names the tool slots a station has (`Thing.Tools`): a chisel and a hammer at the bench, a mortar at the cooking table (`cooktable`).
A recipe that needs a tool at such a station runs at the speed of the fastest of the slotted tool and those carried by players working there (`World.StationTool`).
Worked only by its crew, it runs with the slotted tool and waits when there is none.
At the cooking table it runs only while someone works it (`World.Worked(Thing)`).
The mortar's recipes and dough (`RecipeDef.Worked`) are worked, while jam and stew cook unattended and their worker only hauls.

#### Fuel

A recipe's `Fuels` list what it can burn, one unit per batch from the station's one-slot fuel box (`Thing.Burner`).

- The fuel slot holds one kind at a time, and the next batch burns it only while the recipe lists it (`World.FuelOf`).
- `World.FuelFits` takes what the station's recipe burns, or with no recipe what any of its recipes burns.
- `World.BatchCost` lists materials only.
- Haulers keep the burner topped up with what it holds, fill an empty one with the first listed fuel storage gives out, and carry a fuel the recipe cannot burn back to storage (`Worker.Fueling`).

The canning station (`cannery`) seals meat stew or jam and a `tin_can` into a canned good over fuel.

#### Anvil

An anvil only progresses while someone hammers it: a player standing within `World.WorkReach` (1 m) of its edge without walking or gathering (`World.AtStation`), or a paid assigned worker hammering at the station.
A smith stays with a running batch and fetches the next load once it is done.

#### Furnaces and steel

The stone furnace smelts ore over coal or charcoal, and cooks and makes charcoal over wood, coal or charcoal.
The reinforced furnace (`reinforced`) burns coal only.

Steel recipes consume iron bars and coal.
Steel only progresses while the heat is at or above the clear bar (`World.SteelHeat`, shown as 1450 °C through `World.Celsius`).
A batch below it pauses, keeps burning one coal from its fuel slot per batch length while cold, and resumes when reheated, with no failure.
Once an active crew has the forge hot, it never falls below the bar while they are on duty.

### Taking buildings down

`World.Dismantle` removes a building in reach and puts its cost, a supplied mine's materials and everything its boxes hold into the backpack, and the rest into a recovery box where it stood.
`World.DismantleBlock` keeps up a cabin someone shelters in and a trading post with a cart out.
The red trash can on `ThingPage`'s header turns into "Remove?" on the first click and dismantles on the second.

### Storage, filters and automation

Inventories are slot grids with stacks of 64.
Tools, packed crates and bottles take a slot each everywhere.
The layout is saved and synchronized: removing one stack never shifts unrelated slots.
Sort is the only action that packs and regroups stacks.
Item totals change only through `Inventory` methods, which keep them in line with the stacks.
Stacks are rebuilt from totals once, where the two can disagree: when a save loads, since one edited by hand can hold both.
A window's copy takes totals and stacks together from the server, so it does not rebuild.

The player moves items in and out of storage and stations within `World.ItemReach` (15 m, `World.Reaches`); gathering needs `World.Near`'s 3.2 m, and working a station and pumping need 1 m from its edge (`World.AtStation`).

Input rule for every item slot, in the backpack, storage and stations alike (`SlotInput`):

- LMB picks up the slot's stack onto the cursor, a held stack; RMB picks up half of it.
- With a stack held, LMB puts it down on the slot clicked and RMB puts down one; what does not fit stays held.
  On another item's slot in the same inventory the two swap; back on its own slot LMB lets go of it.
- A held stack lands by the rules of where it is put: each slot sends its own command, the one its Shift- or Ctrl-click sends, with the mark "from held stack" (`World.FromHeldStack`, `Command.Held`, `ItemMoves.PutDown`).
  A station's input slot sends `LoadInput`, a fuel slot `LoadFuel`, a mine's input slot `SupplyMine`, a tool slot `SlotTool`, a crate's contents `MoveInCrate`, and storage `TransferSlot` or `Transfer`.
  The server runs a marked command only when the held stack lies where the command takes from, and on no more than it holds.
  A slot that refuses shakes (`ItemSlot.Shake`), and the stack stays held.
- Shift-click moves the stack clicked to the other inventory open, and Ctrl-click all of that item.
- A double click uses an item (`PagePanel.UseFromBag`, `Hotbar`): eats, drinks, lights a torch, opens a crate as storage or selects a tool (`Hotbar.SelectTool`): on its hotbar slot, or in hand (`World.SelectInHand`, `PlayerState.InHand`) with no slot selected.
  On a slot with a use, a single click waits `SlotInput.DoubleClickMs` (200 ms) before it picks up, so the two can be told apart; recipe cards use the same time.
- A slot acts when the button comes up over it, as a plain button does, so a page that draws itself again never takes the slot away under a press; let go of anywhere else, it does nothing.
- A held stack put on a hotbar slot binds that exact backpack stack (`Inventory.Slot.Hotbar`, `World.BindHeld`), which the backpack rims; the binding goes with the stack and follows it into a stack it merges with (`Inventory.Slot.Empty`, `MoveSlotTo`, `Sort`).
  `PlayerState.HotbarSlots` holds plans, and items a slot waits for until `World.BindWaitingHotbars` binds the first stack of one to come in; `World.HotbarItem` and `HotbarStack` read a slot.
  A stack spent to nothing (`Inventory.Remove`, unlike a move out) notes its slot in `Inventory.UsedUp`, and the slot waits for its item again, so it binds the next stack at once.
  Ctrl batches from the backpack drawer and Store all carry the mark "spares only" (`World.FromSpares`, `Command.Spares`, `Inventory.SparesOnly`), which leaves bound stacks behind.
- Esc, a click on open ground or on the interface anywhere but an item slot, or closing the side panel lets go of the held stack (`HeldCursor`).
- A held crate or building plan over open ground goes into placement, and back to the cursor over the interface (`PlayerControl.PlaceHeld`).
- A held stack put on storage clicked in the world goes into it; an item only selected on the hotbar never does.
- Bulk buttons and recipe batch actions say what they move.

A held stack never leaves its slot until it is put down (`Inventory.Slot.Held`, `HeldBy`).
Totals still count it, but `Inventory.Remove`, `TransferTo` and `Has` take only units nobody holds, so other players, workers and haulers cannot take it, and a station holding one starts no batch (`World.Stocked`).
Another player who tries is told so (`World.HeldByAnother`).
The server keeps only the slot each player's held stack lies in (`PlayerState.HeldSlot`), and builds the `PlayerState.HeldStack` a window shows from it each time it sends the player (`World.ShareHeldStacks`).
It lets go of the stack when the player is down, walks out of reach of the building it lies in, or leaves, and when its crate is put down or sold; a load starts with nothing held.

A worker's hands (`Worker.Bag`) work like a crate: `Worker.BagStack` (12) items in all, one stack or up to twelve kinds, for every load.
The one tool a worker holds lies in `Worker.ToolSlot`, which `Worker.Tool` names.
Both are boxes a player in reach of the worker (`World.Reaches`) picks up from and puts into as with a building's slots: anything goes into the hands, only a tool into the tool slot.

#### Chests

Chests have separate item allowlists for input and output, and empty lists allow all items.
Automated deposits go to the nearest place that asks for the item, then to the nearest unrestricted storage (`World.DepositFor`).
Asking for it are a chest, pile or trading post whose input filter names it, a packing station packing it, a station whose recipe takes it as an input or burns it, a farm plot short of it for a sowing and a mine site short of it.
`World.Intake` gives each kind of place's one answer to which of its boxes takes a carried item now, and how many of it.
Automated withdrawals respect output filters.
Manual transfers ignore those filters.

#### Piles

Piles (`pile`) are 3×3 yards (`World.IsZone`, `ZoneSize`) that do not block walking.
`World.Snap` puts a new yard on the grid of the nearest yard within reach, and `ZoneBlock` lets yards touch but not overlap.

A pile takes part in automation like a chest.
`World.PileFilters` sets its input and output filters each tick from what it holds: its one resource, every fill of a vessel, or nothing while empty.
`HoldBlock` refuses a second resource, including for manual transfers.

#### Compost

A compost pile (`Thing.Compost`) instead takes `Compostable` items (leaves, grass and anything with `food`) and gives `fertilizer`.
It turns `CompostBatch` of them into one fertilizer each in-game hour.
It keeps the player's filters rather than setting its own, and `Thing.SkipFood` keeps workers from delivering food to it.
`DepositFor` ranks compost last for food, so harvests and berries go to storage first.

#### Crates

A wood crate holds `CrateCapacity` (12) units.
A picked-up crate becomes an item `crate_<n>` whose contents live in `SaveState.Crates`; an empty one is the stackable item `crate`.
`World.MoveInCrate` moves items between a packed crate in the backpack and the backpack, and renames the crate after what it then holds under the same number; emptied, it becomes a plain `crate`.
A crate of one good carries the good's id in its name, `crate_<good>_<n>`, so filters can name the kind of crate (`World.CrateOf`, matched in `Inventory.Accepts` through `World.PackedKind`).
A pile of crates holds crates of one good.
Placed crates are manual storage only.

#### Packing station

A packing station (`packer`, `src/Packing.cs`) packs the good in `Thing.Packs` into crates, anything a crate holds but crates and coins.
Its inventory takes only that good and empty crates; `Thing.Output` holds the crate being packed.
While worked like an anvil, it moves one item every `PackSeconds` into that crate, opening an empty crate for the first.
A full crate, or one of another good after the choice changes, waits in the output.

- Haul carries that crate to `DepositFor` and the station's leftovers to storage, and brings the good and empty crates up to a stack of each (`World.PackerCarries`); a batch is one crate.
- `DepositFor` counts the station as storage for its good, ranked with explicit filters, while a stack's delivery still leaves a slot for empty crates (`World.PackerTakes`).
- A crate packed full is a round of the Craft job.

Putting a packed crate into a placed crate asks for confirmation.
If confirmed, it ends the run in a black hole with a reload or main-menu choice.

#### Tool station

A tool station (`toolstation`) is storage for tools only: `World.ToolSlots` (4) slots, each taking any tool, and its rack hangs whatever each slot holds.

### Workers

Workers are tracked individually.
`HireOffers` in `GameTables` lists hiring deals, and each worker keeps the daily wage of the deal they were hired on (`Worker.Wage`).

#### Jobs

The kinds of work are `JobKind`, each with an id in saves and commands (`JobKinds.Id`), and one table holds each job's title, hint, whether a round of it is a unit of the place's work, the tools it takes and what it does (`World.Def`, `JobDef`).
A place offers some of them (`World.Jobs`), such as Craft and Haul at a crafting bench.

- Stock brings tools from storage to a tool station.
- Tend fetches what a compost pile's filters take from storage and carries its fertilizer out.
- Deliveries runs price errands and carries the trading post's mail, other than coins, to storage.

A worker's list (`Worker.Jobs`) holds entries in order: a job at one place (`Duty`), or a haul group (`HaulGroup`) holding hauls.
`Worker.Duties` goes through every job in it, the hauls of each group included, without allocating.
`World.SetDuty` adds a job at the end of the list or drops it wherever it is, and `World.HasDuty` asks for one.

`World.WorkJobs` reads the list from the top each time: the first entry with something to do is kept to (`Worker.InHand`) for one round, then the list is read again.
The job in hand is `Worker.Assignment` and `Worker.Doing`.

- a delivery ends the round of a job that carries (`World.Deliver`);
- a crafted batch, a furnace batch at the bellows, a crate packed full, a piece mined, a harvest and a mine building stage end it for the jobs the table marks, counted by the place (`Thing.Rounds`, `Worker.Mark`).

A job with nothing to do sets the status to what it lacks.
With none to do the worker waits at the nearest trading post (`World.Idle`), shows the first job's status and reads the list again every `JobListSeconds`, or at once when the list changes.
A tool none of their jobs uses goes back to a tool station first (`World.JobUses`).

`World.MoveJob` moves an entry from one `JobSpot` to another: a place in the list, or a place among a group's hauls.
Only a haul goes into a group, and a group moves with its hauls.
`World.AddHaulGroup` adds an empty group, and `RemoveHaulGroup` puts its hauls in its place as jobs of their own.
A haul group counts as one job, and its next carry is picked across all its places.
A command names the entry it moves (`World.JobKey`), so a move sent before the list changed is dropped rather than moving another entry.

On a worker's page `JobList` draws the list and turns a row dragged by its `JobGrip` into a `MoveJob`; each row shows what its place is set to make (`JobList.Made`).

#### Hauling

A haul job carries one load at a time (`World.Haul`).
Each kind of place offers its carries (`World.StationCarries`, `PackerCarries`, `FarmCarries`, `MineCarries`), and the best is the hauler's plan (`Worker.Plan`, a `Carry`) until the load is picked up:

1. an input or the fuel slot holding less than one batch, and fuel the recipe does not burn, which leaves the slot;
2. an output that is full, or anything another recipe left in the slots;
3. the input or fuel covering the fewest batches;
4. the output holding the most.

Inputs and fuel fill to a stack (`Inventory.SlotRoom`); a farm plot's slots hold one sowing (`World.SowRoom`) and a mine site what it lacks.
A load of an item the recipe takes both as an input and as fuel is shared between the two slots as a batch uses them (`World.FuelSplit`).
Spare seed in a plot's output that no storage takes stacks up in the plot's own seed slot.

#### Gathering and the bellows

A gatherer works at the player's pace, `World.GatherYield` pieces a cycle, until their hands are full (`Worker.Loading`), then delivers.
A gatherer whose spot is spent moves the duty to the nearest spot of the same job in `SaveState.Remembered` that still holds something (`World.NextSpot`): seen by any player, even if now under fog, never under black fog.
Spots that need a tool count only while the worker can get one (`World.CanWork`), and loose pieces come only after every other spot; a gatherer with no tool to be had for their spot goes to the nearest spot they can work at once (`World.OtherSpot`).
A woodcutter who fells a tree takes its stump as their spot while a shovel is to be had, without waiting for a player to see it; other stumps count once a player has seen them.
A worker carries one tool, so going between a tree and a stump takes a walk to a tool station to swap.
With none known they say so once, carry off what they hold and go on to their other jobs.
A worker with no duties waits at the nearest trading post.

Only one worker ever pumps a reinforced furnace: the bellows job has nothing to do while another worker has it in hand (`World.Pump`).
It also has nothing to do while the furnace neither smelts nor holds a batch to smelt, so a worker with both jobs hauls then.

#### Worker tools

A worker without the tool a job needs walks to the nearest station holding one, takes the best (`Worker.Tool`), and gathers at that tool's speed.
With none available they wait, show what they need and say it once in a message at the top of the screen.

`World.ReturnTool` walks a tool the job in hand has no use for, or any tool at dusk, back to the nearest station with room.
With no station the worker keeps it.
A dismissed worker's tool goes into their recovery box.

#### Schedule

At the `LunchHour` in `GameData` workers break for one in-game hour.
They sit in a circle around the campfire closest to the whole crew (`World.LunchFire`): every other one cross-legged on the ground and the rest on a rock, or where they stand without one.

As dusk begins (`GameData.DuskStarts`, a tenth of a day before night) they walk to the nearest trading post, or the clearing without one.
Then they walk a cart's trip down the valley at walking pace while fading out, wait out of view for the rest of half a trade trip (`Worker.Commute`, `World.WalkLeg`), and sign out (`Worker.Home`).
At dawn they walk back the same way, fading in, and return to work; a runner off for prices walks the same way out and back.
Workers on these walks keep to their way rather than being pushed apart by the crew.

Assignments and carried items are kept throughout.
A crew at lunch or signed out does not hold the forge at the clear bar or hammer an anvil, and cannot run price errands.
An errand already under way finishes first.

### Trading and economy

Prices, wages and demand follow the rules in [ECONOMY.md](ECONOMY.md).

#### Market

The market accepts any packed crate.
`Market.Goods` lists every good the village trades with its price: a `Sells` entry for one it takes, a `Buys` lot for one it sells.
The game gathers them into `Market.Sells` and `Market.Buys` when it starts.
Goods outside `Market.Sells` are donated: they pay nothing and leave demand alone.
The returning cart names them.
A sale pays for every unit of its contents at the pre-sale price, plus the crates.

`Market.Buys` lists what the village sells and in what lot, as the [README price sheet](../README.md#price-sheet) shows.

#### Demand and prices

In `Market.Sells` each good's first `Steady` units sold keep the full price, and each crate's worth past them takes `Drop` off it.
A sale lowers the price only after it is paid.
A day restores what selling `Daily` units takes (`World.Demand`, which runs above 1 by the steady amount).
Buying costs a fixed 1.25× the base price.

#### Carts and coins

A cart trip takes 30 seconds.
Purchases settle on return into the trading post's market output (`Thing.Output`), with overflow in a box beside it.

The trading post's input filter names the crates it asks for.
`DepositFor` treats a post as storage only for an item its filter names, so workers deliver those crates there when it is the nearest storage asking for them.
With `Thing.AutoSell` set, the cart leaves on its own once every market slot holds a crate and a cart is free.
Sale coins land in the trading post's wage slot (`Thing.Wages`, coins only), whose coin stack (`Inventory.CoinStack`) is `Inventory.UnlimitedCoins`, so the bank has no limit.
Dawn wages, settled strikes and hiring payments draw on every wage slot before the payer's backpack; purchases come from the backpack only.

#### Price sheet

The price sheet shows `SaveState.Prices`, a snapshot of sale prices.
It is taken when a cart returns, or when a worker on the trading post's deliveries job comes back from a price errand (`Worker.Away`).
Trades always use the live price, so the sheet can be out of date.

### Mines

A mine (`src/Mine.cs`, `Mine` in `GameTables`) is marked free and snaps to `World.MineSpot`, in front of the middle of an ore cliff's face between its two deposits, one per cliff.
Each portal follows its own cliff face, its tunnel runs into the rock, and its rails point outward toward the valley.
A continuous outcrop extends from the mountain foot, blocks walking with a matching footprint (`World.InSpur`), and holds the tunnel, which `WorldModels.Mine` darkens step by step to black.

`Thing.MineStage` counts finished stages from `MineStage.Marked`:

| Stage | Done by | Check |
| --- | --- | --- |
| Dug | a worker with a pickaxe | `MineStage.Dug` |
| Supplied | `Mine.Materials` delivered | `MineStage.Supplied` |
| Fitted | a worker with a hammer | `MineStage.Open` |

The two worker stages take `StageWorkdays` of `GameData.WorkdaySeconds`.
The player holding a gather on the mine (`World.Gather`, standing at `World.MineDoor`) adds to the same progress with a pickaxe or hammer carried (`World.BuildMine`), swinging the pickaxe or hammering (`ActorAnimation.PlayerAction`).
Workers fetch the pickaxe or hammer a stage needs from a tool station, like any other job.

Up to `Mine.Workers` miners work underground (`WorkerWorkState.Underground`, hidden by the view).
Each adds one ore and one stone to the mine's two-slot `Thing.Output` every `Mine.Seconds`.
Mining stops while either slot holds a full stack.
A miner then carries a load of it to the nearest place that takes it (`World.Dig`), and with none the mine reports once that it is full and the mine job has nothing to do.

### Farming

A farm plot (`farm`, `src/Farm.cs`) is a zone like a pile and snaps with piles.
`Farm` in `GameTables` sets what sows it, how many days the wheat grows, how many working days the harvest takes and what it yields.

`Thing.FarmStage` is `Fallow`, `Growing` or `Ripe`.
`Thing.Progress` counts growth seconds, then harvest work.
A fallow plot sows itself from its own inventory.
`World.CropLook` gives the look: bare, soil, straws, green plants or wheat.

The player harvests through `World.Gather` and assigned workers through `World.Reap`, both adding to the one progress.
The yield goes into the plot's `Thing.Output`; nothing moves seed back into the plot's input slots on its own.

- A worker on Farm harvests.
- A worker on Haul keeps the input slots at one sowing, from the plot's own output first and then from storage, and carries out the rest (`World.FarmCarries`).

## Intro film

The game opens with the film (`IntroMovie`): it plays `assets/intro.ogv` over the start menu, quiets the game's music
and takes every key and click until it ends or Esc skips it.
Review runs and checks open without it.

The movie is made in two steps: `./tools/intro_film.py` has the game draw the frames and write the cue sheet, and a
movie
tool outside this project turns them into `assets/intro.ogv` (see the `intro-film` skill).

- The film is a list of shots in `WorldView.IntroShots`, each a method that poses everything for a moment of its own
  clock.
  The clock moves one frame at a time, so frames are evenly spaced however long each takes to draw and save.
- It reuses the game's own models: the player's rig, the pack, flask, axe and torch, the trees, the valley with its
  shore and sea, and the fire pit. Walking and running go through the game's gaits.
- New for the film: the office, the rented room and the train car (`IntroSets`), a baseball bat, the boss
  (`ActorSkin.Boss`), and the poses in `IntroPoses`.
- The camera is the game's own, switched to perspective and placed per shot at eye level. A dome around it draws the
  sky,
  since the game's sky is one flat colour that only a view from above hides.
- Indoor shots hide the valley and light their set with the sun as a key light. Valley shots run the game's own
  lighting,
  wind and waves at a set time of day, with fog of war lifted, every tree shown and none faded.
- Sounds are not played while drawing: each is written to a cue sheet (`cues.tsv`) as its moment, its name and its
  level,
  then settings. A game sound goes by its name in `assets/sounds`, and music or one of the film's own recordings by its
  folder and name under `assets`, such as `intro/boss`. A cue's `from` and `length` take a part of a file, `far=1` makes
  it distant, `rate` plays it faster or slower, and a `stop` cue fades out the music track that is playing.
- A hand goes to a thing through `Reaching`, which bends an arm of a pose so its fist lands on a point: packing at home
  and touching the sea use it over the game's crafting squat.
- A pose's elbow folds only forward. `Folding` also turns the upper arm about its own length (`ActorRig.RollArm`), so a
  forearm can lie across the chest: by the fire his arms are crossed, each palm on the other shoulder.

## Sound and music

### Sound effects

Sound effects are one-shot MP3s in `assets/sounds`; `x_1.mp3`, `x_2.mp3` and so on are variants of one sound `x`.
Each play picks a variant other than the previous one with a small random pitch and volume change.

A sound with a world position is heard by its distance from the player, whatever the zoom.
It plays at full volume at the player and fades with the square of the distance left to its reach, where it is silent: half way there it plays at a quarter.
Its reach (`Sounds.ReachOf`) is:

- 16 metres for most sounds;
- twice `WolfSightRange` for the growl of a wolf breaking into its sprint, so it plays at a quarter from where the sprint starts;
- `HowlRadius` for a howl.

A sound with no position, such as the local player's footsteps or a hotbar click, plays at full volume.
A sound within a metre of the player plays with no place too, so a long one such as the stomach's growl stays centred while the player walks on.

Sounds come from three places:

- The simulation names discrete events through `World.Sound` (a fire catching, a wolf bite, a cart's payment) and stays silent while sleep fast-forwards.
- The view plays sounds tied to animation timing: footsteps by ground type, axe, pickaxe, anvil and bench impacts, a shovel going into the soil and throwing it aside, rail clinks while fitting a mine, rustles of hand picking and packing, butchering, the mortar, blows landing on wolves, bellows pumps, storage lids, the cabin door and a felled tree.
- `Net.Do` gives a player's inventory moves and spending a sound by item kind, sent to that player, when the action made none of its own.

A tree felled in sight takes 1 second to reach the ground when it is among the shortest and 2.1 when among the tallest (`WorldView.FallSeconds`).
It is heard twice, each sound started by the animation so both land on their moment whatever the tree's height:

- As it starts to go: `tree_crack`, about a second of cracking or less, or for the tallest three in ten `tree_bend`, 1.8 seconds of wood tearing that lasts about as long as their fall.
- As the trunk reaches the ground: `tree_crash`, the boom and the crown settling.

Both play lower and slower the taller the tree, from a tenth above their recorded pitch to a tenth below (`Sounds.Play`'s `pitch`).

### Music

Music in `assets/music` crossfades over four seconds between the day music and the night music when night falls or dawn breaks, including a sleep that skips to morning.
The day plays its tracks one after another and the night loops one.

Each keeps its place for the running session, so music longer than its part of the day carries on next time.
A track that plays to its end hands over to the next, and the last wraps back to the first.
The options screen sets the music volume, stored in the settings file.

## World map

The world is a fixed 216-tile valley defined in `src/Terrain.cs`:

- sea past the coast;
- mountain ranges beyond a valley half-width that narrows inland;
- a lake whose shore function bounds both walking and the drawn water.

`World.Scatter` places resources with a seeded generator, trees more densely toward the mountains.
`World.Deserialize` also refuses, rather than repairs, a save that is incomplete, names unknown items, marks or counts batches of a recipe at a station that does not make it, or holds player needs out of range.
Workers use grid detours around water and buildings.

## Development and verification

[AGENTS.md](../AGENTS.md#checks) lists the checks and [RELEASE.md](RELEASE.md) the development flow.
The network check's guest joins through a relay in `build.py` that adds about 80 ms of delay and 5% packet loss each way.

### Platform

The project targets Linux x86_64 and Godot's Forward+ (Vulkan) renderer, whose device selection prefers a discrete GPU.
First-time setup requires internet access; interactive play requires a supported graphics driver.
The export is a runnable prototype.
