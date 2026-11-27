using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>The live campfire music shared with guests, not part of a save.</summary>
internal sealed class CampfireMusicState {
    public Dictionary<int, CampfireSeatMusic> Seats { get; set; } = new();
    public Dictionary<int, CampfireSong> Songs { get; set; } = new();

    /// <summary>Whether the shared seats and recordings use known instruments, tracks and clock times.</summary>
    internal bool Valid() {
        if (Seats == null || Songs == null) return false;
        var brought = new HashSet<(int Fire, CampfireInstrument Instrument)>();
        foreach (var (id, seat) in Seats)
            if (id <= 0 || seat.Fire <= 0 || !System.Enum.IsDefined(seat.Instrument) || seat.Solo < 0 ||
                seat.Solo > CampfireScore.SoloCount(seat.Instrument) || !float.IsFinite(seat.SoloAt) || seat.SoloAt < 0 ||
                seat.Instrument != CampfireInstrument.None && !brought.Add((seat.Fire, seat.Instrument)))
                return false;
        foreach (var (fire, song) in Songs)
            if (fire <= 0 || song.Track <= 0 || song.Track > CampfireScore.SongCount ||
                !float.IsFinite(song.StartsAt) || song.StartsAt < 0 ||
                !PlayedBy(song.Harmonica, fire, CampfireInstrument.Harmonica) ||
                !PlayedBy(song.Guitar, fire, CampfireInstrument.Guitar) || !PlayedBy(song.Drum, fire, CampfireInstrument.Drum))
                return false;
        return true;
    }

    private bool PlayedBy(int id, int fire, CampfireInstrument instrument) =>
        Seats.TryGetValue(id, out var seat) && seat.Fire == fire && seat.Instrument == instrument;
}
