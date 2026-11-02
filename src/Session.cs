namespace ChopChopMeow;

/// <summary>Where this window stands with the game it started, if it started one.</summary>
internal abstract record Session {
    /// <summary>It has no game of its own: it is at a menu, or a guest in someone else's.</summary>
    public sealed record None : Session;
    /// <summary>Its server process is coming up and has not said which port it took.</summary>
    public sealed record Launching(int Slot) : Session;
    /// <summary>Its server is up and it is joining, for the game in a save slot, or -1 for a new one.</summary>
    public sealed record Starting(int Slot) : Session;
    /// <summary>It plays on its own server.</summary>
    public sealed record Playing : Session;
    /// <summary>It stepped out of its game on this port to join another, and its server waits a while for it.</summary>
    public sealed record Away(int Port) : Session;
    /// <summary>It is going back to its game after a join failed or was cancelled, for the reason in the note.</summary>
    public sealed record Returning(string Note) : Session;
}
