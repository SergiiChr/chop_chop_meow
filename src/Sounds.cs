using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One-shot sound effects from <c>assets/sounds</c>.
/// A sound named <c>x</c> is <c>x.mp3</c>, or the variants <c>x_1.mp3</c>, <c>x_2.mp3</c> and so on.
/// Each play picks a variant other than the previous one and nudges its pitch and volume, so repeated actions do not sound mechanical.
/// </summary>
public partial class Sounds : Node
{
    private const string Folder = "res://assets/sounds";
    // Audibility follows the camera, so zooming out lets the player hear further.
    // On-screen sounds play at full volume; past the screen edge they drop to a faint level and fade out over the play's reach.
    private const float EdgeVolume = .3f;
    public const float Reach = 5;
    // Variants share a loudness, and these levels set each sound's place in the mix: quiet steps, then impacts and handling, then alerts.
    private static readonly Dictionary<string, float> Levels = new()
    {
        ["footsteps_default"] = -13,
        ["footsteps_rocks"] = -17,
        ["footsteps_water"] = -15,
        ["chop_tree"] = -7.5f,
        ["anvil_work"] = -2,
        ["tool_air_swing"] = -11,
        ["open_chest"] = -8,
        ["close_chest"] = -10,
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
        ["start_fire"] = -9,
        ["payment_from_village_arrives"] = -6,
        ["wolf_growl_distance"] = -11,
        ["wolf_growl_approaching"] = -3,
        ["wolf_bite"] = -3,
    };
    private readonly Dictionary<string, AudioStream[]> variants = new();
    private readonly Dictionary<string, int> previous = new();
    private readonly Random random = new();
    public World World = null!;
    /// <summary>How many tiles a world position lies outside the visible screen; zero when on screen.</summary>
    public Func<Vector2, float> OffScreen = _ => 0;
    /// <summary>Counts every sound started, so a caller can tell whether an action already made one.</summary>
    public int Played { get; private set; }
    public string Last { get; private set; } = "";
    public static IEnumerable<string> Names => Levels.Keys;
    public int VariantCount(string name) => Variants(name).Length;
    public int LastVariant(string name) => previous.GetValueOrDefault(name, -1);

    /// <summary>The inventory sound for moving an item by hand.</summary>
    public static string ForItem(string item) => item switch
    {
        Inventory.Coins => "move_coins_inventory",
        "raw_meat" or "burnt_steak" or "meat_stew" => "meat_inventory",
        "wood" or "stone" or "iron_ore" or "coal" or "tin_ore" or "leaves" or "planks" or "wood_parts" or "wood_hammer" or "stone_chisel" or "torch" => "wood_rocks_inventory",
        _ when World.IsCrate(item) => "wood_rocks_inventory",
        _ when item.StartsWith("iron_") || item.StartsWith("steel_") || item.StartsWith("tin_") || item.StartsWith("canned_") || item == "rake" => "pickup_gold",
        _ => "moving_leather_in_inventory"
    };

    /// <summary>Plays a sound; one with a world position is faint off screen and silent beyond <paramref name="reach"/> tiles past the screen edge.</summary>
    public void Play(string name, Vector2? at = null, float reach = Reach)
    {
        var streams = Variants(name);
        if (streams.Length == 0) return;
        float outside = at is Vector2 p ? OffScreen(p) : 0;
        float fade = outside <= 0 ? 1 : EdgeVolume * MathF.Pow(1 - Math.Min(1, outside / reach), 2);
        if (fade <= 0) return;
        int pick = random.Next(streams.Length - (streams.Length > 1 && previous.ContainsKey(name) ? 1 : 0));
        if (previous.TryGetValue(name, out int last) && streams.Length > 1 && pick >= last) pick++;
        previous[name] = pick;
        var player = new AudioStreamPlayer
        {
            Stream = streams[pick],
            PitchScale = 1 + (float)(random.NextDouble() * .12 - .06),
            VolumeDb = Levels.GetValueOrDefault(name) + Mathf.LinearToDb(fade) - (float)random.NextDouble() * 1.5f
        };
        AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
        Played++; Last = name;
    }

    /// <summary>
    /// Stops every sound.
    /// The audio thread releases a stopped stream only on its next mix, so quitting must wait a moment after this, or Godot reports the stream as leaked.
    /// </summary>
    public void StopAll()
    {
        foreach (var player in GetChildren().OfType<AudioStreamPlayer>()) player.Stop();
    }

    private AudioStream[] Variants(string name)
    {
        if (variants.TryGetValue(name, out var cached)) return cached;
        string single = $"{Folder}/{name}.mp3";
        var found = ResourceLoader.Exists(single)
            ? new[] { single }
            : Enumerable.Range(1, 9).Select(i => $"{Folder}/{name}_{i}.mp3").TakeWhile(path => ResourceLoader.Exists(path)).ToArray();
        if (found.Length == 0) GD.PushError($"Missing sound: {name}");
        return variants[name] = found.Select(path => GD.Load<AudioStream>(path)).ToArray();
    }
}
