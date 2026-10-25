using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A mine's stages while it is built, then its two output slots once it is open.</summary>
public partial class Game
{
    private void MinePanel(Thing mine)
    {
        AddLabel(World.MineName(mine).ToUpperInvariant(), 16);
        var data = world.Data.Mine;
        string workday = data.StageWorkdays == 1 ? "1 working day" : $"{data.StageWorkdays:0.#} working days";
        (string Title, string Needs)[] stages =
        {
            ("Dig the entrance", $"A worker with a pickaxe for {workday}"),
            ("Shore it up", Cost(data.Materials)),
            ("Fit the frame and rails", $"A worker with a hammer for {workday}"),
        };
        for (int i = 0; i < stages.Length; i++)
        {
            int stage = i;
            Dynamic(() => $"{(mine.Stage > stage ? "✓" : mine.Stage == stage ? "▸" : "·")}  Stage {stage + 1} · {stages[stage].Title}", 15);
            Dynamic(() => mine.Stage != stage ? stages[stage].Needs : stage == World.MineDug ? Delivered(mine) : $"{stages[stage].Needs} · {mine.Progress / world.MineStageSeconds:P0} done", 14, Muted);
        }
        if (mine.Stage == World.MineDug) AddButton("Deliver from backpack", () => world.SupplyMine(mine));
        if (mine.Stage == World.MineOpen)
        {
            Dynamic(() => $"{world.S.Workers.Count(w => w.Assignment == mine.Id && w.WorkState == WorkerWorkState.Underground)} of {data.Workers} miners underground", 15);
            AddInventoryGrid(() => mine.Output!, mine, id => MoveStack(mine.Output!, world.S.Backpack, id, mine), "Ore and rocks · Click or drag: collect", _ => false);
            AddLabel("Mining stops when either slot is full. A miner then carries the full stack to the nearest storage that takes it.", 13, Muted);
        }
        Assignment(mine);
        AddLabel($"Up to {data.Workers} workers. Digging and mining need a pickaxe, fitting the frame a hammer.", 13, Muted);
    }

    private string Delivered(Thing mine) => string.Join("  ·  ", world.Data.Mine.Materials.Select(p => $"{world.Data.Name(p.Key)} {mine.Inventory[p.Key]}/{p.Value}"));
}
