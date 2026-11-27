namespace ChopChopMeow;

/// <summary>One song chosen by the three workers, and when it starts on the world clock.</summary>
public readonly record struct CampfireSong(int Track, float StartsAt, int Harmonica, int Guitar, int Drum) {
    public bool Plays(int worker) => worker == Harmonica || worker == Guitar || worker == Drum;
}
