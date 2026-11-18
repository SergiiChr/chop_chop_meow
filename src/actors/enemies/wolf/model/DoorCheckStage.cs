namespace ChopChopMeow;

/// <summary>What a wolf is doing at the door of a cabin with someone inside, in the order it does it.</summary>
public enum DoorCheckStage {
    None,
    /// <summary>On its way to the door, or waiting behind a wolf already at it.</summary>
    Coming,
    /// <summary>Scratching at the shut door; it goes away from a locked one.</summary>
    Scratching,
    /// <summary>Working its snout into the edge of an unlocked door, which the key can no longer stop.</summary>
    Prying,
    /// <summary>Pushing the door slowly open with its head.</summary>
    Opening,
    /// <summary>Standing in the doorway, growling at whoever is inside.</summary>
    Staring,
    /// <summary>Launching itself at the last player who came in.</summary>
    Launching,
    /// <summary>Tugging that player out through the door by the waist.</summary>
    TakingOut,
}
