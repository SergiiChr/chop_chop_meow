
    // Keep the existing mine tunnel openings beneath their separately modelled rock shells.
    for (int mine = 0; mine < 5; mine++) {
        vec2 offset = v_vertex.xz - trial_mines[mine].xy;
        vec2 face = trial_mines[mine].zw;
        float depth = dot(offset, face);
        if (v_vertex.y < 1.83 && abs(dot(offset, vec2(face.y, -face.x))) < 0.77 && depth > -5.5 && depth < -2.15) discard;
    }
    // Terrain3D supplies height, clipmap LOD, terrain normals and UVs. The project supplies its palette.
    vec3 meadow = texture(trial_grass, v_vertex.xz / 9.0 + vec2(0.5)).rgb;
    // Soft patches of lusher and drier grass; the same noise read transposed keeps the two independent.
    float lush = smoothstep(0.4, 0.65, texture(trial_patches, v_vertex.xz / 216.0).r);
    float dry = smoothstep(0.58, 0.75, texture(trial_patches, v_vertex.zx / 216.0).r);
    meadow = mix(meadow * 1.08, meadow * vec3(0.72, 0.84, 0.68), lush);
    meadow = mix(meadow, vec3(0.32, 0.27, 0.1), dry * 0.45);
    float grain = sin(v_vertex.x * 0.57 + sin(v_vertex.z * 0.31)) * sin(v_vertex.z * 0.63) * 0.5 + 0.5;
    vec3 stone = mix(vec3(0.075, 0.08, 0.065), vec3(0.15, 0.15, 0.13), grain);
    float bare = max(smoothstep(18.0, 35.0, v_vertex.y), 1.0 - smoothstep(0.45, 0.82, w_normal.y));
    ALBEDO = mix(meadow, stone, bare);
    ALBEDO = mix(ALBEDO, vec3(0.24, 0.25, 0.23), smoothstep(38.0, 53.0, v_vertex.y) * 0.55);
    // Keep main's worn building footprints on the native valley floor.
    float wear = texture(trial_disturbance, v_vertex.xz / 216.0).r;
    // Footpaths worn by walking.
    wear = max(wear, smoothstep(0.05, 0.6, texture(trial_traffic, v_vertex.xz / 216.0).r));
    ALBEDO = mix(ALBEDO, vec3(0.23, 0.205, 0.14), wear * 0.45 * (1.0 - bare));
    // Scenery shrinks away on worn ground, and its shade with it.
    float scenery = texture(trial_shade, v_vertex.xz / 216.0).r;
    vec2 contact = texture(trial_contact, v_vertex.xz / 216.0).rg;
    ALBEDO = mix(ALBEDO, vec3(0.2, 0.17, 0.11), contact.g * 0.6 * (1.0 - bare));
    ALBEDO *= 1.0 - min(scenery * (1.0 - wear) * 0.6 + contact.r * 0.6, 0.65) * (1.0 - bare);
    ROUGHNESS = 1.0;
    SPECULAR = 0.12;
    NORMAL_MAP_DEPTH = 0.0;
    AO = 1.0;

