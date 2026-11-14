shader_type spatial;
render_mode cull_disabled, depth_draw_never;

// The shore photoscan: dry inland, darker where wet, thinning out into the grass.
uniform sampler2D shore_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D shore_normal : hint_normal, filter_linear_mipmap, repeat_enable;
// The photoscan's height, white highest, which shifts it with the view so its bumps look sunk in.
uniform sampler2D shore_height : hint_default_white, filter_linear_mipmap, repeat_enable;
// How far in world units the photoscan's lowest point shifts from its highest, seen edge on; the model kit's scans,
// drawn by the engine's own parallax, shift as far.
uniform float shore_relief = 0.1;
// World units one shore tile spans.
uniform float shore_tile = 15.0;
// How much darker the shore is at the waterline, where it is wet; the water shader starts from the same shade.
uniform float shore_wet = 0.6;
// Units over which the shore thins out into the grass at its inland edge.
uniform float grass_fade = 2.2;
varying vec2 world_at;

void vertex() {
    world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    // The shore is laid out by world position, so its bumps lean along world x and z.
    TANGENT = vec3(1.0, 0.0, 0.0);
    BINORMAL = vec3(0.0, 0.0, -1.0);
}

void fragment() {
    float fleck = sin(world_at.x * 2.7 + world_at.y * 1.3) * sin(world_at.y * 3.1 - world_at.x * 1.7);
    float distance = UV.x + fleck * 0.1;
    float dryness = smoothstep(0.12, 1.3, distance);
    vec2 shore_uv = world_at / shore_tile;
    // Steps into the photoscan along the view, from its top down, until it reaches the depth the height map gives
    // there, then settles between the last two steps; a steeper view needs fewer steps.
    vec3 view = normalize(VIEW * mat3(TANGENT, -BINORMAL, NORMAL));
    float layers = mix(48.0, 12.0, abs(view.z));
    vec2 step = view.xy * (shore_relief / shore_tile) / layers;
    float layer = 0.0, depth = 1.0 - texture(shore_height, shore_uv).r;
    for (int i = 0; i < 48 && layer < depth; i++) {
        shore_uv -= step;
        depth = 1.0 - texture(shore_height, shore_uv).r;
        layer += 1.0 / layers;
    }
    float after = depth - layer, before = 1.0 - texture(shore_height, shore_uv + step).r - layer + 1.0 / layers;
    shore_uv = mix(shore_uv, shore_uv + step, after / (after - before));
    vec3 color = texture(shore_albedo, shore_uv).rgb;
    ALBEDO = color * mix(shore_wet, 1.0, dryness);
    NORMAL_MAP = texture(shore_normal, shore_uv).rgb;
    ROUGHNESS = mix(0.65, 1.0, smoothstep(0.0, 1.0, distance));
    // Toward the grass the shore thins out along a line that wanders with broad patches and frays at the
    // photoscan's own bright and dark spots.
    float patches = sin(world_at.x * 0.45 + world_at.y * 0.21) * sin(world_at.y * 0.37 - world_at.x * 0.18);
    float fray = dot(color, vec3(0.3, 0.59, 0.11)) - 0.45;
    float inland = distance + (patches * 0.6 + fray * 1.2) * grass_fade / 2.2;
    ALPHA = smoothstep(-0.3, 0.06, distance) * (1.0 - smoothstep(UV.y - grass_fade, UV.y, inland));
}
