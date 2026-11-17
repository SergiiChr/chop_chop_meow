# VHS tape look

`vhs.gdshader` draws the picture from inside a cabin (`InsideWindow`) as an old tape would: smeared colour, tape creases,
a jittering bottom border and noise.
It works on the finished picture of `InsideSet`; the models in it are unchanged.
Its noise is made when the game starts, the same on every run.

The shader is [VHS Post Processing](https://godotshaders.com/shader/vhs-post-processing/) by LazarusOverlook (CC0, as
posted on 2026-11-18), a fork of FMS_Cat's [VCR distortion](https://www.shadertoy.com/view/MdffD7).
It is changed to read the picture it is drawn on instead of the screen behind it.
