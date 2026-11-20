using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Barrels store water loose while placed, and keep it in their item when picked up.</summary>
public sealed class Barrels(World world) {
    internal static Inventory NewTank() => new() { Slots = 1, StackSize = Barrel.Capacity, Capacity = Barrel.Capacity };

    internal static bool CanPour(GameData data, string item) =>
        item == Water.Liquid || data.VesselOf(item) is VesselDef v && v.LitersIn(item) > 0;

    internal Thing? Place(string item, Vector2 at, Inventory source) {
        if (source.Remove(item, 1) != 1) return null;
        var barrel = world.Add(ThingKind.Barrel, at);
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

public sealed partial class World {
    public Thing? PlaceBarrel(string item, Vector2 at) {
        if (!Barrel.Is(item)) return null;
        if (Construction.PlacementBlock(ThingKind.Barrel, at, item) is string blocked) { Say(blocked); return null; }
        if (Busy() || Forwarded(Verb.PlaceBarrel, item: item, count: S.Backpack.PreferredSlot, at: at)) return null;
        return barrels.Place(item, at, MarkedSource ?? S.Backpack);
    }

    public bool PickUpBarrel(Thing barrel) {
        if (barrel.Kind != ThingKind.Barrel || !Near(barrel) || Find(barrel.Id) != barrel) return false;
        if (Barrel.In(S.Backpack) != null) { Say("You can only carry one barrel."); return false; }
        string item = Barrel.AtFill(Barrels.WaterIn(barrel));
        if (S.Backpack.Room(item) == 0) { Say("Backpack full. Make room for the barrel."); return false; }
        if (Busy() || Forwarded(Verb.PickUpBarrel, barrel.Id)) return true;
        if (HeldByOthers(barrel)) return false;
        S.Backpack.Add(item, 1);
        S.Things.Remove(barrel);
        Play("wood_rocks_inventory", barrel.Pos);
        PlayerActed?.Invoke(ActorAction.PickUp, barrel.Pos);
        return true;
    }

    /// <summary>Pours a vessel into the backpack barrel, or draws water into vessels already in the backpack.</summary>
    public int MoveInBarrel(string barrelItem, string item, int quantity, bool puttingIn) {
        if (!Barrel.Is(barrelItem) || S.Backpack[barrelItem] != 1 || quantity <= 0) return 0;
        if (Held is { Box: 0 } held && held.Item == barrelItem) return 0;
        if (puttingIn && !Barrels.CanPour(Data, item)) { Say("The barrel only holds water."); return 0; }
        if (!puttingIn && item != Water.Liquid) return 0;
        if (Busy() || Forwarded(Verb.MoveInBarrel, Barrel.Liters(barrelItem), puttingIn ? 1 : 0, item, quantity, new Vector2(S.Backpack.PreferredSlot, 0))) return 0;
        var tank = Barrels.NewTank();
        int before = Barrel.Liters(barrelItem);
        tank.Add(Water.Liquid, before);
        if (puttingIn) {
            if (Data.VesselOf(item) is not VesselDef vessel) return 0;
            PourVessels(S.Backpack, item, tank, vessel, MarkedSource == S.Backpack ? Math.Min(quantity, heldCount) : quantity, tank.Room(Water.Liquid));
        } else {
            if (FillBlock(tank, S.Backpack, item) is string dry) { Say(dry); return 0; }
            Carry(tank, S.Backpack, null, item, quantity);
        }
        int after = tank[Water.Liquid];
        S.Backpack.Replace(barrelItem, Barrel.AtFill(after));
        if (before == after && puttingIn && tank.Room(Water.Liquid) == 0) Say("The barrel is full.");
        return Math.Abs(after - before);
    }
}
