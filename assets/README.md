# Prototype sprite sheets

Art direction option 2, smooth illustrated sprites, is selected for the playable prototype. The current sprite sheets were generated with the built-in image generation tool for this project; no external copyrighted game art or third-party sprite packs are used. They are prototype assets and may need cleanup or replacement before release.

The complete generation prompts are preserved in [PROMPTS.md](PROMPTS.md).

`world-atlas.png` is 1448 × 1086 px and is consumed as a 4-column × 3-row sheet (362 × 362 px cells). Its cells cover the world objects used by `WorldView`: tree, stone, iron/loose iron, coal, berries, basic furnace, reinforced furnace, hut, chest/recovery box, anvil, signpost, and campfire. `player-atlas.png` is 1536 × 1024 px and is consumed as 4 columns × 2 rows (384 × 512 px cells): four directions on the first row and corresponding walk frames on the second. `travel-atlas.png` is 2172 × 724 px and is consumed as three 724 × 724 px cells for wolves, trade carts carrying goods, and returning payment.

These source images are cropped into atlas cells by `src/WorldView.cs`; displayed sizes are scaled for the game's isometric map. The renderer currently alternates a small number of walk frames and does not provide a full animation set. The image layout can be viewed alongside the earlier code-drawn comparison in [docs/art](../docs/art/README.md); the full prompt summary for the illustrated direction board is there as well.

The refinement adds `worker-atlas.png` (4×2 directions/walk poses), a separate skin with blue bib overalls, cream work shirt and empty hands. `environment-details.png` has two equal cells: a natural pond and a hut with an adult-sized doorway. `item-atlas.png` is a 4×3 sheet for hotbar inventory icons (axe, pickaxe, steel tools, iron, steel, wood, stone, iron ore, coal, berries, water, empty). All were generated with the built-in image tool using the existing artwork as reference.

Scale is anchored to an 85 px adult actor. The renderer uses smaller waist-height chests/anvils and knee-height resource piles, a doorway sized for the actor, and taller trees. Sprite hit regions use the same dimensions as drawing. Hut placement and collision use a larger footprint. Grass is rendered in broad subtle patches with a narrow worn trail instead of a high-contrast tile pattern. These refinements change fresh-map placement while preserving existing saves.
