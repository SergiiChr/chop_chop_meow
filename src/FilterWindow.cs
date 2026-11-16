using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Whether the filter window is open beside the panel, whether its item list is shown, and which row that list adds to.</summary>
public sealed class FilterState {
    public bool Open, Picking, Output;
}

/// <summary>
/// The one filter window for any storage that has filters: a row of what workers bring and one of what they take.
/// Drop an item on a row to add it, click one to take it off; an empty row allows anything.
/// A crate of a good the village buys can be named too; a trading post has only the row of crates workers bring, and an empty one takes no deliveries.
/// A plain pile has only the row of what workers bring, naming one resource, which also sets what they take.
/// Water at any fill is one entry, apart from empty bottles, and a pile can name crates of mixed goods.
/// </summary>
/// <param name="redraw">Draws the page again after a change to this window; the page does so itself when the filters change.</param>
/// <param name="scrolls">Takes the item list's scroll area and the list, so the panel fits it to the screen.</param>
public partial class FilterWindow(UiKit kit, HeldCursor cursor, Thing t, FilterState state, Action redraw, Action<ScrollContainer, Control> scrolls) : VBoxContainer {
    private readonly World world = kit.World;

    public override void _Ready() {
        AddThemeConstantOverride("separation", 12);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10); AddChild(top);
        top.AddChild(Style.Glyph("line_filter", 28, Style.Rim));
        var title = Style.Heading("Filters", 22); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(title);
        top.AddChild(Style.Icon(Icons.Of(t.Kind.Id()), 32));
        top.AddChild(kit.HeaderButton("close", "Close the filters", () => { state.Open = false; state.Picking = false; redraw(); }, Style.CloseRound, "filters_close"));
        bool post = t.Kind == ThingKind.Signpost, pile = World.IsPlainPile(t);
        foreach (bool output in post || pile ? new[] { false } : new[] { false, true }) {
            var filter = output ? t.Inventory.OutputFilter : t.Inventory.InputFilter;
            var row = new HFlowContainer { Name = output ? "filter_out" : "filter_in" }; row.AddThemeConstantOverride("h_separation", 10); row.AddThemeConstantOverride("v_separation", 10); AddChild(Style.InWell(row));
            var glyph = Style.Glyph(output ? "line_out" : "line_in", 30, Style.Ink); glyph.MouseFilter = Control.MouseFilterEnum.Pass;
            glyph.TooltipText = output ? "Workers take only these from here" : post ? "Workers bring these crates here to sell" : "Workers bring only these here";
            row.AddChild(glyph);
            var entries = pile ? (world.PileFilter(t) is string entry ? new[] { entry } : Array.Empty<string>()) : filter.OrderBy(world.FilterName).ToArray();
            foreach (string item in entries) {
                var cell = FilterCell($"filter_{(output ? "out" : "in")}_{item}", item); cell.TooltipText = $"{world.FilterName(item)}\nClick to take it off";
                string id = item;
                cell.Pressed += () => world.SetFilter(t, id, output, false);
                row.AddChild(cell);
            }
            if (filter.Count == 0) { var any = Style.Text(post ? "no deliveries" : "anything", 14, Style.Tan); any.CustomMinimumSize = new Vector2(0, 44); row.AddChild(any); }
            // A pile names one resource: the one it names comes off before another goes on.
            if (pile && filter.Count > 0) continue;
            var (add, _) = ItemSlot.Create($"filter_add_{(output ? "out" : "in")}", 44);
            add.Text = "+"; add.AddThemeFontSizeOverride("font_size", 22); add.AddThemeColorOverride("font_color", Style.Tan);
            add.TooltipText = "Add an item · or put a held one here";
            // A held stack goes back where it was: a filter only names it, or for a packed crate the kind of crate it is.
            add.Controls = new SlotInput(cursor) {
                Click = () => { state.Picking = !state.Picking; state.Output = output; redraw(); },
                // Only a pile names crates of mixed goods.
                Takes = held => held.Stack is HeldStack stack && (World.PackedKind(stack.Item) is var kind && !kind.IsEmpty && (pile || !kind.SequenceEqual(World.MixedCrate)) || !post && world.Data.Items.ContainsKey(stack.Item)),
                Put = (held, _) => {
                    var kind = World.PackedKind(held.Item!);
                    world.SetFilter(t, kind.IsEmpty ? held.Item! : new string(kind), output, true); held.Release();
                },
            };
            row.AddChild(add);
        }
        // A pile whose filter names a resource offers no other until it is taken off, even if the list was open when it was set.
        if (state.Picking && !(pile && t.Inventory.InputFilter.Count > 0)) {
            var picker = new GridContainer { Columns = 6 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
            // The list of every item outgrows short screens, so it scrolls.
            var list = new ScrollContainer { Name = "filter_list", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            list.AddChild(picker); AddChild(Style.InWell(list));
            scrolls(list, picker);
            var crates = world.Data.Market.Sells.Keys.Select(World.CrateOf).Where(id => post || world.Holds(t, id));
            if (pile) crates = crates.Append(World.MixedCrate);
            var items = world.Data.Items.Keys.Where(id => world.Holds(t, id));
            if (pile) items = items.Select(world.PileEntry).Distinct();
            foreach (var id in (post ? crates : items.Concat(crates)).OrderBy(world.FilterName)) {
                var cell = FilterCell($"pick_{id}", id); cell.TooltipText = world.FilterName(id);
                cell.Pressed += () => { world.SetFilter(t, id, state.Output, true); state.Picking = false; redraw(); };
                picker.AddChild(cell);
            }
        }
        var clear = kit.Button("Clear", () => world.ClearFilters(t)); clear.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; AddChild(clear);
    }

    /// <summary>A filter entry's cell: the item, or for a crate of a good, the good with a small crate in the corner.</summary>
    private ItemSlot FilterCell(string name, string id) {
        var (cell, _) = ItemSlot.Create(name, 44);
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
