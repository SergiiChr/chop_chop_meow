
    // Keep the existing mine tunnel openings beneath their separately modelled rock shells.
    for (int mine = 0; mine < 5; mine++) {
        vec2 offset = v_vertex.xz - mines[mine].xy;
        vec2 face = mines[mine].zw;
        float depth = dot(offset, face);
        if (v_vertex.y < 1.83 && abs(dot(offset, vec2(face.y, -face.x))) < 0.77 && depth > -5.5 && depth < -2.15) discard;
    }
    // Terrain3D supplies height, clipmap LOD, terrain normals and the grass and rock photoscans blended by slope.
    // On the meadow the project supplies the colours; the photoscans add only their grain, as brightness around their
    // mean, and their bump.
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
    // High and steep ground, where no paths wear and no scenery shades.
    float bare = max(smoothstep(18.0, 35.0, v_vertex.y), 1.0 - smoothstep(0.45, 0.82, w_normal.y));
    float detail = dot(ALBEDO, LUMA) / MEAN;
    ALBEDO = meadow * mix(1.0, detail, DETAIL);
    // The mountains and a band along their foot take the mountain photoscan as it is, laid from above and from either
    // side and blended by where the ground faces, so steep faces show it unstretched.
    float foot = texture(mountain_foot, (v_vertex.xz - foot_corner) / foot_span).r * mountain_reach;
    if (foot < mountain_reach) {
        vec3 ground = normalize(w_normal);
        vec3 eye = normalize((INV_VIEW_MATRIX * vec4(VIEW, 0.0)).xyz);
        vec3 facing = pow(abs(ground), vec3(4.0));
        facing /= facing.x + facing.y + facing.z;
        vec3 rock = vec3(0.0), bump = vec3(0.0);
        float rise = 0.0;
        // Each side lays the photoscan along two world axes, and its view and ground normal are taken along the same
        // two and out from it.
        if (facing.x > 0.01) {
            vec2 uv = mountain_parallax(v_vertex.zy / mountain_tile, vec3(eye.zy, eye.x * sign(ground.x)));
            rock += texture(mountain_albedo, uv).rgb * facing.x;
            rise += texture(mountain_height, uv).r * facing.x;
            bump += mountain_bump(uv, ground.zyx).zyx * facing.x;
        }
        if (facing.y > 0.01) {
            vec2 uv = mountain_parallax(v_vertex.xz / mountain_tile, vec3(eye.xz, eye.y * sign(ground.y)));
            rock += texture(mountain_albedo, uv).rgb * facing.y;
            rise += texture(mountain_height, uv).r * facing.y;
            bump += mountain_bump(uv, ground.xzy).xzy * facing.y;
        }
        if (facing.z > 0.01) {
            vec2 uv = mountain_parallax(v_vertex.xy / mountain_tile, vec3(eye.xy, eye.z * sign(ground.z)));
            rock += texture(mountain_albedo, uv).rgb * facing.z;
            rise += texture(mountain_height, uv).r * facing.z;
            bump += mountain_bump(uv, ground).xyz * facing.z;
        }
        // Toward the meadow the band's edge wanders with broad patches and reaches further out at the stones.
        float wander = sin(v_vertex.x * 0.45 + v_vertex.z * 0.21) * sin(v_vertex.z * 0.37 - v_vertex.x * 0.18);
        float edge = foot + (0.5 - rise) * 3.0 + wander * 1.5;
        float rocky = 1.0 - smoothstep(mountain_band - 1.5, mountain_band + 1.5, edge);
        ALBEDO = mix(ALBEDO, rock, rocky);
        // The blended normal, in world space, goes into the ground's own normal map frame.
        vec3 seen = mat3(VIEW_MATRIX) * normalize(bump);
        vec3 framed = vec3(dot(seen, TANGENT), dot(seen, BINORMAL), dot(seen, NORMAL));
        NORMAL_MAP = mix(NORMAL_MAP, framed * 0.5 + 0.5, rocky);
    }
    // Worn building footprints and footpaths worn by walking show dirt through the grass.
    float wear = texture(disturbance, v_vertex.xz / 216.0).r;
    wear = max(wear, smoothstep(0.05, 0.6, texture(traffic, v_vertex.xz / 216.0).r)) * (1.0 - bare);
    vec2 dirt_uv = v_vertex.xz * _texture_uv_scale_array[2];
    vec4 dirt = texture(_texture_array_albedo, vec3(dirt_uv, 2.0));
    vec4 dirt_bump = texture(_texture_array_normal, vec3(dirt_uv, 2.0));
    vec3 path = vec3(0.23, 0.205, 0.14) * mix(1.0, dot(dirt.rgb, LUMA) / MEAN, DETAIL);
    // The dirt shows first where it stands higher than the grass, so the edge follows the texture.
    float worn = clamp(wear + (dirt.a - mat.albedo_height.a) * wear, 0.0, 1.0);
    ALBEDO = mix(ALBEDO, path, worn);
    NORMAL_MAP = mix(NORMAL_MAP, dirt_bump.rgb, worn);
    // Scenery shrinks away on worn ground, and its shade with it.
    float scenery = texture(shade, v_vertex.xz / 216.0).r;
    vec2 objectShade = texture(contact, v_vertex.xz / 216.0).rg;
    ALBEDO = mix(ALBEDO, vec3(0.2, 0.17, 0.11), objectShade.g * 0.6 * (1.0 - bare));
    ALBEDO *= 1.0 - min(scenery * (1.0 - wear) * 0.6 + objectShade.r * 0.6, 0.65) * (1.0 - bare);
    // Matte ground: a low sun turns the photoscans' roughness into a sheen that washes out the meadow.
    ROUGHNESS = 1.0;
    SPECULAR = 0.12;
    NORMAL_MAP_DEPTH = 0.5;
