# Terrain3D landscape

The landscape uses Terrain3D's native clipmap renderer.
Launch normally:

```sh
./build.py play
```

`build.py setup` installs the pinned Linux x86_64 runtime into the ignored `addons/terrain_3d/` folder.
The game therefore needs Linux x86_64.
The install uses the official Terrain3D `v1.1.0-rc1` release archive (a release candidate) and verifies its SHA-256:

`549d0c1d45982ae2e30d90275f754a9c7174b8f57eb600bc93a8108b98d44636`

The archive stays in `.tools/terrain3d`.
The source is the [TokisanGames/Terrain3D release](https://github.com/TokisanGames/Terrain3D/releases/tag/v1.1.0-rc1), under the MIT license.
The full license from the archive is in [LICENSE.txt](LICENSE.txt).
The installer extracts only the Linux extension, its manifest, icon and license.
No separate repository checkout is needed.

`Terrain3DView` builds nine heightmap regions in a 3×3 grid around the map, each with 256×256 `RF` samples.
Samples come from `MountainShape.Height`, clamped to zero and lowered by 0.03 to sit below the separately rendered mine shells.
The playable valley floor stays level at height zero.
Terrain3D supplies the clipmaps, height interpolation, generated normals and terrain UVs.
Terrain detail follows the viewed ground rather than the distant orthographic camera.

Collision mode zero is enabled.
Terrain collision does not drive walking, building placement or other simulation rules.
The simulation owns the level floor and mountain checks.
There are no walkable hills, sculpting UI or digging.

The ground keeps the project's own meadow and stone colours and takes grain and bump from two photoscan sets in
`textures/`, each an albedo PNG with the height in its alpha and an OpenGL normal PNG with the roughness in its alpha,
the way Terrain3D reads them: grass (id 0) on flat ground and rock (id 1) on slopes through Terrain3D's auto shader.
Each photoscan's brightness around its mean modulates the palette at partial strength, so the colours stay and the
detail shows; `tools/pack_textures.gd` scales every set to one mean linear brightness, which the shader divides the
grain by.
A grass tile spans 4 world units and a rock tile 6, each with a little detiling rotation and shift from Terrain3D.
The ground is matte, with the bump at half depth: at a low sun the photoscans' roughness turned into a sheen that
washed out the meadow. The height-based ambient occlusion comes from Terrain3D's material defaults.
`tools/ground_textures.py` packs the sets from the pinned ambientCG archives; the `.import` files keep the alpha
channels (VRAM compressed, high quality, no normal-map compression, no alpha border fix).

The shader insertion in `Terrain3DView` keeps the pinned generated Terrain3D shader core.
It adds the meadow and stone palette with lusher and drier patches, the photoscan grain over it, the moss patches, the
woods' floor, the footpath photoscan blended into worn ground and footpaths by its height, baked shade, and fragment
clipping for the five mine openings so their separately modeled rock shells stay open.

Three Poly Haven photoscans are drawn in their own colours, each at about real size and pinned by
`tools/polyhaven_textures.py`:
- Sparse broad patches of the meadow take `aerial_grass_rock` (`moss_*.jpg`, 4K, one tile 15 world units), moss over
  gravelly earth, where the patch noise read at a larger scale and an offset runs high; their edges reach further out
  at the photoscan's moss.
- The woods take `forrest_ground_01` (`forest_*.jpg`, 2K, one tile 2 world units) out to 10 units from the foot of
  the ranges, where trees grow at better than even odds (`World` scatters them by `Terrain.FromMountains`).
  `Terrain3DView` bakes that distance into a map of one texel a world unit; the band's edge wanders in broad patches
  and reaches further out at the photoscan's litter, and it stops where the slopes turn to stone.
- Worn ground takes `brown_mud_leaves_01` (`path_*.jpg`, 2K, one tile 2 world units), over all of these.

One read of each photoscan's height map shifts it along the view, 0.1 units at most.
Stepping down to the height map instead cost 1.1 to 1.4 ms a frame on an RTX 4080 even with no worn ground in view,
since the larger shader slowed all the ground; the single read costs nothing measurable.

The first Godot 4.7.2 editor import after installing the runtime can exit with signal 11 during editor shutdown.
Setup keeps that log and retries once, only when editor initialization completed and Godot reported no engine errors.
A repeated crash or any engine error fails setup.

The extension is used through `ClassDB.Instantiate` and dynamic `Set`/`Call` methods.
Terrain3D's generated C# wrappers are incomplete for this use and are excluded from the isolated probe project.
`./tools/terrain3d.py --probe` extracts the addon under ignored `.tools/` and checks native loading, region heightmap access and the collision API in that separate project.

Capture the terrain view with:

```sh
./build.py play -- --showcase --environment-review terrain --screenshot /absolute/path.png
```

This moves the orthographic camera above the peaks.
Add `--review-fog` to keep the normal exploration mask instead of revealing the whole view.
`./tools/environment_review.py` captures the forest, lake and terrain views into `build/environment-review/`.
