using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>What clicking an item does, for every panel: reach, refusals and the crate-in-crate warning come first.</summary>
public sealed class ItemMoves(UiKit kit, BlackHole blackHole, HeldCursor cursor) {
    private readonly World world = kit.World;

    /// <summary>Whether a click takes more than one, such as five lots at the market: while Ctrl or Shift is held.</summary>
    public static bool All => Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Shift);

    /// <summary>How many of an item a click moves out of an inventory: all of it with Ctrl, the clicked stack with Shift, otherwise one.</summary>
    public static int Amount(Inventory from, string item) {
        if (Input.IsKeyPressed(Key.Ctrl)) return from[item];
        if (!Input.IsKeyPressed(Key.Shift)) return 1;
        int slot = from.PreferredSlot;
        return slot >= 0 && slot < from.Layout.Count && from.Layout[slot].Item == item ? from.Layout[slot].Free : Math.Min(from[item], from.StackOf(item));
    }

    /// <summary>Whether the player can reach a thing; if not, says so.</summary>
    private bool InReach(Thing t) {
        if (world.Reaches(t)) return true;
        kit.Notify("Walk closer first."); return false;
    }

    /// <summary>Moves one item, the clicked stack while Shift is held, or every unit of the item while Ctrl is held.</summary>
    public void Stack(Inventory from, Inventory to, string item, Thing near) {
        if (!InReach(near)) return;
        if (world.BoxBlock(near, to, item) is string refused) { kit.Notify(refused); return; }
        if (blackHole.CrateIntoCrate(near, to, item)) return;
        world.Transfer(from, to, item, Amount(from, item));
    }

    /// <summary>
    /// Puts units of the held stack into an inventory with the slot's own <paramref name="command"/>, marked "from held stack" (<see cref="World.FromHeldStack"/>).
    /// Without one, they go in as storage: into slot <paramref name="slot"/>, or with -1 wherever the inventory takes them.
    /// A crate onto a placed crate sets off the nesting alert instead.
    /// </summary>
    public void PutDown(Inventory to, int slot, int units, Thing? near, Action<string, int>? command = null) {
        if (cursor.Stack is not HeldStack held) return;
        if (blackHole.CrateIntoCrate(near, to, held.Item)) { cursor.Release(); return; }
        world.FromHeldStack(() => (command ?? Store(to, slot))(held.Item, Math.Min(units, held.Count)));
    }

    /// <summary>Storage's command for units of the held stack: into slot <paramref name="slot"/>, or with -1 wherever the inventory takes them.</summary>
    public Action<string, int> Store(Inventory to, int slot) => (item, units) => {
        if (world.HeldBox is not Inventory from || cursor.Stack is not HeldStack held) return;
        if (slot >= 0) world.TransferSlot(from, to, held.Slot, slot, item, units); else world.Transfer(from, to, item, units);
    };

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

    /// <summary>
    /// Binds a slot that shows one item of an inventory: a click picks up its stack and a right click half, and Ctrl or Shift moves it to the backpack as <see cref="Stack"/> does.
    /// A held stack that <paramref name="takes"/> allows goes in with <paramref name="put"/>, or as storage without one; with no <paramref name="takes"/> the slot takes nothing and shakes.
    /// </summary>
    public void TakeCell(ItemSlot cell, Inventory box, string item, Thing near, Func<HeldCursor, bool>? takes = null, Action<string, int>? put = null) => TakeCell(cell, box, () => item, near, takes, put);

    /// <summary>The same for a slot whose item changes; <paramref name="item"/> is null while it has nothing to give.</summary>
    public void TakeCell(ItemSlot cell, Inventory box, Func<string?> item, Thing near, Func<HeldCursor, bool>? takes = null, Action<string, int>? put = null) {
        cell.Controls = new SlotInput(cursor) {
            Pick = () => item() is string id && box.Layout.FindIndex(s => s.Item == id) is int at and >= 0 ? (box, at) : null,
            Takes = takes,
            Put = (_, units) => kit.Run(() => PutDown(box, -1, units, near, put)),
            Batch = () => { if (item() is string id) kit.Run(() => Stack(box, world.S.Backpack, id, near)); },
        };
    }
}
