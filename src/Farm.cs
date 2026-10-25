using System;
using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Farm plots: sown with seeds and fertilizer, growing wheat for days, then harvested by hand with shared progress.</summary>
public sealed partial class World
{
    /// <summary>Values of <see cref="Thing.Stage"/> for a farm plot: waiting for seeds and fertilizer, growing, and ripe for harvest.</summary>
    public const int FarmFallow = 0, FarmGrowing = 1, FarmRipe = 2;
    public float GrowSeconds => Data.Farm.GrowDays * Data.DaySeconds;
    public float HarvestSeconds => Data.Farm.HarvestWorkdays * Data.WorkdaySeconds;

    /// <summary>What the plot shows: 0 bare, 1 sown soil, 2 small green straws, 3 tall green plants, 4 yellow wheat.</summary>
    public static int CropLook(Thing farm, float growSeconds) => farm.Stage switch
    {
        FarmRipe => 4,
        FarmGrowing => 1 + Math.Min(2, (int)(farm.Progress / growSeconds * 3)),
        _ => 0
    };
    private bool FarmItem(string item) => Data.Farm.Sow.ContainsKey(item) || Data.Farm.Yield.ContainsKey(item);

    /// <summary>A fallow plot sows itself once it holds seeds and fertilizer; growing wheat ripens after its days.</summary>
    private void FarmTick(Thing farm, float dt)
    {
        if (farm.Stage == FarmFallow && farm.Inventory.Has(Data.Farm.Sow)) { farm.Inventory.Spend(Data.Farm.Sow); farm.Stage = FarmGrowing; farm.Progress = 0; }
        if (farm.Stage == FarmGrowing && (farm.Progress += dt) >= GrowSeconds) { farm.Stage = FarmRipe; farm.Progress = 0; }
    }

    /// <summary>
    /// Adds harvest work, from the player or any number of workers, to the plot's one shared progress.
    /// A finished harvest waits at full progress until the plot has room for the yield, so nothing is lost.
    /// </summary>
    private void Harvest(Thing farm, float dt)
    {
        if (farm.Stage != FarmRipe) return;
        farm.Progress = Math.Min(HarvestSeconds, farm.Progress + dt);
        if (farm.Progress < HarvestSeconds) return;
        foreach (var pair in Data.Farm.Yield) if (farm.Inventory.Room(pair.Key) < pair.Value) return;
        foreach (var (item, amount) in Data.Farm.Yield) farm.Inventory.Add(item, amount);
        farm.Stage = FarmFallow; farm.Progress = 0;
    }

    /// <summary>
    /// A farm hand carries out the harvest, keeping seed for two more sowings, fetches what the next sowing lacks,
    /// waits by the plot while the wheat grows, and harvests it when ripe.
    /// </summary>
    private void FarmWork(Worker w, Thing farm, float dt)
    {
        // Each pass ends with a return, so the transfer never changes the stacks while they are still being walked.
        foreach (var (item, count) in farm.Inventory.Items)
        {
            int spare = count - Data.Farm.Sow.GetValueOrDefault(item) * 2;
            if (spare <= 0 || DepositFor(item, farm.Pos) is not Thing dest) continue;
            w.Status = Phrase("Carrying out ", Data.Name(item));
            if (Walk(w, farm.Pos, dt)) { farm.Inventory.TransferTo(w.Bag, item, Math.Min(spare, w.Bag.StackSize), true); w.Destination = dest.Id; }
            return;
        }
        if (farm.Stage == FarmFallow)
            foreach (var (item, amount) in Data.Farm.Sow)
            {
                if (farm.Inventory[item] >= amount) continue;
                var source = SourceFor(item, w.Pos);
                if (source == null) { w.Status = Phrase("Needs ", Data.Name(item), " in output-enabled storage"); return; }
                w.Status = Phrase("Fetching ", Data.Name(item));
                if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, item, amount - farm.Inventory[item], true); w.Destination = farm.Id; }
                return;
            }
        if (farm.Stage != FarmRipe) { w.Status = "Waiting for the wheat to grow"; Walk(w, farm.Pos, dt); return; }
        w.Status = Phrase("Harvesting wheat · ", Number(farm.Progress / HarvestSeconds * 100), "%");
        if (!Walk(w, farm.Pos, dt)) return;
        w.WorkState = WorkerWorkState.Gathering;
        Harvest(farm, dt);
    }

    public string FarmStatus(Thing farm) => farm.Stage switch
    {
        FarmRipe => $"Ripe · harvested {farm.Progress / HarvestSeconds * 100:0}%. Hold right mouse on the plot to harvest.",
        FarmGrowing => $"Growing · ripe in {(GrowSeconds - farm.Progress) / Data.DaySeconds * 24:0} hours",
        _ => $"Fallow · sow it with {string.Join(" and ", Data.Farm.Sow.Select(p => $"{p.Value} {Data.Name(p.Key)}"))}"
    };
}
