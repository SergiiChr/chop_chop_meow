// A lily pad, flat and rising and falling with the water's waves at its middle.
shader_type spatial;

#include "res://assets/environment/lake/waves.gdshaderinc"

uniform vec4 color : source_color;
uniform float roughness = 0.7;

void vertex() {
	VERTEX.y += rise(NODE_POSITION_WORLD.xz);
}

void fragment() {
	ALBEDO = color.rgb;
	ROUGHNESS = roughness;
}
