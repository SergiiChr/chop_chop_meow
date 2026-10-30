# Coding rules

Project rules on top of the global coding and testing guides.
`./build.py check` enforces the parts a tool can check.

## C#

- C# 14 on .NET 10 with nullable reference types; the build treats every warning as an error.
- `dotnet format` keeps the style in `.editorconfig`: file-scoped namespaces, opening braces on the same line, four-space indents.
- Short bodies stay on one line: a guard, a one-statement `if` or a field update chain reads better than a block.
- A static field in one file of a `partial` class never depends on a static field in another file, since C# sets them up in no fixed order across files.
- Godot API calls run on the main thread. Pure data work may run in parallel (`Parallel.For` in `Terrain3DView`) when the result is the same as running it in order.

## Python

- `build.py` and `tools/` use only the standard library and run on Python 3.10 or newer, so a fresh checkout plays with nothing but Python.
- `mypy --strict` and `ruff` (check and format) run from versions pinned in `requirements-dev.txt`; `build.py` installs them into `.tools/python`.
- Pinned downloads go through `build.fetch_pinned`, which checks the SHA-256 before anything is unpacked.

## How the global anti-patterns apply

- **Implicit coupling:** pass `World`, `GameData` and views as arguments or fields. Static fields are for shared, immutable or cached resources (meshes, materials, `StringName`s), not game state.
- **Layer-first folders:** the project keeps the flat `src/` and `tests/` layout; group related code by file name prefix instead of adding folders.
- **Illegal states:** use enums such as `ActorAction` rather than combinations of flags or strings.
- **Validation at the boundary:** `GameData` validates `data/game.json` when it loads, `World.Deserialize` checks saves with the same rules `Net` uses for what guests send, and `Net` parses messages when they arrive. Code inside trusts what they produced.
- **Old saves:** upgrades to an older save's data run once when it loads, never in the tick.

## Architecture

- Simulation lives in `World.cs` and never touches menus or rendering, so headless checks can run gameplay.
- Inventory rules belong in `Inventory.cs`, not in individual worker jobs.
- Balance and recipe values belong in `data/game.json`, not in code.

## Tests

- Test at these seams:
  - `World`'s public methods catch simulation rule breaks and miss anything on screen.
  - `Input.ParseInputEvent` and what the player sees catch broken controls and panels, and miss how the game looks.
  - `Net`'s messages between a host and a guest process catch sync breaks and miss a real network.
- Use representative end-to-end checks plus focused edge cases.
- UI checks live in `src/GameChecks.cs`, `src/SurvivalUIChecks.cs` and `src/NetChecks.cs`; a change to a shared check helper covers all three.
- Input tests go through `Input.ParseInputEvent`, not direct calls to input handlers.
- Simulation setup may construct fixtures, but tests claiming resource-to-steel progression must gather raw materials and use the actual recipes and trade delays.
- A fixture built by editing text, such as an old save, fails when the text it edits is missing.
- UI checks wait for a condition with a time limit, not for a fixed time; a fixed wait is only for holding input or letting something play for a set time.
- A check that a load restores something changes that thing between saving and loading.
- Check files hold only checks and their runners. Steps live in feature files: `tests/WorldSteps.cs`, `PlayerSteps.cs`, `WorkerSteps.cs`, `EconomySteps.cs` and `ModelSteps.cs` for simulation checks, and `src/InputSteps.cs`, `PanelSteps.cs` and `SessionSteps.cs` for UI and network checks.
- Checks fail through the shared `Checks.Require` in `tests/Checks.cs`.
- Do not weaken assertions to make broken behavior pass.

## Fast code

Use multi-threading and offload the main thread wherever it does not change results.

Do all instantiation and compilation during loading to avoid in-game stutter.
`WorldView` builds every world object's model when a world loads and shows it once the player sees it; particles, sound players and faded material copies are made in setup.
Building mid-game is for rare events: a new building, a hire, a wolf.
Build-mode placement previews and their faded materials are setup work, made while loading.
A material builds its shader on its first draw, which stalls that frame, unless loading asks for its RID first (`ModelKit.BuildShaders`, faded copies in `ModelKit`).
Sounds load when the game starts, and music pauses rather than stopping: starting an MP3 partway through decodes it from the beginning.

Code that runs every frame or tick allocates nothing once warmed up: every byte is garbage the runtime collects later in a pause that stalls a frame.
Allocation is fine while loading, setting up and on rare events.

- Keep a temporary collection a feature needs every frame as a field and refill it (`Clear()`, then fill): a `List<T>` keeps its capacity. Hand out its contents as `CollectionsMarshal.AsSpan(list)` where a loop needs an array's speed.
- Let a caller pass a list or `Span<T>` to fill rather than returning a new one, and take `ReadOnlySpan<T>` rather than `IEnumerable<T>`.
- LINQ, lambdas that capture, `ToArray()`, `ToList()`, `Clone()`, string interpolation and `foreach` over an interface allocate: keep them out of those paths. Status text built from parts goes through a cache.
- A read-only view of a collection that per-tick code loops over is a class or a concrete type whose public `GetEnumerator()` returns the struct enumerator, as `ItemTotals` does, not an interface such as `IReadOnlyDictionary`: a `foreach` over an interface boxes its enumerator.
- On the Godot side, passing a plain string where a `StringName` is expected, `GetChildren()`, a C# handler on a Godot signal and creating a resource such as a `StyleBoxFlat` allocate too. Keep `StringName`s and resources in static or instance fields, walk children by index, and override virtual methods such as `_Draw` instead of connecting handlers.
- Godot's own memory (meshes, images, packed arrays) isn't the .NET heap: these rules don't cover it.

Allocation budgets:

- `tests/AllocationChecks.cs` holds simulation ticks to their budget after a warm-up with `AllocationChecks.Check()`: walking and a night hunt at 0 bytes, and a working settlement at a commented byte count a tick for stores growing their tables. It counts the calling thread's allocations, so it holds on any machine. A new path that runs every tick gets a case there; a path that must allocate passes its measured bytes per run as an explicit, commented budget.
- `CheckWalkingFrames` in the UI checks holds real frames to a measured budget: a change that adds a third more garbage fails it, and one that removes some lowers the budget.
- `CheckPanelRefresh` holds every panel page to 0 bytes a refresh while what it shows stays the same. Live panel text goes through `LiveText`, which rebuilds it in a reused buffer and hands Godot a new string only when it changes.
- Real frames are measured only while walking, so per-frame code for gathering, a mauling or a settlement in view has no allocation check yet; review those paths by hand.
- The host's network updates, ten a second, go through `WorldUpdates`: its "update: working settlement" case holds an update and the guest's merge of it to a commented budget for the entries that changed, which are new arrays.

Measure frame rates on the RTX 4080 at 2560×1440 in a real window launched by `./build.py play`, and check the device line in the log; integrated-GPU and headless numbers don't count.
`./build.py play -- --performance-run /absolute/path` followed by `python3 tools/performance_report.py /absolute/path` records a walking, day/night and staffed-settlement run.

Keep models under the triangle ceilings in [assets/README.md](assets/README.md#changing-a-model).

Prefer engine and platform defaults over hardware detection, compatibility shims or tuning to match an earlier look.
Use performant algorithms and data structures.
