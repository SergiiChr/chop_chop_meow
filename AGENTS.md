# Chop Chop Meow

Linux-first isometric woodland survival and production POC for one player, or up to four over the network, in Godot 4.7.2 .NET, C# / .NET 10.
The Python launcher `build.py` uses only the standard library.
Icons and models are committed; playing does not call an image-generation service.

## Read before changing

| Doc | Read when |
| --- | --- |
| [CODING.md](CODING.md) | Writing, changing or reviewing any code or test |
| [docs/DESIGN.md](docs/DESIGN.md) | Changing game code: source files, simulation rules, confirmed design decisions |
| [README.md](README.md) | Changing anything the player sees: controls, rules, panels, saves |
| [docs/ECONOMY.md](docs/ECONOMY.md) | Changing prices, wages, recipes or demand |
| [data/game.json](data/game.json) | Tuning items, recipes, building costs and pacing |
| [assets/README.md](assets/README.md) | Changing models, icons, sounds, music or their sources |
| [docs/RELEASE.md](docs/RELEASE.md) | Branching, checking and handing off a change |

## Source map

- `src/World.cs`: authoritative simulation, independent of menus and rendering.
- `src/WorldView.cs`: reads world state and draws it.
- `src/Game.cs`: UI, input and saves.
- `src/GameData.cs`: loads and validates `data/game.json`.
- `src/Inventory.cs`: inventory rules, used by every worker job.
- `src/Net.cs`: multiplayer host and guest.
- `tests/`: simulation checks; `src/GameChecks.cs` and `src/NetChecks.cs`: UI and network checks.
- `build.py` and `tools/`: launcher, checks and asset tools.

[docs/DESIGN.md](docs/DESIGN.md#source-files) maps every source file.

## Working rules

- Plan ahead and split work into well-defined tasks.
- Hand lower-complexity tasks to a cheaper, faster model or subagent; inspect and verify its result.
- Continue until the requested goal is achieved. Ask about material design ambiguities rather than quietly changing the game.
- Several agent sessions share the main checkout: do branch work in a worktree ([docs/RELEASE.md](docs/RELEASE.md)).
- Keep tool downloads and build output ignored, and never depend on another local checkout.
- Always run Godot through `./build.py`. The binary in `.tools/` picks up the system .NET and crashes when called directly; add a `build.py` action or pass flags after `./build.py play --` instead.
- Docs describe the game as it is now. Update them in the same change as the code; git keeps the history.
- For a visual change, look at a rendered screenshot (`./build.py play -- --showcase --screenshot /absolute/path.png`); headless checks cannot verify presentation.
- To play the real window with keyboard and mouse, use the `playtest` skill (`.claude/skills/playtest`).
- Report limitations candidly; do not claim a full playthrough from a fixture that injects finished goods.

## Checks

`./build.py check` runs every offline check: C# compile with warnings as errors, C# format, Python `mypy --strict` and `ruff`, then the simulation, UI and network tests.
CI runs the same command.
Treat Godot error output as a failure even when the process exits zero; `build.py` does.

## Admin access

None: the game has no servers, accounts or deployments.
