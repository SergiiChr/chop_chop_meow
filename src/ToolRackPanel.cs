using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A tool rack: a row per kind of tool workers use, with what hangs there, how many are out, and who waits for one.</summary>
public partial class ToolRackPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        HeaderAction("stash", "Hang spare tools · keeps your best of each", () => {
            if (!world.Near(thing)) { kit.Notify("Walk closer first."); return; }
            foreach (var id in world.Backpack.Items.Keys.Where(i => world.Holds(thing, i) && thing.Inventory.Accepts(i)).ToArray())
                world.Transfer(world.Backpack, thing.Inventory, id, world.BestTool(world.Data.Items[id].Tool) == id ? world.Backpack[id] - 1 : world.Backpack[id]);
        }, Style.ActionRound, "deposit");
        FilterButton();
        var rows = Well(new VBoxContainer()); rows.AddThemeConstantOverride("separation", 0);
        // The four kinds workers fetch always have a row; any other tool hung here, such as a chisel, gets one too.
        var kinds = new List<string> { "axe", "pickaxe", "rake", "hammer" };
        foreach (var id in thing.Inventory.Items.Keys) if (world.Data.Items.TryGetValue(id, out var def) && def.Tool != "" && !kinds.Contains(def.Tool)) kinds.Add(def.Tool);
        for (int k = 0; k < kinds.Count; k++) {
            string kind = kinds[k];
            if (k > 0) rows.AddChild(new HSeparator { CustomMinimumSize = new Vector2(0, 14) });
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 60) }; row.AddThemeConstantOverride("separation", 12); rows.AddChild(row);
            string plainest = world.Data.Items.Where(p => p.Value.Tool == kind).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
            var (cell, count) = ItemSlot.Cup($"rack_{kind}"); cell.Icon = Icons.Of(plainest); row.AddChild(cell);
            moves.TakeCell(cell, thing.Inventory, () => world.BestTool(kind, thing.Inventory), thing);
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var outPill = Style.Pill("", Style.Tag(Style.Enamel), Style.Rim, 12); outPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(outPill);
            var waitPill = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 12); waitPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(waitPill);
            live.Watch(() => {
                int held = 0, out_ = 0, waiting = 0;
                foreach (var pair in thing.Inventory.Items) if (world.Data.Items.TryGetValue(pair.Key, out var def) && def.Tool == kind) held += pair.Value;
                foreach (var w in world.Workers) { if (w.Tool != "" && world.Data.Items[w.Tool].Tool == kind) out_++; if (w.Missing == kind) waiting++; }
                return (world.BestTool(kind, thing.Inventory), held, out_, waiting);
            }, now => {
                var (best, held, out_, waiting) = now;
                cell.Icon = Icons.Of(best ?? plainest);
                cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
                count.Text = held > 1 ? held.ToString() : "";
                cell.TooltipText = held > 0 ? $"{world.Data.Name(best!)}\nClick to take" : $"No {kind} on the rack";
                outPill.Visible = out_ > 0; outPill.Text = $"{out_} out";
                outPill.TooltipText = string.Join("\n", world.Workers.Where(w => w.Tool != "" && world.Data.Items[w.Tool].Tool == kind).Select(w => w.Name));
                waitPill.Visible = waiting > 0; waitPill.Text = $"{waiting} waiting";
                waitPill.TooltipText = string.Join("\n", world.Workers.Where(w => w.Missing == kind).Select(w => $"{w.Name} needs {A(kind)}"));
            });
        }
        BagGrid(id => moves.Stack(world.S.Backpack, thing.Inventory, id, thing), "Click: hang a tool · Ctrl: all of them · Drag to move", wanted: id => world.Holds(thing, id) && thing.Inventory.Accepts(id));
        JobButtons();
    }
    private static string A(string word) => "aeiou".Contains(word[0]) ? $"an {word}" : $"a {word}";
}
