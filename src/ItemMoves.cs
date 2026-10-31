using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>What clicking or dragging an item does, for every panel: reach, refusals and the crate-in-crate warning come first.</summary>
public sealed class ItemMoves(UiKit kit, BlackHole blackHole) {
    private readonly World world = kit.World;

    /// <summary>Whether a click moves every unit of an item rather than one item: while Ctrl or Shift is held.</summary>
    public static bool All => Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Shift);

    /// <summary>Whether the player can reach a thing; if not, says so.</summary>
    private bool InReach(Thing t) {
        if (world.Near(t)) return true;
        kit.Notify("Walk closer first."); return false;
    }

    /// <summary>Moves one item, or every unit of the item while Ctrl or Shift is held.</summary>
    public void Stack(Inventory from, Inventory to, string item, Thing near) {
        if (!InReach(near)) return;
        if (world.BoxBlock(near, to, item) is string refused) { kit.Notify(refused); return; }
        if (blackHole.CrateIntoCrate(near, to, item)) return;
        world.Transfer(from, to, item, All ? from[item] : 1);
    }

    /// <summary>Drops a dragged stack into an inventory and returns what did not fit; the building on either end must be within reach.</summary>
    public int Drag(ItemDrag drag, Inventory to, Thing? container, int target = -1) {
        if ((drag.Container ?? container) is Thing near && !InReach(near)) return 0;
        if (drag.From != to && container != null && world.BoxBlock(container, to, drag.Item) is string refused) { kit.Notify(refused); return 0; }
        if (blackHole.CrateIntoCrate(container, to, drag.Item) || drag.From == null) return 0;
        if (target >= 0 && drag.SourceSlot >= 0) {
            if (drag.From == to && drag.SourceSlot == target) return 0;
            int moved = world.TransferSlot(drag.From, to, drag.SourceSlot, target, drag.Item, drag.Count);
            return world.Remote ? 0 : Math.Max(0, drag.Count - moved);
        }
        int left = Leftover(drag, to.Room(drag.Item));
        world.Transfer(drag.From, to, drag.Item, drag.Count);
        return left;
    }

    /// <summary>Stores what a thing takes from the backpack, <paramref name="amount"/> saying how much of each item.</summary>
    public void StoreAll(Thing t, Func<string, int> amount) {
        if (!InReach(t)) return;
        foreach (var id in world.Backpack.Items.Keys.Where(id => world.Holds(t, id) && t.Inventory.Accepts(id)).ToArray())
            if (amount(id) is > 0 and int count) world.Transfer(world.Backpack, t.Inventory, id, count);
    }

    /// <summary>All of a material; tools stay, and so do crates, so one is never packed inside another.</summary>
    public int Materials(string id) => world.Data.Items.TryGetValue(id, out var def) && def.Tool == "" && id != "crate" ? 999 : 0;

    public void TakeAll(Thing t) {
        if (!InReach(t)) return;
        foreach (var id in t.Inventory.Items.Keys.ToArray()) world.Transfer(t.Inventory, world.Backpack, id, 999);
    }

    /// <summary>Makes a cup hand one item of an inventory to the backpack: a click moves it as <see cref="Stack"/> does, a drag carries the stack.</summary>
    public void TakeCell(ItemSlot cell, Inventory box, string item, Thing near) => TakeCell(cell, box, () => item, near);

    /// <summary>The same for a cup whose item changes; <paramref name="item"/> is null while it has nothing to give.</summary>
    public void TakeCell(ItemSlot cell, Inventory box, Func<string?> item, Thing near) {
        cell.Pressed += () => { if (item() is string id) kit.Run(() => Stack(box, world.S.Backpack, id, near)); };
        cell.Drag = () => item() is string id && box[id] > 0 ? new ItemDrag(id, box[id], box, near) : null;
    }

    /// <summary>
    /// Units of a drag that will not fit into the given room.
    /// Worked out before the move because a guest's move happens later, on the host.
    /// </summary>
    public static int Leftover(ItemDrag drag, int room) => Math.Max(0, Math.Min(drag.Count, drag.From![drag.Item]) - room);
}
