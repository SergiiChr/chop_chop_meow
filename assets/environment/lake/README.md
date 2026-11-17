# Lake and coastal water

The pond and the sea lie over real beds, which `Terrain.BedDepth` digs into the ground from where walking stops, so
the ground people walk on stays flat.
The pond is a bowl 2.5 units deep at its middle; the sea floor drops one unit for every four out, to 6 units deep.
The terrain draws the ground under the drawn water with the shore photoscan, at the wet shade of the waterline: mossy
stones in the pond, sand under the sea, also where the sea's waterline wanders inland of where walking stops.
A beds map baked while loading, one texel a world unit, tells it where the drawn water lies.

`lake.gdshader` is a Godot 4 port of the [Realistic Water Shader](https://godotshaders.com/shader/realistic-water/) by
UnionBytes (Achim Menzel), released under the MIT licence in `LICENSE`.
Its header lists what the port changes.
Three Gerstner waves move the surface; two scrolling normal maps ripple it.
The water reads the ground under it from Godot's depth and screen textures: it bends the view of the bed by the
ripples, tints it from pale shallows to a grey-blue deep by how much water the view passes through, and lays
animated caustics and foam over the shallows.
Things standing in the water are not part of what it shows through, and the sand and stone bank strips are not either:
Godot copies the screen for the water before it draws anything see-through.
`waves.gdshaderinc` holds the waves, which `lily_pad.gdshader` shares so the pads rise and fall with the water.
`LakeAppearance` sets the three waves and their speed, and spaces the surfaces' vertices by the shortest.
The textures are the shader's demo project's (`water_*.png`), downloaded by `tools/water_textures.py`, which pins
their commit and SHA-256; `water_caustics.png` imports as an array of its 16 frames, four by four.

The surfaces have a vertex every 0.3 units, three to the shortest wave.
The pond's is a ring of quads out to its shore.
The sea's starts inland of its line by as far as the waterline wanders and reaches 400 units out; it is dense
within 150 units either side of the valley's middle line and 90 units out, and coarser beyond.
It is cut into tiles of 100 by 100 vertices, so Godot skips the tiles off screen, and the screen copy the water reads
with them while no water is in view.
The coast's waterline wanders in lobes and small bites, up to 0.6 units either side of its line, and the water shader
cuts the surface along it, showing the sand.

`bank.gdshader` draws irregular-width bank strips with a shore photoscan: rippled sand on the coast, mossy ground with
stones around the pond.
The shore darkens toward the waterline as if wet and thins out into the grass along a ragged line.
The coast's sand runs from about 2 to 8 units wide by `Terrain.SandWidth`, which also keeps grass and trees off it.
The grass edge bites in and out by up to 1.2 units more on the coast.
One read of the shore's height map shifts it along the view, so its ripples and stones look sunk in, as far as the
model kit's scans do.
The bank strips start under the water and follow the bed down.
`ShorelineAppearance.ShoreLook` holds each shore's photoscan, tile size, wet shade and fade widths.
Lake stones cluster in patches rather than an even ring.

The `--environment-review` flag freezes the waves, the water's maps and the lily pads at 1.25 seconds for comparable
screenshots, and the intro film moves them by its own clock.
Normal play uses Godot's `TIME`.
Capture the lake view with:

```sh
./build.py play -- --showcase --environment-review lake --screenshot /absolute/path.png
```
