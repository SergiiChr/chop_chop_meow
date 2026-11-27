namespace ChopChopMeow;

/// <summary>
/// Hotbar slots bound to backpack stacks: a held stack put on a hotbar slot binds that exact stack (<see cref="Inventory.Slot.Hotbar"/>).
/// A bound slot uses only its stack and empties once the stack moves out of the backpack; one used up binds the next stack of its item.
/// A Ctrl batch or Store all leaves bound stacks behind (<see cref="FromSpares"/>).
/// A slot that names an item instead, as a new game's or an old save's does, waits for that item and binds the first stack of it to come into the backpack.
/// </summary>
public sealed partial class World {
    /// <summary>What a hotbar slot shows: the item of the backpack stack bound to it, or else its building plan or the item it waits for; empty for none.</summary>
    public string HotbarItem(int slot) => HotbarStack(slot) is int at and >= 0 ? S.Backpack.Layout[at].Item : S.HotbarSlots[slot];

    /// <summary>The backpack cell of the stack bound to a hotbar slot, or -1.</summary>
    public int HotbarStack(int slot) {
        var layout = S.Backpack.Layout;
        for (int i = 0; i < layout.Count; i++) if (layout[i].Hotbar == slot + 1) return i;
        return -1;
    }

    /// <summary>Units in the stack bound to a hotbar slot, or 0 for a slot bound to none.</summary>
    public int HotbarCount(int slot) => HotbarStack(slot) is int at and >= 0 ? S.Backpack.Layout[at].Count : 0;

    /// <summary>Ends the binding of whatever backpack stack a hotbar slot is bound to.</summary>
    private void Unbind(int slot) {
        foreach (var s in S.Backpack.Layout) if (s.Hotbar == slot + 1) s.Hotbar = 0;
    }

    /// <summary>Binds a backpack stack to a hotbar slot; a stack has one hotbar slot, so one it was bound to before empties.</summary>
    private void Bind(int slot, int cell) {
        Unbind(slot);
        S.Backpack.Layout[cell].Hotbar = slot + 1;
        S.HotbarSlots[slot] = "";
    }

    /// <summary>
    /// Server: binds the held stack to a hotbar slot while a command marked "from held stack" runs.
    /// A stack held from elsewhere, such as a chest, moves into the backpack first: into an empty cell, or onto a stack of the item with room for all of it.
    /// </summary>
    private void BindHeld(int slot) {
        var from = heldSource!; int at = from.PreferredSlot;
        var bag = S.Backpack;
        if (from == bag) { Bind(slot, at); return; }
        string item = from.Layout[at].Item;
        int cell = bag.Layout.FindIndex(s => s.Item == "");
        if (cell < 0) cell = bag.Layout.FindIndex(s => s.Item == item && bag.StackOf(item) - s.Count >= heldCount);
        if (cell < 0) { Say("Your backpack is full."); return; }
        if (TransferSlot(from, bag, at, cell, item, heldCount) > 0) Bind(slot, cell);
    }

    /// <summary>Whether a backpack stack of <paramref name="item"/> is what a hotbar slot waiting for <paramref name="waiting"/> takes: the item itself, or any bottle for a bottle or a water bottle.</summary>
    private static bool Waits(string waiting, string item) => item == waiting || Water.IsBottle(item) && (Water.IsBottle(waiting) || waiting == Water.Full);

    /// <summary>
    /// Server: binds each hotbar slot that waits for an item to the first unbound backpack stack of it, once one is there.
    /// A slot whose stack was used up, such as its last berry eaten, waits for its item again, so it binds the next stack of it at once.
    /// </summary>
    private void BindWaitingHotbars() {
        foreach (var p in S.Players) {
            if (p.Backpack.UsedUp is { Count: > 0 } used) {
                foreach (var (hotbar, item) in used) if (p.HotbarSlots[hotbar - 1] == "" && !Bound(p, hotbar - 1)) p.HotbarSlots[hotbar - 1] = item;
                used.Clear();
            }
            for (int slot = 0; slot < p.HotbarSlots.Count; slot++) {
                string waiting = p.HotbarSlots[slot];
                if (waiting == "" || Data.IsBuilding(waiting)) continue;
                if (Bound(p, slot)) { p.HotbarSlots[slot] = ""; continue; }
                foreach (var s in p.Backpack.Layout) {
                    if (s.Hotbar != 0 || s.Item == "" || !Waits(waiting, s.Item)) continue;
                    s.Hotbar = slot + 1; p.HotbarSlots[slot] = "";
                    break;
                }
            }
        }
    }

    private static bool Bound(PlayerState p, int slot) {
        foreach (var s in p.Backpack.Layout) if (s.Hotbar == slot + 1) return true;
        return false;
    }

    /// <summary>Keeps a loaded backpack's hotbar bindings to one stack per hotbar slot, and a bound slot naming nothing else.</summary>
    private static void TidyBindings(PlayerState p) {
        var bound = new bool[9];
        foreach (var s in p.Backpack.Layout) {
            if (s.Hotbar is < 1 or > 9 || s.Item == "" || bound[s.Hotbar - 1]) { s.Hotbar = 0; continue; }
            bound[s.Hotbar - 1] = true; p.HotbarSlots[s.Hotbar - 1] = "";
        }
    }
}
