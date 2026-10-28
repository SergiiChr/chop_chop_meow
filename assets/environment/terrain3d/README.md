# Terrain3D landscape trial

This opt-in trial replaces the broad landscape mesh with Terrain3D's native
clipmap renderer. Launch it with:

```sh
./build.py play -- --terrain3d
```

`build.py setup` installs the pinned Linux runtime into the ignored
`addons/terrain_3d/` folder when `--terrain3d` is forwarded. The install uses
the official Terrain3D `v1.1.0-rc1` release archive and verifies its SHA-256:

`549d0c1d45982ae2e30d90275f754a9c7174b8f57eb600bc93a8108b98d44636`

The source is the [TokisanGames/Terrain3D release](https://github.com/TokisanGames/Terrain3D/releases/tag/v1.1.0-rc1),
under the MIT license; the full license from the archive is in [LICENSE.txt](LICENSE.txt).
The runtime installer extracts only the Linux extension, its manifest, icon,
and license. It does not require a separate repository checkout. The release
is a release candidate, not a stable final version.

`Terrain3DView` builds nine heightmap regions in a 3×3 grid around the map,
each with 256×256 `RF` samples. Samples come from the project's existing
`MountainShape.Height` function, clamped to zero and lowered by 0.03 to sit
below the separately rendered mine shells. The playable valley floor
therefore remains level at height zero. Terrain3D supplies the clipmaps,
height interpolation, generated normals, and terrain UVs; the project retains
its existing simulation height and movement rules.

The trial enables Terrain3D collision mode zero. Terrain collision does not
drive walking, building placement, or other simulation rules. Instead, the
current gameplay model continues to own the level floor and mountain checks.
The shader insertion in `Terrain3DView` keeps the pinned generated Terrain3D
shader core and adds project meadow/rock palette, exploration fog, and fragment
clipping for the five mine openings so their separately modeled rock shells
remain open.

The runtime extension is accessed through `ClassDB.Instantiate` and dynamic
`Set`/`Call` methods. Terrain3D's generated C# wrappers are incomplete for this
use and are excluded from the isolated probe project. The optional
`./tools/terrain3d.py --probe` extracts the addon under ignored `.tools/` and
checks native loading, region heightmap access, and the collision API in that
separate probe; it does not enable collision in the game trial.
