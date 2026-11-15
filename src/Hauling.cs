using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Which of a place's boxes a hauler picks a load up from.</summary>
internal enum CarryBox { Inventory, Output, Burner }

/// <summary>One carry a hauler makes for a place: an input or fuel brought in, or an output taken out.</summary>
internal struct Carry {
    /// <summary>The place hauled for, or null for no carry.</summary>
    public Thing? Place;
    /// <summary>Where the load is picked up, and where it goes: an output leaves the place, an input comes to it.</summary>
    public Thing? From, To;
    public CarryBox Box;
    public string Item;
    public int Count;
    /// <summary>The load goes into the fuel slot.</summary>
    public bool Fuel;
    /// <summary>The load is vessels with room, brought to take out liquid the place's recipe no longer uses.</summary>
    public bool Clears;
    /// <summary>Which of the four haul rules offers the carry, the first before the others.</summary>
    public int Rule;
    /// <summary>Its place among the carries of its rule, lowest first.</summary>
    public float Order;
}

/// <summary>
/// Hauling: what a hauler carries next for the places of a haul job.
/// Four rules pick it, in order: an input or fuel slot short of one batch, an output that is full,
/// the input that covers the fewest batches, the output that holds the most.
/// Inputs and fuel fill to a stack.
/// </summary>
public sealed partial class World {
    /// <summary>A haul job, alone or a haul group: carries the best load across its places, one at a time.</summary>
    private bool Haul(Worker w, JobEntry job, float dt) {
        if (w.Plan.Place is not Thing planned || !Covers(job, planned) || !Stands(w.Plan)) w.Plan = NextCarry(w, job);
        if (w.Plan.Place is not Thing place) { w.Status = HaulWait(job); return false; }
        // The job in hand is the haul at the place the carry is for, also when a haul group picked it.
        w.Assignment = place.Id; w.Doing = JobKind.Haul;
        if (!HandsFree(w, dt)) return true;
        var c = w.Plan;
        w.Status = CarryStatus(c, place);
        if (!Walk(w, c.From!.Pos, dt)) return true;
        var box = BoxOf(c);
        // Seed from a plot's own harvest goes straight back into its input slots.
        if (c.From == c.To) box.TransferTo(c.To!.Inventory, c.Item, c.Count, true);
        else { box.TransferTo(w.Bag, c.Item, c.Count, true); w.Destination = c.To!.Id; w.Fueling = c.Fuel; }
        w.Plan = default;
        return true;
    }

    /// <summary>What a hauler on the way to pick up a carry says.</summary>
    private string CarryStatus(in Carry c, Thing place) {
        if (c.From == c.To) return Phrase("Sowing back ", Data.Name(c.Item));
        if (c.Clears) return Phrase("Fetching ", Lower(Data.Name(c.Item)), "s for leftover ", Lower(Data.Name(Data.VesselOf(c.Item)!.Liquid)));
        if (c.Box == CarryBox.Burner) return Phrase("Clearing ", Lower(Data.Name(c.Item)), " from the fire");
        // Every packed crate has a name of its own, so the status names none.
        if (c.From == place) return c.Item.StartsWith(PackedPrefix) ? "Carrying out a packed crate" : Phrase("Carrying out ", Data.Name(c.Item));
        if (c.Fuel) return Phrase("Fetching ", Data.Name(c.Item), " for the fire");
        return place.Kind == ThingKind.Mine ? Phrase("Fetching ", Data.Name(c.Item), " for the ", MineName(place)) : Phrase("Fetching ", Data.Name(c.Item));
    }

    /// <summary>Whether a haul job still holds a place: its own, or one of a haul group's.</summary>
    private static bool Covers(JobEntry job, Thing place) {
        if (job is Duty duty) return duty.Thing == place.Id;
        foreach (var haul in ((HaulGroup)job).Hauls) if (haul.Thing == place.Id) return true;
        return false;
    }

    private static Inventory BoxOf(in Carry c) => c.Box switch { CarryBox.Output => c.From!.Output!, CarryBox.Burner => c.From!.Burner!, _ => c.From!.Inventory };

    /// <summary>Whether a carry a hauler set out on can still be made: its places stand, the load is still there to take and where it goes still takes it.</summary>
    private bool Stands(in Carry c) =>
        Find(c.Place!.Id) == c.Place && Find(c.From!.Id) == c.From && Find(c.To!.Id) == c.To && BoxOf(c).Free(c.Item) > 0
        && (c.From == c.To || c.Clears || Intake(c.To, c.Item, c.Fuel) != null);

    /// <summary>What a hauler with nothing to carry says: what the place lacks, or for a haul group its first place.</summary>
    private string HaulWait(JobEntry job) {
        if (job is Duty duty) return Find(duty.Thing) is Thing place ? HaulWait(place) : "Waiting for the next load";
        foreach (var haul in ((HaulGroup)job).Hauls) if (Find(haul.Thing) is Thing place) return HaulWait(place);
        return "No hauls in this group";
    }

    private string HaulWait(Thing place) => place.Kind switch {
        ThingKind.Mine when place.MineStage == MineStage.Dug => MineWants(place),
        ThingKind.Packer when place.Packs == "" => "Choose what to pack",
        ThingKind.Packer => "Waiting for goods and crates",
        ThingKind.Farm when Short(place.Inventory, Data.Farm.Sow) is string seed => Phrase("Needs ", Data.Name(seed), " in output-enabled storage"),
        _ when IsStation(place.Kind) && !Data.Recipes.ContainsKey(place.Recipe) => "Nothing queued or marked to make",
        _ when IsStation(place.Kind) && !place.Processing && Short(place.Inventory, BatchCost(place)) is string input => Phrase("Needs ", Data.Name(input), " in output-enabled storage"),
        _ => "Waiting for the next load"
    };

    /// <summary>The first item a box holds less of than one batch takes, or null.</summary>
    private static string? Short(Inventory box, Dictionary<string, int> batch) {
        foreach (var (item, needed) in batch) if (box[item] < needed) return item;
        return null;
    }

    /// <summary>The best carry across a haul job's places: one place, or every place of a haul group.</summary>
    private Carry NextCarry(Worker w, JobEntry job) {
        Carry best = default;
        if (job is Duty duty) { if (Find(duty.Thing) is Thing place) Carries(w, place, ref best); } else
            foreach (var haul in ((HaulGroup)job).Hauls) if (Find(haul.Thing) is Thing place) Carries(w, place, ref best);
        return best;
    }

    private void Carries(Worker w, Thing place, ref Carry best) {
        switch (place.Kind) {
            case ThingKind.Mine: MineCarries(w, place, ref best); break;
            case ThingKind.Farm: FarmCarries(w, place, ref best); break;
            case ThingKind.Packer: PackerCarries(w, place, ref best); break;
            default: StationCarries(w, place, ref best); break;
        }
    }

    /// <summary>Keeps the better of two carries: the earlier rule, then the lower order, then the one offered first.</summary>
    private static void Offer(ref Carry best, in Carry c) {
        if (c.Count > 0 && (best.Place == null || c.Rule < best.Rule || c.Rule == best.Rule && c.Order < best.Order)) best = c;
    }

    /// <summary>A carry that brings a place an input: short of one batch it comes under the first rule, otherwise under the third, fewest batches covered first.</summary>
    private static Carry Bring(Thing place, Thing from, string item, int count, int have, int batch) =>
        new() { Place = place, From = from, To = place, Item = item, Count = count, Rule = have < batch ? 1 : 3, Order = have / (float)batch };

    /// <summary>A carry that takes an output out of a place: a full one comes under the second rule, otherwise under the fourth, most held first.</summary>
    private static Carry TakeOut(Thing place, Thing to, CarryBox box, string item, int count, bool full) =>
        new() { Place = place, From = place, To = to, Box = box, Item = item, Count = count, Rule = full ? 2 : 4, Order = -count };

    /// <summary>The room an input slot has left up to a stack.</summary>
    private static int SlotRoom(Inventory box, string item) => Math.Min(box.Room(item), Math.Max(box.StackSize, box.StackOf(item)) - box[item]);

    private void StationCarries(Worker w, Thing t, ref Carry best) {
        RecipeDef? recipe = Data.Recipes.GetValueOrDefault(t.Recipe);
        foreach (var (item, _) in t.Inventory.Items) {
            int free = t.Inventory.Free(item);
            if (free == 0 || Pending(t, item)) continue;
            // What another recipe left keeps the station from starting a batch, so it leaves as a full output does.
            bool full = recipe != null && (item != recipe.Output || SlotRoom(t.Inventory, item) < recipe.Amount);
            if (Data.VesselFor(item) is VesselDef v) {
                // A liquid leaves in vessels with room for it, which the hauler brings, fills here and stores.
                if (DepositFor(v.Full, t.Pos, t.Id) == null || RoomySource(v, w.Pos) is not (string roomy, Thing holder)) continue;
                Offer(ref best, new Carry { Place = t, From = holder, To = t, Item = roomy, Count = (free + v.Capacity - 1) / v.Capacity, Clears = true, Rule = full ? 2 : 4, Order = -free });
            } else if (DepositFor(item, t.Pos, t.Id) is Thing store) Offer(ref best, TakeOut(t, store, CarryBox.Inventory, item, free, full));
        }
        if (recipe == null) return;
        foreach (var (input, batch) in recipe.Inputs) {
            int have = t.Inventory[input], room = SlotRoom(t.Inventory, input);
            if (room <= 0) continue;
            // A liquid comes in the vessels that pour it, enough of them for the liters of room.
            if (Data.VesselFor(input) is VesselDef carrier) {
                if (LiquidSource(carrier, w.Pos) is (string filled, Thing holder)) Offer(ref best, Bring(t, holder, filled, (room + carrier.LitersIn(filled) - 1) / carrier.LitersIn(filled), have, batch));
            } else if (SourceFor(input, w.Pos, t.Id) is Thing source) Offer(ref best, Bring(t, source, input, room + FuelRoom(t, recipe, input), have, batch));
        }
        if (recipe.Fuels.Count == 0 || t.Burner is not Inventory burner) return;
        if (burner.First() is string held && FuelOf(t) == null) {
            // Fuel the recipe does not burn leaves the fuel slot, so the right one can go in.
            if (!t.Processing && DepositFor(held, t.Pos, t.Id) is Thing store) Offer(ref best, new Carry { Place = t, From = t, To = store, Box = CarryBox.Burner, Item = held, Count = burner.Free(held), Rule = 1 });
        } else if (FuelSource(t, w.Pos) is (string fuel, Thing from))
            Offer(ref best, Bring(t, from, fuel, burner.Room(fuel) + (recipe.Inputs.ContainsKey(fuel) ? Math.Max(0, SlotRoom(t.Inventory, fuel)) : 0), burner[fuel], 1) with { Fuel = true });
    }

    /// <summary>The room a station's fuel slot has for an input that its recipe also burns; a load of it is shared between the two slots.</summary>
    private int FuelRoom(Thing t, RecipeDef recipe, string input) => t.Burner is Inventory burner && recipe.Fuels.Contains(input) && FuelTakes(t, input) ? burner.Room(input) : 0;

    /// <summary>The nearest storage with vessels that have room for a liquid, emptiest first, and that vessel; or null.</summary>
    private (string Item, Thing Source)? RoomySource(VesselDef v, Vector2 from) => NearestFill(v, 0, v.Capacity - 1, from);

    /// <summary>
    /// The fuel a hauler brings a station and where from: more of what its fuel slot burns,
    /// or for an empty slot the first fuel the recipe burns that storage gives out.
    /// </summary>
    private (string Fuel, Thing Source)? FuelSource(Thing t, Vector2 from) {
        if (t.Burner is not Inventory burner) return null;
        if (burner.First() is string held) return FuelOf(t) == held && SourceFor(held, from, t.Id) is Thing source ? (held, source) : null;
        // One pass over storage: the first fuel in the recipe's order that any of it gives out, from the nearest that does.
        var fuels = Fuels(t);
        Thing? best = null; int rank = fuels.Count; float nearest = 0;
        foreach (var kind in Stores)
            foreach (var s in S.Things.Of(kind))
                for (int i = 0; i < fuels.Count && i <= rank; i++) {
                    if (s.Inventory.Free(fuels[i]) <= 0 || !s.Inventory.AllowsWithdrawal(fuels[i])) continue;
                    float distance = s.Pos.DistanceSquaredTo(from);
                    if (i < rank || distance < nearest) { best = s; rank = i; nearest = distance; }
                    break;
                }
        return best == null ? null : (fuels[rank], best);
    }

    /// <summary>A plot's hauler puts seed from its own harvest back into its input slots, brings what a sowing takes and carries the harvest out.</summary>
    private void FarmCarries(Worker w, Thing farm, ref Carry best) {
        foreach (var (item, sowing) in Data.Farm.Sow) {
            int room = SowRoom(farm, item);
            if (room <= 0) continue;
            if (farm.Output?.Free(item) > 0) Offer(ref best, Bring(farm, farm, item, room, farm.Inventory[item], sowing) with { Box = CarryBox.Output });
            else if (SourceFor(item, w.Pos, farm.Id) is Thing source) Offer(ref best, Bring(farm, source, item, room, farm.Inventory[item], sowing));
        }
        if (farm.Output == null) return;
        foreach (var (item, _) in farm.Output.Items) {
            int free = farm.Output.Free(item);
            if (free == 0) continue;
            // A harvest waits on the plot until its output has room for all of it.
            bool full = farm.Output.Room(item) < Data.Farm.Yield.GetValueOrDefault(item);
            if (DepositFor(item, farm.Pos, farm.Id) is Thing store) Offer(ref best, TakeOut(farm, store, CarryBox.Output, item, free, full));
            // With no storage for it, spare seed stacks up in the plot's own input slots.
            else if (Data.Farm.Sow.ContainsKey(item) && SlotRoom(farm.Inventory, item) > 0)
                Offer(ref best, TakeOut(farm, farm, CarryBox.Output, item, Math.Min(free, SlotRoom(farm.Inventory, item)), full));
        }
    }

    /// <summary>A mine site's hauler brings the materials its frame still lacks.</summary>
    private void MineCarries(Worker w, Thing mine, ref Carry best) {
        if (mine.MineStage != MineStage.Dug) return;
        foreach (var (item, wanted) in Data.Mine.Materials) {
            int need = MineNeed(mine, item);
            if (need > 0 && SourceFor(item, w.Pos, mine.Id) is Thing source) Offer(ref best, Bring(mine, source, item, need, wanted - need, wanted));
        }
    }

    /// <summary>A packing station's hauler brings its good and empty crates, and carries out the full crate and anything it no longer packs.</summary>
    private void PackerCarries(Worker w, Thing t, ref Carry best) {
        if (t.Output!.First() is string crate && !OpenCrate(t, crate) && t.Output.Free(crate) > 0 && DepositFor(crate, t.Pos, t.Id) is Thing shelf)
            Offer(ref best, TakeOut(t, shelf, CarryBox.Output, crate, 1, true));
        foreach (var (item, _) in t.Inventory.Items) {
            int free = t.Inventory.Free(item);
            if (free > 0 && item != t.Packs && item != "crate" && DepositFor(item, t.Pos, t.Id) is Thing store) Offer(ref best, TakeOut(t, store, CarryBox.Inventory, item, free, false));
        }
        if (t.Packs == "") return;
        // A batch is one crate: a crate's worth of the good and one empty crate.
        if (SlotRoom(t.Inventory, t.Packs) is > 0 and int room && PackerTakes(t, t.Packs) && SourceFor(t.Packs, w.Pos, t.Id) is Thing goods)
            Offer(ref best, Bring(t, goods, t.Packs, room, t.Inventory[t.Packs], Data.CrateCapacity));
        if (SlotRoom(t.Inventory, "crate") is > 0 and int crates && SourceFor("crate", w.Pos, t.Id) is Thing stack)
            Offer(ref best, Bring(t, stack, "crate", crates, t.Inventory["crate"], 1));
    }
}
