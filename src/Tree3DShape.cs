using Godot;

namespace ChopChopMeow;

/// <summary>Opt-in Tree3D 1.1.0 meshes baked offline by tools/tree3d.py, shared across the forest.</summary>
internal static class Tree3DShape {
    public static readonly bool Enabled = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--tree3d") >= 0;
    private static readonly Mesh?[,] trunks = new Mesh?[4, 3], leaves = new Mesh?[4, 3];
    private static readonly StandardMaterial3D?[] bark = new StandardMaterial3D?[4], foliage = new StandardMaterial3D?[4];
    private static readonly string[] species = { "oak", "spruce", "birch", "bush" };
    private const string Folder = "res://assets/models/tree3d/";

    public static Mesh Add(Node3D parent, int kind, int variant) {
        trunks[kind, variant] ??= GD.Load<Mesh>($"{Folder}{species[kind]}{variant}_trunk.res");
        leaves[kind, variant] ??= GD.Load<Mesh>($"{Folder}{species[kind]}{variant}_leaves.res");
        bark[kind] ??= new StandardMaterial3D {
            AlbedoTexture = GD.Load<Texture2D>(Folder + (kind == 2 ? "birch_bark.svg" : "bark.svg")),
            CullMode = BaseMaterial3D.CullModeEnum.Front,
            Roughness = .95f
        };
        foliage[kind] ??= new StandardMaterial3D {
            AlbedoTexture = GD.Load<Texture2D>(Folder + (kind == 1 ? "needles.svg" : "broadleaf.svg")),
            AlbedoColor = kind == 2 ? new Color("c5d690") : Colors.White,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = .45f,
            Roughness = .95f
        };
        ModelKit.Add(parent, trunks[kind, variant]!, bark[kind]!, Vector3.Zero);
        ModelKit.Add(parent, leaves[kind, variant]!, foliage[kind]!, Vector3.Zero);
        return leaves[kind, variant]!;
    }
}
