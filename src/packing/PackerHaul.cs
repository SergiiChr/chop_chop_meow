namespace ChopChopMeow;

/// <summary>A packing station's part of the Haul job: it brings its good and empty crates, and carries out the full crate and anything it no longer packs.</summary>
internal sealed class PackerHaul : PlaceHaul {
    public PackerHaul(World world) : base(world) { }

    public override void Carries(Worker w, Thing t, ref Carry best) {
        if (t.Output!.First() is string crate && !world.OpenCrate(t, crate) && t.Output.Free(crate) > 0 && world.DepositFor(crate, t.Pos, t.Id) is Thing shelf)
            Offer(ref best, TakeOut(t, shelf, CarryBox.Output, crate, 1, true));
        foreach (var (item, _) in t.Inventory.Items) {
            int free = t.Inventory.Free(item);
            if (free > 0 && !World.IsProduct(t.Packs, item) && item != "crate" && Taker(t, item, free) is (Thing store, int count)) Offer(ref best, TakeOut(t, store, CarryBox.Inventory, item, count, false));
        }
        if (t.Packs == "") return;
        // A batch is one crate: a crate's worth of the good and one empty crate.
        if (World.PackingRoom(t, t.Packs) is > 0 and int room && world.SourceForItem(t.Packs, w.Pos, t.Id) is (Thing goods, string stockItem)) {
            int stocked = t.Inventory.CountOfKind(t.Packs);
            Offer(ref best, Bring(t, goods, stockItem, room, stocked, world.Data.CrateCapacity));
        }
        if (World.PackingRoom(t, "crate") is > 0 and int crates && world.SourceFor("crate", w.Pos, t.Id) is Thing stack)
            Offer(ref best, Bring(t, stack, "crate", crates, t.Inventory["crate"], 1));
    }

    public override string Wait(Thing t) => t.Packs == "" ? "Choose what to pack" : "Waiting for goods and crates";
}
