# Chop Chop Meow — playable proof of concept

## Goal

A single-player isometric woodland survival and production game. The player gathers materials, builds a settlement, makes and sells steel tools, then hires villagers to automate gathering and furnace work.

## Implemented scope

- Linux-first Godot 4.7.2 .NET / C# project with a project-local Python launcher and .NET 10 setup.
- Simulation of resources, inventories, construction, recipes, tool progression, furnaces and bellows, storage filters, market pricing and delayed cart settlement, worker assignments and wages, food/water, day/night, wolves and campfire protection, death recovery, and JSON save/load.
- Isometric movement, gathering, building placement, selection, inventory and station interfaces, worker management, and guided objectives.
- Data-driven item/building/recipe values in `data/game.json`.
- Selected smooth illustrated sprite direction. The two art samples are in `docs/art/`; current rendered atlas assets are in `assets/`.
- Refined shared-scale environment, quieter grass/trail, natural pond, sparse resource groups, human-sized hut doorway and separate blue-overall worker skin.
- Player stats live in inventory; a saved nine-slot Factorio-style hotbar supports item use, building plans and reassignment.
- Deterministic headless simulation checks covering filters, worker gathering and rerouting, furnace hauling and bellows, market settlement, wages, death recovery, save state, and a resource-driven progression through hiring and worker delivery.

## Confirmed design decisions

- Basic furnace accepts wood or coal. Reinforced furnace uses only coal and requires bellows heat for steel production.
- Steel uses iron and mined coal. Charcoal production is not implemented.
- Market is accessed at a village signpost, responds to supply and demand, and settles orders after a visible 30-second cart trip.
- Workers are individually hired, assigned, reassigned, and dismissed. They gather resources or perform simple building/furnace work; two workers on a reinforced furnace split pumping and hauling.
- Storage has separate input and output allowlists. Empty lists mean unrestricted. Automated deposits prefer matching input-filter storage, then general storage; automated withdrawals respect output filters.
- Death drops carried items into a recoverable box and respawns the player.
- Pollution, vampire threats, multiplayer, and technology beyond steel are excluded from this POC.

## Verification status

- Headless simulation test: **passed** with the project-local reference Godot 4.7.2 .NET and .NET 10 environment. The test reports `SELF-TEST PASS` after its full progression simulation.
- Input/UI checks: **passed** through Godot input events for movement, gathering, construction, chest actions, inventory-only stats, 1–9 hotbar use, rebinding, and disk save/load with slot references.
- Linux export and smoke test: **passed**. The extracted portable bundle also starts with the development SDK removed from its environment.
- C# format/compiler checks: **passed**, with no compiler warnings or errors in the final build.
- Graphics review: **passed** for the actual exported executable's [fresh start](screenshots/fresh-start.png), [daylight settlement](screenshots/daylight.png), and [night settlement](screenshots/night.png). Settlement captures use the explicit showcase fixture; the normal game starts empty with an axe, berries, and six wood.

The 10–15 minute fresh-start progression remains a pacing target, not a measured human playthrough. The production-chain fixture takes 324 simulated seconds, excluding travel and UI decision time, and builds a campfire from gathered materials to survive the night while trading. Tune `data/game.json` after hands-on play. Prototype limits include a fixed compact map, simple movement with grid detours for workers rather than a full navigation system, and a maximum of eight workers. Animation asset/state checks, real input/UI checks, lint, Linux export and exported smoke test pass; the exported animation fixture was captured for visual review.
