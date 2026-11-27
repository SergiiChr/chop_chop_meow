using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>
/// One unit that carries properties of its own: its kind, as <see cref="GameData.Items"/> names it, and what sets it apart
/// from others of that kind.
/// A plain unit, such as a log of wood, has none and is only counted in its stack.
/// </summary>
public sealed class Item {
    /// <summary>Puts units in the order takes use them: the soonest to expire first.</summary>
    internal static readonly IComparer<Item> Soonest = Comparer<Item>.Create((a, b) => a.Expires.CompareTo(b.Expires));
    /// <summary>The kinds whose every unit is an object found by its key, such as a lantern.</summary>
    internal static readonly string[] Keyed = [Lanterns.Item];
    /// <summary>The kinds that are objects only while they hold something, such as a packed crate, and plain units of their kind once empty.</summary>
    internal static readonly string[] Holders = [World.Crate];
    private const char KeyMark = '#';

    public string Kind { get; set; } = "";
    /// <summary>The number in the key an object is found by, such as 17 in lantern#17, or 0 for a unit found by its kind.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Id { get; set; }
    /// <summary>The world time it expires, as a firebug dies, or 0 for never.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float Expires { get; set; }
    /// <summary>What it holds, such as a lantern's firebugs, or null for nothing.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Inventory? Contents { get; set; }

    /// <summary>The key a cell holding this object is found by: its kind and its number, such as lantern#17.</summary>
    [JsonIgnore] public string Key => KeyOf(Kind, Id);

    internal static string KeyOf(string kind, int id) => $"{kind}{KeyMark}{id}";
    /// <summary>Whether an item id is an object's key rather than a kind.</summary>
    internal static bool IsKey(string item) => item.Contains(KeyMark);
    /// <summary>Whether an item id names objects found by their key: such a key, or a kind whose every unit is one.</summary>
    internal static bool IsObject(string item) => IsKey(item) || Array.IndexOf(Keyed, item) >= 0;
    /// <summary>The number in an object's key, or 0 for a kind.</summary>
    internal static int IdOf(string item) {
        int at = item.IndexOf(KeyMark);
        return at > 0 && int.TryParse(item.AsSpan(at + 1), out int id) && id > 0 ? id : 0;
    }
    /// <summary>Whether an item id is this kind or the key of an object of it.</summary>
    internal static bool IsKind(string item, string kind) =>
        item.Length > kind.Length ? item[kind.Length] == KeyMark && item.StartsWith(kind, StringComparison.Ordinal) : item == kind;
    /// <summary>The kind an item id names: the id itself, or the kind of the object its key finds.</summary>
    internal static string KindOf(string item) {
        if (!IsKey(item)) return item;
        foreach (string kind in Keyed) if (IsKind(item, kind)) return kind;
        foreach (string kind in Holders) if (IsKind(item, kind)) return kind;
        return item[..item.IndexOf(KeyMark)];
    }
    /// <summary>Whether this object turns back into a plain unit of its kind now that it holds nothing.</summary>
    internal bool Emptied => Contents is { Count: 0 } && Array.IndexOf(Holders, Kind) >= 0;
    /// <summary>The one kind of item this object holds, the empty string when it holds several, or null when it holds nothing.</summary>
    internal string? SoleKind() {
        if (Contents is not { Count: > 0 } contents) return null;
        string? sole = null;
        foreach (var pair in contents.Items) {
            string kind = KindOf(pair.Key);
            if (sole != null && sole != kind) return "";
            sole = kind;
        }
        return sole;
    }
}
