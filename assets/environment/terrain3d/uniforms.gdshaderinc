
// Project palette, patch, footpath, shade and mountain photoscan uniforms and functions, added to Terrain3D's generated
// shader.
uniform sampler2D grass : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D patches : filter_linear, repeat_disable;
uniform sampler2D disturbance : filter_linear, repeat_disable;
uniform sampler2D traffic : filter_linear, repeat_disable;
// Baked shade under scenery, and under world objects: red for contact shade, green for bare soil around rocks.
uniform sampler2D shade : filter_linear, repeat_disable;
uniform sampler2D contact : filter_linear, repeat_disable;
uniform vec4 mines[5];
// The mountain photoscan, drawn in its own colours over the ranges and a band along their foot; its height, white
// highest, shifts it with the view so its stones look raised.
uniform sampler2D mountain_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D mountain_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D mountain_height : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
// Distance from the foot of the nearer range, as a share of mountain_reach: zero in the mountains, one at the reach
// and beyond; foot_corner is the world x and z of its first texel and foot_span the world units it covers.
uniform sampler2D mountain_foot : filter_linear, repeat_disable;
uniform vec2 foot_corner;
uniform float foot_span;
uniform float mountain_reach;
// World units one mountain tile spans, and the band along the foot where the photoscan frays into the meadow.
uniform float mountain_tile = 30.0;
uniform float mountain_band = 5.0;
// How far in world units the photoscan's lowest point shifts from its highest, seen edge on, as on the shores.
uniform float mountain_relief = 0.1;

// Steps into the mountain photoscan at uv, in tiles, along the view given along the photoscan's two axes and out from
// it, from its top down, until it reaches the depth the height map gives there, then settles between the last two
// steps; a steeper view needs fewer steps.
vec2 mountain_parallax(vec2 uv, vec3 view) {
    float layers = mix(48.0, 12.0, abs(view.z));
    vec2 step = view.xy * (mountain_relief / mountain_tile) / layers;
    float layer = 0.0, depth = 1.0 - texture(mountain_height, uv).r;
    for (int i = 0; i < 48 && layer < depth; i++) {
        uv -= step;
        depth = 1.0 - texture(mountain_height, uv).r;
        layer += 1.0 / layers;
    }
    float after = depth - layer, before = 1.0 - texture(mountain_height, uv + step).r - layer + 1.0 / layers;
    return mix(uv, uv + step, after / (after - before));
}

// The mountain photoscan's bump at uv, in tiles, as a normal along the photoscan's two axes and out from it, laid on
// the ground's own normal given the same way.
vec3 mountain_bump(vec2 uv, vec3 ground) {
    vec3 bump = texture(mountain_normal, uv).rgb * 2.0 - 1.0;
    // The photoscan's green points up the image, toward smaller uv.
    bump.y = -bump.y;
    return vec3(bump.xy + ground.xy, abs(bump.z) * ground.z);
}
