
// Project surface palette and footpath uniforms, added to Terrain3D's generated shader.
uniform sampler2D grass : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D patches : filter_linear, repeat_disable;
uniform sampler2D disturbance : filter_linear, repeat_disable;
uniform sampler2D traffic : filter_linear, repeat_disable;
// Baked shade under scenery, and under world objects: red for contact shade, green for bare soil around rocks.
uniform sampler2D shade : filter_linear, repeat_disable;
uniform sampler2D contact : filter_linear, repeat_disable;
uniform vec4 mines[5];

