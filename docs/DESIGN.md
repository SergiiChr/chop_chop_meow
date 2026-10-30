# Design and architecture

The prototype is an isometric woodland survival and production game.
It is rendered in 3D with low-poly models: natural woodland, modern DIY buildings and no fantasy styling.
Item and building icons are rendered from the same models.

## Confirmed design decisions

These came from the user; change them only after asking.

- **Platform and launch:** Linux first, one-command launch from a fresh checkout through the Python launcher.
- **Look:** smooth isometric 3D, natural and lighthearted, with some of Terraria's approachable feel; no fantasy or industrial styling. Buildings are modern DIY, there are several tree species, and workers wear blue overalls over a cream shirt.
- **Animation:** every core interaction has an animation, with a dedicated one for every tool.
- **Progression:** the player starts with a steel axe and no pickaxe; there is no loose iron and there are no iron tools. The first pickaxe is bought at a trading post with crates of berries.
- **Mining:** coal, iron and tin deposits need a carried pickaxe; loose coal needs no tool. Charcoal cannot substitute for mined coal.
- **Steel:** needs iron, mined coal, a reinforced furnace and active bellows heat. Two assigned workers divide bellows and hauling.
- **Worker tools:** workers take the tools their jobs need (axe for trees, pickaxe for deposits and mines, rake for leaves, hammer for fitting a mine) from a tool station and put them back when reassigned or going home. Without one they wait and say so once.
- **Transfers:** automated transfers respect input and output allowlists, where empty means unrestricted, and prefer storage whose input filter explicitly matches. Piles set their own filters from what they hold. Manual transfers bypass automation filters.
- **Carried items:** never deleted when a worker is reassigned, fired, blocked or rerouted.
- **Survival:** food, water, day and night, wolves that fear light, sleeping in a cabin, wages, saves and recovery after death.
- **Market:** reached at a trading post; selling lowers a price, which recovers over time; deeper goods are worth more; orders settle after a visible 30-second cart trip.
- **Workers:** hired individually, paid daily, assigned, reassigned and dismissed; they carry station inputs and outputs, preferring matching filtered storage.
- **Pacing:** 10–15 minutes to the first goal is a target, not a measured playthrough.
- **Out of scope:** pollution, vampires, terraforming, charcoal and technology beyond steel.

## Architecture

The project targets Godot 4.7.2 .NET with C#.
`scenes/Main.tscn` is the project entry point and references `src/Game.cs`.

### Source files

Simulation and data:

| File | Owns |
| --- | --- |
| `src/World.cs` | The `World` simulation and `SaveState` |
| `src/ThingKind.cs` | `ThingKind`, what a world object is, and its ids in saves, data and hotbar slots |
| `src/Inventory.cs` | Inventories and their filter-aware transfers |
| `src/GameData.cs` | Loads tunable items, buildings, recipes, trade values and time values from `data/game.json` |
| `src/Farm.cs` | Farm plots |
| `src/Mine.cs` | Mines |
| `src/Packing.cs` | Packing stations |
| `src/Terrain.cs` | The fixed valley map |
| `src/Recovery.cs` | Skipping to dawn while downed |
| `src/Water.cs` | The refillable three-sip bottle and drinking at a water source |
| `src/Players.cs` | Per-player state, the command dispatcher and guest adoption |
| `src/Net.cs` | The ENet connection, sending and merging world updates, and guest saves |
| `src/WorldUpdates.cs` | World updates as entries, their differences and compression |

UI and input:

| File | Owns |
| --- | --- |
| `src/Game.cs` | Creates and holds the interface's parts, routes input, save/load, the main game loop and command-line review flags |
| `src/Menu.cs` | The pause menu with the goal chain, and the round menu badges |
| `src/UiKit.cs` | What the interface's parts share: `UiKit` (the world, how a player action runs, the buttons), `UiState` and `LiveText` |
| `src/Icons.cs` | Item and building icons, cached as they are first asked for |
| `src/MessageStack.cs` | The fading message stack at the top of the screen |
| `src/RecoveryPrompt.cs` | The "Skip to morning?" prompt |
| `src/MinePanel.cs` | The mine's building steps, miners and outputs |
| `src/Style.cs` | The shared enamel camp-gear look (speckled enamel boards with a cream rim, beige wells, cream cups, brick for what is active or wanted, Fredoka and Nunito) and the heat, day and cart-road dials |
| `src/SidePanel.cs` | The side panel: opening and closing its pages, selecting what it inspects, its size, and `PageMemory`, what pages keep while they are rebuilt |
| `src/BackpackDrawer.cs` | The backpack drawer above the hotbar |
| `src/Pages.cs` | What a page opens as, and the pages of plain buildings |
| `src/Panels.cs` | `PagePanel`, a page and the pieces pages are built from: the filter window, the process row with its fuel slot and side outputs, job buttons and their worker lists |
| `src/ItemMoves.cs` | What clicking or dragging an item does: reach, refusals and the crate-in-crate warning |
| `src/Grid.cs` | Draws the icon-cell grids |
| `src/ItemSlot.cs` | The cream item cup and drag and drop between grids |
| `src/Machine.cs` | Draws station input and output slots |
| `src/Crafting.cs` | The crafting menu and the crafting bench panel |
| `src/BlackHole.cs` | The crate-in-crate warning, collapse shader and game over screen |
| `src/Market.cs` | The trading post market, hiring offers and price sheet |
| `src/Crew.cs` | The crew cards and worker panel |
| `src/PackerPanel.cs` | The packing station panel and its good picker |
| `src/GuidePanel.cs` | The field guide topics |
| `src/NeedsPlate.cs` | The HUD health, hunger and thirst vials, the bank and the red damage edges |
| `src/EndingScreen.cs` | The one-time ending screen |
| `src/StartMenu.cs` | The start menu with its save slot, load and options screens |
| `src/Hotbar.cs` | Nine saved item/plan references, assignment, icons and use |
| `src/NetMenu.cs` | The host and join menus |

Rendering and sound:

| File | Owns |
| --- | --- |
| `src/WorldView.cs` | The 3D scene, orthographic camera, picking, lighting and world animation |
| `src/WorldScene.cs` | Ground dressing, particles and the animation review |
| `src/Terrain3DView.cs` | Mountain and valley terrain through the Terrain3D plugin |
| `src/EnvironmentPlacement.cs`, `src/GroundDressing.cs` | Grass, flowers and shrubs from the baked placement, cleared around buildings and paths |
| `src/LakeAppearance.cs`, `src/ShorelineAppearance.cs` | The lake surface and wet banks |
| `src/WorldModels.cs`, `src/ActorRig.cs`, `src/ModelKit.cs`, `src/*Shape.cs` | Build the models (see [assets/README.md](../assets/README.md)) |
| `src/ActorAnimation.cs` | Picks actor actions and gives their keyframed poses |
| `src/IconRender.cs` | Renders item and building icons from the models (`./build.py icons`) |
| `src/Sounds.cs` | Plays sound effects |

Checks and review tools:

| File | Owns |
| --- | --- |
| `tests/*Checks.cs` | Simulation checks run by `./build.py check` (`SELF-TEST PASS`) |
| `tests/*Steps.cs` | Steps shared by the simulation checks |
| `src/GameChecks.cs`, `src/SurvivalUIChecks.cs` | Input/UI checks through real input events (`UI-TEST PASS`) |
| `src/NetChecks.cs` | The host and guest network checks (`NET-HOST PASS`, `NET-GUEST PASS`) |
| `src/InputSteps.cs`, `src/PanelSteps.cs`, `src/SessionSteps.cs` | Steps shared by the UI and network checks |
| `src/PerformanceRun.cs`, `src/PerformanceFixture.cs` | The rendered performance run |
| `src/AnimationCapture.cs` | Captures the animation review |

### Simulation, view and UI

Keep balance and recipe values in `data/game.json` so they can be adjusted without changing simulation code.
Keep rendering and input separate from state transitions, so headless checks can exercise gameplay rules without relying on screenshots.

- `tests/SimulationChecks.cs` covers filters, worker routing, furnace work, trade, wages, recovery after death, saves and a material-driven progression run.
- `tests/EconomyChecks.cs` measures real workers through a day and checks the economy rules.
- `tests/AllocationChecks.cs` holds ticks to their allocation budgets.
- `tests/ProductionChainChecks.cs` covers the packing station, the trading post's filter and auto-sell, and workers alone running the berry, bread, canned stew and steel pickaxe chains to a sale.

### HUD and backpack

Health, hunger and thirst are HUD icons.
Coins are a backpack item that stacks to 1000.

Every page that works with the backpack shows it in one place, a drawer above the hotbar (`PagePanel.BagGrid`).
The stacks that page takes are rimmed in brick and the rest are faded.
A close button hides the drawer until another page or building opens.
The side panel only shows the building and sizes itself to what it holds.

### Multiplayer

Multiplayer is host-authoritative.
`SaveState.Players` holds one `PlayerState` per player.
The top-level player fields of `SaveState` pass through to `SaveState.Actor`, so saves keep the host's player where single player always had it.

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
`WorldUpdates` writes them into reused buffers and keeps the last update's array for every entry that did not change, so only changed entries allocate (`tests/AllocationChecks.cs`).
The welcome is a guest's first update, with the fog uncovered so far.

- Updates go unreliably on their own channel, so a lost one never delays the next.
- The whole world after a long stall, or a change too large for one packet, goes reliably on a third channel, so messages and actions never wait behind it.
- Guests merge entries into their copy in place, so panels and the view keep their objects.
- Each action carries where the guest stood, so the host checks reach from there rather than from a position still gliding in.
- Leaving asks the host for the guest's player before the line closes.
- A guest whose line drops saves its player from its copy, then `Net.Retry` joins the same address again until `Net.ReconnectLimit` runs out. A new connection gets a new id, so the host may hold the old player until its side of the line times out.

Factorio instead runs the same deterministic simulation on every machine and sends only inputs (lockstep).
This simulation uses floats and frame-length steps, so it is not deterministic, and with hundreds of objects the deltas stay small.

### Models and picking

Every world object is a low-poly model assembled from Godot primitive meshes in code.
Meshes and materials are cached by parameters, so repeated objects share resources.
`WorldModels.Warm` builds one model of every kind in each of its looks while the world loads, so a building, crate or carcass that first appears mid-game builds no mesh, material or shader; the three trade carts are built then too and reused for every trip.
One tile is one world unit and an adult is about 1.65 units tall.
The camera is orthographic with a 30° pitch, giving a 2:1 isometric footprint.

Picking, the hover ring and canopy fading use the same per-kind vertical hit cylinders (`WorldModels.Hits`), so selection matches what is drawn.
A tree crown only wins a click when nothing else is under the cursor, so objects behind a canopy stay selectable.
Trees use three variants chosen by object id, so saves need no new fields.
Fading swaps in cached alpha-blended copies of a model's materials, so faded trees still share a handful of materials.

### Actor animation

Actor animation is driven by gameplay state.
`ActorAnimation` maps state to an action and returns keyframed joint angles (`Pose`) for any time.
`ActorRig` applies them to a hierarchy of hips, torso, head, shoulders, elbows, thighs and knees, lifting the hips so the lower foot stays on the ground.

The player's carried tool selects its matching action and the tool shown in hand.
Workers animate according to movement and an explicit transient `WorkerWorkState`, not localized or presentation status text.
The transient state is excluded from saves and recalculated on the next simulation tick.

At a packing station the packer stands behind the table (`World.PackStand`) and the `Pack` action rummages in the heap of goods, lifts a piece across and settles it into the crate, then turns back.
The player working a station they stand near packs the same way.

Manual bellows pumping and the unarmed punch trigger one loop of their action.
Each work action has an impact moment that shakes its target and spawns chips, leaves, splashes or sparks.
Pausing freezes animation and effects.

## Gameplay

### Player loop

The intended fresh-start loop starts by securing water, shelter and fire, then uses the steel axe the player carries:

1. Collect water, wood, stone and berries; build a cabin and campfire before night.
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
- `goals.profitDays` consecutive days of at least `goals.dailyProfit` profit.

`SaveState.DayRevenue` collects returned sale coins.
At dawn the wages charged are taken off and the result joins `SaveState.Profits`.
Reaching the streak sets the `profit_week` milestone, which stays reached.
The starter goal chain stays as the tutorial, and the pause menu lists both.

### Survival and needs

Food, water, the day/night cycle, shelter, wages and wolf encounters support the production loop.

- Items carry what eating or drinking one restores (`hunger`, `thirst`, `health`) and a `food` flag.
- Food is what wolves eat and compost will take, cooked or not.
- An item that restores nothing, such as raw meat, wheat or dough, only says it needs cooking.
- `hungerHours` and `thirstHours` in `data/game.json` set how many in-game hours a full bar lasts.

Sleeping in a cabin advances time toward dawn.
It is allowed from `World.SleepHour` (22:00), a little before night falls.
It is refused if hunger or thirst would run out first.

#### Sprinting

Holding Shift while moving sprints at `sprintSpeed` times the walking speed (`PlayerState.Sprinting`, sent by guests with their walk).
Hunger and thirst drain `sprintDrain` times as fast meanwhile.
Under 15% food or drink (`GameData.LowNeed`) the player cannot sprint and the screen edges pulse red.

#### Death and recovery boxes

Death moves carried items to a recovery box and respawns the player after a 30-second timer in the cabin nearest to where they fell, or at the clearing without one.
Recovery boxes are never automation storage and disappear once empty.

### Sight and fog of war

Fog of war is simulation state.
`World.Look` runs every tick: it marks tiles within `SightRadius` of the player in `SaveState.Explored` and replaces `SaveState.Remembered` entries in sight with snapshots of the live things there.

`World.Known` is what the view draws and picks: live things in sight, snapshots elsewhere, nothing never seen.
Snapshots are updated in place and keep only the look: no inventory, fire, heat or work in progress.
The view picks against what it draws but only returns things that still exist.

`WorldView` hides wolves, workers and carts outside sight.
It draws one screen-wide quad after everything else, placing each pixel in the world from the depth buffer: black on unexplored tiles, a grey wash on explored ones, clear inside the sight circle.

### Night and wolves

Wolves come out of open ground just beyond every player's sight radius, so none appears on screen.
A chasing wolf (`Wolf.Chase` above zero) gallops at `wolfChase` times `walkSpeed`, which data validation keeps above walking and below `sprintSpeed`.
Stalking, creeping and backing off stay at the slower trot.

Campfire light repels wolves, and `campfireHours` sets how long one log burns.
A wolf already chasing the player still follows into the light for a few seconds.
Inside a cabin (`SaveState.Sheltered`) the player is hidden and wolves back off at once.

#### Torch

A torch in hand (`SaveState.Torch`, burn seconds left) burns as long as one log, only while `SaveState.TorchLit`.
Lighting it takes `World.KindleSeconds` (`SaveState.TorchKindling`).
Swings share the axe cooldown and flare the light (`World.TorchFlare`).
They put the torch out after `World.WildSwings` within `World.WildWindow` seconds.

While the torch burns and fewer than `World.PackSize` wolves are near:

- an unscared wolf creeps in at a third of its speed and can bite;
- a swing aimed within about 45° of a wolf in `World.ScareRange` sets `Wolf.Scared`;
- a scared wolf keeps its bearing at `World.StalkDistance` without biting or leaping;
- spawns slow down, but the cap rises to six.

Once the pack is complete, swings no longer scare and the wolves attack.
A direct torch hit sets `Wolf.Flee`.

#### Mauling and wolf meat

At half health or less, an unprotected player can be pinned by a leaping wolf.
The pinned state (`SaveState.Mauled`) blocks movement and actions and drains health while the pack feeds.
The death that follows strips food from the recovery box.

Killed wolves leave a `wolf_corpse` resource that yields raw meat, which the stone furnace cooks into edible meat.

### Resources

- Oaks drop a patch of leaves beside the trunk (`World.LeafPatch`), refilled every dawn.
- A picked-clean berry bush fruits again after `berryRegrowHours`.
- Tin sits at the foot of a third cliff further inland. Like iron, it needs a pickaxe.
- Tin ore smelts into tin bars at the stone furnace, and a tin bar makes 4 cans at the bench.
- Charcoal production is not implemented.

### Tools

Tools carry a `tool` kind (axe, pickaxe, hammer, chisel, rake, mortar), a `speed` and a `tier` in `data/game.json`:

| `tier` | Material |
| --- | --- |
| 0 | wood or stone |
| 2 | steel |

Tier 1 is unused, since there are no iron tools.

`World.BestTool` picks the fastest carried tool at or above a tier.
Recipes name the tool they need, the lowest `tier` that will do, and whether they can be made by hand (`hand`).
So a steel chisel does everything a stone chisel does, faster, while tin cans need a steel hammer and the mortar a steel chisel.

`World.ToolKind` names the tool a resource needs: an axe for trees, a pickaxe for deposits, a rake for leaves.
Stone, berries and loose coal need none.

Every tool and its parts stay reachable, which `CheckEverythingIsReachableFromFreshStart` verifies:

- a chisel is always 2 stone away;
- a mallet is a wood part;
- neither is sellable;
- the first pickaxe or a lost axe is bought with coins from berry crates.

### Crafting and stations

Hand recipes queue in `SaveState.Crafting` and take their inputs at once.
They run at base speed times the tool's speed while the player carries it, and give the materials back when cancelled.

The bench runs at `benchSpeed` times the speed of the tool in its own chisel or hammer slot (`Thing.Tools`), and like the anvil only while someone works it.

#### Tool slots

`World.SlotKinds` names the tool slots a station has (`Thing.Tools`): a chisel and a hammer at the bench, a mortar at the cooking table (`cooktable`).
A recipe that needs a tool at such a station waits for it to be slotted and runs at that tool's speed.
At the cooking table it runs only while someone works it (`World.Worked(Thing)`).
The mortar's recipes are worked, while jam, stew and dough cook unattended and their worker only hauls.

#### Fuel

A recipe with `fuel` burns one wood or coal per batch from the station's one-slot fuel box (`Thing.Burner`).

- `Thing.Fuel` follows what was last loaded.
- `World.FuelFits` limits the reinforced furnace to coal.
- `World.BatchCost` lists materials only.
- Haulers keep the burner topped up (`Worker.Fueling`).

The canning station (`cannery`) seals a filling and a `tin_can` into a canned good over fuel.

#### Anvil

An anvil only progresses while someone hammers it: the player standing within reach without walking or gathering, or a paid assigned worker hammering at the station.
A smith stays with a running batch and fetches the next load once it is done.

#### Furnaces and steel

The stone furnace accepts wood or mined coal for fuel.
The reinforced furnace (`reinforced`) burns coal only.

Steel recipes consume iron bars and mined coal.
Steel only progresses while the heat is at or above the clear bar (`World.SteelHeat`, shown as 1450 °C through `World.Celsius`).
A batch below it pauses, keeps burning one coal from its fuel slot per batch length while cold, and resumes when reheated, with no failure.
Once an active crew has the forge hot, it never falls below the bar while they are on duty.

### Storage, filters and automation

Inventories are slot grids with stacks of 64.
Tools, packed crates and bottles take a slot each everywhere.
The layout is saved and synchronized: removing one stack never shifts unrelated cells.
Sort is the only action that packs and regroups stacks.
Item totals change only through `Inventory` methods, which keep them in line with the stacks.
Stacks are rebuilt from totals once, where the two can disagree: when a save loads (old saves hold totals only) and when a guest joins.
A guest's copy takes totals and stacks together from the host, so it does not rebuild.

Input rule for every item cell, in storage and stations alike:

- A click moves exactly one item, in either direction.
- Ctrl-click, or Shift-click, moves all of that item.
- Right-click picks up half where a panel supports dragging.
- Dragging carries a stack or a split portion to its exact destination, merging compatible stacks or swapping cells.
- Bulk buttons and recipe batch actions say what they move.

A worker's hands (`Worker.Bag`) work like a crate: `Worker.BagStack` (5) items in all, one stack or up to five kinds; `Worker.Hands` widens them for a full mine or market stack.

#### Chests

Chests have separate item allowlists for input and output, and empty lists allow all items.
Automated deposits prefer a chest with a matching input filter, then a nearby unrestricted chest.
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

A wood crate holds `crateCapacity` (12) units.
A picked-up crate becomes an item `crate_<n>` whose contents live in `SaveState.Crates`; an empty one is the stackable item `crate`.
A crate of one good carries the good's id in its name, `crate_<good>_<n>`, so filters can name the kind of crate (`World.CrateOf`, matched in `Inventory.Accepts` through `World.PackedKind`).
A pile of crates holds crates of one good.
Placed crates are manual storage only.

#### Packing station

A packing station (`packer`, `src/Packing.cs`) packs the good in `Thing.Packs` into crates, anything a crate holds but crates and coins.
Its inventory takes only that good and empty crates; `Thing.Output` holds the crate being packed.
While worked like an anvil, it moves one item every `packSeconds` into that crate, opening an empty crate for the first.
A full crate, or one of another good after the choice changes, waits in the output.

- Haul carries that crate to `DepositFor` and the station's leftovers to storage, then keeps a crate's worth of the good and two empty crates there.
- A worker with both jobs fetches only once the station runs dry, crates first.

Putting a packed crate into a placed crate asks for confirmation.
If confirmed, it ends the run in a black hole with a reload or main-menu choice.

#### Tool station

A tool station (`toolstation`) is storage for tools only.

### Workers

Workers are tracked individually.
`hireOffers` in `data/game.json` lists hiring deals, and each worker keeps the daily wage of the deal they were hired on (`Worker.Wage`).

#### Duties

Workers hold duties (`Worker.Duties`): a place and one of the jobs `World.Jobs` offers there, such as gather, craft, haul, bellows, farm, build, mine, deliveries, fuel, stock or tend.
`World.SetDuty` adds or drops one.
`Worker.Assignment` is the place being worked now.
`World.NextPlace` moves on to the worker's next place when the current one has nothing to do or after a delivery, so one worker can cover several places in turn.

- Stock brings tools from storage to a tool station.
- Tend fetches what a compost pile's filters take from storage and carries its fertilizer out.
- Deliveries runs price errands and carries the trading post's mail, other than coins, to storage.

A gatherer whose spot is spent moves the duty to the nearest spot of the same kind in `SaveState.Remembered` that still holds something (`World.NextSpot`): seen by any player, even if now under fog, never under black fog.
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

At the `lunchHour` in `data/game.json` workers break for one in-game hour.
They sit in a circle around the campfire closest to the whole crew (`World.LunchFire`): every other one cross-legged on the ground and the rest on a rock, or where they stand without one.

As dusk begins (`GameData.DuskStarts`, a tenth of a day before night) they walk to the nearest trading post, or the clearing without one.
Then they walk a cart's trip down the valley over half a trade trip while fading out (`Worker.Commute`), and sign out (`Worker.Home`).
At dawn they walk back the same way, fading in, and return to work.

Assignments and carried items are kept throughout.
A crew at lunch or signed out does not hold the forge at the clear bar or hammer an anvil, and cannot run price errands.
An errand already under way finishes first.

### Trading and economy

Prices, wages and demand follow the rules in [ECONOMY.md](ECONOMY.md).

#### Market

The market only accepts packed crates of the goods in `market.sells`.
A sale pays for the contents unit by unit plus the crate.

`market.buys` lists what the village sells and in what lot, as the [README price sheet](../README.md#price-sheet) shows.

#### Demand and prices

In `market.sells` each good's first `steady` units sell at the full price, and each crate's worth past them takes `drop` off it.
A day restores what selling `daily` units takes (`World.Demand`, which runs above 1 by the steady amount).
Buying costs a fixed 1.25× the base price.

#### Carts and coins

A cart trip takes 30 seconds.
Purchases settle on return into the trading post's market output (`Thing.Output`), with overflow in a box beside it.

The trading post's input filter names the crates it asks for.
`DepositFor` treats a post as storage only for an item its filter names, so workers deliver those crates there when it is the nearest storage asking for them.
With `Thing.AutoSell` set, the cart leaves on its own once every market slot holds a crate and a cart is free.
Sale coins fill the trading post's one-stack wage slot (`Thing.Wages`, coins only) first and the output after.
Dawn wages and settled strikes draw on every wage slot before the payer's backpack.

#### Price sheet

The price sheet shows `SaveState.Prices`, a snapshot of sale prices.
It is taken when a cart returns, or when a worker on the trading post's deliveries job comes back from a price errand (`Worker.Away`).
Trades always use the live price, so the sheet can be out of date.

### Mines

A mine (`src/Mine.cs`, `mine` in `data/game.json`) is marked free and snaps to `World.MineSpot`, directly in front of an ore cliff, one per cliff.
Each portal follows its own cliff face, its tunnel runs into the rock, and its rails point outward toward the valley.
A continuous outcrop extends from the mountain foot, blocks walking with a matching footprint (`World.InSpur`), and holds the tunnel, which `WorldModels.Mine` darkens step by step to black.
Loading a save made on an older valley moves only the mine cart anchor to this site and keeps stock, stages and worker assignments.

`Thing.MineStage` counts finished stages from `MineStage.Marked`:

| Stage | Done by | Check |
| --- | --- | --- |
| Dug | a worker with a pickaxe | `MineStage.Dug` |
| Supplied | `mine.materials` delivered | `MineStage.Supplied` |
| Fitted | a worker with a hammer | `MineStage.Open` |

The two worker stages take `stageWorkdays` of `GameData.WorkdaySeconds`.
Workers fetch the pickaxe or hammer a stage needs from a tool station, like any other job.

Up to `mine.workers` miners work underground (`WorkerWorkState.Underground`, hidden by the view).
Each adds one ore and one stone to the mine's two-slot `Thing.Output` every `mine.seconds`.
Mining stops while either slot holds a full stack.
The first free miner then carries the whole stack to storage (`Worker.Bag` grows to the stack size until emptied), and with no storage the mine reports once that it is full.

### Farming

A farm plot (`farm`, `src/Farm.cs`) is a zone like a pile and snaps with piles.
`farm` in `data/game.json` sets what sows it, how many days the wheat grows, how many working days the harvest takes and what it yields.

`Thing.FarmStage` is `Fallow`, `Growing` or `Ripe`.
`Thing.Progress` counts growth seconds, then harvest work.
A fallow plot sows itself from its own inventory.
`World.CropLook` gives the look: bare, soil, straws, green plants or wheat.

The player harvests through `World.Gather` and assigned workers through `World.FarmWork`, both adding to the one progress.
The yield goes into the plot's `Thing.Output`; nothing moves seed back into the sowing inventory on its own.

- A worker on Farm harvests.
- A worker on Haul first moves seed from the output into the sowing inventory, then carries out the rest and fetches what the next sowing lacks.

## Sound and music

### Sound effects

Sound effects are one-shot MP3s in `assets/sounds`; `x_1.mp3`, `x_2.mp3` and so on are variants of one sound `x`.
Each play picks a variant other than the previous one with a small random pitch and volume change.

Audibility follows the camera, so zooming out lets the player hear further.
A sound on screen plays at full volume, one just past the screen edge is faint, and it fades to silence 5 tiles beyond the edge (14 for a distant wolf growl).

Sounds come from three places:

- The simulation names discrete events through `World.Sound` (a fire catching, a wolf bite, a cart's payment) and stays silent while sleep fast-forwards.
- The view plays sounds tied to animation timing: footsteps by ground type, axe, pickaxe and anvil impacts, bellows pumps, storage lids and the cabin door.
- `UiKit.Act` gives manual inventory moves and spending a sound by item kind when the action made none of its own.

### Music

Music in `assets/music` crossfades over four seconds between the day music and the night music when night falls or dawn breaks, including a sleep that skips to morning.
The day plays its tracks one after another and the night loops one.

Each keeps its place for the running session, so music longer than its part of the day carries on next time.
It wraps back to the first track at the end.
The options screen sets the music volume, stored in the settings file.

## World map

The world is a fixed 216-tile valley defined in `src/Terrain.cs`:

- sea past the coast;
- mountain ranges beyond a valley half-width that narrows inland;
- a lake whose shore function bounds both walking and the drawn water.

`World.Scatter` places resources with a seeded generator, trees more densely toward the mountains.
Saves from earlier maps are refused.
`World.Deserialize` upgrades older saves, then refuses one that names unknown items or holds player needs out of range, with the same checks `Adopt` applies to a joining guest.
Workers use grid detours around water and buildings.

## Development and verification

[AGENTS.md](../AGENTS.md#checks) lists the checks and [RELEASE.md](RELEASE.md) the development flow.
The network check's guest joins through a relay in `build.py` that adds about 80 ms of delay and 5% packet loss each way.

### Platform

The project targets Linux x86_64 and Godot's Forward+ (Vulkan) renderer, whose device selection prefers a discrete GPU.
First-time setup requires internet access; interactive play requires a supported graphics driver.
The export is a runnable prototype.
