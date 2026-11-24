using System;

namespace ChopChopMeow;

/// <summary>
/// The Haul job: what a hauler carries next for the places of a haul job, alone or in a haul group.
/// Each kind of place offers its carries through its own <see cref="PlaceHaul"/>, and the best of them across the job's places is carried, one at a time.
/// Inputs and fuel fill to a stack.
/// With no carry anywhere, the job's barrels are filled and its dry farm plots watered from the worker's tool slot.
/// </summary>
internal sealed class Hauling(World world, BarrelHaul barrels) {
    private readonly StationHaul stations = new(world);
    private readonly FarmHaul farms = new(world);
    private readonly MineHaul mines = new(world);
    private readonly PackerHaul packers = new(world);
    private readonly StoreHaul stores = new(world);

    /// <summary>A haul job, alone or a haul group: carries the best load across its places, one at a time.</summary>
    public bool Haul(Worker w, JobEntry job, float dt) {
        if (w.Plan.Place is not Thing planned || !Covers(job, planned) || !Stands(w.Plan)) w.Plan = NextCarry(w, job);
        if (w.Plan.Place is not Thing place) {
            w.Status = Wait(job);
            return barrels.Haul(w, job, dt);
        }
        // The job in hand is the haul at the place the carry is for, also when a haul group picked it.
        w.WaterWalk = null;
        w.Assignment = place.Id; w.Doing = JobKind.Haul;
        if (!world.HandsFree(w, dt)) return true;
        var c = w.Plan;
        w.Status = CarryStatus(c, place);
        if (!world.Walk(w, c.From!.Pos, dt)) return true;
        var box = BoxOf(c);
        // Seed from a plot's own harvest goes straight back into its input slots.
        if (c.From == c.To) box.TransferTo(c.To!.Inventory, c.Item, Math.Min(c.Count, c.To.Inventory.SlotRoom(c.Item)), true);
        else {
            // A load carried out is sized again at pickup, since where it goes may have filled while the hauler walked here.
            int count = c.From == place && !c.Clears ? Takes(world, c.To!, c.Item, c.Count) : c.Count;
            box.TransferTo(w.Bag, c.Item, count, true); w.Destination = c.To!.Id; w.Fueling = c.Fuel;
        }
        w.Plan = default;
        return true;
    }

    /// <summary>How many of the free units of an item a place a load goes to takes, as far as it has room; vessels that pour there go as many as its liters of room take.</summary>
    internal static int Takes(World world, Thing to, string item, int free) {
        if (world.Intake(to, item, false) is not (Inventory into, int room)) return 0;
        if (world.PoursInto(to, into, item) is VesselDef pours) room = (room + pours.LitersIn(item) - 1) / pours.LitersIn(item);
        return Math.Min(free, room);
    }

    /// <summary>The part of the job a kind of place offers.</summary>
    private PlaceHaul For(Thing place) => place.Kind switch {
        ThingKind.Barrel or ThingKind.Hut => barrels,
        ThingKind.Mine => mines,
        ThingKind.Farm => farms,
        ThingKind.Packer => packers,
        ThingKind.Chest or ThingKind.Crate => stores,
        _ => stations
    };

    /// <summary>What a hauler on the way to pick up a carry says.</summary>
    private string CarryStatus(in Carry c, Thing place) {
        var data = world.Data;
        if (c.From == c.To) return world.Phrase("Sowing back ", data.Name(c.Item));
        if (c.Clears) return world.Phrase("Fetching ", world.Lower(data.Name(c.Item)), "s for leftover ", world.Lower(data.Name(data.VesselOf(c.Item)!.Liquid)));
        if (c.Box == CarryBox.Burner) return world.Phrase("Clearing ", world.Lower(data.Name(c.Item)), " from the fire");
        // Every packed crate has a name of its own, so the status names none.
        if (c.From == place) return World.IsPacked(c.Item) ? "Carrying out a packed crate" : world.Phrase("Carrying out ", data.Name(c.Item));
        if (c.Fuel) return world.Phrase("Fetching ", data.Name(c.Item), " for the fire");
        return place.Kind == ThingKind.Mine ? world.Phrase("Fetching ", data.Name(c.Item), " for the ", Mines.Name(place)) : world.Phrase("Fetching ", data.Name(c.Item));
    }

    /// <summary>Whether a haul job still holds a place: its own, or one of a haul group's.</summary>
    internal static bool Covers(JobEntry job, Thing place) {
        if (job is Duty duty) return duty.Thing == place.Id;
        foreach (var haul in ((HaulGroup)job).Hauls) if (haul.Thing == place.Id) return true;
        return false;
    }

    private static Inventory BoxOf(in Carry c) => c.Box switch { CarryBox.Output => c.From!.Output!, CarryBox.Burner => c.From!.Burner!, _ => c.From!.Inventory };

    /// <summary>Whether a carry a hauler set out on can still be made: its places stand, the load is still there to take and where it goes still takes it.</summary>
    private bool Stands(in Carry c) =>
        world.Find(c.Place!.Id) == c.Place && world.Find(c.From!.Id) == c.From && world.Find(c.To!.Id) == c.To && BoxOf(c).Free(c.Item) > 0
        && (c.From == c.To ? c.To.Inventory.SlotRoom(c.Item) > 0 : c.Clears || world.Intake(c.To, c.Item, c.Fuel) != null);

    /// <summary>What a hauler with nothing to carry says: what the place lacks, or for a haul group its first place.</summary>
    private string Wait(JobEntry job) {
        if (job is Duty duty) return world.Find(duty.Thing) is Thing place ? For(place).Wait(place) : "Waiting for the next load";
        foreach (var haul in ((HaulGroup)job).Hauls) if (world.Find(haul.Thing) is Thing place) return For(place).Wait(place);
        return "No hauls in this group";
    }

    /// <summary>The best carry across a haul job's places: one place, or every place of a haul group.</summary>
    private Carry NextCarry(Worker w, JobEntry job) {
        Carry best = default;
        if (job is Duty duty) { if (world.Find(duty.Thing) is Thing place) For(place).Carries(w, place, ref best); } else
            foreach (var haul in ((HaulGroup)job).Hauls) if (world.Find(haul.Thing) is Thing place) For(place).Carries(w, place, ref best);
        return best;
    }
}
