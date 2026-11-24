using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The nettle's model: sprigs of Poly Haven's photoscanned nettle plant, baked by tools/nettle_models.py, whose leaves
/// are cut out of one shared texture.
/// </summary>
internal static class NettleShape {
    private const string Folder = "res://assets/models/nettle/";
    /// <summary>How many times its scanned size a sprig stands in the game: the tallest, 21 cm as scanned, comes to knee height.</summary>
    public const float Scale = 3.4f;
    /// <summary>The sprigs' shared texture, with the leaves cut out of it and seen from both sides.</summary>
    public static readonly StandardMaterial3D Leaves = new() {
        AlbedoTexture = GD.Load<Texture2D>(Folder + "nettle_plant.png"), Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = .5f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = .8f,
    };
    // The sprigs each look of the plant is made of, tallest first.
    private static readonly string[][] Clumps = [["tall_a", "medium_b", "small_a"], ["tall_b", "medium_a", "small_b"], ["tall_a", "tall_b", "small_b"]];
    private static readonly ArrayMesh?[] plants = new ArrayMesh?[Clumps.Length];

    /// <summary>One scanned sprig at its scanned size, standing on its origin: tall, medium or small, a or b.</summary>
    public static ArrayMesh Sprig(string name) => GD.Load<ArrayMesh>($"{Folder}nettle_plant_{name}.res");

    /// <summary>A nettle plant as it stands in the world: a clump of three sprigs, each turned its own way, in one of three looks.</summary>
    public static ArrayMesh Plant(int look) {
        look = Math.Abs(look) % Clumps.Length;
        if (plants[look] is ArrayMesh built) return built;
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < Clumps[look].Length; i++) {
            // The tallest stands in the middle and the others lean out from it.
            float turn = look * 1.3f + i * 2.399963f, off = i == 0 ? 0 : .12f;
            var basis = new Basis(Vector3.Up, turn) * new Basis(Vector3.Right, i == 0 ? 0 : .22f) * Basis.FromScale(Vector3.One * Scale);
            tool.AppendFrom(Sprig(Clumps[look][i]), 0, new Transform3D(basis, new Vector3(MathF.Sin(turn), 0, MathF.Cos(turn)) * off));
        }
        tool.GenerateTangents();
        return plants[look] = tool.Commit();
    }
}
