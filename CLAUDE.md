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
- `./build.py play -- --showcase --screenshot /absolute/path.png`: a separate visual-review fixture, never the normal opening state.

Run checks appropriate to the change. For a visual change, launch a rendered instance and inspect the actual image; headless tests cannot verify presentation. Treat Godot error output as a failure even when the process exits zero. Report limitations candidly; do not claim a full playthrough from a fixture that injects finished goods.

## Architecture

Keep authoritative simulation in `World.cs`, independent of menus or rendering. `WorldView.cs` reads state and draws; `Game.cs` handles UI/input/save integration. `GameData.cs` loads and validates editable data. Inventory rules belong in `Inventory.cs`, not in individual worker jobs.

Automated transfers must respect input/output allowlists. Empty means unrestricted. Prefer explicitly matching input-filter storage over general storage. Manual transfers can bypass automation filters. Never delete carried items when a worker is reassigned, fired, blocked or rerouted.

Preserve the progression: loose iron provides a tool-free bootstrap; the player starts with an iron axe; mining coal and iron deposits requires a carried pickaxe or steel tools. Workers receive tools after the first pickaxe is forged. Steel requires iron, mined coal, a reinforced furnace and active bellows heat. Two assigned workers divide bellows and hauling duties.

Use representative end-to-end checks plus focused edge cases. Input tests go through `Input.ParseInputEvent`, not direct calls to input handlers. Simulation setup may construct fixtures, but tests claiming resource-to-steel progression must gather raw materials and use the actual recipes and trade delays. Do not weaken assertions to make broken behavior pass.
