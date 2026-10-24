# Chop Chop Meow

A single-player isometric woodland survival and production prototype. Gather nearby materials, build a small settlement, produce and sell steel tools, then hire workers to automate resource gathering and furnace work. The world is rendered in 3D with code-built low-poly models under an orthographic isometric camera: natural woodland, modern DIY buildings and readable silhouettes. Hotbar and toolbar icons are the original illustrated set; the earlier 2D art directions are kept in [docs/art](docs/art/README.md).

## Run on Linux

From the project directory:

```sh
./build.py play
```

The launcher keeps Godot 4.7.2 .NET, .NET 10, import files, export templates, and build output under `.tools/` and `build/`. First-time setup downloads missing tools and needs internet access. To use an installed Godot 4.7.2 .NET binary, set `GODOT_BIN=/path/to/godot`. Interactive play needs a working Linux graphics driver; the project uses Godot's Compatibility renderer.

```sh
./build.py setup    # prepare local tools, compile C#, and import assets
./build.py editor   # open the Godot editor
./build.py test     # run headless simulation and input/UI checks
./build.py build    # test, export, and smoke-test build/ChopChopMeow.x86_64
./build.py lint     # verify C# formatting and compile
```

`build` also produces `build/ChopChopMeow-linux-x86_64.tar.gz`. Extract it and run `ChopChopMeow/ChopChopMeow.x86_64`; keep the adjacent `.pck` and runtime folder together. The bundle includes the .NET runtime and does not need the SDK. The extracted bundle is smoke-tested without the development SDK in its environment.

Play accepts Godot arguments after `--`. `--showcase` loads a populated review fixture instead of a fresh game; `--showcase --wolves` adds a night ambush to that fixture; `--screenshot /tmp/chop.png` captures the running view after startup and exits:

```sh
./build.py play -- --showcase --screenshot /tmp/chop-chop-meow.png
```

For animation review, `--animation-review` shows every actor action on rotating rigs and `--animation-capture /tmp/directory` captures its frames for visual inspection:

```sh
./build.py play -- --animation-review --animation-capture /tmp/chop-chop-meow-animation
```

These options are developer review tools; they do not represent a normal fresh-start playthrough.

The launcher keeps its Godot user-data directory under `.tools/xdg-data`; a launcher-managed save is at `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/settlement.json`. A manually exported or otherwise normally launched Godot build uses Godot's standard per-user application-data location instead.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrow keys | Move in screen directions |
| Hold right mouse button on a resource | Approach and gather while held |
| Left mouse button | Select a resource, building, or worker; place selected building |
| E | Inspect the selected object if nearby, otherwise the nearest nearby object |
| B | Open building plans |
| I | Open backpack; crafting is available when inspecting a station |
| Tab | Open or close the field guide |
| 1–9 | Use the corresponding hotbar slot; defaults: axe, pickaxe, chest, campfire, furnace, anvil, bellows furnace, berries, water |
| Space | Swing axe, or pump the selected nearby reinforced furnace |
| F5 / F9 | Save / load |
| Escape | Cancel placement/assignment, close a panel, or open the pause menu with goals, save and load |
| Mouse wheel | Zoom |
| Click / Shift-click an inventory cell | Move one stack / every unit of that item |
| Drag a cell | Move its stack to another inventory or a station input slot; drop an item or building plan on the hotbar to assign it |

The bottom bar holds icon buttons for building (hammer), the backpack, workers (hard hat) and the field guide (book), next to the hotbar. Worker assignment starts from worker management; select a resource to assign a gatherer. Assigning a building job to a furnace enables material hauling and operation, with one worker on the bellows and any others hauling.

Hunger and thirst are a stomach and a water droplet next to the clock; each empties as its level falls and its outline turns red at 25% or less. Health, coins, carrying capacity, and daily wages are shown in Inventory. Stations show one box per recipe input and output, each holding a single stack of 64, with the item's icon above the box and a progress bar between them. Click a backpack item to fill its input box, or drag it in; click or drag a box to take its contents back. Inventories are grids of slots and every item stacks to 64: the backpack has 24 slots, chests 18, and a worker carries one stack of up to 5. Clicking a berry, cooked meat or water cell in the backpack consumes one; raw meat must be cooked first. Building plans are an icon grid; hovering a plan shows its description and what it still needs. Assign hotbar slots from **Inventory → Assign hotbar slots**. Slots reference carried items or building plans; they are not extra storage. Consumable slots use one item, building slots enter placement, and tools select a working reference. A selected raw-material stack can be deposited by clicking a nearby chest. Hotbar assignments persist in saves.

## What the prototype includes

Death drops your backpack in a recovery box and respawns you at the clearing after 30 seconds; emptied boxes disappear and workers never use them as storage.

Wolves hunt at night and run nearly as fast as you. At half health or less, away from fire and shelter, a nearby wolf leaps for your throat: you are pinned and cannot move or fight, the rest of the pack runs in to feed, and you die within seconds. A wolf kill destroys all food in the recovery box; the pack keeps feeding until you respawn. A slain wolf falls over and leaves a carcass that lasts until it is butchered: hold right mouse on it for 4 raw meat. Cook 2 raw meat into 2 cooked meat at the stone furnace (Cook meat recipe, one fuel per batch); only cooked meat can be eaten. Completing the goal chain shows an ending screen with Restart and Keep playing.

The opening progression covers hand gathering, construction, food and water, day/night, shelter, mining, iron and steel production, village trade, and worker hiring. The basic furnace accepts wood or mined coal as fuel. The reinforced furnace uses mined coal only and requires heat from manual or worker-operated bellows. Charcoal production is not implemented, and charcoal cannot substitute for mined coal.

Market orders settle after a visible 30-second cart trip. Selling lowers an item's price unit by unit and the price recovers over time; buying costs a fixed 1.25× the base price, so a buy-and-resell loop never profits. Workers are individual entities with assignments, wages, cargo, and visible work status. An unpaid worker strikes, and owed wages stop growing until the strike is settled. Storage input filters govern automated deposits; output filters govern worker withdrawals. Empty filters allow all items. Workers prefer explicitly accepting chests and reroute cargo when filters change. Manual inventory transfers ignore these automation filters.

The world is a fixed, compact map. Iron and coal deposits sit at the foot of two cliffs near the map edge; loose pieces lie a little further out and are picked up whole with one right-click, with no tool. Workers use simple grid detours around water and buildings; this is not a full navigation system. Player and worker actions use a jointed 3D rig with keyframed poses for idling, walking, hand gathering, iron and steel axe and pick strokes, hammering, bellows pumping, crate carrying and an unarmed punch. Player tool actions follow the carried tool, while worker actions follow explicit transient work state rather than display text. The world reacts too: trees shake and topple into stumps, deposits shrink and throw chips, the bellows board travels with each press, the heat gauge tracks the forge, anvils spark, storage lids open on transfers, the cabin door opens as you approach, the market mailbox flag rises while a cart is out, wolves trot, leap, feed and fall dead, a pinned player struggles on their back, and couriers pull the trade cart. Animations pause with gameplay. Trees in front of the player, a worker or a structure turn see-through. Worker automation is limited to gathering and station/furnace tasks, with at most eight workers. There is no charcoal system, pollution, vampire threat, multiplayer, or technology beyond steel in this POC.

The environment uses a shared human scale, an irregular pond with reeds and lily pads, a grass field with scattered tufts and wildflowers, a worn trail, and three tree types (oak, spruce, birch). Workers wear blue bib overalls, cream shirts and yellow hard hats; the player wears a teal T-shirt, jeans and a daypack. Night is lit by the sun, ambient light and real fire light from campfires, furnaces and cabin windows.
