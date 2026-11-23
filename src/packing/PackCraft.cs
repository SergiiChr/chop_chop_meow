namespace ChopChopMeow;

/// <summary>A packing station's part of the Craft job: a packer works it while it has its good and a crate to pack it into; each crate filled is a round.</summary>
internal sealed class PackCraft : Crafting {
    public PackCraft(World world) : base(world) { }

    public override bool Work(Worker w, Thing station, float dt) {
        if (!world.CanPack(station)) { w.Status = station.Packs == "" ? "Choose what to pack" : "Waiting for goods and crates"; return false; }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Packing crates";
        if (world.Walk(w, station.ToWorld(World.PackStand), dt, .1f)) w.WorkState = WorkerWorkState.Hammering;
        return true;
    }
}
