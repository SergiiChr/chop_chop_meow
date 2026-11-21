namespace ChopChopMeow;

/// <summary>
/// The lake's part of the Gather job: a water collector takes bottles with room from storage, as many as their hands hold, fills one each gather at the water and stores them.
/// Gathering never creates packaging.
/// </summary>
internal sealed class WaterGathering : Gathering {
    public WaterGathering(World world) : base(world) { }

    public override bool Work(Worker worker, Duty duty, Thing? spot, float dt) {
        string? carried = Water.FindBottle(worker.Bag);
        if (carried != null && carried != Water.Full) {
            worker.Loading = true;
            worker.Status = "Collecting water";
            if (!world.Walk(worker, Terrain.WaterPoint(worker.Pos), dt, 2.5f)) return true;
            worker.WorkState = WorkerWorkState.Gathering;
            worker.Timer += dt;
            if (worker.Timer < world.Data.GatherSeconds) return true;
            worker.Timer = 0; worker.Bag.Replace(carried, Water.Full); world.Got(Water.Full);
            return true;
        }
        // Every bottle in hand is full: off they go.
        if (worker.Loading) { worker.Loading = false; return true; }
        Thing? source = null; string item = Water.Empty;
        foreach (var candidate in Water.BottleItems) {
            if (candidate == Water.Full) break;
            source = world.SourceFor(candidate, worker.Pos);
            if (source != null) { item = candidate; break; }
        }
        if (source == null) { worker.Status = "Needs refillable bottles in output-enabled storage"; return false; }
        if (world.DepositFor(Water.Full, worker.Pos) == null) { worker.Status = "Needs storage for filled bottles"; return false; }
        if (!world.HandsFree(worker, dt)) return true;
        worker.Status = "Fetching empty bottles";
        if (world.Walk(worker, source.Pos, dt)) { source.Inventory.TransferTo(worker.Bag, item, Worker.BagStack, true); worker.Loading = worker.Bag.Count > 0; }
        return true;
    }
}
