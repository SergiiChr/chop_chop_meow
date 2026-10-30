using System;
using Godot;

namespace ChopChopMeow;

/// <summary>What clicking or dragging an item does, for every panel: reach, refusals and the crate-in-crate warning come first.</summary>
public sealed class ItemMoves(UiKit kit, BlackHole blackHole) {
    private readonly World world = kit.World;

    /// <summary>Whether a click moves every unit of an item rather than one item: Ctrl, or Shift as before.</summary>
    public static bool All => Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Shift);

    /// <summary>Moves one item, or every unit of the item while Ctrl or Shift is held.</summary>
    public void Stack(Inventory from, Inventory to, string item, Thing near) {
        if (!world.Near(near)) { kit.Notify("Walk closer first."); return; }
        if (world.BoxBlock(near, to, item) is string refused) { kit.Notify(refused); return; }
        if (blackHole.CrateIntoCrate(near, to, item)) return;
        world.Transfer(from, to, item, All ? from[item] : 1);
    }

    /// <summary>Drops a dragged stack into an inventory and returns what did not fit; the building on either end must be within reach.</summary>
    public int Drag(ItemDrag drag, Inventory to, Thing? container, int target = -1) {
        if ((drag.Container ?? container) is Thing near && !world.Near(near)) { kit.Notify("Walk closer first."); return 0; }
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

    /// <summary>
    /// Units of a drag that will not fit into the given room.
    /// Worked out before the move because a guest's move happens later, on the host.
    /// </summary>
    public static int Leftover(ItemDrag drag, int room) => Math.Max(0, Math.Min(drag.Count, drag.From![drag.Item]) - room);
}
