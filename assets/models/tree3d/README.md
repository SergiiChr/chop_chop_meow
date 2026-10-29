# Tree3D meshes

Baked oak, spruce, silver birch and berry-bush meshes made with [Tree3D 1.1.0](https://github.com/JekSun97/gdTree3D/tree/v1.1.0) (gdTree3D, MIT).
Each species has three seeded variants.
The notices are in `TREE3D-LICENSE.md` and `PROCTREE-LICENSE.txt`.
`src/Tree3DShape.cs` loads the meshes.
The SVG leaf and bark textures are original project assets.

Regenerate with `./tools/tree3d.py`.
It downloads the pinned release, verifies its SHA-256 and loads the Linux x86_64 GDExtension only inside an ignored authoring project in `.tools/tree3d`.
`tools/bake_trees.gd` does the baking.
It runs Godot with `--headless --import`.
`--headless --editor --import` crashes with signal 11.

No native Tree3D binary ships with the game or loads at runtime.
Tree3D's wind weights are removed from the baked meshes.

`meshes.json` holds triangle counts and dimensions per mesh.

Preview with:

- `./build.py play -- --showcase --trees`
- `./build.py play -- --showcase --bushes`
- `./build.py play -- --showcase --trees --tree-shadow` (faded foreground canopy with its shadow)
