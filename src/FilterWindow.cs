using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Whether the filter window is open beside the panel, whether its item list is shown, and which row that list adds to.</summary>
public sealed class FilterState {
    public bool Open, Picking, Output;
}

/// <summary>
/// The one filter window for any storage that has filters: a row of what workers bring and one of what they take.
/// Drop an item on a row to add it, click one to take it off; an empty row allows anything, and a row shows its items in the order they were added.
/// The item list runs in <see cref="GameData.ListOrder"/>, with crates of goods after the items in their goods' order, and crates of mixed goods last.
/// A crate of a good the village buys can be named too; a trading post has only the row of crates workers bring, and an empty one takes no deliveries.
/// A plain pile has only the row of what workers bring, naming one resource, which also sets what they take.
/// A barrel has the same single row and names water.
/// A placed crate's filters name no crate.
/// Water in a full or partly filled vessel is one entry.
/// </summary>
/// <param name="redraw">Draws the page again after a change to this window; the page does so itself when the filters change.</param>
/// <param name="scrolls">Takes the item list's scroll area and the list, so the panel fits it to the screen.</param>
public partial class FilterWindow(UiKit kit, HeldCursor cursor, Thing t, FilterState state, Action redraw, Action<ScrollContainer, Control> scrolls) : VBoxContainer {
    private readonly World world = kit.World;

    public override void _Ready() {
        AddThemeConstantOverride("separation", 12);
        bool post = t.Kind == ThingKind.Signpost, pile = World.IsPlainPile(t), barrel = t.Kind == ThingKind.Barrel;
        bool single = World.IsSingleResourceStore(t);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10); AddChild(top);
        top.AddChild(Style.Glyph("line_filter", 28, Style.Rim));
        var title = Style.Heading("Filters", 22); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(title);
        top.AddChild(Style.Icon(Icons.Of(t.Kind.Id()), 32));
        top.AddChild(kit.HeaderButton("close", "Close the filters", () => { state.Open = false; state.Picking = false; redraw(); }, Style.CloseRound, "filters_close"));
        foreach (bool output in post || single ? new[] { false } : new[] { false, true }) {
            var filter = output ? t.Inventory.OutputFilter : t.Inventory.InputFilter;
            var row = new HFlowContainer { Name = output ? "filter_out" : "filter_in" }; row.AddThemeConstantOverride("h_separation", 10); row.AddThemeConstantOverride("v_separation", 10); AddChild(Style.InWell(row));
            var glyph = Style.Glyph(output ? "line_out" : "line_in", 30, Style.Ink); glyph.MouseFilter = Control.MouseFilterEnum.Pass;
            glyph.TooltipText = output ? "Workers take only these from here" : post ? "Workers bring these crates here to sell" : "Workers bring only these here";
            row.AddChild(glyph);
            IReadOnlyList<string> entries = pile ? (world.PileFilter(t) is string entry ? [entry] : []) : filter;
            foreach (string item in entries) {
                var cell = FilterCell($"filter_{(output ? "out" : "in")}_{item}", item); cell.TooltipText = $"{world.FilterName(item)}\nClick to take it off";
                string id = item;
                cell.Pressed += () => world.SetFilter(t, id, output, false);
                row.AddChild(cell);
            }
            if (filter.Count == 0) { var any = Style.Text(post ? "no deliveries" : barrel ? "water" : "anything", 14, Style.Tan); any.CustomMinimumSize = new Vector2(0, 44); row.AddChild(any); }
            // Take the named resource off before adding another.
            if (single && filter.Count > 0) continue;
            var (add, _) = ItemSlot.Create($"filter_add_{(output ? "out" : "in")}", 44);
            add.Text = "+"; add.AddThemeFontSizeOverride("font_size", 22); add.AddThemeColorOverride("font_color", Style.Tan);
            add.TooltipText = "Add an item · or put a held one here";
            // A held stack goes back where it was: a filter only names it, or for a packed crate the kind of crate it is.
            add.Controls = new SlotInput(cursor) {
                Click = () => { state.Picking = !state.Picking; state.Output = output; redraw(); },
                Takes = held => held.Stack is HeldStack stack && CanName(stack.Item),
                Put = (held, _) => {
                    var kind = World.PackedKind(held.Item!);
                    string item = barrel ? Water.Liquid : kind.IsEmpty ? held.Item! : new string(kind);
                    world.SetFilter(t, item, output, true); held.Release();
                },
            };
            row.AddChild(add);
        }
        if (state.Picking && !(single && t.Inventory.InputFilter.Count > 0)) {
            var picker = new GridContainer { Columns = 6 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
            // The list of every item outgrows short screens, so it scrolls.
            var list = new ScrollContainer { Name = "item_list", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            list.AddChild(picker); AddChild(Style.InWell(list));
            scrolls(list, picker);
            var crates = world.Data.ListOrder.Where(world.Data.Market.Sells.ContainsKey).Select(World.CrateOf).Where(id => post || Holds(id));
            if (pile) crates = crates.Append(World.MixedCrate);
            var items = world.Data.ListOrder.Where(Holds);
            if (pile) items = items.Select(world.PileEntry).Distinct();
            var choices = barrel ? new[] { Water.Liquid } : post ? crates : items.Concat(crates);
            foreach (var id in choices) {
                var cell = FilterCell($"pick_{id}", id); cell.TooltipText = world.FilterName(id);
                cell.Pressed += () => { world.SetFilter(t, id, state.Output, true); state.Picking = false; redraw(); };
                picker.AddChild(cell);
            }
        }
        var clear = kit.Button("Clear", () => world.ClearFilters(t)); clear.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; AddChild(clear);

        // A placed crate's filters name no crate, since a crate never goes into one.
        bool Holds(string item) => world.Holds(t, item) && !World.Nests(t, item);

        bool CanName(string item) {
            if (barrel) return Barrels.CanPour(world.Data, item);
            if (World.Nests(t, item)) return false;
            var kind = World.PackedKind(item);
            return !kind.IsEmpty && (pile || !kind.SequenceEqual(World.MixedCrate)) || !post && world.Data.Items.ContainsKey(item);
        }
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
