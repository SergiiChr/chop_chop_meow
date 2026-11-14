
    // Keep the existing mine tunnel openings beneath their separately modelled rock shells.
    for (int mine = 0; mine < 5; mine++) {
        vec2 offset = v_vertex.xz - mines[mine].xy;
        vec2 face = mines[mine].zw;
        float depth = dot(offset, face);
        if (v_vertex.y < 1.83 && abs(dot(offset, vec2(face.y, -face.x))) < 0.77 && depth > -5.5 && depth < -2.15) discard;
    }
    // Terrain3D supplies height, clipmap LOD, terrain normals and the grass and rock photoscans blended by slope.
    // The project supplies the colours; the photoscans add only their grain, as brightness around their mean, and their bump.
    const float DETAIL = 0.4;
    // tools/pack_textures.gd scales every photoscan to this mean linear brightness, so any blend of them has it too.
    const float MEAN = 0.14;
    const vec3 LUMA = vec3(0.3, 0.59, 0.11);
    vec3 meadow = texture(grass, v_vertex.xz / 9.0 + vec2(0.5)).rgb;
    // Soft patches of lusher and drier grass; the same noise read transposed keeps the two independent.
    float lush = smoothstep(0.4, 0.65, texture(patches, v_vertex.xz / 216.0).r);
    float dry = smoothstep(0.58, 0.75, texture(patches, v_vertex.zx / 216.0).r);
    meadow = mix(meadow * 1.08, meadow * vec3(0.72, 0.84, 0.68), lush);
    meadow = mix(meadow, vec3(0.32, 0.27, 0.1), dry * 0.45);
    float grain = sin(v_vertex.x * 0.57 + sin(v_vertex.z * 0.31)) * sin(v_vertex.z * 0.63) * 0.5 + 0.5;
    vec3 stone = mix(vec3(0.075, 0.08, 0.065), vec3(0.15, 0.15, 0.13), grain);
    float bare = max(smoothstep(18.0, 35.0, v_vertex.y), 1.0 - smoothstep(0.45, 0.82, w_normal.y));
    vec3 palette = mix(meadow, stone, bare);
    palette = mix(palette, vec3(0.24, 0.25, 0.23), smoothstep(38.0, 53.0, v_vertex.y) * 0.55);
    float detail = dot(ALBEDO, LUMA) / MEAN;
    ALBEDO = palette * mix(1.0, detail, DETAIL);
    // The ground photoscans below lie flat in world x and z, with their height along world up.
    vec3 view = normalize((INV_VIEW_MATRIX * vec4(VIEW, 0.0)).xzy);
    // Sparse broad patches of the meadow take the moss photoscan as it is: the patch noise read at a larger scale and an
    // offset, independent of the lusher and drier patches, with edges reaching further out at the photoscan's moss.
    float mossy = texture(patches, v_vertex.xz / 300.0 + vec2(0.13, 0.21)).r;
    // The edge below can lower the patch value by at most 0.04, so below 0.56 the moss weighs nothing.
    if (mossy > 0.56 && bare < 1.0) {
        vec2 moss_uv = scan_parallax(moss_height, v_vertex.xz / moss_tile, view, scan_relief / moss_tile);
        float edge = mossy + (texture(moss_height, moss_uv).r - 0.5) * 0.08;
        float moss = smoothstep(0.6, 0.68, edge) * (1.0 - bare);
        ALBEDO = mix(ALBEDO, texture(moss_albedo, moss_uv).rgb, moss);
        NORMAL_MAP = mix(NORMAL_MAP, texture(moss_normal, moss_uv).rgb, moss);
    }
    // The woods along the mountains take the forest floor photoscan as it is, its edge wandering in broad patches and
    // reaching further out at its litter.
    float woods = texture(woods_distance, (v_vertex.xz - woods_corner) / woods_span).r * woods_reach;
    if (woods < woods_reach && bare < 1.0) {
        vec2 forest_uv = scan_parallax(forest_height, v_vertex.xz / forest_tile, view, scan_relief / forest_tile);
        float wander = sin(v_vertex.x * 0.31 + v_vertex.z * 0.17) * sin(v_vertex.z * 0.27 - v_vertex.x * 0.13);
        float edge = woods + (0.5 - texture(forest_height, forest_uv).r) * 3.0 + wander * 2.0;
        float litter = (1.0 - smoothstep(woods_band - 2.0, woods_band + 2.0, edge)) * (1.0 - bare);
        ALBEDO = mix(ALBEDO, texture(forest_albedo, forest_uv).rgb, litter);
        NORMAL_MAP = mix(NORMAL_MAP, texture(forest_normal, forest_uv).rgb, litter);
    }
    // Worn building footprints and footpaths worn by walking show the footpath photoscan through the grass.
    float wear = texture(disturbance, v_vertex.xz / 216.0).r;
    wear = max(wear, smoothstep(0.05, 0.6, texture(traffic, v_vertex.xz / 216.0).r)) * (1.0 - bare);
    if (wear > 0.0) {
        vec2 path_uv = scan_parallax(path_height, v_vertex.xz / path_tile, view, scan_relief / path_tile);
        // The path shows first where it stands higher than the grass, so the edge follows the texture.
        float worn = clamp(wear + (texture(path_height, path_uv).r - mat.albedo_height.a) * wear, 0.0, 1.0);
        ALBEDO = mix(ALBEDO, texture(path_albedo, path_uv).rgb, worn);
        NORMAL_MAP = mix(NORMAL_MAP, texture(path_normal, path_uv).rgb, worn);
    }
    // Scenery shrinks away on worn ground, and its shade with it.
    float scenery = texture(shade, v_vertex.xz / 216.0).r;
    vec2 objectShade = texture(contact, v_vertex.xz / 216.0).rg;
    ALBEDO = mix(ALBEDO, vec3(0.2, 0.17, 0.11), objectShade.g * 0.6 * (1.0 - bare));
    ALBEDO *= 1.0 - min(scenery * (1.0 - wear) * 0.6 + objectShade.r * 0.6, 0.65) * (1.0 - bare);
    // Matte ground: a low sun turns the photoscans' roughness into a sheen that washes out the meadow.
    ROUGHNESS = 1.0;
    SPECULAR = 0.12;
    NORMAL_MAP_DEPTH = 0.5;
