# Lake and coastal water

The lake is a single flat radial mesh at `y = 0.025`.
Its outer ring samples the same `Terrain.Shore(angle)` radius as the walkability and shoreline props.
Normalized UV radius drives an authored teal shallow-to-blue center gradient.
This is an approximation, because the ground plane has no lake-depth geometry.

`lake.gdshader` adapts Gerstner normal and Fresnel equations from the MIT-licensed `Chrisknyfe/boujie_water_shader` source at commit `25f3c2da458e0a4f65bdb5903dc068b050930c06`.
It does not use the upstream open-ocean plugin.
It has no geometry displacement, refraction or buoyancy, and it does not read screen or depth textures.
The upstream license is in `LICENSE`.

The shader also shades the coastline.
Coast mesh UVs encode distance from the boundary, which drives shallows and a narrow animated foam wash.
`bank.gdshader` draws irregular-width bank strips with a shore photoscan: rippled sand on the coast, pebbles around the
pond.
The shore darkens toward the waterline as if wet and thins out into the grass along a ragged line.
Its height map shifts it with the view, so its ripples and pebbles look sunk in, as far as the model kit's scans do.
The water shader draws the same photoscan under the shallows, sinking into the water's colour a few units out.
`ShorelineAppearance.ShoreLook` holds each shore's photoscan, tile size, wet shade and fade widths.
Lake stones cluster in patches rather than an even ring.

The `--environment-review` flag freezes the shader's time input at 1.25 seconds for comparable screenshots.
Normal play uses Godot's `TIME`.
Capture the lake view with:

```sh
./build.py play -- --showcase --environment-review lake --screenshot /absolute/path.png
```
