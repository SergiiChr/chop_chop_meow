using Godot;

namespace ChopChopMeow;

/// <summary>A diary page lying in the world: the sheet, which floats on the lake or flutters down off a tree while the model's root stays at its thing's place.</summary>
public sealed class PageProp : Prop {
    /// <summary>The sheet, which moves about while the root stays put.</summary>
    public Node3D Sheet = null!;
    /// <summary>Seconds into its fall off a tree, or past <see cref="PageShape.FallSeconds"/> once it lies on the ground.</summary>
    public float Fall = float.MaxValue;
}
