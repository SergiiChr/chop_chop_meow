using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Plays the campfire recordings at the parts of the songs and solos the world has reached.</summary>
internal sealed class CampfireMusicAudio {
    // Normal updates arrive thirty times a second. A world that has held still this long is waiting or paused.
    private const float HoldAfter = .15f;
    private const float SeekDrift = .2f;
    // Normal snapshot and physics delays must not make playback skip the attack of a kick or strum.
    private const float StartWindow = .08f;
    private const float SoloDb = -3;
    private readonly float reach;
    private readonly float outputLatency;
    private readonly Clip[] songs = new Clip[CampfireScore.SongCount + 1];
    private readonly Dictionary<(CampfireInstrument Instrument, int Solo), Clip> solos = new();
    // Each voice needs at least one seated worker, so the crew limit is enough for simultaneous play across all fires.
    private readonly Dictionary<Source, Voice> playing = new(World.MaxWorkers);
    private readonly Dictionary<Source, Cue> audible = new(World.MaxWorkers);
    private readonly Stack<Voice> free = new(World.MaxWorkers);
    private float time = float.NegativeInfinity, heldFor;

    public CampfireMusicAudio(Func<AudioStreamPlayer3D> placeVoice, float reach) {
        this.reach = reach;
        outputLatency = (float)AudioServer.GetOutputLatency();
        for (int i = 0; i < World.MaxWorkers; i++) free.Push(new Voice(placeVoice()));
        for (int track = 1; track < songs.Length; track++) {
            var score = CampfireScore.Song(track);
            songs[track] = new Clip(score.Duration, GD.Load<AudioStreamOggVorbis>($"res://assets/music/{score.Name}.ogg"));
        }
        foreach (var instrument in Enum.GetValues<CampfireInstrument>()) {
            for (int solo = 1; solo <= CampfireScore.SoloCount(instrument); solo++) {
                var score = CampfireScore.Solo(instrument, solo);
                var stream = GD.Load<AudioStreamMP3>($"res://assets/sounds/{score.Name}_{solo}.mp3");
                solos.Add((instrument, solo), new Clip(score.Duration, stream));
            }
        }
    }

    /// <summary>Follows the world snapshot and returns the share of the day or night soundtrack still wanted.</summary>
    public float Update(World world, float dt, bool paused, float musicVolume, float volume) {
        heldFor = world.Time == time ? heldFor + dt : 0;
        time = world.Time;
        paused |= heldFor >= HoldAfter;
        audible.Clear();
        float loudestSong = 0;
        var state = world.CampfireMusic.State;
        var listener = world.Player;
        foreach (var (fire, song) in state.Songs) {
            if (!PlayingSong(song) || world.Find(fire) is not { } site) continue;
            var at = site.Pos;
            float gain = Math.Clamp((reach - listener.DistanceTo(at)) / (reach - World.FireReach), 0, 1);
            if (gain > 0 && Add(new Source(fire, true), songs[song.Track], song.StartsAt, at, gain, volume))
                loudestSong = Math.Max(loudestSong, gain);
        }
        if (musicVolume > 0 && state.Seats.Count > 0) {
            foreach (var worker in world.Workers) {
                if (!state.Seats.TryGetValue(worker.Id, out var seat) || seat.Solo <= 0 ||
                    state.Songs.TryGetValue(seat.Fire, out var song) && PlayingSong(song)) continue;
                var at = worker.Pos;
                float fraction = Math.Max(0, 1 - listener.DistanceTo(at) / reach);
                if (fraction > 0) Add(new Source(worker.Id, false), solos[(seat.Instrument, seat.Solo)], seat.SoloAt, at,
                    fraction * fraction, volume + SoloDb);
            }
        }
        if (musicVolume <= 0) audible.Clear();
        foreach (var (source, voice) in playing) if (!audible.ContainsKey(source)) Release(source, voice);
        float speed = world.Remote ? 1 : (float)Engine.TimeScale;
        foreach (var (source, cue) in audible) {
            if (!playing.TryGetValue(source, out var voice)) {
                if (paused || !free.TryPop(out voice)) continue;
                playing.Add(source, voice);
            }
            voice.Follow(cue, time - cue.StartsAt, speed, paused);
        }
        return paused ? 1 : 1 - loudestSong;
    }

    private bool PlayingSong(CampfireSong song) =>
        song.Track > 0 && time >= song.StartsAt && time - song.StartsAt < songs[song.Track].Duration;

    private bool Add(Source source, Clip clip, float startsAt, Vector2 at, float gain, float volume) {
        float offset = time - startsAt;
        if (clip.Stream == null || offset < 0 || offset >= clip.Duration) return false;
        audible.Add(source, new Cue(clip.Stream, startsAt, at, volume + Mathf.LinearToDb(gain)));
        return true;
    }

    public float SongTime(World world, int fire, CampfireSong song) => TimeHeard(world, new Source(fire, true), songs[song.Track], song.StartsAt);

    public float SoloTime(World world, int worker, CampfireSeatMusic seat) =>
        TimeHeard(world, new Source(worker, false), solos[(seat.Instrument, seat.Solo)], seat.SoloAt);

    private float TimeHeard(World world, Source source, Clip clip, float startsAt) {
        float offset = world.Time - startsAt;
        if (offset < 0 || !playing.TryGetValue(source, out var voice) || !voice.Matches(clip.Stream, startsAt)) return offset;
        if (!voice.Heard) return voice.WaitingForOnset && offset < SeekDrift ? 0 : offset;
        float audio = voice.HeardAt(outputLatency);
        if (Math.Abs(offset - audio) <= SeekDrift) return audio;
        return offset;
    }

    public void Stop() {
        foreach (var (source, voice) in playing) Release(source, voice);
        time = float.NegativeInfinity;
        heldFor = 0;
    }

    private void Release(Source source, Voice voice) {
        voice.Stop(); free.Push(voice); playing.Remove(source);
    }

    private readonly record struct Source(int Id, bool Song);
    private readonly record struct Clip(float Duration, AudioStream? Stream);
    private readonly record struct Cue(AudioStream Stream, float StartsAt, Vector2 At, float Volume);

    private sealed class Voice(AudioStreamPlayer3D player) {
        private AudioStream? stream;
        private float startedAt, seekAt;
        public bool WaitingForOnset { get; private set; }
        public bool Heard => player.Playing && !player.StreamPaused && player.HasStreamPlayback();
        private float Position => player.GetPlaybackPosition() + (float)AudioServer.GetTimeSinceLastMix() * player.PitchScale;

        public float HeardAt(float latency) => Position - latency * player.PitchScale;

        public bool Matches(AudioStream? clip, float start) => stream == clip && startedAt == start;

        public void Follow(Cue cue, float offset, float speed, bool paused) {
            player.Position = new Vector3(cue.At.X, 0, cue.At.Y);
            player.VolumeDb = cue.Volume;
            if (paused) { player.StreamPaused = true; return; }
            player.PitchScale = speed;
            if (!Matches(cue.Stream, cue.StartsAt)) {
                player.Stop();
                player.StreamPaused = false;
                player.Stream = stream = cue.Stream;
                startedAt = cue.StartsAt;
                seekAt = offset;
                WaitingForOnset = offset <= StartWindow;
                player.Play(WaitingForOnset ? 0 : offset);
            } else {
                // A placed voice starts on the next physics frame. Give it time to mix before correcting its clock.
                if (player.HasStreamPlayback()) {
                    WaitingForOnset = false;
                    if (Math.Abs(offset - seekAt) > SeekDrift && Math.Abs(Position - offset) > SeekDrift) {
                        seekAt = offset; player.Seek(offset);
                    }
                }
                player.StreamPaused = false;
            }
        }

        public void Stop() { player.Stop(); stream = null; }
    }
}
