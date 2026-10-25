# Chop Chop Meow

A single-player isometric woodland survival and production prototype. Gather nearby materials, build a small settlement, craft parts and tools, pack steel tools into crates and sell them, then hire workers to automate resource gathering and furnace work. The world is rendered in 3D with code-built low-poly models under an orthographic isometric camera: natural woodland, modern DIY buildings and readable silhouettes. Hotbar and toolbar icons are the original illustrated set; the earlier 2D art directions are kept in [docs/art](docs/art/README.md).

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

The game opens on a start menu with Continue (the most recent save), New game, Load game, Options and Quit. There are three save slots and a separate autosave written every 90 seconds. Options holds a window resolution picker; the choice is stored in `settings.cfg` and applied at launch.

The launcher keeps its Godot user-data directory under `.tools/xdg-data`; launcher-managed saves are in `.tools/xdg-data/godot/app_userdata/Chop Chop Meow/` as `settlement.json` (slot 1), `settlement-2.json`, `settlement-3.json` and `autosave.json`. A manually exported or otherwise normally launched Godot build uses Godot's standard per-user application-data location instead.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrow keys | Move in screen directions |
| Hold right mouse button on a resource | Approach and gather while held |
| Hold right mouse button on a placed crate | Walk over and pick it up with its contents |
| Left mouse button | Select a resource, building, crate or worker; place selected building; clicking a cabin walks you inside; on open ground, use the item in hand (swing a tool or torch, eat, drink) |
| E | Inspect the selected object if nearby, otherwise the nearest nearby object |
| B | Open building plans |
| I | Open backpack |
| C | Open the crafting menu |
| Tab | Open or close the field guide |
| 1–9 or click a hotbar slot | Put that slot in hand, shown with an outline; building plans and crates go straight to placement; defaults: axe, pickaxe, chest, campfire, furnace, anvil, crafting bench, berries, water |
| Space | Swing axe, or pump the selected nearby reinforced furnace |
| F5 / F9 | Quick save / load to the current slot; until a slot is chosen they open the save or load screen |
| Escape | Cancel placement/assignment, close a panel, or open the pause menu with goals, save, load and options |
| Mouse wheel | Zoom |
| Click / Shift-click an inventory cell | Move one stack / every unit of that item |
| Drag a cell | Move its stack to another inventory or a station input slot; drop an item or building plan on the hotbar to assign it |

The bottom bar holds icon buttons for building (hammer), the backpack, crafting (chisel), workers (hard hat) and the field guide (book), next to the hotbar. Worker assignment starts from worker management; select a resource to assign a gatherer. Assigning a building job to a furnace enables material hauling and operation, with one worker on the bellows and any others hauling.

Health, hunger and thirst are a heart, a stomach and a water droplet next to the clock; each empties as its level falls and its outline turns red at 25% or less. A hit flashes red along the screen edges. A full stomach lasts one in-game day and a full droplet half a day; a day lasts 14 minutes. Coins are a backpack item that stacks to 1000; purchases, hiring and wages are paid from carried coins, sale proceeds arrive in the signpost's market output, and coins go into the recovery box on death like any other item. Health, carrying capacity, and daily wages are also shown in Inventory. Stations show one box per recipe input and output, each holding a single stack of 64, with the item's icon above the box and a progress bar between them. Click a backpack item to fill its input box, or drag it in; click or drag a box to take its contents back. Inventories are grids of slots and every item stacks to 64 except coins (1000), packed crates (1) and water bottles, which take a slot each unless they are in a crate: the backpack has 24 slots, chests 18, and a worker carries one stack of up to 5. Clicking a berry, cooked meat, canned food or water cell in the backpack consumes one, clicking a torch lights it and clicking a crate starts placing it; raw meat must be cooked first. Building plans are an icon grid; hovering a plan shows its description and what it still needs. Assign hotbar slots from **Inventory → Assign hotbar slots**. Slots reference carried items or building plans; they are not extra storage. Consumable slots use one item, building slots enter placement, and tools select a working reference. A selected raw-material stack can be deposited by clicking a nearby chest. Hotbar assignments persist in saves.

## What the prototype includes

Death drops your backpack in a recovery box and respawns you at the clearing after 30 seconds; emptied boxes disappear and workers never use them as storage.

Wolves hunt at night and run nearly as fast as you. A burning campfire keeps them away, and a wolf already chasing you follows you into its light for a few seconds; one log burns about 4 in-game hours. A torch burns just as long, once. With a torch in hand, the first left click on open ground strikes flint and steel for 2 seconds to light it; after that each click swings it, which briefly widens its light by a fifth. Swinging four times within 2 seconds, as fast as swings go, puts it out with a message; relighting takes the same 2 seconds and keeps the burn time it had left. While it burns, a lone wolf still creeps in slowly, but a swing toward it scares it into following you at the edge of the light. More wolves gather over time, and once five are around you they attack together and swings no longer scare them. A direct hit with the torch hurts a wolf and sends it running for a moment. Cabin windows glow but do not deter wolves. Click a cabin to step inside: wolves back off at once, and any movement steps you back out. You can sleep until dawn only if your food and water will last until morning; otherwise the sleep button explains why. At half health or less, away from fire and shelter, a nearby wolf leaps for your throat: you are pinned and cannot move or fight, the rest of the pack runs in to feed, and you die within seconds. A wolf kill destroys all food in the recovery box; the pack keeps feeding until you respawn. A slain wolf falls over and leaves a carcass that lasts until it is butchered: hold right mouse on it for 4 raw meat. Cook 2 raw meat into 2 cooked meat at the stone furnace (Cook meat recipe, one fuel per batch); only cooked meat can be eaten. Completing the goal chain shows an ending screen with Restart and Keep playing.

The opening progression covers hand gathering, construction, food and water, day/night, shelter, mining, iron and steel production, village trade, and worker hiring. The basic furnace accepts wood or mined coal as fuel. The reinforced furnace uses mined coal only and requires heat from manual or worker-operated bellows; below the clear bar it keeps burning coal without making steel. Anvils only work while someone stands at them hammering. Charcoal production is not implemented, and charcoal cannot substitute for mined coal.

Crafting has two places. The crafting menu (C) works on the go at base speed, about 10 seconds a recipe, and queues up to eight recipes; clicking a queued one cancels it and returns its materials. Recipes are grouped into boxes by the tool they need, with that tool's icon on the box corner, red while you carry none. A stone chisel takes 2 stone and no tool. With a chisel you carve planks (1 wood → 4), wood parts (1 wood → 2), a wooden mallet and torches (3 wood). The crafting bench costs 10 planks, works twice as fast and must be worked like the anvil, by you or an assigned worker. It has a chisel slot and a hammer slot, and a slotted tool's speed multiplies its rate. It makes everything the menu makes plus crates (6 planks, hammer) and the iron and steel axe, pickaxe and hammer (hammer). The anvil turns one iron or steel bar into one part. A small tool such as a hammer takes 1 part of its metal and a big one such as an axe or pickaxe 2, plus 1 wood part for the handle.

A wood crate holds 12 items in total, as one stack or as single items, and its slots grow and shrink with its contents. Place one from the backpack or hotbar, click it to open it, and hold right mouse on it to pick it up with its contents packed inside. Workers never use crates. Putting a crate inside a crate asks first; saying yes ends the game.

The village signpost has three icon buttons: Market, Workers and the price sheet. The market works like a station with six unlabeled input boxes and six output boxes. It only takes crates, and only of raw or cooked meat, berries, or steel axes, pickaxes and hammers; a sale pays for the goods and the crate. Load crates, press Sell, and a cart takes them to Bramblewick and returns 30 seconds later with coins in the output boxes. The price sheet opens to the left of the panel and lists sell and buy prices with a Buy button for what the village sells: single steel tools, and canned food and water by the crate of 12. Bought goods also arrive in the output boxes. The sheet only shows sale prices as last heard from the village and says how many days old they are: it updates when a cart returns, or when a worker assigned to the signpost is sent to fetch fresh prices (a one-time errand down the road and back). Selling lowers an item's price unit by unit and the price recovers over time; buying costs a fixed 1.25× the base price, so a buy-and-resell loop never profits. The Workers tab offers several hiring deals: more coins up front means a lower daily wage. Workers are individual entities with assignments, wages, cargo, and visible work status. An unpaid worker strikes, and owed wages stop growing until the strike is settled. Storage input filters govern automated deposits; output filters govern worker withdrawals. Empty filters allow all items. Workers prefer explicitly accepting chests and reroute cargo when filters change. Manual inventory transfers ignore these automation filters.

Fog of war covers the map. Ground you have never seen is black. You see live only within a circle around you (`sightRadius` in `data/game.json`, 20 tiles), which fills the screen at full zoom-out. Ground you have seen before is greyed out and shows things as you last saw them: a tree felled out of sight still stands there until you look again. Remembered fires and furnaces show unlit, and wolves, workers and carts are not shown. Explored ground and remembered things are kept in saves.

The world is a fixed, compact map. Iron and coal deposits sit at the foot of two cliffs near the map edge, left for the player to find; loose pieces lie a little further out and are picked up whole with one right-click, with no tool. Workers use simple grid detours around water and buildings; this is not a full navigation system. Player and worker actions use a jointed 3D rig with keyframed poses for idling, walking, hand gathering, iron and steel axe and pick strokes, hammering, bellows pumping, crate carrying and an unarmed punch. Player tool actions follow the carried tool, while worker actions follow explicit transient work state rather than display text. The world reacts too: trees shake and topple into stumps, deposits shrink and throw chips, the bellows board travels with each press, the heat gauge tracks the forge, a bar under the bellows furnace and its panel gauge mark the clear bar steel needs, furnaces smoke (the bellows furnace darker and thicker), anvils spark, storage lids open on transfers, the cabin door opens as you approach, the market mailbox flag rises while a cart is out, wolves trot, leap, feed and fall dead, a pinned player struggles on their back, and couriers pull the trade cart. Animations pause with gameplay. Sound effects follow the same actions: footsteps change on grass, rock and the pond shore, axes, anvils and swings have impacts, fires crackle as they catch, including a furnace lighting a new batch, and wolves growl from afar, growl again as they close in and bite. Repeated sounds rotate between variants. Sounds on screen play at full volume and fade out just past its edge, so zooming out lets you hear further. Trees in front of the player, a worker or a structure turn see-through. Worker automation is limited to gathering and station/furnace tasks, with at most eight workers. There is no charcoal system, pollution, vampire threat, multiplayer, or technology beyond steel in this POC.

The environment uses a shared human scale, an irregular pond with reeds and lily pads, a grass field with scattered tufts and wildflowers, a worn trail, and three tree types (oak, spruce, birch). Workers wear blue bib overalls, cream shirts and yellow hard hats; the player wears a teal T-shirt, jeans and a daypack. Night is lit by the sun, ambient light and real fire light from campfires, furnaces and cabin windows.

## Licensing

The code is licensed under the GNU GPL v3; see [LICENSE](LICENSE).

Sound credits:

- Kenney ([www.kenney.nl](https://www.kenney.nl))
- Little Robot Sound Factory ([www.littlerobotsoundfactory.com](https://www.littlerobotsoundfactory.com))
