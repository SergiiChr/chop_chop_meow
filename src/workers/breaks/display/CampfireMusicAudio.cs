using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Plays the campfire recordings at the parts of the songs and solos the world has reached.</summary>
internal sealed class CampfireMusicAudio {
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
    private float time = float.NegativeInfinity;

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
    public float Update(World world, bool paused, float musicVolume, float volume) {
        var state = world.CampfireMusic.State;
        bool fastForwarding = world.Remote ? state.Playback == CampfirePlayback.FastForwarding : world.Sleep.SpeedingUp;
        if (musicVolume <= 0 || fastForwarding) {
            Stop();
            return 1;
        }
        bool clockMoved = world.Time != time;
        time = world.Time;
        paused |= world.Remote && state.Playback == CampfirePlayback.Paused;
        audible.Clear();
        var listener = world.Player;
        foreach (var (fire, song) in state.Songs) {
            if (!PlayingSong(song) || world.Find(fire) is not { } site) continue;
            var at = site.Pos;
            float gain = Math.Clamp((reach - listener.DistanceTo(at)) / (reach - World.FireReach), 0, 1);
            if (gain > 0) Add(new Source(fire, true), songs[song.Track], song.StartsAt, at, gain, volume);
        }
        if (state.Seats.Count > 0) {
            foreach (var worker in world.Workers) {
                if (!state.Seats.TryGetValue(worker.Id, out var seat) || seat.Solo <= 0 ||
                    state.Songs.TryGetValue(seat.Fire, out var song) && PlayingSong(song)) continue;
                var at = worker.Pos;
                float fraction = Math.Max(0, 1 - listener.DistanceTo(at) / reach);
                if (fraction > 0) Add(new Source(worker.Id, false), solos[(seat.Instrument, seat.Solo)], seat.SoloAt, at,
                    fraction * fraction, volume + SoloDb);
            }
        }
        foreach (var (source, voice) in playing) if (!audible.ContainsKey(source)) Release(source, voice);
        float speed = world.Remote ? 1 : (float)Engine.TimeScale;
        float loudestSong = 0;
        foreach (var (source, cue) in audible) {
            if (!playing.TryGetValue(source, out var voice)) {
                if (paused || !free.TryPop(out voice)) continue;
                playing.Add(source, voice);
            }
            voice.Follow(cue, time - cue.StartsAt, speed, paused, clockMoved);
            if (source.Song && !voice.Finished) loudestSong = Math.Max(loudestSong, cue.Gain);
        }
        return paused ? 1 : 1 - loudestSong;
    }

    private bool PlayingSong(CampfireSong song) =>
        song.Track > 0 && time >= song.StartsAt && time - song.StartsAt < songs[song.Track].Duration;

    private void Add(Source source, Clip clip, float startsAt, Vector2 at, float gain, float volume) {
        float offset = time - startsAt;
        if (clip.Stream == null || offset < 0 || offset >= clip.Duration) return;
        audible.Add(source, new Cue(clip.Stream, startsAt, at, gain, volume + Mathf.LinearToDb(gain)));
    }

    public float SongTime(World world, int fire, CampfireSong song) => TimeHeard(world, new Source(fire, true), songs[song.Track], song.StartsAt);

    public float SoloTime(World world, int worker, CampfireSeatMusic seat) =>
        TimeHeard(world, new Source(worker, false), solos[(seat.Instrument, seat.Solo)], seat.SoloAt);

    private float TimeHeard(World world, Source source, Clip clip, float startsAt) {
        float offset = world.Time - startsAt;
        if (offset < 0 || !playing.TryGetValue(source, out var voice) || !voice.Matches(clip.Stream, startsAt)) return offset;
        return voice.Finished ? clip.Duration : voice.TimeHeard(outputLatency, offset);
    }

    public void Stop() {
        foreach (var (source, voice) in playing) Release(source, voice);
        audible.Clear();
        time = float.NegativeInfinity;
    }

    private void Release(Source source, Voice voice) {
        voice.Stop(); free.Push(voice); playing.Remove(source);
    }

    private readonly record struct Source(int Id, bool Song);
    private readonly record struct Clip(float Duration, AudioStream? Stream);
    private readonly record struct Cue(AudioStream Stream, float StartsAt, Vector2 At, float Gain, float Volume);

    private sealed class Voice(AudioStreamPlayer3D player) {
        private AudioStream? stream;
        private float startedAt, seekAt;
        private float heardAt = float.NegativeInfinity;
        private bool waitingForOnset;
        public bool Finished => stream != null && !player.Playing && !player.StreamPaused;
        private float Position => player.GetPlaybackPosition() + (float)AudioServer.GetTimeSinceLastMix() * player.PitchScale;

        public float TimeHeard(float latency, float offset) {
            if (player.Playing && !player.StreamPaused && player.HasStreamPlayback())
                heardAt = Math.Max(heardAt, Position - latency * player.PitchScale);
            if (float.IsFinite(heardAt)) return heardAt;
            return waitingForOnset && offset < SeekDrift ? 0 : offset;
        }

        public bool Matches(AudioStream? clip, float start) => stream == clip && startedAt == start;

        public void Follow(Cue cue, float offset, float speed, bool paused, bool clockMoved) {
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
                heardAt = float.NegativeInfinity;
                waitingForOnset = offset <= StartWindow;
                player.Play(waitingForOnset ? 0 : offset);
            } else {
                // A placed voice starts on the next physics frame. Give it time to mix before correcting its clock.
                if (player.HasStreamPlayback()) {
                    waitingForOnset = false;
                    // Catch up to a newer world position, but never repeat sound and movement the player already heard.
                    if (clockMoved && offset - seekAt > SeekDrift && offset - Position > SeekDrift) {
                        seekAt = offset; player.Seek(offset);
                    }
                }
                player.StreamPaused = false;
            }
        }

        public void Stop() { player.Stop(); stream = null; }
    }
}
