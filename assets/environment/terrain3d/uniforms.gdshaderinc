
// Project palette, patch, footpath, woods and shade uniforms and the photoscans' parallax, added to Terrain3D's
// generated shader.
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
// World units one footpath tile spans.
uniform float path_tile;
// How far in world units the ground photoscans' lowest points shift from their highest, seen edge on
// (ModelKit.ScanRelief).
uniform float scan_relief;

// The photoscan of moss over gravelly earth, drawn in its own colours in sparse broad patches over the meadow, and its
// height.
uniform sampler2D moss_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D moss_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D moss_height : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float moss_tile;
// The forest floor photoscan, drawn in its own colours in the woods along the mountains, and its height.
uniform sampler2D forest_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D forest_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D forest_height : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float forest_tile;
// Distance from the foot of the nearer range, as a share of woods_reach: zero in the mountains, one at the reach and
// beyond; woods_corner is the world x and z of its first texel and woods_span the world units it covers.
uniform sampler2D woods_distance : filter_linear, repeat_disable;
uniform vec2 woods_corner;
uniform float woods_span;
uniform float woods_reach;
// How far out from the foot of the ranges the woods' floor reaches.
uniform float woods_band;

// Shifts a photoscan at uv, in tiles, along the view given along world x and z and up, by the depth its height map
// gives there, so its lowest point moves shift tiles from its highest, seen edge on: one read in place of stepping down
// to the height map, which in this shader slowed all the ground.
vec2 scan_parallax(sampler2D height, vec2 uv, vec3 view, float shift) {
    return uv - view.xy * shift * (1.0 - texture(height, uv).r);
}
