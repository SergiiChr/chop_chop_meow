using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ChopChopMeow;

/// <summary>Loads and maps the named player and worker animation atlases.</summary>
public sealed class ActorClips
{
    private readonly IReadOnlyDictionary<string, ActorClip> clips;

    /// <summary>Named clips for animation review and asset inspection.</summary>
    public IReadOnlyDictionary<string, ActorClip> NamedClips => clips;

    private ActorClips(Dictionary<string, ActorClip> loaded) => clips = new ReadOnlyDictionary<string, ActorClip>(loaded);

    public static ActorClips Load()
    {
        var loaded = new Dictionary<string, ActorClip>(StringComparer.Ordinal);
        using var file = Godot.FileAccess.Open("res://data/animations.json", Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException("Missing baked animation manifest.");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, ClipData>>(file.GetAsText())
            ?? throw new InvalidOperationException("Invalid baked animation manifest.");
        foreach (var entry in manifest) loaded.Add(entry.Key, ActorClip.Load(entry.Key, entry.Value));
        return new ActorClips(loaded);
    }

    public ActorClip Get(bool worker, ActorAction action)
    {
        string name = (worker, action) switch
        {
            (false, ActorAction.Walk) => "player-walk",
            (false, ActorAction.Chop) => "player-axe",
            (false, ActorAction.Mine) => "player-pick",
            (false, ActorAction.SteelChop) => "player-steel-axe",
            (false, ActorAction.SteelMine) => "player-steel-pick",
            (false, ActorAction.Hammer) => "player-hammer",
            (false, ActorAction.Pump) => "player-pump",
            (false, ActorAction.Gather) => "player-gather",
            (false, ActorAction.Attack) => "player-attack",
            (true, ActorAction.Walk) => "worker-walk",
            (true, ActorAction.Chop) => "worker-axe",
            (true, ActorAction.SteelChop) => "worker-axe",
            (true, ActorAction.Mine) => "worker-pick",
            (true, ActorAction.SteelMine) => "worker-pick",
            (true, ActorAction.Carry) => "worker-carry",
            (true, ActorAction.Pump) => "worker-pump",
            (true, ActorAction.Hammer) => "worker-hammer",
            (false, ActorAction.Idle) => "player-walk",
            (true, ActorAction.Idle or ActorAction.Attack) => "worker-walk",
            (true, ActorAction.Gather) => "worker-gather",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown actor action.")
        };
        return clips[name];
    }
}
