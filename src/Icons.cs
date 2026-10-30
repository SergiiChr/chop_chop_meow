using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Item and building icons, loaded the first time each is asked for and kept.</summary>
public static class Icons {
    private static readonly Dictionary<string, Texture2D> cache = new();
    private static readonly Texture2D? Atlas = ResourceLoader.Exists("res://assets/item-atlas.png") ? GD.Load<Texture2D>("res://assets/item-atlas.png") : null;

    public static Texture2D? Of(string id) {
        if (World.IsCrate(id)) id = "crate";
        if (cache.TryGetValue(id, out var cached)) return cached;
        // Icons rendered from the game's own models come first; items without a model have drawn icons named by their id.
        foreach (var path in new[] { $"res://assets/icons/{id}.png", $"res://assets/ui/{id}.svg" })
            if (ResourceLoader.Exists(path)) return cache[id] = GD.Load<Texture2D>(path);
        int cell = id == "water" ? 10 : -1;
        if (Atlas == null || cell < 0) return null;
        const int Columns = 4, Rows = 3;
        float width = Atlas.GetWidth() / (float)Columns, height = Atlas.GetHeight() / (float)Rows;
        return cache[id] = new AtlasTexture { Atlas = Atlas, Region = new Rect2(cell % Columns * width, cell / Columns * height, width, height) };
    }
}
