# Paint filter

`paint_filter.gdshader` is a generalized Kuwahara filter that `PaintFilter` draws once over the whole screen, so the
world looks painted.
Each pixel takes the average colour of whichever of eight wedges around it varies least, so even areas turn into
patches of one paint while edges stay sharp.
It works on the finished picture: models, materials and textures are unchanged.
It draws after the 3D world and before the name tags, progress bars and interface, which stay sharp.
It is on by default, in play and in review runs such as screenshots and the intro film's render.
The options switch it off and on; a run that needs it otherwise changes `paint_filter` in its settings file.
Its `kernel_size`, 8, sets how far each pixel reads its colour from: 4 render pixels to each side, an area 9 across.
Cost grows with its square.

The shader adapts the [Generalized Kuwahara](https://godotshaders.com/shader/generalized-kuwahara/) Godot 4 port by
Firerabbit (MIT, as posted on 2026-11-17) of Acerola's Unity filter in
[GarrettGunnell/Post-Processing](https://github.com/GarrettGunnell/Post-Processing/tree/871a7ffcc44174f237b4c0127db51026c438d7a4/Assets/Kuwahara%20Filter).
The upstream license is in `LICENSE`.
