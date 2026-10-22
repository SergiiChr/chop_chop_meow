# Chop Chop Meow: playable woodland production POC

## Summary

This change turns an empty project into a Linux-first, one-command Godot .NET game prototype. A fresh-start player can gather and build by hand, survive day and night, produce and sell steel goods, then hire workers to automate gathering and furnace work.

The game uses smooth illustrated isometric woodland art, with consistent human-scale props, a natural pond and trail, less crowded resource groups, and a distinct overall-clad worker skin. Its early progression connects loose-iron bootstrap and basic tools to mining, reinforced-furnace steel production, village trade, and worker automation. Food, water, wolves, light, hut sleep, wages, save/load, and death recovery support that progression.

The signpost market adjusts prices with supply and demand and displays a 30-second cart trip before a sale or purchase settles. Workers are individually assigned and paid; two workers on a reinforced furnace divide bellows pumping and hauling. Chests use separate input and output allowlists, with empty lists unrestricted. Automation follows those filters and prefers matching input storage, while manual inventory transfers remain available.

Launch with `./build.py play`. The Python launcher provisions local Godot/.NET tools, runs checks, and exports a portable Linux bundle. Hotbar slots 1–9 reference carried items or building plans and persist across saves; health, hunger, thirst, coins and carrying stats appear only in inventory.

The proof of concept ends at steel. It uses a fixed compact map, at most eight workers, and a limited directional animation set. Charcoal production, pollution/terraforming, vampires, multiplayer, and later technology remain outside scope. The 10–15 minute first-goal duration remains a pacing target rather than a measured human playthrough.

## Validation

- Simulation checks exercise raw-resource gathering through steel-tool sale, hiring and actual worker delivery, plus survival, furnace heat/fuel, storage filters, rerouting, wages and death recovery.
- Godot input/UI checks exercise movement, gathering, placement, inventory-only stats, hotbar use/rebinding, and disk save/load with saved slot references.
- Format/compiler checks, Linux export and executable smoke tests pass. Fresh-start, daylight and night screens are reviewed from the exported game.

The original pitch and confirmed clarifications are recorded in [REQUEST_SUMMARY.md](REQUEST_SUMMARY.md).
