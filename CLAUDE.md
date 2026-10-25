# Chop Chop Meow

## Working approach

- Plan ahead and separate work into well-defined tasks.
- Delegate lower-complexity tasks to GPT 6 Luna with Medium reasoning; inspect and verify the result.
- Continue until the requested goal is achieved. Ask about material design ambiguities rather than quietly changing the game.

## Project

Linux-first, single-player isometric woodland survival and production POC in Godot 4.7.2 .NET, C# / .NET 10. The Python launcher uses only the standard library. Illustrated assets are committed; playing does not call an image-generation service.

- [README.md](README.md): launch, controls, saves and limitations.
- [docs/DESIGN.md](docs/DESIGN.md): architecture and gameplay rules.
- [docs/ECONOMY.md](docs/ECONOMY.md): pricing, wages and demand rules.
- [data/game.json](data/game.json): items, recipes, building costs and pacing.
- [assets/README.md](assets/README.md): 3D model sources, icon atlases and provenance.
- [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md): original scope and decisions.

## Workflow

Start with `./build.py setup` if the tools have not been provisioned. The launcher downloads missing Godot and .NET into `.tools/`, compiles C#, and imports assets. Use `./build.py play` for the normal fresh-start game. Keep tool downloads and generated build outputs ignored; do not introduce a dependency on another local checkout.

- `./build.py test`: deterministic simulation and real Godot input/UI checks; both require explicit pass markers.
- `./build.py lint`: compilation and C# format verification.
- `./build.py build`: tests, Linux export, and an exported executable smoke test.
- Ambient music goes in `assets/music` as 192 kbps MP3 normalised to -18 LUFS integrated with true peaks below -1 dBTP (measure with ffmpeg `ebur128`, then apply the gain).
- `./build.py play -- --showcase --screenshot /absolute/path.png`: a separate visual-review fixture, never the normal opening state.

Run checks appropriate to the change. For a visual change, launch a rendered instance and inspect the actual image; headless tests cannot verify presentation. Treat Godot error output as a failure even when the process exits zero. Report limitations candidly; do not claim a full playthrough from a fixture that injects finished goods.

## Architecture

Keep authoritative simulation in `World.cs`, independent of menus or rendering. `WorldView.cs` reads state and draws; `Game.cs` handles UI/input/save integration. `GameData.cs` loads and validates editable data. Inventory rules belong in `Inventory.cs`, not in individual worker jobs.

Automated transfers must respect input/output allowlists. Empty means unrestricted. Prefer explicitly matching input-filter storage over general storage. Piles set their own filters from what they hold. Manual transfers can bypass automation filters. Never delete carried items when a worker is reassigned, fired, blocked or rerouted.

Preserve the progression: loose iron provides a tool-free bootstrap; the player starts with an iron axe; mining coal and iron deposits requires a carried pickaxe or steel tools. Workers take the tools their jobs need (axe for trees, pickaxe for deposits and mines, rake for leaves, hammer for fitting a mine) from a tool station and put them back when reassigned or going home; without one they wait and say so once. Steel requires iron, mined coal, a reinforced furnace and active bellows heat. Two assigned workers divide bellows and hauling duties.

Use representative end-to-end checks plus focused edge cases. Input tests go through `Input.ParseInputEvent`, not direct calls to input handlers. Simulation setup may construct fixtures, but tests claiming resource-to-steel progression must gather raw materials and use the actual recipes and trade delays. Do not weaken assertions to make broken behavior pass.

## Writing fast code

Use multi-threading and offload the main thread as much as possible when it's available without compromising results.

Do all instantiation and compilation during loading to avoid in-game stutter. `WorldView` builds every world object's model when a world loads and shows it once the player sees it; particles, sound players and faded material copies are made in setup. Building mid-game is for rare events: a new building, a hire, a wolf.

Code that runs every frame or tick allocates nothing once warmed up: every byte is garbage the runtime collects later in a pause that stalls a frame. Allocation is fine while loading, setting up and on rare events.

- Keep a temporary collection a feature needs every frame as a field and refill it (`Clear()`, then fill): a `List<T>` keeps its capacity. Hand out its contents as `CollectionsMarshal.AsSpan(list)` where a loop needs an array's speed.
- Let a caller pass a list or `Span<T>` to fill rather than returning a new one, and take `ReadOnlySpan<T>` rather than `IEnumerable<T>`.
- LINQ, lambdas that capture, `ToArray()`, `ToList()`, `Clone()`, string interpolation and `foreach` over an interface allocate: keep them out of those paths. Status text built from parts goes through a cache.
- On the Godot side, passing a plain string where a `StringName` is expected, `GetChildren()`, a C# handler on a Godot signal and creating a resource such as a `StyleBoxFlat` allocate too: keep `StringName`s and resources in static or instance fields, walk children by index, and override virtual methods such as `_Draw` instead of connecting handlers.
- Godot's own memory (meshes, images, packed arrays) isn't the .NET heap: these rules don't cover it.

Allocation budgets: `tests/AllocationChecks.cs` holds simulation ticks to their budget after a warm-up with `AllocationChecks.Check()`: walking and a night hunt at 0 bytes, and a working settlement at a commented byte a tick for stores growing their tables. It counts the calling thread's allocations, so it holds on any machine. A new path that runs every tick gets a case there; a path that must allocate passes its measured bytes per run as an explicit, commented budget. Real frames are held to a measured budget by `CheckWalkingFrames` in the UI checks: a change that adds a third more garbage fails it, and one that removes some lowers the budget. `CheckPanelRefresh` holds every panel page to 0 bytes a refresh while what it shows stays the same; live panel text goes through `LiveText`, which rebuilds it in a reused buffer and hands Godot a new string only when it changes.

Use performant algorithms and data structures wherever possible.
