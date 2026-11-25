using Godot;

namespace ChopChopMeow;

/// <summary>A firebug in the air: the part that flies about its thing's place, and its glow.</summary>
public sealed class FirebugProp : Prop {
    /// <summary>The bug itself, which hovers and darts while the model's root stays at its thing's place.</summary>
    public Node3D Fly = null!;
    /// <summary>The soft glow round it and its lantern.</summary>
    public MeshInstance3D Halo = null!, Heart = null!;
    /// <summary>Where it is drawn, which follows where the world has it; null until it is first drawn.</summary>
    public Vector3? Drawn;
}
