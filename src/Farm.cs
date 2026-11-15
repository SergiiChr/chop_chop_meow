using System;
using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Where a farm plot is: waiting for seeds and fertilizer, growing, or ripe for harvest.</summary>
public enum FarmStage { Fallow, Growing, Ripe }

/// <summary>Farm plots: sown with seeds and fertilizer, growing wheat for days, then harvested by hand with shared progress.</summary>
public sealed partial class World {
    public float GrowSeconds => Data.Farm.GrowDays * Data.DaySeconds;
    public float HarvestSeconds => Data.Farm.HarvestWorkdays * Data.WorkdaySeconds;

    /// <summary>What the plot shows: 0 bare, 1 sown soil, 2 small green straws, 3 tall green plants, 4 yellow wheat.</summary>
    public static int CropLook(Thing farm, float growSeconds) => farm.FarmStage switch {
        FarmStage.Ripe => 4,
        FarmStage.Growing => 1 + Math.Min(2, (int)(farm.Progress / growSeconds * 3)),
        _ => 0
    };

    /// <summary>A fallow plot sows once seeds and fertilizer are put in its input slots; growing wheat ripens after its days.</summary>
    private void FarmTick(Thing farm, float dt) {
        if (farm.FarmStage == FarmStage.Fallow && farm.Inventory.Has(Data.Farm.Sow)) { farm.Inventory.Spend(Data.Farm.Sow); farm.FarmStage = FarmStage.Growing; farm.Progress = 0; }
        if (farm.FarmStage == FarmStage.Growing && (farm.Progress += dt) >= GrowSeconds) { farm.FarmStage = FarmStage.Ripe; farm.Progress = 0; }
    }

    /// <summary>
    /// Adds harvest work, from the player or any number of workers, to the plot's one shared progress.
    /// A finished harvest waits at full progress until the plot has room for the yield, so nothing is lost.
    /// </summary>
    private void Harvest(Thing farm, float dt) {
        if (farm.FarmStage != FarmStage.Ripe) return;
        farm.Progress = Math.Min(HarvestSeconds, farm.Progress + dt);
        if (farm.Progress < HarvestSeconds) return;
        if (!YieldFits(farm)) return;
        var yield = farm.Output ?? farm.Inventory;
        foreach (var (item, amount) in Data.Farm.Yield) { yield.Add(item, amount); Got(item); }
        farm.FarmStage = FarmStage.Fallow; farm.Progress = 0; farm.Rounds++;
    }

    /// <summary>How many more of an item a plot's input slots take: what one sowing still lacks.</summary>
    private int SowRoom(Thing farm, string item) => Data.Farm.Sow.TryGetValue(item, out int sowing) ? Math.Max(0, sowing - farm.Inventory[item]) : 0;

    /// <summary>Whether a plot has room for a whole harvest.</summary>
    private bool YieldFits(Thing farm) {
        var yield = farm.Output ?? farm.Inventory;
        foreach (var pair in Data.Farm.Yield) if (yield.Room(pair.Key) < pair.Value) return false;
        return true;
    }

    /// <summary>A farmer harvests ripe wheat; each finished harvest is a round. A harvest the plot's output has no room for waits for a hauler.</summary>
    private bool Reap(Worker w, Thing farm, float dt) {
        if (farm.FarmStage != FarmStage.Ripe) { w.Status = "Waiting for the wheat to grow"; return false; }
        if (farm.Progress >= HarvestSeconds && !YieldFits(farm)) { w.Status = "Harvest waiting for room on the plot"; return false; }
        if (!HandsFree(w, dt)) return true;
        w.Status = Phrase("Harvesting wheat · ", Number(farm.Progress / HarvestSeconds * 100), "%");
        if (!Walk(w, farm.Pos, dt)) return true;
        w.WorkState = WorkerWorkState.Gathering;
        Harvest(farm, dt);
        return true;
    }
}
