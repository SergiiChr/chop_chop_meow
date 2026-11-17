// The water's waves, from the Realistic Water Shader port in lake.gdshader (MIT; copyright and permission notice in this
// folder's LICENSE), shared by the water and what floats on it so both rise and fall together.
// LakeAppearance gives them the waves and speed that WaterBody holds.

// Seconds the waves show when zero or more, for still screenshots and the intro film's frames; TIME when negative.
uniform float review_time = -1.0;
uniform float wave_speed;
// Each wave's direction in xy, steepness in z and length in w.
uniform vec4 wave_a;
uniform vec4 wave_b;
uniform vec4 wave_c;

float now() {
	return review_time >= 0.0 ? review_time : TIME;
}

// One Gerstner wave's shift of a point at rest at world x and z, adding its slope to tangent and binormal.
vec4 wave(vec4 parameter, vec2 position, float time, inout vec3 tangent, inout vec3 binormal) {
	float wave_steepness = parameter.z;
	float wave_length = parameter.w;

	float k = TAU / wave_length;
	float c = sqrt(9.8 / k);
	vec2 d = normalize(parameter.xy);
	float f = k * (dot(d, position) - c * time);
	float a = wave_steepness / k;

	tangent += normalize(vec3(1.0 - d.x * d.x * (wave_steepness * sin(f)), d.x * (wave_steepness * cos(f)), -d.x * d.y * (wave_steepness * sin(f))));
	binormal += normalize(vec3(-d.x * d.y * (wave_steepness * sin(f)), d.y * (wave_steepness * cos(f)), 1.0 - d.y * d.y * (wave_steepness * sin(f))));

	return vec4(d.x * (a * cos(f)), a * sin(f) * 0.25, d.y * (a * cos(f)), 0.0);
}

// All three waves' shift of a point at rest at world x and z, now, adding their slopes to tangent and binormal.
vec4 waves(vec2 position, inout vec3 tangent, inout vec3 binormal) {
	float time = now() * wave_speed;
	return wave(wave_a, position, time, tangent, binormal) + wave(wave_b, position, time, tangent, binormal)
		+ wave(wave_c, position, time, tangent, binormal);
}

// How far the waves lift the surface above its rest at a world x and z, now.
float rise(vec2 position) {
	vec3 tangent = vec3(0.0), binormal = vec3(0.0);
	return waves(position, tangent, binormal).y;
}
