using Godot;

namespace ChopChopMeow;

/// <summary>A cabin as drawn: its door, how far open it stands, and the window and lamp that glow at night.</summary>
public sealed class HutProp : Prop {
    public Node3D Door = null!;
    public MeshInstance3D Window = null!;
    public OmniLight3D Light = null!;
    public float Open;
    /// <summary>The last one through the door ran, so it slams shut behind them.</summary>
    public bool Slam;
    /// <summary>Whether the hut shows its night window glow and lamp.</summary>
    public bool Lit;
}
