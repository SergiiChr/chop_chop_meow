shader_type spatial;
render_mode cull_disabled, depth_draw_never;

uniform vec4 dry_color : source_color;
uniform vec4 wet_color : source_color;
varying vec2 world_at;

void vertex() {
    world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    float grain = sin(world_at.x * 15.3 + sin(world_at.y * 9.7)) * sin(world_at.y * 18.1) * 0.5 + 0.5;
    float fleck = sin(world_at.x * 2.7 + world_at.y * 1.3) * sin(world_at.y * 3.1 - world_at.x * 1.7);
    float distance = UV.x + fleck * 0.1;
    ALBEDO = mix(wet_color.rgb, dry_color.rgb, smoothstep(0.12, 1.3, distance));
    ALBEDO *= 0.985 + grain * 0.03;
    ROUGHNESS = mix(0.65, 1.0, smoothstep(0.0, 1.0, distance));
    ALPHA = smoothstep(-0.3, 0.06, distance) * (1.0 - smoothstep(max(0.1, UV.y - 0.65), UV.y, distance));
}
