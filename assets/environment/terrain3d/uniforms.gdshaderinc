
// Project surface palette and fog uniforms, added to Terrain3D's generated shader.
uniform sampler2D trial_grass : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D trial_explored : filter_linear, repeat_disable;
uniform vec2 trial_player;
uniform float trial_radius = 18.0;
uniform vec3 trial_memory : source_color;
uniform vec4 trial_mines[5];

