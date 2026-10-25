using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Mines: marked free at an ore cliff, built in three stages, then worked underground for ore and rocks that never run out.</summary>
public sealed partial class World
{
    /// <summary>Values of <see cref="Thing.Stage"/>: the entrance is dug, the materials are in, and the frame and rails are fitted.</summary>
    public const int MineDug = 1, MineSupplied = 2, MineOpen = 3;
    /// <summary>How far from a cliff a mine plan still snaps to it.</summary>
    private const float MineReach = 10;
    // Mines already reported full with nowhere to take the output, so the message shows once per stoppage.
    private readonly HashSet<int> mineFullReported = new();

    public static Cliff CliffAt(Vector2 p) => Cliffs.MinBy(c => c.DistanceTo(p));
    /// <summary>The way out of a mine: along its cliff toward the sea, since the adit is cut into the cliff's seaward end, which faces the camera.</summary>
    public static Vector2 MineOut(Cliff c) => c.Along * Math.Sign(c.Along.Dot(Terrain.Seaward));
    /// <summary>A mine's spot: on open ground past the cliff's seaward end, where the cart stands at the end of the rails.</summary>
    public static Vector2 MineSpot(Cliff c) => c.Center + MineOut(c) * (c.HalfLength + 3);
    /// <summary>Where diggers and miners stand: at the entrance, against the rock.</summary>
    public static Vector2 MineDoor(Thing mine) => mine.Pos - MineOut(CliffAt(mine.Pos)) * 1.4f;
    /// <summary>Where a plan placed at <paramref name="p"/> goes: a mine near a cliff snaps to that cliff's spot.</summary>
    public static Vector2 Snap(string kind, Vector2 p) => kind == "mine" && CliffAt(p) is var c && c.DistanceTo(p) <= MineReach ? MineSpot(c) : p;
    public static string MineOre(Thing mine) => ResourceItem(new Thing { Kind = CliffAt(mine.Pos).Ore });
    public static string MineName(Thing mine) => $"{CliffAt(mine.Pos).Ore} mine";
    public float MineStageSeconds => Data.Mine.StageWorkdays * Data.WorkdaySeconds;
    /// <summary>Materials the site still needs before its frame can go in.</summary>
    public Dictionary<string, int> MineNeeds(Thing mine) => mine.Stage != MineDug ? new() :
        Data.Mine.Materials.Where(p => mine.Inventory[p.Key] < p.Value).ToDictionary(p => p.Key, p => p.Value - mine.Inventory[p.Key]);
    /// <summary>Mining stops as soon as either the ore or the rock slot holds a full stack.</summary>
    public bool MineFull(Thing mine) => mine.Output is Inventory output && (output[MineOre(mine)] >= output.StackSize || output["stone"] >= output.StackSize);
    /// <summary>The tool a stage needs each worker to have, or null for the stage where materials are hauled in.</summary>
    public static string? MineTool(int stage) => stage switch { 0 => "pickaxe", MineDug => null, MineSupplied => "hammer", _ => "pickaxe" };

    private string? MineBlock(Vector2 p)
    {
        var cliff = CliffAt(p);
        if (cliff.DistanceTo(p) > MineReach) return "Mark a mine at the foot of an ore cliff.";
        if (S.Things.Any(t => t.Kind == "mine" && t.Pos.DistanceTo(MineSpot(cliff)) < .1f)) return "This cliff already has a mine.";
        return Player.DistanceTo(MineSpot(cliff)) > 8 ? "Walk closer to the cliff." : null;
    }

    /// <summary>Hands the site whatever it still needs from the backpack.</summary>
    public void SupplyMine(Thing mine)
    {
        if (!Near(mine)) { Say("Walk closer to the mine."); return; }
        var needs = MineNeeds(mine);
        if (needs.Count == 0) return;
        if (needs.Sum(p => S.Backpack.TransferTo(mine.Inventory, p.Key, p.Value)) == 0) Say($"The mine still needs {Goods(needs)}.");
    }

    private void MineTick(Thing mine, float dt)
    {
        if (mine.Stage == MineDug && mine.Inventory.Spend(Data.Mine.Materials))
        {
            mine.Stage = MineSupplied; mine.Progress = 0; Say($"The {MineName(mine)} has its timber, iron and stone. A worker with a hammer can fit the frame and rails.");
        }
        if (mine.Stage != MineOpen || MineFull(mine)) return;
        int miners = S.Workers.Count(w => w.Assignment == mine.Id && w.WorkState == WorkerWorkState.Underground);
        mine.Progress += dt * miners;
        while (mine.Progress >= Data.Mine.Seconds && !MineFull(mine))
        {
            mine.Progress -= Data.Mine.Seconds; mine.Output!.Add(MineOre(mine), 1); mine.Output.Add("stone", 1);
        }
    }

    private void MineWork(Worker w, Thing mine, List<Worker> crew, float dt)
    {
        if (MineTool(mine.Stage) is string tool && w.Tool == "") { FetchTool(w, tool, mine, dt); return; }
        if (mine.Stage == MineDug)
        {
            foreach (var (item, need) in MineNeeds(mine))
            {
                var source = SourceFor(item, w.Pos);
                if (source == null) continue;
                w.Status = $"Fetching {Data.Name(item)} for the {MineName(mine)}";
                if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, item, need, true); w.Destination = mine.Id; }
                return;
            }
            w.Status = $"Needs {Goods(MineNeeds(mine))} in output-enabled storage"; return;
        }
        if (mine.Stage < MineOpen)
        {
            bool digging = mine.Stage == 0;
            w.Status = digging ? "Digging the mine entrance" : "Fitting the mine frame and rails";
            if (!Walk(w, MineDoor(mine), dt, .5f)) return;
            w.WorkState = digging ? WorkerWorkState.Digging : WorkerWorkState.Hammering;
            mine.Progress += dt;
            if (mine.Progress < MineStageSeconds) return;
            mine.Stage++; mine.Progress = 0;
            Say(digging ? $"The {MineName(mine)} entrance is dug. Deliver {Goods(Data.Mine.Materials)}." : $"The {MineName(mine)} is open. Up to {Data.Mine.Workers} workers with pickaxes can mine it.");
            return;
        }
        string ore = MineOre(mine);
        if (MineFull(mine))
        {
            // The first miner with free hands brings the full stack out to storage; the rest wait underground.
            string full = mine.Output![ore] >= mine.Output.StackSize ? ore : "stone";
            var dest = DepositFor(full, mine.Pos);
            if (dest == null)
            {
                w.Status = $"Mine full · no storage takes {Data.Name(full)}";
                if (mineFullReported.Add(mine.Id)) Say($"The {MineName(mine)} is full with no output available.");
            }
            else if (crew.First(c => c.Bag.Count == 0 && OnDuty(c)) == w)
            {
                w.Status = $"Bringing out {Data.Name(full)}";
                if (Walk(w, MineDoor(mine), dt, .5f)) { w.Bag.StackSize = mine.Output.StackSize; mine.Output.TransferTo(w.Bag, full, mine.Output.StackSize); w.Destination = dest.Id; }
                return;
            }
            else w.Status = "Waiting · the mine output is full";
        }
        else
        {
            mineFullReported.Remove(mine.Id);
            w.Status = $"Mining {Data.Name(ore)} underground";
        }
        if (Walk(w, MineDoor(mine), dt, .5f)) w.WorkState = WorkerWorkState.Underground;
    }
}
