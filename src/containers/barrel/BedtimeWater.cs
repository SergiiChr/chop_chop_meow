using System;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Serves thirst and bottles from each cabin's barrel in the order its players entered.</summary>
internal sealed class BedtimeWater(World world) {
    // A player who chooses sleep from outside the cabin comes after the people already inside.
    private static int EntryOrder(PlayerState player) => player.Sheltered == 0 ? int.MaxValue : player.CabinEntry;

    private static int ThirstLiters(float thirst) => (int)MathF.Ceiling((100 - thirst) / Water.LiterThirst);
    private static float Drank(float thirst, int liters) => Math.Min(100, thirst + liters * Water.LiterThirst);

    private static int Supply(Thing cabin) =>
        Barrel.In(cabin.Inventory) is string item && cabin.Inventory.Free(item) > 0 ? Barrel.Liters(item) : 0;

    /// <summary>Projects thirst before sleep starts and on waking, using the cabin barrel's current water.</summary>
    internal (float Bedtime, float Dawn) Preview(PlayerState player, float loss, Thing? cabin = null) {
        cabin ??= world.Find(player.Sheltered);
        float dryDawn = Math.Max(0, player.Thirst - loss);
        if (cabin == null) return (player.Thirst, dryDawn);
        int available = Supply(cabin);
        if (available == 0) return (player.Thirst, dryDawn);
        int own = ThirstLiters(player.Thirst), total = own, ahead = 0, aheadLiters = 0;
        int entry = EntryOrder(player);
        foreach (var other in world.Players) {
            if (other == player || other.Sheltered != cabin.Id) continue;
            int liters = ThirstLiters(other.Thirst);
            total += liters;
            if (other.CabinEntry < entry || other.CabinEntry == entry && other.Id < player.Id) {
                ahead++;
                aheadLiters += liters;
            }
        }
        float bedtime = Drank(player.Thirst, Math.Min(own, Math.Max(0, available - aheadLiters)));
        float dawn = Math.Max(0, bedtime - loss);
        // If there is any water left after bedtime, every occupant started the night full.
        int beforeDawn = ahead * ThirstLiters(Math.Max(0, 100 - loss));
        int dawnLiters = Math.Min(ThirstLiters(dawn), Math.Max(0, available - total - beforeDawn));
        return (bedtime, Drank(dawn, dawnLiters));
    }

    /// <summary>The players turning in and the cabin serving each, ordered by when they got inside.</summary>
    internal (PlayerState Player, Thing Cabin)[] Occupants(PlayerState sleeper, Thing cabin) =>
        world.S.Players.OrderBy(EntryOrder).ThenBy(p => p.Id)
            .Select(p => (Player: p, Cabin: p == sleeper ? cabin : world.Find(p.Sheltered)))
            .Where(pair => pair.Cabin != null).Select(pair => (pair.Player, pair.Cabin!)).ToArray();

    /// <summary>Serves everyone in entry order at bedtime or when they wake.</summary>
    internal static void ServeThirst((PlayerState Player, Thing Cabin)[] occupants) {
        foreach (var (player, cabin) in occupants)
            if (player.Health > 0 && player.Thirst < 100)
                player.Thirst = Drank(player.Thirst, Barrels.TakeWater(cabin, ThirstLiters(player.Thirst)));
    }

    /// <summary>Fills bottles in entry order once every occupant has had a chance to drink.</summary>
    internal static void FillBottles((PlayerState Player, Thing Cabin)[] occupants) {
        foreach (var (player, cabin) in occupants) {
            if (player.Health <= 0 || Supply(cabin) == 0) continue;
            for (int held = Water.Capacity - 1; held >= 0; held--) {
                string bottle = Water.WaterItem(held);
                while (player.Backpack.Free(bottle) > 0 && Supply(cabin) is > 0 and int available) {
                    int add = Math.Min(available, Water.Capacity - held);
                    if (!player.Backpack.ReplaceOne(bottle, Water.WaterItem(held + add))) break;
                    Barrels.TakeWater(cabin, add);
                }
            }
        }
    }
}
