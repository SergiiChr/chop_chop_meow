using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Icon slots shared by inventory, storage, station and build pages.</summary>
public partial class PagePanel {
    protected const int GridColumns = 6;
    /// <summary>The backpack drawer is two rows of twelve, or three of eight on a narrow screen.</summary>
    protected int BagColumns => panel.ScreenWidth >= 1400 ? 12 : 8;
    /// <summary>Room between slots for the count pills that sit on their corners.</summary>
    protected const int SlotGap = 10;

    /// <summary>An empty grid with room between its slots, for the caller to place.</summary>
    protected static GridContainer NewGrid(int columns = GridColumns) {
        var grid = new GridContainer { Columns = columns };
        grid.AddThemeConstantOverride("h_separation", SlotGap);
        grid.AddThemeConstantOverride("v_separation", SlotGap);
        return grid;
    }

    /// <summary>
    /// A grid with one slot per slot, refreshed live from the inventory's stacks and not yet placed anywhere.
    /// An inventory with a capacity shows only <see cref="Inventory.ShownSlots"/>; slots grow in and shrink away as that changes.
    /// <paramref name="container"/> is the building the inventory belongs to, or null for the backpack or an opened crate.
    /// A left click picks up a slot's stack and a right click half of it; a held stack put on a slot lands there, unless <paramref name="takes"/> narrows what the grid takes.
    /// <paramref name="batch"/> is what a Ctrl- or Shift-click on a stack does, and <paramref name="use"/> what a double click does.
    /// <paramref name="put"/>, given a slot and a number of units, puts them down instead of the move to that slot.
    /// The inventory is re-read on each refresh because death replaces the backpack object.
    /// With <paramref name="wanted"/>, stacks it names are rimmed in orange and the rest fade, so what the open page takes stands out.
    /// </summary>
    protected GridContainer NewInventoryGrid(Func<Inventory> inventory, Thing? container, Action<string>? batch, string hint, Func<HeldCursor, bool>? takes = null, Func<string, bool>? wanted = null, int columns = GridColumns, float size = 52, Action<string>? use = null, Action<int, string, int>? put = null, Func<string, bool>? open = null) {
        var grid = NewGrid(columns);
        if (size < 52) { grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); }
        var stacks = new (string Item, int Count)?[inventory().Slots];
        var cells = Enumerable.Range(0, stacks.Length).Select(i => ItemSlot.Create($"slot_{i}", size)).ToArray();
        var shown = new bool[cells.Length];
        bool first = true;
        for (int i = 0; i < cells.Length; i++) {
            int slot = i;
            var cell = cells[i].Cell;
            // A click on a stack acts on that very slot when the item lies in several.
            Action? On(Action<string>? act) => act == null ? null : () => {
                if (stacks[slot] is not (string id, _)) return;
                var held = inventory(); held.PreferredSlot = slot;
                try { kit.Run(() => act(id)); } finally { held.PreferredSlot = -1; }
            };
            cell.Controls = new SlotInput(cursor) {
                Pick = () => stacks[slot] != null ? (inventory(), slot) : null,
                // A building's own rules answer at once, so a slot that will not take the stack shakes; a liquid with no vessel to go into also says why.
                Takes = takes ?? (held => held.Stack is HeldStack stack && Accepts(stack.Item) && Fills(stack.Item)),
                Put = (_, units) => kit.Run(() => moves.PutDown(inventory(), slot, units, container, put == null ? null : (item, count) => put(slot, item, count))),
                Batch = On(batch),
                Use = On(use),
                Open = open == null ? null : () => stacks[slot] is (string id, _) && open(id),
            };
            grid.AddChild(cell);
        }
        bool Accepts(string item) {
            if (container == null || world.BoxBlock(container, inventory(), item) is not string refused) return true;
            if (Barrel.Is(item)) kit.Notify(refused);
            return false;
        }
        // A liquid held from another box goes only into vessels here with room for it.
        bool Fills(string item) {
            if (world.HeldBox is not Inventory from || from == inventory() || world.FillBlock(from, inventory(), item, container) is not string dry) return true;
            kit.Notify(dry); return false;
        }
        // What each slot last drew: its stack, the inventory's total of that item and whether the stack is bound to the hotbar. A slot is redrawn only when they change.
        var current = new System.Collections.Generic.List<(string Item, int Count)>();
        var drawn = new (string? Item, int Count, int Total, bool Bound, int Life, bool Dark)[cells.Length];
        live.Add(() => {
            var held = inventory();
            held.FillStacks(current);
            int visible = held.ShownSlots, changed = 0;
            for (int i = 0; i < cells.Length; i++) {
                var (cell, count) = cells[i];
                stacks[i] = i < current.Count && current[i].Item != "" ? current[i] : null;
                bool bound = i < held.Layout.Count && held.Layout[i].Hotbar != 0;
                var looks = stacks[i] is (string item, int n)
                    ? (item, n, held[item], bound, item == Firebugs.Item ? world.Firebugs.AverageMinutes(held, i) : -1, Lanterns.IsItem(item) && world.Lanterns.BugCount(item) == 0)
                    : ((string?)null, 0, 0, false, -1, false);
                if (first || looks != drawn[i]) {
                    drawn[i] = looks;
                    Redraw(cell, count, stacks[i], held, bound, drawn[i].Life);
                }
                if (shown[i] == i < visible && !first) continue;
                shown[i] = i < visible;
                if (first) cell.Visible = shown[i];
                else GrowCell(cell, shown[i], changed++ * .035f);
            }
            first = false;
        });
        return grid;
        void Redraw(ItemSlot cell, Badge count, (string Item, int Count)? stack, Inventory held, bool bound, int life) {
            cell.Icon = stack is (string id, _) ? Icons.Of(id) : null;
            var contents = stack is (string crate, _) ? world.Packed(crate) : null;
            var badge = cell.GetNode<TextureRect>("badge");
            badge.Visible = contents != null;
            if (contents != null) badge.Texture = Icons.Of(contents.Items.OrderByDescending(p => p.Value).First().Key);
            count.Text = stack is (string barrel, 1) && Barrel.Is(barrel) ? $"{Barrel.Liters(barrel)}/{Barrel.Capacity} L"
                : stack is (string bottle, 1) && world.Data.VesselOf(bottle) is VesselDef vessel ? $"{vessel.LitersIn(bottle)}/{vessel.Capacity}" : contents != null ? contents.Count.ToString() : stack is (_, int n) ? n.ToString() : "";
            bool takes = wanted != null && stack is (string item, _) && wanted(item);
            if (wanted != null) cell.AddThemeColorOverride(ItemSlot.IconNormal, stack == null || takes ? Colors.White : Style.Faded);
            // A backpack stack bound to the hotbar has the selected hotbar slot's rim.
            cell.AddThemeStyleboxOverride("normal", takes || bound ? Style.SlotWanted : Style.SlotNormal);
            string clicks = open != null && stack is (string itemToOpen, _) && Lanterns.IsItem(itemToOpen)
                ? "LMB: pick up · RMB: open for refill · Double LMB: take in hand" : hint;
            string expiry = life < 0 ? "" : "\n" + PageText.FirebugLife(life);
            cell.TooltipText = stack is (string named, _) ? $"{world.Describe(named)}{(contents != null ? "" : $" · {held[named]} total")}{world.Data.DescriptionLine(named)}{expiry}\n{clicks}" : "";
        }
    }

    /// <summary>A slot that springs open with a slight overshoot, or shrinks and fades before it leaves the grid.</summary>
    protected static void GrowCell(Control cell, bool open, float delay) {
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
}
