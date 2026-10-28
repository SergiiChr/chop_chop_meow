// Adapted from the Gerstner wave, normal and Fresnel math in Chrisknyfe/boujie_water_shader.
// This lake surface stays flat at the authored shore; wave motion changes its shading only.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform float review_time = -1.0;
uniform bool coastal = false;
uniform float roughness : hint_range(0.0, 1.0) = 0.22;
uniform float specular : hint_range(0.0, 1.0) = 0.7;
varying vec3 world_position;

// Adapted from Chrisknyfe/boujie_water_shader commit 25f3c2d.
vec3 N_DEG(float x, float z, float t, float steepness, float amplitude, float direction_degrees, float frequency, float speed, float phase_degrees) {
	vec2 direction = vec2(sin(direction_degrees * TAU / 360.0), cos(direction_degrees * TAU / 360.0));
	float phase = phase_degrees * TAU / 360.0;
	float angle = frequency * TAU * dot(direction, vec2(x, z)) + speed * (t + phase);
	return vec3(
		-direction.x * frequency * amplitude * cos(angle),
		1.0 - steepness * frequency * amplitude * sin(angle),
		-direction.y * frequency * amplitude * cos(angle)
	);
}

void vertex() {
	// Keep the default vertex transform, exactly matching the exploration-fog overlay's depth.
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Mesh UVs are the normalized, lake-centred radius; 1 - length(UV) is shore distance.
}

void fragment() {
	float t = review_time >= 0.0 ? review_time : TIME;
	float radial = clamp(length(UV), 0.0, 1.0);
	float shore_distance = coastal ? max(UV.y, 0.0) : (1.0 - radial) * 13.5;
	float edge = coastal ? 1.0 - smoothstep(0.0, 10.0, shore_distance) : smoothstep(0.12, 1.0, radial);
	vec3 deep_water = vec3(0.025, 0.095, 0.14);
	vec3 shallow_water = vec3(0.08, 0.28, 0.26);
	vec3 water_color = mix(deep_water, shallow_water, edge);
	// A narrow wet transition connects water to the bank without widening the gameplay boundary.
	water_color = mix(vec3(0.075, 0.14, 0.105), water_color, smoothstep(0.0, 0.65, shore_distance));

	// Two low-energy Gerstner normals animate small highlights without moving the shore.
	vec3 wave_a = N_DEG(world_position.x, world_position.z, t, 0.30, 0.24, 28.0, 0.32, 0.72, 0.0);
	vec3 wave_b = N_DEG(world_position.x, world_position.z, t, 0.22, 0.14, 121.0, 0.55, -0.48, 34.0);
	vec3 wave_normal_world = normalize((wave_a + wave_b) * 0.5);
	vec3 wave_normal = (VIEW_MATRIX * vec4(wave_normal_world, 0.0)).xyz;
	NORMAL = normalize(mix(NORMAL, wave_normal, 0.62));
	float fresnel = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 3.0);

	// A thin, irregular foam trace breaks into gaps along the bank.
	float breakup = sin(world_position.x * 1.13 + world_position.z * 1.71) *
		sin(world_position.x * 2.37 - world_position.z * 1.39 + t * 0.08);
	float wash = coastal ? 0.14 + 0.06 * sin(world_position.x * 0.7 + t * 0.8) : 0.055;
	float foam_line = 1.0 - smoothstep(wash, wash + (coastal ? 0.24 : 0.14), shore_distance);
	float broken_foam = foam_line * smoothstep(-0.10, 0.55, breakup);

	ALBEDO = water_color + vec3(0.055, 0.105, 0.13) * fresnel;
	// Small crossing ripples keep the calm surface legible under an orthographic camera.
	float ripple = sin(dot(world_position.xz, vec2(0.04, 0.62)) * TAU - t * 0.72 + sin(world_position.x * 0.3) * 0.5);
	float ripple_breakup = smoothstep(0.0, 0.7, sin(world_position.x * 0.64 + world_position.z * 0.2 + t * 0.18));
	ALBEDO += vec3(0.012, 0.024, 0.022) * smoothstep(0.94, 1.0, ripple) * ripple_breakup * (coastal ? 0.8 : 1.0 - radial * 0.5);
	ALBEDO = mix(ALBEDO, vec3(0.72, 0.83, 0.76), broken_foam * 0.38);
	ROUGHNESS = roughness;
	SPECULAR = specular;
	METALLIC = 0.0;
}
