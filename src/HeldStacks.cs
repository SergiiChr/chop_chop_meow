using System;

namespace ChopChopMeow;

/// <summary>
/// Held stacks: a player picks up a stack onto the cursor and puts it down somewhere else.
/// Held units stay in their cell, so a stack that is let go of is back where it was, but nobody else takes them meanwhile:
/// not another player, not a worker or hauler, and a station holding one starts no batch.
/// </summary>
public sealed partial class World {
    public const string HeldByAnother = "Another player is holding that.";

    /// <summary>The acting player's held stack, or null.</summary>
    public HeldStack? Held => S.Actor.HeldStack;

    /// <summary>Picks up <paramref name="count"/> units of a cell onto the cursor, letting go of any stack held before.</summary>
    public void Hold(Inventory box, int slot, int count) {
        int code = Locate(box);
        if (code == -1 || Downed || (uint)slot >= (uint)box.Layout.Count || box.Layout[slot] is not { Item: not "" } cell) return;
        if (Forwarded(Verb.Hold, code, count, cell.Item, slot)) return;
        if (Building(code) is Thing owner && !Reaches(owner)) { Say("Walk closer first."); return; }
        if (cell.HeldByOther(S.Actor.Id)) { Say(HeldByAnother); return; }
        Release();
        int held = box.Hold(slot, count, S.Actor.Id);
        if (held == 0) return;
        S.Actor.HeldBox = box; S.Actor.HeldSlot = cell;
        S.Actor.HeldStack = new HeldStack(code, slot, cell.Item, held);
    }

    /// <summary>Picks up units of a cell of the backpack a view shows.</summary>
    public void Hold(InventoryView box, int slot, int count) => Hold(box.Inventory, slot, count);

    /// <summary>Puts units of the held stack into the backpack a view shows.</summary>
    public void PutDown(InventoryView to, int slot, int count) => PutDown(to.Inventory, slot, count);

    /// <summary>Lets go of the held stack, which is then back in its cell as it always was.</summary>
    public void Release() {
        if (S.Actor.HeldStack == null || Forwarded(Verb.Release)) return;
        if (HeldCell(S.Actor, out var box, out _, out int at)) box.Release(at);
        Forget(S.Actor);
    }

    private static void Forget(PlayerState p) { p.HeldStack = null; p.HeldBox = null; p.HeldSlot = null; }

    /// <summary>Server: the cell a player's held stack lies in, while it is still there and theirs; when it is not, the player holds nothing.</summary>
    private static bool HeldCell(PlayerState p, out Inventory box, out Inventory.Slot cell, out int at) {
        box = p.HeldBox!; cell = p.HeldSlot!; at = -1;
        if (p.HeldStack != null && box != null && cell != null && (at = box.Layout.IndexOf(cell)) >= 0 && cell.HeldBy == p.Id) return true;
        Forget(p);
        return false;
    }

    /// <summary>
    /// Puts <paramref name="count"/> units of the held stack into an inventory: into cell <paramref name="slot"/>, or with -1 wherever that inventory takes them.
    /// The move follows the rules of where it lands, as a Ctrl- or Shift-click would: a station's input slot takes only its inputs from the backpack, a fuel slot its fuels, a mine what it still needs.
    /// What does not fit stays held; a put-down on the held stack's own cell lets go of it.
    /// </summary>
    public void PutDown(Inventory to, int slot, int count) {
        if (S.Actor.HeldStack is not HeldStack held) return;
        int code = Locate(to);
        if (code == -1 || slot >= to.Layout.Count) return;
        if (Forwarded(Verb.PutDown, code, count, held.Item, slot) || !HeldCell(S.Actor, out var from, out var cell, out int at)) return;
        // On its own cell, the whole stack is let go of, or as many units as put down.
        if (to == from && slot == at) {
            int keep = cell.Held - count;
            if (keep <= 0) { Release(); return; }
            from.Release(at); from.Hold(at, keep, S.Actor.Id); S.Actor.HeldStack = held with { Count = keep };
            return;
        }
        if (slot >= 0 && to.Layout[slot].HeldByOther(S.Actor.Id)) { Say(HeldByAnother); return; }
        Loosened((from, at, units) => Land(from, to, at, slot, held.Item, Math.Clamp(count, 1, units)));
    }

    /// <summary>
    /// Runs a move with the held stack's units loose and first in line to leave their inventory, then holds again what is left of them in their cell.
    /// <paramref name="move"/> gets the inventory, the cell and how many units were held.
    /// </summary>
    private void Loosened(Action<Inventory, int, int> move) {
        if (S.Actor.HeldStack is not HeldStack held || !HeldCell(S.Actor, out var from, out var cell, out int at)) return;
        int units = cell.Held, before = cell.Count;
        from.Release(at);
        from.PreferredSlot = at;
        try { move(from, at, units); } finally { from.PreferredSlot = -1; }
        // A swap moves the cell itself, and the stack it took the place of is not held.
        bool stayed = cell.Item == held.Item && from.Layout.IndexOf(cell) == at;
        int left = stayed ? Math.Min(units - (before - cell.Count), cell.Count) : 0;
        if (left > 0 && from.Hold(at, left, S.Actor.Id) is int kept and > 0) S.Actor.HeldStack = held with { Count = kept };
        else Forget(S.Actor);
    }

    /// <summary>Runs a move that takes <paramref name="item"/> from the backpack, out of the held stack when that is what it holds.</summary>
    private void FromHeld(string item, Action move) {
        if (S.Actor.HeldStack?.Item == item && S.Actor.HeldBox == S.Backpack) Loosened((_, _, _) => move()); else move();
    }

    /// <summary>Moves units of the held stack, lying in cell <paramref name="at"/> of <paramref name="from"/>, by the rules of the inventory they land in.</summary>
    private void Land(Inventory from, Inventory to, int at, int slot, string item, int count) {
        var owner = Owner(to);
        if (CrateOf(to) is int packing) { if (from == S.Backpack) MoveInCrate(packing, item, count, true); return; }
        if (CrateOf(from) is int unpacking) { if (to == S.Backpack) MoveInCrate(unpacking, item, count, false); return; }
        if (owner != null && to == owner.Tools) { if (from == S.Backpack) SlotTool(owner, item); return; }
        if (owner != null && to == owner.Burner) { if (from == S.Backpack) LoadFuel(owner, item, count); return; }
        if (owner is { Kind: ThingKind.Mine } && to == owner.Inventory) { if (from == S.Backpack) SupplyMine(owner, item, count); return; }
        if (owner != null && to == owner.Inventory && (owner.Kind == ThingKind.Packer || Selected(owner) != "")) {
            if (from == S.Backpack) LoadInput(owner, item, count);
            return;
        }
        if (slot >= 0) TransferSlot(from, to, at, slot, item, count); else Transfer(from, to, item, count);
    }

    /// <summary>The building an inventory belongs to, or null for a backpack or a packed crate's contents.</summary>
    private Thing? Owner(Inventory box) => Building(Locate(box));

    /// <summary>The building a number from <see cref="Locate"/> names a box of, or null.</summary>
    private Thing? Building(int code) => code > 0 ? Find(code / BoxCodes) : null;

    /// <summary>The number of the packed crate in the acting player's backpack whose contents these are, or null.</summary>
    private int? CrateOf(Inventory box) => Locate(box) is int code and <= CrateCode ? CrateCode - code : null;

    /// <summary>
    /// Server: lets go of held stacks whose player can no longer hold them: one that is down, out of reach of the building the stack lies in, or whose cell has gone.
    /// A stack whose inventory has moved, such as a packed crate's contents once the crate is put down or sold, is let go of too.
    /// </summary>
    private void KeepHolds() {
        foreach (var p in S.Players) {
            if (p.HeldStack is not HeldStack held) continue;
            using (ActAs(p)) {
                if (!HeldCell(p, out var box, out _, out _)) continue;
                // The box its number names now, which differs once the inventory has moved.
                bool moved = Resolve(held.Box) != box;
                if (moved || Downed || Building(held.Box) is Thing owner && !Reaches(owner)) Release();
            }
        }
    }

    /// <summary>Server: a player who leaves lets go of what they held.</summary>
    private void ReleaseFor(PlayerState p) { using (ActAs(p)) Release(); }

    /// <summary>
    /// Before a building and what it holds go, such as a crate lifted or a station taken down: whether another player holds a stack in it, which they are told stops it.
    /// The acting player's own held stack there is let go of.
    /// </summary>
    private bool HeldByOthers(Thing t) {
        foreach (var p in S.Players) {
            if (p.HeldBox is not Inventory box || BoxIndex(t, box) < 0) continue;
            if (p != S.Actor) { Say(HeldByAnother); return true; }
            Release();
        }
        return false;
    }

    /// <summary>Whether a station holds a stack a player has picked up, which stops it starting a batch.</summary>
    private static bool HeldAt(Thing t) => t.Inventory.Holding || t.Output?.Holding == true || t.Burner?.Holding == true;
}
