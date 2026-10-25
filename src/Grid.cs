using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Icon cells shared by inventory, storage, station and build pages.</summary>
public partial class Game {
    private const int GridColumns = 6;

    private (ItemSlot Cell, Label Count) IconCell(string name) {
        var cell = new ItemSlot {
            Name = name,
            CustomMinimumSize = new Vector2(44, 44),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        // Tighter padding than regular buttons so the icon fills most of the cell.
        foreach (var (state, color) in new[] { ("normal", "34483c"), ("hover", "597252"), ("pressed", "7c8550"), ("disabled", "34483c") })
            cell.AddThemeStyleboxOverride(state, Box(new Color(color), 4, 3));
        var count = new Label {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetRight = -3,
            OffsetBottom = 1
        };
        count.AddThemeFontSizeOverride("font_size", 12);
        count.AddThemeColorOverride("font_outline_color", new Color("1c2b24"));
        count.AddThemeConstantOverride("outline_size", 4);
        cell.AddChild(count);
        // A packed crate shows what it holds in the corner.
        cell.AddChild(new TextureRect { Name = "badge", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(2, 22), Size = new Vector2(20, 20), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        return (cell, count);
    }

    private GridContainer AddGrid() {
        var grid = new GridContainer { Columns = GridColumns };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        content.AddChild(grid);
        return grid;
    }

    /// <summary>
    /// One cell per slot, refreshed live from the inventory's stacks.
    /// An inventory with a capacity shows only <see cref="Inventory.ShownSlots"/>; cells grow in and shrink away as that changes.
    /// Cells drag their stack out, or half of it on a right click, and accept stacks dragged in from another inventory, unless <paramref name="accepts"/> narrows that.
    /// The inventory is re-read on each refresh because death replaces the backpack object.
    /// </summary>
    private void AddInventoryGrid(Func<Inventory> inventory, Thing? container, Action<string> click, string hint, Func<ItemDrag, bool>? accepts = null) {
        var grid = AddGrid();
        var stacks = new (string Item, int Count)?[inventory().Slots];
        var cells = Enumerable.Range(0, stacks.Length).Select(i => IconCell($"slot_{i}")).ToArray();
        var shown = new bool[cells.Length];
        bool first = true;
        for (int i = 0; i < cells.Length; i++) {
            int slot = i;
            var cell = cells[i].Cell;
            cell.Pressed += () => { if (stacks[slot] is (string id, _)) { Act(() => click(id)); RefreshLive(); } };
            cell.Drag = () => stacks[slot] is (string id, int count) ? new ItemDrag(id, count, inventory(), container) : null;
            cell.Half = () => stacks[slot] is (string id, int count) ? new ItemDrag(id, (count + 1) / 2, inventory(), container) : null;
            cell.CanTake = accepts ?? (drag => drag.From != null && drag.From != inventory());
            cell.Take = drag => { int left = 0; Act(() => left = MoveDrag(drag, inventory(), container)); RefreshLive(); return left; };
            grid.AddChild(cell);
        }
        // What each cell last drew: its stack and the inventory's total of that item. A cell is redrawn only when they change.
        var current = new System.Collections.Generic.List<(string Item, int Count)>();
        var drawn = new (string? Item, int Count, int Total)[cells.Length];
        live.Add(() => {
            var held = inventory();
            held.FillStacks(current);
            int visible = held.ShownSlots, changed = 0;
            for (int i = 0; i < cells.Length; i++) {
                var (cell, count) = cells[i];
                if (!GodotObject.IsInstanceValid(cell)) return;
                stacks[i] = i < current.Count ? current[i] : null;
                var looks = stacks[i] is (string item, int n) ? (item, n, held[item]) : ((string?)null, 0, 0);
                if (first || looks != drawn[i]) {
                    drawn[i] = looks;
                    Redraw(cell, count, stacks[i], held);
                }
                if (shown[i] == i < visible && !first) continue;
                shown[i] = i < visible;
                if (first) cell.Visible = shown[i];
                else GrowCell(cell, shown[i], changed++ * .035f);
            }
            first = false;
        });
        void Redraw(ItemSlot cell, Label count, (string Item, int Count)? stack, Inventory held) {
            cell.Icon = stack is (string id, _) ? HotbarIcon(id) : null;
            var contents = stack is (string crate, _) ? world.Packed(crate) : null;
            var badge = cell.GetNode<TextureRect>("badge");
            badge.Visible = contents != null;
            if (contents != null) badge.Texture = HotbarIcon(contents.Items.OrderByDescending(p => p.Value).First().Key);
            count.Text = contents != null ? contents.Count.ToString() : stack is (_, int n) ? n.ToString() : "";
            cell.TooltipText = stack is (string named, _) ? $"{world.Describe(named)}{(contents != null ? "" : $" · {held[named]} total")}\n{hint}" : "";
        }
    }

    /// <summary>A slot that springs open with a slight overshoot, or shrinks and fades before it leaves the grid.</summary>
    private static void GrowCell(Control cell, bool open, float delay) {
        if (cell.HasMeta("tween") && cell.GetMeta("tween").As<Tween>() is Tween running && running.IsValid()) running.Kill();
        cell.PivotOffset = cell.CustomMinimumSize / 2;
        var tween = cell.CreateTween(); cell.SetMeta("tween", tween);
        tween.SetParallel();
        if (open) {
            if (!cell.Visible) { cell.Visible = true; cell.Scale = Vector2.One * .2f; cell.Modulate = new Color(1, 1, 1, 0); }
            tween.TweenProperty(cell, "scale", Vector2.One, .42f).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(cell, "modulate:a", 1f, .25f).SetDelay(delay);
        } else {
            tween.TweenProperty(cell, "scale", Vector2.One * .15f, .28f).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tween.TweenProperty(cell, "modulate:a", 0f, .28f).SetDelay(delay);
            tween.Chain().TweenCallback(Callable.From(() => cell.Visible = false));
        }
    }

    /// <summary>Moves one stack, or every unit of the item while Shift is held.</summary>
    private void MoveStack(Inventory from, Inventory to, string item, Thing near) {
        if (!world.Near(near)) { Notify("Walk closer first."); return; }
        if (world.BoxBlock(near, to, item) is string refused) { Notify(refused); return; }
        if (CrateIntoCrate(near, to, item)) return;
        world.Transfer(from, to, item, Input.IsKeyPressed(Key.Shift) ? from[item] : from.StackOf(item));
    }

    /// <summary>Drops a dragged stack into an inventory and returns what did not fit; the building on either end must be within reach.</summary>
    private int MoveDrag(ItemDrag drag, Inventory to, Thing? container) {
        if ((drag.Container ?? container) is Thing near && !world.Near(near)) { Notify("Walk closer first."); return 0; }
        if (container != null && world.BoxBlock(container, to, drag.Item) is string refused) { Notify(refused); return 0; }
        if (CrateIntoCrate(container, to, drag.Item) || drag.From == null) return 0;
        int left = Leftover(drag, to.Room(drag.Item));
        world.Transfer(drag.From, to, drag.Item, drag.Count);
        return left;
    }

    /// <summary>
    /// Units of a drag that will not fit into the given room.
    /// Worked out before the move because a guest's move happens later, on the host.
    /// </summary>
    private static int Leftover(ItemDrag drag, int room) => Math.Max(0, Math.Min(drag.Count, drag.From![drag.Item]) - room);

    /// <summary>Building plans and palette entries drag as hotbar references.</summary>
    private static void DragReference(ItemSlot cell, string id) => cell.Drag = () => new ItemDrag(id, 0, null, null);
}
