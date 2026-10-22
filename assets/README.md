# Assets

The world is rendered with code-built low-poly 3D models; there are no world or character sprite sheets. Models are assembled from Godot primitive meshes in `src/WorldModels.cs` (world objects, wolf and trade cart) and `src/ActorRig.cs` (player, worker and courier). `src/ModelKit.cs` holds the shared primitive, material and fade helpers. To change a model, edit its builder: sizes are in world units, where one tile is one unit and an adult is about 1.65 units tall.

The art direction is natural and modern rather than fantasy. Buildings are DIY: a cinder-block furnace with a steel stovepipe, a propane-tank forge on an angle-iron stand fed by a plywood lever bellows with a heat gauge, an OSB cabin on block piers with a corrugated shed roof and solar panel, a plywood storage box, an orange recovery tote, an anvil on a welded stand, a market post with a mailbox, and a steel wheel-rim fire pit. Trees come in three variants chosen by object id: broadleaf oak, spruce and silver birch.

The 2D images that remain are interface icons and are unchanged:

- `item-atlas.png` is a 4×3 sheet of hotbar inventory icons (axe, pickaxe, steel tools, iron, steel, wood, stone, iron ore, coal, berries, water, empty).
- `world-atlas.png` is a 4×3 sheet (362 × 362 px cells) whose building cells supply the building-plan and hotbar icons.
- `ui/` holds hand-written SVG toolbar icons: hammer (build), backpack (inventory), hard hat (workers) and open book (guide).

Both PNG atlases were generated with the built-in image generation tool for this project; no external copyrighted game art or third-party packs are used. Their prompts are preserved in [PROMPTS.md](PROMPTS.md).
