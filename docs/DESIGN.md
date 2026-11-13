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
- **Mining:** coal, iron and tin deposits need a carried pickaxe; loose pieces need no tool. A pickaxe hit on rock or ore chips a loose piece off one time in twenty, so loose iron exists only from mining.
- **Fuel:** iron and tin smelt over coal or charcoal; wood only cooks food and makes charcoal. Charcoal cannot substitute for coal in steel.
- **Steel:** needs iron, coal, a reinforced furnace and active bellows heat. Two assigned workers divide bellows and hauling.
- **Worker tools:** workers take the tools their jobs need (axe for trees, pickaxe for deposits and mines, hammer for fitting a mine) from a tool station and put them back when reassigned or going home; a rake for leaves and a pickaxe for rock only speed them up. Without a needed tool they pick up loose pieces of the same resource, or wait and say so once.
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
| `src/GatherRules.cs` | One table of how each resource is gathered: its item, the tool it needs or that helps, the by-hand factor and its chip |
| `src/Loose.cs` | Loose stones, sticks and the chips a pickaxe knocks off rock |
| `src/Scenery.cs` | The baked scenery spots, the ground they may stand on, and which are loose pieces |
| `src/Packing.cs` | Packing stations |
| `src/Terrain.cs` | The fixed valley map |
| `src/Recovery.cs` | Skipping to dawn while downed |
| `src/Water.cs` | The refillable three-sip bottle and drinking at a water source |
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
| `src/Hud.cs` | The top-right corner with the clock plate and frame rate counter, and the mode, full-bottle and death labels |
| `src/MessageStack.cs` | The fading message stack at the top of the screen |
| `src/RecoveryPrompt.cs` | The "Skip to morning?" prompt |
| `src/MinePanel.cs` | The mine's building steps, miners and outputs |
| `src/Style.cs` | The shared enamel camp-gear look (speckled enamel boards with a cream rim, beige wells, cream cups, brick for what is active or wanted, Fredoka and Nunito) and the heat, day and cart-road dials |
| `src/SidePanel.cs` | The side panel: opening and closing its pages, selecting what it inspects, its size, and `PageMemory`, what outlasts a page |
| `src/BackpackDrawer.cs` | The backpack drawer above the hotbar |
| `src/InspectPage.cs` | Picks the page for the selected thing or worker, and `ThingPage`, the base of a page about one thing |
| `src/PagePanel.cs` | `PagePanel`, the base of every page |
| `src/ProcessRow.cs` | The process row every station shares and the fuel row under its bar |
| `src/JobRow.cs` | A place's job buttons, their worker lists and what each job is called |
| `src/FilterWindow.cs` | The filter window beside the panel |
| `src/ItemMoves.cs` | What clicking or dragging an item does: reach, refusals and the crate-in-crate warning |
| `src/Grid.cs` | Draws the icon-cell grids |
| `src/ItemSlot.cs` | The cream item cup and drag and drop between grids |
| `src/StationPanel.cs` | A station's page: recipe board, process row, bellows and jobs, with its tool slots in a window beside it |
| `src/Cups.cs` | `Cups`: a building's item cups and a station's process row |
| `src/CraftPanel.cs` | The crafting menu |
| `src/RecipeBoard.cs` | The recipe board the crafting menu and every station show, a recipe's hover popup, and the crafting menu's queue strip under the work |
| `src/RecipeMap.cs` | The recipe map: a row of item chains for each category, the station strip, chips that go to their item and the details strip |
| `src/PageText.cs` | How panels word durations and costs |
| `src/BlackHole.cs` | The crate-in-crate warning, collapse shader and game over screen |
| `src/MarketPanel.cs` | The trading post market, hiring offers and price sheet |
| `src/CrewPanel.cs` | The crew cards and a worker's own panel |
| `src/StageTrack.cs` | Stages on a line, for a farm plot and a mine |
| `src/FarmPanel.cs` | A farm plot's stages and process row |
| `src/CabinPanel.cs` | A cabin: its door and sleep |
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
| `src/EnvironmentPlacement.cs`, `src/GroundDressing.cs` | Grass, flowers and shrubs from the baked placement, cleared around buildings and paths |
| `src/WorldLook.cs`, `src/Wind.cs` | Worn footpaths, the sound listener and the wind |
| `src/LakeAppearance.cs`, `src/ShorelineAppearance.cs` | The lake surface and wet banks |
| `src/WorldModels.cs`, `src/ActorRig.cs`, `src/ModelKit.cs`, `src/*Shape.cs` | Build the models (see [assets/README.md](../assets/README.md)) |
| `src/ActorAnimation.cs` | Picks actor actions and gives their keyframed poses |
| `src/WolfModel.cs` | Poses the wolf: its gaits, leap, howl, recoil and fall |
| `src/IconRender.cs` | Renders item and building icons from the models (`./build.py icons`) |
| `src/Sounds.cs` | Plays sound effects |

Checks and review tools:

| File | Owns |
| --- | --- |
| `tests/*Checks.cs` | Simulation checks run by `./build.py check` (`SELF-TEST PASS`) |
| `tests/*Steps.cs` | Steps shared by the simulation checks |
| `src/GameChecks.cs`, `src/SurvivalUIChecks.cs` | Input/UI checks through real input events (`UI-TEST PASS`) |
| `src/NetChecks.cs` | The host and guest network checks (`NET-HOST PASS`, `NET-GUEST PASS`) |
| `src/ServerChecks.cs` | One window playing on the real server process (`SERVER-TEST PASS`) |
| `src/InputSteps.cs`, `src/PanelSteps.cs`, `src/SessionSteps.cs` | Steps shared by the UI and network checks |
| `src/Showcase.cs` | The review scenes behind `--showcase` |
| `src/ReviewDrive.cs` | Plays the window from a command file behind `--drive`, for the `playtest` skill |
| `src/PerformanceRun.cs`, `src/PerformanceFixture.cs`, `src/PerformanceStage.cs` | The rendered performance run: the window's samples, the camp it plays and the server's half |
| `src/AnimationCapture.cs` | Captures the animation review |
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
On a page about a building or an opened crate (`PagePanel.StoresFromHotbar`), the drawer's click (`BackpackDrawer.Store`) also runs for a mouse click on a hotbar slot; number keys still put the slot in hand.
The stacks that page takes are rimmed in brick and the rest are faded.
A close button hides the drawer until another page or building opens.
The side panel only shows the building and sizes itself to what it holds.

### Multiplayer

Every game runs on a `Server`, alone or together, which owns the only simulation and has no player of its own.
The window that starts a game runs it as a headless child process of the same executable (`--server`).
The server takes the first free port from 24565 itself when it starts serving, since a port a window picked could be gone by the time the server came up, and writes it to a file named for the window's process.
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

A player with something to make by hand squats over it, turning it in both hands (`ActorAction.Craft`), only while standing still.

The player's carried tool selects its matching action and the tool shown in hand.
Workers animate according to movement and an explicit transient `WorkerWorkState`, not localized or presentation status text.
The transient state is excluded from saves and recalculated on the next simulation tick.

At a packing station the packer stands behind the table (`World.PackStand`) and the `Pack` action rummages in the heap of goods, lifts a piece across and settles it into the crate, then turns back.
The player working a station they stand near packs the same way.

At a cooking table, which is only worked for the mortar's recipes, the `Grind` action holds the mortar in hand (`ActorAnimation.StationAction`).
The left fist holds the bowl out in front, and the right takes the pestle twice round the bowl's floor, then lifts it and pounds.
Both arms are solved from where the hands have to be (`ActorAnimation.ArmTo`), so the pestle stays over the bowl.
`ActorRig` turns the bowl and pestle against their forearms in the resting pose (`ActorAnimation.GrindRest`), which keeps the bowl level and the pestle leaning toward its hand.
While someone grinds, the table's own mortar is off the counter (`WorldView.grinding`).

Manual bellows pumping and the unarmed punch trigger one loop of their action.
Each striking work action has an impact moment that shakes its target and spawns chips, leaves, splashes or sparks; packing and grinding strike nothing.
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
2. Fell trees with the starting steel axe; gather stone and berries by hand.
3. Knap a stone chisel, then carve planks and a mallet.
4. Build a crafting bench and make crates there.
5. Build a trading post and sell crates of berries for a steel pickaxe.
6. Mine iron and coal, and smelt iron in a stone furnace.
7. Build and operate a reinforced furnace (`reinforced`) to make steel.
8. Hammer steel into steel parts at the anvil and make steel tools.
9. Pack a steel tool into a crate, sell it through the trading post and use the proceeds to hire and assign a worker.

There are no iron tools; iron bars only go into buildings and steel.

#### Water

The player starts with one refillable bottle (`src/Water.cs`); the player-facing rules are in [README.md](../README.md#food-water-and-time).
Filling it or sipping straight from the source takes one `GatherSeconds`.
Every point on the pond shore is a collection target.
Recipes take only a full bottle, and cooking returns the empty bottle rather than destroying it.

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

- Items carry what eating or drinking one restores (`ItemDef.Hunger`, `Thirst`, `Health`) and a `Food` flag.
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

#### Death and recovery boxes

Death moves carried items to a recovery box and respawns the player after a 30-second timer in the cabin nearest to where they fell, or at the clearing without one.
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
Work out of sight is heard but shakes nothing and throws no chips.

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
- **Smell and howl.** A patrolling or called wolf within `SmellRadius` of a player out in the open takes them as its target and howls, standing still, for as long as the howl recording plays (`LoneHowlSeconds` or `PackHowlSeconds`, `Wolf.Howl` counting down `Wolf.HowlLength`).
  The night's first howl plays `wolf_howl`, a lone wolf, and later ones `wolf_howl_group` (`SaveState.Howled`).
  A howl fades with distance from the player, on screen too, and is silent beyond `HowlRadius`, so a far pack sounds far; like every placed sound it pans with its direction.
  The howl calls every patrolling wolf within `HowlRadius` to where it stood (`WolfMode.Called`, `Wolf.Call`); a called wolf still smells on its way and goes back to patrolling once there.
- **Approach.** After its howl a wolf trots at its player out of firelight until within `WolfSightRange`, then sprints (`Wolf.Chase` above zero) at `WolfChase` times `WalkSpeed` with one distant growl (`Wolf.Sprinted`), which data validation keeps above walking and below `SprintSpeed`.
- **Home.** From `WolfRetreatStarts`, `WolfRetreatHours` before dawn, every wolf drops its hunt and runs to the nearest den's door (`WolfMode.Home`), where it goes in; dawn clears any still out.
  A player a wolf had pinned goes free as it leaves.
  Patrolling, going home or losing its player (who sheltered or left the game) clears everything a wolf was doing to them (`World.Calm`).

Campfire light repels wolves, and `CampfireHours` sets how long one log burns.
A wolf already chasing the player still follows into the light for a few seconds.
Inside a cabin (`SaveState.Sheltered`) the player is hidden: their hunters give up at once and patrol on away from it.

#### Torch

A torch in hand (`SaveState.Torch`, burn seconds left) burns as long as one log, only while `SaveState.TorchLit`.
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
- A berry bush holds `World.BushBerries` at the start and each time it fruits again, which is `BerryRegrowHours` after the first berry was picked from it, picked clean or not.
- Tin sits at the foot of a third cliff further inland. Like iron, it needs a pickaxe.
- Tin ore smelts into tin bars at the stone furnace, and a tin bar makes 4 cans at the bench.
- The stone furnace burns 2 wood into 1 charcoal.

### Tools

Tools carry a `Tool` kind (axe, pickaxe, hammer, chisel, rake, mortar), a `Speed` and a `Tier` in `GameTables`:

| `Tier` | Material |
| --- | --- |
| 0 | wood or stone |
| 2 | steel |

Tier 1 is unused, since there are no iron tools.

`World.BestTool` picks the fastest carried tool at or above a tier.
Recipes name the tool they need, the lowest `Tier` that will do, and whether they can be made by hand (`Hand`).
So a steel chisel does everything a stone chisel does, faster, while tin cans need a steel hammer and the mortar a steel chisel.

`GatherRules` holds every resource's rule in one table: the tool it needs (an axe for trees, a pickaxe for deposits), one that only speeds gathering up (a pickaxe for rock, a rake for leaves, which take three times as long by hand), and the loose piece a pickaxe chips off it.
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
`World.Selected` is the recipe the station is on, whose cups its page draws and whose card is outlined: `Recipe`, else `Asked`, else the first recipe on its board.
A station is always on one: taking the mark off with no batches waiting leaves the marked recipe in `Asked`.
So a station with nothing to make keeps showing its cups, and more batches of that recipe are not a turn to another.
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
A station starts no batch while its cups hold something that is neither such an input nor what its recipe makes (`World.Blocked`), as when a hauler delivers after the station has turned.
What no storage takes stays in the station, which goes on working until its output stack is full.
`World.SourceFor` gives out those leftovers too, so a hauler fetches an input from storage or straight from the station that made it.
Every station page shows the same `RecipeBoard` as the crafting menu; only the crafting menu has a `QueueStrip` under the work.
The board asks `World.SwitchBlock` before it queues at a station and `World.MarkBlock` before it marks, and when the answer is a refusal it shakes the cups of the station's `ProcessRow` (`ProcessRow.Shake`).
A click on a station's card marks at once, also as the first press of a double-click: queueing puts the mark on the recipe whatever that press did.
A mark the station refuses is sent and shown only after `RecipeBoard.DoubleClickSeconds`, the time Godot on Linux allows a double-click's second press, since a double-click empties the cups in the way.
The board lists recipe cards under a heading per `RecipeGroup`; `PageMemory.ClosedGroups` keeps the groups the player closed, for every board.
Its list scrolls by itself: the board hands it to `SidePanel.ShowPageRows`, and `SidePanel.Fit` gives it the room the rest of the page leaves.
The panel keeps the list's place while the page draws again, so a click on a recipe leaves the list where it was scrolled to.
A card names the tool its recipe lacks when `World.ToolInUse` finds none slotted or carried, and `RecipePopup` says where the tool is.
A station's tool slots are drawn in `SidePanel.Aside`, a window of its own that follows the panel (`StationPanel.ToolWindow`).

`RecipeMap` covers the screen while the game goes on under it; it is drawn afresh on each `RecipeMap.Open`.
`PlayerControl.Update` moves nobody while it shows, and a `MapNode` turns to made before as soon as `World.Had` knows its item.
`RecipeMap.Place` puts an item on a grid of steps across and lines down: what is made out of it goes one step on, the first on the same line and the rest on lines below, and `MapTree` draws the lines between them.
A `MapNode` draws a filled cup for an item `World.Had` knows and a dashed one otherwise, with a chip for the tool its recipe requires and for each input after the first.
A click on a chip scrolls to that item and lights it (`MapNode.Light`); a click on a cup shows its details, whose button leaves the map through `SidePanel.ShowRecipe`.

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
The mortar's recipes are worked, while jam, stew and dough cook unattended and their worker only hauls.

#### Fuel

A recipe's `Fuels` list what it can burn, one unit per batch from the station's one-slot fuel box (`Thing.Burner`).

- The fuel slot holds one kind at a time, and the next batch burns it only while the recipe lists it (`World.FuelOf`).
- `World.FuelFits` takes what the station's recipe burns, or with no recipe what any of its recipes burns.
- `World.BatchCost` lists materials only.
- Haulers keep the burner topped up with what it holds, fill an empty one with the first listed fuel storage gives out, and carry a fuel the recipe cannot burn back to storage (`Worker.Fueling`).

The canning station (`cannery`) seals meat stew or jam and a `tin_can` into a canned good over fuel.

#### Anvil

An anvil only progresses while someone hammers it: a player standing within reach without walking or gathering, or a paid assigned worker hammering at the station.
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
The layout is saved and synchronized: removing one stack never shifts unrelated cells.
Sort is the only action that packs and regroups stacks.
Item totals change only through `Inventory` methods, which keep them in line with the stacks.
Stacks are rebuilt from totals once, where the two can disagree: when a save loads, since one edited by hand can hold both.
A window's copy takes totals and stacks together from the server, so it does not rebuild.

The player moves items in and out of storage and stations within `World.ItemReach` (15 m, `World.Reaches`); gathering, working a station and pumping need `World.Near`'s 3.2 m.

Input rule for every item cell, in storage and stations alike:

- A click moves exactly one item, in either direction.
- Shift-click moves the stack clicked, and Ctrl-click all of that item.
- Right-click picks up half where a panel supports dragging.
- A carried stack shows its count on the cursor: the cell's own pill when it carries the whole stack, otherwise how many it carries.
- Dragging carries a stack or a split portion to its exact destination, merging compatible stacks or swapping cells.
- Bulk buttons and recipe batch actions say what they move.

A worker's hands (`Worker.Bag`) work like a crate: `Worker.BagStack` (5) items in all, one stack or up to five kinds; `Worker.Hands` widens them for a full mine or market stack.

#### Chests

Chests have separate item allowlists for input and output, and empty lists allow all items.
Automated deposits prefer a chest with a matching input filter, or a packing station packing the item, then a nearby unrestricted chest.
Automated withdrawals respect output filters.
Manual transfers ignore those filters.

#### Piles

Piles (`pile`) are 3×3 yards (`World.IsZone`, `ZoneSize`) that do not block walking.
`World.Snap` puts a new yard on the grid of the nearest yard within reach, and `ZoneBlock` lets yards touch but not overlap.

A pile takes part in automation like a chest.
`World.PileFilters` sets its input and output filters each tick from what it holds: its one resource, or nothing while empty.
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

- Haul carries that crate to `DepositFor` and the station's leftovers to storage, then keeps a crate's worth of the good and two empty crates there.
- `DepositFor` counts the station as storage for its good, ranked with explicit filters, while a stack's delivery still leaves a slot for empty crates (`World.PackerTakes`).
- A worker with both jobs fetches only once the station runs dry, crates first.

Putting a packed crate into a placed crate asks for confirmation.
If confirmed, it ends the run in a black hole with a reload or main-menu choice.

#### Tool station

A tool station (`toolstation`) is storage for tools only: `World.ToolSlots` (4) slots, each taking any tool, and its rack hangs whatever each slot holds.

### Workers

Workers are tracked individually.
`HireOffers` in `GameTables` lists hiring deals, and each worker keeps the daily wage of the deal they were hired on (`Worker.Wage`).

#### Duties

Workers hold duties (`Worker.Duties`): a place and one of the jobs `World.Jobs` offers there, such as gather, craft, haul, bellows, farm, build, mine, deliveries, fuel, stock or tend.
`World.SetDuty` adds or drops one.
`Worker.Assignment` is the place being worked now.
`World.NextPlace` moves on to the worker's next place when the current one has nothing to do or after a delivery, so one worker can cover several places in turn.

- Stock brings tools from storage to a tool station.
- Tend fetches what a compost pile's filters take from storage and carries its fertilizer out.
- Deliveries runs price errands and carries the trading post's mail, other than coins, to storage.

A gatherer whose spot is spent moves the duty to the nearest spot yielding the same item in `SaveState.Remembered` that still holds something (`World.NextSpot`): seen by any player, even if now under fog, never under black fog.
Spots that need a tool count only while the worker can get one, and loose pieces come only after every other spot; a gatherer with no tool to be had goes to known loose pieces at once (`World.LooseSpot`).
With none known they say so once and go on to their other places, or wait at the nearest trading post until one is seen or the spot comes back.
A worker with no duties waits at the nearest trading post.

Only one worker ever pumps a reinforced furnace.
Of those with the bellows job, the first pumps and the rest haul if they also hold haul.
A lone worker with both alternates.

#### Worker tools

A worker without the tool a job needs walks to the nearest station holding one, takes the best (`Worker.Tool`), and gathers at that tool's speed.
With none available they wait, show what they need and say it once in a message at the top of the screen.

`World.ReturnTool` walks a tool the job no longer needs, or any tool at dusk, back to the nearest station with room.
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
Goods outside `Market.Sells` are donated: they pay nothing and leave demand alone.
The returning cart names them.
A sale pays for the contents unit by unit plus the crate.

`Market.Buys` lists what the village sells and in what lot, as the [README price sheet](../README.md#price-sheet) shows.

#### Demand and prices

In `Market.Sells` each good's first `Steady` units sell at the full price, and each crate's worth past them takes `Drop` off it.
A day restores what selling `Daily` units takes (`World.Demand`, which runs above 1 by the steady amount).
Buying costs a fixed 1.25× the base price.

#### Carts and coins

A cart trip takes 30 seconds.
Purchases settle on return into the trading post's market output (`Thing.Output`), with overflow in a box beside it.

The trading post's input filter names the crates it asks for.
`DepositFor` treats a post as storage only for an item its filter names, so workers deliver those crates there when it is the nearest storage asking for them.
With `Thing.AutoSell` set, the cart leaves on its own once every market slot holds a crate and a cart is free.
Sale coins fill the trading post's one-stack wage slot (`Thing.Wages`, coins only) first and the output after.
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
The first free miner then carries the whole stack to storage (`Worker.Bag` grows to the stack size until emptied), and with no storage the mine reports once that it is full.

### Farming

A farm plot (`farm`, `src/Farm.cs`) is a zone like a pile and snaps with piles.
`Farm` in `GameTables` sets what sows it, how many days the wheat grows, how many working days the harvest takes and what it yields.

`Thing.FarmStage` is `Fallow`, `Growing` or `Ripe`.
`Thing.Progress` counts growth seconds, then harvest work.
A fallow plot sows itself from its own inventory.
`World.CropLook` gives the look: bare, soil, straws, green plants or wheat.

The player harvests through `World.Gather` and assigned workers through `World.FarmWork`, both adding to the one progress.
The yield goes into the plot's `Thing.Output`; nothing moves seed back into the sowing inventory on its own.

- A worker on Farm harvests.
- A worker on Haul first moves seed from the output into the sowing inventory, then carries out the rest and fetches what the next sowing lacks.

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
  trail,
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

Audibility follows the camera, so zooming out lets the player hear further.
A sound on screen plays at full volume, one just past the screen edge is faint, and it fades to silence 5 tiles beyond the edge (14 for the growl of a wolf breaking into its sprint).

Sounds come from three places:

- The simulation names discrete events through `World.Sound` (a fire catching, a wolf bite, a cart's payment) and stays silent while sleep fast-forwards.
- The view plays sounds tied to animation timing: footsteps by ground type, axe, pickaxe, anvil and bench impacts, rail clinks while fitting a mine, rustles of hand picking and packing, butchering, the mortar, blows landing on wolves, bellows pumps, storage lids and the cabin door.
- `Net.Do` gives a player's inventory moves and spending a sound by item kind, sent to that player, when the action made none of its own.

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
