using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Tree3D 1.1.0 meshes baked offline by tools/tree3d.py, shared across the forest.</summary>
internal static class Tree3DShape {
    private static readonly Mesh?[,] trunks = new Mesh?[4, 3], leaves = new Mesh?[4, 3];
    private static readonly StandardMaterial3D?[] bark = new StandardMaterial3D?[4];
    private static readonly ShaderMaterial?[] foliage = new ShaderMaterial?[4], cores = new ShaderMaterial?[4];
    private static readonly string[] species = { "oak", "spruce", "birch", "bush" };
    private const string Folder = "res://assets/models/tree3d/";
    public static readonly StringName Opacity = "opacity";

    /// <summary>
    /// Leaf cards that sway in the wind, more toward the crown.
    /// Cut out like the alpha-scissor material they replace; the fading twin blends instead, so a canopy in front of the player keeps swaying while it fades.
    /// The solid variant draws whole cards and is only used for shadows.
    /// </summary>
    private static string FoliageCode(string alpha, string mode = "") => $$"""
        shader_type spatial;
        render_mode cull_disabled{{mode}};
        uniform sampler2D albedo_texture : source_color, filter_linear_mipmap;
        uniform vec4 albedo : source_color = vec4(1.0);
        uniform float opacity = 1.0;
        {{Wind.ShaderCode}}
        void vertex() {
            vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            float bend = clamp(VERTEX.y / 4.9, 0.0, 1.0);
            VERTEX += to_local(MODEL_MATRIX, wind_offset(world, bend * bend, 0.3));
        }
        void fragment() {
            vec4 color = texture(albedo_texture, UV) * albedo;
            ALBEDO = color.rgb;
            ROUGHNESS = 0.95;
            {{alpha}}
        }
        """;
    public static readonly Shader Foliage = new() { Code = FoliageCode("ALPHA = color.a; ALPHA_SCISSOR_THRESHOLD = 0.45;") },
        FadingFoliage = new() { Code = FoliageCode("ALPHA = color.a * opacity;", ", depth_draw_never") },
        SolidFoliage = new() { Code = FoliageCode("") };
    /// <summary>How far toward the canopy's middle the solid shadow core is drawn, so leafy edges stay cut out.</summary>
    private const float CoreScale = .75f;

    private static Mesh Trunk(int kind, int variant) => trunks[kind, variant] ??= GD.Load<Mesh>($"{Folder}{species[kind]}{variant}_trunk.res");

    private static readonly (Vector2 Center, float Radius, float Height)?[,] starts = new (Vector2, float, float)?[4, 3];
    /// <summary>
    /// Where a trunk's model starts: the middle and radius of the ring of points at its lower end, and how far above the ground that ring is.
    /// A trunk's mesh starts with that ring.
    /// The bake stands a tree's lowest point on the ground, which on some trees is a hanging branch, so their trunks start higher.
    /// </summary>
    private static (Vector2 Center, float Radius, float Height) Start(int kind, int variant) {
        if (starts[kind, variant] is { } known) return known;
        var points = Trunk(kind, variant).SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector2 low = Vector2.Inf, high = -Vector2.Inf;
        for (int i = 0; i < points.Length && Math.Abs(points[i].Y - points[0].Y) < .01f; i++) {
            var flat = new Vector2(points[i].X, points[i].Z);
            low = low.Min(flat); high = high.Max(flat);
        }
        var start = ((low + high) / 2, Math.Max(high.X - low.X, high.Y - low.Y) / 2, points[0].Y);
        starts[kind, variant] = start;
        return start;
    }

    /// <summary>How much thicker a trunk is at the ground for every metre its model starts above it.</summary>
    private const float Flare = .25f;
    /// <summary>Where a trunk stands in its model and how thick it is at the ground.</summary>
    public static (Vector2 Center, float Radius) Foot(int kind, int variant) {
        var (center, radius, height) = Start(kind, variant);
        return (center, radius * (1 + height * Flare));
    }

    private static readonly Mesh?[,] stems = new Mesh?[4, 3];
    private static readonly StandardMaterial3D?[] stemBark = new StandardMaterial3D?[4];
    /// <summary>How far a stem reaches up into the trunk it carries down, so no gap shows between them.</summary>
    private const float StemOverlap = .05f;

    public static Mesh Add(Node3D parent, int kind, int variant) {
        Trunk(kind, variant);
        leaves[kind, variant] ??= GD.Load<Mesh>($"{Folder}{species[kind]}{variant}_leaves.res");
        bark[kind] ??= new StandardMaterial3D {
            AlbedoTexture = GD.Load<Texture2D>(Folder + (kind == 2 ? "birch_bark.svg" : "bark.svg")),
            CullMode = BaseMaterial3D.CullModeEnum.Front,
            Roughness = .95f
        };
        if (foliage[kind] == null) {
            var material = new ShaderMaterial { Shader = Foliage };
            material.SetShaderParameter("albedo_texture", GD.Load<Texture2D>(Folder + (kind == 1 ? "needles.svg" : "broadleaf.svg")));
            material.SetShaderParameter("albedo", kind == 2 ? new Color("c5d690") : Colors.White);
            foliage[kind] = material;
        }
        // A trunk whose model starts above the ground is carried down to it by a stem that widens toward its foot.
        var (center, radius, height) = Start(kind, variant);
        if (height > .03f) {
            float tall = height + StemOverlap;
            stems[kind, variant] ??= new CylinderMesh { TopRadius = radius, BottomRadius = Foot(kind, variant).Radius, Height = tall, RadialSegments = 6, Rings = 1, CapTop = false, CapBottom = false };
            // The baked trunks are wound inside out and drawn by their back faces; a built cylinder is not.
            stemBark[kind] ??= new StandardMaterial3D { AlbedoTexture = bark[kind]!.AlbedoTexture, Roughness = .95f };
            ModelKit.Add(parent, stems[kind, variant]!, stemBark[kind]!, new Vector3(center.X, tall / 2, center.Y));
        }
        ModelKit.Add(parent, trunks[kind, variant]!, bark[kind]!, Vector3.Zero);
        ModelKit.Add(parent, leaves[kind, variant]!, foliage[kind]!, Vector3.Zero);
        return leaves[kind, variant]!;
    }

    /// <summary>
    /// A shadow-only copy of the leaf cards drawn whole and shrunk toward the canopy's middle.
    /// It fills the gaps between cut-out leaves, so a tree's shadow is solid inside and leafy only at its edge.
    /// </summary>
    public static void AddShadowCore(Node3D parent, int kind, Mesh leafMesh) {
        cores[kind] ??= new ShaderMaterial { Shader = SolidFoliage };
        var middle = leafMesh.GetAabb().GetCenter();
        ModelKit.Add(parent, leafMesh, cores[kind]!, middle * (1 - CoreScale), null, Vector3.One * CoreScale).CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
    }
}
