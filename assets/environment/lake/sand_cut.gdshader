shader_type spatial;

uniform sampler2D sand_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float sand_tile;
uniform float soil_tile;

const float half_width = 0.47;
const float corner_radius = 0.18;
const float cut_depth = 0.15;
const float wall_width = 0.15;
const float rim_variation = 0.009;
const vec2 rim_frequency = vec2(16.0, 19.0);
varying vec2 tile_at;
varying vec2 world_at;
varying vec2 rim_phase;

void vertex() {
    tile_at = VERTEX.xz;
    world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    rim_phase = MODEL_MATRIX[3].xz * vec2(0.83, 0.61);
}

// Distance inward from the round-edged opening. The last few centimetres of sand stay between adjoining cuts.
float inset(vec2 at) {
    vec2 shape = at + rim_variation * sin(at.yx * rim_frequency + rim_phase);
    vec2 corner = max(abs(shape) - vec2(half_width - corner_radius), vec2(0.0));
    return corner_radius - length(corner);
}

float profile_depth(vec2 at) {
    return cut_depth * smoothstep(0.0, wall_width, inset(at));
}

// Step by the most the clearance can close along the ray, so grazing views cannot skip the near wall.
float hit_depth(vec2 entry, vec2 ray) {
    // Smoothstep's steepest slope is 1.5, and the rim's variation can steepen the wall further.
    const float max_slope = 1.5 * cut_depth / wall_width
        * (1.0 + rim_variation * max(rim_frequency.x, rim_frequency.y));
    float advance = 1.0 / (1.0 + max_slope * length(ray));
    float depth = 0.0;
    for (int i = 0; i < 32; i++) {
        float gap = profile_depth(entry + ray * depth) - depth;
        if (gap < 0.0003) break;
        depth += gap * advance;
    }
    return depth;
}

void fragment() {
    if (inset(tile_at) <= 0.0) discard;
    vec3 toward_camera = PROJECTION_MATRIX[3][3] == 1.0
        ? CAMERA_DIRECTION_WORLD : mat3(INV_VIEW_MATRIX) * VIEW;
    vec2 ray = -toward_camera.xz / max(toward_camera.y, 0.22);
    float depth = hit_depth(tile_at, ray);
    vec2 hit = tile_at + ray * depth;
    vec2 at = world_at + ray * depth;

    vec3 sand = texture(sand_albedo, at / sand_tile).rgb;
    vec3 soil = texture(soil_albedo, at / soil_tile).rgb;
    float bare = smoothstep(0.018, 0.075, depth);
    ALBEDO = mix(sand, soil, bare);
    AO = mix(1.0, 0.7, depth / cut_depth);
    AO_LIGHT_AFFECT = 0.35;
    ROUGHNESS = 1.0;

    const float step = 0.008;
    vec2 slope = vec2(
        profile_depth(hit + vec2(step, 0.0)) - profile_depth(hit - vec2(step, 0.0)),
        profile_depth(hit + vec2(0.0, step)) - profile_depth(hit - vec2(0.0, step))
    ) / (2.0 * step);
    vec2 grain = texture(soil_normal, at / soil_tile).rg * 2.0 - 1.0;
    slope = 0.8 * slope + grain * vec2(0.3, -0.3) * bare;
    NORMAL = normalize(mat3(VIEW_MATRIX) * vec3(slope.x, 1.0, slope.y));
}
