using Godot;

namespace ChopChopMeow;

/// <summary>A firebug in the air: the part that flies about its thing's place, its glow and the light it throws on the ground.</summary>
public sealed class FirebugProp : Prop {
    /// <summary>The bug itself, which hovers and darts while the model's root stays at its thing's place.</summary>
    public Node3D Fly = null!;
    /// <summary>The soft glow round it and its lantern, in the 3D look.</summary>
    public MeshInstance3D? Halo, Heart;
    /// <summary>The pixel picture of it, in the sprite look.</summary>
    public Sprite3D? Sprite;
    public OmniLight3D Light = null!;
    /// <summary>Dots of light left where it has just been, nearest first, and where each is on the ground and in the air.</summary>
    public MeshInstance3D[] Trail = [];
    public Vector3[] Trailed = [];
    /// <summary>Where it is drawn, which follows where the world has it; null until it is first drawn.</summary>
    public Vector3? Drawn;
}
