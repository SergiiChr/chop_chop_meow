using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One-shot sound effects from <c>assets/sounds</c>.
/// A sound named <c>x</c> is <c>x.mp3</c>, or the variants <c>x_1.mp3</c>, <c>x_2.mp3</c> and so on.
/// Each play picks a variant other than the previous one and nudges its pitch and volume, so repeated actions do not sound mechanical.
/// Day and night music crossfade when the time of day changes, and each resumes where it last stopped.
/// </summary>
public partial class Sounds : Node {
    private const string Folder = "res://assets/sounds";
    // Audibility follows the camera, so zooming out lets the player hear further.
    // On-screen sounds play at full volume; past the screen edge they drop to a faint level and fade out over the play's reach.
    private const float EdgeVolume = .3f;
    public const float Reach = 5;
    // Variants share a loudness, and these levels set each sound's place in the mix: quiet steps, then impacts and handling, then alerts.
    private static readonly Dictionary<string, float> Levels = new() {
        ["footsteps_default"] = -13,
        ["footsteps_rocks"] = -17,
        ["footsteps_water"] = -15,
        ["chop_tree"] = -7.5f,
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
        ["butcher_carcass"] = -6.5f,
        ["place_building"] = -6.5f,
        ["take_down_building"] = -4.5f,
        ["tool_air_swing"] = -11,
        ["open_chest"] = -11,
        ["close_chest"] = -13,
        ["hut_door_normal"] = -6.5f,
        ["hut_door_escaping"] = -8,
        ["open_steel_smelter"] = -9,
        ["meat_inventory"] = -6.5f,
        ["move_coins_inventory"] = -1,
        ["moving_leather_in_inventory"] = -9,
        ["wood_rocks_inventory"] = -9,
        ["pickup_gold"] = -10,
        ["putting_items_on_hotbar"] = -3,
        ["eat_berry"] = -7.5f,
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
        ["wolf_hit"] = -5.5f,
    };
    private const float MusicDb = -14;
    public const float MusicFadeSeconds = 4;
    private Track day = null!, night = null!;
    private readonly Dictionary<string, AudioStream[]> variants = new();
    private readonly Dictionary<string, int> previous = new();
    private Random random = new();
    public World World = null!;
    /// <summary>How many tiles a world position lies outside the visible screen; zero when on screen.</summary>
    public Func<Vector2, float> OffScreen = _ => 0;
    /// <summary>Counts every sound started, so a caller can tell whether an action already made one.</summary>
    public int Played { get; private set; }
    public string Last { get; private set; } = "";
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
    public int DayMusicIndex => day.Index;
    /// <summary>Jumps the current day track to its last moment, so it plays to its end without the wait.</summary>
    public void SkipToDayTrackEnd() => day.Player.Seek((float)day.Player.Stream.GetLength() - .1f);
    public bool NightMusicPlaying => night.Player.Playing;
    public float NightMusicPosition => night.Position;

    // Players made while loading and reused in turn, so a sound instantiates nothing mid-game.
    private readonly List<AudioStreamPlayer> voices = new();
    // A sound with a world position plays from there and pans with its place on screen.
    // Distance is left to the existing on-screen rule, so these players neither fade nor muffle with distance themselves.
    private readonly List<AudioStreamPlayer3D> placedVoices = new();
    private const int VoiceCount = 24;
    private int nextVoice, nextPlacedVoice;

    public override void _Ready() {
        for (int i = 0; i < VoiceCount; i++) { var voice = new AudioStreamPlayer(); AddChild(voice); voices.Add(voice); }
        for (int i = 0; i < VoiceCount; i++) {
            var voice = new AudioStreamPlayer3D { AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, AttenuationFilterDb = 0, DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled };
            AddChild(voice); placedVoices.Add(voice);
        }
        day = new Track(this, "path_to_lake_land", "mystic_theme");
        night = new Track(this, "forest");
        // Loading a sound on its first play would stall that frame.
        foreach (string name in Levels.Keys) Variants(name);
    }

    public override void _Process(double delta) => UpdateMusic((float)delta);

    /// <summary>Fades the track for the current time of day in and the other out.</summary>
    public void UpdateMusic(float dt) {
        float volume = MusicDb + Mathf.LinearToDb(Math.Max(MusicVolume, .0001f));
        day.Update(!World.Night, dt, volume);
        night.Update(World.Night, dt, volume);
    }

    /// <summary>The inventory sound for moving an item by hand.</summary>
    public static string ForItem(string item) => item switch {
        Inventory.Coins => "move_coins_inventory",
        "raw_meat" or "burnt_steak" or "meat_stew" => "meat_inventory",
        "wood" or "stone" or "iron_ore" or "coal" or "charcoal" or "tin_ore" or "leaves" or "planks" or "wood_parts" or "wood_hammer" or "stone_chisel" or "torch" => "wood_rocks_inventory",
        _ when World.IsCrate(item) => "wood_rocks_inventory",
        _ when item.StartsWith("iron_") || item.StartsWith("steel_") || item.StartsWith("tin_") || item.StartsWith("canned_") || item == "rake" => "pickup_gold",
        _ => "moving_leather_in_inventory"
    };

    // A howl carries across the valley: it fades with distance from the player, on screen too, and falls silent where wolves stop hearing it.
    private static bool Carries(string name) => name is "wolf_howl" or "wolf_howl_group";
    // A howl keeps its pitch, so it lasts exactly as long as the wolf holds its howl, and the mortar's, so its tap lands as the pestle comes down.
    private static bool KeepsPitch(string name) => Carries(name) || name == "grind_mortar";
    /// <summary>The volume scale of the last sound started, from one at full volume.</summary>
    public float LastFade { get; private set; }
    /// <summary>Seconds the longest variant of a sound plays.</summary>
    public double Length(string name) => Variants(name).Max(stream => stream.GetLength());

    /// <summary>
    /// Plays a sound; one with a world position pans with its place on screen.
    /// A howl fades with distance from the player; any other placed sound is faint off screen and silent beyond <paramref name="reach"/> tiles past the screen edge.
    /// </summary>
    public void Play(string name, Vector2? at = null, float reach = Reach) {
        var streams = Variants(name);
        if (streams.Length == 0 || silenced) return;
        float fade = at is not Vector2 p ? 1
            : Carries(name) ? MathF.Pow(1 - Math.Min(1, World.Player.DistanceTo(p) / World.Data.HowlRadius), 2)
            : OffScreen(p) is var outside && outside <= 0 ? 1 : EdgeVolume * MathF.Pow(1 - Math.Min(1, outside / reach), 2);
        if (fade <= 0) return;
        int pick = random.Next(streams.Length - (streams.Length > 1 && previous.ContainsKey(name) ? 1 : 0));
        if (previous.TryGetValue(name, out int last) && streams.Length > 1 && pick >= last) pick++;
        previous[name] = pick;
        float pitch = KeepsPitch(name) ? 1 : 1 + (float)(random.NextDouble() * .12 - .06);
        float volume = Levels.GetValueOrDefault(name) + Mathf.LinearToDb(fade) - (float)random.NextDouble() * 1.5f;
        if (at is Vector2 place) {
            var placed = FreeVoice(placedVoices, ref nextPlacedVoice, static voice => voice.Playing);
            placed.Position = new Vector3(place.X, 0, place.Y);
            placed.Stream = streams[pick]; placed.PitchScale = pitch; placed.VolumeDb = volume;
            placed.Play();
        } else {
            var player = FreeVoice(voices, ref nextVoice, static voice => voice.Playing);
            player.Stream = streams[pick]; player.PitchScale = pitch; player.VolumeDb = volume;
            player.Play();
        }
        Played++; Last = name; LastFade = fade;
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
    }

    /// <summary>
    /// A music playlist that pauses once faded out, so it carries on from the same place next time.
    /// Its tracks play in order and wrap to the first after the last; a single track loops seamlessly.
    /// It pauses rather than stopping because starting an MP3 partway through decodes it from the beginning, which stalls the frame for longer the further in it starts.
    /// </summary>
    private sealed class Track {
        public readonly AudioStreamPlayer Player;
        private readonly AudioStreamMP3[] streams;
        private float level, at;
        private bool started;
        public int Index { get; private set; }
        public float Position => Player.Playing ? Player.GetPlaybackPosition() : at;

        public Track(Node parent, params string[] names) {
            streams = names.Select(name => GD.Load<AudioStreamMP3>($"res://assets/music/{name}.mp3")).ToArray();
            streams[0].Loop = streams.Length == 1;
            Player = new AudioStreamPlayer { Stream = streams[0] };
            parent.AddChild(Player);
        }

        public void Update(bool wanted, float dt, float volume) {
            level = Mathf.MoveToward(level, wanted ? 1 : 0, dt / MusicFadeSeconds);
            if (level > 0) {
                if (Player.StreamPaused) Player.StreamPaused = false;
                else if (!Player.Playing) {
                    // A player that has started and is neither playing nor paused has reached the end of its track, so the next one follows.
                    // The player's own finished signal comes too late for this: it fires after this update has already started the track again.
                    if (started) { Index = (Index + 1) % streams.Length; Player.Stream = streams[Index]; }
                    Player.Play(); started = true;
                }
            } else if (Player.Playing) {
                at = Player.GetPlaybackPosition();
                Player.StreamPaused = true;
            }
            Player.VolumeDb = volume + Mathf.LinearToDb(Math.Max(level, .0001f));
        }
    }

    private AudioStream[] Variants(string name) {
        if (variants.TryGetValue(name, out var cached)) return cached;
        string single = $"{Folder}/{name}.mp3";
        var found = ResourceLoader.Exists(single)
            ? new[] { single }
            : Enumerable.Range(1, 9).Select(i => $"{Folder}/{name}_{i}.mp3").TakeWhile(path => ResourceLoader.Exists(path)).ToArray();
        if (found.Length == 0) GD.PushError($"Missing sound: {name}");
        return variants[name] = found.Select(path => GD.Load<AudioStream>(path)).ToArray();
    }
}
