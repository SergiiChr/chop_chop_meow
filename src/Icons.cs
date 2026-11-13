using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Item and building icons, loaded the first time each is asked for and kept.</summary>
public static class Icons {
    private static readonly Dictionary<string, Texture2D> cache = new();

    public static Texture2D? Of(string id) {
        if (World.IsCrate(id)) id = "crate";
        if (cache.TryGetValue(id, out var cached)) return cached;
        // Items and buildings have icons rendered from their models; the hard hat is a drawn icon.
        foreach (var path in new[] { $"res://assets/icons/{id}.png", $"res://assets/ui/{id}.svg" })
            if (ResourceLoader.Exists(path)) return cache[id] = GD.Load<Texture2D>(path);
        return null;
    }
}
