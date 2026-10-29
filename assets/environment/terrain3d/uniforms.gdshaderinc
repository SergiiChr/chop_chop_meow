
// Project surface palette and footpath uniforms, added to Terrain3D's generated shader.
uniform sampler2D trial_grass : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D trial_patches : filter_linear, repeat_disable;
uniform sampler2D trial_disturbance : filter_linear, repeat_disable;
uniform sampler2D trial_traffic : filter_linear, repeat_disable;
// Baked shade under scenery, and under world objects: red for contact shade, green for bare soil around rocks.
uniform sampler2D trial_shade : filter_linear, repeat_disable;
uniform sampler2D trial_contact : filter_linear, repeat_disable;
uniform vec4 trial_mines[5];

