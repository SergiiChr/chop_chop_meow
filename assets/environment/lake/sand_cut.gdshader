shader_type spatial;

uniform sampler2D cut_corners : filter_nearest, repeat_disable;
uniform sampler2D sand_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float sand_tile;
uniform float soil_tile;

const float rim = 0.04;
const float outer_radius = 0.18;
const float inner_radius = 0.09;
const float cut_depth = 0.15;
const float wall_width = 0.15;
const float rim_variation = 0.009;
const vec2 rim_frequency = vec2(16.0, 19.0);
varying vec2 world_at;

void vertex() {
    world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

vec2 quarter_direction(int tile) {
    return vec2((tile & 10) != 0 ? 1.0 : -1.0, (tile & 12) != 0 ? 1.0 : -1.0);
}

// Signed distance inside a quadrant with a rounded tip and straight sides.
float inside_quarter(vec2 at, float radius, float edge) {
    vec2 to_corner = vec2(radius + edge) - at;
    return radius - length(max(to_corner, vec2(0.0))) - min(max(to_corner.x, to_corner.y), 0.0);
}

float inset(vec2 at) {
    vec2 shaped = at + rim_variation * sin(at.yx * rim_frequency);
    vec2 cell = floor(shaped);
    vec2 from_corner = shaped - cell - vec2(0.5);
    ivec2 texel = ivec2(cell) + ivec2(1);
    if (any(lessThan(texel, ivec2(0))) || any(greaterThanEqual(texel, textureSize(cut_corners, 0)))) {
        return -wall_width;
    }
    int tiles = int(round(texelFetch(cut_corners, texel, 0).r * 255.0));
    if (tiles == 0) return -wall_width;
    if (tiles == 15) return wall_width;

    // Two tiles sharing an edge leave a straight side. Corner-only neighbors keep two rounded tips.
    if (tiles == 3) return -from_corner.y - rim;
    if (tiles == 12) return from_corner.y - rim;
    if (tiles == 5) return -from_corner.x - rim;
    if (tiles == 10) return from_corner.x - rim;
    if (tiles == 6 || tiles == 9) {
        vec2 quarter = from_corner * (tiles == 9 ? vec2(1.0) : vec2(1.0, -1.0));
        return max(inside_quarter(quarter, outer_radius, rim), inside_quarter(-quarter, outer_radius, rim));
    }
    // A single dug tile has a rounded outer corner. Three dug tiles round the tip of the sand they enclose.
    if ((tiles & (tiles - 1)) == 0) return inside_quarter(from_corner * quarter_direction(tiles), outer_radius, rim);
    return -inside_quarter(from_corner * quarter_direction(15 ^ tiles), inner_radius, -rim);
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
    if (inset(world_at) <= 0.0) discard;
    vec3 toward_camera = PROJECTION_MATRIX[3][3] == 1.0
        ? CAMERA_DIRECTION_WORLD : mat3(INV_VIEW_MATRIX) * VIEW;
    vec2 ray = -toward_camera.xz / max(toward_camera.y, 0.22);
    float depth = hit_depth(world_at, ray);
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
        profile_depth(at + vec2(step, 0.0)) - profile_depth(at - vec2(step, 0.0)),
        profile_depth(at + vec2(0.0, step)) - profile_depth(at - vec2(0.0, step))
    ) / (2.0 * step);
    vec2 grain = texture(soil_normal, at / soil_tile).rg * 2.0 - 1.0;
    slope = 0.8 * slope + grain * vec2(0.3, -0.3) * bare;
    NORMAL = normalize(mat3(VIEW_MATRIX) * vec3(slope.x, 1.0, slope.y));
}
