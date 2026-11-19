# Coding rules

Project rules on top of the global coding and testing guides.
`./build.py check` enforces the parts a tool can check.

## C#

- C# 14 on .NET 10 with nullable reference types; the build treats every warning as an error.
- `dotnet format` keeps the style in `.editorconfig`: file-scoped namespaces, opening braces on the same line, four-space indents.
- Short bodies stay on one line: a guard, a one-statement `if` or a field update chain reads better than a block.
- A static field in one file of a `partial` class never depends on a static field in another file, since C# sets them up in no fixed order across files.
- A Godot node with constructor arguments builds its children in `_Ready`, or in the constructor when it lives as long as the game. A primary constructor parameter never shares a name with a base class field.
- `SetProcess(false)` goes in `_Ready`: Godot turns processing on just before calling it, so a call in the constructor does nothing.
- Godot API calls run on the main thread. Pure data work may run in parallel (`Parallel.For` in `TerrainView`) when the result is the same as running it in order.
  A Godot object only one thread uses, such as an intro frame's `Image` saved to PNG in the background, may be used off the main thread.

## Python

- `build.py` and `tools/` use only the standard library and run on Python 3.10 or newer, so a fresh checkout plays with nothing but Python.
- `mypy --strict` and `ruff` (check and format) run from versions pinned in `requirements-dev.txt`; `build.py` installs them into `.tools/python`.
- Pinned downloads go through `build.fetch_pinned`, which checks the SHA-256 before anything is unpacked.

## How the global anti-patterns apply

- **Implicit coupling:** pass `World`, `GameData` and views as arguments or fields. Static fields are for shared, immutable or cached resources (meshes, materials, `StringName`s), not game state.
- **Layer-first folders:** new code goes in a folder for its feature or actor (`src/cabins/`, `src/actors/enemies/wolf/`), with pure data in `model/` and drawing in `display/`, one class a file, and `tests/` mirroring it. A feature is its own class that `World` holds, not another `partial class World` file. The rest of `src/` is still flat; a change moves the flat files it touches.
- **Illegal states:** use enums such as `ActorAction` rather than combinations of flags or strings.
- **Validation at the boundary:** `GameData` checks its tables fit together when the game starts, `World.Deserialize` checks saves, and `Net` parses messages when they arrive. Code inside trusts what they produced.
- **Saves:** a change that would make a save written before it load wrong raises `SaveState.Version`, and `World.Deserialize` refuses every other version. Write no code that converts an old save or repairs a broken one, and no check for either: a save the game cannot take as it is, is refused with the reason. The user's own save is fixed by hand when they ask.
- **Network messages:** a change to what a host and a guest send each other raises `Net.Protocol`, so builds that differ refuse each other: a command added to or renamed in `Verb`, which travels as its number, a building or other kind added to `ThingKind`, or a field added to or taken out of an update entry.

## Architecture

- Simulation lives in `World.cs` and never touches menus or rendering, so headless checks can run gameplay.
- Inventory rules belong in `Inventory.cs`, not in individual worker jobs.
- Keep parameters, tuning values and configuration in C# as constants, enums and static tables, so code references them directly and the compiler checks them.
  A data file such as JSON is only for what has to be saved, like saves and settings, or read by another program or language.

## Tests

- Test at these seams:
  - `World`'s public methods catch simulation rule breaks and miss anything on screen.
  - `Input.ParseInputEvent` and what the player sees catch broken controls and panels, and miss how the game looks.
  - `Net`'s messages between a host and a guest process catch sync breaks and miss a real network.
- Use representative end-to-end checks plus focused edge cases.
- Checks are filed by feature, not by the kind of check. A feature whose code has a folder under `src/` has one under `tests/` with every check of it, named as in `tests/paths/`: `GravelPathChecks.cs` for its rules through `World`, `GravelPathUIChecks.cs` for what the player does and sees, `GravelPathNetChecks.cs` for its scene in the network session, and its steps beside them. Gravel paths, cabins, wolves and a player's swings are filed this way.
- The checks of features whose code is still flat are in `tests/*Checks.cs` (rules) and in `src/GameChecks.cs`, `src/SurvivalUIChecks.cs`, `src/NetChecks.cs` and `src/ServerChecks.cs` (UI, network and server). They move to a feature's folder when its code does, and a check that covers two features is split first.
- UI and network check files are parts of `Game` wherever they are, so a change to a shared step covers them all. Each feature's UI file has a runner, such as `RunGravelPathUIChecks`, that `Game.RunUIChecks` calls; its rule checks' `Run` is called from `SimulationChecks.Run`.
- The network session plays `Game.NetScenes` in order between the guest joining and leaving. A feature adds a `NetScene` in its folder: what the host puts down before the guest joins, the host's half and the guest's half. Both sides finish a scene before either starts the next, so a scene never waits on another's signals.
- Input tests go through `Input.ParseInputEvent`, not direct calls to input handlers.
- UI checks play on a server inside the window: they set up through `Hosted`, its world, then use real input and read what the screen shows or the window's `world`. A direct call into private `Game` code says in a comment why no input reaches it.
- `Frames` passes everything on the line to that server and back at once, so a window's copy is up to date after any step that waits. Setup followed at once by a read of `world` needs a `Frames` between them. A window keeps its own player's position and hotbar, so those are set with `Warp` and `SetHotbarSlot(world, …)`.
- How long an answer takes shows only on a real line: measure that in `src/NetChecks.cs` or `src/ServerChecks.cs`.
- Checks never touch `world.S`, the raw save, and never change a player's backpack, needs or respawn timer directly:
  - They read results through `World`'s public members; a missing read becomes a small read-only member like `Coins`.
  - `World.Backpack` is a read-only `InventoryView`, which `World.Transfer` takes as an end of a move, so a check moves items the way a player does.
  - They set up through real play: `Build`, `Buy`, `Consume`, `Tick` and the like, with `Add` for placing things. A time of day comes from a named step that ticks there, such as `RunUntilDawn`.
  - A state real play cannot reach, or cannot reach within a check's time limit, goes behind a named step in `tests/SetupSteps.cs` whose comment says why.
  - `./build.py check` fails on the raw save anywhere else in `tests/`, `src/*Checks.cs` or `src/*Steps.cs`.
  - A check may set fields directly on things and workers it placed, such as a farm's stage or a worker's tool, and on a `PlayerState` it builds as save text.
  - A check may place the player, with `world.Player = position`, and hold a player's input, with `Sprinting` or `GatherTarget`.
- Simulation setup may construct fixtures, but tests claiming resource-to-steel progression must gather raw materials and use the actual recipes and trade delays.
- A fixture built by editing text, such as a save edited by hand, fails when the text it edits is missing.
- A UI check picks a backpack cell with the `BagCell` step, by what the cell holds, never by its place among a page's grids: a page that gains or loses a grid shifts those numbers.
- The UI runner calls each check through `TestClean`, which fails a check that leaves the game other than it found it: the player somewhere else, a building, crate or box more or fewer on the ground, something held, a page or menu open, or a click mode on. So every check starts where the one before it did, and a check that leaves a mess fails under its own name. A check that moves the player ends with `Warp(Hosted, home)`. Checks that hand what they build to the next ones (the chest, the furnace) run through `Test`, and the last of them removes it.
- A check waits for a sound with `sounds.Heard(name, since)`, reading `sounds.Played` before the action that makes it: another sound may follow at once, such as a chest's lid after the item put into it. `sounds.Last` is for saying that nothing else played.
- UI checks wait for a condition with a time limit, not for a fixed time; a fixed wait is only for holding input, letting something play for a set time or pausing between retries.
- An open page refreshes at once after the player's own action, but only every 0.2 seconds otherwise, and a page that has to draw itself again does so a frame later.
  A check that reads the page after `Hosted.Tick` or other setup waits on something that refresh draws, such as a slot's count; after `Frames` alone it reads the page as it was.
- A check that a load restores something changes that thing between saving and loading.
- Check files hold only checks and their runners. Steps only one feature uses live in its folder, as `GravelPathSteps.cs` does, or `GravelPathUISteps.cs` for its UI and network checks. Steps several features use live in the shared step files: `tests/*Steps.cs` for rule checks and `src/*Steps.cs` for UI and network checks. A step moves to the shared files when a second feature starts to use it.
- Checks fail through the shared `Checks.Require` in `tests/Checks.cs`.
- A runner calls each check through `Checks.Test`, which logs its time and stops the process when it runs past 20 seconds. A check that needs longer, such as a whole network session, is tagged `slow: true` for 180 seconds, since the lossy relay makes reconnects vary from 20 to over 130 seconds. `./build.py` also gives all test stages together 300 seconds.
- A check over 50 lines is long: split it into one check per behaviour, or move its setup into steps. A longer one stays only when it is a single scenario that loses its point when split, and says so in its summary.
- Do not weaken assertions to make broken behavior pass.

## Fast code

Use multi-threading and offload the main thread wherever it does not change results.

Do all instantiation and compilation during loading to avoid in-game stutter.
`WorldView` builds every world object's model when a world loads and shows it once the player sees it; particles, sound players and faded material copies are made in setup.
Building mid-game is for rare events: a new building, a hire, a wolf.
Build-mode placement previews and their faded materials are setup work, made while loading.
A material builds its shader on its first draw, which stalls that frame, unless loading asks for its RID first (`ModelKit.BuildShaders`, faded copies in `ModelKit`).
Drawing builds a pipeline the first time too, up to a second on a machine with empty caches, and asking for a RID does not: `WorldView` draws one of every model behind the ground for its first frames (`WorldModels.Warm`).
A `ShaderMaterial` made outside the models goes on that warm stage too, on a small mesh of the kind it draws (`LakeDressing.Warm` for the water and lily pads): asking for its RID alone still stalls the first frame that shows it.
Sounds load when the game starts, and music pauses rather than stopping: starting an MP3 partway through decodes it from the beginning.

Code that runs every frame or tick allocates nothing once warmed up: every byte is garbage the runtime collects later in a pause that stalls a frame.
It takes no `params ReadOnlySpan<T>` either: a call to one allocated on every tick, where a plain `ReadOnlySpan<T>` parameter did not.
Allocation is fine while loading, setting up and on rare events.

- Keep a temporary collection a feature needs every frame as a field and refill it (`Clear()`, then fill): a `List<T>` keeps its capacity. Hand out its contents as `CollectionsMarshal.AsSpan(list)` where a loop needs an array's speed.
- Let a caller pass a list or `Span<T>` to fill rather than returning a new one, and take `ReadOnlySpan<T>` rather than `IEnumerable<T>`.
- LINQ, lambdas that capture, `ToArray()`, `ToList()`, `Clone()`, string interpolation and `foreach` over an interface allocate: keep them out of those paths. Status text built from parts goes through a cache.
- A read-only view of a collection that per-tick code loops over is a class or a concrete type whose public `GetEnumerator()` returns the struct enumerator, as `ItemTotals` and `ListView` do, not an interface such as `IReadOnlyDictionary`: a `foreach` over an interface boxes its enumerator.
- On the Godot side, passing a plain string where a `StringName` is expected, `GetChildren()`, a C# handler on a Godot signal and creating a resource such as a `StyleBoxFlat` allocate too. Keep `StringName`s and resources in static or instance fields, walk children by index, and override virtual methods such as `_Draw` instead of connecting handlers.
- Godot's own memory (meshes, images, packed arrays) isn't the .NET heap: these rules don't cover it.

Allocation budgets:

- `tests/AllocationChecks.cs` holds simulation ticks to their budget after a warm-up with `AllocationChecks.Check()`: walking and a night hunt at 0 bytes, and a working settlement at a commented byte count a tick for stores growing their tables. It counts the calling thread's allocations, so it holds on any machine. A warm-up runs every path its budget allows at least once, so a first-time cost stays out of the measured runs. A new path that runs every tick gets a case there; a path that must allocate passes its measured bytes per run as an explicit, commented budget.
- `CheckWalkingStaysWithinAllocationBudget` holds real frames to a measured budget: a change that adds a third more garbage fails it, and one that removes some lowers the budget. `src/ServerChecks.cs` runs it on the real server, whose updates are what a window pays for: each update's changed entries are new arrays, while numbers go into the window's copy in place through `WorldUpdates.SetNumbers`.
- `CheckPanelPagesRefreshWithoutAllocating` holds every panel page to 0 bytes a refresh while what it shows stays the same. Live panel updates go through `LiveUpdates`: `Watch` draws only when a value changes, and `Bind` rebuilds text in a reused buffer and hands Godot a new string only when it changes.
- Real frames are measured only while walking, so per-frame code for gathering, a mauling or a settlement in view has no allocation check yet; review those paths by hand.
- The server's network updates, thirty a second, go through `WorldUpdates`: its "update: working settlement" case holds an update and the guest's merge of it to a commented budget for the entries that changed, which are new arrays.
- A list that grows with play gets an update entry of its own, as gravel path tiles (`EntryKey.Paths`) and packed crates (`EntryKey.Crates`) have, and stays out of the world's state entry, which is written again on every update because time moves.

Measure frame rates on the RTX 4080 at 2560×1440 in a real window launched by `./build.py play`, and check the device line in the log; integrated-GPU and headless numbers don't count.
`./build.py play -- --performance-run /absolute/path` followed by `python3 tools/performance_report.py /absolute/path` records a sprint from the sea past a staffed camp and the lake to the valley's top corner, by day and at night with a lit torch, split by leg.
That run plays on a real server process and measures the window, merging its updates included.

Keep models under the triangle ceilings in [assets/README.md](assets/README.md#changing-a-model).

Prefer engine and platform defaults over hardware detection, compatibility shims or tuning to match an earlier look.
Use performant algorithms and data structures.
