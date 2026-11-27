using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Barrels store one liquid loose while placed and keep it in their item when picked up.</summary>
public sealed class Barrels(World world) {
    internal static Inventory NewTank() => new() { Slots = 1, StackSize = Barrel.Capacity, Capacity = Barrel.Capacity };

    /// <summary>The liquid an item pours: a liquid itself, or what a bottle, bucket or barrel holds; null for anything else.</summary>
    /// <param name="unit">The object the item's key finds in the box it comes from.</param>
    internal static string? PouredLiquid(GameData data, string item, Item? unit) =>
        data.VesselFor(item) != null ? item : Vessels.LitersIn(unit) > 0 && (Barrel.Is(item) || data.VesselOf(item) != null) ? Vessels.LiquidIn(unit) : null;

    internal static bool CanPourWater(GameData data, string item, Item? unit) => PouredLiquid(data, item, unit) == Water.Liquid;

    internal static string LiquidIn(Thing barrel) => barrel.Inventory.First() ?? Water.Liquid;
    internal static bool CanHold(Thing barrel, string liquid) =>
        barrel.Inventory.Count == 0 || LiquidIn(barrel) == liquid;

    internal static bool Accepts(GameData data, Thing barrel, string item, Item? unit) =>
        PouredLiquid(data, item, unit) is string liquid && CanHold(barrel, liquid);

    internal Thing? Place(string item, Vector2 at, Inventory source, float rotation) {
        if (source.PreferredSlot is >= 0 and int preferred &&
            ((uint)preferred >= (uint)source.Layout.Count || source.Layout[preferred] is not { Free: > 0 } cell ||
                cell.Item != item || source.SparesOnly && cell.Hotbar != 0)) return null;
        Item? filled = Barrel.IsFilled(item) ? source.TakeObject(item) : null;
        if (filled == null && (Barrel.IsFilled(item) || source.Remove(item, 1) != 1)) return null;
        var barrel = world.Add(ThingKind.Barrel, at, rotation: rotation);
        if (filled != null) barrel.Inventory.Add(Barrel.LiquidOf(filled), Barrel.Liters(filled));
        world.PileFilters(barrel);
        world.ClearGround(barrel);
        world.Play("place_building", at);
        return barrel;
    }

    internal static int WaterIn(Thing t) => t.Kind switch {
        ThingKind.Barrel => t.Inventory[Water.Liquid],
        ThingKind.Hut when Barrel.In(t.Inventory) is string barrel => Barrel.Liters(t.Inventory.Object(barrel)),
        _ => 0,
    };

    internal static int Room(Thing t) => t.Kind switch {
        ThingKind.Barrel => t.Inventory.Room(Water.Liquid),
        ThingKind.Hut when Barrel.In(t.Inventory) is string barrel && t.Inventory.Free(barrel) > 0 => Barrel.Capacity - Barrel.Liters(t.Inventory.Object(barrel)),
        _ => 0,
    };

    /// <summary>Collects water into a placed tank or a roof barrel, up to the room available.</summary>
    internal int AddWater(Thing t, int liters) {
        int put = Math.Min(liters, Room(t));
        if (put <= 0) return 0;
        if (t.Kind == ThingKind.Barrel) {
            t.Inventory.Add(Water.Liquid, put);
            world.PileFilters(t);
        } else if (Barrel.In(t.Inventory) is string barrel) {
            world.SetFill(t.Inventory, barrel, Water.Liquid, Barrel.Liters(t.Inventory.Object(barrel)) + put);
        }
        return put;
    }

    /// <summary>Removes only water available to use, respecting a stack another player holds.</summary>
    internal int TakeWater(Thing t, int liters) {
        if (t.Kind == ThingKind.Barrel) return t.Inventory.Remove(Water.Liquid, liters);
        if (Barrel.In(t.Inventory) is not string item || t.Inventory.Free(item) == 0) return 0;
        int fill = Barrel.Liters(t.Inventory.Object(item)), taken = Math.Min(liters, fill);
        return taken > 0 && world.SetFill(t.Inventory, item, Water.Liquid, fill - taken) != null ? taken : 0;
    }
}
