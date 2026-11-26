using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Sound effects from <c>assets/sounds</c>.
/// A sound named <c>x</c> is <c>x.mp3</c>, <c>x.wav</c> for a held sound, or the variants <c>x_1.mp3</c>, <c>x_2.mp3</c> and so on.
/// Each play picks a variant other than the previous one and nudges its pitch and volume, so repeated actions do not sound mechanical.
/// A held sound loops for as long as its source keeps holding it (<see cref="Hold"/>).
/// Day and night music crossfade when the time of day changes, and each resumes where it last stopped.
/// </summary>
public partial class Sounds : Node {
    private const string Folder = "res://assets/sounds";
    // A sound is an MP3, or a WAV for a held sound: that starts at a random moment, and an MP3 decodes from its beginning to get there, which stalls the frame by up to 9 ms.
    private static readonly string[] Formats = ["mp3", "wav"];
    // A sound with a world position is loudest at the player and fades to silence this many metres away, whatever the zoom.
    private const float Reach = 16;
    // A sound this close to the player plays with no place, so it stays centred while they walk on.
    private const float OwnSpot = 1;
    // Variants share a loudness, and these levels set each sound's place in the mix: quiet steps, then impacts and handling, then alerts.
    private static readonly Dictionary<string, float> Levels = new() {
        ["footsteps_default"] = -18,
        ["footsteps_gravel"] = -18,
        ["footsteps_rocks"] = -17,
        ["footsteps_water"] = -15,
        ["chop_tree"] = -7.5f,
        ["tree_crack"] = -5,
        ["tree_bend"] = -6,
        ["tree_crash"] = -5,
        ["bench_work"] = -3.5f,
        ["rail_clink"] = -13,
        ["anvil_work"] = -2,
        ["mine_ore"] = -8,
        ["dig_earth"] = -8,
        ["dig_toss"] = -11,
        ["dig_up"] = -7,
        ["bellows_pump"] = -14,
        ["grind_mortar"] = -10,
        ["rustle"] = -7,
        ["rake_leaves"] = -9,
        ["butcher_carcass"] = -6.5f,
        ["place_building"] = -6.5f,
        ["take_down_building"] = -4.5f,
        ["tool_air_swing"] = -11,
        ["open_chest"] = -11,
        ["close_chest"] = -13,
        ["hut_door_normal"] = -6.5f,
        ["hut_door_escaping"] = -8,
        ["door_knock"] = -11,
        ["door_lock"] = -8,
        ["door_scratch"] = -5,
        ["door_rattle"] = -6,
        ["open_steel_smelter"] = -9,
        ["meat_inventory"] = -6.5f,
        ["move_coins_inventory"] = -1,
        ["moving_leather_in_inventory"] = -9,
        ["wood_rocks_inventory"] = -9,
        ["pickup_gold"] = -10,
        ["putting_items_on_hotbar"] = -3,
        ["eat_berry"] = -7.5f,
        ["stomach_growl"] = -8,
        ["gather_water"] = -10,
        ["start_fire"] = -8,
        ["torch_out"] = -16,
        ["buy_coins"] = -2,
        ["hire_sign"] = -7,
        ["payment_from_village_arrives"] = -6,
        ["wolf_growl_distance"] = -11,
        ["wolf_howl"] = -11,
        ["wolf_howl_group"] = -11,
        ["wolf_growl_approaching"] = -3,
        ["wolf_bite"] = -3,
        ["tug_scream"] = -5,
        ["takedown_scream"] = -5,
        ["sting_groan"] = -9,
        ["wolf_hit"] = -5.5f,
    };
    /// <summary>
    /// Seconds into each variant at which it is loudest, for sounds whose loudest moment is timed onto what makes it, such
    /// as a lid shutting: measured a hundredth of a second at a time with the sound-cues skill's loudness tool.
    /// </summary>
    private static readonly Dictionary<string, float[]> Loudest = new() {
        ["close_chest"] = [.11f],
        ["hut_door_escaping"] = [.07f, .20f],
    };
    private const float MusicDb = -14;
    public const float MusicFadeSeconds = 4;
    private Playlist day = null!, night = null!;
    private readonly Dictionary<string, AudioStream[]> variants = new();
    private readonly Dictionary<string, int> previous = new();
    private Random random = new();
    public World World = null!;
    /// <summary>Counts every sound started, so a caller can tell whether an action already made one.</summary>
    public int Played { get; private set; }
    public string Last { get; private set; } = "";
    // The names of the last sounds started, the newest over the oldest.
    private readonly string[] recent = new string[32];
    /// <summary>
    /// Whether a sound of this name was started after <see cref="Played"/> read <paramref name="since"/>, whatever has
    /// played after it; it remembers the last 32 sounds.
    /// </summary>
    public bool Heard(string name, int since) {
        for (int i = Math.Max(since, Played - recent.Length); i < Played; i++) if (recent[i % recent.Length] == name) return true;
        return false;
    }
    public static IEnumerable<string> Names => Levels.Keys;
    /// <summary>A sound's place in the mix, in decibels.</summary>
    public static float Level(string name) => Levels.GetValueOrDefault(name);
    public int VariantCount(string name) => Variants(name).Length;
    public int LastVariant(string name) => previous.GetValueOrDefault(name, -1);
    /// <summary>Starts the picks of variant, pitch and volume over from a seed with no sound played before, so a check hears the same ones on every run.</summary>
    public void Reseed(int seed) { random = new Random(seed); previous.Clear(); }

    /// <summary>Music loudness from the options, 0 to 1.</summary>
    public float MusicVolume = 1;
    public bool DayMusicPlaying => day.Player.Playing;
    public string DayMusicTrack => day.Name;
    public float DayMusicPosition => day.Position;
    /// <summary>Jumps the current day track to its last moment, so it plays to its end without the wait.</summary>
    public void SkipToDayTrackEnd() => day.SkipToTrackEnd();
    public bool NightMusicPlaying => night.Player.Playing;
    public string NightMusicTrack => night.Name;
    public float NightMusicPosition => night.Position;
    /// <summary>Jumps the current night track to its last moment, so it plays to its end without the wait.</summary>
    public void SkipToNightTrackEnd() => night.SkipToTrackEnd();

    // Players made while loading and reused in turn, so a sound instantiates nothing mid-game.
    private readonly List<AudioStreamPlayer> voices = new();
    // A sound with a world position plays from there and pans with its place on screen.
    // Play sets its volume by its distance from the player, so these players neither fade nor muffle with distance themselves.
    private readonly List<AudioStreamPlayer3D> placedVoices = new();
    private const int VoiceCount = 24;
    private int nextVoice, nextPlacedVoice;
    // Players for held sounds, kept apart from the others so a burst of one-shots never cuts a held sound off.
    // One of each kind a player in a full game: a player holds at most one sound, with no place for their own and placed for another's.
    private const int HeldVoiceCount = Net.MaxPlayers;
    private readonly List<AudioStreamPlayer> heldVoices = new();
    private readonly List<AudioStreamPlayer3D> heldPlacedVoices = new();
    private readonly Dictionary<int, HeldSound> held = new();
    // Seconds a held sound takes to fade in when it starts and out once let go, so it neither clicks on nor cuts off.
    private const float HeldFadeSeconds = .15f;

    public override void _Ready() {
        for (int i = 0; i < VoiceCount; i++) { var voice = new AudioStreamPlayer(); AddChild(voice); voices.Add(voice); }
        for (int i = 0; i < VoiceCount; i++) placedVoices.Add(PlacedVoice());
        for (int i = 0; i < HeldVoiceCount; i++) {
            var voice = new AudioStreamPlayer(); AddChild(voice); heldVoices.Add(voice);
            heldPlacedVoices.Add(PlacedVoice());
        }
        day = new Playlist(this, "path_to_lake_land", "mystic_theme",
            "jazz_jazz_music", "jazz_bar", "lofi_soul", "groove_soul");
        night = new Playlist(this, "forest", "soft_jazz");
        // Loading a sound on its first play would stall that frame.
        foreach (string name in Levels.Keys) Variants(name);
    }

    private AudioStreamPlayer3D PlacedVoice() {
        var voice = new AudioStreamPlayer3D { AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, AttenuationFilterDb = 0, DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled };
        AddChild(voice);
        return voice;
    }

    public override void _Process(double delta) {
        UpdateMusic((float)delta);
        UpdateHeld((float)delta);
    }

    /// <summary>Fades the track for the current time of day in and the other out.</summary>
    public void UpdateMusic(float dt) {
        float volume = MusicDb + Db(MusicVolume);
        day.Update(!World.Night, dt, volume);
        night.Update(World.Night, dt, volume);
    }

    /// <summary>The inventory sound for moving an item by hand.</summary>
    public static string ForItem(string item) => item switch {
        Inventory.Coins => "move_coins_inventory",
        "raw_meat" or "burnt_steak" or "meat_stew" => "meat_inventory",
        "wood" or "stone" or "gravel" or "iron_ore" or "coal" or "charcoal" or "tin_ore" or "leaves" or "planks" or "wood_parts" or "wood_hammer" or "stone_chisel" or "torch" => "wood_rocks_inventory",
        _ when World.IsCrate(item) => "wood_rocks_inventory",
        _ when item.StartsWith("iron_") || item.StartsWith("steel_") || item.StartsWith("tin_") || item.StartsWith("canned_") || item == "rake" => "pickup_gold",
        _ => "moving_leather_in_inventory"
    };

    private static bool IsHowl(string name) => name is "wolf_howl" or "wolf_howl_group";
    /// <summary>
    /// Metres from the player at which a sound falls silent.
    /// A howl carries across the valley, to where wolves stop hearing it.
    /// A wolf's growl as it breaks into its sprint carries twice as far as the wolf sees, so it plays at a quarter of its volume from where the sprint starts.
    /// </summary>
    private float ReachOf(string name) => IsHowl(name) ? World.Data.HowlRadius : name == "wolf_growl_distance" ? 2 * World.Data.WolfSightRange : Reach;
    // A howl keeps its pitch, so it lasts exactly as long as the wolf holds its howl, and the mortar's, so its tap lands as the pestle comes down.
    private static bool KeepsPitch(string name) => IsHowl(name) || name == "grind_mortar";
    /// <summary>The volume scale of the last sound started, from one at full volume.</summary>
    public float LastFade { get; private set; }
    /// <summary>Whether the last sound started plays from a place in the world, panning with it.</summary>
    public bool LastPlaced { get; private set; }
    /// <summary>Seconds the longest variant of a sound plays.</summary>
    public double Length(string name) => Variants(name).Max(stream => stream.GetLength());

    /// <summary>
    /// Plays a sound; one with a world position fades with its distance from the player, to silence at its reach.
    /// It pans with its place on screen, unless it is at the player's own spot.
    /// A <paramref name="pitch"/> below one plays it lower and slower, as from something bigger.
    /// </summary>
    public void Play(string name, Vector2? at = null, float pitch = 1) {
        if (Audible(name, at)) Play(Next(name, pitch), at);
    }

    /// <summary>Whether a sound would be heard from <paramref name="at"/>: it has a recording, sound is on and the player is within its reach.</summary>
    private bool Audible(string name, Vector2? at) => Variants(name).Length > 0 && !silenced && World.Player.DistanceTo(at ?? World.Player) < ReachOf(name);

    /// <summary>One play of a sound: which variant and at what pitch.</summary>
    public readonly record struct Cue(string Name, int Variant, float Pitch);

    /// <summary>
    /// Picks the variant and pitch the next play of a sound has, so a caller can start it <see cref="Lead"/> seconds ahead
    /// of the moment its loudest part marks; a variant other than the previous one, its pitch nudged unless it keeps it.
    /// </summary>
    public Cue Next(string name, float pitch = 1) {
        int count = Variants(name).Length;
        int pick = count == 0 ? 0 : random.Next(count - (count > 1 && previous.ContainsKey(name) ? 1 : 0));
        if (previous.TryGetValue(name, out int last) && count > 1 && pick >= last) pick++;
        previous[name] = pick;
        if (!KeepsPitch(name)) pitch *= 1 + (float)(random.NextDouble() * .12 - .06);
        return new Cue(name, pick, pitch);
    }

    /// <summary>Seconds from a play's start to its loudest moment, or zero for a sound not timed that way.</summary>
    public static float Lead(Cue cue) => Loudest.TryGetValue(cue.Name, out var peaks) ? peaks[cue.Variant] / cue.Pitch : 0;

    /// <summary>Plays a picked play of a sound, as <see cref="Play(string, Vector2?, float)"/> does.</summary>
    public void Play(Cue cue, Vector2? at = null) {
        if (!Audible(cue.Name, at)) return;
        var streams = Variants(cue.Name);
        var place = at ?? World.Player;
        float fade = DistanceFade(cue.Name, place);
        bool placed = World.Player.DistanceTo(place) > OwnSpot;
        float volume = Levels.GetValueOrDefault(cue.Name) + Mathf.LinearToDb(fade) - Nudge();
        if (placed) {
            var source = FreeVoice(placedVoices, ref nextPlacedVoice, static voice => voice.Playing);
            source.Position = new Vector3(place.X, 0, place.Y);
            source.Stream = streams[cue.Variant]; source.PitchScale = cue.Pitch; source.VolumeDb = volume;
            source.Play();
        } else {
            var player = FreeVoice(voices, ref nextVoice, static voice => voice.Playing);
            player.Stream = streams[cue.Variant]; player.PitchScale = cue.Pitch; player.VolumeDb = volume;
            player.Play();
        }
        Started(cue.Name, fade, placed);
    }

    /// <summary>The next idle player in turn; with all of them busy, the next in turn is cut off.</summary>
    private static T FreeVoice<T>(List<T> pool, ref int next, Func<T, bool> playing) {
        for (int i = 0; i < pool.Count; i++) {
            var voice = pool[(next + i) % pool.Count];
            if (playing(voice)) continue;
            next = (next + i + 1) % pool.Count;
            return voice;
        }
        var oldest = pool[next];
        next = (next + 1) % pool.Count;
        return oldest;
    }

    /// <summary>The volume scale of a sound played at <paramref name="place"/>, from one at the player to zero at its reach.</summary>
    private float DistanceFade(string name, Vector2 place) => MathF.Pow(1 - Math.Min(1, World.Player.DistanceTo(place) / ReachOf(name)), 2);

    /// <summary>A volume scale in decibels, with zero as silence.</summary>
    private static float Db(float scale) => Mathf.LinearToDb(Math.Max(scale, .0001f));

    /// <summary>Decibels taken off one play, so repeats of a sound differ a little in volume.</summary>
    private float Nudge() => (float)random.NextDouble() * 1.5f;

    private void Started(string name, float fade, bool placed) {
        recent[Played % recent.Length] = name;
        Played++; Last = name; LastFade = fade; LastPlaced = placed;
    }

    /// <summary>
    /// Keeps a looping sound going for <paramref name="source"/>, one sound a source; the caller holds it again every frame it
    /// should go on, and once it stops it fades out.
    /// It starts at a random moment of its recording, fading in, and plays from <paramref name="at"/>, fading with distance
    /// as <see cref="Play(string, Vector2?, float)"/> does, or with no place when that is null.
    /// Unlike a one-shot it keeps its place even within a metre of the player, since it follows its source as it moves.
    /// Its recording is a WAV imported to loop, since a WAV starts anywhere in it at no cost.
    /// </summary>
    public void Hold(string name, int source, Vector2? at = null) {
        if (held.TryGetValue(source, out var sound) && sound.Name == name) { sound.At = at; sound.Kept = true; return; }
        if (sound != null) Let(source);
        if (!Audible(name, at)) return;
        var own = at == null ? heldVoices.FirstOrDefault(Free) : null;
        var placed = at == null ? null : heldPlacedVoices.FirstOrDefault(Free);
        if (own == null && placed == null) return;
        var cue = Next(name);
        var stream = Variants(name)[cue.Variant];
        held[source] = sound = new HeldSound(name, own, placed, Nudge()) { At = at, Kept = true };
        Place(sound);
        sound.Start(stream, cue.Pitch, (float)(random.NextDouble() * stream.GetLength()));
        Started(name, DistanceFade(name, at ?? World.Player), at != null);
    }

    /// <summary>Whether a sound of this name is playing as a held sound, fading out included.</summary>
    public bool Holding(string name) => held.Values.Any(sound => sound.Name == name && sound.Playing);

    private bool Free(Node voice) => !held.Values.Any(sound => sound.Own == voice || sound.Placed == voice);

    /// <summary>Fades each held sound in while held and out once let go, and follows its source as it moves.</summary>
    private void UpdateHeld(float dt) {
        foreach (var (source, sound) in held) {
            sound.Fade = Mathf.MoveToward(sound.Fade, sound.Kept ? 1 : 0, dt / HeldFadeSeconds);
            if (!sound.Kept && sound.Fade == 0) Let(source);
            else Place(sound);
            sound.Kept = false;
        }
    }

    /// <summary>Sets a held sound's volume from its fade and its distance from the player, and moves it to its source.</summary>
    private void Place(HeldSound sound) {
        sound.Set(Levels.GetValueOrDefault(sound.Name) + Db(sound.Fade * DistanceFade(sound.Name, sound.At ?? World.Player)) - sound.Nudge);
    }

    /// <summary>Stops a held sound at once and frees its player.</summary>
    private void Let(int source) {
        if (held.Remove(source, out var sound)) sound.Stop();
    }

    /// <summary>A looping sound one source holds, on a player of its own: one with no place, or one placed in the world.</summary>
    private sealed class HeldSound(string name, AudioStreamPlayer? own, AudioStreamPlayer3D? placed, float nudge) {
        public readonly string Name = name;
        public readonly AudioStreamPlayer? Own = own;
        public readonly AudioStreamPlayer3D? Placed = placed;
        /// <summary>Decibels taken off this play, as <see cref="Sounds.Nudge"/> takes off a one-shot.</summary>
        public readonly float Nudge = nudge;
        /// <summary>Where the source is, or null for the player's own spot.</summary>
        public Vector2? At;
        /// <summary>Volume scale of the fade in and out, from zero to one.</summary>
        public float Fade;
        /// <summary>Held again since the last update.</summary>
        public bool Kept;

        public void Start(AudioStream stream, float pitch, float from) {
            if (Own != null) { Own.Stream = stream; Own.PitchScale = pitch; Own.Play(from); }
            if (Placed != null) { Placed.Stream = stream; Placed.PitchScale = pitch; Placed.Play(from); }
        }

        /// <summary>Sets the volume, and moves a placed sound to where its source is.</summary>
        public void Set(float volume) {
            if (Own != null) Own.VolumeDb = volume;
            if (Placed == null) return;
            Placed.VolumeDb = volume;
            if (At is Vector2 at) Placed.Position = new Vector3(at.X, 0, at.Y);
        }

        public void Stop() { Own?.Stop(); Placed?.Stop(); }

        public bool Playing => Own?.Playing == true || Placed?.Playing == true;
    }

    private bool silenced;

    /// <summary>
    /// Stops every sound, music included, for good ahead of quitting.
    /// The audio thread releases a stopped stream only on its next mix, so quitting must wait a moment after this, or Godot reports the stream as leaked.
    /// </summary>
    public void StopAll() {
        silenced = true;
        ProcessMode = ProcessModeEnum.Disabled;
        foreach (var player in GetChildren().OfType<AudioStreamPlayer>()) player.Stop();
        foreach (var player in placedVoices) player.Stop();
        foreach (var player in heldPlacedVoices) player.Stop();
        held.Clear();
    }

    /// <summary>
    /// A music playlist that pauses once faded out, so it carries on from the same place next time.
    /// Its tracks play in order and wrap to the first after the last; a single track loops seamlessly.
    /// It pauses rather than stopping because starting an MP3 partway through decodes it from the beginning, which stalls the frame for longer the further in it starts.
    /// </summary>
    private sealed class Playlist {
        public readonly AudioStreamPlayer Player;
        private readonly AudioStreamMP3[] streams;
        private float level, at;
        private bool started;
        private int index;
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Player.Stream.ResourcePath);
        public float Position => Player.Playing ? Player.GetPlaybackPosition() : at;

        public Playlist(Node parent, params string[] names) {
            streams = names.Select(name => GD.Load<AudioStreamMP3>($"res://assets/music/{name}.mp3")).ToArray();
            streams[0].Loop = streams.Length == 1;
            Player = new AudioStreamPlayer { Stream = streams[0] };
            parent.AddChild(Player);
        }

        public void SkipToTrackEnd() => Player.Seek((float)Player.Stream.GetLength() - .1f);

        public void Update(bool wanted, float dt, float volume) {
            level = Mathf.MoveToward(level, wanted ? 1 : 0, dt / MusicFadeSeconds);
            if (level > 0) {
                if (Player.StreamPaused) Player.StreamPaused = false;
                else if (!Player.Playing) {
                    // A player that has started and is neither playing nor paused has reached the end of its track, so the next one follows.
                    // The player's own finished signal comes too late for this: it fires after this update has already started the track again.
                    if (started) { index = (index + 1) % streams.Length; Player.Stream = streams[index]; }
                    Player.Play(); started = true;
                }
            } else if (Player.Playing) {
                at = Player.GetPlaybackPosition();
                Player.StreamPaused = true;
            }
            Player.VolumeDb = volume + Db(level);
        }
    }

    private AudioStream[] Variants(string name) {
        if (variants.TryGetValue(name, out var cached)) return cached;
        string? single = Formats.Select(format => $"{Folder}/{name}.{format}").FirstOrDefault(path => ResourceLoader.Exists(path));
        var found = single != null
            ? new[] { single }
            : Enumerable.Range(1, 9).Select(i => $"{Folder}/{name}_{i}.mp3").TakeWhile(path => ResourceLoader.Exists(path)).ToArray();
        if (found.Length == 0) GD.PushError($"Missing sound: {name}");
        if (Loudest.TryGetValue(name, out var peaks) && peaks.Length != found.Length)
            GD.PushError($"Sound {name} has {found.Length} recordings but {peaks.Length} loudest moments");
        var streams = variants[name] = found.Select(path => GD.Load<AudioStream>(path)).ToArray();
        // A WAV is a held sound's, which loops.
        if (streams.Any(stream => stream is AudioStreamWav { LoopMode: not AudioStreamWav.LoopModeEnum.Forward }))
            GD.PushError($"Held sound {name} does not loop: set edit/loop_mode=2 in its .import file");
        return streams;
    }
}
