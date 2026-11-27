namespace ChopChopMeow;

/// <summary>
/// The lake's part of the Gather job: a water collector takes bottles with room from storage, as many as their hands hold, fills one each gather at the water and stores them.
/// Gathering never creates packaging.
/// </summary>
internal sealed class WaterGathering : Gathering {
    public WaterGathering(World world) : base(world) { }

    public override bool Work(Worker w, Duty duty, Thing? spot, float dt) {
        string? carried = Water.FindBottle(w.Bag);
        if (carried != null && Water.BottleLiters(w.Bag, carried) < Water.Capacity) {
            w.Loading = true;
            w.Status = "Collecting water";
            if (!world.Walk(w, Terrain.WaterPoint(w.Pos), dt, 2.5f)) return true;
            w.WorkState = WorkerWorkState.Gathering;
            w.Timer += dt;
            if (w.Timer < world.Data.GatherSeconds) return true;
            w.Timer = 0; world.SetFill(w.Bag, carried, Water.Liquid, Water.Capacity); world.Got(Water.Full);
            return true;
        }
        // Every bottle in hand is full: off they go.
        if (w.Loading) { w.Loading = false; return true; }
        var bottle = world.Data.VesselOf(Water.Empty, Water.Liquid)!;
        if (world.NearestFill(bottle, 0, bottle.Capacity - 1, w.Pos) is not (string item, Thing source)) { w.Status = "Needs refillable bottles in output-enabled storage"; return false; }
        var full = world.FullVessel(bottle);
        if (world.DepositFor(full.Key, w.Pos, unit: full) == null) { w.Status = "Needs storage for filled bottles"; return false; }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Fetching empty bottles";
        // Part-filled bottles each have a cell of their own: the ones holding as much as the first go along with it.
        if (world.Walk(w, source.Pos, dt)) { world.TransferLike(source.Inventory, w.Bag, item, Worker.BagStack, true); w.Loading = w.Bag.Count > 0; }
        return true;
    }
}
