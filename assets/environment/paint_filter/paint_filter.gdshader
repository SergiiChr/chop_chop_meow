// Paint filter, a generalized Kuwahara filter.
// Each pixel takes the average colour of whichever of eight wedges around it varies least, so even areas turn into
// patches of one paint while edges stay sharp.
// Ported from Acerola's Unity filter (MIT, Copyright (c) 2022 Garrett Gunnell) via Firerabbit's Godot 4 port (MIT).
shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_nearest, repeat_disable;
// Each pixel reads kernel_size / 2 render pixels to each side: larger gives bigger patches of paint and costs more.
uniform int kernel_size : hint_range(2, 20, 1) = 8;
// How strongly a wedge with an even colour wins over the others.
uniform float hardness : hint_range(1.0, 100.0) = 8.0;
uniform float sharpness : hint_range(1.0, 18.0) = 8.0;
// Shape of the wedges: how far they overlap and how fast their weight falls off.
uniform float zero_crossing : hint_range(0.01, 2.0) = 0.58;
uniform float zeta : hint_range(0.01, 3.0) = 1.0;

void fragment() {
	vec4 m[8];
	vec3 s[8];
	for (int k = 0; k < 8; k++) {
		m[k] = vec4(0.0);
		s[k] = vec3(0.0);
	}
	int radius = kernel_size / 2;
	float sin_zero = sin(zero_crossing);
	float eta = (zeta + cos(zero_crossing)) / (sin_zero * sin_zero);

	for (int y = -radius; y <= radius; y++) {
		for (int x = -radius; x <= radius; x++) {
			vec2 v = vec2(float(x), float(y)) / float(radius);
			vec3 c = clamp(texture(screen, SCREEN_UV + vec2(float(x), float(y)) * SCREEN_PIXEL_SIZE).rgb, 0.0, 1.0);
			float w[8];
			float sum = 0.0;
			// Polynomial weights of the four straight wedges, then of the four turned by 45 degrees.
			float vxx = zeta - eta * v.x * v.x;
			float vyy = zeta - eta * v.y * v.y;
			w[0] = pow(max(0.0, v.y + vxx), 2.0);
			w[2] = pow(max(0.0, -v.x + vyy), 2.0);
			w[4] = pow(max(0.0, -v.y + vxx), 2.0);
			w[6] = pow(max(0.0, v.x + vyy), 2.0);
			v = sqrt(2.0) / 2.0 * vec2(v.x - v.y, v.x + v.y);
			vxx = zeta - eta * v.x * v.x;
			vyy = zeta - eta * v.y * v.y;
			w[1] = pow(max(0.0, v.y + vxx), 2.0);
			w[3] = pow(max(0.0, -v.x + vyy), 2.0);
			w[5] = pow(max(0.0, -v.y + vxx), 2.0);
			w[7] = pow(max(0.0, v.x + vyy), 2.0);
			for (int k = 0; k < 8; k++) sum += w[k];
			float g = exp(-3.125 * dot(v, v)) / sum;
			for (int k = 0; k < 8; k++) {
				float wk = w[k] * g;
				m[k] += vec4(c * wk, wk);
				s[k] += c * c * wk;
			}
		}
	}

	vec4 result = vec4(0.0);
	for (int k = 0; k < 8; k++) {
		vec3 mean = m[k].rgb / m[k].w;
		vec3 spread = abs(s[k] / m[k].w - mean * mean);
		float w = 1.0 / (1.0 + pow(hardness * 1000.0 * (spread.r + spread.g + spread.b), 0.5 * sharpness));
		result += vec4(mean * w, w);
	}
	COLOR = vec4(clamp(result.rgb / result.w, 0.0, 1.0), 1.0);
}
