using Godot;

namespace ChopChopMeow;

/// <summary>Dark patches on the bare earth or tilled rows spread as the plot's water reserve fills.</summary>
internal sealed class FarmSoil {
    private static readonly StringName WaterFraction = "water_fraction";
    private static readonly Shader WetEarth = new() { Code = """
        shader_type spatial;
        render_mode blend_mul, unshaded, fog_disabled, depth_draw_never, shadows_disabled;
        uniform float water_fraction : hint_range(0.0, 1.0);
        varying vec2 ground;
        varying float ridge;

        void vertex() {
            ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
            ridge = smoothstep(0.03, 0.08, VERTEX.y);
        }

        void fragment() {
            // Distort the edges and spacing of the damp spots in meters so adjoining plots differ.
            vec2 p = ground * 0.47;
            p += 0.22 * sin(p.yx * 2.7 + vec2(1.4, 0.6))
               + 0.08 * sin(p.yx * 7.9 + p.xy * 1.6);
            float distance_to_center = length(fract(p) - 0.5);
            float radius = sqrt(water_fraction) * mix(0.56, 0.63, pow(water_fraction, 4.0));
            float patch = 1.0 - smoothstep(radius - 0.17, radius + 0.13, distance_to_center);
            // The ridge crests stay lighter than the deeper, damp soil between the rows.
            float damp = patch * (0.96 - 0.36 * distance_to_center) * (1.0 - 0.20 * ridge);
            ALBEDO = mix(vec3(1.0), vec3(0.46, 0.42, 0.38), damp);
            ALPHA = 1.0;
        }
        """ };
    private readonly ShaderMaterial water = new() { Shader = WetEarth, RenderPriority = -1 };
    private readonly MeshInstance3D pad, rows;
    private readonly StandardMaterial3D dampPad, dampRows;
    private int percent;
    private bool tilled;

    public FarmSoil(MeshInstance3D pad, MeshInstance3D rows) {
        this.pad = pad;
        this.rows = rows;
        dampPad = (StandardMaterial3D)PileShape.Ground.Duplicate();
        dampRows = (StandardMaterial3D)FarmShape.Earth.Duplicate();
        dampPad.NextPass = dampRows.NextPass = water;
    }

    public void Show(float liters, bool hasRows) {
        int next = Mathf.Clamp(Mathf.RoundToInt(liters / Farms.LitersPerDay * 100), 0, 100);
        if (next == percent && hasRows == tilled) return;
        if (next > 0 && next != percent) water.SetShaderParameter(WaterFraction, next / 100f);
        if ((next > 0) != (percent > 0) || hasRows != tilled) {
            pad.MaterialOverride = next > 0 && !hasRows ? dampPad : PileShape.Ground;
            rows.MaterialOverride = next > 0 && hasRows ? dampRows : FarmShape.Earth;
        }
        (percent, tilled) = (next, hasRows);
    }
}
