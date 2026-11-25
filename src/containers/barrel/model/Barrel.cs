using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChopChopMeow;

/// <summary>A steel barrel carried as one item whose fill travels with it.</summary>
public static class Barrel {
    public const string Item = "steel_barrel";
    public const int Capacity = 50;
    internal static readonly string[] Fills = MakeFills(Item, Capacity);
    private static readonly Dictionary<string, int> byFill = IndexFills(Fills);
    private static readonly Dictionary<string, int> byJam = IndexFills(MakeFills(Item, Capacity, "_jam_"));

    public static bool Is(string item) => item.StartsWith(Item, StringComparison.Ordinal) && Liters(item) >= 0;
    public static bool IsFilled(string item) => item != Item && Is(item);
    public static int Liters(string item) => byFill.TryGetValue(item, out int water) ? water : byJam.GetValueOrDefault(item, -1);
    public static string LiquidOf(string item) => item != Item && byJam.ContainsKey(item) ? BerryJam.Liquid : Water.Liquid;
    public static string AtFill(int liters, string liquid = Water.Liquid) => (liquid == BerryJam.Liquid ? BerryJam.Barrels : Fills)[liters];
    public static float Speed(string item) => 1 - .5f * Liters(item) / Capacity;

    public static string? In(Inventory inventory) {
        foreach (var (item, _) in inventory.Items) if (Is(item)) return item;
        return null;
    }

    public static string? FilledIn(Inventory inventory) {
        foreach (var (item, _) in inventory.Items) if (IsFilled(item)) return item;
        return null;
    }

    internal static string[] MakeFills(string empty, int capacity, string marker = "_") {
        var fills = new string[capacity + 1];
        fills[0] = empty;
        for (int i = 1; i < fills.Length; i++) fills[i] = empty + marker + i.ToString(CultureInfo.InvariantCulture);
        return fills;
    }

    internal static Dictionary<string, int> IndexFills(string[] fills) {
        var index = new Dictionary<string, int>(fills.Length, StringComparer.Ordinal);
        for (int i = 0; i < fills.Length; i++) index.Add(fills[i], i);
        return index;
    }
}
