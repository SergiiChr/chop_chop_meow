using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>The refillable three-liter bottle, and a liter drunk straight from a water source.</summary>
public static class Water {
    /// <summary>The bottle's item ids, full to empty, and the water a full one pours into a station.</summary>
    public const string Full = "water", TwoLiters = "water_2", OneLiter = "water_1", Empty = "empty_bottle", Liquid = "fresh_water";
    public const float LiterThirst = 100f / 3f;
    public static readonly string[] BottleItems = { Empty, OneLiter, TwoLiters, Full };

    public static bool IsBottle(string item) => item is Full or TwoLiters or OneLiter or Empty;

    /// <summary>Remaining liters, or -1 when the item is not a bottle.</summary>
    public static int BottleLiters(string item) => item switch {
        Full => 3,
        TwoLiters => 2,
        OneLiter => 1,
        Empty => 0,
        _ => -1
    };

    public static string WaterItem(int liters) => liters switch {
        >= 3 => Full,
        2 => TwoLiters,
        1 => OneLiter,
        _ => Empty
    };

    /// <summary>Finds an empty bottle first, then the least-full partial bottle, and a full bottle last.</summary>
    public static string? FindBottle(Inventory inventory) {
        foreach (var item in BottleItems) if (inventory[item] > 0) return item;
        return null;
    }

    /// <summary>Refills an empty bottle first, then a partial bottle. With no bottle, gathering at a source drinks one liter straight from it.</summary>
    public static WaterCollectResult CollectWater(World world, Thing source) {
        if (source.Kind != ThingKind.Water) return WaterCollectResult.NoChange;
        Inventory backpack = world.S.Backpack;
        string? bottle = FindBottle(backpack);
        if (bottle == Full) return WaterCollectResult.NoChange;
        if (bottle != null) {
            Replace(backpack, bottle, Full);
            return WaterCollectResult.RefilledBottle;
        }
        world.S.Thirst = System.Math.Min(100, world.Thirst + LiterThirst);
        return WaterCollectResult.SourceLiter;
    }

    /// <summary>Drinks one liter and preserves the same individual bottle in its next state.</summary>
    public static bool DrinkLiter(World world, string item) {
        if (BottleLiters(item) <= 0) return false;
        Inventory backpack = world.S.Backpack;
        if (!backpack.Replace(item, WaterItem(BottleLiters(item) - 1))) return false;
        world.S.Thirst = System.Math.Min(100, world.Thirst + LiterThirst);
        return true;
    }

    private static void Replace(Inventory inventory, string oldItem, string newItem) {
        if (!inventory.Replace(oldItem, newItem))
            throw new System.InvalidOperationException("Bottle state replacement did not fit in its inventory.");
    }
}

public enum WaterCollectResult { NoChange, RefilledBottle, SourceLiter }

public sealed partial class World {
    /// <summary>A water collector refills existing empty bottles; gathering never creates packaging.</summary>
    private bool WaterWork(Worker worker, Thing pond, float dt) {
        string? carried = Water.FindBottle(worker.Bag);
        if (carried != null && carried != Water.Full) {
            worker.Status = "Collecting water";
            if (!Walk(worker, Terrain.WaterPoint(worker.Pos), dt, 2.5f)) return true;
            worker.WorkState = WorkerWorkState.Gathering;
            worker.Timer += dt;
            if (worker.Timer < Data.GatherSeconds) return true;
            worker.Timer = 0; worker.Bag.Replace(carried, Water.Full); Got(Water.Full);
            worker.Destination = DepositFor(Water.Full, worker.Pos)?.Id ?? 0;
            return true;
        }
        Thing? source = null; string item = Water.Empty;
        foreach (var candidate in Water.BottleItems) {
            if (candidate == Water.Full) break;
            source = SourceFor(candidate, worker.Pos);
            if (source != null) { item = candidate; break; }
        }
        if (source == null) { worker.Status = "Needs refillable bottles in output-enabled storage"; return false; }
        if (DepositFor(Water.Full, worker.Pos) == null) { worker.Status = "Needs storage for filled bottles"; return false; }
        worker.Status = "Fetching an empty bottle";
        if (Walk(worker, source.Pos, dt)) {
            source.Inventory.TransferTo(worker.Bag, item, 1, true);
            worker.Destination = pond.Id;
        }
        return true;
    }
}
