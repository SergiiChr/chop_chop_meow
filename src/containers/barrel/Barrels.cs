using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Barrels store water loose while placed, and keep it in their item when picked up.</summary>
public sealed class Barrels(World world) {
    internal static Inventory NewTank() => new() { Slots = 1, StackSize = Barrel.Capacity, Capacity = Barrel.Capacity };

    internal static bool CanPour(GameData data, string item) =>
        item == Water.Liquid || data.VesselOf(item) is VesselDef v && v.LitersIn(item) > 0;

    internal Thing? Place(string item, Vector2 at, Inventory source, float rotation) {
        if (source.PreferredSlot is >= 0 and int preferred &&
            ((uint)preferred >= (uint)source.Layout.Count || source.Layout[preferred] is not { Free: > 0 } cell ||
                cell.Item != item || source.SparesOnly && cell.Hotbar != 0)) return null;
        if (source.Remove(item, 1) != 1) return null;
        var barrel = world.Add(ThingKind.Barrel, at, rotation: rotation);
        barrel.Inventory.Add(Water.Liquid, Barrel.Liters(item));
        world.PileFilters(barrel);
        world.ClearGround(barrel);
        world.Play("place_building", at);
        return barrel;
    }

    internal static int WaterIn(Thing t) => t.Kind switch {
        ThingKind.Barrel => t.Inventory[Water.Liquid],
        ThingKind.Hut when Barrel.In(t.Inventory) is string barrel => Barrel.Liters(barrel),
        _ => 0,
    };

    internal static int Room(Thing t) => t.Kind switch {
        ThingKind.Barrel => t.Inventory.Room(Water.Liquid),
        ThingKind.Hut when Barrel.In(t.Inventory) is string barrel && t.Inventory.Free(barrel) > 0 => Barrel.Capacity - Barrel.Liters(barrel),
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
            t.Inventory.Replace(barrel, Barrel.AtFill(Barrel.Liters(barrel) + put));
        }
        return put;
    }

    /// <summary>Removes only water available to use, respecting a stack another player holds.</summary>
    internal static int TakeWater(Thing t, int liters) {
        if (t.Kind == ThingKind.Barrel) return t.Inventory.Remove(Water.Liquid, liters);
        if (Barrel.In(t.Inventory) is not string item || t.Inventory.Free(item) == 0) return 0;
        int fill = Barrel.Liters(item), taken = Math.Min(liters, fill);
        return t.Inventory.Replace(item, Barrel.AtFill(fill - taken)) ? taken : 0;
    }
}
