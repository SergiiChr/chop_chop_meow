# Chop Chop Meow — playable proof of concept

This records the original scope and the design decisions that still hold. For how the game works today, see [README.md](../README.md) and [DESIGN.md](DESIGN.md).

## Goal

A single-player isometric woodland survival and production game. The player gathers materials, builds a settlement, makes and sells steel tools, then hires villagers to automate gathering and furnace work. Up to four players can share a world.

## Scope

- Linux-first Godot 4.7.2 .NET / C# project with a project-local Python launcher and .NET 10 setup.
- Simulation of resources, inventories, construction, recipes, tool progression, furnaces and bellows, storage filters, market pricing and delayed cart settlement, worker assignments and wages, food and water, day and night, wolves and campfire protection, death recovery, mines, farming and JSON save/load.
- Isometric movement, gathering, building placement, selection, inventory and station panels, worker management and guided goals.
- Item, building and recipe values in `data/game.json`.
- A 3D world of code-built low-poly models: modern DIY buildings, three tree variants and a jointed actor rig with keyframed actions. Item and building icons are rendered from the same models. The earlier 2D art samples remain in `docs/art/`.
- A fixed 216×216-tile valley with a lake, open to the sea and closed in by mountain ranges.
- Health, hunger and thirst on the HUD, and a saved nine-slot hotbar for item use, building plans and tools.
- Deterministic simulation checks and real Godot input/UI checks (see [DESIGN.md](DESIGN.md#development-and-verification)).

## Confirmed design decisions

- The stone furnace burns wood or coal. The reinforced furnace burns only mined coal and needs bellows heat to make steel.
- Steel needs iron and mined coal. Charcoal production is not implemented.
- The player starts with a steel axe and no pickaxe. There is no loose iron and there are no iron tools. The first pickaxe is bought at a trading post with crates of berries.
- The market is reached at a trading post. Prices respond to supply and demand, and orders settle after a visible 30-second cart trip.
- Workers are individually hired, assigned, reassigned and dismissed. They gather resources or work buildings and furnaces. Two workers on a reinforced furnace split pumping and hauling.
- Storage has separate input and output allowlists. An empty list means unrestricted. Automated deposits prefer matching input-filter storage, then general storage; automated withdrawals respect output filters.
- Death drops carried items into a recovery box. The player respawns in the nearest cabin, or in the clearing without one.
- Pollution, vampire threats and technology beyond steel are left out of this POC.

## Limits

The 10–15 minute fresh-start progression is a pacing target, not a measured human playthrough. Tune `data/game.json` after hands-on play. Workers use simple grid detours rather than full navigation, and a settlement has at most eight workers.
