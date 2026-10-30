using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Icon cells shared by inventory, storage, station and build pages.</summary>
public partial class Game {
    private const int GridColumns = 6;
    /// <summary>The backpack drawer is two rows of twelve, or three of eight on a narrow screen.</summary>
    private int BagColumns => ui.Size.X >= 1400 ? 12 : 8;
    /// <summary>Room between cups for the count pills that sit on their corners.</summary>
    private const int CupGap = 10;

    private GridContainer AddGrid(int columns = GridColumns) {
        var grid = new GridContainer { Columns = columns };
        grid.AddThemeConstantOverride("h_separation", CupGap);
        grid.AddThemeConstantOverride("v_separation", CupGap);
        content.AddChild(grid);
        return grid;
    }

    /// <summary>
    /// One cell per slot, refreshed live from the inventory's stacks.
    /// An inventory with a capacity shows only <see cref="Inventory.ShownSlots"/>; cells grow in and shrink away as that changes.
    /// Cells drag their stack out, or half of it on a right click, and accept stacks dragged in from another inventory, unless <paramref name="accepts"/> narrows that.
    /// The inventory is re-read on each refresh because death replaces the backpack object.
    /// With <paramref name="wanted"/>, stacks it names are rimmed in orange and the rest fade, so what the open page takes stands out.
    /// </summary>
    private GridContainer AddInventoryGrid(Func<Inventory> inventory, Thing? container, Action<string> click, string hint, Func<ItemDrag, bool>? accepts = null, Func<string, bool>? wanted = null, int columns = GridColumns, float size = 52) {
        var grid = AddGrid(columns);
        if (size < 52) { grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); }
        var stacks = new (string Item, int Count)?[inventory().Slots];
        var cells = Enumerable.Range(0, stacks.Length).Select(i => ItemSlot.Cup($"slot_{i}", size)).ToArray();
        var shown = new bool[cells.Length];
        bool first = true;
        for (int i = 0; i < cells.Length; i++) {
            int slot = i;
            var cell = cells[i].Cell;
            cell.Pressed += () => {
                if (stacks[slot] is not (string id, _)) return;
                var held = inventory(); held.PreferredSlot = slot;
                try { kit.Act(() => click(id)); } finally { held.PreferredSlot = -1; }
                RefreshLive();
            };
            cell.Drag = () => stacks[slot] is (string id, int count) ? new ItemDrag(id, count, inventory(), container, slot) : null;
            cell.CanTake = accepts ?? (drag => drag.From != null);
            cell.Take = drag => { int left = 0; kit.Act(() => left = MoveDrag(drag, inventory(), container, slot)); RefreshLive(); return left; };
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
                stacks[i] = i < current.Count && current[i].Item != "" ? current[i] : null;
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
        return grid;
        void Redraw(ItemSlot cell, Badge count, (string Item, int Count)? stack, Inventory held) {
            cell.Icon = stack is (string id, _) ? Icons.Of(id) : null;
            var contents = stack is (string crate, _) ? world.Packed(crate) : null;
            var badge = cell.GetNode<TextureRect>("badge");
            badge.Visible = contents != null;
            if (contents != null) badge.Texture = Icons.Of(contents.Items.OrderByDescending(p => p.Value).First().Key);
            count.Text = stack is (string bottle, _) && Water.IsBottle(bottle) ? $"{Water.BottleSips(bottle)}/3" : contents != null ? contents.Count.ToString() : stack is (_, int n) ? n.ToString() : "";
            if (wanted != null) {
                bool takes = stack is (string item, _) && wanted(item);
                cell.AddThemeStyleboxOverride("normal", takes ? Style.CupWanted : Style.CupNormal);
                cell.AddThemeColorOverride(ItemSlot.IconNormal, stack == null || takes ? Colors.White : Style.Faded);
            }
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

    /// <summary>Whether a click moves every unit of an item rather than one item: Ctrl, or Shift as before.</summary>
    private static bool MoveAll => Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Shift);
    /// <summary>Moves one item, or every unit of the item while Ctrl or Shift is held.</summary>
    private void MoveStack(Inventory from, Inventory to, string item, Thing near) {
        if (!world.Near(near)) { Notify("Walk closer first."); return; }
        if (world.BoxBlock(near, to, item) is string refused) { Notify(refused); return; }
        if (CrateIntoCrate(near, to, item)) return;
        world.Transfer(from, to, item, MoveAll ? from[item] : 1);
    }

    /// <summary>Drops a dragged stack into an inventory and returns what did not fit; the building on either end must be within reach.</summary>
    private int MoveDrag(ItemDrag drag, Inventory to, Thing? container, int target = -1) {
        if ((drag.Container ?? container) is Thing near && !world.Near(near)) { Notify("Walk closer first."); return 0; }
        if (drag.From != to && container != null && world.BoxBlock(container, to, drag.Item) is string refused) { Notify(refused); return 0; }
        if (CrateIntoCrate(container, to, drag.Item) || drag.From == null) return 0;
        if (target >= 0 && drag.SourceSlot >= 0) {
            if (drag.From == to && drag.SourceSlot == target) return 0;
            int moved = world.TransferSlot(drag.From, to, drag.SourceSlot, target, drag.Item, drag.Count);
            return world.Remote ? 0 : Math.Max(0, drag.Count - moved);
        }
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
