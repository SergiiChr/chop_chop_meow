using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Plant clearing texture and materials, refreshed only when the world changes.</summary>
public partial class WorldView {
    private readonly byte[] disturbed = new byte[Terrain.Size * Terrain.Size];
    private Image disturbanceImage = null!;
    private ImageTexture disturbanceTexture = null!;
    private ShaderMaterial dressingMaterial = null!, solidDressingMaterial = null!, dressingFog = null!;

    /// <summary>Plants shrink away on worn ground; wear is read at each plant's origin so the whole plant scales evenly.</summary>
    private static readonly string DressingShader = $$"""
        shader_type spatial;
        render_mode cull_disabled;
        uniform sampler2D trial_disturbance : filter_linear, repeat_disable;
        varying vec3 tint;
        void vertex() {
            float wear = texture(trial_disturbance, MODEL_MATRIX[3].xz / {{Terrain.Size}}.0).r;
            VERTEX *= 1.0 - smoothstep(0.12, 0.65, wear);
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
        dressingMaterial.SetShaderParameter("trial_disturbance", disturbanceTexture);
        // Closed shapes such as shrubs keep back faces culled, or their unlit insides show where they meet a slope.
        solidDressingMaterial = new ShaderMaterial { Shader = new Shader { Code = DressingShader.Replace("cull_disabled", "cull_back") } };
        solidDressingMaterial.SetShaderParameter("trial_disturbance", disturbanceTexture);
        // The fog pass must shrink the same vertices, or cleared shrubs leave silhouettes in remembered areas.
        string shrink = $$"""
            uniform sampler2D trial_disturbance : filter_linear, repeat_disable;
            void vertex() {
                float wear = texture(trial_disturbance, MODEL_MATRIX[3].xz / {{Terrain.Size}}.0).r;
                VERTEX *= 1.0 - smoothstep(0.12, 0.65, wear);
            }
            void fragment()
            """;
        dressingFog = new ShaderMaterial { Shader = new Shader { Code = FogShader.Replace("void fragment()", shrink) } };
        dressingFog.SetShaderParameter("trial_disturbance", disturbanceTexture);
        dressingFog.SetShaderParameter("explored", exploredTexture);
        dressingFog.SetShaderParameter("map_size", World.MapSize);
    }

    /// <summary>A soft footprint in a reusable map; max blending keeps overlapping work areas continuous.</summary>
    private static void Stamp(byte[] map, Vector2 at, float radius, float strength) {
        int left = Math.Max(0, (int)(at.X - radius)), right = Math.Min(Terrain.Size - 1, (int)(at.X + radius) + 1);
        int top = Math.Max(0, (int)(at.Y - radius)), bottom = Math.Min(Terrain.Size - 1, (int)(at.Y + radius) + 1);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++) {
                float distance = new Vector2(x + .5f, y + .5f).DistanceTo(at) / radius;
                float edge = Math.Clamp((1 - distance) * 2.4f, 0, 1);
                byte value = (byte)(edge * edge * (3 - 2 * edge) * strength * 255);
                int index = y * Terrain.Size + x;
                map[index] = Math.Max(map[index], value);
            }
    }

    /// <summary>Called only when loading or things are added/removed, never for ordinary frames, jobs or panel refreshes.</summary>
    private void RefreshDressing() {
        if (disturbanceTexture == null) return;
        Array.Clear(disturbed);
        Stamp(disturbed, World.Spawn, 7.5f, .8f);
        float across = Terrain.Across(World.Spawn);
        for (float along = Terrain.Along(World.Spawn); along < Terrain.Coast(across); along += .8f) {
            var at = Terrain.Point(along, across + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f);
            Stamp(disturbed, at, 1.25f, .85f);
        }
        foreach (var cliff in World.Cliffs) {
            var at = World.MineSpot(cliff); var outwards = World.MineOut(cliff);
            for (float d = -3; d < 2; d += .75f) Stamp(disturbed, at + outwards * d, 2.6f, .95f);
        }
        foreach (var t in World.S.Things) {
            if (t.IsResource || t.Kind is "crate" or "grave") continue;
            float radius = t.Kind switch { "hut" or "pile" or "farm" => 3.4f, "mine" => 3, "campfire" => 2.3f, _ => 1.8f };
            Stamp(disturbed, t.Pos, radius, 1);
        }
        disturbanceImage.SetData(Terrain.Size, Terrain.Size, false, Image.Format.R8, disturbed);
        disturbanceTexture.Update(disturbanceImage);
    }
}
