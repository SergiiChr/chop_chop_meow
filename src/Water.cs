namespace ChopChopMeow;

/// <summary>The refillable three-liter bottle, and a liter drunk straight from a water source.</summary>
public static class Water {
    public const string BottleTool = "bottle";
    /// <summary>The empty bottle, the full water bottle the village sells, and the water a bottle pours into a station.</summary>
    public const string Full = "water", Empty = "empty_bottle", Liquid = "fresh_water";
    public const float LiterThirst = 100f / 3f;
    public const int Capacity = 3;
    /// <summary>The bottle's icon at each fill of water, from empty to full.</summary>
    public static readonly string[] Looks = { Empty, "water_1", "water_2", Full };

    /// <summary>Whether an item is a bottle, empty or holding something.</summary>
    public static bool IsBottle(string item) => Item.IsKind(item, Empty);

    /// <summary>The liters of water a bottle in a box holds: 0 for an empty one, -1 for anything else, a bottle of jam included.</summary>
    public static int BottleLiters(Inventory box, string item) =>
        !IsBottle(item) ? -1 : box.Object(item) is not { } bottle ? 0 : Vessels.LiquidIn(bottle) == Liquid ? Vessels.LitersIn(bottle) : -1;

    /// <summary>Finds a bottle with room for water, an empty one first and then the least full, among those no player holds; null when every bottle is full.</summary>
    public static string? FindBottle(Inventory inventory) {
        if (inventory.Free(Empty) > 0) return Empty;
        string? least = null;
        int fewest = int.MaxValue;
        foreach (var (item, _) in inventory.Items)
            if (BottleLiters(inventory, item) is > 0 and < Capacity and int liters && liters < fewest && inventory.Free(item) > 0) { least = item; fewest = liters; }
        return least;
    }

    /// <summary>Refills an empty bottle first, then a partial bottle. With no bottle, gathering at a source drinks one liter straight from it.</summary>
    public static WaterCollectResult CollectWater(World world, Thing source) {
        if (source.Kind != ThingKind.Water) return WaterCollectResult.NoChange;
        Inventory backpack = world.S.Backpack;
        if (FindBottle(backpack) is string bottle) {
            if (world.SetFill(backpack, bottle, Liquid, Capacity) == null) throw new System.InvalidOperationException("A bottle refilled where it lay found no room.");
            return WaterCollectResult.RefilledBottle;
        }
        if (FillBucket(world, backpack)) return WaterCollectResult.RefilledBottle;
        // Full bottles carried: nothing to refill, and the player drinks from them rather than from the lake.
        if (Vessels.Count(backpack, Empty, Liquid, Capacity) > 0) return WaterCollectResult.NoChange;
        world.S.Thirst = System.Math.Min(100, world.Thirst + LiterThirst);
        return WaterCollectResult.SourceLiter;
    }

    /// <summary>The bucket in the backpack with room for water, the fullest first and an empty one last, or null.</summary>
    private static string? RoomyBucket(Inventory backpack) {
        string? best = null;
        int most = -1;
        foreach (var (item, _) in backpack.Items) {
            if (!Bucket.Is(item) || backpack.Free(item) == 0) continue;
            var bucket = backpack.Object(item);
            if (bucket != null && Vessels.LiquidIn(bucket) != Liquid) continue;
            int liters = Vessels.LitersIn(bucket);
            if (liters < Bucket.Capacity && liters > most && (bucket != null || backpack.RoomForFilled(Bucket.Item) > 0)) { best = item; most = liters; }
        }
        return best;
    }

    internal static bool HasBucketRoom(Inventory backpack) => RoomyBucket(backpack) != null;

    private static bool FillBucket(World world, Inventory backpack) =>
        RoomyBucket(backpack) is string bucket && world.SetFill(backpack, bucket, Liquid, Bucket.Capacity) != null;
}

public enum WaterCollectResult { NoChange, RefilledBottle, SourceLiter }
