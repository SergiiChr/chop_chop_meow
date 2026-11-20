using System;
using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>A ten-liter vessel that a worker can use on a water haul.</summary>
public static class Bucket {
    public const string Item = "steel_bucket";
    public const int Capacity = 10;
    internal static readonly string[] Fills = Barrel.MakeFills(Item, Capacity);
    private static readonly Dictionary<string, int> byFill = Barrel.IndexFills(Fills);

    public static bool Is(string item) => item.StartsWith(Item, StringComparison.Ordinal) && byFill.ContainsKey(item);
    public static int Liters(string item) => byFill.GetValueOrDefault(item, -1);
    public static string AtFill(int liters) => Fills[liters];
}
