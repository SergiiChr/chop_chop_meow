# Request summary and confirmed decisions

This document records the requested playable proof of concept and the clarifications that shaped its scope. It separates user-confirmed requirements from implementation choices and current status so future changes can be checked against the original goal.

## Original request

Create an executable videogame proof of concept that can be launched with one command, using Godot .NET/C# and Python in the same general spirit as `SergiiChr/this_scalpel_is_mine` (a separate game, used as a reference for its launch/setup approach). The game should begin with manual gathering and building, then progress toward automation and technology. Its look should be smooth, isometric pseudo-3D, illustrated, natural, and lighthearted, with some of Terraria's approachable feel rather than Factorio's industrial visual style.

The initial settlement should use primitive huts, stone, wood, and furnaces. Food, water and day/night should give the production loop a survival context: wild animals roam after dark and are afraid of light. At a signpost, a virtual village market should offer basic-resource purchases and sell player goods, including food and drink. Prices should respond to supply: repeatedly selling an item lowers its price, which recovers over time; advanced goods should be worth more. Market orders should show a cart traveling for 30 seconds, carrying purchases back or visibly returning with cash after a sale.

Workers should be individually hired, paid daily, assigned, reassigned, and dismissed. Building assignment selects specific named workers; worker Work orders target a clicked resource and repeat. They should carry furnace inputs and outputs, prioritizing matching filtered storage before nearby general storage. The environment should become more threatening over time. The sketch extended toward steam engines, oil, optics/chips, computers and later automation, with pollution reducing sunlight and allowing vampires out during the day. Pollution, positive/negative terraforming and those later threats/technologies were deferred beyond the steel POC.

## Clarifications confirmed by the user

- **Platform and launch:** Linux first. The target is a one-command launch from a fresh checkout, with setup handled by the Python launcher.
- **Pacing:** 10–15 minutes is a first-goal pacing target, not a measured human playthrough guarantee.
- **Steel progression:** Steel requires iron and genuinely mined coal. Charcoal cannot substitute.
- **Survival loop:** Include food, water, day/night, wolves, light from huts/campfires, sleeping in a hut, wages, save/load, and recovery after death. Death drops the full carried inventory in a box and respawns the player at the initial spawn.
- **Tool progression:** Do not start with wooden axes or picks. The player starts with a basic iron axe. Loose iron can be picked up by hand near the mine to provide a realistic bootstrap and avoid a tool-gated dead end; mining iron and coal deposits requires a pickaxe or steel tools.
- **Furnace work:** Basic furnaces can burn wood or coal. Reinforced furnaces use coal and require bellows to make steel. With two workers assigned, one pumps the bellows and the other hauls materials.
- **Storage filters:** Input and output filters are separate allowlists. An empty filter means all items are allowed. Automated transfers respect the relevant filter; manual transfers can bypass filters.
- **Art direction:** The user reviewed drawn/illustrated player and furnace samples and selected the illustrated option (option 2). Later guidance asked for a cohesive, to-scale environment and visually distinct workers in overalls; the current intended worker appearance is blue overalls over a cream shirt with a distinct sprite.
- **Animation:** Add more animation frames and a dedicated animation for every available tool. The implementation now has thirteen four-direction, eight-frame atlases covering player/worker walking, iron and steel axe/pick use, player and worker hammer/bellows work, and worker crate carrying. Gameplay state selects the relevant tool animation; worker animation follows movement and job status, pauses with the game, and attacks/manual pumping trigger their own cycles.
- **Interface:** Show stats in the inventory only, not as a persistent HUD. Use Factorio-style hotbar slots 1–9 for item use. The implementation includes saved item/building-plan assignment and keyboard/click activation.
- **Delivery, updated:** Rebase the animation changes on `main` and push them on branch `codex/improved_animations`; `origin/main` already contains the initial POC. Provide a PR description focused on the final animation changes. This supersedes the earlier `codex/initial_poc` delivery target.

## Implementation choices recorded in the repository

The project uses Godot 4.7.2 .NET, C#, .NET 10, and a Python standard-library launcher. Data-driven recipes and costs live in `data/game.json`. The selected visual direction is smooth illustrated sprites with natural colors and readable silhouettes.

The market is accessed from a village signpost, uses supply-and-demand pricing with recovery over time, and settles orders after a visible 30-second cart trip. Workers are individual entities with assignments, wages, cargo, and work status; current automation covers gathering and building/furnace tasks. Storage has separate input and output allowlists. Automated deposits prefer matching input-filter chests before general storage, and withdrawals respect output filters. Workers reroute cargo when filters change; manual inventory transfers ignore automation filters.

The POC stops at steel. Pollution, vampires, multiplayer, charcoal production, and later technology are excluded. The world is a compact fixed map, worker navigation uses simple grid detours, and the worker count is capped at eight, as documented in the README and design notes.

## Current state and evidence limits

Animation asset/state checks, real Godot input/UI checks, lint, Linux export and exported smoke test pass. Rendered export review captures cover all four directions. The 10–15 minute target is not a measured human playthrough; production-chain simulation time excludes travel and UI decision time.
