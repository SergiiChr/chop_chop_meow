using System;

namespace ChopChopMeow;

/// <summary>
/// Held stacks: a player picks up a stack onto the cursor and puts it down somewhere else.
/// Held units stay in their slot, so a stack that is let go of is back where it was, but nobody else takes them meanwhile:
/// not another player, not a worker or hauler, and a station holding one starts no batch.
/// The server keeps only the slot a stack lies in; the <see cref="HeldStack"/> players see is built from that slot.
/// </summary>
public sealed partial class World {
    public const string HeldByAnother = "Another player is holding that.";

    /// <summary>The acting player's held stack, or null: built from its slot on the server, as the server last sent it in a player's window.</summary>
    public HeldStack? Held => Host != null ? S.Actor.HeldStack : HeldOf(S.Actor);

    /// <summary>The inventory the acting player's held stack lies in, or null.</summary>
    public Inventory? HeldBox => Held is HeldStack held ? Resolve(held.Box) : null;

    /// <summary>Server: a player's held stack as their window sees it, built from the slot it lies in; null while they hold nothing or its inventory has gone.</summary>
    private HeldStack? HeldOf(PlayerState p) {
        if (!HeldCell(p, out var box, out var cell, out int at)) return null;
        int code = Locate(box);
        return code == -1 ? null : new HeldStack(code, at, cell.Item, cell.Held);
    }

    /// <summary>Server: writes each player's held stack from its slot into what is sent to their window.</summary>
    internal void ShareHeldStacks() {
        foreach (var p in S.Players) using (ActAs(p)) p.HeldStack = HeldOf(p);
    }

    /// <summary>Picks up <paramref name="count"/> units of a slot onto the cursor, letting go of any stack held before.</summary>
    public void Hold(Inventory box, int slot, int count) {
        int code = Locate(box);
        if (code == -1 || (uint)slot >= (uint)box.Layout.Count || box.Layout[slot] is not { Item: not "" } cell) return;
        if (Busy() || Forwarded(Verb.Hold, code, count, cell.Item, slot)) return;
        if (!InReach(code)) { Say("Walk closer first."); return; }
        if (cell.HeldByOther(S.Actor.Id)) { Say(HeldByAnother); return; }
        Release();
        if (box.Hold(slot, count, S.Actor.Id) == 0) return;
        S.Actor.HeldBox = box; S.Actor.HeldSlot = cell;
    }

    /// <summary>Picks up units of a slot of the backpack a view shows.</summary>
    public void Hold(InventoryView box, int slot, int count) => Hold(box.Inventory, slot, count);

    /// <summary>Lets go of the held stack, which is then back in its slot as it always was.</summary>
    public void Release() {
        if (S.Actor.HeldBox == null && Held == null || Forwarded(Verb.Release)) return;
        if (HeldCell(S.Actor, out var box, out _, out int at)) box.Release(at);
        Forget(S.Actor);
    }

    private static void Forget(PlayerState p) { p.HeldBox = null; p.HeldSlot = null; }

    /// <summary>Server: the slot a player's held stack lies in, while it is still there and theirs; when it is not, the player holds nothing.</summary>
    private static bool HeldCell(PlayerState p, out Inventory box, out Inventory.Slot cell, out int at) {
        box = p.HeldBox!; cell = p.HeldSlot!; at = -1;
        if (box != null && cell != null && (at = box.Layout.IndexOf(cell)) >= 0 && cell.HeldBy == p.Id) return true;
        Forget(p);
        return false;
    }

    // Set while a command marked "from held stack" is being sent or run.
    private bool fromHeld;
    /// <summary>Server: the inventory a command marked "from held stack" takes from while it runs, or null, and how many units the stack held.</summary>
    private Inventory? heldSource;
    private int heldCount;
    /// <summary>The inventory the command being sent or run takes from when it is marked "from held stack", or null.</summary>
    internal Inventory? MarkedSource => heldSource ?? (fromHeld ? HeldBox : null);

    /// <summary>
    /// Runs a slot's own command, such as <see cref="Transfer"/>, <see cref="LoadFuel"/> or <see cref="MoveInCrate"/>, with the mark "from held stack".
    /// The command then takes its units from the held stack, no more than it holds, and only when the stack lies where the command takes from; what is left stays held.
    /// It runs as a guest's command does, so what it returns tells nothing.
    /// </summary>
    public void FromHeldStack(Action command) {
        if (Held == null) return;
        fromHeld = true;
        try { command(); } finally { fromHeld = false; }
    }

    /// <summary>Server: runs a command marked "from held stack" with the held units loose and first in line to leave, then holds again what is left of them.</summary>
    private void RunFromHeld(Command c) {
        if (!HeldCell(S.Actor, out var box, out var cell, out int at) || c.Item != cell.Item) return;
        if (HeldSource(c, Locate(box)) is not int source || Resolve(source) != box || c.Verb == Verb.TransferSlot && (int)c.At.X != at) return;
        int units = cell.Held, before = cell.Count;
        var item = cell.Item;
        box.Release(at);
        box.PreferredSlot = at; heldSource = box;
        heldCount = units;
        // A count that is a number of units is no more than the held stack; others, such as a hotbar slot's number, stay as they are.
        var run = c with { Count = c.Verb is Verb.SetHotbar or Verb.PlaceCrate or Verb.PlaceBarrel or Verb.SlotTool ? c.Count : Math.Clamp(c.Count, 1, units), Held = false };
        try { Dispatch(run); } finally { box.PreferredSlot = -1; heldSource = null; }
        // A swap moves the slot itself, and the stack it took the place of is not held.
        bool stayed = cell.Item == item && box.Layout.IndexOf(cell) == at;
        int left = stayed ? Math.Min(units - (before - cell.Count), cell.Count) : 0;
        if (left == 0 || box.Hold(at, left, S.Actor.Id) == 0) Forget(S.Actor);
    }

    /// <summary>The inventory, as <see cref="Locate"/> numbers it, that a command takes its units from; null for a command that takes none.</summary>
    private static int? HeldSource(Command c, int held) => c.Verb switch {
        Verb.Transfer or Verb.TransferSlot => c.Target,
        Verb.MoveInCrate => c.Other != 0 ? 0 : CrateCode - c.Target,
        Verb.LoadFuel or Verb.LoadInput or Verb.SupplyMine or Verb.SlotTool => 0,
        Verb.MoveInBarrel when c.Other != 0 => 0,
        Verb.PlaceCrate or Verb.PlaceBarrel or Verb.SetHotbar => held,
        _ => null
    };

    /// <summary>
    /// Server: lets go of held stacks whose player can no longer hold them: one that is down, out of reach of the building or worker the stack lies with, or whose slot has gone.
    /// A stack whose inventory has gone, such as a packed crate's contents once the crate is sold, is let go of too.
    /// </summary>
    private void KeepHolds() {
        foreach (var p in S.Players) {
            if (p.HeldBox == null) continue;
            using (ActAs(p)) {
                if (!HeldCell(p, out var box, out _, out _)) continue;
                int code = Locate(box);
                if (code == -1 || Downed || !InReach(code)) Release();
            }
        }
    }

    /// <summary>Server: lets go of every stack held in an inventory, such as a packed crate's contents once the crate is put down.</summary>
    internal void ReleaseIn(Inventory box) {
        foreach (var p in S.Players) if (p.HeldBox == box) using (ActAs(p)) Release();
    }

    /// <summary>Server: a player who leaves lets go of what they held.</summary>
    private void ReleaseFor(PlayerState p) { using (ActAs(p)) Release(); }

    /// <summary>
    /// Before a building and what it holds go, such as a crate lifted or a station taken down: whether another player holds a stack in it, which they are told stops it.
    /// The acting player's own held stack there is let go of.
    /// </summary>
    internal bool HeldByOthers(Thing t) {
        foreach (var p in S.Players) {
            if (p.HeldBox is not Inventory box || BoxIndex(t, box) < 0) continue;
            if (p != S.Actor) { Say(HeldByAnother); return true; }
            Release();
        }
        return false;
    }

    /// <summary>Whether a station or compost holds a stack a player has picked up, which stops it starting a batch.</summary>
    private static bool HeldAt(Thing t) => t.Inventory.Holding || t.Output?.Holding == true || t.Burner?.Holding == true;
}
