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
| [src/GameTables.cs](src/GameTables.cs) and [src/GameData.cs](src/GameData.cs) | Tuning items, recipes, building costs and pacing |
| [assets/README.md](assets/README.md) | Changing models, icons, sounds, music or their sources |
| [docs/RELEASE.md](docs/RELEASE.md) | Branching, checking and handing off a change |

## Source map

- `src/World.cs`: authoritative simulation, independent of menus and rendering.
- `src/WorldView.cs`: reads world state and draws it.
- `src/Game.cs`: creates and holds the interface's parts, routes input.
- `src/SidePanel.cs` and `src/*Panel.cs`: the side panel and its pages, one node per feature.
- `src/GameData.cs` and `src/GameTables.cs`: items, buildings, recipes, trade and time values, checked to fit together.
- `src/Inventory.cs`: inventory rules, used by every worker job.
- `src/intro/`: the intro film. `IntroMovie.cs` plays it when the game opens; `WorldIntro.cs`, `IntroSets.cs` and
  `IntroPoses.cs` draw its frames (`intro-film` skill).
- `src/Server.cs`: the headless server every game runs on.
- `src/Net.cs`: the connection between a server and its players.
- `tests/`: simulation checks; `src/GameChecks.cs`, `src/NetChecks.cs` and `src/ServerChecks.cs`: UI, network and real server checks.
- `build.py` and `tools/`: launcher, checks and asset tools.

[docs/DESIGN.md](docs/DESIGN.md#source-files) maps every source file.

## Working rules

- Plan ahead and split work into well-defined tasks.
- Continue until the requested goal is achieved. Ask about material design ambiguities rather than quietly changing the game.
- Several agent sessions share the main checkout: do branch work in a worktree ([docs/RELEASE.md](docs/RELEASE.md)).
- Keep tool downloads and build output ignored, and never depend on another local checkout.
- Always run Godot through `./build.py`. The binary in `.tools/` picks up the system .NET and crashes when called directly; add a `build.py` action or pass flags after `./build.py play --` instead.
- Docs describe the game as it is now. Update them in the same change as the code; git keeps the history.
- `./build.py play` runs the optimized release export, which leaves out Godot's debug-only warnings.
  A clean `play` log does not replace the checks, which run the debug build.
  Until Godot 4.8, its log also shows `Attempt to disconnect a nonexistent connection` for `tree_exited` or
  `focus_entered` when a game ends or a dropdown closes.
  This is a harmless engine bug in release builds ([godotengine/godot#87626](https://github.com/godotengine/godot/issues/87626)),
  with a fix pending in [godotengine/godot#123998](https://github.com/godotengine/godot/pull/123998).
  Remove this note after upgrading.
- For a visual change, look at a rendered screenshot (`./build.py play -- --showcase --screenshot /absolute/path.png`); headless checks cannot verify presentation.
- To play the real window with keyboard and mouse, use the `playtest` skill (`.claude/skills/playtest`).
- After a scripted rename, run `python3 tools/compare_strings.py <base>`: checks do not read most text, and a rename can reach into it.
- Report limitations candidly; do not claim a full playthrough from a fixture that injects finished goods.

## Checks

`./build.py check` runs every offline check: C# compile with warnings as errors, C# format, Python `mypy --strict` and `ruff`, then the simulation, UI, network and server tests, printing each stage's time and the total against its limit.
`./build.py test sim`, `ui`, `net` or `server` runs one stage.
CI runs the same command.
Treat Godot error output as a failure even when the process exits zero; `build.py` does.

To wait for a `./build.py` run, run it in the background and wait for it to exit.
Never find or stop its processes with `pgrep -f` or `pkill -f`: both match the calling shell's own command line, so a
wait loop on `pgrep -f` never ends and `pkill -f` kills that shell.
Find them by name instead: `pgrep -a Godot` for a check run, `pgrep -a ChopChopMeow` for the exported game.

## Admin access

None: the game has no servers, accounts or deployments.
