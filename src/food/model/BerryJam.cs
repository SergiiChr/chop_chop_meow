using System;

namespace ChopChopMeow;

/// <summary>Berry jam as loose liters, or in the bottle, bucket and barrel that water also uses.</summary>
internal static class BerryJam {
    public const string Liquid = "berry_jam", Bottle = "jam_bottle", Color = "9b2f4f";
    public const int BerriesPerCrush = 10, CrushesPerLiter = 2;
    public const int BerryHunger = 5;
    private const int PrepBonus = 5;
    public const int CrushedHunger = BerriesPerCrush * BerryHunger + PrepBonus;
    public const int HungerPerLiter = CrushesPerLiter * CrushedHunger + PrepBonus;
    public const int CannedHunger = HungerPerLiter + PrepBonus;
    public static readonly string[] Bottles = [Water.Empty, "jam_bottle_1", "jam_bottle_2", Bottle];
    public static readonly string[] Buckets = Barrel.MakeFills(Bucket.Item, Bucket.Capacity, "_jam_");
    public static readonly string[] Barrels = Barrel.MakeFills(Barrel.Item, Barrel.Capacity, "_jam_");

    public static bool InBottle(string item) => BottleLiters(item) > 0;
    public static int BottleLiters(string item) => item.StartsWith(Bottle, StringComparison.Ordinal) ? Array.IndexOf(Bottles, item) : -1;
}
