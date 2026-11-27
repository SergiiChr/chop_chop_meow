using System;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Serves thirst and bottles from each cabin's barrel in the order its players entered.</summary>
internal sealed class BedtimeWater(World world) {
    // Sleeping subtracts thirst each second, so allow for rounding at a whole-liter boundary.
    private const float ThirstRoundoff = .001f;
    private static int ThirstLiters(float thirst) => Math.Max(0, (int)MathF.Ceiling((100 - thirst - ThirstRoundoff) / Water.LiterThirst));
    private static float Drank(float thirst, int liters) {
        float after = thirst + liters * Water.LiterThirst;
        return liters > 0 && after >= 100 - ThirstRoundoff ? 100 : after;
    }

    private static int Supply(Thing cabin) =>
        Barrel.In(cabin.Inventory) is string item && cabin.Inventory.Free(item) > 0 ? Barrel.Liters(cabin.Inventory.Object(item)) : 0;

    private static int BottleRoom(PlayerState player) {
        var box = player.Backpack;
        int liters = box.Free(Water.Empty) * Water.Capacity;
        foreach (var (item, _) in box.Items) if (Water.BottleLiters(box, item) is > 0 and int held && box.Free(item) > 0) liters += Water.Capacity - held;
        return liters;
    }

    /// <summary>The fullest water bottle in a backpack that still has room, an empty one last, or null.</summary>
    private static string? FullestRoomyBottle(Inventory box) {
        string? fullest = box.Free(Water.Empty) > 0 ? Water.Empty : null;
        int most = 0;
        foreach (var (item, _) in box.Items)
            if (Water.BottleLiters(box, item) is > 0 and int held && held < Water.Capacity && held > most && box.Free(item) > 0) { fullest = item; most = held; }
        return fullest;
    }

    /// <summary>Projects thirst before sleep starts and on waking, using the cabin barrel's current water.</summary>
    internal (float Bedtime, float Dawn) Preview(PlayerState player, float loss, bool fillBottles = true) {
        var cabin = world.Find(player.Sheltered);
        float dryDawn = Math.Max(0, player.Thirst - loss);
        if (cabin == null) return (player.Thirst, dryDawn);
        int available = Supply(cabin);
        if (available == 0) return (player.Thirst, dryDawn);
        int own = player.SleepingAt == cabin.Id ? 0 : ThirstLiters(player.Thirst);
        int total = own + (fillBottles ? BottleRoom(player) : 0), aheadLiters = 0, aheadAtWake = 0;
        foreach (var other in world.Players) {
            if (other == player || other.Sheltered != cabin.Id) continue;
            bool asleep = other.SleepingAt == cabin.Id;
            int liters = asleep ? 0 : ThirstLiters(other.Thirst);
            if (asleep && fillBottles) total += BottleRoom(other);
            if (Before(other, player)) {
                aheadLiters += liters;
                if (asleep) aheadAtWake += ThirstLiters(Math.Max(0, other.Thirst - loss));
            }
        }
        float bedtime = Drank(player.Thirst, Math.Min(own, Math.Max(0, available - aheadLiters)));
        float dawn = Math.Max(0, bedtime - loss);
        int dawnLiters = Math.Min(ThirstLiters(dawn), Math.Max(0, available - total - aheadAtWake));
        return (bedtime, Drank(dawn, dawnLiters));
    }

    /// <summary>The players turning in and the cabin serving each, ordered by when they got inside.</summary>
    internal (PlayerState Player, Thing Cabin)[] Occupants() =>
        world.S.Players.OrderBy(p => p.CabinEntry).ThenBy(p => p.Id)
            .Select(p => (Player: p, Cabin: world.Find(p.Sheltered)))
            .Where(pair => pair.Cabin != null && pair.Player.SleepingAt == pair.Cabin.Id).Select(pair => (pair.Player, pair.Cabin!)).ToArray();

    /// <summary>Drinks on lying down or getting up, leaving earlier occupants who are still awake the water they need.</summary>
    internal void DrinkInTurn(PlayerState player, Thing cabin) {
        int available = Supply(cabin);
        foreach (var other in world.Players)
            if (other != player && other.Sheltered == cabin.Id && other.SleepingAt == 0
                && Before(other, player))
                available -= ThirstLiters(other.Thirst);
        Drink(player, cabin, Math.Max(0, available));
    }

    private static bool Before(PlayerState a, PlayerState b) =>
        a.CabinEntry < b.CabinEntry || a.CabinEntry == b.CabinEntry && a.Id < b.Id;

    internal void Drink(PlayerState player, Thing cabin, int limit = int.MaxValue) {
        if (player.Health > 0 && player.Thirst < 100 && limit > 0)
            player.Thirst = Drank(player.Thirst, world.Barrels.TakeWater(cabin, Math.Min(limit, ThirstLiters(player.Thirst))));
    }

    /// <summary>Serves sleepers in entry order when they wake together.</summary>
    internal void ServeThirst((PlayerState Player, Thing Cabin)[] occupants) {
        foreach (var (player, cabin) in occupants) Drink(player, cabin);
    }

    /// <summary>Fills bottles in entry order after everyone has had their bedtime drink.</summary>
    internal void FillBottles((PlayerState Player, Thing Cabin)[] occupants) {
        foreach (var (player, cabin) in occupants) {
            if (player.Health <= 0) continue;
            var box = player.Backpack;
            while (Supply(cabin) is > 0 and int available && FullestRoomyBottle(box) is string bottle) {
                int held = Math.Max(0, Water.BottleLiters(box, bottle)), add = Math.Min(available, Water.Capacity - held);
                if (world.SetFill(box, bottle, Water.Liquid, held + add) == null) break;
                world.Barrels.TakeWater(cabin, add);
            }
        }
    }
}
