# Environment trials

All three trials are developed in sequence on `codex/terrain`. They are opt-in,
and combine with the existing `--tree3d` vegetation trial. The ordinary launch
keeps the existing landscape until the alternatives have been evaluated.

## 1. Environment placement

```sh
./build.py play -- --tree3d --proton-scatter
```

ProtonScatter generates 40,000 fixed-seed candidate transforms offline. The game
loads the committed JSON once, filters wet ground, the clearing, cart trail,
resource footprints and mine approaches, then builds five shared MultiMeshes:
meadow patches, ferns, flowers, pebbles and twigs. These are decorative; resource
trees and berry bushes keep their simulation positions and gathering rules.
The forest fixture contains 7,600 meadow patches, 331 ferns, 183 flowers,
60 pebbles and 35 twigs. Existing default tufts are replaced only with the flag.
The masks use the initial world layout; this trial does not clear individual
decoration instances when a new building is placed during play.

Upstream is MIT, pinned to commit
`7198cc00078ebde61307578137c3f35223c6e438`. Regenerate with
`./tools/scatter.py`; downloads remain under ignored `.tools`. Two independent
bakes produced SHA-256
`14c2648589f67a4d32a0f383c48e4839dfa27356d4a4ad26f9a5004b7cf96ee1`.
See `assets/environment/proton_scatter/README.md` and its adjacent license.

| Before | After |
| --- | --- |
| ![Original placement](../reviews/environment/01-placement-before.png) | ![ProtonScatter placement](../reviews/environment/01-placement-after.png) |

Capture either view with the same camera and lighting:

```sh
./build.py play -- --showcase --environment-review forest --tree3d --screenshot /absolute/path/before.png
./build.py play -- --showcase --environment-review forest --tree3d --proton-scatter --screenshot /absolute/path/after.png
```

The comparison fixture freezes simulation at daylight, hides interface and fog,
and uses the same world seed, camera, zoom and viewport (1440×900). It is a
visual-review fixture, not evidence of a complete gameplay playthrough.

## 2. Lake appearance

```sh
./build.py play -- --tree3d --proton-scatter --lake-water
```

The lake uses a bounded shader adapted from Boujie's Gerstner normal and
Fresnel equations, pinned to source commit
`25f3c2da458e0a4f65bdb5903dc068b050930c06` under MIT. The surface stays at the
existing water height and follows `Terrain.Shore`. Its shallow teal edge,
deeper blue center, animated ripple normals and broken foam replace the three
flat water discs. Banks, reeds, stones and lily pads retain their positions.
This is an adaptation of shader math, not installation of Boujie's ocean
plugin. The radial depth colors are authored; no real lake bed, refraction,
fluid simulation or buoyancy is added. See `assets/environment/lake/README.md`.

| Before | After |
| --- | --- |
| ![Original lake](../reviews/environment/02-lake-before.png) | ![Lake shader](../reviews/environment/02-lake-after.png) |

```sh
./build.py play -- --showcase --environment-review lake --tree3d --proton-scatter --screenshot /absolute/path/before.png
./build.py play -- --showcase --environment-review lake --tree3d --proton-scatter --lake-water --screenshot /absolute/path/after.png
```

Both frames show the entire surrounding resource layout with the player on
the dry bank. Shader time is fixed to 1.25 seconds in the review fixture.
