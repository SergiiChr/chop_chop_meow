
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
// beyond. map_corner is the world x and z of its first texel and map_span the world units it covers, for this map and
// the beds map alike.
uniform sampler2D woods_distance : filter_linear, repeat_disable;
uniform vec2 map_corner;
uniform float map_span;
uniform float woods_reach;
// How far out from the foot of the ranges the woods' floor reaches.
uniform float woods_band;

// The shore photoscans the beds under the water take, with the world units one tile spans and how dark they are, as a
// share of the dry shore's: mossy stones in the pond, sand under the sea.
uniform sampler2D rocks_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rocks_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rocks_height : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float rocks_tile;
uniform float rocks_wet;
uniform sampler2D sand_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D sand_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D sand_height : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float sand_tile;
uniform float sand_wet;
// Red under the pond's drawn water and green under the sea's, on the same texels as the woods map.
uniform sampler2D beds : filter_linear, repeat_disable;

// Shifts a photoscan at uv, in tiles, along the view given along world x and z and up, by the depth its height map
// gives there, so its lowest point moves shift tiles from its highest, seen edge on: one read in place of stepping down
// to the height map, which in this shader slowed all the ground.
vec2 scan_parallax(sampler2D height, vec2 uv, vec3 view, float shift) {
    return uv - view.xy * shift * (1.0 - texture(height, uv).r);
}

// A shore photoscan as a bed under the water, at the shade of its wet waterline, with its bumps through normal_map.
vec3 bed_scan(sampler2D albedo, sampler2D normal, sampler2D height, float tile, float wet, vec2 at, vec3 view,
        out vec3 normal_map) {
    vec2 uv = scan_parallax(height, at / tile, view, scan_relief / tile);
    normal_map = texture(normal, uv).rgb;
    return texture(albedo, uv).rgb * wet;
}
