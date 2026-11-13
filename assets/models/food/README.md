# Food meshes

Four models of [Kenney's Food Kit](https://kenney.nl/assets/food-kit) (CC0 1.0), baked into meshes that carry their
colours on their corners, so the game loads no texture for them and dresses them with its own materials.
`src/ItemShape.cs` loads them, recolours them to the game's palette and shows them as item icons and pile stock.

| Mesh | Kenney's model | Shown as |
| --- | --- | --- |
| `loaf_round.res` | Loaf round | Bread |
| `wholer_ham.res` | Wholer ham | Raw meat |
| `meat_cooked.res` | Meat cooked | Burnt steak |
| `bowl_soup.res` | Bowl soup | Meat stew and crushed berries |

Regenerate with `./tools/food_models.py`.
It downloads the pinned files from [Patch's asset library](https://patch.gg), which serves Kenney's models re-packed,
with the palette texture inside the file and scaled to real size, and checks their SHA-256.
`tools/bake_models.gd` does the baking in a throwaway project: it reads each triangle's colour from the palette at the
middle of the triangle, and keeps each triangle's own three corners, so the faces stay flat like the game's code-built
models.
It prints each mesh's triangle count and colours, which the recolouring in `ItemShape` goes by.
