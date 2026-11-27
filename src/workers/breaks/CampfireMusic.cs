using System;
using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>Instruments brought out by idle workers and what they play at each campfire.</summary>
public sealed class CampfireMusic {
    private const float SoloGap = 5, SoloGapRange = 5, Lead = .2f;
    private const double InstrumentChance = .25;
    private Random random = new();
    private readonly Dictionary<int, int> seated = new();
    private readonly Dictionary<int, float> soloAgain = new();
    private readonly Dictionary<int, Trio> trios = new();
    internal CampfireMusicState State { get; set; } = new();

    public CampfireSeatMusic SeatOf(Worker worker) => State.Seats.GetValueOrDefault(worker.Id);
    public CampfireSong SongAt(int fire) => State.Songs.GetValueOrDefault(fire);

    /// <summary>Uses the same visit rolls for every run of a check or showcase.</summary>
    internal void Seed(int seed) => random = new Random(seed);

    internal void Reset() {
        State.Seats.Clear(); State.Songs.Clear(); soloAgain.Clear();
        Begin();
    }

    internal void Begin() { seated.Clear(); trios.Clear(); }

    /// <summary>Marks a worker who reached a campfire seat.</summary>
    internal void Sit(Worker worker, Thing fire) => seated.Add(worker.Id, fire.Id);

    private void AssignInstruments(float at) {
        // Record returning instruments first, even if their workers took their turn after a newcomer.
        foreach (var (id, seat) in State.Seats) {
            if (seated.GetValueOrDefault(id) != seat.Fire) { State.Seats.Remove(id); soloAgain.Remove(id); continue; }
            if (seat.Instrument != CampfireInstrument.None)
                trios[seat.Fire] = trios.GetValueOrDefault(seat.Fire).Add(seat.Instrument, id);
        }
        foreach (var (id, fire) in seated) {
            if (State.Seats.ContainsKey(id)) continue;
            var trio = trios.GetValueOrDefault(fire);
            var instrument = RollInstrument(trio);
            State.Seats.Add(id, new(fire, instrument));
            if (instrument == CampfireInstrument.None) continue;
            soloAgain[id] = at + Wait();
            trios[fire] = trio.Add(instrument, id);
        }
    }

    private CampfireInstrument RollInstrument(Trio trio) {
        double roll = random.NextDouble();
        if (roll >= InstrumentChance || trio.Complete) return CampfireInstrument.None;
        Span<CampfireInstrument> available = stackalloc CampfireInstrument[3];
        int count = 0;
        if (trio.Harmonica == 0) available[count++] = CampfireInstrument.Harmonica;
        if (trio.Guitar == 0) available[count++] = CampfireInstrument.Guitar;
        if (trio.Drum == 0) available[count++] = CampfireInstrument.Drum;
        // Divide the successful part of the roll equally among the instruments this fire is missing.
        return available[(int)(roll / InstrumentChance * count)];
    }

    /// <summary>Starts one song for each new trio and schedules solos at fires without all three instruments.</summary>
    internal void Finish(float at) {
        AssignInstruments(at);
        foreach (var (fire, _) in State.Songs)
            if (!trios.GetValueOrDefault(fire).Complete) State.Songs.Remove(fire);
        foreach (var (fire, trio) in trios) {
            if (!trio.Complete) continue;
            var song = SongAt(fire);
            // Keep the song chosen for this gathering, including after it finishes, until the instruments break up.
            if (song.Track == 0)
                State.Songs[fire] = new(random.Next(1, CampfireScore.SongCount + 1), at + Lead, trio.Harmonica, trio.Guitar, trio.Drum);
            else if (song.Harmonica != trio.Harmonica || song.Guitar != trio.Guitar || song.Drum != trio.Drum)
                State.Songs[fire] = song with { Harmonica = trio.Harmonica, Guitar = trio.Guitar, Drum = trio.Drum };
        }
        foreach (var (id, seat) in State.Seats) {
            if (seat.Instrument == CampfireInstrument.None) continue;
            if (State.Songs.ContainsKey(seat.Fire)) {
                if (seat.Solo != 0) State.Seats[id] = seat with { Solo = 0, SoloAt = 0 };
                soloAgain.Remove(id);
                continue;
            }
            if (seat.Solo != 0 && at >= seat.SoloAt + CampfireScore.Solo(seat.Instrument, seat.Solo).Duration)
                State.Seats[id] = seat with { Solo = 0, SoloAt = 0 };
            if (!soloAgain.TryGetValue(id, out float next)) { soloAgain[id] = at + Wait(); continue; }
            if (at < next) continue;
            int variant = random.Next(1, CampfireScore.SoloCount(seat.Instrument) + 1);
            float start = at + Lead;
            State.Seats[id] = seat with { Solo = variant, SoloAt = start };
            soloAgain[id] = start + CampfireScore.Solo(seat.Instrument, variant).Duration + Wait();
        }
    }

    private float Wait() => SoloGap + (float)random.NextDouble() * SoloGapRange;

    private readonly record struct Trio(int Harmonica, int Guitar, int Drum) {
        public bool Complete => Harmonica != 0 && Guitar != 0 && Drum != 0;

        public Trio Add(CampfireInstrument instrument, int id) => instrument switch {
            CampfireInstrument.Harmonica => this with { Harmonica = id },
            CampfireInstrument.Guitar => this with { Guitar = id },
            CampfireInstrument.Drum => this with { Drum = id },
            _ => this,
        };
    }
}
