using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The one filter window for any storage that has filters: a row of what workers bring and one of what they take.
/// Drop an item on a row to add it, click one to take it off; an empty row allows anything.
/// A crate of a good the village buys can be named too; a trading post has only the row of crates workers bring, and an empty one takes no deliveries.
/// </summary>
/// <param name="redraw">Draws the page again after a change to the filters.</param>
public partial class FilterWindow(UiKit kit, Thing t, PageMemory memory, Action redraw) : VBoxContainer {
    private readonly World world = kit.World;

    public override void _Ready() {
        AddThemeConstantOverride("separation", 12);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10); AddChild(top);
        top.AddChild(Style.Glyph("line_filter", 28, Style.Rim));
        var title = Style.Heading("Filters", 22); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(title);
        top.AddChild(Style.Icon(Icons.Of(t.Kind.Id()), 32));
        top.AddChild(kit.HeaderButton("close", "Close the filters", () => { memory.FilterOpen = false; memory.FilterPicking = false; redraw(); }, Style.CloseRound, "filters_close"));
        bool post = t.Kind == ThingKind.Signpost;
        foreach (bool output in post ? new[] { false } : new[] { false, true }) {
            var filter = output ? t.Inventory.OutputFilter : t.Inventory.InputFilter;
            var row = new HFlowContainer { Name = output ? "filter_out" : "filter_in" }; row.AddThemeConstantOverride("h_separation", 10); row.AddThemeConstantOverride("v_separation", 10); AddChild(Style.InWell(row));
            var glyph = Style.Glyph(output ? "line_out" : "line_in", 30, Style.Ink); glyph.MouseFilter = Control.MouseFilterEnum.Pass;
            glyph.TooltipText = output ? "Workers take only these from here" : post ? "Workers bring these crates here to sell" : "Workers bring only these here";
            row.AddChild(glyph);
            foreach (string item in filter.OrderBy(FilterName)) {
                var cell = FilterCell($"filter_{(output ? "out" : "in")}_{item}", item); cell.TooltipText = $"{FilterName(item)}\nClick to take it off";
                string id = item;
                cell.Pressed += () => { world.SetFilter(t, id, output, false); redraw(); };
                row.AddChild(cell);
            }
            if (filter.Count == 0) { var any = Style.Text(post ? "no deliveries" : "anything", 14, Style.Tan); any.CustomMinimumSize = new Vector2(0, 44); row.AddChild(any); }
            var (add, _) = ItemSlot.Cup($"filter_add_{(output ? "out" : "in")}", 44);
            add.Text = "+"; add.AddThemeFontSizeOverride("font_size", 22); add.AddThemeColorOverride("font_color", Style.Tan);
            add.TooltipText = "Add an item · or drop one from your backpack";
            add.Pressed += () => { memory.FilterPicking = !memory.FilterPicking; memory.PickingOutput = output; redraw(); };
            add.CanTake = drag => drag.From != null && (!World.PackedKind(drag.Item).IsEmpty || !post && world.Data.Items.ContainsKey(drag.Item));
            // The item stays where it was: a filter only names it, or for a packed crate the kind of crate it is.
            add.Take = drag => {
                var kind = World.PackedKind(drag.Item);
                world.SetFilter(t, kind.IsEmpty ? drag.Item : new string(kind), output, true); Callable.From(() => redraw()).CallDeferred(); return 0;
            };
            row.AddChild(add);
        }
        if (memory.FilterPicking) {
            var picker = new GridContainer { Columns = 6 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
            AddChild(Style.InWell(picker));
            var crates = world.Data.Market.Sells.Keys.Select(World.CrateOf).Where(id => post || world.Holds(t, id));
            foreach (var id in (post ? crates : world.Data.Items.Keys.Where(id => world.Holds(t, id)).Concat(crates)).OrderBy(FilterName)) {
                var cell = FilterCell($"pick_{id}", id); cell.TooltipText = FilterName(id);
                cell.Pressed += () => { world.SetFilter(t, id, memory.PickingOutput, true); memory.FilterPicking = false; redraw(); };
                picker.AddChild(cell);
            }
        }
        var clear = kit.Button(post ? "Clear" : "Clear both", () => { world.ClearFilters(t); redraw(); }); clear.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; AddChild(clear);
    }

    /// <summary>What a filter entry names: an item, or a crate of a good.</summary>
    private string FilterName(string id) => world.CrateGood(id) is string good ? $"Crate of {world.Data.Name(good).ToLowerInvariant()}" : world.Data.Name(id);

    /// <summary>A filter entry's cell: the item, or for a crate of a good, the good with a small crate in the corner.</summary>
    private ItemSlot FilterCell(string name, string id) {
        var (cell, _) = ItemSlot.Cup(name, 44);
        string? good = world.CrateGood(id);
        cell.Icon = Icons.Of(good ?? id);
        if (good != null) {
            var badge = Style.Icon(Icons.Of("crate"), 22); badge.MouseFilter = Control.MouseFilterEnum.Ignore;
            badge.AnchorLeft = badge.AnchorTop = badge.AnchorRight = badge.AnchorBottom = 1;
            badge.OffsetLeft = badge.OffsetTop = -20; badge.OffsetRight = badge.OffsetBottom = 2;
            cell.AddChild(badge);
        }
        return cell;
    }
}
