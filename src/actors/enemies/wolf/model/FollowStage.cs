namespace ChopChopMeow;

/// <summary>What a torch has made of a wolf hunting a player (<see cref="WolfFollow"/>).</summary>
public enum FollowStage {
    /// <summary>No torch wave has reached it: it hunts as the light lets it.</summary>
    None,
    /// <summary>In follow mode, walking back out to its circle after a torch wave reached it nearer its player.</summary>
    BackingOff,
    /// <summary>In follow mode, circling its player at the edge of the torchlight.</summary>
    Circling,
    /// <summary>In follow mode, standing braced where it is because its player has walked up to it.</summary>
    Braced,
    /// <summary>It turned on its player when one of a pack following them stood its ground: no torch wave scares it again.</summary>
    Bold,
    /// <summary>Following alone, it runs from its player until it is out of their line of sight, for <see cref="Wolf.Flee"/> seconds at most.</summary>
    Routed,
}
