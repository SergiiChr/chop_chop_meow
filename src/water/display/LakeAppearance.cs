using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The water surfaces of the lake and the sea, with the waves, refraction, caustics and foam of one shader.</summary>
public static class LakeAppearance {
    private const string ShaderPath = "res://assets/environment/lake/lake.gdshader";
    private const string LilyPadShaderPath = "res://assets/environment/lake/lily_pad.gdshader";
    /// <summary>Seconds the waves show in still screenshots, for comparable pictures.</summary>
    private const float ReviewSeconds = 1.25f;
    private static readonly StringName ReviewTime = "review_time";
    /// <summary>Height of the water's surface at rest over the valley floor.</summary>
    internal const float Level = .025f;
    /// <summary>Columns and rows of vertices in one tile of a large surface, which Godot leaves undrawn while it is off screen.</summary>
    private const int TileVertices = 100;
    private static Shader? shader;
    /// <summary>
    /// Seconds after which the water's flow and caustics come back to where they started: the shader's sampler direction
    /// moves its maps by whole tiles in that time, and its 16 caustic pictures at 14 a second come round whole.
    /// </summary>
    private const double WaterLoop = 800;
    private static readonly StringName WavesUniform = "water_waves", TimeUniform = "water_time";
    /// <summary>Seconds the water has moved for, kept in double precision so it never loses its step however long the game runs.</summary>
    private static double clock;

    /// <summary>
    /// Moves every water surface on by <paramref name="dt"/> seconds: by the game's own clock, so the water stands still
    /// while the game is paused, and with each wave's phase kept below a turn, so nothing jumps however long it runs.
    /// </summary>
    internal static void Advance(float dt) {
        clock += dt;
        // The sea and the lake share their waves' lengths, and so their speeds.
        var waves = WaterBody.Sea.Waves;
        RenderingServer.GlobalShaderParameterSet(WavesUniform, new Vector3(Phase(waves[0].W), Phase(waves[1].W), Phase(waves[2].W)));
        RenderingServer.GlobalShaderParameterSet(TimeUniform, (float)(clock % WaterLoop));
    }

    /// <summary>How far round its turn a wave of <paramref name="length"/> metres is: its crests run at the speed deep water gives that length, slowed to the water's speed.</summary>
    private static float Phase(float length) {
        double k = Math.Tau / length;
        return (float)(Math.Sqrt(Movement.Gravity * k) * WaterBody.Speed * clock % Math.Tau);
    }

    /// <summary>Adds the lake's water surface, which ends at the shore that bounds walking.</summary>
    public static void BuildLakeAppearance(Node3D parent, bool still) {
        float reach = Terrain.LakeRadius * 1.05f;
        var mesh = Grid(Steps(0, Mathf.Tau, WaterBody.Spacing / reach), Steps(0, 1, WaterBody.Spacing / reach), (angle, share) => {
            float shore = Terrain.Shore(angle) * share;
            return new Vector3(MathF.Cos(angle) * shore, 0, MathF.Sin(angle) * shore);
        });
        parent.AddChild(new MeshInstance3D {
            Name = "lake_water_surface",
            Mesh = mesh,
            MaterialOverride = CreateMaterial(WaterBody.Lake, still),
            Position = new Vector3(Terrain.Lake.X, Level, Terrain.Lake.Y),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>The material of <paramref name="body"/>'s water; <paramref name="still"/> holds its waves at one moment.</summary>
    internal static ShaderMaterial CreateMaterial(WaterBody body, bool still) {
        shader ??= GD.Load<Shader>(ShaderPath);
        if (shader == null) throw new InvalidOperationException($"Could not load lake shader at {ShaderPath}");
        // Drawn after the see-through bank strips that run under its edge, so its foam lies over them wherever the
        // tiles sit in Godot's back-to-front order.
        var material = new ShaderMaterial { Shader = shader, RenderPriority = 1 };
        material.SetShaderParameter("uv_sampler", Texture("water_flow.png"));
        material.SetShaderParameter("normalmap_a_sampler", Texture("water_normal_a.png"));
        material.SetShaderParameter("normalmap_b_sampler", Texture("water_normal_b.png"));
        material.SetShaderParameter("foam_sampler", Texture("water_foam.png"));
        material.SetShaderParameter("caustic_sampler", GD.Load<TextureLayered>("res://assets/environment/lake/water_caustics.png"));
        material.SetShaderParameter("color_deep", body.Deep);
        material.SetShaderParameter("color_shallow", body.Shallow);
        material.SetShaderParameter("foam", body.Foam);
        UseWaves(material, body, still);
        return material;
    }

    /// <summary>A lily pad's material: flat <paramref name="color"/>, rising and falling with the lake's waves.</summary>
    internal static ShaderMaterial LilyPadMaterial(Color color, bool still) {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(LilyPadShaderPath) };
        material.SetShaderParameter("color", color);
        UseWaves(material, WaterBody.Lake, still);
        return material;
    }

    /// <summary>Gives a material <paramref name="body"/>'s waves; <paramref name="still"/> holds them at one moment.</summary>
    private static void UseWaves(ShaderMaterial material, WaterBody body, bool still) {
        material.SetShaderParameter("wave_speed", WaterBody.Speed);
        material.SetShaderParameter("wave_a", body.Waves[0]);
        material.SetShaderParameter("wave_b", body.Waves[1]);
        material.SetShaderParameter("wave_c", body.Waves[2]);
        if (still) SetWaveTime(material, ReviewSeconds);
    }

    /// <summary>Holds a water or lily pad material's waves at <paramref name="seconds"/>.</summary>
    internal static void SetWaveTime(ShaderMaterial material, float seconds) => material.SetShaderParameter(ReviewTime, seconds);

    /// <summary>Whether a material's waves follow the water's clock: the water's own, and a lily pad's.</summary>
    internal static bool FollowsWaves(ShaderMaterial material) => material.Shader?.ResourcePath is ShaderPath or LilyPadShaderPath;

    private static Texture2D Texture(string name) => GD.Load<Texture2D>($"res://assets/environment/lake/{name}");

    /// <summary>
    /// Adds a large surface as tiles of <see cref="Grid"/>, each sharing its edge columns and rows with its neighbours so
    /// they meet without cracks, which Godot leaves undrawn while they are off screen.
    /// </summary>
    internal static void AddTiles(Node3D parent, string name, float[] columns, float[] rows, Func<float, float, Vector3> at, Material material, Func<float, float, Vector2> uv) {
        for (int column = 0; column + 1 < columns.Length; column += TileVertices - 1)
            for (int row = 0; row + 1 < rows.Length; row += TileVertices - 1)
                parent.AddChild(new MeshInstance3D {
                    Name = $"{name}_{column}_{row}",
                    Mesh = Grid(columns[column..Math.Min(columns.Length, column + TileVertices)], rows[row..Math.Min(rows.Length, row + TileVertices)], at, uv),
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    // The waves move the surface a little past the mesh's own bounds.
                    ExtraCullMargin = 1,
                });
    }

    /// <summary>
    /// A surface of quads over every pair of neighbouring columns and rows, with each vertex where <paramref name="at"/>
    /// puts its column and row values and those values as its UV.
    /// Every row shares the columns, so a grid denser in some places than others has no cracks between them.
    /// </summary>
    internal static ArrayMesh Grid(float[] columns, float[] rows, Func<float, float, Vector3> at, Func<float, float, Vector2>? uv = null) {
        int width = columns.Length;
        var vertices = new Vector3[width * rows.Length];
        var uvs = new Vector2[vertices.Length];
        for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < width; column++) {
                vertices[row * width + column] = at(columns[column], rows[row]);
                uvs[row * width + column] = uv?.Invoke(columns[column], rows[row]) ?? new Vector2(columns[column], rows[row]);
            }
        var normals = new Vector3[vertices.Length];
        Array.Fill(normals, Vector3.Up);
        var indices = new int[(width - 1) * (rows.Length - 1) * 6];
        // Godot draws a triangle's front where its corners run clockwise; the last quad, never a point, tells which way
        // round the columns and rows run seen from above.
        int last = vertices.Length - 1;
        bool upward = (vertices[last - width] - vertices[last - width - 1]).Cross(vertices[last - 1] - vertices[last - width - 1]).Y < 0;
        int next = 0;
        for (int row = 0; row + 1 < rows.Length; row++)
            for (int column = 0; column + 1 < width; column++) {
                int a = row * width + column, b = a + 1, c = a + width, d = c + 1;
                indices[next++] = a; indices[next++] = upward ? b : d; indices[next++] = upward ? d : b;
                indices[next++] = a; indices[next++] = upward ? d : c; indices[next++] = upward ? c : d;
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Values from <paramref name="from"/> to <paramref name="to"/> at most <paramref name="step"/> apart, both ends included.</summary>
    internal static float[] Steps(float from, float to, float step) {
        int count = (int)MathF.Ceiling((to - from) / step);
        return Enumerable.Range(0, count + 1).Select(i => from + (to - from) * i / count).ToArray();
    }
}
