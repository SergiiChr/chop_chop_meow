using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>What a world object is. Saves, network updates and data/game.json name each kind by its id, such as "wolf_corpse".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThingKind>))]
public enum ThingKind {
    [JsonStringEnumMemberName("tree")] Tree,
    [JsonStringEnumMemberName("stone")] Stone,
    [JsonStringEnumMemberName("berries")] Berries,
    [JsonStringEnumMemberName("water")] Water,
    [JsonStringEnumMemberName("leaves")] Leaves,
    [JsonStringEnumMemberName("wolf_corpse")] WolfCorpse,
    [JsonStringEnumMemberName("iron")] Iron,
    [JsonStringEnumMemberName("coal")] Coal,
    [JsonStringEnumMemberName("tin")] Tin,
    [JsonStringEnumMemberName("loose_coal")] LooseCoal,
    [JsonStringEnumMemberName("chest")] Chest,
    [JsonStringEnumMemberName("campfire")] Campfire,
    [JsonStringEnumMemberName("hut")] Hut,
    [JsonStringEnumMemberName("furnace")] Furnace,
    [JsonStringEnumMemberName("anvil")] Anvil,
    [JsonStringEnumMemberName("bench")] Bench,
    [JsonStringEnumMemberName("reinforced")] Reinforced,
    [JsonStringEnumMemberName("mine")] Mine,
    [JsonStringEnumMemberName("pile")] Pile,
    [JsonStringEnumMemberName("farm")] Farm,
    [JsonStringEnumMemberName("toolstation")] Toolstation,
    [JsonStringEnumMemberName("cooktable")] Cooktable,
    [JsonStringEnumMemberName("cannery")] Cannery,
    [JsonStringEnumMemberName("packer")] Packer,
    [JsonStringEnumMemberName("signpost")] Signpost,
    [JsonStringEnumMemberName("grave")] Grave,
    [JsonStringEnumMemberName("crate")] Crate,
}

/// <summary>Converts kinds to and from the ids that hotbar slots, icons and sounds use.</summary>
public static class Kinds {
    private static readonly string[] ids;
    private static readonly Dictionary<string, ThingKind> byId = new();
    public static readonly ThingKind[] All = Enum.GetValues<ThingKind>();

    static Kinds() {
        ids = new string[All.Length];
        foreach (var kind in All) {
            var name = typeof(ThingKind).GetField(kind.ToString())!.GetCustomAttributes(typeof(JsonStringEnumMemberNameAttribute), false);
            ids[(int)kind] = ((JsonStringEnumMemberNameAttribute)name[0]).Name;
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
