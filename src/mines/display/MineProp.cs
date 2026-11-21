using Godot;

namespace ChopChopMeow;

/// <summary>A mine's parts, shown stage by stage: the marked outline, the dug hole, the delivered materials, the timber frame, the rails and the cart with its load.</summary>
public sealed class MineProp : Prop {
    public Node3D Outline = null!, Hole = null!, Supplies = null!, Frame = null!, Rails = null!, Cart = null!, Load = null!, Fire = null!;
    /// <summary>Seconds of work on the mine's stage as its hole and frame show them.</summary>
    public readonly LocalClock Built = new(2);
    public MeshInstance3D Glow = null!;
    public OmniLight3D Light = null!;
}
