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

    public string Kind { get; set; } = "";
    /// <summary>The world time it expires, as a firebug dies, or 0 for never.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float Expires { get; set; }
}
