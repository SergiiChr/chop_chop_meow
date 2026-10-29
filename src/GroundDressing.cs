using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Plant clearing texture and materials, refreshed only when the world changes.</summary>
public partial class WorldView {
    private readonly byte[] disturbed = new byte[Terrain.Size * Terrain.Size];
    private Image disturbanceImage = null!;
    private ImageTexture disturbanceTexture = null!;
    private ShaderMaterial dressingMaterial = null!, solidDressingMaterial = null!, stillDressingMaterial = null!;

    // Baked ground shade: four texels a tile under scenery, two under world objects so restamping them stays cheap.
    // Scenery shade is baked once while loading and fades where worn ground shrinks the plants.
    // Objects come and go, so their map is restamped with the disturbance map: red right under them, green for bare soil around rocks.
    private const int ShadeScale = 4, ShadeSize = Terrain.Size * ShadeScale, ContactScale = 2, ContactSize = Terrain.Size * ContactScale;
    private readonly byte[] sceneryShade = new byte[ShadeSize * ShadeSize], objectShade = new byte[ContactSize * ContactSize * 2];
    private Image sceneryShadeImage = null!, objectShadeImage = null!;
    private ImageTexture sceneryShadeTexture = null!, objectShadeTexture = null!;
    private bool sceneryShadeBaked;

    /// <summary>
    /// Plants shrink away on worn ground; wear is read at each plant's origin so the whole plant scales evenly.
    /// Plants sway in the wind and part around the player; pebbles and twigs use the still variant.
    /// </summary>
    private static readonly string DressingShader = $$"""
        shader_type spatial;
        render_mode cull_disabled;
        uniform sampler2D disturbance : filter_linear, repeat_disable;
        // Footpaths worn by walking.
        uniform sampler2D traffic : filter_linear, repeat_disable;
        varying vec3 tint;
        {{Wind.ShaderCode}}
        void vertex() {
            vec2 uv = MODEL_MATRIX[3].xz / {{Terrain.Size}}.0;
            float wear = max(texture(disturbance, uv).r, texture(traffic, uv).r);
            VERTEX *= 1.0 - smoothstep(0.12, 0.65, wear);
            #ifndef STILL
            vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            float bend = clamp(VERTEX.y / 0.3, 0.0, 1.0);
            VERTEX += to_local(MODEL_MATRIX, wind_offset(world, bend, 0.12) + push_offset(world, bend));
            #endif
            // The same sRGB to linear conversion StandardMaterial3D applies to sRGB vertex colors.
            tint = COLOR.rgb;
            if (!OUTPUT_IS_SRGB) {
                tint = mix(pow((tint + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), tint * (1.0 / 12.92), lessThan(tint, vec3(0.04045)));
            }
        }
        void fragment() {
            ALBEDO = tint;
            ROUGHNESS = 1.0;
        }
        """;

    private void InitializeDressing() {
        disturbanceImage = Image.CreateFromData(Terrain.Size, Terrain.Size, false, Image.Format.R8, disturbed);
        disturbanceTexture = ImageTexture.CreateFromImage(disturbanceImage);
        dressingMaterial = new ShaderMaterial { Shader = new Shader { Code = DressingShader } };
        dressingMaterial.SetShaderParameter("disturbance", disturbanceTexture);
        // Closed shapes such as shrubs keep back faces culled, or their unlit insides show where they meet a slope.
        solidDressingMaterial = new ShaderMaterial { Shader = new Shader { Code = DressingShader.Replace("cull_disabled", "cull_back") } };
        solidDressingMaterial.SetShaderParameter("disturbance", disturbanceTexture);
        stillDressingMaterial = new ShaderMaterial { Shader = new Shader { Code = DressingShader.Replace("cull_disabled;", "cull_back;\n#define STILL") } };
        stillDressingMaterial.SetShaderParameter("disturbance", disturbanceTexture);
        sceneryShadeImage = Image.CreateFromData(ShadeSize, ShadeSize, false, Image.Format.R8, sceneryShade);
        sceneryShadeTexture = ImageTexture.CreateFromImage(sceneryShadeImage);
        objectShadeImage = Image.CreateFromData(ContactSize, ContactSize, false, Image.Format.Rg8, objectShade);
        objectShadeTexture = ImageTexture.CreateFromImage(objectShadeImage);
    }

    /// <summary>
    /// A soft footprint in a reusable map of <paramref name="scale"/> texels a tile, written to one of its interleaved channels.
    /// Max blending keeps overlapping work areas continuous; adding lets a clump of plants shade the ground more than one.
    /// </summary>
    private static void Stamp(byte[] map, Vector2 at, float radius, float strength, int scale = 1, int channels = 1, int channel = 0, bool add = false) {
        int size = Terrain.Size * scale;
        at *= scale; radius *= scale;
        int left = Math.Max(0, (int)(at.X - radius)), right = Math.Min(size - 1, (int)(at.X + radius) + 1);
        int top = Math.Max(0, (int)(at.Y - radius)), bottom = Math.Min(size - 1, (int)(at.Y + radius) + 1);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++) {
                float distance = new Vector2(x + .5f, y + .5f).DistanceTo(at) / radius;
                float edge = Math.Clamp((1 - distance) * 2.4f, 0, 1);
                int value = (int)(edge * edge * (3 - 2 * edge) * strength * 255);
                int index = (y * size + x) * channels + channel;
                map[index] = (byte)(add ? Math.Min(255, map[index] + value) : Math.Max(map[index], value));
            }
    }

    private void ShadeScenery(Vector2 at, float radius, float strength) => Stamp(sceneryShade, at, radius, strength, ShadeScale, add: true);
    private void ShadeObject(Vector2 at, float radius, float strength) => Stamp(objectShade, at, radius, strength, ContactScale, 2, 0);
    private void BareSoil(Vector2 at, float radius) => Stamp(objectShade, at, radius, 1, ContactScale, 2, 1);

    /// <summary>Contact shade and bare soil under one world object; trees keep a dark ring at the trunk even as stumps.</summary>
    private void ShadeThing(Thing t) {
        if (t.IsDeposit) { ShadeObject(t.Pos, .9f, .7f); BareSoil(t.Pos, 1.6f); return; }
        switch (t.Kind) {
            case "stone": ShadeObject(t.Pos, .7f, .7f); BareSoil(t.Pos, 1.3f); break;
            case "tree": ShadeObject(t.Pos, .55f, .7f); break;
            case "berries": ShadeObject(t.Pos, .6f, .6f); break;
            case "water" or "leaves" or "wolf_corpse" or "campfire" or "grave" or "signpost" or "mine": break;
            default:
                if (t.IsLoose) ShadeObject(t.Pos, .3f, .6f);
                else if (!World.IsZone(t.Kind)) ShadeObject(t.Pos, World.Footprint(t.Kind) + .25f, .5f);
                break;
        }
    }

    /// <summary>Called only when loading or things are added/removed, never for ordinary frames, jobs or panel refreshes.</summary>
    private void RefreshDressing() {
        if (disturbanceTexture == null) return;
        Array.Clear(disturbed);
        Stamp(disturbed, Terrain.Spawn, 7.5f, .8f);
        float across = Terrain.Across(Terrain.Spawn);
        for (float along = Terrain.Along(Terrain.Spawn); along < Terrain.Coast(across); along += .8f) {
            var at = Terrain.Point(along, across + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f);
            Stamp(disturbed, at, 1.25f, .85f);
        }
        foreach (var cliff in World.Cliffs) {
            var at = World.MineSpot(cliff); var outwards = cliff.Face;
            for (float d = -3; d < 2; d += .75f) Stamp(disturbed, at + outwards * d, 2.6f, .95f);
        }
        foreach (var t in World.S.Things) {
            if (t.IsResource || t.Kind is "crate" or "grave") continue;
            float radius = t.Kind switch { "hut" or "pile" or "farm" => 3.4f, "mine" => 3, "campfire" => 2.3f, _ => 1.8f };
            Stamp(disturbed, t.Pos, radius, 1);
        }
        disturbanceImage.SetData(Terrain.Size, Terrain.Size, false, Image.Format.R8, disturbed);
        disturbanceTexture.Update(disturbanceImage);
        // Scenery is placed before the first refresh and never changes after it.
        if (!sceneryShadeBaked) {
            sceneryShadeBaked = true;
            sceneryShadeImage.SetData(ShadeSize, ShadeSize, false, Image.Format.R8, sceneryShade);
            sceneryShadeTexture.Update(sceneryShadeImage);
        }
        Array.Clear(objectShade);
        foreach (var t in World.S.Things) ShadeThing(t);
        objectShadeImage.SetData(ContactSize, ContactSize, false, Image.Format.Rg8, objectShade);
        objectShadeTexture.Update(objectShadeImage);
    }
}
