using System;
using System.Collections.Generic;

namespace ChopChopMeow;

public sealed partial class Inventory {
    /// <summary>Adds caught firebugs with the world time each one dies, without extending the life of any in the stack.</summary>
    internal int AddFirebugs(ReadOnlySpan<float> expires) => Add(Firebugs.Item, expires.Length, expires);

    internal float AverageFirebugExpiry(int slot) {
        if ((uint)slot >= (uint)Layout.Count || Layout[slot].FirebugExpiries is not { Count: > 0 } dates) return 0;
        double total = 0;
        foreach (float date in dates) total += date;
        return (float)(total / dates.Count);
    }

    /// <summary>The average expiry over all the firebug stacks in the box, such as a station's one input.</summary>
    internal float AverageFirebugExpiry() {
        double total = 0;
        int count = 0;
        foreach (var slot in Layout) if (slot.FirebugExpiries is { } dates) {
            foreach (float date in dates) total += date;
            count += dates.Count;
        }
        return count == 0 ? 0 : (float)(total / count);
    }

    /// <summary>Takes the shortest-lived free firebugs first, starting with the slot the player clicked.</summary>
    internal int TakeFirebugs(Span<float> taken) => TakeFirebugs(taken.Length, taken, false);

    private int TakeFirebugs(int quantity, Span<float> taken, bool spent) {
        if (quantity <= 0 || this[Firebugs.Item] == 0) return 0;
        int removed = 0;
        if ((uint)PreferredSlot < (uint)Layout.Count) {
            var preferred = Layout[PreferredSlot];
            if (preferred.Item == Firebugs.Item) TakeFirebugsFrom(preferred, Math.Min(quantity, Takeable(preferred)), taken, ref removed, spent);
        }
        while (removed < quantity) {
            Slot? oldest = null;
            float expires = float.PositiveInfinity;
            foreach (var slot in Layout) {
                if (slot.Item != Firebugs.Item || Takeable(slot) <= 0 || slot.FirebugExpiries![slot.Held] >= expires) continue;
                oldest = slot;
                expires = slot.FirebugExpiries[slot.Held];
            }
            if (oldest == null) break;
            TakeFirebugsFrom(oldest, 1, taken, ref removed, spent);
        }
        if (removed > 0) {
            int left = this[Firebugs.Item] - removed;
            if (left == 0) items.Remove(Firebugs.Item); else items[Firebugs.Item] = left;
            Changed?.Invoke();
        }
        return removed;
    }

    private void TakeFirebugsFrom(Slot slot, int count, Span<float> taken, ref int removed, bool spent) {
        if (count == 0) return;
        var dates = slot.FirebugExpiries!;
        if (!taken.IsEmpty) for (int i = 0; i < count; i++) taken[removed + i] = dates[slot.Held + i];
        dates.RemoveRange(slot.Held, count);
        removed += count;
        RemoveFromSlot(slot, count, spent);
    }

    private static void AddFirebugExpiries(Slot slot, ReadOnlySpan<float> expires) {
        if (expires.IsEmpty) return;
        var dates = slot.FirebugExpiries ??= new List<float>();
        foreach (float date in expires) dates.Add(date);
        // The held units are reserved at the front. Anything that joins the stack while held sorts behind them.
        dates.Sort(slot.Held, dates.Count - slot.Held, null);
    }

    /// <summary>Moves the oldest units from an unheld stack when two cells merge.</summary>
    private static void MoveFirebugExpiries(Slot from, Slot to, int count) {
        if (from.FirebugExpiries is not { } dates) return;
        AddFirebugExpiries(to, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(dates)[..count]);
        dates.RemoveRange(0, count);
    }

    /// <summary>Removes dead firebugs, returning the next time any in this inventory dies.</summary>
    internal float ExpireFirebugs(float now) {
        if (this[Firebugs.Item] == 0) return float.PositiveInfinity;
        int removed = 0;
        float next = float.PositiveInfinity;
        foreach (var slot in Layout) {
            if (slot.FirebugExpiries is not { } dates) continue;
            int keep = 0, held = 0;
            for (int i = 0; i < dates.Count; i++) {
                float expiry = dates[i];
                if (expiry <= now) continue;
                dates[keep++] = expiry;
                if (i < slot.Held) held++;
                next = Math.Min(next, expiry);
            }
            int dead = dates.Count - keep;
            if (dead == 0) continue;
            heldUnits -= slot.Held - held;
            slot.Held = held;
            if (held == 0) slot.HeldBy = 0;
            dates.RemoveRange(keep, dead);
            slot.Count -= dead;
            removed += dead;
            if (slot.Count == 0) {
                if (slot.Hotbar != 0) (UsedUp ??= new()).Add((slot.Hotbar, slot.Item));
                slot.Empty();
            }
        }
        if (removed > 0) {
            int left = this[Firebugs.Item] - removed;
            if (left == 0) items.Remove(Firebugs.Item); else items[Firebugs.Item] = left;
            Changed?.Invoke();
        }
        return next;
    }

    /// <summary>Every saved caught firebug has one valid, finite expiry in its own stack.</summary>
    internal bool ValidFirebugs() {
        int found = 0;
        foreach (var slot in Layout) {
            if (slot.Item != Firebugs.Item) { if (slot.FirebugExpiries != null) return false; continue; }
            if (slot.FirebugExpiries is not { } dates || dates.Count != slot.Count) return false;
            foreach (float date in dates) if (!float.IsFinite(date) || date <= 0) return false;
            found += slot.Count;
        }
        return found == this[Firebugs.Item];
    }

    /// <summary>Changes a packed crate's item name after something in it expires, preserving its cell and any hold on it.</summary>
    internal void RenamePackedCrate(int id, string name) {
        bool changed = false;
        foreach (var slot in Layout) {
            if (slot.Item == name || World.PackedId(slot.Item) != id) continue;
            slot.Item = name;
            changed = true;
        }
        if (changed) SyncAggregate();
    }
}
