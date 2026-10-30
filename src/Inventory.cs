using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>Item quantities and explicit automation access rules. Player access is independent of automation filters.</summary>
public sealed class Inventory {
    /// <summary>A persisted stack in a particular inventory cell.</summary>
    public sealed class Slot { public string Item { get; set; } = ""; public int Count { get; set; } }
    private readonly Dictionary<string, int> items = new();
    private int slots = 18, stackSize = 64;
    /// <summary>Read-only view of each item's total across all stacks; the inventory's methods are the only way to change it.</summary>
    [JsonIgnore] public ItemTotals Items { get; }
    /// <summary>
    /// The totals as saved under the name "Items", for loading and network copies.
    /// Assigning copies the totals in; the stacks then need <see cref="RebuildStacks"/> unless <see cref="Layout"/> is set from the same source.
    /// </summary>
    [JsonPropertyName("Items")] public IReadOnlyDictionary<string, int> SavedItems { get => items; set { if (!ReferenceEquals(value, items)) { items.Clear(); foreach (var pair in value) items[pair.Key] = pair.Value; } } }
    /// <summary>Physical stack positions; <see cref="Items"/> holds each item's total across them.</summary>
    public List<Slot> Layout { get; set; } = new();
    public HashSet<string> InputFilter { get; set; } = new();
    public HashSet<string> OutputFilter { get; set; } = new();
    /// <summary>Number of cells. Changing it adds empty cells or moves stacks out of removed ones.</summary>
    public int Slots { get => slots; set { if (value == slots) return; slots = value; RebuildStacks(); } }
    /// <summary>Most units of one item per cell. Changing it splits or merges stacks to fit.</summary>
    public int StackSize { get => stackSize; set { if (value == stackSize) return; stackSize = value; RebuildStacks(); } }
    /// <summary>Most units the inventory holds across all its stacks, or 0 for no limit beyond its slots.</summary>
    public int Capacity { get; set; }
    /// <summary>Physical cell a UI action targets when an item id appears in several cells.</summary>
    [JsonIgnore] public int PreferredSlot { get; set; } = -1;
    private readonly Dictionary<string, int> layoutRemainder = new();
    public const string Coins = "coins";
    public Inventory() { Items = new ItemTotals(items); PadLayout(); }
    public int Count {
        get {
            int count = 0;
            foreach (var pair in items) count += pair.Value;
            return count;
        }
    }
    public int UsedSlots {
        get {
            int used = 0;
            foreach (var slot in Layout) if (slot.Item != "") used++;
            return used;
        }
    }
    /// <summary>Minimum number of physical stacks required to represent the aggregate item counts.</summary>
    public int AggregateSlotsNeeded {
        get {
            int needed = 0;
            foreach (var pair in items) {
                int size = StackOf(pair.Key);
                needed += (pair.Value + size - 1) / size;
            }
            return needed;
        }
    }
    public int this[string item] => items.GetValueOrDefault(item);

    /// <summary>Items that take a slot each in any container: every tool, filled from the data when it loads.</summary>
    public static readonly HashSet<string> Unstacked = new();
    /// <summary>Coins stack to 1000 in any container, packed crates and tools never stack; every other item uses the container's stack size.</summary>
    public int StackOf(string item) => item == Coins ? 1000 : item.StartsWith(World.PackedPrefix) || Unstacked.Contains(item) ? 1 : StackSize;
    /// <summary>Units of an item that still fit: empty slots plus the unfilled part of its last stack.</summary>
    public int Room(string item) {
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
            int last = 0;
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
        foreach (var slot in Layout) stacks.Add((slot.Item, slot.Count));
    }

    /// <summary>
    /// Brings the stacks in line with the item totals, for totals that arrived without matching stacks such as an old save's.
    /// Surviving stacks keep their cells, new units fill empty cells, and stacks beyond the slot count move into free cells.
    /// </summary>
    public void RebuildStacks() {
        Layout ??= new();
        PadLayout();
        var remaining = layoutRemainder; remaining.Clear(); foreach (var pair in items) remaining[pair.Key] = pair.Value;
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

    private void PadLayout() { while (Layout.Count < Slots) Layout.Add(new Slot()); }

    /// <summary>Removes every item.</summary>
    public void Clear() {
        items.Clear();
        foreach (var slot in Layout) { slot.Item = ""; slot.Count = 0; }
        PadLayout();
    }

    /// <summary>Moves an exact source cell to a destination cell; matching stacks merge up to their stack limit, otherwise cells swap.</summary>
    public int MoveSlotTo(int source, Inventory destination, int target, int quantity = int.MaxValue) {
        if ((uint)source >= (uint)Layout.Count || (uint)target >= (uint)destination.Layout.Count) return 0;
        Slot a = Layout[source], b = destination.Layout[target]; if (a.Item == "" || quantity <= 0) return 0;
        bool same = ReferenceEquals(this, destination);
        if (same && source == target) return 0;
        if (b.Item == "" || b.Item == a.Item) {
            int room = same ? int.MaxValue : destination.Room(a.Item);
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
        items.Clear(); foreach (var s in Layout) if (s.Item != "") items[s.Item] = items.GetValueOrDefault(s.Item) + s.Count;
    }

    /// <summary>Stacks loose items and orders player inventory by item name for predictable access.</summary>
    public void Sort() {
        Layout.RemoveAll(s => s.Item == "");
        Layout.Sort((a, b) => string.CompareOrdinal(a.Item, b.Item));
        for (int i = 0; i < Layout.Count - 1;) {
            var a = Layout[i]; var b = Layout[i + 1];
            if (a.Item == b.Item && a.Count < StackOf(a.Item)) { int n = Math.Min(StackOf(a.Item) - a.Count, b.Count); a.Count += n; b.Count -= n; if (b.Count == 0) Layout.RemoveAt(i + 1); else i++; } else i++;
        }
        PadLayout();
    }

    public bool Accepts(string item) => InputFilter.Count == 0 || Names(InputFilter, item);
    public bool AllowsWithdrawal(string item) => OutputFilter.Count == 0 || Names(OutputFilter, item);
    /// <summary>Whether the input filter asks for this item by name, so automation prefers this storage over unrestricted ones.</summary>
    public bool Wants(string item) => InputFilter.Count > 0 && Names(InputFilter, item);
    /// <summary>A filter names an item itself, or for a packed crate the kind of crate it is (<see cref="World.CrateOf"/>).</summary>
    private static bool Names(HashSet<string> filter, string item) {
        if (filter.Contains(item)) return true;
        var kind = World.PackedKind(item);
        return !kind.IsEmpty && filter.GetAlternateLookup<ReadOnlySpan<char>>().Contains(kind);
    }

    public int Add(string item, int quantity) {
        int amount = Math.Min(Math.Max(0, quantity), Room(item));
        if (amount > 0) {
            items[item] = this[item] + amount; var left = amount;
            int limit = Math.Min(Slots, Layout.Count);
            for (int i = 0; i < limit; i++) { var s = Layout[i]; if (s.Item == item && s.Count < StackOf(item)) { int n = Math.Min(left, StackOf(item) - s.Count); s.Count += n; left -= n; if (left == 0) break; } }
            for (int i = 0; i < limit && left > 0; i++) if (Layout[i].Item == "") { int n = Math.Min(left, StackOf(item)); Layout[i].Item = item; Layout[i].Count = n; left -= n; }
        }
        return amount;
    }

    public int Remove(string item, int quantity) {
        int amount = Math.Min(Math.Max(0, quantity), this[item]);
        if (amount == this[item]) items.Remove(item);
        else items[item] -= amount;
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
        int slotIndex = (uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == oldItem ? PreferredSlot : -1;
        if (slotIndex < 0) for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == oldItem) { slotIndex = i; break; }
        if (slotIndex < 0) return false;
        Slot slot = Layout[slotIndex]; int quantity = slot.Count;
        if (quantity > StackOf(newItem)) return false;
        slot.Item = newItem;
        items[oldItem] -= quantity; if (items[oldItem] <= 0) items.Remove(oldItem);
        items[newItem] = items.GetValueOrDefault(newItem) + quantity;
        return true;
    }

    public bool Fits(IReadOnlyDictionary<string, int> added) {
        int total = 0, empty = 0, limit = Math.Min(Slots, Layout.Count);
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
        foreach (var pair in items) return pair.Key;
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

/// <summary>Read-only view of an inventory's item totals whose enumerator is a struct, so a loop over it allocates nothing.</summary>
public sealed class ItemTotals : IReadOnlyDictionary<string, int> {
    private readonly Dictionary<string, int> totals;
    internal ItemTotals(Dictionary<string, int> totals) { this.totals = totals; }
    public int Count => totals.Count;
    public int this[string item] => totals[item];
    public Dictionary<string, int>.KeyCollection Keys => totals.Keys;
    public bool ContainsKey(string item) => totals.ContainsKey(item);
    public bool TryGetValue(string item, out int count) => totals.TryGetValue(item, out count);
    public Dictionary<string, int>.Enumerator GetEnumerator() => totals.GetEnumerator();
    IEnumerable<string> IReadOnlyDictionary<string, int>.Keys => totals.Keys;
    IEnumerable<int> IReadOnlyDictionary<string, int>.Values => totals.Values;
    IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator() => totals.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => totals.GetEnumerator();
}
