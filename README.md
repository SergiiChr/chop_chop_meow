# Chop Chop Meow

An isometric woodland survival and production prototype. Gather nearby materials, build a small settlement, craft parts and tools, pack steel tools into crates and sell them, then hire workers to automate resource gathering and furnace work. Play alone or with up to three friends at home or over the internet.

The world is rendered in 3D with code-built low-poly models under an orthographic isometric camera: natural woodland, modern DIY buildings and readable silhouettes. Item and building icons are rendered from the same models, and the rest are drawn icons. The earlier 2D art directions are kept in [docs/art](docs/art/README.md).

## Run on Linux

From the project directory:

```sh
./build.py play
```

The launcher keeps Godot 4.7.2 .NET, .NET 10, import files, export templates and build output under `.tools/` and `build/`. First-time setup downloads missing tools and needs internet access. To use an installed Godot 4.7.2 .NET binary, set `GODOT_BIN=/path/to/godot`.

Interactive play needs a Vulkan-capable graphics driver. The project uses Godot's Forward+ renderer, which picks a discrete GPU over an integrated one by itself.

### Launcher commands

```sh
./build.py setup    # prepare local tools, compile C#, and import assets
./build.py editor   # open the Godot editor
./build.py test     # run headless simulation and input/UI checks
./build.py build    # test, export, and smoke-test build/ChopChopMeow.x86_64
./build.py lint     # verify C# formatting and compile
./build.py icons    # re-render item and building icons from their models (opens a window)
```

### Release bundle

`build` also produces `build/ChopChopMeow-linux-x86_64.tar.gz`. Extract it and run `ChopChopMeow/ChopChopMeow.x86_64`; keep the adjacent `.pck` and runtime folder together.

The bundle includes the .NET runtime and does not need the SDK. The extracted bundle is smoke-tested without the development SDK in its environment.

### Developer review options

Play accepts Godot arguments after `--`:

| Option | Effect |
| --- | --- |
| `--showcase` | Load a populated review fixture instead of a fresh game |
| `--showcase --wolves` | Add a night ambush to that fixture |
| `--showcase --lunch` | Show the crew at lunch with the worker overview open |
| `--showcase --strike` | Show the unpaid crew holding protest signs |
| `--screenshot /tmp/chop.png` | Capture the running view after startup and exit |
| `--animation-review` | Show every actor action on rotating rigs |
| `--animation-capture /tmp/directory` | Capture the animation review frames for visual inspection |

```sh
./build.py play -- --showcase --screenshot /tmp/chop-chop-meow.png
./build.py play -- --animation-review --animation-capture /tmp/chop-chop-meow-animation
```

These options are developer review tools; they do not represent a normal fresh-start playthrough.

The default environment uses baked forest placement, the lake and coastal water
shaders, and Terrain3D rendering. `./build.py setup` downloads and installs the
pinned Terrain3D Linux x86_64 runtime when needed. See
[the environment notes and historical comparisons](docs/art/ENVIRONMENT_TRIALS.md)
for provenance and limitations. Recreate the current landscape views with
`./tools/environment_review.py` or the coast and mine views with
`./tools/environment_review.py --shoreline`; historical before screenshots are
preserved and are not regenerated.

For a rendered walking/day-night and eight-worker production benchmark, run
`./build.py play -- --performance-run /absolute/path/to/results`, then
`python3 tools/performance_report.py /absolute/path/to/results`.
See [local performance measurements and limitations](docs/art/PERFORMANCE.md).

## Start menu and options

The game opens on a start menu with Continue (the most recent save), New game, Load game, Join game, Options and Quit.

The pause menu also has New game and Quit; both ask first, since anything done since the last save is lost.

Options holds a window resolution picker, a frame rate limit (off, 60 or 90 fps) and a music volume slider. The resolution and frame rate choices are stored in `settings.cfg` and applied at launch.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrow keys | Move in screen directions |
| Shift while moving | Sprint 1.6× as fast; food and drink drain 1.7× as fast; not under 15% food or drink |
| Hold right mouse button on a resource | Approach and gather while held |
| Hold right mouse button on a placed crate | Walk over and pick it up with its contents |
| Left mouse button | Select a resource, building, crate or worker; place selected building; clicking a cabin walks you inside; on open ground, use the item in hand (swing a tool or torch, eat, drink) |
| E | Inspect the selected object if nearby, otherwise the nearest nearby object |
| B | Open building plans |
| Tab | Open or close the backpack |
| C | Open the crafting menu |
| H | Open the crew overview |
| I | Open or close the field guide |
| 1–9 or click a hotbar slot | Put that slot in hand, shown with an outline; building plans and crates go straight to placement, which stays on after each placement until Esc or the materials or crates run out (a mine ends it and opens); defaults: axe, pickaxe, chest, campfire, furnace, anvil, crafting bench, berries, water |
| Space | Swing axe, or pump the selected nearby reinforced furnace |
| F5 / F9 | Quick save / load to the current slot; until a slot is chosen they open the save or load screen |
| Escape | Cancel placement/assignment, close a panel, or open the pause menu with goals, save, load, options, new game and quit |
| Mouse wheel | Zoom |
| Click / Ctrl-click an item cell | Move one item / all of that item (Shift works like Ctrl); this applies across inventory and station panels |
| Drag a cell | Move its stack to another inventory or a station input slot; drop an item or building plan on the hotbar to assign it |

## Interface

### Status bar

Health, hunger and thirst are three upright vials in the top-left corner with a heart, a stomach and a droplet under them. Each empties as its level falls and turns red at 25% or less.

Beside them the bank shows the coins in the trading post's wage slot and the wages due each day, in red when they exceed the bank. Drop coins on it to pay in, or click it beside a trading post to take them out.

The day dial in the top-right corner shows the sun crossing its arc from sunrise to sunset; then the moon crosses the same arc until dawn. A hit flashes red along the screen edges, and they pulse faintly while food or drink is under 15%.

Messages stack at the top of the screen, up to four at a time; each fades out after 7 seconds. A message repeated while it still shows restarts its time instead of stacking again, and a full stack drops the one closest to fading. Actions whose result shows by itself, such as gathering, crafting, building or filling a bottle, post no message; warnings, refusals and news do. With a side panel open they keep to the space left of it.

### Bottom bar and hotbar

The bottom bar holds round badges for building, the backpack, crafting, the crew and the field guide, each with its key, next to the hotbar.

Assign a hotbar slot by dragging an item from the backpack or a plan from the building plans onto it. Slots reference carried items or building plans; they are not extra storage. Consumable slots use one item, building slots enter placement, and tools select a working reference. Hotbar assignments persist in saves.

Building plans are an icon grid; hovering a plan shows its description and what it still needs.

### Inventories

Inventories are grids of slots. Every item stacks to 64 except coins (1000), and packed crates, tools, and refillable bottles, which take a slot each.

| Inventory | Capacity |
| --- | --- |
| Backpack | 24 slots |
| Chest | 5 slots |
| Worker | 5 items, like a crate: one stack of 5 or up to 5 different items |
| Tool station | 12 slots |

- Inventory cells retain their positions. Drag to move, merge, split or swap stacks; Sort packs loose stacks and groups them by item type.
- Right-click a stack to pick up half of it, then left-click a slot to drop it there.
- A dropped stack that does not all fit fills the slot, and the rest stays held until you drop it elsewhere or click somewhere it cannot go.
- Clicking food consumes one item; clicking a filled bottle drinks one sip and retains the bottle. A torch click lights it, and a crate click starts placement.
- Raw food such as raw meat or wheat says it needs cooking first. What each food restores is set per item in `data/game.json`.
- A selected raw-material stack can be deposited by clicking a nearby chest.
- Chests, the tool station and compost have a funnel button beside close that opens their input and output filters in one window.

### Station panels

Stations show one cup per recipe input, the batch time above a progress bar and the output cup, with what one batch takes or makes under each cup. A station that burns fuel has its own fuel cup under the bar, marked by a flame, with how long the fuel in it lasts beside it. Side outputs and leftovers sit in a second row under the output. Each cup holds a single stack of 64.

- Click a backpack item to add one to its cup, Ctrl-click to move all of it, or drag it in.
- Double-click a bench recipe to load one batch of its inputs.
- Right-click a cup to pick up half.
- Click or drag a cup to take its contents back.

### Job buttons

Every building's panel ends in a row of job buttons, one per kind of work there with how many workers do it:

| Place | Jobs |
| --- | --- |
| Crafting bench | Craft, Haul |
| Reinforced furnace | Bellows, Haul |
| Mine | Build, Haul, Mine |
| Farm plot | Farm, Haul |
| Trading post | Manage deliveries |
| Tool station | Stock |
| Compost pile | Tend |
| Resource | Gather |

Clicking a job lists the crew with a tick box each. A circled number beside a worker counts their other jobs, and hovering it lists them. A worker can hold several jobs, at one place or several, and works through them in turn, moving on when one place has nothing to do.

The crew button opens an overview with each worker's daily pay and the total above.

## Survival

### Food, water and time

A full stomach lasts one in-game day and a full water gauge lasts one day; a day lasts 14 minutes. Raw meat cannot be eaten. Your refillable bottle holds three sips, each restoring one third of the water gauge. Keep the empty bottle after drinking and refill it at the lake; without a bottle, drink directly from a fresh water source for one sip, taking the same gathering time. Filling a bottle takes the same time as one gather action. Empty or filled replacements can be bought individually. Craft an empty bottle at a bench from one tin bar and one wood part, with the same steel hammer used for tin cans. Cooking returns the empty bottle when it uses water.

From 10 pm you can sleep until dawn, but only if your food and water will last until morning; otherwise the sleep button explains why.

### Death and recovery

Death drops your backpack, coins included, in a recovery box marked by a bobbing gold gem. You respawn after 30 seconds inside the cabin nearest to where you fell, or in the clearing without one. Emptied boxes disappear, and workers never use them as storage. During the respawn countdown, if no cabin exists, “Skip to morning?” lets you advance the world to dawn. In multiplayer, every player must be downed before skipping.

### Fog of war

Fog of war covers the map. Ground you have never seen is black. You see live only within a circle around you (`sightRadius` in `data/game.json`, 20 tiles), which fills the screen at full zoom-out.

Ground you have seen before is greyed out and shows things as you last saw them: a tree felled out of sight still stands there until you look again. Remembered fires and furnaces show unlit, and wolves, workers and carts are not shown. Explored ground and remembered things are kept in saves.

### The valley

The world is a fixed 216×216-tile valley. It opens onto the sea at the bottom of the screen, which cannot be drunk and is the map's edge. Impassable mountain ranges close in from the left and right toward the valley's inland end, and a lake lies in the middle.

The plains have occasional rocks, bushes and berry bushes, and the trees thicken into woods toward the mountains, so finding more means going out to look. Iron and coal deposits sit at the foot of rock faces at the base of both ranges and need a pickaxe. Loose coal is scattered below the nearer coal face and is picked up whole with one right-click, with no tool. A pale grey cliff further inland holds tin: mine it with a pickaxe.

## Progression

The opening progression prioritizes water, shelter and fire, then hand gathering with the starting steel axe: chisel, planks, mallet, bench and crates; sell berries at a trading post for the first pickaxe; then work toward steel.

### Goals

The starter goal chain in the pause menu (Esc) is a tutorial. Below it are the end goals:

- a working mine on every ore found in the cliffs;
- 1300 coins profit a day for 7 days in a row, where profit is the coins from sales that came back that day minus the wages charged at the next dawn.

Reaching both shows an ending screen with Restart and Keep playing.

## Crafting

### Crafting menu

The crafting menu (C) works on the go at base speed, about 10 seconds a recipe, and queues up to eight recipes. Clicking a queued one cancels it and returns its materials. Recipes are grouped into boxes by the tool they need, with that tool's icon on the box corner, red while you carry none.

A stone chisel takes 2 stone and no tool. With a chisel you carve planks (1 wood → 4), wood parts (1 wood → 2), a wooden mallet and torches (3 wood).

### Crafting bench

The crafting bench costs 10 planks, works twice as fast and must be worked like the anvil, by you or an assigned worker. It has a chisel slot and a hammer slot, and a slotted tool's speed multiplies its rate.

It makes everything the menu makes, plus these, all with any hammer:

| Recipe | Takes |
| --- | --- |
| Crate | 6 planks |
| Steel axe, pickaxe, hammer | see Steel tools below |
| Steel chisel | 1 steel part + 1 wood part |
| Rake | 1 steel part + 2 wood parts |

Some recipes need a better tool:

- Tin cans (1 tin bar → 4) need a steel hammer.
- A mortar and pestle (3 stone, by hand or at the bench) needs a steel chisel.

A better tool always does a plainer tool's work; the steel chisel works twice as fast as the stone one.

### Steel tools

The anvil turns one steel bar into one steel part. Anvils only work while someone stands at them hammering. A small steel tool such as a hammer takes 1 steel part and a big one such as an axe or pickaxe 2, plus 1 wood part for the handle.

## Furnaces and steel

- The stone furnace accepts wood or mined coal as fuel.
- The stone furnace smelts 2 tin ore into a tin bar, sears meat and bakes bread (see Cooking).
- The reinforced furnace takes iron and mined coal as materials, burns mined coal from its fuel slot, and needs heat from manual or worker-operated bellows. Steel forms from 1450 °C; below that it keeps burning coal without making steel.

Charcoal production is not implemented, and charcoal cannot substitute for mined coal.

## Cooking and canning

Sear 2 raw meat into 2 burnt steaks at the stone furnace (Sear meat recipe, one fuel per batch). Each dough bakes into 4 bread at the stone furnace.

The cooking table (2 steel bars, 6 stone, 8 planks) is a steel grill over a stone firebox with a plank counter and a mortar slot. With a mortar and pestle slotted and someone working it, it crushes 4 berries into 4 crushed berries and grinds wheat into flour. You can do both by hand from the crafting menu with a carried mortar.

Its other recipes cook on their own:

- 2 crushed berries → jam, over one wood or coal
- 1 water + 1 raw meat → meat stew, over one wood or coal
- 1 flour + 1 water → dough, kneaded with no fire

The canning station (2 steel bars, 8 planks) seals meat stew, jam or raw meat into a tin can over one wood or coal. Canned meat stew is the best food to sell.

Both stations, like the stone furnace, take wood or coal in their fuel slot.

## Storage

### Wood crates

A wood crate holds 12 items in total, as one stack or as single items, and its slots grow and shrink with its contents. Place one from the backpack or hotbar, click it to open it, and hold right mouse on it to pick it up with its contents packed inside.

Workers never pack, open or place crates; a worker managing deliveries only carries packed crates from the mailbox to storage. Putting a crate inside a crate asks first; saying yes ends the game.

### Piles

A pile (8 wood) is a 3×3 yard for one resource, holding 14 stacks that build up as a 9-4-1 pyramid in the colour of what it holds. The first resource put in sets it: from then on workers store and take only that, and anything else is refused. Emptying it frees it again.

Piles are walkable and snap edge to edge to a pile beside them, so they line up in neat grids.

### Compost

The compost switch in an empty pile's header makes it take leaves, grass and any food, cooked, raw or half-made, and turn 4 of them into 1 fertilizer every in-game hour. It keeps its own filters.

Workers bring leaves and grass to it first, and food only when no other storage takes it. **Skip food delivery** stops food deliveries, while food put in by hand still composts. A worker on its Tend job fetches what its filters take from storage and carries the fertilizer to storage.

### Filters

Storage input filters govern automated deposits; output filters govern worker withdrawals. Empty filters allow all items. Workers prefer explicitly accepting chests and reroute cargo when filters change. Manual inventory transfers ignore these automation filters.

### Tool station

The tool station (8 planks, 4 stone) is a rack that holds only tools. Workers need tools for some jobs:

- an axe to chop wood;
- a pickaxe to mine ore and coal;
- a rake to rake leaves;
- a hammer to fit a mine.

Stone, berries and loose coal need no tool. A worker without the tool walks to the nearest tool station, takes the best tool of that kind and works at its speed. They put it back when reassigned or when heading home at dusk. If there is none, the worker waits, a message says once what they need, and they carry on as soon as one is put in.

A worker on the station's Stock job brings tools from other storage to the rack.

Oaks drop a patch of leaves beside the trunk. Rake them with a rake and they fall again every day.

## Farming

A farm plot (8 wood) looks like an empty pile and snaps edge to edge with piles and other plots. Put 1 wheat seeds and 1 fertilizer in it and it sows. The soil darkens, and over 3 days small green straws come up, grow into tall green plants and turn into yellow wheat.

Hold right mouse on a ripe plot to harvest it by hand, no tool needed. An assigned worker also harvests, and everyone adds to one shared progress, so you can stop and come back. A harvest takes one worker about 1 working day and gives 4 wheat, 2 wheat seeds and 1 grass into the plot's output.

Seeds do not sow themselves: move them from the output into the sowing slot by hand, or let a hauler do it. Its panel shows seeds as the input, fertilizer as its fuel and wheat, seeds and grass as outputs.

- A worker on Farm harvests.
- A worker on Haul first moves seeds from the output into the sowing slot, then carries out the rest of the harvest and brings fertilizer and any missing seeds from storage.

Wheat seeds and fertilizer can also be bought from the village.

## Trading post

The trading post has three icon buttons: Market, Workers and the price sheet.

### Market

The market works like a station with six unlabeled input boxes and six output boxes. It only takes crates, and only of:

- raw meat, burnt steak, berries, bread, berry jam, meat stew, canned jam, canned meat stew;
- steel axes, pickaxes and hammers.

A sale pays for the goods and the crate. Load crates, press Sell, and a cart takes them to Bramblewick and returns 30 seconds later with the coins. Bought goods also arrive in the output boxes.

Trade carts keep their timing: a cart walks down the valley at a steady pace, fades out once it is well along, and waits out of view before walking back.

### Coins and wage slot

Coins are a backpack item that stacks to 1000. Purchases and hiring are paid from carried coins.

Sale proceeds fill the wage slot first, a single stack of up to 1000 coins between the input and output boxes, and the rest goes into the output boxes. Daily wages are paid from the wage slot first and then from your backpack. The slot only holds coins, and you can move coins in or out by hand.

### Price sheet

The price sheet opens to the left of the panel and lists sell and buy prices with a Buy button for what the village sells:

| Goods | Sold by |
| --- | --- |
| Single steel tools | One |
| Canned food | Crate of 12 |
| Filled bottle, empty bottle | One bottle |
| Wheat seeds, fertilizer | Crate of 4 |

The sheet only shows sale prices as last heard from the village and says how many days old they are. It updates when a cart returns, or when a worker on Manage deliveries is sent to fetch fresh prices (a one-time errand down the road and back). That worker also carries what the cart brings back, other than coins, to storage. Errand runners only walk a short way down the valley before fading from view.

### Prices

Selling lowers an item's price unit by unit, and a day restores one day's worth. The village pays the full price for three crates of berries and two of each meat a day before food prices slowly slip. Every steel tool sold lowers its price sharply, and about a crate of each a day keeps them steady ([docs/ECONOMY.md](docs/ECONOMY.md)).

Buying costs a fixed 1.25× the base price, so a buy-and-resell loop never profits.

## Workers

The Workers tab at the trading post offers several hiring deals: more coins up front means a lower daily wage. Workers are individual entities with assignments, wages, cargo and visible work status. There are at most eight workers.

### Daily routine

- Workers break for an hour of lunch at noon, sitting around the campfire closest to the crew.
- As dusk falls they walk to the nearest trading post and head off down the valley like a trade cart, fading from view, then walk back in the same way at dawn.
- Assignments and anything they carry stay as they were.
- While they are away a forge cools and an anvil stops.

An unpaid worker strikes, standing with a "No pay - no work" sign, and owed wages stop growing until the strike is settled.

## Mines

A mine is marked for free from the building plans. The plan snaps to a rock spur past the seaward end of the nearest ore cliff and shows a see-through green outline. Each cliff takes one mine.

Clicking the mine shows its three stages:

1. A worker with a pickaxe digs the entrance in about one working day.
2. 50 wood, 10 iron bars and 50 stone go in, hauled from storage by assigned workers or delivered from the backpack.
3. A worker with a hammer fits the timber frame and the rails, which run into the dark tunnel, in another working day.

Workers take the pickaxe and hammer from a tool station.

A finished mine takes up to 3 workers with pickaxes, who mine out of sight underground. The cart goes underground with them, and the lantern on the frame lights the area like a torch. When nobody is working, the cart stands outside, loaded once there is output.

Each miner brings out one ore and one rock every 1.6 seconds, about half what a worker gets from a deposit, but a mine never runs out. Output fills two slots, ore and rocks, of one stack each. When either is full, mining stops and a miner carries the full stack out to the nearest storage that takes it. With none, a message says the mine is full with no output available.

## Wolves and night

Wolves hunt at night. A chasing wolf gallops faster than you walk, but sprinting outruns it. They come out of the dark beyond what you can see, never on screen.

### Fire

A burning campfire keeps them away, and a wolf already chasing you follows you into its light for a few seconds. One log burns about 4 in-game hours. Cabin windows glow but do not deter wolves.

### Torch

A torch burns just as long as a log, once. With a torch in hand, the first left click on open ground strikes flint and steel for 2 seconds to light it. After that each click swings it, which briefly widens its light by a fifth.

Swinging four times within 2 seconds, as fast as swings go, puts it out with a message. Relighting takes the same 2 seconds and keeps the burn time it had left.

While it burns, a lone wolf still creeps in slowly, but a swing toward it scares it into following you at the edge of the light. More wolves gather over time, and once five are around you they attack together and swings no longer scare them. A direct hit with the torch hurts a wolf and sends it running for a moment.

### Cabins

Click a cabin to step inside, even while walking. Wolves back off at once and give up once out of sight, and you stay in until you press Step outside in its panel.

### Wolf kills

At half health or less, away from fire and shelter, a nearby wolf leaps for your throat. You are pinned and cannot move or fight, the rest of the pack runs in to feed, and you die within seconds. A wolf kill destroys all food in the recovery box; the pack keeps feeding until you respawn.

A slain wolf falls over and leaves a carcass that lasts until it is butchered: hold right mouse on it for 4 raw meat.

## Multiplayer

### Hosting and joining

**Host** in the pause menu opens the current game to others on UDP port 24565. The pause menu then shows the port and the number of guests, and the game no longer pauses while a menu is open.

**Join game** on the start menu, or **Join** in the pause menu, opens a popup with the port already filled in and the address of the last game joined. Join is unavailable while hosting, since it would drop the guests. Up to three guests can join.

### Guest saves

The host keeps the world and saves it as usual; guests are not kept in the host's saves. Each guest keeps its own player (position, health, needs, backpack, hotbar, crafting queue and carried crates) on its own computer, one file per host address.

Leaving takes the player back from the host, so nothing in flight is lost or kept twice. In between, it is saved a couple of seconds after it changes, and on F5. A guest cannot save or load the host's world.

A guest that joins for the first time, or whose save is missing, broken or does not fit the host's world, starts with an empty backpack at the clearing. Leaving, or losing the connection, returns a guest to the start menu.

### Shared world

Everyone shares the world, the market and the workers. Sleep needs every player inside a cabin with enough food and water. Night wolves hunt each player separately.

Ten times a second the host sends each guest only what changed since the last update that guest confirmed, usually a few hundred bytes, so it plays over the internet as well as at home.

### Multiplayer limitations

- A guest's actions on items and buildings wait one round trip to the host (walking does not).
- The host's port must be reachable, as there is no address discovery or NAT traversal.
- Hiring is paid by whoever hires, while daily wages come from the trading post wage slot and then the host's backpack.
- The host trusts what a guest's save and position reports say, so play with people you trust.

## Saves

There are three save slots and a separate autosave written every 5 minutes. F5 and F9 quick save and load to the current slot.

The launcher keeps its Godot user-data directory under `.tools/xdg-data`. Launcher-managed saves are in `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/`:

| File | Contents |
| --- | --- |
| `settlement.json` | Slot 1 |
| `settlement-2.json` | Slot 2 |
| `settlement-3.json` | Slot 3 |
| `autosave.json` | Autosave |

A manually exported or otherwise normally launched Godot build uses Godot's standard per-user application-data location instead. Saves from the earlier, smaller maps cannot be loaded.

## Look and sound

### Environment

The environment uses a shared human scale, Terrain3D mountain ranges, a sandy shore and sea, and an irregular lake with reeds, lily pads and clustered shore stones. Meadow grass and wildflowers grow in patches; ferns, ordinary shrubs and fallen twigs add woodland ground cover. Building footprints, mine approaches and the cart trail keep the ground cover clear, with worn soil around active work areas. A worn trail runs down to the shore, and there are three tree types (oak, spruce, birch).

Workers wear blue bib overalls, cream shirts and yellow hard hats; the player wears a teal T-shirt, jeans and a daypack. The sun crosses the sky from dawn to nightfall, so shadows swing round through the day and stretch out at either end, and its light warms near sunrise and sunset. The moon takes the same path at night and waxes and wanes over a cycle of `moonCycleDays` (8): a full moon casts a faint blue light, while a new moon leaves only firelight, torches and cabin windows to see by. Night is lit by the moon, ambient light and real fire light from campfires, furnaces and cabin windows. Trees in front of the player, a worker or a structure turn see-through.

### Animation

Player and worker actions use a jointed 3D rig with keyframed poses for idling, walking, hand gathering, axe and pick strokes, hammering, bellows pumping, crate carrying, sitting at lunch (cross-legged on the ground or on a rock) and an unarmed punch. Player tool actions follow the carried tool, while worker actions follow explicit transient work state rather than display text.

The world reacts too:

- trees shake and topple into stumps;
- deposits shrink and throw chips;
- the bellows board travels with each press, and the heat gauge tracks the forge;
- a bar under the reinforced furnace and its panel dial mark the heat steel needs;
- furnaces smoke (the reinforced furnace darker and thicker), and anvils spark;
- storage lids open on transfers, and the cabin door opens as you approach;
- the market mailbox flag rises while a cart is out;
- wolves trot, leap, feed and fall dead, and a pinned player struggles on their back;
- couriers pull the trade cart.

Animations pause with gameplay.

### Sound and music

Sound effects follow the same actions:

- footsteps change on grass, rock and the lake shore;
- axes, pickaxes, anvils and swings have impacts;
- bellows puff;
- fires crackle as they catch, including a furnace lighting a new batch;
- wolves growl from afar, growl again as they close in, and bite.

Repeated sounds rotate between variants. Sounds on screen play at full volume and fade out just past its edge, so zooming out lets you hear further.

Day and night have their own music, which crossfades as the time changes and picks up where it stopped last time.

## Limitations

- Worker automation is limited to gathering and station/furnace tasks.
- Workers use simple grid detours around water and buildings; this is not a full navigation system.
- There is no charcoal system, pollution, vampire threat, or technology beyond steel in this POC.

## Licensing

The code is licensed under the GNU GPL v3; see [LICENSE](LICENSE).

Sound credits:

- Kenney ([www.kenney.nl](https://www.kenney.nl))
- Little Robot Sound Factory ([www.littlerobotsoundfactory.com](https://www.littlerobotsoundfactory.com))

Music credits:

- Alexandr Zhelanov ([soundcloud.com/alexandr-zhelanov](https://soundcloud.com/alexandr-zhelanov))
- Augmentality (Brandon Morris)
