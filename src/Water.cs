using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>The refillable three-sip bottle and the direct sip taken at a water source.</summary>
public static class Water {
    public const string FullBottleWarning = "Requires full water bottle";
    public const float SipThirst = 100f / 3f;
    public static readonly string[] BottleItems = { "empty_bottle", "water_1", "water_2", "water" };

    public static bool IsBottle(string item) => item is "water" or "water_2" or "water_1" or "empty_bottle";

    /// <summary>Remaining sips, or -1 when the item is not a bottle.</summary>
    public static int BottleSips(string item) => item switch {
        "water" => 3,
        "water_2" => 2,
        "water_1" => 1,
        "empty_bottle" => 0,
        _ => -1
    };

    public static string WaterItem(int sips) => sips switch {
        >= 3 => "water",
        2 => "water_2",
        1 => "water_1",
        _ => "empty_bottle"
    };

    /// <summary>Finds an empty bottle first, then the least-full partial bottle, and a full bottle last.</summary>
    public static string? FindBottle(Inventory inventory) {
        foreach (var item in BottleItems) if (inventory[item] > 0) return item;
        return null;
    }

    /// <summary>Refills an empty bottle first, then a partial bottle. With no bottle, a source gathering gives one direct sip.</summary>
    public static WaterCollectResult CollectWater(World world, Thing source) {
        if (source.Kind != ThingKind.Water) return WaterCollectResult.NoChange;
        Inventory backpack = world.S.Backpack;
        string? bottle = FindBottle(backpack);
        if (bottle == "water") return WaterCollectResult.NoChange;
        if (bottle != null) {
            Replace(backpack, bottle, "water");
            return WaterCollectResult.RefilledBottle;
        }
        world.S.Thirst = System.Math.Min(100, world.S.Thirst + SipThirst);
        return WaterCollectResult.SourceSip;
    }

    /// <summary>Drinks one sip and preserves the same individual bottle in its next state.</summary>
    public static bool SipBottle(World world, string item) {
        if (BottleSips(item) <= 0) return false;
        Inventory backpack = world.S.Backpack;
        if (!backpack.Replace(item, WaterItem(BottleSips(item) - 1))) return false;
        world.S.Thirst = System.Math.Min(100, world.S.Thirst + SipThirst);
        return true;
    }

    private static void Replace(Inventory inventory, string oldItem, string newItem) {
        if (!inventory.Replace(oldItem, newItem))
            throw new System.InvalidOperationException("Bottle state replacement did not fit in its inventory.");
    }
}

public enum WaterCollectResult { NoChange, RefilledBottle, SourceSip }

public sealed partial class World {
    /// <summary>Old saves may have a stack of bottles. Preserve excess containers in a recovery box.</summary>
    private void NormalizeBottleStorage() {
        foreach (var player in S.Players) Spill(player.Backpack, player.Pos);
        foreach (var thing in S.Things.ToArray()) foreach (var inventory in Boxes(thing)) if (inventory != null) Spill(inventory, thing.Pos);
        foreach (var worker in S.Workers) Spill(worker.Bag, worker.Pos);
        foreach (var inventory in S.Crates.Values) Spill(inventory, Player);
        void Spill(Inventory inventory, Godot.Vector2 position) {
            Thing? overflow = null;
            while (inventory.AggregateSlotsNeeded > inventory.Slots) {
                string? bottle = Water.FindBottle(inventory);
                if (bottle == null) break;
                overflow ??= Add(ThingKind.Grave, position);
                overflow.Inventory.Slots = System.Math.Max(overflow.Inventory.Slots, overflow.Inventory.Count + 1);
                inventory.Remove(bottle, 1); overflow.Inventory.Add(bottle, 1);
            }
            inventory.EnsureLayout();
        }
    }

    /// <summary>A water collector refills existing empty bottles; gathering never creates packaging.</summary>
    private bool WaterWork(Worker worker, Thing pond, float dt) {
        string? carried = Water.FindBottle(worker.Bag);
        if (carried != null && carried != "water") {
            worker.Status = "Collecting water";
            if (!Walk(worker, Terrain.WaterPoint(worker.Pos), dt, 2.5f)) return true;
            worker.WorkState = WorkerWorkState.Gathering;
            worker.Timer += dt;
            if (worker.Timer < Data.GatherSeconds) return true;
            worker.Timer = 0; worker.Bag.Replace(carried, "water");
            worker.Destination = DepositFor("water", worker.Pos)?.Id ?? 0;
            return true;
        }
        Thing? source = null; string item = "empty_bottle";
        foreach (var candidate in Water.BottleItems) {
            if (candidate == "water") break;
            source = SourceFor(candidate, worker.Pos);
            if (source != null) { item = candidate; break; }
        }
        if (source == null) { worker.Status = "Needs refillable bottles in output-enabled storage"; return false; }
        if (DepositFor("water", worker.Pos) == null) { worker.Status = "Needs storage for filled bottles"; return false; }
        worker.Status = "Fetching an empty bottle";
        if (Walk(worker, source.Pos, dt)) {
            source.Inventory.TransferTo(worker.Bag, item, 1, true);
            worker.Destination = pond.Id;
        }
        return true;
    }
}
