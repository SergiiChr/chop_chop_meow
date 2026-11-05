using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The personal crafting menu: the recipe board, then what is being made by hand and what waits after it.</summary>
public partial class CraftPanel(PageTools tools) : PagePanel(tools) {
    protected override void Build() {
        headingIcon.Texture = GD.Load<Texture2D>("res://assets/ui/line_craft.svg"); headingIcon.SelfModulate = Style.Rim; headingIcon.Visible = true;
        headingIcon.TooltipText = "Crafting by hand. A crafting bench works twice as fast.";
        AddChild(new RecipeBoard(kit, live, memory, Redraw, world.HandRecipes, group => group.Tool == "" ? null : world.BestTool(group.Tool, null, group.Tier), null, null));
        CraftQueue();
        BagGrid(UseFromBag, UseHint, wanted: id => world.HandRecipes.Any(r => world.Data.Recipes[r].Inputs.ContainsKey(id)));
    }

    /// <summary>
    /// What is being made by hand, in the stations' process row: its materials on the left, the time over the bar and the product on the right.
    /// The rest of the queue waits in a row under it, one cup per recipe.
    /// </summary>
    private void CraftQueue() {
        var queue = world.Crafting;
        // The row shows one recipe and the strip the queue's recipes, so the page is drawn again when either changes.
        RedrawOnChange(() => (world.Making, QueueCode(world.Crafting)));
        string? shown = world.Making != "" ? world.Making : queue.Count > 0 ? queue[0].Recipe : null;
        if (shown == null) { Well(new VBoxContainer()).AddChild(Style.Text("Nothing in the works. Click a recipe to make it.", 14, Style.Ink)); return; }
        var making = world.Data.Recipes[shown];
        var inputs = making.Inputs.Select(p => (Control)RecipeCup($"queue_input_{p.Key}", p.Key, $"×{p.Value}", 52));
        var product = RecipeCup("making", making.Output, $"×{making.Amount}", 64);
        AddChild(new ProcessRow(live, inputs, LiveText.Of(text => PageText.AppendDuration(text, making.Seconds / Math.Max(.01f, world.CraftRate))),
            () => world.Making == shown && making.Seconds > 0 ? world.CraftTime / making.Seconds : 0, null, product, Array.Empty<Control>()));
        AddChild(new QueueStrip(kit, live, queue, id => World.Waiting(world.Crafting, id), (id, count) => world.CancelCraft(id, count)));
        var status = Style.Text("", 14, Style.Warn); AddChild(status);
        live.Watch(() => world.CraftRate <= 0, stalled => {
            bool noTool = stalled && making.Tool != "";
            status.Text = noTool ? $"Waiting for a {making.Tool} in your backpack" : "Crafting goes on while you stand still.";
            status.AddThemeColorOverride("font_color", noTool ? Style.Warn : Style.Beige);
        });
    }

    /// <summary>A number that changes with the recipes in a queue and their order, built without allocating.</summary>
    public static int QueueCode(ListView<Queued> queue) {
        int code = queue.Count;
        for (int i = 0; i < queue.Count; i++) code = code * 31 + queue[i].Recipe.GetHashCode();
        return code;
    }

    /// <summary>A cup in the craft row with what one batch takes or makes under it.</summary>
    private VBoxContainer RecipeCup(string name, string item, string perBatch, float size) {
        var (cell, _) = ItemSlot.Cup(name, size); cell.Icon = Icons.Of(item);
        cell.TooltipText = world.Data.Name(item);
        return Cups.Labelled(cell, perBatch);
    }
}
