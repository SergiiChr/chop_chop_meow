# Lake and coastal water

The lake and the sea lie over real beds, which `Terrain.BedDepth` digs into the ground from where walking stops, so
the ground people walk on stays flat. The sea bed starts at the low-tide edge of the beach.
The lake is a bowl 2.5 units deep at its middle, level at the shore and deepening gently; the sea floor drops one
unit for every eight out, to 6 units deep.
The terrain draws the ground under the drawn water with the shore photoscan, at the wet shade of the waterline: mossy
stones in the lake, sand under the sea, also where the sea's waterline wanders inland of where walking stops.
`BedMap`, baked at one texel a world unit, marks the lake bed and the tide that submerges each point on the beach.

`lake.gdshader` is a Godot 4 port of the [Realistic Water Shader](https://godotshaders.com/shader/realistic-water/) by
UnionBytes (Achim Menzel), released under the MIT licence in `LICENSE`, with its contributors in `AUTHORS.md`.
Its header lists what the port changes.
Three Gerstner waves move the surface, a twelfth as steep on the lake as on the sea; two scrolling normal maps ripple
it.
The water reads the ground under it from Godot's depth and screen textures: it bends the view of the bed by the
ripples, tints it from its shallow colour to its deep one by how much water the view passes through, and lays
animated caustics and foam over the shallows.
The sea goes from light cyan shallows to a cyan deep, the lake from lighter shallows to a lighter deep still; only the
sea has foam.
Things standing in the water are not part of what it shows through, and the sand and stone bank strips are not either:
Godot copies the screen for the water before it draws anything see-through.
`waves.gdshaderinc` holds the waves, which `lily_pad.gdshader` shares so the pads rise and fall with the water.
`WaterBody` holds each body's waves and colours, the waves' speed, and spaces the surfaces' vertices by the shortest
wave; `LakeAppearance` gives them to the water and lily pad materials.
The textures are the shader's demo project's (`water_*.png`), downloaded by `tools/water_textures.py`, which pins
their commit and SHA-256; `water_caustics.png` imports as an array of its 16 frames, four by four.

The surfaces have a vertex every 0.3 units, three to the shortest wave.
The lake's is a ring of quads out to its shore.
The sea's starts inland of the high-tide line and reaches 400 units out; it is dense
within 150 units either side of the valley's middle line and 90 units out, and coarser beyond.
It is cut into tiles of 100 by 100 vertices, so Godot skips the tiles off screen, and the screen copy the water reads
with them while no water is in view.
The coast's waterline wanders in lobes and small bites, up to 0.6 units either side of its line, and the water shader
cuts the surface along it, showing the sand.
The tide raises the surface as the cut moves inland, covering the sand strip at the full moon.
At the new moon the surface drops slightly below its original position and exposes more sand.
The lowest waterline bounds walking at all tides.

`bank.gdshader` draws irregular-width bank strips with a shore photoscan: rippled sand on the coast, mossy ground with
stones around the lake.
The shore darkens toward the waterline as if wet and thins out into the grass along a ragged line.
The coast's sand runs from about 2 to 8 units wide by `Terrain.SandWidth`, which also keeps grass and trees off it.
The grass edge bites in and out by up to 1.2 units more on the coast.
One read of the shore's height map shifts it along the view, so its ripples and stones look sunk in, as far as the
model kit's scans do.
The bank strips start under the water and follow the bed down.
`ShoreLook` holds each shore's photoscan, tile size, wet shade, grass fade and wander.
Lake stones cluster in patches rather than an even ring.

The `--environment-review` flag freezes the waves, the water's maps and the lily pads at 1.25 seconds for comparable
screenshots, and the intro film moves them by its own clock.
Normal play moves them by the game's own water clock (`LakeAppearance.Advance`), which stands still while the game is
paused: each wave's phase is kept below a turn, and the maps' scroll and the caustics' 16 frames come round whole every
800 seconds, so nothing jumps however long the game runs.
The caustic frames blend one into the next.
Capture the lake view with:

```sh
./build.py play -- --showcase --environment-review lake --screenshot /absolute/path.png
```
