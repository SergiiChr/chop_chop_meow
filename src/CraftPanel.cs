using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>The personal crafting menu: the recipe board, then what is being made by hand.</summary>
public partial class CraftPanel(PageTools tools) : PagePanel(tools) {
    protected override void Build() {
        headingIcon.Texture = GD.Load<Texture2D>("res://assets/ui/line_craft.svg"); headingIcon.SelfModulate = Style.Rim; headingIcon.Visible = true;
        headingIcon.TooltipText = "Crafting by hand. A crafting bench works twice as fast.";
        var groups = world.HandRecipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();
        AddChild(new RecipeBoard(kit, live, memory, Redraw, groups, group => group.Tool == "" ? null : world.BestTool(group.Tool, null, group.Tier), null, id => kit.Act(() => world.Craft(id)), _ => false, world.CanCraft));
        CraftQueue();
        BagGrid(UseFromBag, UseHint, wanted: id => world.HandRecipes.Any(r => world.Data.Recipes[r].Inputs.ContainsKey(id)));
    }

    /// <summary>
    /// What is being made by hand, in the stations' process row: its materials on the left, the time over the bar and the product on the right.
    /// The rest of the queue waits in a row under it; clicking the product or a waiting recipe cancels it and returns its materials.
    /// </summary>
    private void CraftQueue() {
        var queue = world.Crafting;
        string? head = queue.Count > 0 ? queue[0] : null;
        int queued = queue.Count;
        // The row shows one recipe, so the page is rebuilt once when the queue moves on or grows.
        bool rebuilding = false;
        live.Add(() => {
            if (rebuilding || (queue.Count > 0 ? queue[0] : null) == head && queue.Count == queued) return;
            rebuilding = true; RedrawLater();
        });
        if (head == null) { Well(new VBoxContainer()).AddChild(Style.Text("Nothing in the works. Click a recipe to make it.", 14, Style.Ink)); return; }
        var making = world.Data.Recipes[head];
        var inputs = making.Inputs.Select(p => (Control)RecipeCup($"queue_input_{p.Key}", p.Key, $"×{p.Value}", 52, null));
        var product = RecipeCup("queue_0", making.Output, $"×{making.Amount}", 64, () => world.CancelCraft(0));
        var waiting = new List<Control>();
        for (int i = 1; i < queue.Count; i++) {
            int index = i;
            var r = world.Data.Recipes[queue[i]];
            waiting.Add(RecipeCup($"queue_{i}", r.Output, "", 44, () => world.CancelCraft(index)));
        }
        AddChild(new ProcessRow(live, inputs, LiveText.Of(text => PageText.AppendDuration(text, making.Seconds / Math.Max(.01f, world.CraftRate))), () => making.Seconds > 0 ? world.CraftTime / making.Seconds : 0, null, product, waiting));
        if (making.Tool != "") {
            var status = Style.Text("", 14, Style.Warn); AddChild(status);
            live.Watch(() => world.CraftRate <= 0, stalled => status.Text = stalled ? $"Waiting for a {making.Tool} in your backpack" : "");
        }
    }

    /// <summary>A cup in the craft queue's row with what one batch takes or makes under it; with a cancel action, clicking it cancels that entry.</summary>
    private VBoxContainer RecipeCup(string name, string item, string perBatch, float size, Action? cancel) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, _) = ItemSlot.Cup(name, size); cell.Icon = Icons.Of(item);
        cell.TooltipText = cancel == null ? world.Data.Name(item) : $"{world.Data.Name(item)}\nClick to cancel and get the materials back.";
        if (cancel != null) {
            cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            cell.Pressed += () => { kit.Act(cancel); kit.Refresh(); };
        }
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(Style.PerBatch(perBatch));
        return column;
    }
}

/// <summary>A crafting bench or cooking table: its tool cups in the header, the recipe board, and the work of the chosen recipe.</summary>
public partial class BenchPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        // The station's own tool cups sit in the header: what is slotted sets which recipes can run and how fast.
        foreach (string tool in World.SlotKinds(thing.Kind)) { var slot = ToolSlot(tool); heading.AddChild(slot); heading.MoveChild(slot, heading.GetChildCount() - 2); }
        if (thing.Kind == ThingKind.Bench) headingIcon.TooltipText = $"Works {world.Data.BenchSpeed:0.#}× as fast as by hand, at the speed of the slotted tool. Someone has to work it.";
        var groups = world.Data.Recipes.Where(p => p.Value.Station == thing.Kind).Select(p => p.Key).GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();
        AddChild(new RecipeBoard(kit, live, memory, Redraw, groups, group => group.Tool == "" ? null : world.BenchTool(thing, group.Tool, group.Tier), thing,
            id => { world.SetRecipe(thing, id); Redraw(); }, id => thing.Recipe == id, id => world.Backpack.Has(world.Data.Recipes[id].Inputs),
            // A double click also loads one batch of inputs from the backpack.
            id => kit.Act(() => { foreach (var (item, amount) in world.Data.Recipes[id].Inputs) world.LoadInput(thing, item, amount); })));
        if (thing.Recipe != "") AddChild(cups.Machine());
        else AddLabel("Choose what to make.", 14, Style.Beige);
        var slotted = World.SlotKinds(thing.Kind);
        BagGrid(id => {
            if (slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(thing, world.Data.Items[id].Tool) == null) world.SlotTool(thing, id);
            else world.LoadInput(thing, id, ItemMoves.All ? world.Backpack[id] : 1);
        }, "Click: slot a tool, or add one · Ctrl: all of it · Drag onto a slot",
            wanted: id => thing.Recipe != "" && world.BatchCost(thing).ContainsKey(id) || slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(thing, world.Data.Items[id].Tool) == null);
        JobButtons();
    }

    /// <summary>A station's cup for one tool kind; while empty it shows a faint picture of the tool it wants.</summary>
    private ItemSlot ToolSlot(string tool) {
        var (cell, _) = ItemSlot.Cup($"tool_{tool}");
        cell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        string plainest = world.Data.Items.Where(p => p.Value.Tool == tool).OrderBy(p => p.Value.Speed).First().Key;
        cell.Pressed += () => { if (world.BenchTool(thing, tool) is string held) { kit.Act(() => world.UnslotTool(thing, held)); kit.Refresh(); } };
        cell.Drag = () => world.BenchTool(thing, tool) is string held ? new ItemDrag(held, 1, thing.Tools, thing) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && world.Data.Items.GetValueOrDefault(drag.Item)?.Tool == tool;
        cell.Take = drag => { kit.Act(() => world.SlotTool(thing, drag.Item)); kit.Refresh(); return 0; };
        live.Watch(() => world.BenchTool(thing, tool), held => {
            cell.Icon = Icons.Of(held ?? plainest);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held != null ? Colors.White : Style.Ghost);
            cell.TooltipText = held != null ? $"{world.Data.Name(held)} · {world.Data.Items[held].Speed:0.#}× speed\nClick: take it back" : $"Empty {tool} slot\nDrag a {tool} here from your backpack";
        });
        return cell;
    }
}
