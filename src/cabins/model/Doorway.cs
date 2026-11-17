namespace ChopChopMeow;

/// <summary>What a player is doing at a cabin's door.</summary>
public enum Doorway {
    None,
    /// <summary>Stepping in through the door, out in the open for the first half.</summary>
    Entering,
    /// <summary>Stepping up behind someone entering the same cabin, until the door shuts in front of them.</summary>
    Following,
    /// <summary>Knocking on the shut door of a cabin someone is in.</summary>
    Knocking,
    /// <summary>Tugged back out of the doorway by a wolf holding them by the waist, to step in again at a run.</summary>
    Tugged,
    /// <summary>Tugged off the door they knocked on, or stood aside at, by a wolf, to stand free.</summary>
    TuggedOff,
    /// <summary>Stepping out through the door, out in the open for the second half.</summary>
    Leaving,
    /// <summary>Standing aside from the door for someone stepping out, to step in once they are out.</summary>
    Aside,
}
