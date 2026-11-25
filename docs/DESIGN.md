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
- **Plumbing:** Pumps draw from the freshwater shallows. Each connected pipe holds one liter, including standing water
  after the pressure falls. Buildings with ten steel pipes in their Plumbing slot share water according to demand.
- **Building orientation:** Plans turn in quarter turns during placement. A mine faces the cliff it fits.
- **Worker tools:** workers take the tools their jobs need (axe for trees, pickaxe for deposits and mines, shovel for stumps and bushes, hammer for fitting a mine) from a tool station and put them back when reassigned or going home; a rake for leaves and a pickaxe for rock only speed them up. Without a needed tool they go to another spot of the job that they can work, loose pieces last, or wait and say so once.
- **Transfers:** automated transfers respect input and output allowlists of storage, where empty lets nothing through and `Inventory.Everything` lets all, and prefer storage whose input filter names the item. A pile's filter names one resource, set by the player or by the first resource put in. Manual transfers bypass automation filters.
- **Gravel paths:** a steel hammer at the bench crushes 1 stone into 1 gravel in 10 base seconds, and a path tile, one
  metre square, costs 4 gravel. Players, walking or sprinting, and workers move 1.2 times as fast where gravel is drawn,
  and workers keep their routes.
  Tiles join with round ends, strips between neighbours and a slanted strip between any two tiles corner to corner, so
  every corner is cut on the slant; a square of four is filled.
  A tile goes under buildings and buildings over tiles, but not on a tree, boulder, deposit, berry bush, leaf spot or
  wolf carcass.
  A tile cannot be clicked in normal play.
- **Taking down:** the red cross in the building plans gives the remove selector: a building or path tile in reach
  turns red under the mouse and one click takes it down, with no question. Right-click switches layers and Esc exits.
  Building pages have no take-down button.
- **Carried items:** never deleted when a worker is reassigned, fired, blocked or rerouted.
- **Survival:** food, water, day and night, wolves that fear light, sleeping in cabins or straw beds, wages, saves and recovery after death.
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
| `src/Inventory.cs` | Inventories and their filter-aware transfers, and the read-only views `World`'s read members hand out: `ItemTotals`, `ListView` and `InventoryView`, and `FilterList`, a filter's entries in the order named |
| `src/GameData.cs` | Time, pace and need values, the shapes of the game tables, and the check that they fit together |
| `src/GameTables.cs` | Items, buildings, recipes, hiring deals, the market, the mine, the farm and the goals |
| `src/buildings/Construction.cs` | Putting buildings and yards up and taking buildings down: where a plan snaps to, why it or a crate cannot go somewhere, what it costs and what comes back |
| `src/buildings/model/Removal.cs` | A removal target: a building, gravel path tile or steel pipe tile |
| `src/paths/GravelPaths.cs` | Gravel path tiles: where one may go, laying and taking one up, and how much faster walkers are on gravel |
| `src/paths/WalkSearch.cs` | Finds routes around water and building footprints |
| `src/pipes/SteelPipes.cs` | Steel pipe tiles beneath the surface, their shapes, rotations and shared connections |
| `src/pipes/model/PipeShape.cs`, `src/pipes/model/PipeTile.cs` | Straight, tee and cross shapes, and tile rotation |
| `src/pipes/Plumbing.cs` | Connected pipes, standing water and how buildings share and store the supply |
| `src/pipes/WaterPumps.cs` | Player and worker pump strokes, pressure loss and flow per simulation tick |
| `src/pipes/display/PipeLayerView.cs`, `src/pipes/display/PipeGround.cs` | Underground view and placement preview |
| `src/pipes/display/PipePanel.cs` | A laid pipe's standing water and pressure |
| `src/pipes/display/WaterPumpPanel.cs`, `src/pipes/display/PressureGauge.cs` | Pump control, job assignment and dial |
| `src/pipes/display/WaterPumpProp.cs` | The pump handle's movement in the world |
| `src/pipes/display/SteelPipeShape.cs`, `src/pipes/display/WaterPumpShape.cs` | Steel pipes, hand pump and lever |
| `src/pipes/display/InstalledPlumbingShape.cs` | Plumbing on buildings and placed barrels |
| `src/pipes/display/FarmSpray.cs` | Water from working sprinklers |
| `src/paths/GravelOutline.cs` | The outline gravel is drawn with and walked by, from the tiles laid |
| `src/farms/Farms.cs` | Farm plots, from sowing and watered growth to harvest |
| `src/farms/model/FarmStage.cs` | Whether a plot is fallow, growing or ripe |
| `src/mines/Mines.cs` | Mines: where one is marked and stands at its cliff, its three building stages, supplying it, working it underground and the end goal's working mines |
| `src/mines/model/Cliff.cs`, `src/mines/model/MineStage.cs` | `Cliff`, the valley's ore cliffs and the mine spot at each, and `MineStage`, how far a mine is built |
| `src/resources/GatherRules.cs` | One table of how each resource is gathered: its item, the tool it needs or that helps, the by-hand factor, its chip, whether it is dug up whole and its job |
| `src/resources/Loose.cs` | Loose stones, sticks and the chips a pickaxe knocks off rock |
| `src/digging/Digging.cs` | Stumps and bushes: what a felled tree leaves, the valley's bushes, digging either up and building over them |
| `src/digging/GroundHoles.cs`, `src/digging/display/HolePanel.cs` | Held shovel work, empty holes and the hole's input slot |
| `src/digging/Beach.cs`, `src/digging/display/CoastView.cs` | Beach sand, tide, and the exposed soil after a dig |
| `src/trees/TreePlanting.cs`, `src/trees/LeafSpots.cs`, `src/trees/display/TreePlantingShape.cs` | Tree planting and growth, leaf spots and their acorns, and the dig outline, hole and nub |
| `src/Scenery.cs` | The baked scenery spots, the ground they may stand on, and which are loose pieces or bushes |
| `src/packing/Packing.cs` | Packing stations |
| `src/Jobs.cs` | The jobs and their table, a worker's job list with its haul groups, and adding, moving and grouping its entries |
| `src/WorkerJobs.cs` | A worker's job list read top down, the round each job is done for, deliveries, and the bellows and fire jobs |
| `src/jobs/hauling/Hauling.cs`, `src/jobs/hauling/Carry.cs` | The Haul job: the best carry across a haul job's places, the carry in hand and what a hauler says |
| `src/jobs/hauling/PlaceHaul.cs` | The part of the Haul job each kind of place extends, and the four haul rules its carries come under |
| `src/jobs/hauling/TakeBack.cs` | Where a worker takes a load nothing else takes: the nearest building whose output filter lets it out |
| `src/stations/StationHaul.cs`, `src/farms/FarmHaul.cs`, `src/mines/MineHaul.cs`, `src/packing/PackerHaul.cs`, `src/storage/StoreHaul.cs` | What a crafting station, a farm plot, a mine site, a packing station, and a chest or placed crate offer a hauler |
| `src/jobs/gathering/Gathering.cs`, `src/water/WaterGathering.cs` | The Gather job at resource spots, and the lake's version of it, which fills bottles |
| `src/stations/model/ToolSlotRule.cs` | What one of a station's tool slots takes: a kind of tool at a tier or better |
| `src/stations/model/BatchFor.cs` | `BatchFor`, who a station's batch in progress was started for: none in progress, the mark or a player's queue |
| `src/stations/display/SlottedTool.cs` | A tool lying in a station's slot, at the bench and the anvil, showing the grade slotted |
| `src/jobs/crafting/Crafting.cs`, `src/packing/PackCraft.cs` | The Craft job at a station, and the packing station's version of it, which packs crates |
| `src/terrain/Terrain.cs` | The fixed valley map, with the beds under the lake and the sea |
| `src/Recovery.cs` | Skipping to dawn while downed |
| `src/Water.cs` | The refillable three-liter bottle and drinking at a water source |
| `src/containers/ContainerItem.cs` | The base item shared by every fill of a steel bucket or barrel |
| `src/containers/barrel/Barrels.cs`, `src/containers/barrel/model/Barrel.cs` | Portable and placed steel barrels, with a fill encoded in each portable barrel |
| `src/containers/barrel/model/Bucket.cs`, `src/containers/barrel/BarrelHaul.cs` | Steel buckets, and the Haul job's water work: barrels filled and dry farm plots watered from the worker's tool slot |
| `src/containers/barrel/model/WorkerWaterAction.cs` | The haul actions sent to the window for rendering |
| `src/containers/barrel/BedtimeWater.cs` | Cabin water at bedtime and its effect on dawn's needs |
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
| `src/menus/PauseMenu.cs` | The pause menu with the Goal Log, Controls and hosting |
| `src/goals/model/Goals.cs` | Goal names, details and the conditions that reveal and complete each goal |
| `src/goals/display/GoalLog.cs` | The active and completed goals, their details and item links to the recipe map |
| `src/ui/UiKit.cs` | What the interface's parts share: `UiKit` (the world, the pause, how a player action runs, the buttons), `LiveUpdates`, `LiveText` and `MovableWindow`, a window the player drags into place |
| `src/Icons.cs` | Item and building icons, cached as they are first asked for |
| `src/ui/Hud.cs` | The top-right corner with the clock plate and frame rate counter, and the mode and death labels |
| `src/MessageStack.cs` | The fading message stack at the top of the screen |
| `src/RecoveryPrompt.cs` | The "Skip to morning?" prompt |
| `src/mines/display/MinePanel.cs` | The mine's building steps, miners and outputs |
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
| `src/stations/display/StationPanel.cs` | A station's page: recipe board, process row, bellows and jobs, with its tool slots in a window beside it |
| `src/StationSlots.cs` | `StationSlots`: a building's item slots and a station's process row |
| `src/crafting/display/CraftPanel.cs` | The crafting menu |
| `src/stations/display/RecipeBoard.cs` | The recipe board the crafting menu and every station show, a recipe's hover popup, and the crafting menu's queue strip under the work |
| `src/RecipeMap.cs` | The recipe map: a row of item chains for each category, the station strip, chips that go to their item and the details strip |
| `src/PageText.cs` | How panels word durations and costs |
| `src/BlackHole.cs` | The crate-in-crate warning, collapse shader and game over screen |
| `src/MarketPanel.cs` | The trading post market, hiring offers and price sheet |
| `src/CrewPanel.cs` | The crew cards and a worker's own panel, with their hands and tool slot |
| `src/JobList.cs` | A worker's jobs as rows in order, with haul groups, dragged by their grips to another place |
| `src/StageTrack.cs` | Stages on a line, for a farm plot and a mine |
| `src/farms/display/FarmPanel.cs` | A farm plot's stages, input column, harvest row and fill bars |
| `src/cabins/display/CabinPanel.cs` | A cabin: its door and key, the roof barrel and the shared sleep controls |
| `src/rest/SleepCycle.cs`, `src/rest/StrawBed.cs` | Individual sleep, the shared skip to dawn, outdoor bed expiry and salvage |
| `src/rest/display/SleepPage.cs`, `src/rest/display/StrawBedPanel.cs` | Shared sleep controls and the straw bed’s remaining life |
| `src/rest/display/StrawBedModel.cs`, `src/rest/display/StrawBedView.cs` | The mat and a player lowering onto it or getting up |
| `src/cabins/CabinApproach.cs`, `src/cabins/Doorways.cs` | The walk to a cabin door, stepping in and out, following, knocking, standing aside and the key |
| `src/workers/breaks/BreakSeats.cs` | Reserves campfire and ground seats for workers at lunch or with nothing to do, and lights the campfires they sit at |
| `src/actors/enemies/wolf/DoorCheck.cs`, `src/actors/enemies/wolf/model/DoorCheckStage.cs` | A wolf at the door of a cabin with someone inside: scratching, going away from a locked door, forcing an unlocked one and taking a player out |
| `src/actors/enemies/wolf/DoorTug.cs` | A wolf tugging a player back out of a cabin's doorway, letting go and handing a badly hurt one to the takedown |
| `src/actors/enemies/wolf/WolfBodies.cs` | How wolves at a player keep their distance: closing in only to biting reach, and stepping aside from another wolf's body |
| `src/cabins/model/Doorway.cs` | `Doorway`, what a player is doing at a cabin's door |
| `src/actors/Momentum.cs` | How the player's movement answers the keys: braking, turning round and running back on a key for the other way |
| `src/actors/player/model/PlayerState.cs`, `src/actors/player/model/HeldStack.cs`, `src/actors/player/model/Command.cs` | One player's saved body and belongings, a held stack, and the commands a guest sends its host |
| `src/actors/enemies/wolf/WolfPatrols.cs` | Wolf dens, the wolves they let out, patrols, smell, howls and the way home |
| `src/actors/enemies/wolf/WolfHunt.cs` | The pack hunting a player: howling, closing in, leaping, pinning and backing off from firelight |
| `src/actors/enemies/wolf/WolfSnap.cs` | A wolf's bite: planting its paws, leaping on a fixed line and closing its jaws |
| `src/actors/enemies/wolf/WolfCounter.cs` | A struck wolf's sideways hop, skid, run, bound behind the attacker and pivot |
| `src/actors/enemies/wolf/WolfFollow.cs`, `src/actors/enemies/wolf/model/FollowStage.cs` | Follow mode: campfire and torch circles, and a torch follower bracing, running or standing its ground |
| `src/actors/player/Swings.cs`, `src/actors/player/model/Swing.cs`, `src/actors/player/model/SwingShape.cs` | Players' swings at wolves: when a swing lands, the ground its blade, flame or fist passes over, and the wolves it touches |
| `src/actors/enemies/wolf/model/Wolf.cs` | `Wolf`, a wolf as the world saves it |
| `src/actors/enemies/wolf/model/Den.cs` | `Den`, a wolf den's mouth, door and clearance |
| `src/actors/model/IWalker.cs` | `IWalker`, anything that walks the map on its own, along a route around what blocks it |
| `src/CampfirePanel.cs` | A campfire |
| `src/resources/display/ResourcePanel.cs` | Shared resource inspection with stock slots and any extra controls |
| `src/PilePanel.cs` | A pile or compost heap |
| `src/ToolRackPanel.cs` | A tool station |
| `src/containers/barrel/display/BarrelPanel.cs`, `src/containers/barrel/display/CarriedBarrelPanel.cs` | Water and hauling on a placed barrel and the water slot in a backpack barrel |
| `src/CratePanel.cs` | A placed crate with its filters and Haul job, and a packed crate opened from the backpack |
| `src/RecoveryBoxPanel.cs` | A recovery box |
| `src/WelcomePage.cs`, `src/PlansPanel.cs`, `src/BackpackPage.cs` | The welcome page, the building plans and the backpack page |
| `src/StoragePanel.cs` | A chest or other plain storage |
| `src/PackerPanel.cs` | The packing station panel and its good picker |
| `src/NeedsPlate.cs` | The HUD health, hunger and thirst vials, the bank and the red damage edges |
| `src/EndingScreen.cs` | The one-time ending screen |
| `src/menus/StartMenu.cs` | The main menu and its save, load, options, join and Controls screens |
| `src/menus/ControlsList.cs` | Key caps and actions on the shared Controls screen |
| `src/Saves.cs`, `src/Settings.cs`, `src/Launch.cs` | The save slots with quick save and load, the settings file and the command line |
| `src/actors/player/PlayerControl.cs` | The player in the world: walking, gathering, lifting crates, placing buildings, the remove selector, clicking things and using Space, and `ClickMode`, what the next click does |
| `src/ui/Hotbar.cs` | The tool belt: the round menu badges and nine saved item/plan references, assignment and use |

Rendering and sound:

| File | Owns |
| --- | --- |
| `src/world/display/WorldView.cs` | The 3D scene, orthographic camera, picking, lighting and world animation |
| `src/props/display/TreeFall.cs`, `src/props/display/Thrown.cs` | A felled tree going over about its trunk's foot, and a dug out stump or bush or a chip thrown through the air |
| `src/props/display/LocalClock.cs` | A count the server keeps, run on by the window between updates for a motion that follows it |
| `src/world/display/WorldScene.cs` | Ground dressing, particles and the animation review |
| `src/terrain/display/TerrainView.cs` | Mountain and valley terrain through the Terrain3D plugin |
| `src/terrain/display/BedMap.cs` | The baked map of where the drawn water lies, which the terrain draws its beds by |
| `src/EnvironmentPlacement.cs`, `src/GroundDressing.cs` | Grass, ferns and flowers from the baked placement, cleared around buildings and paths |
| `src/paths/display/GravelPathView.cs` | Laid gravel paths as the ground draws them: the map of how far each spot is from the gravel's edge |
| `src/paths/display/GravelPathShape.cs` | One gravel path tile as a model, for the plan's icon, its ghost while placing and the remove selector's red pad |
| `src/buildings/display/RemovalMark.cs` | The red mark on the building or gravel path tile the remove selector points at |
| `src/WorldLook.cs`, `src/Wind.cs` | The sound listener, the wind and the grass parting round the people in sight |
| `src/terrain/display/FootpathWear.cs` | Footpaths worn where the player and workers walk, and their growing back |
| `src/actors/display/Footsteps.cs` | Which footstep sound the ground gives at a spot |
| `src/water/display/LakeAppearance.cs` | The lake and sea surfaces, their tiles, the water and lily pad materials, and the water's clock |
| `src/water/display/ShorelineAppearance.cs` | The sand and stony bank strips along the lake and the coast, and the coast's water |
| `src/water/display/LakeDressing.cs` | The lake's bank, water, stones, reeds and lily pads |
| `src/water/model/ShoreLook.cs`, `src/water/model/WaterBody.cs` | The shore photoscans' looks, and the sea's and the lake's waves and colours |
| `src/paint_filter/display/PaintFilter.cs` | The paint filter over the drawn world ([assets/environment/paint_filter](../assets/environment/paint_filter/README.md)) |
| `src/WorldModels.cs`, `src/ModelKit.cs`, `src/*Shape.cs` | Build the models (see [assets/README.md](../assets/README.md)) |
| `src/actors/display/ActorRig.cs`, `src/actors/display/HumanShape.cs` | The person model, its joints and the shapes of its body (see [assets/README.md](../assets/README.md)) |
| `src/actors/display/Outfit.cs`, `src/actors/display/BodyBuild.cs` | The colours a person is drawn in, and the game's and the slim build of a body |
| `src/actors/display/ActorAnimation.cs` | Picks actor actions and gives their keyframed poses |
| `src/actors/display/RakeStroke.cs` | The rake's ground stroke and both hands' grips |
| `src/actors/model/BodySide.cs` | Which side of a body is its left and which its right, and which side a spot is on |
| `src/actors/enemies/wolf/display/WolfModel.cs`, `src/actors/enemies/wolf/display/WolfShape.cs` | Builds the wolf and poses it: its gaits, leap, howl, tug, recoil and fall |
| `src/actors/enemies/wolf/display/WolfCounterPoses.cs` | Poses the wolf's sideways hop, sliding paws and forepaw landing |
| `src/actors/display/Turning.cs` | How an actor's head, then body, turn toward where it heads, with inertia |
| `src/actors/display/ActorState.cs` | How a player or worker is drawn: where, facing which way, doing what, and at which doorway stage |
| `src/actors/display/ArmReach.cs` | Putting a hand on a point: the arm's joint angles for it, and a pose with the arm moved there |
| `src/actors/display/StrikePoints.cs` | Where a blow lands its tool on what is worked: a trunk's side, a rock, a mine's rock face, a work top or a stump's foot |
| `src/actors/player/display/FallBackView.cs` | A player knocked or tugged off their feet falling onto their back, their throat as they go over and their hands at the wolf |
| `src/cabins/display/DoorwayView.cs`, `src/cabins/model/DoorShown.cs` | Players at cabin doors: stepping in and out, following, knocking, standing aside and squeezing past with a palm on the frame |
| `src/cabins/display/CabinModel.cs`, `src/cabins/display/CabinShape.cs`, `src/cabins/display/HutProp.cs` | Builds the cabin's model, its wall shell, roof and interior, and the cabin as drawn: its door, window and lamp |
| `src/cabins/display/CabinWaterModel.cs` | Exterior ladder, roof walk points and the mounted barrel's feet and fill opening |
| `src/containers/barrel/display/VesselShape.cs`, `src/containers/barrel/display/BarrelProp.cs` | Steel barrel, its tap and sliding bung, and the bucket and its bail, shared by placed, carried, heaped and icon views |
| `src/containers/barrel/display/WorkerWaterView.cs` | A worker's carry, fill, ladder climb and pour poses |
| `src/cabins/display/StairSteps.cs` | The cabin's steps, how high they stand at a spot, and a pose's boots fitted onto them |
| `src/cabins/display/DoorKeyView.cs` | The round lock of a cabin's door with its key in it, drawn into a picture for the cabin's page |
| `src/cabins/display/InsideDoorPOV.cs` | What a player inside sees of a wolf forcing the door: a dark cabin with copies of the wolves and the player taken out, in a window that shows it as an old tape ([assets/environment/vhs](../assets/environment/vhs/README.md)) |
| `src/farms/display/FarmShape.cs` | Tilled soil and wheat at each growth stage |
| `src/farms/display/FarmSoil.cs` | Wet patches |
| `src/mines/display/MineModel.cs`, `src/mines/display/MineShape.cs`, `src/mines/display/MineRockShape.cs`, `src/mines/display/MineProp.cs` | Builds a mine's model and the outcrop at every cliff, and the mine as drawn stage by stage |
| `src/actors/enemies/wolf/display/DoorCheckView.cs` | A wolf at a cabin's door: its stands, scratching paw, prying snout, steps, launch, and how far it has levered and pushed the door open |
| `src/actors/enemies/wolf/display/TugView.cs` | A wolf tugging a player out of a doorway, wolves reared up onto the player's arms, and the lunge onto them as they fall |
| `src/actors/enemies/wolf/display/FeedingSpots.cs` | Where wolves stand round a fallen player: feeders side-on to the body, a pinner's paws on the chest |
| `src/IconRender.cs` | Renders item and building icons from the models (`./build.py icons`) |
| `src/audio/Sounds.cs` | Plays sound effects |

Checks and review tools:

| File | Owns |
| --- | --- |
| `tests/<feature>/` | Every check of a feature, as a player or worker sees it, beside its steps, such as `tests/hauling/`, `tests/storage/`, `tests/trade/`, `tests/tools/`, `tests/workers/jobs/`, `tests/chains/` and `tests/actors/player/` |
| `tests/SimulationChecks.cs` | The runner of every simulation check, `SimulationChecks.Run` (`SELF-TEST PASS`), which calls each feature's `Run` |
| `tests/*Checks.cs` | Simulation checks of the features that have no folder yet |
| `tests/*Steps.cs` | Steps several features' simulation checks share |
| `tests/NetScene.cs` | `NetScene`, one feature's scene in the network session: what the host puts down, the host's half and the guest's |
| `src/GameChecks.cs` | Input/UI checks through real input events for the features that have no folder yet, and the runner of them all, `Game.RunUIChecks` (`UI-TEST PASS`) |
| `tests/crafting/RecipeReadyUIChecks.cs` | Every station recipe brought to the point where its next tick starts the batch, by double-click and by hand, and refused short of it with a shake and a message |
| `src/NetChecks.cs` | The host and guest network session (`NET-HOST PASS`, `NET-GUEST PASS`): joining, leaving and reconnecting, with each feature's `NetScene` played in between (`Game.NetScenes`) |
| `src/ServerChecks.cs` | One window playing on the real server process (`SERVER-TEST PASS`) |
| `src/InputSteps.cs`, `src/PanelSteps.cs`, `src/SessionSteps.cs` | Steps several features' UI and network checks share, and `Game.TestClean`, which fails a UI check that leaves a mess |
| `src/RecipeSteps.cs` | Steps that open a fresh station for a recipe, give and load its items and fuel, and expect a batch to start or be refused |
| `src/Showcase.cs` | The review scenes behind `--showcase` |
| `src/ReviewDrive.cs` | Plays the window from a command file behind `--drive`, for the `playtest` skill |
| `src/performance/PerformanceRun.cs` | The rendered performance run's window half: sprinting the route by day and night, sampling frames, writing results |
| `src/performance/PerformanceStage.cs`, `src/performance/PerformanceFixture.cs` | The run's server half, and the staffed camp it plays |
| `src/performance/PerformanceRoute.cs` | The run's sprint from the beach past the camp and the lake to the valley's top corner |
| `src/performance/ResidentMemory.cs`, `src/performance/model/PerformanceSample.cs` | Reading resident memory without allocating, and one sampled frame |
| `src/cabins/DoorwayReview.cs` | The `--doorway` scenes: two players and wolves at a cabin's door, outside it or inside behind it, moved by a script and run by the game's rules |
| `src/actors/enemies/wolf/WolfReview.cs` | The `--wolf-scene` scenes: wolves turning on a patrol, turning to a player they smell and howling, feeding on one they brought down, following one with a torch, and fighting one with an axe |
| `src/intro/WorldIntro.cs` | The intro film behind `--render-intro`: its shots, film camera, lighting, fades and sound cues |
| `src/intro/IntroSets.cs`, `src/intro/IntroPoses.cs` | The film's office, rented room and train car, and the poses nobody strikes in the game |
| `src/intro/display/CatgirlPage.cs` | The web page on the hero's screen in the film: three catgirls dancing inside a browser window |
| `src/intro/display/Catgirl.cs` | A catgirl of that page: face, hair, ears, tail, hands with fingers and one of three outfits on the person model's slim build |
| `src/intro/display/CatgirlDance.cs`, `src/intro/display/DanceFrame.cs` | The catgirls' dances: one the two at the sides do in step, and the solo of the one in the middle; and a moment of a dance |
| `src/intro/IntroMovie.cs` | Plays the film over the start menu when the game opens, until it ends or Esc skips it |
| `tools/intro_film.py` | Draws the film's frames and writes its sound cue sheet |

### Simulation, view and UI

Keep balance and recipe values in `GameData` and `GameTables`, apart from the rules in the simulation code.
Keep rendering and input separate from state transitions, so headless checks can exercise gameplay rules without relying on screenshots.

- `tests/EconomyChecks.cs` measures real workers through a day and checks the economy rules.
- `tests/performance/AllocationChecks.cs` holds ticks to their allocation budgets.
  `tests/pipes/PlumbingAllocationChecks.cs` applies the tick budget to pumping into two barrels.
- `tests/chains/ProductionChainChecks.cs` has workers alone run the berry, bread, canned stew and steel pickaxe chains to a sale.

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

- Sleep is individual. The clock advances to dawn only when every present player is asleep.
- Wolves each hunt one player (`Wolf.Target`).
- Messages reach the acting player unless they concern everyone.

#### Network updates

An update holds only what changed since the last one the guest confirmed.
The host keeps each recent update as one JSON entry per thing, worker, wolf, trade cart and player.
Shared entries hold the remaining state, worker jobs, gravel paths, steel pipes, stripped sand and packed crates.
Entries leave out fields at their type's default and write a fresh inventory as `{}` (`UpdateJson`).
A field whose new object starts elsewhere is marked to be always written, which `MultiplayerChecks` enforces.
A changed entry whose field names or order changed is sent whole.
When the field order matches, the host and guest step through the fields once to write or apply changed values.
`tests/pipes/PipeWaterUpdateChecks.cs` checks that a pipe water update preserves the host's fields and field order.
`WorldUpdates` writes them into reused buffers and keeps the last update's array for every entry that did not change, so only changed entries allocate (`tests/performance/AllocationChecks.cs`).
The welcome is a guest's first update, with the fog uncovered so far.

- Updates go unreliably on their own channel, so a lost one never delays the next.
  Other players and wolves glide between updates. On a bite's landing the host uses the target's latest movement report.
  Observers align the landing wolf and its target to the same update. The target keeps their own movement input.
  A reliable sound message carries the first-hit effect even when the update showing the wince is lost.
- The whole world after a long stall, or a change too large for one packet, goes reliably on a third channel, so messages and actions never wait behind it.
- Guests merge entries into their copy in place, so panels and the view keep their objects, and touch only entries that differ from what they show. A change to numbers alone is set directly (`WorldUpdates.SetNumbers`); anything else reads the entry whole.
- The server writes every entry for each update and compares the bytes. With 876 things that costs 0.7 ms of its own process; marking what changed would save most of it, and one mutation missed would leave a guest's copy wrong without a sound.
- A player keeps only the updates the server can still build on: confirmations only move forward, so nothing older than the update just taken's baseline is needed.
- Each action carries where the guest stood, so the host checks reach from there rather than from a position still gliding in.
- `Net.SendEveryUpdate` turns off ENet's throttle: it read the hitch of loading a game as a slow line, and let through one update in 32 for several seconds.
- An action's result comes back with a later update. A window checks what it can before sending, and `Net.Made` names a new mine or worker so the panel opens on it. Panels refresh with every update while `Net.AwaitingAnswer` holds, and a page draws itself again when what sets its layout changes (`PagePanel.RedrawOnChange`), since drawing it right after an action would show the world before the answer.
- Leaving waits for the server to confirm before the line closes, so actions still in flight arrive; the owner's server closes instead, and the line going is the confirmation.
- Joining another game from your own autosaves it and leaves with `Game.StepAway`, which keeps its server up: a failed or cancelled join goes back to it, and after a good one it ends with nobody in it, 30 seconds later.
- A window applies updates thirty times a second and draws more often, so a motion the view drives from a count the server keeps runs on its own clock in between (`LocalClock`): a stump or bush leaning as it is dug, a thrown wolf carcass, a mine's hole and frame, a forge's heat and gauge, a cart's trip and the time of day the sun and light show. The clock moves every drawn frame at the rate the count last ran at against the world's clock; an update makes up a small difference over the next frames without turning the motion back, and a big one, such as a pump of the bellows or a sleep that skips the night, is taken at once.
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
The default camera is orthographic with a 30° pitch, giving a 2:1 isometric footprint.
The first-person preview switches the same camera to a perspective at eye level.
`FirstPersonCamera` turns the camera from mouse motion and rotates walking directions to match.
`WorldView` casts the same picking rays from the eye, and shows the sky, distant terrain and live objects without fog.
The first-person camera hides the local player's head so its view from the eyes can show the body, hands and tools.
`FirstPersonSky` is created when the preview first opens and draws a sun or moon following the world light's arc.
Overhead view uses its own ambient sky resource and a solid background.
Cabins, downed players and underground pipe work keep the overhead view.
The sun and moon share one shadow map across this view.
Its small shadow offset keeps the shade attached to feet, trunks and low props, while ambient occlusion shades creases.

Picking and canopy fading use the same per-kind vertical hit cylinders (`WorldModels.Hits`).
Hover labels name buildings, resources and workers in the world.
The shore label reads “Collect water”.
Other objects under the cursor take priority over the extra width of an old tree’s trunk.
Trees use three variants chosen by object id, so saves need no new fields.
A tree's height comes from its id too (`TreePlanting.Tallness`). `TreePlanting.BaseScale` sizes its model and hit boxes from 0.9 to 1.5.
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

Away from hunting wolves, the window moves its own player through `Momentum`: a key for about the opposite way brakes the run to a stop over `Momentum.BrakeSeconds`, stands `Momentum.TurnSeconds` to turn round and builds back up to full pace over `Momentum.StartSeconds`; any other change of keys turns at once, and letting go stops at once.
While it brakes, `PlayerState.Braking` is set and carried to other machines, and the view shows the `Brake` lunge: front heel planted ahead, the weight sunk onto the bent back leg, leaning back with the arms forward.

Digging (`ActorAction.Dig`) holds the shovel in both hands: the right on top of the handle and the left `ActorAnimation.ShovelGrip` down the shaft.
`ActorAnimation.DigKeys` give where the blade's tip is and how the shaft leans at each moment: driven into the ground ahead, leant on, levered up and thrown aside to the left.
Both arms are solved from there with `ArmReach.ArmTo`, and `ActorRig.Show` lays the shaft from the right fist through the left, so the blade goes where the keys take it.
The front boot stays flat and the back one on its toe at fixed spots, so neither slides as the hips go down and up.
The handle is kept ahead of the right side of the chest and below the shoulders: at the middle of the chest the folded right arm would lie inside the torso, and higher it would be in front of the face.

Players with a rake in the backpack and workers with one in hand rake leaf spots (`ActorAction.Rake`).
`RakeStroke` places both hands on the shaft and draws the teeth across the ground toward the actor's feet.
The leaf rustle starts as the teeth touch the patch.
The animation review clips are `player rake` and `worker rake`.

A player with something to make by hand squats over it, turning it in both hands (`ActorAction.Craft`), only while standing still.

The player's carried tool selects its matching action and the tool shown in hand.
Workers animate according to movement and an explicit transient `WorkerWorkState`, not localized or presentation status text.
The transient state is excluded from saves and recalculated on the next simulation tick.

Players and workers hug a carried steel barrel to their trunk with both hands on its front sides (`ActorRig.HugBarrel`).
A player carrying water in the barrel keeps a walking gait, with one or both feet planted through each step.
For a water haul, `WorkerWaterView` poses the worker using the water action, target and progress sent by the world.
A worker climbs the fitted ladder holding the vessel in one hand, walks across the roof, tips the vessel using both
hands, and returns down the ladder.
The stream starts at the vessel's lip and ends at the barrel's opening or at the soil when watering a farm plot.

At a packing station the packer stands behind the table (`World.PackStand`) and the `Pack` action rummages in the heap of goods, lifts a piece across and settles it into the crate, then turns back.
The player working a station they stand near packs the same way.

At a cooking table, which is only worked for the mortar's recipes and dough, the `Grind` action holds the mortar in hand (`ActorAnimation.StationAction`); kneading dough uses the `Pack` action, facing the table.
The left fist holds the bowl out in front, and the right takes the pestle twice round the bowl's floor, then lifts it and pounds.
Both arms are solved from where the hands have to be (`ArmReach.ArmTo`), so the pestle stays over the bowl.
`ActorRig` turns the bowl and pestle against their forearms in the resting pose (`ActorAnimation.GrindRest`), which keeps the bowl level and the pestle leaning toward its hand.
While someone grinds, the table's own mortar is off the counter (`WorldView.grinding`).
Likewise a bench's or an anvil's slotted hammer is off its top while someone swings it (`WorldView.swung`): a worker,
who works a station only with its slotted tool and is drawn with it, or a player who carries no hammer as fast
(`World.WorksWithSlotted`).

Felling a tree (`Chop`, `SteelChop`) is a stroke from the side: the axe is wound round to the left at shoulder height and swung across, almost level, into the trunk's flank.
The shoulders turn through the stroke, the grip turns the blade to cut sideways (`Pose.Grip`, which `ActorRig` applies at the right fist), and the left fist is put on the handle just past the right (`ActorAnimation.TwoHanded`).
An axe swung in the air with Space or a click takes turns between two strokes.
The first (`AxeSwing`, `SteelAxeSwing`) goes up overhead and comes straight down in front; the second (`AxeSweep`, `SteelAxeSweep`, `ActorAnimation.SweepKeys`) is wound far round behind the left shoulder and swept wide across the front at chest height and on round to the right, in one stroke.
A torch is swung bent over at the waist with the knees giving (`ActorAnimation.TorchSwingKeys`), so its flame passes low enough to reach a wolf.
The swing keeps the heading set by the pointer or the movement keys as it begins (`PlayerControl.Swing`).
Space uses the last walking direction while standing still.
Near a hunting wolf a player can reverse immediately, including through the swing.

Manual bellows pumping, the unarmed punch and the axe swing trigger one loop of their action.
Each striking work action has an impact moment that shakes its target and spawns chips, leaves, soil, splashes or sparks; packing and grinding strike nothing.
The axe, pick and hammer land there (`ActorAnimation.ChopStrike`, `MineStrike`, `HammerStrike`), speeding up all the way into the blow; a punch draws the fist back first.
A dig strikes at `ActorAnimation.DigStrike` and throws its soil off the blade (`ActorRig.ShovelTip`) at `DigToss`.

A blow lands its tool's working end on what is worked, wherever within reach the worker stands (`StrikePoints`, `ActorState.Mark`): the axe's head on the trunk's side, the pick's point where its swing first meets the rock or on the cliff's rock face beside a marked mine's cave mouth (`Mines.DigFace`), the hammer's face on the anvil's or bench's work top, and the shovel's blade in the ground at a stump's or bush's foot.
Chips and soil fly from there.
Where the blow's own swing meets what is worked, the swing is cut short there (`ActorAnimation.Swing`).
Where it passes it by, the right arm is solved to reach it with the tool held in line with the forearm (`ArmReach.ArmTo`), the trunk turning to face it and leaning in or back as far as the arm needs; an arm that would have to swing behind the trunk or fold its elbow tight stops where its swing passes nearest instead.
A dig turns toward its spot, the hands go part of the way further out and the shaft leans out the rest, the trunk bending just far enough for both hands to keep hold.
The blow and what follows it move; the wind-up stays as keyed.

Work done in place stands both boots on fixed spots (`ActorAnimation.FeetOf`): a pose gives only its hip height, and the legs bend to it with the boots flat, a heel lifting only where the ankle would bend past 30° or the leg would straighten.
An action's key poses are made once (`ActorAnimation.ChopKeys` and the like): every worker and player is posed every frame, so a pose is worked out without allocating.
Pausing freezes animation and effects.

### Wolf animation

`BodySide` (`src/actors/model/BodySide.cs`) is the one place that says which side of a body is which: 1 its left, -1 its right, from the way it faces, standing or fallen onto its back.
A wolf that comes at a body from a side takes the side it is on (`BodySide.Of`, `BodySide.Free`) and stands out from it that way (`BodySide.Out`): taking an arm in a tug (`DoorTug.TakeSide`, `TugView.ShowHolding`), angling away from another tugging wolf (`TugView.ApartFrom`), pinning a tugged player from the side of them it stands on (`DoorTug.LetGo`), waiting behind a door (`DoorCheck.WaitSpot`) and choosing a spot to feed at (`FeedingSpots.FreeSpot`, which counts a spot across the body a metre further off).
A wolf's own forepaws take the spots on their own sides (`WolfModel.Uncrossed`).

At a gallop, or scared off, a wolf turns fast; reversing at a gallop it brakes.
At a walk or a trot its head turns first and its body comes round over about a second (`WolfModel.SlowTurn`), the spine curling into the turn by `WolfModel.CurlShare` of the head's lead and the tail swinging in after it, as a dog walking a circle bends.
Setting off more than 75 degrees from the way it faces (`WolfModel.SetsOffWithin`), it turns round where it stands first, about its hips with its forepaws stepping round (`WolfModel.HipsBehind`, `WolfModel.TurnStep`), then catches up with its place in the world at `WolfModel.CatchUp` more than its pace (`WolfModel.Drift`), though a wolf walking in the world catches up no faster than it still walks, so it never trots to do it (`WolfModel.CatchingUp`).

`WolfShape` builds one skinned wolf mesh on a skeleton of spine, neck, head, jaw, ears, upper lips, brows, a three-bone tail and four legs of three segments and a paw; `WolfModel` poses it.
The head has a broad skull with a ruff of fur spikes down each cheek, a mane of them round the neck and hackles along the back (`WolfShape.FurSpikes`), and fangs, front teeth and cheek teeth in both jaws.
The upper lips hang over the upper teeth on a bone of their own: `WolfPose.Snarl`, 0 to 1, lifts them and lowers the inner end of each brow.
A wolf snarls while it stalks behind torchlight, leaps, tugs, holds an arm, feeds, recoils from a hit, and stares in at a cabin's door; otherwise its mouth is relaxed.
Legs are placed by two-bone IK from where each paw should stand, with the front elbow bending back and the hind stifle forward as in a real canine (`WolfModel.Reach`, `WolfModel.ReachTo`).
No joint bends past a dog's range (Jaegger, Marcellin-Little and Levine 2002): the elbow stops 15 degrees short of straight and the stifle 18, the wrist opens 16 past straight and the hock stops 16 short of it; where the lowest segment's angle asked for would take the wrist or hock past that, the leg takes the nearest angle that does not, and a paw out of reach is reached for as far as the leg goes.
`WolfModel.Plant`, once the view has placed the model for the frame, holds each paw that comes down where its toe pads landed in the world while the body moves and turns over it, until its pose lifts it; a lifted paw goes on from where it left the ground to where its pose has it, lifting clear rather than dragging, and no part of a sole goes into the ground.
Standing, braced, feeding, howling, pinning, reared or recoiling, a paw left more than `WolfModel.StepAfter` from where its pose has it steps there, one paw at a time, and a paw its leg cannot keep where it stands steps too.
`WolfModel.SolePoint` gives the outline of each paw's underside, which the checks measure the ground against.
A stance moves the paw back at exactly the body's speed, so paws stay where they land; to push off the heel lifts about the toe pads (`WolfModel.PushOff`), and each swing leaves and lands moving as fast as the ground goes under it, so a paw neither slides as it lands nor as it leaves.
`WolfModel.Move` picks a lateral-sequence walk, a diagonal trot or a rotary gallop from the wolf's smoothed speed; the gallop folds the spine at the gathered float and stretches it at the extended one.
The gallop takes about two and a half strides a second, a few more the faster (`WolfModel.GallopStride`, after Heglund, Taylor and McMahon 1974), so a faster gallop mostly reaches further; it runs a little low (`WolfModel.GallopLow`) so the forelegs reach the ground ahead, and in its floats the body rises and falls under gravity, the legs taking its weight like springs on the ground between them (`WolfModel.Bounce`).
Galloping, the hind paws pass outside the fore, which land close to one line.
A change of motion, or of a motion's wary or calm form, blends from the last shown pose over a short smoothstep: the body's angles go in between and each paw goes from where it is in one pose to where it is in the other (`WolfModel.Blended`), so standing, walking, trotting, galloping, leaping and recoiling run into each other without a paw on the ground in both leaving it.
A wolf turns as people do (`WolfModel.Turning`, `WolfModel.TurnToward`): its neck and head turn toward a new heading first and the body follows, leaning into the turn at a gallop; one struck turns straight to its hitter, and one tugging, lunging or feeding is turned by that motion.
Reversing at speed, a wolf's body faces the way it came for a moment while it moves the other way; it brakes then (`WolfModel.Brake`), the forelegs braced out ahead and the paws sliding, instead of running backward.
The path is the simulation's, unchanged: the brake is only drawn.

`WorldView` times the wolf against the player:

- a throat leap fixes its direction before it springs.
  The wolf rises under gravity and its jaws can catch the player at landing.
  On a hit the wolf keeps its jaws on the player's throat as they fall and while they lie on the ground (`FallBackView.Throat`);
- a wolf that recoils from a swing (`Wolf.Stagger` rising) hops back `World.KnockBackDistance`, moving only while in the air, at an even pace and rising and falling under gravity (`WolfModel.HopSeconds`), and ducks low once it has landed before rising wary; where the blow reaches a machine a moment before the swing it shows comes down (`ActorAnimation.Contact`), the wolf stands until it does (`WolfModel.Struck`);
- a snapping wolf (`Wolf.Snap`) sinks onto its haunches, springs out with its jaws open and shuts them as it lands.
  The forelegs absorb the landing as the wolf holds its place (`WolfModel.Snap`);
- a following wolf its player has walked up to (`FollowStage.Braced`) stands braced, low over forepaws set wide, ears flat and lips drawn back (`WolfModel.Brace`);
- a following wolf walking round its player turns its head to them for `GlanceSeconds` in every `GlanceEvery`, each wolf at its own moments by its id (`WolfModel.Glance`, up to `WolfModel.MaxGlance` from its body);
- a killing blow throws the wolf off its feet onto its side from the pose it was killed in (`WolfModel.FallFrom`), covering the knockback in the air at an even pace and dropping under gravity with its paws clear of the ground, and it lands with a bounce and lies limp.
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
8. Buy a steel hammer with more crates of berries, slot it in the anvil, hammer steel into steel parts there and make
   steel tools.
9. Pack a steel tool into a crate, sell it with another crate of berries through the trading post and use the proceeds to hire and assign a worker, with enough left for their first wage.

There are no iron tools; iron bars only go into buildings and steel.

#### Water

The player starts with one refillable bottle (`src/Water.cs`); the player-facing rules are in [README.md](../README.md#food-water-and-time).
Filling it or drinking straight from the source takes one `GatherSeconds`.
Every point on the lake shore is a collection target.
A station takes water as a liquid in liters, `Water.Liquid`, with a drawn droplet icon.
Stations and barrels hold it loose, and a recipe asks for so many liters.
`GameData.Vessels` lists each refillable vessel (`VesselDef.States`, its item at each fill from empty to full, and its liquid), and `src/Vessels.cs` holds the rules for all of them:
a vessel put into a station whose recipe takes its liquid pours every liter it holds there and stays where it was, emptied (`World.PourVessels`); loading a batch pours from the least full first (`World.PourLiters`);
the liquid leaves only into vessels with room, fullest first (`World.Fill`), so taking it with none is refused (`World.FillBlock`) and the slot shakes.
Every move of a liquid or a vessel between boxes, by hand or by a worker, goes through `World.Carry`, which picks one of those or a plain transfer.
Loading a station by hand and a worker's delivery pass it what the input slot has left (`Inventory.SlotRoom`).
Station haulers fetch a liquid input in the fullest bottles storage has (`World.LiquidSource`), pour them all and store them again; leftover liquid leaves in vessels with room a hauler fetches (`StationHaul.RoomySource`), and a dismantled station fills the backpack's vessels and spills the rest.
No hand recipe takes a liquid, which `GameData` checks.

#### Steel barrels and buckets

A barrel holds up to 50 liters of water.
Its item id records its fill, so moving it between a player, a worker,
the ground, a pile and a cabin preserves its water without a separate packed inventory.
The ground barrel uses a one-slot tank and the same filter rules as a plain pile.
A cabin's only storage slot holds the actual barrel item.
The Roof barrel window sits beside the cabin page in `SidePanel.Aside`, like a station's tool window.
Refills update that item in place.
A backpack stacks empty barrels and carries up to one barrel containing water, whose fill determines how much it slows the player.
Pouring into an empty stack keeps the barrel being filled in its original cell and requires room for the remaining empty barrels.
A worker with a barrel cannot carry other goods in their hands.

The steel bucket is a refillable 10-liter vessel.
Empty buckets stack where the inventory allows it.
A worker's tool slot holds one bucket or bottle, and a tool rack accepts both.
A water haul uses the tool slot to preserve the bucket or bottle and its fill throughout the trip.
Workers fetch water from the nearest full floor barrel or the lake.
If a building blocks the nearest shoreline point, the worker looks along the shore for open ground.
The worker keeps the chosen source and standing point while walking there.
If a worker cannot reach a barrel's pouring point, they move on to the next job they can do.
A cabin haul walks to the outside ladder, climbs, pours into the mounted barrel and climbs back down.
The cabin offers Haul only while a barrel is mounted.
The window advances the visible haul phase between server updates through `LocalClock`.
Construction keeps the ladder approach on dry ground and clear of other buildings.

A player who sleeps in a cabin drinks from the roof barrel, leaving water for earlier occupants who have not turned in.
Sleepers fill bottles when a shared skip starts, or at dawn if time runs normally.
On waking, sleepers drink from what is left, in cabin-entry order when they wake together.

During the respawn countdown, a player with no cabin anywhere in the world is offered “Skip to morning?”.
It advances normal simulation through dawn, including needs, trades, production and wages.
In multiplayer it is available only when everyone is downed; a living player prevents a shared time skip.

### Goal Log

`Goals` reads the settlement's milestones and its history of obtained items to decide which goals are visible and
which are complete.
The initial goals can be completed in any order.
Building the bench or stone furnace reveals a related goal.
The first steel bar reveals Too soft and the first steel parts reveal Infinite power.
The first time a player sees a mineable ore deposit, the server records the discovery in `SaveState.Milestones` and
Steel tools appears for the settlement.
Finding loose ore on the ground leaves that goal hidden.

Infinite power lists a mine and links to its item for each ore in `Cliff.Ores`.
It is complete when a mine is open for every ore in that list.
NEET is complete after `GoalsDef.ProfitDays` consecutive days of at least `GoalsDef.DailyProfit` profit.
`SaveState.DayRevenue` collects returned sale coins.
At dawn the wages charged are taken off and the result joins `SaveState.Profits`.
Reaching the streak sets the `profit_week` milestone, which stays reached.
The open mines and profit milestone together unlock the ending.

### Survival and needs

Food, water, the day/night cycle, shelter, wages and wolf encounters support the production loop.

- Items carry what eating or drinking one restores (`ItemDef.Hunger`, `Thirst`) and a `Food` flag; nothing eaten heals.
- Health comes back only in sleep, `SleepCycle.HealPerSecond` a second while hunger and thirst both stay above `SleepCycle.FedNeed`.
  Healing is prorated to the part of the tick before either need falls below the threshold.
- Food is what wolves eat and compost will take, cooked or not.
- An item that restores nothing, such as raw meat, wheat or dough, only says it needs cooking.
- `HungerHours` and `ThirstHours` in `GameData` set how many in-game hours a full bar lasts.

A player can sleep at any hour inside a cabin or on an outdoor straw bed. The cabin's Sleep button appears only inside.
`PlayerState.SleepingAt` identifies a sleeper's place. The window draws outdoor sleep on the mat without sheltering the player.
`SleepCycle` advances the simulation toward dawn only while everyone is asleep, once an outdoor sleeper has lain down.
Hunger or thirst running out, an attack outdoors, or scratching at an unlocked cabin door wakes those affected and stops the skip.
Sleepers can also choose Wake. At dawn everyone still asleep wakes.
A straw bed takes two grass and twenty leaves, lasts three full days from `Thing.BedBuiltAt` and refunds only within one hour.

#### Sprinting

Holding Shift while moving sprints at `SprintSpeed` times the walking speed (`PlayerState.Sprinting`, sent by guests with their walk).
On a gravel path either is `GravelSpeed` times as fast ([Gravel paths](#gravel-paths)).
Hunger and thirst drain `SprintDrain` times as fast meanwhile.
Under 15% food or drink (`GameData.LowNeed`) the player cannot sprint and the screen edges pulse red.
As food falls under 20% (`GameData.GrowlBelow`), and with every further 5% gone (`GameData.GrowlEvery`), `stomach_growl` plays at the player.

#### Death and recovery boxes

Death moves carried items to a recovery box and respawns the player after a 30-second timer in the free cabin nearest to where they fell, or at the clearing without one.
Recovery boxes are never automation storage and disappear once empty.

### Sight and fog of war

A player's **line of sight** is the circle of `SightRadius` (20 metres) around them, which ends where the grey fog starts.
"In sight" and "out of sight" mean inside and outside it; what a wolf sees is its own `WolfSightRange`.

Fog of war is simulation state.
`World.Look` runs every tick: it marks tiles within `SightRadius` of the player in `SaveState.Explored` and replaces `SaveState.Remembered` entries in sight with snapshots of the live things there.

`World.CollectKnown` gives what the overhead view draws and picks: live things in sight, snapshots elsewhere, nothing never seen.
The first-person view draws live objects across the valley. Picking and small details such as fitted tools use the nearby sight circle.
Snapshots are updated in place and keep only the look: no inventory, fire, heat or work in progress.
The view picks against what it draws but only returns things that still exist.

In overhead view only what is in sight is drawn every frame (`WorldView.SyncThings`).
A thing out of sight is drawn once as its snapshot, moved on far enough to finish whatever it was doing, and left alone until its snapshot changes, night falls or lifts, or it is seen again.
A change that did not happen in front of the player is not played when they see it (`WorldView.Watched`): a tree felled out of sight is a stump, a chest filled out of sight keeps its lid shut, and a chip or a building that appeared out of sight neither flies nor lands in dust.
A thing that leaves the world in front of the player may play its way out (`WorldView.Departs`): its model is kept until `WorldView.Depart` is done with it.
A rock or ore deposit mined of its last piece in front of the player breaks into chips with a puff of dust and crumbles away in a fifth of a second (`Prop.Crumbled`), though the depleted thing stays in the world.
A station's or campfire's fire burns fully while it has work or fuel and through the tick between one batch and the next, and dies down over half a second once they run out (`HearthProp.Burning`).
A felled tree topples and fades over the stump that took its place, and a stump or bush dug out is thrown up, tumbles and shrinks away; one a building now stands over (`World.Clears`), or a gravel path tile lies on (`GravelPaths.Laid`), just goes.
A tree falls away from whoever landed the last blow, player or worker, the way its stump records (`Thing.FellToward`), so it falls the same way on every machine.
It turns about the foot of its trunk on the side it falls to, as a stiff pole hinged at its foot does (`TreeFall`): slowly at first, then faster, a taller tree taking longer and landing faster.
It starts upright at the speed a 5° lean would give it, and comes to rest where its trunk or a branch first touches the ground, between about 45° over and lying flat; its leaf cards bear no weight and lie flat on the ground where they reach it.
The shake of the last blow carries on as it goes over.
A dug out stump or bush is thrown straight up and comes back down in `Thrown.DugOutSeconds` under gravity, tumbling steadily over about its middle (`Thrown.DugOut`), and no part of it goes lower than it stood as it came out.
A chip knocked off rock flies in an arc under gravity, spinning steadily round its middle, lands where it lies and skids and turns to a stop over `WorldModels.ChipSettle`.
While it is dug at, a stump or bush leans further over with `Thing.Progress` (`DugProp`).
Work out of sight shakes nothing and throws no chips.
It is silent too, since `SightRadius` (20 metres) lies beyond the 16 metres a sound carries ([Sound effects](#sound-effects)).

`WorldView` hides wolves, workers and carts outside sight.
It draws one screen-wide quad after everything else, placing each pixel in the world from the depth buffer: black on unexplored tiles, a grey wash on explored ones, clear inside the sight circle.

### Night and wolves

`WolfPatrols` (`src/actors/enemies/wolf/WolfPatrols.cs`) runs the wolves that hunt nobody; `WolfHunt.TickWolves` runs each player's hunters.
Every wolf, like players and workers, moves only onto ground `World.CanStand` allows: `World.Slide` for a step that may slide along what blocks it, and `World.Travel` for a walk to a goal that routes around it.
`Travel` swerves round anything a route's straight leg clips instead of walking through it, and after a search finds no way it waits `RouteRetrySeconds` before the next.

- **Dens.** `WolfPatrols.Dens` are five small caves at the foot of the ranges, drawn as a mine's outcrop and tunnel at half size.
  The outcrop is solid like a mine's (`WolfPatrols.InOutcrop`), and nothing may be built within `Den.Clearance` of a door, so the wolves always have a way out.
  From `GameData.DuskStarts` (20:24), each den lets out `WolvesPerDen` wolves at its `Den.Door` once no player sees it (`SaveState.DensOut`), one den every `DenGapSeconds` so their wolves never all appear on one frame.
  After each further hour one den, picked at random among those whose door no player sees, lets one more out until the wolves head home (`SaveState.DenHours`).
- **Strength.** A wolf's health, bite and pace are `GameData.WolfHealth`, `WolfBite` and `WolfTrot`, the same every night: it stands two blows of a steel axe and falls to the third or fourth, and its fourth bite brings a healthy player to half health.
- **Patrol.** A wolf hunting nobody (`Wolf.Target` zero, `WolfMode.Patrol`) walks its `Wolf.Heading` at `PatrolPace` (half) of the trot it stalks at, a walk, and picks a new random heading it can take when the ground ahead is blocked or in campfire light.
- **Smell and howl.** A patrolling or called wolf within `SmellRadius` of a player out in the open takes them as its target.
  One that finds them beyond `WolfSightRange` first turns to them where it stands (`Wolf.HowlIn`): `WolfPatrols.HowlTurnSeconds` for half a turn from the way it was walking, and the square root of its share of that for a smaller turn, which is how long the view's slow turn takes.
  Then it howls (`WolfPatrols.Howl`), standing still, for as long as the howl recording plays (`LoneHowlSeconds` or `PackHowlSeconds`, `Wolf.Howl` counting down `Wolf.HowlLength`); one that finds them within sight goes for them at once.
  The night's first howl plays `wolf_howl`, a lone wolf, and later ones `wolf_howl_group` (`SaveState.Howled`).
  A howl is silent beyond `HowlRadius`, so a far pack sounds far; like every placed sound it fades with distance from the player and pans with its direction.
  The howl calls every patrolling wolf within `HowlRadius` to where it stood (`WolfMode.Called`, `Wolf.Call`); a called wolf still smells on its way and goes back to patrolling once there.
- **Approach.** After its howl a wolf trots at its player out of firelight until within `WolfSightRange`, then sprints (`Wolf.Chase` above zero) at `WolfChase` times `WalkSpeed` with one distant growl (`Wolf.Sprinted`), which data validation keeps above walking and below `SprintSpeed`.
  Within sight it closes in only to `WolfBodies.StandOff`, within biting reach, and backs off to it at a walk (`WolfBodies.BackOffSpeed`) from a player another wolf tugs toward it (`DoorTug.TuggedTo`).
  Wolves at a player keep their bodies apart (`WolfBodies.Body`, `WolfBodies.WolfHalfWidth`): one in another's way steps aside at `WolfBodies.SideStepSpeed`, unless it holds the player.
  A hunting wolf's body faces the way it runs, or its player while it stands, creeps or backs off (`Wolf.Heading`, `WolfHunt.Face`).
- **Bite.** Within `WolfSnap.Trigger` the wolf plants its paws and fixes its bearing and destination (`Wolf.Heading`,
  `Wolf.SnapTo`). After `WolfSnap.Windup` it leaps for `WolfSnap.Flight` along that line.
  Solid ground and buildings can stop the leap. At landing it hits only if its jaws reach the player's body and the way
  between them is clear (`WolfSnap.JawsAhead`, `WolfSnap.Reach`). Wolves approach the body
  on the doorway path (`DoorwayPath`), and their jaws can reach onto the exposed step.
  The wolf stays facing the same way during `WolfSnap.MissRecovery` or `WolfSnap.HitRecovery`, and waits
  `WolfSnap.Rest` before starting another attack.
  A blow that lands first interrupts either leap. A torch wave can interrupt a normal snap's windup while the wolf is still wary.
  A bite that lands on a player in the open part of a doorway starts a tug (`DoorTug.TugOut`).
- **First hit.** A wolf that survives a weapon blow in the open winces, hops sideways, and slides on its forepaws.
  It takes a wide running arc into a second hop behind the line of the blow (`WolfCounter`). It lands on its forepaws first,
  slides its rear paws round them, and stands facing the player before it can start a bite.
  The wolf tries the other side if the first route is blocked. With no room, it uses its usual recoil and growl.
  Fire, shelter, and bodies that enter the route can cut the move short. A torch burn sends the wolf running off as soon as the recoil ends.
  A struck wolf runs thereafter (`Wolf.Aggressive`) and torch waves cease to scare it.
  After a missed bite it holds for the usual recovery, then runs out and around the player (`Wolf.Circle`, `WolfHunt.CircleOnMiss`).
- **Home.** From `WolfRetreatStarts`, `WolfRetreatHours` before dawn, every wolf drops its hunt and runs to the nearest den's door (`WolfMode.Home`), where it goes in; dawn clears any still out.
  A player a wolf had pinned goes free as it leaves.
  Patrolling, going home or losing its player (who sheltered or left the game) clears the wolf’s current attack, stance and repositioning (`WolfPatrols.Calm`). It still runs if it has been hit.

Campfire light repels wolves, and `CampfireHours` sets how long one log burns; a campfire with no wood left burns while workers sit at it.
A wolf hunting a player in campfire light uses the same orbit drawing from `WolfFollow` as a wolf scared by a torch swing.
The orbit is centered on the fire and stays beyond its light. A windup is stopped, but a leap already in flight finishes.
Inside a cabin (`SaveState.Sheltered`) the player is out of reach: their hunters go to its door (`DoorCheck.Follow`), or give up and patrol on away from a cabin they leave alone.

#### Cabin doors

`Doorways` (`src/cabins/Doorways.cs`) runs a cabin's door, which lets one player through at a time into a cabin that holds `Doorways.Places`, and `DoorTug` (`src/actors/enemies/wolf/DoorTug.cs`) a wolf tugging a player out of it; `World` holds one of each (`World.Doorways`, `World.DoorTug`) and ticks the doorway with the acting player.
A double click on a cabin, or its page's Enter button, starts a `CabinApproach` to `Doorways.DoorPoint`, using the same grid routes workers use to avoid obstacles.
The click's first press opens the page, which moves the world aside and can put the page's windows under the pointer, so `Game._Input` hands the second press to `PlayerControl.WalkIntoClickedCabin` before the interface sees it.
`WalkSearch` checks nearby building footprints and spreads long searches across frames.
If a failed search saw a layout that has changed, the approach tries again with the current buildings.
The panel highlights Enter while finding a path and during the walk.
A second press or Escape cancels the approach, and an unreachable door ends it.
On arrival, `Doorways.Enter` starts the doorway animation.
What a player does at a door is a `Doorway` stage on `PlayerState.Door`, timed by `PlayerState.DoorTime`; while it lasts they are occupied (`World.Occupied`), as when downed, and any action they try is refused with "You are busy." (`World.Busy`); inside a cabin they are not.
Picking a hotbar slot, putting a held stack back, marking the ending seen and skipping to morning are not refused.
Building keeps `Construction.DoorRoom` clear round every cabin's door spot, and a cabin needs dry, open ground there; stepping out and waking in a cabin put the player on it, or, for a cabin from an older save with it blocked, on the first open ground round the cabin (`Doorways.Doorstep`).

- `Entering` takes `Doorways.EnterSeconds`, or `Doorways.RushSeconds` for a player who sprinted there (`PlayerState.DoorRush`), then shelters them.
- A player who arrives while someone else steps in is `Following` until that one is inside, then steps in themselves (`Doorways.Ahead`).
  At the door of a cabin that is full (`Doorways.Inside`), locked with someone inside (`Thing.Locked`) or being stepped out of, they are `Knocking`: the door swings shut over `Doorways.ShutSeconds`, they step up to it, and from `Doorways.KnockLead` knock `Doorways.Knocks` times over `Doorways.KnockSeconds`, then stand free.
- `Doorways.TurnKey` locks or unlocks the door of the cabin the acting player is in, with `door_lock`; `Doorways.Leave` unlocks it first, and a cabin nobody is left inside is unlocked (`Doorways.UnlockEmpty`).
  A player wakes after death in the nearest cabin with a place left (`Doorways.HasPlace`), locked or not.
  Each arrival at a door is numbered (`SaveState.DoorArrivals`, `PlayerState.DoorQueued`), so whoever came to it first goes in first, and a wolf takes out whoever came in last.
- `Doorways.Indoors` is what wolves go by: inside, through the second half of `Entering` and the first half of `Leaving`.
- A bite or a leap landing on a player out in the open at a door (`DoorTug.TugOut`) has that wolf take them by the waist (`Wolf.Tugging`) and tug them back out along `Doorways.OutOfDoor`, `DoorTug.TugDistance` or as far as open ground runs with room for the wolf behind them (`DoorTug.TugDistanceFrom`), in `DoorTug.Tugs` tugs over `DoorTug.TugSeconds` (`Tugged`, or `TuggedOff` while knocking), the way `DoorTug.TugProgress` paces it, with one `tug_scream` through the tug.
  The world moves the player there when the tug ends; meanwhile the wolf stands `DoorTug.TugReach` behind them, or where it stood with no open ground there, and no other wolf leaps.
  A hit knocks the wolf back (`Wolf.Stagger`): the player stands free as far out as it got them.
  The wolf then lets go and bites no sooner than a second later; at half health or less it ends as the last tug lands (`DoorTug.Heave`) and it pins them, otherwise the player stands free where the tug left them, and goes in again only when they go for the cabin.
- While one wolf tugs, another that comes within `DoorTug.SideWithin` of the player takes the side it is on, or the other when that one is taken or has no open ground (`DoorTug.TakeSide`, `Wolf.Side`, a `BodySide`), one wolf a side.
  It bites once as it takes hold, or as soon after as its last bite is a second behind it (`DoorTug.BiteArm`, `Wolf.SideBitten`), stands `DoorTug.SideReach` to that side and bites no more (`DoorTug.Hold`) until the tug ends or it is hit.
  A wolf with no side to take, with both held, without open ground or not out of the door yet, waits at biting distance and snaps again once the tug has ended (`WolfSnap.Start`).
  A player a wolf takes out of a cabin has no sides to take until `DoorTug.OutOfDoorBy` into the tug, when they are out of its door.
  `TugView.ShowHolding` draws it reared up (`WolfModel.Rearing`, `WolfModel.Rear`) as steeply as puts its jaws on the player's forearm as their rig draws it, its forepaws on their flank and its hind paws on the ground.
  Each captured arm opens out at the elbow as the wolf takes its weight. With both arms held, the chest comes up.
- `Leaving` puts the player on the doorstep and takes `Doorways.LeaveSeconds`.
  Anyone knocking on that door goes `Aside` to `Doorways.AsideSpot`, a step along `Doorways.AsideWay`, and steps in once the leaver is through the doorway, in the order they came, while the leaver keeps `Doorways.MakeWay` to the other side; a player just standing at the door stays put, and the leaver ends `Doorways.ClearOf` beside them.
  The simulation moves them in along the side of the step (`DoorwayPath.Inward`).
  The view draws the two turning chest to chest as they pass (`DoorwayView.SqueezePast`).
  The one going in starts raising their right hand while they wait aside and keeps their elbow below the palm on the
  door frame (`ArmReach.Reaching`).
- A follower waits aside, out of the way, while the one ahead is tugged back out (`DoorwayPath.Follow`).

The simulation moves the player through each doorway stage (`DoorwayPath.At`), hurrying from the second tread.
The host sends the path's starting point and the current body position, and `DoorwayView.AtDoor` draws that position.
A tugged player faces the door, bends over and steps back with each tug (`ActorAction.Tugged`).
The wolf braces its forelegs and throws its weight back with its jaws on the player's waist (`WolfModel.Tug`).
On the two treads of the step and the floor (`StairSteps.StepEdges`) the body heads for the tread of the boot that bears its weight, rising onto the other boot's tread as one lifts off behind, and lowering ahead of a boot about to step down (`StairSteps.OnSteps`).
It gets there over several frames, carried from frame to frame in `ActorState.Lift`, never higher than a boot on the ground reaches down to its tread toe first, never lower than a leg bearing the weight alone bends, and never dropping faster than falling.
A knee bends to put a boot on a higher tread, so no boot sinks into the step; a boot a tug drags off a tread's edge hangs over the lower one while the body falls onto it.
Every takedown falls the player backward onto their seat and back (`ActorAction.FallBack`, `FallBackView.FallBack`, `FallBackView.FallPath`), facing the wolf after a leap; a leaping or pinning wolf puts its forepaws on the player's chest (`FeedingSpots.PawsOnChest`), astride with its hind paws wide of the legs, while the player's hands push its head off by the jaw within reach, or by the neck when the jaw is too close, elbows out at the sides (`WolfModel.NeckLine`, `ArmReach.ArmTo` with an elbow direction and `ActorRig.RollArm`), and every other wolf feeding there stands perpendicular to the body at the part of it nearest to it where it touches neither another wolf nor the player's arms (`FeedingSpots.FreeSpot`, `WolfModel.Volumes`), keeping that spot while it stays free.
Feeding wolves close their jaws low, rock back over their hind paws, then release and reach down for the next bite.
A wolf choosing a spot leaves room for its neighbor's present pose and the whole feeding pull.
A wounded player tugged out falls backward still facing the door, and the wolf lets go, steps round to the side of them it stands on, their left for one straight behind them (`Wolf.LungeFrom`), and lunges onto the throat from there as they land.
A player knocking on the door stands on the lower tread, with the front boot on the upper one, and steps back down off it when done.
#### Wolves at a cabin's door

`DoorCheck` (`src/actors/enemies/wolf/DoorCheck.cs`, `World.DoorCheck`) runs a wolf's door check at a cabin with someone inside, a `DoorCheckStage` stage on `Wolf.DoorCheck` at `Wolf.DoorCheckCabin`, timed by `Wolf.DoorCheckTime`.
A wolf whose player got inside goes there (`DoorCheck.Follow`), and so does a patrolling or called wolf within `SmellRadius` of such a cabin (`DoorCheck.Smell`), without a howl; a campfire lighting the door keeps both off it.

- `Coming`: the wolf nearest the door comes round to face it and goes straight up to `DoorCheck.ScratchAside` toward the free edge from `Doorways.DoorPoint`; the others wait `DoorCheck.WaitBehind` out from it, `DoorCheck.WaitApart` apart.
- `Scratching` lasts `DoorCheck.ScratchSeconds` with `door_scratch`.
  From a locked door every wolf there goes away, and a wolf with no way to a door gives up on it (`DoorCheck.GiveUp`); it leaves alone every cabin it gave up on (`Wolf.Tried`) until it has been out of smell range of that one.
  Until its snout is in the door a wolf turns to a player out in the open within `WolfSightRange`.
- `Prying`, `Opening`, `Staring` (with a growl) and `Launching` last `PrySeconds`, `OpenSeconds`, `StareSeconds` and `LaunchSeconds`.
  From `Prying` on the key does not turn, nobody steps in past the wolf, and nobody steps out from the first scratch on (`DoorCheck.Forcing`, `DoorCheck.AtDoor`).
- `TakingOut`: the wolf bites the last player who came in (`PlayerState.DoorQueued`), puts them at the door point as `Doorway.Tugged` and tugs them out as `DoorTug` does; the wolves that waited hunt that player.
- A struck wolf, an emptied cabin or a fire lit at the door ends the door check.

`DoorCheckView` (`src/actors/enemies/wolf/display/DoorCheckView.cs`) draws the door check from the stage and its seconds alone (`DoorCheckView.Clock`), so the world and `InsideDoorPOV` agree.
The cabin's door opens outward, so the wolf cannot push it in.
It stands in front of the door's free half to scratch, its right forepaw raking the boards once for each scratch of the recording, then shuffles round to face the gap the free edge opens and puts its nose to the seam.
Prying, it hooks its snout behind the edge and levers the door out toward itself in three pries, its nose following the edge (`DoorCheckView.Nose`, `DoorCheckView.NoseTo`); then it walks straight on along that line, in between the door and its frame, stands with its shoulders in the doorway looking at where whoever is inside watches from, the inside view's camera (`DoorCheckView.Watcher`, `WolfModel.LookingAt`), and springs in (`DoorCheckView.PoseAt`, `DoorCheckView.Placement`).
Its paws stay where they stand while the body moves over them and step one at a time, timed by how far the body has come, each paw's height running from what it left to what it lands on (`StairSteps.HeightAt`, `WolfModel.Stood`, `WolfModel.ReachTo`).
A paw stands on the highest tread under any part of its sole, from the heel to the tips of the toes (`DoorCheckView.Under`), so its toes are never in a tread's front.
Springing in, it starts from the height and tip it stands at in the doorway (`DoorCheckView.DoorwayLift`, `DoorCheckView.DoorwayPitch`); each paw stays where it stands as the body springs on until it lifts, the forepaws lifting flat before they fold and tucking under the shoulders clear of the floor (`DoorCheckView.TuckBelow`).
The door stands as far open as the wolf has levered and pushed it (`DoorCheckView.DoorDegrees`, `DoorwayView.PushDoor`): a table made once of the least opening that clears the hooked snout and every part of the wolf behind the door that is as high as the door's lower edge, which only grows and swings no faster than `DoorCheckView.SwingRate` until the launch.
On top of it the door rattles: at each pry it jumps a few degrees out off the snout in quick knocks that die away (`DoorCheckView.Rattle`), with `door_rattle` played `DoorCheck.RattleFrom` into each of the `DoorCheck.Pries` pries.
A player the wolf takes out is drawn from where they stood inside (`DoorwayPath.IntoCabin`).

A player inside the cabin sees the door from in there in `InsideDoorPOV`, a window three tenths of the screen wide and 72% of it tall in its left third, from `Prying` until the wolf has them; one left inside sees the other taken out for `WorldView.TakenOutSeconds` more.
Its picture is a cabin of its own in a viewport with its own world, lit only by dim moonlight through the door, where `WorldView.SyncInsideDoorPOV` copies the poses of the wolves at the door and of the player taken out as the world draws them (`ModelKit.CopyPose`).
The window draws that picture through the VHS shader.
`DoorKeyView` draws the round lock and the flat key in it that the cabin's page shows, seen from above and from the left, the key turning a quarter turn to the right from the first click of the lock's sound to its clack (`DoorKeyView.TurnFrom`, `DoorKeyView.TurnTo`) and lying across the lock while the door is locked.

The animation review's `player cabin` clip plays every stage at a cabin, `player jog back` and `player run back` a run braking and turning round, and `player jog turns` and `player run turns` the keys A, W, D and S pressed in quick turn.
The door opens while someone steps in or out: it swings with `hut_door_normal` after a walk, and slams shut with `hut_door_escaping` after a run.
A swing eases in and out over `Doorways.ShutSeconds`; a slam from wide open speeds up all the way into the frame over `DoorwayView.SlamSeconds`.
The slam's sound and a chest's or crate's `close_chest` start as far ahead of the shut as each variant's loudest moment lies into it (`Sounds.Lead`), so it lands as the door hits its frame or the lid shuts; a slam's sound that builds up longer than the slam starts while the runner is still in the doorway.

#### Swings

`Swings` (`src/actors/player/Swings.cs`) runs a player's swings at wolves.
`World.Attack` swings the best axe carried, or a fist without one, and `World.UseTorch` a lit torch, along the aim the player gives, with `Swings.Cooldown` between swings.
The swing lands `ActorAnimation.Contact` into its animation (`PlayerState.Swing`, a `Swing` counting down), not when it starts, and a player who went down or indoors meanwhile lands nothing.
It lands on every wolf whose body touches the ground it passes over then, a `SwingShape` seen from above, and that stands as tall as the swing comes low (`SwingShape.Low`):

| Swing | Shape | Passes over | Comes down to | Damage |
| --- | --- | --- | --- | --- |
| Overhead chop | `Swings.Chop` | 0.5 to 1.35 m ahead, 10° left to 8° right of the aim | 0.7 m | 2.2 to 3.2 with a steel axe |
| Sideways sweep | `Swings.Sweep` | 0.5 to 1.3 m out, from 138° left of the aim round the front to 62° right | 1.1 m | 2.2 to 3.2 with a steel axe |
| Torch | `Swings.Torch` | within 1 m, 52° left to 33° right | 0.65 m | 0.8 to 1.2 |
| Punch | `Swings.Punch` | within 0.7 m, 10° to 35° right | any height | 0.4 to 0.6 |

The shapes are where the player's model carries the axe head (`ActorRig.AxeHead`), the flame and the fist through each stroke, which `CheckSwingShapesFollowThePlayersModel` holds them to.
A wolf's body is `Swings.Touches`' line of round sections from the root of its tail to its nose, the way it faces (`WolfBodies.Body`), as tall as `WolfBodies.Top`: `WolfBodies.StandingTop` on the ground, up to `WolfBodies.LeapRise` more through a leap for the throat, and `WolfBodies.RearedTop` reared up with an arm in its jaws, as its model stands.
So the sweep passes over a wolf on the ground and catches one in the air or reared up.
Each wolf touched takes its own roll of the damage, the swing's own give or take its spread (`Swings.AxeDamage` times the axe's speed, `AxeSpread`), and is knocked back and left reeling (`World.Strike`).

#### Torch

A lit torch (`SaveState.Torch`, burn seconds left) burns as long as one log, only while `SaveState.TorchLit`; it stays in the backpack, burning out removes it, and a backpack with no torch left puts it out.
Lighting it takes `World.KindleSeconds` (`SaveState.TorchKindling`).
Swings share the axe cooldown and flare the light (`World.TorchFlare`).
They put the torch out after `World.WildSwings` within `World.WildWindow` seconds.

While the torch burns and fewer than `World.PackSize` wolves are near:

- an unscared wolf creeps in at a third of its speed and can bite;
- a swing aimed within about 45° of a wolf in `World.ScareRange` puts it in follow mode (`Wolf.Follow`, a `FollowStage`; `Wolf.Following` while it backs off, circles or stands braced).

Once the pack is complete, swings no longer scare and the wolves attack.
A torch that lands on a wolf sets `Wolf.Flee`; it leaves alone one in the air or feeding.

**Follow mode** (`WolfFollow`) is what wolves a torch scared off do while it burns:

- **Backing off.** Every wave that reaches a wolf, a first one or one at a wolf already following, sends it walking back out to its circle (`FollowStage.BackingOff`), however near the player it was.
  Until it is `WolfFollow.BraceFrom` off, the rules below wait, unless the player comes after it: any step of theirs toward it (`Wolf.Watched`) makes a wolf with no other follower in their line of sight run as in **Alone**, and makes one among others stand its ground at once, as in **Among others**.
- **Circling.** They walk round the player about `World.StalkDistance` off without biting or leaping, never faster than a walk (`WolfPatrols.PatrolPace` of the trot), each the way round it was given at random as it began to follow (`Wolf.Round`).
  Wolves going the same way spread evenly: `WolfFollow.Arrange` orders them by bearing and turns that way's places to fit them, and each heads a little ahead of its place, going round the player and never across them.
  It heads along the line touching its circle where it stands, so going round never takes it nearer the player, where it would brace.
  The two ways walk in lanes a little outside and inside the circle, so wolves going opposite ways pass each other, and each wolf's place drifts ahead and back and its lane in and out at its own pace by its id; all of it stays within `WolfFollow.Wander` of the circle.
- **Bracing.** One the player comes within `WolfFollow.BraceFrom` of stops and stands braced (`FollowStage.Braced`) until they are `WolfFollow.BraceUntil` off again.
- **Alone.** At `WolfFollow.StandOrRunAt` a wolf with no other follower in the player's line of sight runs (`FollowStage.Routed`) at a gallop until it is out of the player's line of sight, or for `WolfFollow.RoutSeconds`, then patrols again (`WolfFollow.GiveUp`).
  It leaves that player alone until it has been out of smell range of them (`Wolf.Shunned`), as a wolf does a cabin it gave up on (`Wolf.Tried`).
- **Among others.** With more than one wolf following within the player's line of sight, the one walked at stands its ground: it growls every `WolfFollow.GrowlEvery` from `WolfFollow.GrowlFrom`, and at `WolfFollow.StandOrRunAt` every wolf hunting the player within their line of sight turns on them (`FollowStage.Bold`).
  Each attacks as a wolf does with no torch lit, leap for the throat included, and no torch wave scares it again; one out of sight keeps following.

#### Mauling and wolf meat

At half health or less, an unprotected player can be pinned by a wolf that lands a throat leap.
The windup and fixed bearing work as in a snap. This leap can start further away (`World.LeapRange`) and stays in the
air for `World.LeapSeconds`.
A miss leaves the wolf recovering on the ground. Contact knocks the player onto their back and the wolf stands over them.
The pinned state (`SaveState.Mauled`) blocks movement and actions and drains health while the pack feeds.
The death that follows strips food from the recovery box, as does a death with a hunting wolf within `LeapRange`, such as one bitten down behind a torch.

Killed wolves leave a `wolf_corpse` resource that yields raw meat, which the stone furnace cooks into edible meat.

### Resources

`ResourcePanel` shows gatherable items in read-only slots with live stock counts.
Leaf spots show a second slot for acorns, and the lake shows a water droplet in an unnumbered slot.

- The valley starts with a leaf spot beside each oak where ground allows it (`World.LeavesPerSpot`), refilled every dawn.
- Loose stones and sticks (`loose_stone`, `stick`) sit on the baked scenery spots `Scenery.Loose` picks; `World.ScatterLoose` lays them when a world is made, and the view dresses the remaining spots with plants. Each gives 1 stone or 1 wood and does not come back.
- `World.Chip` drops a loose piece within `GatherRules.ChipReach` of the target on `GatherRules.ChipChance` of
  successful player or worker hits. A pickaxe chips rock and ore. An axe drops an acorn from a tree.
- A felled tree is taken out of the world and its stump (`stump`, `World.StumpWood`) put in its place by `World.Fell`, as a thing of its own with a new id that keeps the tree's id in `Thing.OfTree`.
- Plain bushes (`bush`, `World.BushLeaves`) grow on the scenery spots `Scenery.Bush` picks (`World.GrowBushes`), away from the clearing and the mountain feet.
- A stump or a bush is dug up whole (`GatherShape.Dug`, `Thing.IsDug`): digging adds to `Thing.Progress` at the shovel's speed until `GatherRules.DigSeconds`, then `World.Unearth` moves all it holds to the digger and removes it with a `dig_up` sound. The player's backpack must have room for all of it (`World.GatherBlock`), and a worker's hands are sized for it.
- Stumps and bushes never block building (`World.BuiltOver`): a new building, yard or crate clears away those on the ground it keeps clear of other resources (`World.ClearGround`).
- A berry bush holds `World.BushBerries` at the start and each time it fruits again, which is `BerryRegrowHours` after the first berry was picked from it, picked clean or not.
- Tin sits at the foot of a third cliff further inland. Like iron, it needs a pickaxe.
- Tin ore smelts into tin bars at the stone furnace, the anvil hammers a tin bar into 2 tin sheets, and a tin sheet
  makes 4 cans at the bench.
- The stone furnace burns 2 wood into 1 charcoal.

### Sand and tides

A shovel highlights the dry beach square it points at.
The held dig uses the same reach and work as digging a planting hole.
The server checks for dry sand and room for all 10 units of sand before starting and finishing.
A completed dig strips a one-meter square.
A rounded rim and sloping walls show the depth of the cut, with the compost pile's soil texture at its bottom.
Cuts that share an edge join across a continuous floor. The remaining rim rounds turns in either direction.
Gravel laid over a cut hides it until taken up. The tide still refills the cut while it is covered.
The view shows less of the floor behind the near rim at shallower camera angles.
Only the tile index is saved and shared with guests.
The sea recedes slightly past its original waterline at the new moon and covers the sand strip at the full moon.
The day dial shows the moon's lit side waxing and waning over the same eight-day cycle.
The tide clears each stripped square as water covers it. The low-tide waterline bounds walking throughout the cycle.

### Replanting trees

Selecting a shovel on the hotbar or double-clicking it in the backpack shows the placement outline.
Esc leaves the outline active while the shovel is selected on the hotbar.
Holding right mouse on open ground walks the player to the spot.
`GroundHoles.Begin` starts digging once the player is in reach.
The outline shows the target while held and lives in the view.
The server counts shovel work for each hold.
When enough work has accumulated, `GroundHoles.Finish` places a `Hole` on open ground away from sand if the player is still holding right mouse.
Releasing early discards the work.
The hole has one empty input slot.
Inserting an acorn spends one from the stack and replaces the hole with a selectable oak nub.
Empty holes disappear at dawn.
The remove selector, buildings and gravel paths can clear an empty hole.
`ResourcePanel` shows the wood count in a read-only item slot throughout the tree’s life.
Its status is **Planted** with a water slot until it takes five liters, then **Growing** until it becomes an **Adult tree**.

The full five liters start growth and are used up.
`Thing.NextTreeStageAt` holds the next date on the world clock and `TreePlanting` checks only trees ready to grow.
`Thing.TreeStage` records the completed days through saves, network updates and fog memories.
A replanted oak grows linearly for ten full days, then becomes an adult and puts down one leaf spot next to its trunk.
Size and total wood for an adult use a common logarithmic curve, so daily gains taper with age.
Each day adds the increase in whole wood units since the last, so it preserves wood already chopped.
The valley's starting trees first grow at evenly spaced times over one day.
If a clock jump leaves many trees due, growth work is spread over several ticks.
A growing trunk destroys buildings whose footprints it overlaps.
Cabin occupants are moved outside, stored items go to a recovery box, and any cart still on the road delivers its order.

`LeafSpots` gives each leaf spot a capacity of one or two acorns.
Each spot keeps its tree's id and growth stage. Its ground area widens with the tree and stays at its last size after felling.
When a player gathers leaves, they also take the acorns that fit in their backpack.
Leaves refill at dawn. Missing acorns refill one at a time, every ten days from the first gather that took one.
Chopping out all its current wood fells the tree and leaves its stump.

### Tools

Tools carry a `Tool` kind (axe, pickaxe, shovel, hammer, chisel, rake, mortar), a `Speed` and a `Tier` in `GameTables`:

| `Tier` | Material |
| --- | --- |
| 0 | wood or stone |
| 2 | steel |

Tier 1 is unused, since there are no iron tools.

`World.BestTool` picks the fastest carried tool at or above a tier.
Recipes name the tool they need, the lowest `Tier` that will do, and whether they can be made by hand (`Hand`).
So a steel chisel does everything a stone chisel does, faster, while the anvil, tin cans and gravel need a steel hammer
and the mortar a steel chisel.

`GatherRules` holds every resource's rule in one table: the tool it needs (an axe for trees, a pickaxe for boulders and deposits, a shovel for stumps and bushes), one that only speeds gathering up (a rake for leaves, which take three times as long by hand), the loose piece a pickaxe chips off it, and the job it belongs to (`GatherRule.JobName`).
A job is every spot yielding the same item unless a rule names its own: stumps go with trees and sticks as wood, while bushes are apart from leaf spots.
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
At a station someone has to work, a batch a player queued moves while a player or a paid worker works it; a batch for the mark moves only with a worker (`World.Hammered`).
`Thing.Batch` says who the batch in progress was started for (`BatchFor`), and `World.PlayersWork` says whether players work it: only a queued one takes a carried tool or shows the player hammering.
The packing station has no queue: whoever works it packs.

A station counts batches of one recipe at a time: `Thing.Asked` is the recipe a player queued last and `Thing.Batches` how many of it still wait; a batch leaves the count as it starts.
`Thing.Keep` is the recipe workers keep it making once those batches are done, and a building's `Recipe` in `GameTables` names the one it starts with.
`Thing.Making` is the batch in progress, as `PlayerState.Making` is by hand.
`Thing.Recipe` is worked out, not stored: the batch in progress, else `Asked` while batches of it wait, else `Keep`; with none the station stands idle.
`World.Selected` is the recipe the station is on, whose slots its page draws and whose card is outlined: `Recipe`, else `Asked`, else the first recipe on its board.
A station is always on one: taking the mark off with no batches waiting leaves the marked recipe in `Asked`.
So a station with nothing to make keeps showing its slots, and more batches of that recipe are not a turn to another.
Saves and world updates leave out an empty mark, count and batch, so a tree or a rock carries none of them.
`World.QueueBatch` adds to the count, and for a recipe other than the selected one it first turns the station to it:
it drops the batches that waited and moves to the acting player's backpack what the new recipe has no use for: in `Thing.Inventory` all but its inputs, and the old product even where the new recipe uses it, and in the fuel slot (`Thing.Burner`) a fuel it does not burn (`World.Stays`).
It then loads from the backpack only what the slots still lack for the batches queued, where more of the same recipe loads a whole batch on top.
It always puts the mark on the recipe queued.
`World.SwitchBlock` refuses that turn while a batch is in progress or while the backpack has no room for what leaves (`Inventory.Fits`).
So batches only wait for the recipe in progress when there is one, and `World.Deserialize` refuses a save where they wait for another.
`World.Unqueue` takes waiting batches off the count.
`World.SetRecipe` moves the mark, and a move to a recipe other than the selected one turns the station to it and drops the batches that waited; `World.MarkBlock` refuses that move while a batch is in progress or `Thing.Inventory` holds anything.
So a station with a mark is on its marked recipe, and `World.Deserialize` refuses a save where batches wait for another.
A station takes inputs for the selected recipe and the kept one, and haulers carry out only what neither takes.
A station starts no batch while its slots hold something that is neither such an input nor what its recipe makes (`World.Blocked`), as when a hauler delivers after the station has turned.
What no storage takes stays in the station, which goes on working until its output stack is full.
The output slot of the recipe the station is on takes back by hand what that recipe makes, one stack at most, as every station slot holds (`StationSlots.StationSlot`).
Every move by hand into a station's slots goes through `World.HoldCheck`, which lets in only an input of the recipe it is on or keeps to, a vessel holding a liquid input, or what the recipe makes (`World.SlotsTake`); its fuel slot takes what `World.FuelBlock` allows.
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

The recipe map badge on the bottom bar opens `RecipeMap` over the running game.
An item link in the Goal Log opens it through `RecipeMap.OpenAt` with that item highlighted and the game still paused.
Closing the map returns to the goal details.
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

`BuildingDef.ToolSlots` names the tool slots a station has (`Thing.Tools`), each with the tool it takes
(`ToolSlotRule`): a chisel and a hammer at the bench, a mortar at the cooking table (`cooktable`) and a steel hammer at
the anvil.
`GameData` checks that a station has a slot for each tool its recipes need, taking the plainest one any of them asks for.
`World.SlotBlock` refuses a tool of the slot's kind below its tier, both for `World.SlotTool` and for a move by hand.
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
Its recipes need a steel hammer, slotted or carried by a player working it, and run at its speed.
It hammers a steel bar into a steel part or two steel sheets, and a tin bar into two tin sheets.
The containers are made of sheets.
A smith stays with a running batch and fetches the next load once it is done.

#### Furnaces and steel

The stone furnace smelts ore over coal or charcoal, and cooks and makes charcoal over wood, coal or charcoal.
The reinforced furnace (`reinforced`) burns coal only.

Steel recipes consume iron bars and coal.
Steel only progresses while the heat is at or above the clear bar (`World.SteelHeat`, shown as 1450 °C through `World.Celsius`).
A batch below it pauses, keeps burning one coal from its fuel slot per batch length while cold, and resumes when reheated, with no failure.
Once an active crew has the forge hot, it never falls below the bar while they are on duty.

### Taking buildings down

`Construction.Dismantle` removes a building in reach and puts its cost, a supplied mine's materials and everything its boxes hold into the backpack, and the rest into a recovery box where it stood.
`Construction.DismantleBlock` keeps up a cabin someone shelters in and a trading post with a cart out.

The building plans' last cell (`PlansPanel`, named `remove`) sets `ClickMode.Remove`, the remove selector.
`Construction.RemovalAt` finds a `Removal` target under the pointer.
On the surface it picks a building first, then a gravel path tile (`GravelPaths.TileUnder`).
Underground it picks a visible steel pipe.
For a target within reach (`Construction.InReach`), `RemovalMark.Update` marks the model or tile in red.
A click runs `Construction.Remove` at once.
Right-click switches between the surface and underground.
Esc exits.

### Gravel paths

A bench recipe (`gravel`, a hammer of tier 2, not by hand) crushes 1 stone into 1 gravel.
Each hammer blow on it plays `mine_ore`, the recipe's own sound (`RecipeDef.Sound`), in place of the bench's knock.
The gravel path is a building plan (`ThingKind.GravelPath`, 4 gravel) but never a thing: `Construction.Build` hands it
to `World.GravelPaths` (`src/paths/GravelPaths.cs`), which keeps the laid tiles.
A tile is a map tile, centred on whole coordinates like a building, saved as its place in the map's rows in
`SaveState.GravelPaths`.
`GravelPaths` keeps one flag a tile beside that list and brings the flags in step whenever another list takes its place:
a new game, a load, or a guest's update that brings other tiles.
The tiles have an update entry of their own (`EntryKey.Paths`), so only an update that lays or takes up a tile carries
them.
`GravelPaths.Revision` moves only when the tiles change.

- `GravelPaths.Block` refuses a tile only on water or cliffs, on a tile already laid, on a tile where a resource stands,
  with or without anything left on it (not a loose piece, a stump or a plain bush), out of the 5 tiles a building may
  be placed in, and without the gravel.
  Buildings and yards do not count either way, and `Construction.PlacementBlock` never looks at tiles.
- `GravelPaths.Lay` clears a stump or plain bush off the tile (`World.ClearTile`).
- `GravelPaths.TakeUp` (`Verb.TakeUpPath`) gives the cost back through `Construction.GiveBack`, which `Dismantle` also
  uses: into the backpack, the rest into a recovery box.

`GravelOutline.Distance` is the one definition of where gravel lies: within `GravelOutline.HalfWidth` (0.375 m) of the
paths' middle lines.
Those are each laid tile's middle, a line between the middles of tiles side by side, and a line between two tiles corner
to corner, whatever lies on the tiles between them; four tiles in a square count whole.
So a turn's inside corner is cut on the slant and a staircase of tiles has straight slanted edges.
`GravelPaths.Covers` reads it for the simulation and `GravelPathView` for the picture, so walkers are faster exactly
where gravel is drawn.
Workers step `GameData.GravelSpeed` (1.2) times as far in `World.Walk` when they stand on gravel, and
`PlayerControl.Walk` moves the player the same way; routes are found as before.
Data validation keeps `GravelSpeed` below `WolfChase`, so a walk on gravel never outpaces a chasing wolf.

`GravelPathView` (`src/paths/display/GravelPathView.cs`) keeps a map of 8 texels a tile holding how far inside the
gravel's edge each spot is, a half at the edge.
It redraws only the tiles within two of one laid or taken up, when `Revision` moves.
The terrain shader draws the gravel photoscan where the map passes a half, with stones that stand higher reaching a
little further, and the plants' shader shrinks plants there to nothing.
The scan is the models' `Scan.Gravel` at the same size, so the gravel heap, the plan's ghost (`GravelPathShape`) and the
ground match.

### Steel pipes

`World.SteelPipes` keeps the underground pipe tiles apart from things on the ground and gravel paths.
`SaveState.SteelPipes` records each occupied tile's place in the map's rows, pipe shape and rotation in radians.
The tiles have a network update entry of their own (`EntryKey.Pipes`), so the guest receives them on joining and when
they change.
`SteelPipes.ConnectionMask` marks tiles that share a side and have pipe ends pointing toward each other.
A straight has two opposite openings, a tee has three, and a cross has one on all four sides.
Pipes that only touch at a corner are separate.
Each quarter-turn rotates the available openings.
The underground view darkens the surface and exposes an earth bed 0.9 m below grade.
Blueprint-blue lines mark the trench edges, pipes and inlets.
A pipe and its vertical inlet turn dark blue while the tile holds any water, even at zero pressure.
Ground remains visible under people standing over the trench.
The Layer button below the left side of the status bar switches the view between the surface and underground.
In the underground view, a click opens the laid pipe's page (`PipePanel`).
Placement and removal project the pointer onto the bed.
Inspection tests each tube and riser so bare ground between junction arms stays unselectable.
A vertical pipe joins each building with plumbing to its inlet tile.
A dotted guide marks a connection still to be made.
A pump or a building in `Plumbing.HasPlumbing` attaches to the pipe under its center.
The building's Plumbing slot accepts ten steel pipes to install its plumbing.
Explored ground remembers the building's visible plumbing when it leaves sight.
A worker finishes a round at the end of each pump stroke, then checks whether a higher-priority job is ready.
A pump stroke holds pressure while the handle moves.
Once it stops, pressure falls by ten percentage points per second.
The simulation integrates flow over each tick, including the moment pressure reaches zero.
The pressure dial shows the pump's flow from 0 to 5 L/s, with half-liter ticks.
`Plumbing` finds connected groups whenever the pipe layout changes.
Each running pump fills its own pipe first, then the connected pipes outward from it, up to one liter per tile.
Connected buildings share the remaining water evenly.
When a tank or recipe fills, its excess goes to other buildings that can take water.
When pumps stop supplying water, consumers can still draw the water left in the pipes.
Each pipe's water is saved and sent to guests separately from its shape and rotation.
Taking out a pipe spills the water it held, while the two sides of the line keep theirs.
`World.PipeReading` reports a tile's standing water and the highest pressure of the pumps connected to it.
A receiver with room keeps any fraction below a liter in `Thing.PipedWaterRemainder` until it can store a whole liter.
A cooking table requests water for the next queued or marked batch.
A batch already being worked has spent its water.
A farm with plumbing takes its share in every crop stage.
The sprinklers top up the 10 L soil reserve and use any excess.
`Thing.ReceivingWater` switches the spray on for each tick that delivers water, including standing water from the pipes.
The spray stops as soon as delivery stops.
Growing crops continue to draw on the soil reserve.
`Construction` snaps the Steel piping plan to tile centers and lays it in the pipe layer.
In pipe placement, R turns the plan a quarter turn and T cycles its shape.
Left-click lays the pipe.
Right-click takes up the pipe under the pointer, even after the backpack runs out of pipes.
The underground remove selector also takes up a pipe within reach (`Verb.TakeUpPipe`).
The plan's materials return to the backpack or go in a recovery box through `Construction.GiveBack`.

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
- Right-clicking open ground uses the selected hotbar item (`Hotbar.UseSelected`).
  Right-clicking a resource starts gathering, and holding the button on a placed crate or barrel starts picking it up.
  Right-clicking a hotbar slot uses the slot's item.
- A double click uses an item in the backpack or on the hotbar (`PagePanel.UseFromBag`, `Hotbar`): eats, drinks, lights a torch, opens a crate as storage or selects a tool (`Hotbar.SelectTool`): on its hotbar slot, or in hand (`World.SelectInHand`, `PlayerState.InHand`) with no slot selected.
  On a slot with a use, a single click waits `SlotInput.DoubleClickMs` (200 ms) before it picks up, so the two can be told apart; recipe cards use the same time.
- A slot acts when the button comes up over it, as a plain button does, so a page that draws itself again never takes the slot away under a press; let go of anywhere else, it does nothing.
- A held stack put on a hotbar slot binds that exact backpack stack (`Inventory.Slot.Hotbar`, `World.BindHeld`), which the backpack rims; the binding goes with the stack and follows it into a stack it merges with (`Inventory.Slot.Empty`, `MoveSlotTo`, `Sort`).
  `PlayerState.HotbarSlots` holds plans, and items a slot waits for until `World.BindWaitingHotbars` binds the first stack of one to come in; `World.HotbarItem` and `HotbarStack` read a slot.
  A stack spent to nothing (`Inventory.Remove`, unlike a move out) notes its slot in `Inventory.UsedUp`, and the slot waits for its item again, so it binds the next stack at once.
  Ctrl batches from the backpack drawer and Store all carry the mark "spares only" (`World.FromSpares`, `Command.Spares`, `Inventory.SparesOnly`), which leaves bound stacks behind.
  Pouring from vessels with Ctrl includes hotbar-bound vessels and preserves their bindings.
- Esc, a click on open ground or on the interface anywhere but an item slot, or closing the side panel lets go of the held stack (`HeldCursor`).
- A held crate, barrel or building plan over open ground goes into placement, and back to the cursor over the interface (`PlayerControl.PlaceHeld`).
- A held stack put on storage clicked in the world goes into it; an item only selected on the hotbar never does.
- Bulk buttons and recipe batch actions say what they move.

A held stack never leaves its slot until it is put down (`Inventory.Slot.Held`, `HeldBy`).
Totals still count it, but `Inventory.Remove`, `TransferTo` and `Has` take only units nobody holds, so other players, workers and haulers cannot take it, and a station holding one starts no batch (`World.Stocked`).
Another player who tries is told so (`World.HeldByAnother`).
The server keeps only the slot each player's held stack lies in (`PlayerState.HeldSlot`), and builds the `PlayerState.HeldStack` a window shows from it each time it sends the player (`World.ShareHeldStacks`).
It lets go of the stack when the player is down, walks out of reach of the building it lies in, or leaves, and when its crate is put down or sold; a load starts with nothing held.

A worker's hands (`Worker.Bag`) work like a crate: `Worker.BagStack` (12) items in all, one stack or up to twelve kinds, for every load.
A barrel or a packed crate fills them alone, one at a time (`Inventory.CarriedBy`).
The one tool a worker holds lies in `Worker.ToolSlot`, which `Worker.Tool` names.
Both are boxes a player in reach of the worker (`World.Reaches`) picks up from and puts into as with a building's slots: anything goes into the hands, only a tool into the tool slot.

#### Chests

Chests have separate item allowlists for input and output.
Storage with a filter window has `Inventory.Gated` set: there an empty list lets nothing through and `Inventory.Everything`, shown as an "A" cell, lets every item through.
`Inventory.Filter` keeps `Everything` alone in its row.
`World.Add` gives a chest and tool station an empty input list and `Everything` as output (`Inventory.GateNew`), and a pile, barrel and trading post empty lists; every crate's contents get the chest's lists when made (`World.NewCrate`), packed or not.
Any other box, such as a station's slots or a worker's hands, is not gated, and lets anything through its empty lists.
Automated deposits go to the nearest place that asks for the item, then to the nearest storage taking everything (`World.DepositFor`).
A load nothing takes goes back to the nearest building whose output filter lets it out, up to one stack of it (`TakeBack`, which `World` holds): storage by the output filter set on it (`Inventory.AllowsWithdrawal`), and a building with output slots by what it outputs, worked out from the building rather than stored (`TakeBack.Outbox`): a station its recipe's product, a farm plot its yield, an open mine its ore and stone (a mine's output shows only once it opens), a packing station crates of its good.
A station's slots hold inputs and leftovers of other recipes as well as its product, so a stored output filter there would also stop haulers carrying the leftovers out.
With no taker found, a worker looks again every `JobListSeconds` (`Worker.LookAgain`) rather than every tick.
With none, the load stays in hand while the worker does only the jobs that need no hands: the job table's `JobDef.Hands` says which jobs carry loads (`World.NeedsHands`).
Asking for it are a chest, pile, placed crate or trading post whose input filter names it, a packing station packing it, a station whose recipe takes it as an input or burns it, a farm plot short of it for a sowing and a mine site short of it.
`World.Intake` gives each kind of place's one answer to which of its boxes takes a carried item now, and how many of it.
Automated withdrawals respect output filters.
Manual transfers ignore those filters.
A filter is a `FilterList`: its entries in the order they were named, each once, which the filter window's rows show.
The window's item list runs in `GameData.ListOrder`, with crates of goods after the items in their goods' order.
`GameData.Create` works `ListOrder` out once: what is gathered, grown or bought comes first, then materials, tools, containers and food, by the recipe that makes each item.
An item no recipe makes goes with the item it comes out of, so water goes with the empty bottle; a steel bucket or barrel at any fill stands with the empty one (`ContainerItem.Base`), and a part-drunk bottle with the full one.
In each group fewer making steps come first, and recipe map order breaks ties.
Making steps count the longest way through a recipe's inputs, or the items a source comes out of and also takes, from items that take no other item, such as wood.
Gathered water used to grow wheat adds no making steps.

#### Piles

Piles (`pile`) are 3×3 yards (`Construction.IsZone`, `ZoneSize`) that do not block walking.
`Construction.Snap` puts a new yard on the grid of the nearest yard within reach, and `ZoneBlock` lets yards touch but not overlap.

A pile takes part in automation like a chest.
Its input and output filters name the same one `World.PileEntry`: an item, a kind of crate (`World.MixedCrate` for crates of mixed goods), or a vessel's liquid for every fill of that vessel holding some, apart from the empty vessel.
A pile with none named takes nothing from workers.
The player sets it on an empty pile through `SetFilter`, or `World.PileFilters` sets it on a tick from the first resource put in by hand.
A pile's filter never holds `Inventory.Everything`.
It stays when the pile empties, and `SetFilter` and `ClearFilters` refuse to change it while the pile holds anything.
`HoldBlock` refuses any other resource, including for manual transfers.

#### Compost

`World.SetCompost` turns only an empty pile with no filter into compost.
A compost pile (`Thing.Compost`) instead takes `Compostable` items (leaves, grass and anything with `food`) and gives `fertilizer`.
It turns `CompostBatch` of them into one fertilizer each in-game hour.
It keeps the player's filters rather than setting its own, and `Thing.SkipFood` keeps workers from delivering food to it.
`DepositFor` ranks compost last for food, so harvests and berries go to storage first.

#### Crates

A wood crate holds `CrateCapacity` (12) units.
A picked-up crate becomes an item `crate_<n>` whose contents live in `SaveState.Crates`; an empty one is the stackable item `crate`.
Those contents have an update entry of their own (`EntryKey.Crates`), sent only when a packed crate changes.
`World.MoveInCrate` moves items between a packed crate in the backpack and the backpack, and renames the crate after what it then holds under the same number; emptied, it becomes a plain `crate`.
A crate of one good carries the good's id in its name, `crate_<good>_<n>`, so filters can name the kind of crate (`World.CrateOf`, matched in `Inventory.Accepts` through `World.PackedKind`).
`World.PackedKind` gives `World.MixedCrate` for a crate of mixed goods, which only a pile's filter can name.
A pile of crates holds crates of one good, or crates of mixed goods.
A placed crate takes part in automation like a chest, and its filters stay with its contents while it is packed.
No worker carries a crate into one, and its filters name none (`World.Nests`).

#### Packing station

A packing station (`packer`, `src/packing/Packing.cs`) packs the good in `Thing.Packs` into crates, anything a crate holds but crates and coins.
Its inventory applies the usual station limit to each input, the chosen good and empty crates.
Tools each take their own storage slot, with room for up to `CrateCapacity` tools.
`Thing.Output` holds the crate being packed.
Player transfers and worker deliveries use the same input limit.
Any excess already in a save can still be used or removed.
While worked like an anvil, it moves one item every `PackSeconds` into that crate, opening an empty crate for the first.
A full crate, or one of another good after the choice changes, waits in the output.

- Haul carries the crate to `DepositFor` and takes the station's leftovers to storage.
  It brings the chosen good and empty crates up to their input limits (`PackerHaul`).
  A batch is one crate.
- `DepositFor` counts the station as storage for its good, ranked with explicit filters, and accepts deliveries until the chosen good reaches its input limit (`World.PackingRoom`).
- A crate packed full is a round of the Craft job.

Putting a packed crate into a placed crate asks for confirmation.
If confirmed, it ends the run in a black hole with a reload or main-menu choice.

#### Tool station

A tool station (`toolstation`) holds tools, buckets and bottles in `World.ToolSlots` (4) slots.
Its rack hangs whatever each slot holds.

### Workers

Workers are tracked individually.
`HireOffers` in `GameTables` lists hiring deals, and each worker keeps the daily wage of the deal they were hired on (`Worker.Wage`).

#### Jobs

The kinds of work are `JobKind`, each with an id in saves and commands (`JobKinds.Id`), and one table holds each job's title, hint, whether a round of it is a unit of the place's work, whether it carries loads in the worker's hands, the tools it takes and what it does (`World.Def`, `JobDef`).
A place offers some of them (`World.Jobs`), such as Craft and Haul at a crafting bench.
A job that several kinds of place offer is a class in `src/jobs/` that `World` holds: `Hauling`, `Gathering` and `Crafting`.
A place with its own way of doing it extends it in its feature's folder: every place that offers Haul has a `PlaceHaul`, the lake gathers through `WaterGathering` and a packing station is worked through `PackCraft`.

- Haul at a chest or placed crate carries out and fills it by its filters ([hauling](#hauling)).
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
With none to do the worker sits at the nearest campfire as at lunch (`World.Idle`, `BreakSeats.Rest`), or with no campfire waits at the nearest trading post, shows the first job's status and reads the list again every `JobListSeconds`, or at once when the list changes.
A tool none of their jobs uses goes back to a tool station first (`World.JobUses`).

`World.MoveJob` moves an entry from one `JobSpot` to another: a place in the list, or a place among a group's hauls.
Only a haul goes into a group, and a group moves with its hauls.
`World.AddHaulGroup` adds an empty group, and `RemoveHaulGroup` puts its hauls in its place as jobs of their own.
A haul group counts as one job, and its next carry is picked across all its places.
A command names the entry it moves (`World.JobKey`), so a move sent before the list changed is dropped rather than moving another entry.

On a worker's page `JobList` draws the list and turns a row dragged by its `JobGrip` into a `MoveJob`; each row shows what its place is set to make (`JobList.Made`).

#### Hauling

A haul job carries one load at a time (`Hauling.Haul`).
Each kind of place offers its carries through its own `PlaceHaul` (`StationHaul`, `PackerHaul`, `FarmHaul`, `MineHaul`, `StoreHaul`, and `BarrelHaul`, which offers none), and the best is the hauler's plan (`Worker.Plan`, a `Carry`) until the load is picked up:

1. an input or the fuel slot holding less than one batch, and fuel the recipe does not burn, which leaves the slot;
2. an output that is full, or anything another recipe left in the slots;
3. the input or fuel covering the fewest batches;
4. the output holding the most.

Inputs and fuel fill to a stack (`Inventory.SlotRoom`); a farm plot's slots hold one sowing (`Farms.SowRoom`) and a mine site what it lacks.
A load of an item the recipe takes both as an input and as fuel is shared between the two slots as a batch uses them (`World.FuelSplit`).
Spare seed in a plot's output that no storage takes stacks up in the plot's own seed slot.
A load carried out of a place is as much as the place taking it has room for (`PlaceHaul.Taker`), and is sized again at pickup (`Hauling.Takes`), so none is left in hand when the place filled meanwhile.

A chest's or placed crate's hauler carries out what its output filter lets go to the nearest place asking for it (`World.AskerFor`), then fills it with what its input filter names (`StoreHaul.FillSource`, never for `Everything`); every such carry comes under the first rule.
Storage that asks for an item never passes it to other storage asking for it, nor takes it from one, so two never trade it back and forth.

#### Gathering and the bellows

A gatherer works at the player's pace, `World.GatherYield` pieces a cycle, until their hands are full (`Worker.Loading`), then delivers.
A gatherer whose spot is spent moves the duty to the nearest spot of the same job in `SaveState.Remembered` that still holds something (`World.NextSpot`): seen by any player, even if now under fog, never under black fog.
Spots that need a tool count only while the worker can get one (`World.CanWork`), and loose pieces come only after every other spot; a gatherer with no tool to be had for their spot goes to the nearest spot they can work at once (`World.OtherSpot`).
Woodcutters skip trees that have yet to become adults.
A woodcutter who fells a tree takes its stump as their spot while a shovel is to be had, without waiting for a player to see it; other stumps count once a player has seen them.
A worker carries one tool, so going between a tree and a stump takes a walk to a tool station to swap.
With none known they say so once, carry off what they hold and go on to their other jobs.
A worker with no duties sits at the nearest campfire, or with none waits at the nearest trading post.

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
`BreakSeats` reserves the nearest available campfire seats, with up to six seats per fire.
It seats workers with nothing to do the same way at any hour; their ground groups gather round the nearest campfire rather than where they stand.
A worker who sat in the last tick keeps their seat while they still have nothing to do, so the crew still sitting when lunch ends stays put.
A campfire burns its wood as usual while workers sit at it; once it has none left, a worker sitting in one of its seats keeps it burning at `KindledBurn`, one second (`BreakSeats.Kindle`), and it dies down within that second once they leave.
A blocked walk releases its seat, and the worker remembers seats they could not reach until nobody is on a break any more, lunch or idle, or until the number of buildings that block walking changes (`BreakSeats.Prepare`), when every seat is tried again.
Workers on the ground keep their seats when the crew changes, unless they take a vacancy by a fire.
Workers without a fire seat sit on open ground in groups of up to three and face the center of their group.
When a worker cannot reach a group, they sit near where they stopped if there is room, or wait standing.

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

The trading post's input filter names the crates it asks for, or holds `Inventory.Everything` for every kind of crate it buys.
`DepositFor` treats a post as storage only for an item its filter takes, so workers deliver those crates there when it is the nearest storage asking for them; with `Everything` it ranks with storage taking everything.
With `Thing.AutoSell` set, the cart leaves on its own once every market slot holds a crate and a cart is free.
Sale coins land in the trading post's wage slot (`Thing.Wages`, coins only), whose coin stack (`Inventory.CoinStack`) is `Inventory.UnlimitedCoins`, so the bank has no limit.
Dawn wages, settled strikes and hiring payments draw on every wage slot before the payer's backpack; purchases come from the backpack only.

#### Price sheet

The price sheet shows `SaveState.Prices`, a snapshot of sale prices.
It is taken when a cart returns, or when a worker on the trading post's deliveries job comes back from a price errand (`Worker.Away`).
Trades always use the live price, so the sheet can be out of date.

### Mines

A mine (`src/mines/`, `Mine` in `GameTables`) is marked free and snaps to `Cliff.MineSpot`, in front of the middle of an ore cliff's face between its two deposits, one per cliff.
Each portal follows its own cliff face, its tunnel runs into the rock, and its rails point outward toward the valley.
A continuous outcrop extends from the mountain foot, blocks walking with a matching footprint (`Mines.InOutcrop`), and holds the tunnel, which `MineModel.Build` darkens step by step to black.

`Thing.MineStage` counts finished stages from `MineStage.Marked`:

| Stage | Done by | Check |
| --- | --- | --- |
| Dug | a worker with a pickaxe | `MineStage.Dug` |
| Supplied | `Mine.Materials` delivered | `MineStage.Supplied` |
| Fitted | a worker with a hammer | `MineStage.Open` |

The two worker stages take `StageWorkdays` of `GameData.WorkdaySeconds`.
The player holding a gather on the mine (`World.Gather`, standing at `Mines.WorkSpot`) adds to the same progress with a pickaxe or hammer carried (`Mines.Work`), swinging the pickaxe or hammering (`ActorAnimation.PlayerAction`).
Workers fetch the pickaxe or hammer a stage needs from a tool station, like any other job.

Up to `Mine.Workers` miners work underground (`WorkerWorkState.Underground`, hidden by the view).
Each adds one ore and one stone to the mine's two-slot `Thing.Output` every `Mine.Seconds`.
Mining stops while either slot holds a full stack.
A miner then carries a load of it to the nearest place that takes it (`Mines.Mine`), and with none the mine reports once that it is full and the mine job has nothing to do.

### Farming

A farm plot (`farm`, `src/farms/Farms.cs`) is a walkable 6×6 zone that snaps beside piles and other plots.
The ground dressing clears meadow plants across the square.
`Farm` in `GameTables` sets what sows it, how many days the wheat grows, how many working days the harvest takes and what it yields.

`Thing.FarmStage` is `Fallow`, `Growing` or `Ripe`.
A fallow plot sows itself from its own inventory.
The water slot stores up to 10 liters, which supply one day of growth.
A ripe plot keeps any water left for the next crop, and its panel hides the water slot and fill bar.
The area of dark soil on the bare plot or its tilled rows follows the water level, including the liter in use.
Each liter in use grants a tenth of a day of growth and counts toward capacity until spent.
`Thing.FarmWaterLeft` counts the growth seconds still covered by that liter.
`Thing.Progress` counts completed growth from whole liters, then harvest work.
`Farms.Growth` includes work from the liter in use and `Farms.CropLook` uses it to choose bare soil, tilled soil, straws,
green plants or wheat.

The player harvests through `World.Gather` and assigned workers through `World.Reap`, both adding to the one progress.
The yield goes into the plot's `Thing.Output`; nothing moves seed back into the plot's input slots on its own.

- A worker on Farm harvests.
- A worker on Haul keeps the input slots at one sowing, from the plot's own output first and then from storage, and carries out the rest (`FarmHaul`).
- `BarrelHaul` sends the hauler to fill a bucket when the growing plot is dry.
  The worker walks between four places on the plot, stops to tip part of the bucket at each and keeps the empty bucket.

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
  Frames are drawn without waiting for the screen to refresh, and each is saved in the background while the next is
  drawn.
- It reuses the game's own models: the player's rig, the pack, flask, axe and torch, the trees, the valley with its
  shore and sea, and the fire pit. Walking and running go through the game's gaits.
- New for the film: the office, the rented room and the train car (`IntroSets`), a baseball bat, the boss
  (`ActorSkin.Boss`), the catgirls (`Catgirl`) and the poses in `IntroPoses`.
- The hero's screen shows a web page of three catgirls dancing (`CatgirlPage`). The page is a small world of its own,
  drawn to a picture that a sheet over his screen wears; it is drawn again each frame only while his screen is seen.
  All three catgirls (`Catgirl`) share the same slim build (`HumanShape.Slim`), with a shaped face, defined shoulders,
  tapered arms, rounded thighs, defined knees, slim calves, articulated fingers, cat ears and a five-joint tail.
  The build follows front and side figure studies, with arms set back at the shoulders, thighs set into the pelvis,
  and slightly longer lower legs with the calf bulging behind the shin.
  Its trunk, limbs and palms are one skinned surface (`JoinedHuman`), which bends at the waist, shoulders, elbows,
  wrists, hips, knees and ankles.
  The build's trunk (`JoinedTrunk`) says where the arms and legs meet it, each as a distance from its own joint.
  The soloist's waistband, shorts and thigh-high socks are colours of that same surface.
  The five-second close-up starts square on to the dancers and moves around the stage to their left.
  The dancer on the left wears a maid's dress, the soloist wears a sleeveless collared blouse, ribbon tie, tailored
  shorts and thigh-high socks, and the dancer on the right wears a long black hoodie.
  All three dance at 135 beats per minute, on a clock that starts with the close-up of the screen.
  Pink and blue spotlights and the bars behind the dancers take turns pulsing on that beat, over a dim violet stage.
  The film plays no music under them; the beat they dance to plays only in the dance's preview.
  `CatgirlDance.PoseTime` holds each pose for two to four drawing frames, changing all three dancers together.
  Every step of the dance falls on one of those poses.
  In `CatgirlDance.Solo`, the middle dancer reaches up, sweeps her arms out, frames her face, crosses her knees,
  then steps wide and rings one eye, followed by a shoulder accent and the eye gesture on the other side.
  The two at the sides answer her gestures with `CatgirlDance.Chorus`.
  The dancers put their weight into the beat, then rebound, with heads and wrists following the hips.
  Each step places the hands and ankles and sets the hips' height, all for the slim build.
  `ArmReach.WithHandAt` bends the arms from that build's shoulders, and the legs bend to keep the planted feet where
  the step puts them.
  The person model has joints the game's own poses leave at rest, which any rig has: the chest turns on the belly at
  the waist (`ActorRig.BendChest`) and the hips tuck, turn and tilt (`TurnHips`), both kept until set again; and each
  foot tips at the ankle (`RollFoot`), on top of the pose shown.
  Each thigh tips out on its own through `Pose.SplayL` and `Pose.SplayR`.
  `Catgirl` adds wrists that bend, twist and tip, a head that nods, turns and tilts, and a turn of the whole body.
  A `DanceFrame` also holds where the dancer stands, so the soloist can move her weight over a planted leg.
- The paint filter is drawn over the frames wider than in the game (`WorldView.IntroPaintWidth`), since the movie tool
  shrinks them.
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
  it distant, `muffled=1` leaves only its low part as if heard through a wall, `rate` plays it faster or slower, and a
  `stop` cue fades out the music track that is playing.
- A hand goes to a thing through `Reaching`, which bends an arm of a pose so its fist lands on a point: packing at home
  and touching the sea use it over the game's crafting squat.
- A pose's elbow folds only forward. `ArmReach.WithHandAt` also turns the upper arm about its own length
  (`ActorRig.RollArm`), so a forearm can lie across the chest: by the fire his arms are crossed, each palm on the other
  shoulder.

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
- The view plays sounds tied to animation timing: footsteps by ground type (`Footsteps.Sound`: gravel paths, water, rock, grass), axe, pickaxe, anvil and bench impacts, a shovel going into the soil and throwing it aside, rail clinks while fitting a mine, rustles of hand picking and packing, butchering, the mortar, blows landing on wolves, bellows pumps, storage lids, the cabin door and a felled tree.
- `Net.Do` gives a player's inventory moves and spending a sound by item kind, sent to that player, when the action made none of its own.

A tree felled in sight takes about 1.7 seconds to come to rest when it is among the shortest and 2.5 when among the tallest, depending on the way it falls, its top landing at 6 to 13 metres a second (`TreeFall`).
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

The world is a fixed 216-tile valley defined in `src/terrain/Terrain.cs`:

- sea past the coast;
- mountain ranges beyond a valley half-width that narrows inland;
- a lake whose shore function bounds both walking and the drawn water.

`World.Scatter` places resources with a seeded generator, trees more densely toward the mountains.
`World.Deserialize` also refuses, rather than repairs, a save that is incomplete, names unknown items, marks or counts batches of a recipe at a station that does not make it, or holds player needs out of range.
Workers use grid detours around water and buildings.

## Development and verification

[AGENTS.md](../AGENTS.md#checks) lists the checks and [RELEASE.md](RELEASE.md) the development flow.
The network check's guest joins through a relay in `build.py` that adds about 80 ms of delay and 5% packet loss each way.
Through it the guest chops the wolf its swing is aimed at, misses one to the side, and is bitten by a wolf on the host.

### Platform

The project targets Linux x86_64 and Godot's Forward+ (Vulkan) renderer, whose device selection prefers a discrete GPU.
First-time setup requires internet access; interactive play requires a supported graphics driver.
The export is a runnable prototype.
