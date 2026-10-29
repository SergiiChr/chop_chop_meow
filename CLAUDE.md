# Chop Chop Meow

## Working approach

- Plan ahead and split work into well-defined tasks.
- Hand lower-complexity tasks to a cheaper, faster model or subagent; inspect and verify its result.
- Continue until the requested goal is achieved. Ask about material design ambiguities rather than quietly changing the game.
- Prefer engine and platform defaults over hardware detection, compatibility shims or tuning to match an earlier look.

## Project

Linux-first isometric woodland survival and production POC for one player, or up to four over the network, in Godot 4.7.2 .NET, C# / .NET 10.
The Python launcher uses only the standard library.
Icons and models are committed; playing does not call an image-generation service.

Read before changing:

| Doc | Read when |
| --- | --- |
| [README.md](README.md) | Changing anything the player sees: controls, rules, panels, saves |
| [docs/DESIGN.md](docs/DESIGN.md) | Changing code: source layout, simulation rules, confirmed design decisions |
| [docs/ECONOMY.md](docs/ECONOMY.md) | Changing prices, wages, recipes or demand |
| [docs/RELEASE.md](docs/RELEASE.md) | Branching, checking and handing off a change |
| [data/game.json](data/game.json) | Tuning items, recipes, building costs and pacing |
| [assets/README.md](assets/README.md) | Changing models, icons, sounds, music or their sources |

Docs describe the game as it is now.
Update them in the same change as the code; git keeps the history.

## Workflow

- Several agent sessions share the main checkout. Do branch work in a worktree (see [docs/RELEASE.md](docs/RELEASE.md)).
- Always run Godot through `./build.py`. Calling the binary in `.tools/` directly picks up the system .NET and crashes; add a `build.py` action or pass flags after `./build.py play --` instead.
- `./build.py setup` downloads missing Godot and .NET into `.tools/`, compiles C# and imports assets. Keep tool downloads and build output ignored; never depend on another local checkout.
- `./build.py play`: the normal fresh-start game.
- `./build.py test`: simulation, real Godot input/UI and two-process network checks; each needs its explicit pass marker.
- `./build.py lint`: compilation and C# format verification.
- `./build.py build`: tests, Linux export and a smoke test of the exported executable.
- `./build.py play -- --showcase --screenshot /absolute/path.png`: a visual-review fixture, never the normal opening state.
- Ambient music goes in `assets/music` as 192 kbps MP3 normalised to -18 LUFS integrated with true peaks below -1 dBTP (measure with ffmpeg `ebur128`, then apply the gain).

Run checks that fit the change.
To play the real window with keyboard and mouse, use the `playtest` skill (`.claude/skills/playtest`).
For a visual change, launch a rendered instance and inspect the actual image; headless tests cannot verify presentation.
Treat Godot error output as a failure even when the process exits zero.
Report limitations candidly; do not claim a full playthrough from a fixture that injects finished goods.

## Architecture

Keep authoritative simulation in `World.cs`, independent of menus or rendering.
`WorldView.cs` reads state and draws; `Game.cs` handles UI, input and saves.
`GameData.cs` loads and validates editable data.
Inventory rules belong in `Inventory.cs`, not in individual worker jobs.

Automated transfers respect input/output allowlists, where empty means unrestricted.
Prefer storage whose input filter explicitly matches over general storage.
Piles set their own filters from what they hold.
Manual transfers can bypass automation filters.
Never delete carried items when a worker is reassigned, fired, blocked or rerouted.

Preserve the progression:

- The player starts with a steel axe and no pickaxe. There is no loose iron and there are no iron tools.
- The first pickaxe is bought at a trading post with crates of berries. Mining coal, iron and tin deposits needs a carried pickaxe; loose coal needs no tool.
- Workers take the tools their jobs need (axe for trees, pickaxe for deposits and mines, rake for leaves, hammer for fitting a mine) from a tool station and put them back when reassigned or going home. Without one they wait and say so once.
- Steel needs iron, mined coal, a reinforced furnace and active bellows heat. Two assigned workers divide bellows and hauling.

## Tests

Use representative end-to-end checks plus focused edge cases.
Input tests go through `Input.ParseInputEvent`, not direct calls to input handlers.
Simulation setup may construct fixtures, but tests claiming resource-to-steel progression must gather raw materials and use the actual recipes and trade delays.
Do not weaken assertions to make broken behavior pass.

## Writing fast code

Use multi-threading and offload the main thread wherever it does not change results.

Do all instantiation and compilation during loading to avoid in-game stutter.
`WorldView` builds every world object's model when a world loads and shows it once the player sees it; particles, sound players and faded material copies are made in setup.
Building mid-game is for rare events: a new building, a hire.
A material builds its shader on its first draw, which stalls that frame, unless loading asks for its RID first (`ModelKit.BuildShaders`, faded copies in `ModelKit`).
Sounds load when the game starts, and music pauses rather than stopping: starting an MP3 partway through decodes it from the beginning.

Code that runs every frame or tick allocates nothing once warmed up: every byte is garbage the runtime collects later in a pause that stalls a frame.
Allocation is fine while loading, setting up and on rare events.

- Keep a temporary collection a feature needs every frame as a field and refill it (`Clear()`, then fill): a `List<T>` keeps its capacity. Hand out its contents as `CollectionsMarshal.AsSpan(list)` where a loop needs an array's speed.
- Let a caller pass a list or `Span<T>` to fill rather than returning a new one, and take `ReadOnlySpan<T>` rather than `IEnumerable<T>`.
- LINQ, lambdas that capture, `ToArray()`, `ToList()`, `Clone()`, string interpolation and `foreach` over an interface allocate: keep them out of those paths. Status text built from parts goes through a cache.
- On the Godot side, passing a plain string where a `StringName` is expected, `GetChildren()`, a C# handler on a Godot signal and creating a resource such as a `StyleBoxFlat` allocate too. Keep `StringName`s and resources in static or instance fields, walk children by index, and override virtual methods such as `_Draw` instead of connecting handlers.
- Godot's own memory (meshes, images, packed arrays) isn't the .NET heap: these rules don't cover it.

Allocation budgets:

- `tests/AllocationChecks.cs` holds simulation ticks to their budget after a warm-up with `AllocationChecks.Check()`: walking and a night hunt at 0 bytes, and a working settlement at a commented byte count a tick for stores growing their tables. It counts the calling thread's allocations, so it holds on any machine. A new path that runs every tick gets a case there; a path that must allocate passes its measured bytes per run as an explicit, commented budget.
- `CheckWalkingFrames` in the UI checks holds real frames to a measured budget: a change that adds a third more garbage fails it, and one that removes some lowers the budget.
- `CheckPanelRefresh` holds every panel page to 0 bytes a refresh while what it shows stays the same. Live panel text goes through `LiveText`, which rebuilds it in a reused buffer and hands Godot a new string only when it changes.

Measure frame rates on the RTX 4080 at 2560×1440 in a real window launched by `./build.py play`, and check the device line in the log; integrated-GPU and headless numbers don't count.
`./build.py play -- --performance-run /absolute/path` followed by `python3 tools/performance_report.py /absolute/path` records a walking, day/night and staffed-settlement run.

Keep models under the triangle ceilings in [assets/README.md](assets/README.md#changing-a-model).

Use performant algorithms and data structures wherever possible.
