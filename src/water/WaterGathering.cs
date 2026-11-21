namespace ChopChopMeow;

/// <summary>
/// The lake's part of the Gather job: a water collector takes bottles with room from storage, as many as their hands hold, fills one each gather at the water and stores them.
/// Gathering never creates packaging.
/// </summary>
internal sealed class WaterGathering : Gathering {
    public WaterGathering(World world) : base(world) { }

    public override bool Work(Worker w, Duty duty, Thing? spot, float dt) {
        string? carried = Water.FindBottle(w.Bag);
        if (carried != null && carried != Water.Full) {
            w.Loading = true;
            w.Status = "Collecting water";
            if (!world.Walk(w, Terrain.WaterPoint(w.Pos), dt, 2.5f)) return true;
            w.WorkState = WorkerWorkState.Gathering;
            w.Timer += dt;
            if (w.Timer < world.Data.GatherSeconds) return true;
            w.Timer = 0; w.Bag.Replace(carried, Water.Full); world.Got(Water.Full);
            return true;
        }
        // Every bottle in hand is full: off they go.
        if (w.Loading) { w.Loading = false; return true; }
        Thing? source = null; string item = Water.Empty;
        foreach (var candidate in Water.BottleItems) {
            if (candidate == Water.Full) break;
            source = world.SourceFor(candidate, w.Pos);
            if (source != null) { item = candidate; break; }
        }
        if (source == null) { w.Status = "Needs refillable bottles in output-enabled storage"; return false; }
        if (world.DepositFor(Water.Full, w.Pos) == null) { w.Status = "Needs storage for filled bottles"; return false; }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Fetching empty bottles";
        if (world.Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, item, Worker.BagStack, true); w.Loading = w.Bag.Count > 0; }
        return true;
    }
}
