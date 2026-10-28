
    // Keep the existing mine tunnel openings beneath their separately modelled rock shells.
    for (int mine = 0; mine < 5; mine++) {
        vec2 offset = v_vertex.xz - trial_mines[mine].xy;
        vec2 face = trial_mines[mine].zw;
        float depth = dot(offset, face);
        if (v_vertex.y < 1.83 && abs(dot(offset, vec2(face.y, -face.x))) < 0.77 && depth > -5.5 && depth < -2.15) discard;
    }
    // Terrain3D supplies height, clipmap LOD, terrain normals and UVs. The project supplies its palette.
    vec3 meadow = texture(trial_grass, v_vertex.xz / 9.0 + vec2(0.5)).rgb;
    float grain = sin(v_vertex.x * 0.57 + sin(v_vertex.z * 0.31)) * sin(v_vertex.z * 0.63) * 0.5 + 0.5;
    vec3 stone = mix(vec3(0.075, 0.08, 0.065), vec3(0.15, 0.15, 0.13), grain);
    float bare = max(smoothstep(18.0, 35.0, v_vertex.y), 1.0 - smoothstep(0.45, 0.82, w_normal.y));
    ALBEDO = mix(meadow, stone, bare);
    ALBEDO = mix(ALBEDO, vec3(0.24, 0.25, 0.23), smoothstep(38.0, 53.0, v_vertex.y) * 0.55);
    ROUGHNESS = 1.0;
    SPECULAR = 0.12;
    NORMAL_MAP_DEPTH = 0.0;
    AO = 1.0;

    // Match the mesh overlay: unseen terrain is black, remembered terrain is muted, live terrain is clear.
    float seen = texture(trial_explored, (v_vertex.xz + vec2(0.5)) / 216.0).r;
    float live = 1.0 - smoothstep(trial_radius - 1.5, trial_radius, distance(v_vertex.xz, trial_player));
    float fog_alpha = (1.0 - live) * mix(1.0, 0.62, seen);
    ALBEDO = mix(ALBEDO, vec3(0.0), fog_alpha);
    EMISSION = trial_memory * seen * fog_alpha;
