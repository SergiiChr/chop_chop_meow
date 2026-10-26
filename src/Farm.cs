using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChopChopMeow;

/// <summary>Farm plots: sown with seeds and fertilizer, growing wheat for days, then harvested by hand with shared progress.</summary>
public sealed partial class World {
    /// <summary>Values of <see cref="Thing.Stage"/> for a farm plot: waiting for seeds and fertilizer, growing, and ripe for harvest.</summary>
    public const int FarmFallow = 0, FarmGrowing = 1, FarmRipe = 2;
    public float GrowSeconds => Data.Farm.GrowDays * Data.DaySeconds;
    public float HarvestSeconds => Data.Farm.HarvestWorkdays * Data.WorkdaySeconds;

    /// <summary>What the plot shows: 0 bare, 1 sown soil, 2 small green straws, 3 tall green plants, 4 yellow wheat.</summary>
    public static int CropLook(Thing farm, float growSeconds) => farm.Stage switch {
        FarmRipe => 4,
        FarmGrowing => 1 + Math.Min(2, (int)(farm.Progress / growSeconds * 3)),
        _ => 0
    };

    /// <summary>A fallow plot sows itself once it holds seeds and fertilizer; growing wheat ripens after its days.</summary>
    private void FarmTick(Thing farm, float dt) {
        // The plot keeps seed from its harvest for two more sowings.
        if (farm.Output != null) foreach (var (item, amount) in Data.Farm.Sow) if (farm.Inventory[item] < amount * 2 && farm.Output[item] > 0) farm.Output.TransferTo(farm.Inventory, item, amount * 2 - farm.Inventory[item]);
        if (farm.Stage == FarmFallow && farm.Inventory.Has(Data.Farm.Sow)) { farm.Inventory.Spend(Data.Farm.Sow); farm.Stage = FarmGrowing; farm.Progress = 0; }
        if (farm.Stage == FarmGrowing && (farm.Progress += dt) >= GrowSeconds) { farm.Stage = FarmRipe; farm.Progress = 0; }
    }

    /// <summary>
    /// Adds harvest work, from the player or any number of workers, to the plot's one shared progress.
    /// A finished harvest waits at full progress until the plot has room for the yield, so nothing is lost.
    /// </summary>
    private void Harvest(Thing farm, float dt) {
        if (farm.Stage != FarmRipe) return;
        farm.Progress = Math.Min(HarvestSeconds, farm.Progress + dt);
        if (farm.Progress < HarvestSeconds) return;
        var yield = farm.Output ?? farm.Inventory;
        foreach (var pair in Data.Farm.Yield) if (yield.Room(pair.Key) < pair.Value) return;
        foreach (var (item, amount) in Data.Farm.Yield) yield.Add(item, amount);
        farm.Stage = FarmFallow; farm.Progress = 0;
    }

    /// <summary>
    /// A farm hand carries out the harvest, keeping seed for two more sowings, fetches what the next sowing lacks,
    /// waits by the plot while the wheat grows, and harvests it when ripe.
    /// </summary>
    /// <summary>A farmer harvests the ripe wheat; a hauler carries out what is beyond two sowings and fetches what the next sowing lacks.</summary>
    private bool FarmWork(Worker w, Thing farm, float dt) {
        if (HasDuty(w, farm.Id, "haul")) {
            // Each pass ends with a return, so the transfer never changes the stacks while they are still being walked.
            if (farm.Output != null) foreach (var (item, count) in farm.Output.Items) {
                if (DepositFor(item, farm.Pos) is not Thing dest) continue;
                w.Status = Phrase("Carrying out ", Data.Name(item));
                if (Walk(w, farm.Pos, dt)) { farm.Output.TransferTo(w.Bag, item, Math.Min(count, w.Bag.StackSize), true); w.Destination = dest.Id; }
                return true;
            }
            if (farm.Stage == FarmFallow)
                foreach (var (item, amount) in Data.Farm.Sow) {
                    if (farm.Inventory[item] >= amount) continue;
                    var source = SourceFor(item, w.Pos);
                    if (source == null) { w.Status = Phrase("Needs ", Data.Name(item), " in output-enabled storage"); return false; }
                    w.Status = Phrase("Fetching ", Data.Name(item));
                    if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, item, amount - farm.Inventory[item], true); w.Destination = farm.Id; }
                    return true;
                }
        }
        bool farms = HasDuty(w, farm.Id, "farm");
        if (farm.Stage != FarmRipe) { w.Status = "Waiting for the wheat to grow"; if (farms) Walk(w, farm.Pos, dt); return false; }
        if (!farms) { w.Status = "Waiting for the harvest"; return false; }
        w.Status = Phrase("Harvesting wheat · ", Number(farm.Progress / HarvestSeconds * 100), "%");
        if (!Walk(w, farm.Pos, dt)) return true;
        w.WorkState = WorkerWorkState.Gathering;
        Harvest(farm, dt);
        return true;
    }

    public string FarmStatus(Thing farm) {
        var text = new StringBuilder();
        WriteFarmStatus(farm, text);
        return text.ToString();
    }

    /// <summary>Writes <see cref="FarmStatus"/> into a reused buffer, for a panel that refreshes while open.</summary>
    public void WriteFarmStatus(Thing farm, StringBuilder text) {
        if (farm.Stage == FarmRipe) { text.Append($"Ripe · harvested {farm.Progress / HarvestSeconds * 100:0}%. Hold right mouse on the plot to harvest."); return; }
        if (farm.Stage == FarmGrowing) { text.Append($"Growing · ripe in {(GrowSeconds - farm.Progress) / Data.DaySeconds * 24:0} hours"); return; }
        text.Append("Fallow · sow it with ");
        bool first = true;
        foreach (var (item, amount) in Data.Farm.Sow) {
            if (!first) text.Append(" and ");
            first = false;
            text.Append($"{amount} {Data.Name(item)}");
        }
    }
}
