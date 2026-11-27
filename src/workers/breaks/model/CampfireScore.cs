namespace ChopChopMeow;

/// <summary>Time in the campfire recordings, measured from their first sample.</summary>
internal static class CampfireScore {
    private static readonly CampfireRecording[] Songs = [
        new("campfire_american_western_country", 131.16f),
        new("campfire_country_music", 131.16f),
    ];

    private static readonly CampfireRecording[] Guitar = [
        new("campfire_guitar", 2f, [.012f]),
        new("campfire_guitar", 2f, [.016f]),
        new("campfire_guitar", 2f, [.060f]),
        new("campfire_guitar", 2f, [.110f]),
    ];
    private static readonly CampfireRecording[] Drum = [
        new("campfire_drum", .370f, [.010f]),
        new("campfire_drum", 2f, [.010f, .510f, 1.010f, 1.510f]),
        new("campfire_drum", 1.516f, [.006f, .096f, .171f, .271f, .356f, .466f, .576f]),
        new("campfire_drum", 3.307f, [.016f]),
    ];
    private static readonly float[] Harmonica = [
        .349f, .168f, .373f, .205f, .444f, .274f, .393f, .178f, .373f, .227f, .421f, .306f, .355f, .187f, .412f, .244f,
        3.952f, .482f, 2.282f, .376f, .310f, 2.526f, 2.493f, .425f, .331f, .181f, .353f, 1.389f,
    ];

    internal static int SongCount => Songs.Length;
    internal static CampfireRecording Song(int track) => Songs[track - 1];
    internal static int SoloCount(CampfireInstrument instrument) => instrument switch {
        CampfireInstrument.Harmonica => Harmonica.Length,
        CampfireInstrument.Guitar => Guitar.Length,
        CampfireInstrument.Drum => Drum.Length,
        _ => 0,
    };
    internal static CampfireRecording Solo(CampfireInstrument instrument, int variant) => instrument switch {
        CampfireInstrument.Harmonica => new("campfire_harmonica", Harmonica[variant - 1]),
        CampfireInstrument.Guitar => Guitar[variant - 1],
        _ => Drum[variant - 1],
    };
}
