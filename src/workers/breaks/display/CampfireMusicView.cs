using System;
using Godot;

namespace ChopChopMeow;

/// <summary>How a seated worker plays an instrument or nods in time with the recording.</summary>
internal readonly record struct CampfireMusicView(CampfireInstrument Instrument, float Playing = 0, float Nodding = 0, float Beat = 0) {
    internal bool Eating => Instrument == CampfireInstrument.None && Nodding == 0;

    internal static CampfireMusicView At(World world, Sounds? sounds, Worker worker) {
        var music = world.CampfireMusic;
        var seat = music.SeatOf(worker);
        if (seat.Fire == 0) return default;
        var song = music.SongAt(seat.Fire);
        if (song.Track > 0) {
            float elapsed = sounds?.CampfireSongTime(seat.Fire, song) ?? world.Time - song.StartsAt;
            var beats = CampfireBeats.ForSong(song.Track);
            if (elapsed >= CampfireScore.Song(song.Track).Duration) return new(seat.Instrument);
            float weight = Mathf.SmoothStep(beats[0] - .20f, beats[0], elapsed) * (1 - Mathf.SmoothStep(beats[^1], beats[^1] + .30f, elapsed));
            float beat = BeatAt(beats, elapsed);
            return song.Plays(worker.Id) ? new(seat.Instrument, weight, Beat: beat) : new(seat.Instrument, Nodding: weight, Beat: beat);
        }
        if (seat.Solo == 0) return new(seat.Instrument);
        float soloTime = world.Time - seat.SoloAt;
        var clip = CampfireScore.Solo(seat.Instrument, seat.Solo);
        if (seat.Instrument == CampfireInstrument.Harmonica) {
            float weight = Mathf.SmoothStep(-.18f, 0, soloTime) * Mathf.SmoothStep(0, .07f, clip.Duration - soloTime);
            return new(seat.Instrument, weight, Beat: Mathf.PosMod(soloTime / .74f, 1));
        }
        var (playing, phase) = Strike(clip.Hits, soloTime);
        return new(seat.Instrument, playing, Beat: phase);
    }

    private static float BeatAt(ReadOnlySpan<float> beats, float elapsed) {
        int next = beats.BinarySearch(elapsed);
        if (next >= 0) return 0;
        next = ~next;
        int previous = Math.Clamp(next - 1, 0, beats.Length - 2);
        return Mathf.PosMod((elapsed - beats[previous]) / (beats[previous + 1] - beats[previous]), 1);
    }

    private static (float Playing, float Beat) Strike(ReadOnlySpan<float> hits, float elapsed) {
        const float gather = .18f, returnToRest = .28f;
        int next = hits.BinarySearch(elapsed);
        if (next >= 0) return (1, 0);
        next = ~next;
        float since = next > 0 ? elapsed - hits[next - 1] : float.PositiveInfinity;
        float until = next < hits.Length ? hits[next] - elapsed : float.PositiveInfinity;
        if (since + until <= gather + returnToRest) return (1, since / (since + until));
        if (since < returnToRest) return (1 - Mathf.SmoothStep(.10f, returnToRest, since), since / (2 * returnToRest));
        if (until < gather) return (1 - Mathf.SmoothStep(.06f, gather, until), 1 - until / (2 * gather));
        return default;
    }
}
