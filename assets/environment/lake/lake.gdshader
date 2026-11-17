/*
Realistic Water Shader for Godot 3.4
Modified to work with Godot 3.4 with thanks to jmarceno.
Copyright (c) 2019 UnionBytes, Achim Menzel (alias AiYori)
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
-- UnionBytes
-- YouTube: www.youtube.com/user/UnionBytes
*/

// Ported to Godot 4 for the lake and the sea. Changes from the original:
// - The depth and the ground's position under the water come from the inverse projection, which works for the game's
//   orthographic camera and the intro film's perspective one, with Godot 4's reversed depth.
// - The refraction bends by how far the ripples tilt the surface, on screen: the original's bend by the normal's own
//   x and y, in the mesh's space where y is up, shifted all of it up the screen and drew the shore into the water.
// - The refraction shrinks with the view's distance, or in orthographic view with half its height.
// - Refraction does not pick up things standing in front of the water.
// - The caustics lie in world x and z, at the original's 300 tiles per 1024 units.
// - The foam drops a test that always passed.
// - The coast's waterline wanders and cuts the mesh along it.
// - The sampler scale is the demo material's; LakeAppearance gives each body of water its colours, waves and how much
//   foam it has.
// - The waves live in waves.gdshaderinc, which the lily pads share, and review_time can hold them at one moment.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx, blend_mix;

#include "res://assets/environment/lake/waves.gdshaderinc"

uniform vec2 sampler_scale = vec2(0.2, 0.2); // Scale for the sampler
uniform vec2 sampler_direction = vec2(0.05, 0.04); // Direction and speed for the sampler offset

uniform sampler2D uv_sampler : filter_linear_mipmap_anisotropic, repeat_enable; // UV motion sampler for shifting the normalmap
uniform vec2 uv_sampler_scale = vec2(0.25, 0.25); // UV sampler scale
uniform float uv_sampler_strength = 0.04; // UV shifting strength

uniform sampler2D normalmap_a_sampler : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable; // Normalmap sampler A
uniform sampler2D normalmap_b_sampler : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable; // Normalmap sampler B

uniform sampler2D foam_sampler : hint_default_black, filter_linear_mipmap, repeat_enable; // Foam sampler
// How much of the foam lies over the shallow water, from none at zero to all of it at one.
uniform float foam = 1.0;

uniform float refraction = 0.075; // Refraction of the water

uniform vec4 color_deep : source_color; // Color for deep places in the water, medium to dark blue
uniform vec4 color_shallow : source_color; // Color for lower places in the water, bright blue - green
uniform float beers_law = 2.0; // Beers law value, regulates the blending size to the deep water level
uniform float depth_offset = -0.75; // Offset for the blending

uniform sampler2DArray caustic_sampler : hint_default_black, filter_linear_mipmap, repeat_enable; // Caustic sampler, (Texture array with 16 Textures for the animation)

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;

// Units over which the coast's waterline wanders in broad lobes and small bites; the water is cut away inland of it,
// showing the sand, along its UV.y, the distance out from its line, which starts half of this inland.
// Zero for the lake, which keeps its whole surface.
uniform float shore_wander;

varying vec2 surface_uv; // Where the surface's maps are read, from its world position
varying vec2 rest_position; // World x and z of the surface where the waves leave it, which the coast's cut follows

// Where the ground seen at a screen point is, in view space.
vec3 ground_at(vec2 screen, mat4 inverse_projection) {
	vec4 ground = inverse_projection * vec4(screen * 2.0 - 1.0, texture(depth_texture, screen).r, 1.0);
	return ground.xyz / ground.w;
}

void vertex() {
	vec4 vertex = vec4(VERTEX, 1.0);
	rest_position = (MODEL_MATRIX * vertex).xz;

	vec3 tang = vec3(0.0, 0.0, 0.0);
	vec3 bin = vec3(0.0, 0.0, 0.0);
	vertex += waves(rest_position, tang, bin);

	TANGENT = tang;
	BINORMAL = bin;
	NORMAL = normalize(cross(bin, tang));

	surface_uv = (MODEL_MATRIX * vertex).xz * sampler_scale;

	VERTEX = vertex.xyz;
}

void fragment() {
	vec2 at = rest_position;
	float lobes = sin(at.x * 0.83 + at.y * 0.37) * sin(at.y * 0.61 - at.x * 0.29);
	float bites = sin(at.x * 2.7 + at.y * 1.9) * sin(at.y * 3.1 - at.x * 2.3);
	if (UV.y < (lobes * 0.7 + bites * 0.3) * shore_wander * 0.5) discard;
	float time = now();

	// Calculation of the UV with the UV motion sampler
	vec2 uv_offset = sampler_direction * time;
	vec2 uv_sampler_uv = surface_uv * uv_sampler_scale + uv_offset;
	vec2 uv_sampler_uv_offset = uv_sampler_strength * texture(uv_sampler, uv_sampler_uv).rg * 2.0 - 1.0;
	vec2 uv = surface_uv + uv_sampler_uv_offset;

	// Normalmap:
	vec3 normalmap = texture(normalmap_a_sampler, uv - uv_offset * 2.0).rgb * 0.75; // 75 % sampler A
	normalmap += texture(normalmap_b_sampler, uv + uv_offset).rgb * 0.25; // 25 % sampler B

	// Refraction UV:
	bool orthogonal = PROJECTION_MATRIX[3][3] == 1.0;
	float view_span = orthogonal ? 1.0 / PROJECTION_MATRIX[1][1] : -VERTEX.z;
	vec3 ref_normalmap = normalmap * 2.0 - 1.0;
	ref_normalmap = normalize(TANGENT * ref_normalmap.x + BINORMAL * ref_normalmap.y + NORMAL * ref_normalmap.z);
	vec2 ref_uv = SCREEN_UV + ((ref_normalmap - NORMAL).xy * refraction) / view_span;

	// Where the ground under the water is; a bent read landing on something in front of the water reads straight
	// through instead.
	vec3 ground = ground_at(ref_uv, INV_PROJECTION_MATRIX);
	if (ground.z > VERTEX.z) {
		ref_uv = SCREEN_UV;
		ground = ground_at(ref_uv, INV_PROJECTION_MATRIX);
	}
	// How much water the view passes through to reach the ground.
	float thickness = VERTEX.z - ground.z;

	float depth_blend = exp((thickness + depth_offset) * -beers_law);
	depth_blend = clamp(1.0 - depth_blend, 0.0, 1.0);
	float depth_blend_pow = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	// Ground color:
	vec3 screen_color = textureLod(screen_texture, ref_uv, depth_blend_pow * 2.5).rgb;

	vec3 dye_color = mix(color_shallow.rgb, color_deep.rgb, depth_blend_pow);
	vec3 color = mix(screen_color * dye_color, dye_color * 0.25, depth_blend_pow * 0.5);

	// Caustic projection onto the ground under the water
	vec2 ground_world = (INV_VIEW_MATRIX * vec4(ground, 1.0)).xz;
	vec4 caustic_color = texture(caustic_sampler, vec3(ground_world * 300.0 / 1024.0, mod(time * 14.0, 16.0)));

	color *= 1.0 + pow(caustic_color.r, 1.50) * (1.0 - depth_blend) * 6.0;

	// Foam:
	float foam_noise = clamp(pow(texture(foam_sampler, (uv * 4.0) - uv_offset).r, 10.0) * 40.0, 0.0, 0.2);
	float foam_mix = clamp(pow((1.0 - thickness + foam_noise), 8.0) * foam_noise * 0.4, 0.0, 1.0) * foam;
	color = mix(color, vec3(1.0), foam_mix);

	ALBEDO = color;
	METALLIC = 0.1;
	ROUGHNESS = 0.2;
	SPECULAR = 0.2 + depth_blend_pow * 0.4;
	NORMAL_MAP = normalmap;
	NORMAL_MAP_DEPTH = 1.25;
}
