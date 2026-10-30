using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>What a world object is. Saves, network updates and data/game.json name each kind by its id, the member name in snake case, such as "wolf_corpse".</summary>
[JsonConverter(typeof(KindConverter))]
public enum ThingKind {
    Tree,
    Stone,
    Berries,
    Water,
    Leaves,
    WolfCorpse,
    Iron,
    Coal,
    Tin,
    LooseCoal,
    Chest,
    Campfire,
    Hut,
    Furnace,
    Anvil,
    Bench,
    Reinforced,
    Mine,
    Pile,
    Farm,
    Toolstation,
    Cooktable,
    Cannery,
    Packer,
    Signpost,
    Grave,
    Crate,
}

/// <summary>Converts kinds to and from the ids that hotbar slots, icons and sounds use.</summary>
public static class Kinds {
    private static readonly string[] ids;
    private static readonly Dictionary<string, ThingKind> byId = new();
    public static readonly ThingKind[] All = Enum.GetValues<ThingKind>();

    static Kinds() {
        ids = new string[All.Length];
        foreach (var kind in All) {
            ids[(int)kind] = JsonNamingPolicy.SnakeCaseLower.ConvertName(kind.ToString());
            byId[ids[(int)kind]] = kind;
        }
    }

    /// <summary>The kind's id, such as "wolf_corpse"; cached, so asking every frame allocates nothing.</summary>
    public static string Id(this ThingKind kind) => ids[(int)kind];

    public static bool TryParse(string? id, out ThingKind kind) {
        if (id != null && byId.TryGetValue(id, out kind)) return true;
        kind = default;
        return false;
    }

    /// <summary>The kind an id names, or null for an item id or an empty slot.</summary>
    public static ThingKind? Parse(string? id) => TryParse(id, out var kind) ? kind : null;
}

/// <summary>Reads and writes a kind as its id.</summary>
public sealed class KindConverter() : JsonStringEnumConverter<ThingKind>(JsonNamingPolicy.SnakeCaseLower);

/// <summary>Reads an optional kind, where an empty id means none, and writes none as an empty id.</summary>
public sealed class OptionalKind : JsonConverter<ThingKind?> {
    public override bool HandleNull => true;

    public override ThingKind? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var id = reader.GetString();
        return id == "" ? null : Kinds.TryParse(id, out var kind) ? kind : throw new JsonException($"Unknown kind: {id}");
    }

    public override void Write(Utf8JsonWriter writer, ThingKind? value, JsonSerializerOptions options) => writer.WriteStringValue(value?.Id() ?? "");
}
