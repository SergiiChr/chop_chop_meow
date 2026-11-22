shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never;
instance uniform float spray_time;
varying flat float airborne;
varying flat float age;
varying flat float seed;
varying flat float flight;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(35.1, 73.9))) * 1637.4);
}

void vertex() {
    airborne = COLOR.r;
    vec2 head = UV2;
    vec2 radial = VERTEX.xz - head;
    float clock = spray_time * .96 + hash(head + MODEL_MATRIX[3].xz);
    age = fract(clock);
    seed = hash(head + floor(clock));
    flight = 0.0;
    vec2 facing = vec2(cos(TAU * seed), sin(TAU * seed));
    vec2 direction = facing * radial.x + vec2(-facing.y, facing.x) * radial.y;
    vec3 at = VERTEX;
    if (airborne < .5) {
        float lift = sin(PI * clamp(age / .63, 0.0, 1.0));
        at.xz = head + direction * (.34 + .81 * smoothstep(.01, .41, age));
        at.y += length(UV) * (.025 + .31 * lift) - .12 * smoothstep(.24, .61, age);
    } else {
        seed = hash(radial + vec2(seed, 3.1));
        float speed = hash(radial.yx + seed);
        flight = clamp((age - .035 - .09 * seed) / (.64 + .19 * speed), 0.0, 1.0);
        at.xz = head + direction * flight * (.53 + .41 * speed);
        at.y = mix(VERTEX.y, .065, flight) + 4.0 * flight * (1.0 - flight) * (.43 + .55 * seed);
    }
    vec4 view = MODELVIEW_MATRIX * vec4(at, 1.0);
    if (airborne > .5) {
        view.xy += UV * (.115 + .050 * seed) * (.60 + .40 * sin(PI * flight));
    }
    POSITION = PROJECTION_MATRIX * view;
}

void fragment() {
    vec3 blue = vec3(.048, .39, .66);
    vec3 glint = vec3(.82, .96, 1.0);
    float alpha;
    vec3 water;
    if (airborne < .5) {
        float r = length(UV);
        float angle = atan(UV.y, UV.x + .00001);
        float lobe = .5 + .5 * cos(6.0 * angle + 1.3 * sin(3.0 * angle + seed) + TAU * seed);
        float rim = .22 + .72 * lobe * lobe;
        float edge = 1.0 - smoothstep(.02, .15, abs(r - rim));
        alpha = smoothstep(0.0, .07, age) * (1.0 - smoothstep(.23, .54, age))
            * (1.0 - smoothstep(rim - .07, rim + .04, r)) * (.14 + .26 * edge + .09 * lobe);
        water = mix(blue, glint, .09 + .80 * max(edge, 1.0 - smoothstep(.06, .27, r)));
    } else {
        // A bright leading bead and two smaller drops separate as the cluster flies.
        vec2 p = UV;
        float spread = .34 + .52 * flight;
        float bead = length(p - vec2(.12, .20)) - (.18 + .10 * seed);
        float chip = length(p - vec2(-.54, -.35) * spread) - .15;
        float speck = length(p - vec2(.58, -.61) * spread) - .10;
        float dist = min(bead, min(chip, speck));
        float width = clamp(fwidth(dist), .018, .20);
        float shape = 1.0 - smoothstep(-width, width, dist);
        alpha = shape * (.56 + .21 * seed) * smoothstep(0.0, .07, flight)
            * (1.0 - smoothstep(.85, 1.0, flight));
        water = mix(blue, glint, clamp(.56 + .8 * p.y + .32 * seed, .25, 1.0));
    }
    ALBEDO = water;
    ALPHA = alpha;
}
