# Chop Chop Meow

A single-player isometric woodland survival and production prototype. Gather nearby materials, build a small settlement, produce and sell steel tools, then hire workers to automate resource gathering and furnace work. The selected art direction is smooth illustrated sprites with natural colors and readable silhouettes; the previews in [docs/art](docs/art/README.md) show the two directions that were considered.

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

Play accepts Godot arguments after `--`. `--showcase` loads a populated review fixture instead of a fresh game; `--screenshot /tmp/chop.png` captures the running view after startup and exits:

```sh
./build.py play -- --showcase --screenshot /tmp/chop-chop-meow.png
```

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
| Tab | Open or close objectives and help |
| 1–9 | Use the corresponding hotbar slot; defaults: axe, pickaxe, chest, campfire, furnace, anvil, bellows furnace, berries, water |
| Space | Swing axe, or pump the selected nearby reinforced furnace |
| F5 / F9 | Save / load |
| Escape | Cancel placement/assignment, close a panel, or pause/resume |
| Mouse wheel | Zoom |
| Shift while transferring | Move a full stack |

The interface also exposes visible buttons for common actions. Worker assignment starts from worker management; select a resource to assign a gatherer. Assigning a building job to a furnace enables material hauling and operation, with two workers splitting hauling and bellows work.

Health, hunger, thirst, coins, carrying capacity, and daily wages are shown in Inventory only. Assign hotbar slots from **Inventory → Assign hotbar slots**. Slots reference carried items or building plans; they are not extra storage. Consumable slots use one item, building slots enter placement, and tools select a working reference. A selected raw-material stack can be deposited by clicking a nearby chest. Hotbar assignments persist in saves.

## What the prototype includes

The opening progression covers hand gathering, construction, food and water, day/night, shelter, mining, iron and steel production, village trade, and worker hiring. The basic furnace accepts wood or mined coal as fuel. The reinforced furnace uses mined coal only and requires heat from manual or worker-operated bellows. Charcoal production is not implemented, and charcoal cannot substitute for mined coal.

Market orders settle after a visible 30-second cart trip. Prices respond to supply and demand. Workers are individual entities with assignments, wages, cargo, and visible work status. Storage input filters govern automated deposits; output filters govern worker withdrawals. Empty filters allow all items. Workers prefer explicitly accepting chests and reroute cargo when filters change. Manual inventory transfers ignore these automation filters.

The world is a fixed, compact map. Workers use simple grid detours around water and buildings; this is not a full navigation system. The player and workers use a small set of directional walk frames, not a full animation set. Worker automation is limited to gathering and station/furnace tasks, with at most eight workers. There is no charcoal system, pollution, vampire threat, multiplayer, or technology beyond steel in this POC.

The environment uses a shared human scale, a natural shoreline, a quiet grass field and worn trail, and less densely repeated resource clusters. Workers have a separate illustrated skin with blue overalls and cream shirts.
