using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>A farm plot's part of the Haul job: seed from its own harvest goes back into its input slots, what a sowing takes comes in and the harvest goes out.</summary>
internal sealed class FarmHaul : PlaceHaul {
    public FarmHaul(World world) : base(world) { }

    public override void Carries(Worker w, Thing farm, ref Carry best) {
        var plot = world.Data.Farm;
        foreach (var (item, sowing) in plot.Sow) {
            int room = world.Farms.SowRoom(farm, item);
            if (room <= 0) continue;
            if (farm.Output?.Free(item) > 0) Offer(ref best, Bring(farm, farm, item, room, farm.Inventory[item], sowing) with { Box = CarryBox.Output });
            else if (world.SourceFor(item, w.Pos, farm.Id) is Thing source) Offer(ref best, Bring(farm, source, item, room, farm.Inventory[item], sowing));
        }
        if (farm.Output == null) return;
        foreach (var (item, _) in farm.Output.Items) {
            int free = farm.Output.Free(item);
            if (free == 0) continue;
            // A harvest waits on the plot until its output has room for all of it.
            bool full = farm.Output.Room(item) < plot.Yield.GetValueOrDefault(item);
            if (Taker(farm, item, free, farm.Output.Object(item)) is (Thing store, int count)) Offer(ref best, TakeOut(farm, store, CarryBox.Output, item, count, full));
            // With no storage for it, spare seed stacks up in the plot's own input slots.
            else if (plot.Sow.ContainsKey(item) && farm.Inventory.SlotRoom(item) > 0)
                Offer(ref best, TakeOut(farm, farm, CarryBox.Output, item, System.Math.Min(free, farm.Inventory.SlotRoom(item)), full));
        }
    }

    public override string Wait(Thing farm) =>
        farm.Inventory.Short(world.Data.Farm.Sow) is string seed ? world.Phrase("Needs ", world.Data.Name(seed), " in output-enabled storage") : base.Wait(farm);
}
