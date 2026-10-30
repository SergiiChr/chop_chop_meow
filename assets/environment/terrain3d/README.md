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

The shader insertion in `Terrain3DView` keeps the pinned generated Terrain3D shader core.
It adds the project meadow/rock palette, exploration fog, and fragment clipping for the five mine openings so their separately modeled rock shells stay open.

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
