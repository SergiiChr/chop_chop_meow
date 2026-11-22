using System;
using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>The refillable three-liter bottle, and a liter drunk straight from a water source.</summary>
public static class Water {
    public const string BottleTool = "bottle";
    /// <summary>The bottle's item ids, full to empty, and the water a full one pours into a station.</summary>
    public const string Full = "water", TwoLiters = "water_2", OneLiter = "water_1", Empty = "empty_bottle", Liquid = "fresh_water";
    public const float LiterThirst = 100f / 3f;
    /// <summary>The bottle at each fill, from empty to full: the index is the liters it holds.</summary>
    public static readonly string[] BottleItems = { Empty, OneLiter, TwoLiters, Full };
    /// <summary>Liters a full bottle holds.</summary>
    public static int Capacity => BottleItems.Length - 1;

    public static bool IsBottle(string item) => BottleLiters(item) >= 0;

    /// <summary>Remaining liters, or -1 when the item is not a bottle.</summary>
    public static int BottleLiters(string item) => Array.IndexOf(BottleItems, item);

    /// <summary>The bottle holding so many liters, up to full.</summary>
    public static string WaterItem(int liters) => BottleItems[Math.Clamp(liters, 0, Capacity)];

    /// <summary>Finds an empty bottle first, then the least-full partial bottle, and a full bottle last, among those no player holds.</summary>
    public static string? FindBottle(Inventory inventory) {
        foreach (var item in BottleItems) if (inventory.Free(item) > 0) return item;
        return null;
    }

    /// <summary>Refills an empty bottle first, then a partial bottle. With no bottle, gathering at a source drinks one liter straight from it.</summary>
    public static WaterCollectResult CollectWater(World world, Thing source) {
        if (source.Kind != ThingKind.Water) return WaterCollectResult.NoChange;
        Inventory backpack = world.S.Backpack;
        string? bottle = FindBottle(backpack);
        if (bottle != null && bottle != Full) {
            Replace(backpack, bottle, Full);
            return WaterCollectResult.RefilledBottle;
        }
        if (FillBucket(backpack)) return WaterCollectResult.RefilledBottle;
        if (bottle == Full) return WaterCollectResult.NoChange;
        world.S.Thirst = System.Math.Min(100, world.Thirst + LiterThirst);
        return WaterCollectResult.SourceLiter;
    }

    internal static bool HasBucketRoom(Inventory backpack) {
        foreach (var (item, _) in backpack.Items)
            if (Bucket.Liters(item) is >= 0 and < Bucket.Capacity && backpack.CanReplaceOne(item, Bucket.AtFill(Bucket.Capacity))) return true;
        return false;
    }

    private static bool FillBucket(Inventory backpack) {
        for (int liters = Bucket.Capacity - 1; liters >= 0; liters--)
            if (backpack.ReplaceOne(Bucket.AtFill(liters), Bucket.AtFill(Bucket.Capacity))) return true;
        return false;
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
