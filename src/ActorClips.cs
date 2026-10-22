using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChopChopMeow;

/// <summary>Loads and maps the named player and worker animation atlases.</summary>
public sealed class ActorClips
{
    private static readonly (string Name, string Path)[] Assets =
    {
        ("player-walk", "res://assets/animations/player-walk.png"),
        ("worker-walk", "res://assets/animations/worker-walk.png"),
        ("player-axe", "res://assets/animations/player-axe.png"),
        ("player-pick", "res://assets/animations/player-pick.png"),
        ("player-steel-axe", "res://assets/animations/player-steel-axe.png"),
        ("player-steel-pick", "res://assets/animations/player-steel-pick.png"),
        ("worker-axe", "res://assets/animations/worker-axe.png"),
        ("worker-pick", "res://assets/animations/worker-pick.png"),
        ("worker-pump", "res://assets/animations/worker-pump.png"),
        ("worker-carry", "res://assets/animations/worker-carry.png"),
        ("worker-hammer", "res://assets/animations/worker-hammer.png"),
        ("player-hammer", "res://assets/animations/player-hammer.png"),
        ("player-pump", "res://assets/animations/player-pump.png")
    };

    private readonly IReadOnlyDictionary<string, ActorClip> clips;

    /// <summary>Named clips for animation review and asset inspection.</summary>
    public IReadOnlyDictionary<string, ActorClip> NamedClips => clips;

    private ActorClips(Dictionary<string, ActorClip> loaded) => clips = new ReadOnlyDictionary<string, ActorClip>(loaded);

    public static ActorClips Load()
    {
        var loaded = new Dictionary<string, ActorClip>(StringComparer.Ordinal);
        foreach ((string name, string path) in Assets) loaded.Add(name, ActorClip.Load(path));
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
            (false, ActorAction.Gather or ActorAction.Attack) => "player-pump",
            (true, ActorAction.Walk) => "worker-walk",
            (true, ActorAction.Chop) => "worker-axe",
            (true, ActorAction.SteelChop) => "worker-axe",
            (true, ActorAction.Mine) => "worker-pick",
            (true, ActorAction.SteelMine) => "worker-pick",
            (true, ActorAction.Carry) => "worker-carry",
            (true, ActorAction.Pump) => "worker-pump",
            (true, ActorAction.Hammer) => "worker-hammer",
            (false, ActorAction.Idle) => "player-walk",
            (true, ActorAction.Idle or ActorAction.Gather or ActorAction.Attack) => "worker-walk",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown actor action.")
        };
        return clips[name];
    }
}
