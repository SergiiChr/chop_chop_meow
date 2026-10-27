using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>Item quantities and explicit automation access rules. Player access is independent of automation filters.</summary>
public sealed class Inventory {
    /// <summary>A persisted stack in a particular inventory cell.</summary>
    public sealed class Slot { public string Item { get; set; } = ""; public int Count { get; set; } }
    public Dictionary<string, int> Items { get; set; } = new();
    /// <summary>Physical stack positions. Items remains the aggregate compatibility view used by simulation code.</summary>
    public List<Slot> Layout { get; set; } = new();
    public HashSet<string> InputFilter { get; set; } = new();
    public HashSet<string> OutputFilter { get; set; } = new();
    public int Slots { get; set; } = 18;
    public int StackSize { get; set; } = 64;
    /// <summary>Most units the inventory holds across all its stacks, or 0 for no limit beyond its slots.</summary>
    public int Capacity { get; set; }
    /// <summary>Physical cell a UI action targets when an item id appears in several cells.</summary>
    [JsonIgnore] public int PreferredSlot { get; set; } = -1;
    private readonly Dictionary<string, int> layoutRemainder = new();
    public const string Coins = "coins";
    public int Count {
        get {
            int count = 0;
            foreach (var pair in Items) count += pair.Value;
            return count;
        }
    }
    public int UsedSlots {
        get {
            SyncLayout();
            int slots = 0;
            foreach (var slot in Layout) if (slot.Item != "") slots++;
            return slots;
        }
    }
    /// <summary>Minimum number of physical stacks required to represent the aggregate item counts.</summary>
    public int AggregateSlotsNeeded {
        get {
            int needed = 0;
            foreach (var pair in Items) {
                int size = StackOf(pair.Key);
                needed += (pair.Value + size - 1) / size;
            }
            return needed;
        }
    }
    public int this[string item] => Items.GetValueOrDefault(item);

    /// <summary>Items that take a slot each in any container: every tool, filled from the data when it loads.</summary>
    public static readonly HashSet<string> Unstacked = new();
    /// <summary>Coins stack to 1000 in any container, packed crates and tools never stack; every other item uses the container's stack size.</summary>
    public int StackOf(string item) => item == Coins ? 1000 : item.StartsWith(World.PackedPrefix) || Unstacked.Contains(item) ? 1 : StackSize;
    /// <summary>Units of an item that still fit: empty slots plus the unfilled part of its last stack.</summary>
    public int Room(string item) {
        SyncLayout();
        int stack = StackOf(item), room = 0, used = 0;
        for (int i = 0; i < Math.Min(Slots, Layout.Count); i++) {
            var slot = Layout[i];
            if (slot.Item == "") room += stack;
            else { used++; if (slot.Item == item) room += Math.Max(0, stack - slot.Count); }
        }
        room = Math.Max(0, room);
        if (Capacity > 0) room = Math.Min(room, Math.Max(0, Capacity - Count));
        return used > Slots ? 0 : room;
    }

    /// <summary>Slots to show: every stack, plus one single-item slot per unit of capacity left.</summary>
    public int ShownSlots {
        get {
            if (Capacity <= 0) return Slots;
            SyncLayout(); int last = 0;
            for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item != "") last = i + 1;
            return Math.Min(Slots, Math.Max(last, UsedSlots + Math.Max(0, Capacity - Count)));
        }
    }

    /// <summary>Each item split into full stacks followed by its remainder.</summary>
    public IEnumerable<(string Item, int Count)> Stacks() {
        var stacks = new List<(string Item, int Count)>();
        FillStacks(stacks);
        stacks.RemoveAll(s => s.Item == "");
        return stacks;
    }

    /// <summary>Fills <paramref name="stacks"/> with <see cref="Stacks"/>, without allocating, for panels that refresh while open.</summary>
    public void FillStacks(List<(string Item, int Count)> stacks) {
        stacks.Clear();
        SyncLayout();
        foreach (var slot in Layout) stacks.Add((slot.Item, slot.Count));
    }

    /// <summary>Reconciles aggregate edits made by legacy simulation code while retaining positions for surviving stacks.</summary>
    public void EnsureLayout() => SyncLayout();
    private void SyncLayout() {
        Layout ??= new();
        while (Layout.Count < Slots) Layout.Add(new Slot());
        var remaining = layoutRemainder; remaining.Clear(); foreach (var pair in Items) remaining[pair.Key] = pair.Value;
        for (int i = 0; i < Layout.Count; i++) {
            var s = Layout[i];
            if (s.Item == "") { s.Count = 0; continue; }
            if (s.Count <= 0 || !remaining.TryGetValue(s.Item, out int qty)) { s.Item = ""; s.Count = 0; continue; }
            s.Count = Math.Min(Math.Min(s.Count, qty), StackOf(s.Item)); remaining[s.Item] = qty - s.Count;
            if (s.Count == 0) { s.Item = ""; }
        }
        foreach (var (item, qty) in remaining) for (int left = qty; left > 0;) {
            int empty = -1;
            for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == "") { empty = i; break; }
            if (empty < 0) { Layout.Add(new Slot()); empty = Layout.Count - 1; }
            int n = Math.Min(StackOf(item), left);
            Layout[empty].Item = item; Layout[empty].Count = n;
            left -= n;
        }
        int active = Math.Min(Slots, Layout.Count);
        for (int i = active; i < Layout.Count; i++) {
            if (Layout[i].Item == "") continue;
            for (int j = 0; j < active; j++) if (Layout[j].Item == "") {
                (Layout[j], Layout[i]) = (Layout[i], Layout[j]);
                break;
            }
        }
    }

    /// <summary>Moves an exact source cell to a destination cell; matching stacks merge up to their stack limit, otherwise cells swap.</summary>
    public int MoveSlotTo(int source, Inventory destination, int target, int quantity = int.MaxValue) {
        SyncLayout(); if (!ReferenceEquals(this, destination)) destination.SyncLayout();
        if ((uint)source >= (uint)Layout.Count || (uint)target >= (uint)destination.Layout.Count) return 0;
        Slot a = Layout[source], b = destination.Layout[target]; if (a.Item == "" || quantity <= 0) return 0;
        bool same = ReferenceEquals(this, destination);
        if (same && source == target) return 0;
        if (b.Item == "" || b.Item == a.Item) {
            int room = same ? int.MaxValue : destination.Room(a.Item);
            if (!same && destination.Capacity > 0) room = Math.Min(room, Math.Max(0, destination.Capacity - destination.Count));
            int targetCount = b.Item == "" ? 0 : b.Count;
            int moved = Math.Min(Math.Min(Math.Min(quantity, a.Count), destination.StackOf(a.Item) - targetCount), room);
            if (moved > 0) {
                if (b.Item == "") b.Item = a.Item;
                b.Count = targetCount + moved; a.Count -= moved; if (a.Count == 0) a.Item = "";
                SyncAggregate(); if (!same) destination.SyncAggregate(); return moved;
            }
        }
        if (same && b.Item != a.Item && quantity >= a.Count) {
            Layout[source] = b; Layout[target] = a; SyncAggregate(); return a.Count;
        }
        return 0;
    }

    private void SyncAggregate() {
        Items.Clear(); foreach (var s in Layout) if (s.Item != "") Items[s.Item] = Items.GetValueOrDefault(s.Item) + s.Count;
    }

    /// <summary>Stacks loose items and orders player inventory by item name for predictable access.</summary>
    public void Sort() {
        SyncLayout(); Layout.RemoveAll(s => s.Item == "");
        Layout.Sort((a, b) => string.CompareOrdinal(a.Item, b.Item));
        for (int i = 0; i < Layout.Count - 1;) {
            var a = Layout[i]; var b = Layout[i + 1];
            if (a.Item == b.Item && a.Count < StackOf(a.Item)) { int n = Math.Min(StackOf(a.Item) - a.Count, b.Count); a.Count += n; b.Count -= n; if (b.Count == 0) Layout.RemoveAt(i + 1); else i++; } else i++;
        }
        SyncLayout();
    }

    public bool Accepts(string item) => InputFilter.Count == 0 || InputFilter.Contains(item);
    public bool AllowsWithdrawal(string item) => OutputFilter.Count == 0 || OutputFilter.Contains(item);

    public int Add(string item, int quantity) {
        SyncLayout();
        int amount = Math.Min(Math.Max(0, quantity), Room(item));
        if (amount > 0) {
            Items[item] = this[item] + amount; var left = amount;
            int limit = Math.Min(Slots, Layout.Count);
            for (int i = 0; i < limit; i++) { var s = Layout[i]; if (s.Item == item && s.Count < StackOf(item)) { int n = Math.Min(left, StackOf(item) - s.Count); s.Count += n; left -= n; if (left == 0) break; } }
            for (int i = 0; i < limit && left > 0; i++) if (Layout[i].Item == "") { int n = Math.Min(left, StackOf(item)); Layout[i].Item = item; Layout[i].Count = n; left -= n; }
        }
        return amount;
    }

    public int Remove(string item, int quantity) {
        SyncLayout();
        int amount = Math.Min(Math.Max(0, quantity), this[item]);
        if (amount == this[item]) Items.Remove(item);
        else Items[item] -= amount;
        int left = amount;
        if ((uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == item) left -= RemoveFromSlot(Layout[PreferredSlot], left);
        for (int i = Layout.Count - 1; i >= 0 && left > 0; i--) if (i != PreferredSlot && Layout[i].Item == item) left -= RemoveFromSlot(Layout[i], left);
        return amount;
    }

    private static int RemoveFromSlot(Slot slot, int quantity) {
        int removed = Math.Min(quantity, slot.Count); slot.Count -= removed;
        if (slot.Count == 0) slot.Item = "";
        return removed;
    }

    /// <summary>Changes the first matching stack to another item while keeping that exact cell and its quantity.</summary>
    public bool Replace(string oldItem, string newItem) {
        if (oldItem == newItem) return this[oldItem] > 0;
        SyncLayout();
        int slotIndex = (uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == oldItem ? PreferredSlot : -1;
        if (slotIndex < 0) for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == oldItem) { slotIndex = i; break; }
        if (slotIndex < 0) return false;
        Slot slot = Layout[slotIndex]; int quantity = slot.Count;
        if (quantity > StackOf(newItem)) return false;
        if (quantity > Room(newItem) + StackOf(newItem)) return false;
        slot.Item = newItem;
        Items[oldItem] -= quantity; if (Items[oldItem] <= 0) Items.Remove(oldItem);
        Items[newItem] = Items.GetValueOrDefault(newItem) + quantity;
        return true;
    }

    public bool Fits(IReadOnlyDictionary<string, int> added) {
        SyncLayout(); int total = 0, empty = 0, limit = Math.Min(Slots, Layout.Count);
        for (int i = 0; i < limit; i++) if (Layout[i].Item == "") empty++;
        int free = empty;
        foreach (var pair in added) {
            if (pair.Value < 0) return false;
            total += pair.Value;
            int space = 0; for (int i = 0; i < limit; i++) if (Layout[i].Item == pair.Key) space += Math.Max(0, StackOf(pair.Key) - Layout[i].Count);
            int remainder = Math.Max(0, pair.Value - space), size = StackOf(pair.Key);
            int required = (remainder + size - 1) / size;
            if (required > free) return false;
            free -= required;
        }
        return Capacity == 0 || Count + total <= Capacity;
    }

    public bool Has(Dictionary<string, int> cost) {
        foreach (var pair in cost) if (this[pair.Key] < pair.Value) return false;
        return true;
    }

    /// <summary>Any one item held, or null when empty.</summary>
    public string? First() {
        foreach (var pair in Items) return pair.Key;
        return null;
    }

    public bool Spend(Dictionary<string, int> cost) {
        if (!Has(cost)) return false;
        foreach (var pair in cost) Remove(pair.Key, pair.Value);
        return true;
    }

    public int TransferTo(Inventory destination, string item, int quantity, bool automated = false) {
        if (automated && (!AllowsWithdrawal(item) || !destination.Accepts(item))) return 0;
        int amount = Math.Min(this[item], Math.Min(Math.Max(0, quantity), destination.Room(item)));
        Remove(item, amount);
        return destination.Add(item, amount);
    }
}
