# Tree asset research

Research checked 2026-10-27. The game currently builds its three seeded tree variants (oak, spruce and silver birch) as shared, vertex-coloured low-poly meshes in `src/NatureShape.cs`. This keeps the woodland consistent with the existing illustrated, code-built world and adds no external modeling tool, asset pack or runtime dependency. The current tree style is documented in [asset provenance](../../assets/README.md).

The project now includes an opt-in **Tree3D 1.1.0** trial. Run `./build.py play -- --showcase --trees --tree3d` for the three-species comparison, or `./build.py play -- --tree3d` to explore a fresh game with the generated forest. The normal launch retains the code-built models while the new look is evaluated. The same `--tree3d` option also enables three low, branching berry-bush variants; inspect those with `./build.py play -- --showcase --bushes --tree3d`.

`./tools/tree3d.py` downloads the pinned official release, verifies its SHA-256, and loads the Linux x86_64 GDExtension only inside an ignored authoring project under `.tools/tree3d`. `tools/bake_trees.gd` generates three fixed seeds per tree species and berry shrub, normalizes their bounds, applies an authored taper to the conifer preset and saves ordinary ArrayMeshes to `assets/models/tree3d`. Nine standing trees share eighteen meshes and six materials; each instance uses two visible mesh instances plus two shadow-only instances sharing the same meshes and original materials. Procedural SVG broadleaf/needle cutouts and bark textures are original project assets. Tree3D's MIT and underlying Proctree BSD notices are kept beside the baked assets.

Compatibility tested on Godot 4.7.2 .NET: asset-only import and runtime generation pass. An initial `--headless --editor --import` run completed its import progress but crashed with exit -11 while loading the editor layout; the baker uses `--headless --import` instead. Interactive plugin editing is therefore unverified. No native Tree3D binary is shipped or loaded by the game. The trial uses StandardMaterial3D foliage cutouts and the existing cached material fade, shake, fall and stump paths. Tree shadow-only instances retain the original opaque/cutout material while the visible canopy fades; they inherit shaking, falling and final visibility from the same parent. `--showcase --trees --tree3d --tree-shadow` reviews a faded foreground canopy with its shadow. Tree3D's wind weights are removed from the baked meshes; animated shader wind and automatic LOD are outside this trial. Triangle counts per seed are 4,212 for oak, 9,844 for spruce and 6,324 for birch (including foliage), with the exact counts and dimensions recorded in `assets/models/tree3d/meshes.json`.

The berry bushes fit the existing 0.65-unit picking radius and stay below one unit tall. Each uses two shared baked meshes (2,804 triangles total) plus a cached fruit mesh. Berry clusters attach to the central twig of selected foliage cards, avoiding transparent card corners. The existing independent fruit pivot hides after harvesting and reappears when the bush replenishes; foliage and woody stems remain. Small decorative scenery shrubs retain their existing inexpensive MultiMesh geometry.

Upstream sources: [Tree3D release 1.1.0](https://github.com/JekSun97/gdTree3D/releases/tag/v1.1.0) and [pinned release source](https://github.com/JekSun97/gdTree3D/tree/v1.1.0). The asset library listing still describes 1.0 and advertises Godot 4.5+ support.

If the art direction moves to authored external trees, the practical starting workflow is **MTree in Blender → apply/bake the generated geometry → export GLB → import in Godot**. MTree offers presets and procedural controls for branching and leaves, making it useful for building a small, controlled set of tree silhouettes. Godot 4.7 recommends glTF 2.0 for 3D scenes, so GLB is the straightforward handoff format. Blender and Godot can remain authoring/import tools; the game need not gain a runtime dependency.

SpeedTree can also be used by exporting supported mesh formats such as FBX or OBJ and importing the result through Godot's asset pipeline. Its modeler provides game-oriented LOD and wind features, but the available sources do not establish a native Godot SpeedTree runtime path or direct compatibility for its wind setup. Plan to treat the exported tree as mesh and material data, then implement and tune any wind motion in the game's own rendering path.

Other options include Blender's Sapling Tree Gen for parametric trees and Tree It for a standalone generator with FBX/OBJ export and adjustable LOD. These are useful for quick experiments or a small offline-authored set. As with any external tree workflow, inspect the generated silhouette, leaf shading, scale, triangle count and distant presentation in the actual isometric camera before replacing the current style.

## Primary references

- [Tree3D in the Godot Asset Library](https://godotengine.org/asset-library/asset/3141) — version, MIT license, platform and wind support; accessed 2026-10-27.
- [gdTree3D source repository](https://github.com/JekSun97/gdTree3D) — native generator implementation; accessed 2026-10-27.

- [Godot 4.7 supported 3D import formats](https://docs.godotengine.org/en/4.7/about/list_of_features.html) — accessed 2026-10-27.
- [MTree on Blender Extensions](https://extensions.blender.org/add-ons/modular-tree/) — features, requirements and extension license metadata; accessed 2026-10-27.
- [MTree version history](https://extensions.blender.org/add-ons/modular-tree/versions/) — current Blender compatibility and license metadata; accessed 2026-10-27.
- [Sapling Tree Gen on Blender Extensions](https://extensions.blender.org/add-ons/sapling-tree-gen/) — parametric tree generator; accessed 2026-10-27.
- [Sapling Tree Gen in the Blender Manual](https://docs.blender.org/manual/sr/3.6/addons/add_curve/sapling.html) — presets and generator overview; accessed 2026-10-27.
- [Tree It official page](https://www.evolved-software.com/treeit/treeit) — generator features and exports; accessed 2026-10-27.
- [SpeedTree product and FAQ](https://unity.com/products/speedtree) — modeler features, export formats and workflow information; accessed 2026-10-27.
- [SpeedTree documentation](https://docs.speedtree.com/doku.php) — product documentation; accessed 2026-10-27.
