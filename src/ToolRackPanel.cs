using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A tool rack: a row per kind of tool workers use, with what hangs there, how many are out, and who waits for one.</summary>
public partial class ToolRackPanel(SidePanel side, Thing t) : PagePanel(side) {
    public override void _Ready() {
        HeaderAction("stash", "Hang spare tools · keeps your best of each", () => {
            if (!world.Near(t)) { kit.Notify("Walk closer first."); return; }
            foreach (var id in world.Backpack.Items.Keys.Where(i => world.Holds(t, i) && t.Inventory.Accepts(i)).ToArray())
                world.Transfer(world.Backpack, t.Inventory, id, world.BestTool(world.Data.Items[id].Tool) == id ? world.Backpack[id] - 1 : world.Backpack[id]);
        }, Style.ActionRound, "deposit");
        FilterButton(t);
        var rows = Well(new VBoxContainer()); rows.AddThemeConstantOverride("separation", 0);
        // The four kinds workers fetch always have a row; any other tool hung here, such as a chisel, gets one too.
        var kinds = new List<string> { "axe", "pickaxe", "rake", "hammer" };
        foreach (var id in t.Inventory.Items.Keys) if (world.Data.Items.TryGetValue(id, out var def) && def.Tool != "" && !kinds.Contains(def.Tool)) kinds.Add(def.Tool);
        for (int k = 0; k < kinds.Count; k++) {
            string kind = kinds[k];
            if (k > 0) rows.AddChild(new HSeparator { CustomMinimumSize = new Vector2(0, 14) });
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 60) }; row.AddThemeConstantOverride("separation", 12); rows.AddChild(row);
            string plainest = world.Data.Items.Where(p => p.Value.Tool == kind).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
            var (cell, count) = ItemSlot.Cup($"rack_{kind}"); cell.Icon = Icons.Of(plainest); row.AddChild(cell);
            cell.Pressed += () => { if (world.BestTool(kind, t.Inventory) is string best) { kit.Act(() => moves.Stack(t.Inventory, world.S.Backpack, best, t)); kit.Refresh(); } };
            cell.Drag = () => world.BestTool(kind, t.Inventory) is string best ? new ItemDrag(best, t.Inventory[best], t.Inventory, t) : null;
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var outPill = Style.Pill("", Style.Tag(Style.Enamel), Style.Rim, 12); outPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(outPill);
            var waitPill = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 12); waitPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(waitPill);
            (string? Best, int Held, int Out, int Waiting) shown = (null, -1, -1, -1);
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(cell)) return;
                int held = 0, out_ = 0, waiting = 0;
                foreach (var pair in t.Inventory.Items) if (world.Data.Items.TryGetValue(pair.Key, out var def) && def.Tool == kind) held += pair.Value;
                foreach (var w in world.Workers) { if (w.Tool != "" && world.Data.Items[w.Tool].Tool == kind) out_++; if (w.Missing == kind) waiting++; }
                var now = (world.BestTool(kind, t.Inventory), held, out_, waiting);
                if (now == shown) return;
                shown = now;
                cell.Icon = Icons.Of(now.Item1 ?? plainest);
                cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
                count.Text = held > 1 ? held.ToString() : "";
                cell.TooltipText = held > 0 ? $"{world.Data.Name(now.Item1!)}\nClick to take" : $"No {kind} on the rack";
                outPill.Visible = out_ > 0; outPill.Text = $"{out_} out";
                outPill.TooltipText = string.Join("\n", world.Workers.Where(w => w.Tool != "" && world.Data.Items[w.Tool].Tool == kind).Select(w => w.Name));
                waitPill.Visible = waiting > 0; waitPill.Text = $"{waiting} waiting";
                waitPill.TooltipText = string.Join("\n", world.Workers.Where(w => w.Missing == kind).Select(w => $"{w.Name} needs {A(kind)}"));
            });
        }
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: hang a tool · Ctrl: all of them · Drag to move", wanted: id => world.Holds(t, id) && t.Inventory.Accepts(id));
        JobButtons(t);
    }
    private static string A(string word) => "aeiou".Contains(word[0]) ? $"an {word}" : $"a {word}";
}
