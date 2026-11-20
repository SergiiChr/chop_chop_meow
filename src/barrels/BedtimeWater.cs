using System;
using System.Linq;

namespace ChopChopMeow;

public sealed partial class World {
    private static bool BeforeBed(PlayerState first, PlayerState second) =>
        first.CabinEntry < second.CabinEntry || first.CabinEntry == second.CabinEntry && first.Id < second.Id;

    private static int ThirstLiters(float thirst) => (int)MathF.Ceiling((100 - thirst) / Water.LiterThirst);
    private static float Drank(float thirst, int liters) => Math.Min(100, thirst + liters * Water.LiterThirst);

    private static int BedtimeSupply(Thing cabin) =>
        Barrel.In(cabin.Inventory) is string item && cabin.Inventory.Free(item) > 0 ? Barrel.Liters(item) : 0;

    /// <summary>Thirst after the cabin barrel serves everyone ahead of this player.</summary>
    private float BedtimeThirst(PlayerState player) {
        if (Find(player.Sheltered) is not Thing cabin) return player.Thirst;
        int available = BedtimeSupply(cabin);
        if (available == 0) return player.Thirst;
        foreach (var other in S.Players)
            if (BeforeBed(other, player) && other.Sheltered == cabin.Id) available -= ThirstLiters(other.Thirst);
        return Drank(player.Thirst, Math.Min(Math.Max(0, available), ThirstLiters(player.Thirst)));
    }

    public float ThirstAtDawn() => Math.Max(0, BedtimeThirst(S.Actor) - SecondsToDawn * Data.ThirstPerSecond);

    /// <summary>Spends water on every occupant's thirst first, then fills their bottles in the same order.</summary>
    private void DrinkAtBedtime() {
        var inOrder = S.Players.OrderBy(p => p.CabinEntry).ThenBy(p => p.Id).ToArray();
        foreach (var p in inOrder)
            if (Find(p.Sheltered) is Thing cabin) p.Thirst = Drank(p.Thirst, Barrels.TakeWater(cabin, ThirstLiters(p.Thirst)));
        foreach (var p in inOrder) {
            if (Find(p.Sheltered) is not Thing cabin || BedtimeSupply(cabin) == 0) continue;
            for (int held = Water.Capacity - 1; held >= 0; held--) {
                string bottle = Water.WaterItem(held);
                while (p.Backpack.Free(bottle) > 0 && BedtimeSupply(cabin) is > 0 and int available) {
                    int add = Math.Min(available, Water.Capacity - held);
                    if (!p.Backpack.ReplaceOne(bottle, Water.WaterItem(held + add))) break;
                    Barrels.TakeWater(cabin, add);
                }
            }
        }
    }
}
