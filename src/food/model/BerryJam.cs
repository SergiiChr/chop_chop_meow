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
    /// <summary>The bottle's icon at each fill of jam, from empty to full.</summary>
    public static readonly string[] Looks = [Water.Empty, "jam_bottle_1", "jam_bottle_2", Bottle];
}
