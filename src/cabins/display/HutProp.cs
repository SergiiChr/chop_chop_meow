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
    /// <summary>Whether the door is slamming shut now, which it does speeding up into its frame.</summary>
    public bool Slamming;
    /// <summary>The id of the player going through the door as last drawn, or null with nobody in it.</summary>
    public int? Through;
    /// <summary>The slam's sound, picked as a runner goes through and started so its loudest moment lands as the door hits the frame; null once started.</summary>
    public Sounds.Cue? SlamCue;
    /// <summary>Whether the hut shows its night window glow and lamp.</summary>
    public bool Lit;
}
