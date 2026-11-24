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
    vec2 shape = at + 0.009 * sin(at.yx * vec2(16.0, 19.0) + rim_phase);
    vec2 corner = max(abs(shape) - vec2(half_width - corner_radius), vec2(0.0));
    return corner_radius - length(corner);
}

float profile_depth(vec2 at) {
    return cut_depth * smoothstep(0.0, wall_width, inset(at));
}

// Follow the view ray below the rim and stop at the first wall or floor it meets.
float hit_depth(vec2 entry, vec2 ray) {
    const int steps = 12;
    const float step_depth = cut_depth / float(steps);
    float previous_gap = -profile_depth(entry);
    for (int i = 1; i <= steps; i++) {
        float depth = float(i) * step_depth;
        float gap = depth - profile_depth(entry + ray * depth);
        if (gap >= 0.0) return depth - step_depth * gap / max(gap - previous_gap, 0.0001);
        previous_gap = gap;
    }
    return cut_depth;
}

void fragment() {
    if (inset(tile_at) <= 0.0) discard;
    vec3 toward_camera = mat3(INV_VIEW_MATRIX) * VIEW;
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
