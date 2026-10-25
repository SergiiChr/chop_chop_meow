# Assets

The world is rendered with code-built low-poly 3D models; there are no world or character sprite sheets. Models are assembled from Godot primitive meshes in `src/WorldModels.cs` (world objects, wolf and trade cart) and `src/ActorRig.cs` (player, worker and courier). `src/ModelKit.cs` holds the shared primitive, material and fade helpers. To change a model, edit its builder: sizes are in world units, where one tile is one unit and an adult is about 1.65 units tall.

The art direction is natural and modern rather than fantasy. Buildings are DIY: a cinder-block furnace with a steel stovepipe, a propane-tank forge on an angle-iron stand fed by a plywood lever bellows with a heat gauge, an OSB cabin on block piers with a corrugated shed roof and solar panel, a plywood storage box, an orange recovery tote, an anvil on a welded stand, a crafting bench with its slotted chisel and hammer, slatted wood crates, a market post with a mailbox, and a steel wheel-rim fire pit. Trees come in three variants chosen by object id: broadleaf oak, spruce and silver birch.

The 2D images that remain are interface icons and are unchanged:

- `item-atlas.png` is a 4×3 sheet of hotbar inventory icons (axe, pickaxe, an unused steel tools cell, iron, steel, wood, stone, iron ore, coal, berries, water, empty).
- `world-atlas.png` is a 4×3 sheet (362 × 362 px cells) whose building cells supply the building-plan and hotbar icons.
- `ui/` holds hand-written SVG toolbar icons: hammer (build), backpack (inventory), hard hat (workers) and open book (guide). Items added after the atlas also have hand-written SVG icons named by item id, which the hotbar prefers over the atlas: planks, wood and metal parts, stone and steel chisels, mallet, iron and steel hammers, steel axe and pickaxe, rake, mortar and pestle, crate, torch, the crafting bench, tin ore, bars and cans, leaves, grass, fertilizer, wheat seeds, wheat, flour, dough, and the foods: raw meat, burnt steak, bread, crushed berries, berry jam, meat stew and the canned goods.

Both PNG atlases were generated with the built-in image generation tool for this project; no external copyrighted game art or third-party packs are used. Their prompts are preserved in [PROMPTS.md](PROMPTS.md).

## Sounds

`music/` holds the ambient music as 192 kbps MP3: `path_to_lake_land.mp3` for the day and `forest.mp3` for the night. Every ambient track is normalised to -18 LUFS integrated loudness with true peaks below -1 dBTP, so tracks switch without a jump in volume. Credits: Alexandr Zhelanov ([soundcloud.com/alexandr-zhelanov](https://soundcloud.com/alexandr-zhelanov)) and Augmentality (Brandon Morris).

`sounds/` holds the sound effects as 320 kbps MP3, converted from the supplied files and peak-normalised, with variants of one sound matched in loudness. A numbered suffix marks variants of one sound. Credits: Kenney ([www.kenney.nl](https://www.kenney.nl)) and Little Robot Sound Factory ([www.littlerobotsoundfactory.com](https://www.littlerobotsoundfactory.com)).
