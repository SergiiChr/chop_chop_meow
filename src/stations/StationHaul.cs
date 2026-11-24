using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>A crafting station's part of the Haul job: it brings inputs and fuel and carries out what is made and what the recipe no longer uses.</summary>
internal sealed class StationHaul : PlaceHaul {
    public StationHaul(World world) : base(world) { }

    public override void Carries(Worker w, Thing t, ref Carry best) {
        var data = world.Data;
        RecipeDef? recipe = data.Recipes.GetValueOrDefault(t.Recipe);
        foreach (var (item, _) in t.Inventory.Items) {
            int free = t.Inventory.Free(item);
            if (free == 0 || world.Pending(t, item)) continue;
            // What another recipe left keeps the station from starting a batch, so it leaves as a full output does.
            bool full = recipe != null && (item != recipe.Output || t.Inventory.SlotRoom(item) < recipe.Amount);
            if (data.VesselFor(item) is VesselDef v) {
                // A liquid leaves in vessels with room for it, which the hauler brings, fills here and stores.
                if (world.DepositFor(v.Full, t.Pos, t.Id) == null || RoomySource(v, w.Pos) is not (string roomy, Thing holder)) continue;
                Offer(ref best, new Carry { Place = t, From = holder, To = t, Item = roomy, Count = (free + v.Capacity - 1) / v.Capacity, Clears = true, Rule = full ? 2 : 4, Order = -free });
            } else if (Taker(t, item, free) is (Thing store, int count)) Offer(ref best, TakeOut(t, store, CarryBox.Inventory, item, count, full));
        }
        if (recipe == null) return;
        foreach (var (input, batch) in recipe.Inputs) {
            int have = t.Inventory[input], room = t.Inventory.SlotRoom(input);
            if (room <= 0) continue;
            // A liquid comes in the vessels that pour it, enough of them for the liters of room.
            if (data.VesselFor(input) is VesselDef carrier) {
                if (world.LiquidSource(carrier, w.Pos) is (string filled, Thing holder)) Offer(ref best, Bring(t, holder, filled, (room + carrier.LitersIn(filled) - 1) / carrier.LitersIn(filled), have, batch));
            } else if (world.SourceFor(input, w.Pos, t.Id) is Thing source) Offer(ref best, Bring(t, source, input, room + FuelRoom(t, recipe, input), have, batch));
        }
        if (recipe.Fuels.Count == 0 || t.Burner is not Inventory burner) return;
        if (burner.First() is string held && world.FuelOf(t) == null) {
            // Fuel the recipe does not burn leaves the fuel slot, so the right one can go in.
            if (!t.Processing && world.DepositFor(held, t.Pos, t.Id) is Thing store) Offer(ref best, new Carry { Place = t, From = t, To = store, Box = CarryBox.Burner, Item = held, Count = burner.Free(held), Rule = 1 });
        } else if (FuelSource(t, w.Pos) is (string fuel, Thing from))
            Offer(ref best, Bring(t, from, fuel, burner.Room(fuel) + (recipe.Inputs.ContainsKey(fuel) ? Math.Max(0, t.Inventory.SlotRoom(fuel)) : 0), burner[fuel], 1) with { Fuel = true });
    }

    public override string Wait(Thing place) {
        if (!world.Data.Recipes.ContainsKey(place.Recipe)) return "Nothing queued or marked to make";
        if (!place.Processing && place.Inventory.Short(world.BatchCost(place)) is string input) return world.Phrase("Needs ", world.Data.Name(input), " in output-enabled storage");
        return base.Wait(place);
    }

    /// <summary>The room a station's fuel slot has for an input that its recipe also burns; a load of it is shared between the two slots.</summary>
    private int FuelRoom(Thing t, RecipeDef recipe, string input) => t.Burner is Inventory burner && recipe.Fuels.Contains(input) && world.FuelTakes(t, input) ? burner.Room(input) : 0;

    /// <summary>The nearest storage with vessels that have room for a liquid, emptiest first, and that vessel; or null.</summary>
    private (string Item, Thing Source)? RoomySource(VesselDef v, Vector2 from) => world.NearestFill(v, 0, v.Capacity - 1, from);

    /// <summary>
    /// The fuel a hauler brings a station and where from: more of what its fuel slot burns,
    /// or for an empty slot the first fuel the recipe burns that storage gives out.
    /// </summary>
    private (string Fuel, Thing Source)? FuelSource(Thing t, Vector2 from) {
        if (t.Burner is not Inventory burner) return null;
        if (burner.First() is string held) return world.FuelOf(t) == held && world.SourceFor(held, from, t.Id) is Thing source ? (held, source) : null;
        // One pass over storage: the first fuel in the recipe's order that any of it gives out, from the nearest that does.
        var fuels = world.Fuels(t);
        Thing? best = null; int rank = fuels.Count; float nearest = 0;
        foreach (var kind in World.Stores)
            foreach (var s in world.ThingsOf(kind))
                for (int i = 0; i < fuels.Count && i <= rank; i++) {
                    if (s.Inventory.Free(fuels[i]) <= 0 || !s.Inventory.AllowsWithdrawal(fuels[i])) continue;
                    float distance = s.Pos.DistanceSquaredTo(from);
                    if (i < rank || distance < nearest) { best = s; rank = i; nearest = distance; }
                    break;
                }
        return best == null ? null : (fuels[rank], best);
    }
}
