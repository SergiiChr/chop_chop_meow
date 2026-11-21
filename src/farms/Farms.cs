using System;
using System.Linq;

namespace ChopChopMeow;

/// <summary>The sowing, watering and harvesting of wheat on a farm plot.</summary>
public sealed class Farms(World world) {
    public const float HalfSize = 3;
    public const int LitersPerDay = 10;
    private GameData Data => world.Data;
    private float SecondsPerLiter => Data.DaySeconds / LitersPerDay;

    public string Crop => crop ??= Data.Farm.Yield.Keys.First();
    private string? crop;
    public float GrowSeconds => Data.Farm.GrowDays * Data.DaySeconds;
    public float HarvestSeconds => Data.Farm.HarvestWorkdays * Data.WorkdaySeconds;

    public int WaterRoom(Thing farm) =>
        Math.Max(0, LitersPerDay - farm.Inventory[Water.Liquid] - (farm.FarmWaterLeft > 0 ? 1 : 0));

    public float WaterLevel(Thing farm) => farm.Inventory[Water.Liquid] + farm.FarmWaterLeft / SecondsPerLiter;
    public float Growth(Thing farm) => farm.Progress + (farm.FarmWaterLeft > 0 ? SecondsPerLiter - farm.FarmWaterLeft : 0);
    public bool Dry(Thing farm) => farm.FarmWaterLeft == 0 && farm.Inventory[Water.Liquid] == 0;

    /// <summary>What the plot shows: 0 bare, 1 sown soil, 2 small green straws, 3 tall green plants, 4 yellow wheat.</summary>
    public int CropLook(Thing farm) => farm.FarmStage switch {
        FarmStage.Ripe => 4,
        FarmStage.Growing => 1 + Math.Min(2, (int)(Growth(farm) / GrowSeconds * 3)),
        _ => 0
    };

    internal void Tick(Thing farm, float dt) {
        if (farm.FarmStage == FarmStage.Fallow && farm.Inventory.Has(Data.Farm.Sow)) {
            farm.Inventory.Spend(Data.Farm.Sow);
            farm.FarmStage = FarmStage.Growing;
            farm.Progress = farm.FarmWaterLeft > 0 ? farm.FarmWaterLeft - SecondsPerLiter : 0;
        }
        if (farm.FarmStage != FarmStage.Growing) return;
        while (dt > 0) {
            float untilRipe = GrowSeconds - farm.Progress;
            // Subtract the whole liter first so a nearly ripe crop never rounds up to needing one more.
            if (farm.FarmWaterLeft > 0) untilRipe = untilRipe - SecondsPerLiter + farm.FarmWaterLeft;
            else if (untilRipe > 0) {
                if (farm.Inventory.Remove(Water.Liquid, 1) == 0) return;
                farm.FarmWaterLeft = SecondsPerLiter;
            }
            float grown = Math.Clamp(untilRipe, 0, Math.Min(dt, farm.FarmWaterLeft));
            farm.FarmWaterLeft -= grown;
            if (grown >= untilRipe) { farm.FarmStage = FarmStage.Ripe; farm.Progress = 0; return; }
            // Add one complete liter at a time so rounding each tick cannot shorten a day's growth.
            if (farm.FarmWaterLeft == 0) farm.Progress += SecondsPerLiter;
            dt -= grown;
        }
    }

    internal bool ValidWater(Thing farm) => farm.Inventory.Slots == Data.Farm.Sow.Count + 1 &&
        farm.FarmWaterLeft >= 0 && farm.FarmWaterLeft <= SecondsPerLiter &&
        farm.Inventory[Water.Liquid] + (farm.FarmWaterLeft > 0 ? 1 : 0) <= LitersPerDay;

    /// <summary>
    /// Adds harvest work, from the player or any number of workers, to the plot's one shared progress.
    /// A finished harvest waits at full progress until the plot has room for the yield, so nothing is lost.
    /// </summary>
    internal void Harvest(Thing farm, float dt) {
        if (farm.FarmStage != FarmStage.Ripe) return;
        farm.Progress = Math.Min(HarvestSeconds, farm.Progress + dt);
        if (farm.Progress < HarvestSeconds) return;
        if (!YieldFits(farm)) return;
        var yield = farm.Output ?? farm.Inventory;
        foreach (var (item, amount) in Data.Farm.Yield) { yield.Add(item, amount); world.Got(item); }
        farm.FarmStage = FarmStage.Fallow; farm.Progress = 0; farm.Rounds++;
    }

    /// <summary>How many more of an item a plot's input slots take: what one sowing still lacks.</summary>
    internal int SowRoom(Thing farm, string item) => Data.Farm.Sow.TryGetValue(item, out int sowing) ? Math.Max(0, sowing - farm.Inventory[item]) : 0;

    /// <summary>Whether a plot has room for a whole harvest.</summary>
    internal bool YieldFits(Thing farm) {
        var yield = farm.Output ?? farm.Inventory;
        foreach (var pair in Data.Farm.Yield) if (yield.Room(pair.Key) < pair.Value) return false;
        return true;
    }
}
