
// Project palette, patch, footpath and shade uniforms and the footpath parallax, added to Terrain3D's generated shader.
uniform sampler2D grass : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D patches : filter_linear, repeat_disable;
uniform sampler2D disturbance : filter_linear, repeat_disable;
uniform sampler2D traffic : filter_linear, repeat_disable;
// Baked shade under scenery, and under world objects: red for contact shade, green for bare soil around rocks.
uniform sampler2D shade : filter_linear, repeat_disable;
uniform sampler2D contact : filter_linear, repeat_disable;
uniform vec4 mines[5];
// The footpath photoscan, drawn in its own colours where the ground is worn; its height, white highest, shifts it with
// the view so its leaves look raised.
uniform sampler2D path_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D path_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D path_height : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
// World units one footpath tile spans, and how far in world units its lowest point shifts from its highest, seen edge
// on, as on the shores and the model kit's scans.
uniform float path_tile = 2.0;
uniform float path_relief = 0.1;

// Shifts the footpath photoscan at uv, in tiles, along the view given along world x and z and up, by the depth its
// height map gives there: one read in place of stepping down to it, so the shift is close where the height changes
// slowly and its lowest point still moves path_relief from its highest, seen edge on.
vec2 path_parallax(vec2 uv, vec3 view) {
    return uv - view.xy * (path_relief / path_tile) * (1.0 - texture(path_height, uv).r);
}
