using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>Item quantities and explicit automation access rules. Player access is independent of automation filters.</summary>
public sealed class Inventory {
    public enum Carrier { None, Player, Worker }

    /// <summary>A player's empty barrels stack beside one filled barrel, and a worker with a barrel or a packed crate carries nothing else in their hands.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public Carrier CarriedBy { get; set; }

    /// <summary>Whether a worker carries an item in both arms, one at a time and with nothing beside it: a barrel or a packed crate.</summary>
    private static bool BothArms(string item) => Barrel.Is(item) || World.IsPacked(item);

    /// <summary>Whether a worker's hands hold a barrel or a packed crate, which leaves room for nothing else.</summary>
    public bool ArmsFull {
        get {
            // Such an item is always the only one in hand.
            if (CarriedBy != Carrier.Worker || Count != 1) return false;
            foreach (string item in items.Keys) if (BothArms(item)) return true;
            return false;
        }
    }

    private int CarryRoom(string item) {
        if (CarriedBy == Carrier.Player && Barrel.IsFilled(item)) return Barrel.FilledIn(this) == null ? 1 : 0;
        if (CarriedBy != Carrier.Worker) return int.MaxValue;
        if (BothArms(item)) return Count == 0 ? 1 : 0;
        return ArmsFull ? 0 : int.MaxValue;
    }

    /// <summary>Whether an item counts toward the one a carrier may hold: a player's filled barrel, or a worker's barrel or packed crate.</summary>
    private bool CountsTowardCarryLimit(string item) => CarriedBy == Carrier.Player ? Barrel.IsFilled(item) : CarriedBy == Carrier.Worker && BothArms(item);
    private bool WithinCarryLimit(int limited, int incoming) =>
        limited <= 1 && (CarriedBy != Carrier.Worker || limited == 0 || incoming == 1);

    internal bool ValidFor(Carrier carrier) {
        if (CarriedBy != carrier) return false;
        int limited = 0;
        foreach (var (item, count) in items) if (CountsTowardCarryLimit(item)) limited += count;
        return WithinCarryLimit(limited, Count);
    }
    /// <summary>
    /// A persisted stack in a particular inventory cell.
    /// <see cref="Held"/> of its units are a player's held stack: they stay in the cell and count as here, but nothing takes them out until that player puts them down or lets go.
    /// </summary>
    public sealed class Slot {
        public string Item { get; set; } = "";
        public int Count { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Held { get; set; }
        /// <summary>The id of the player holding <see cref="Held"/>, or 0.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int HeldBy { get; set; }
        /// <summary>In a backpack, the number (1–9) of the hotbar slot bound to this stack, or 0; it goes when the stack does, and follows it into a stack it merges with.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Hotbar { get; set; }
        /// <summary>Units in the cell that no player holds.</summary>
        [JsonIgnore] public int Free => Count - Held;
        /// <summary>Empties the cell, which ends its hotbar binding.</summary>
        public void Empty() { Item = ""; Count = 0; Hotbar = 0; }
        /// <summary>Whether a player other than <paramref name="player"/> holds units here.</summary>
        public bool HeldByOther(int player) => HeldBy != 0 && HeldBy != player;
    }
    private readonly Dictionary<string, int> items = new();
    /// <summary>Units a player holds, which only the world that runs the game keeps count of.</summary>
    private int heldUnits;
    private const int NewSlots = 18, NewStackSize = 64, NewCoinStack = 1000;
    /// <summary>A coin stack no game comes near, for a box with no coin limit; small enough that adding stacks up never overflows.</summary>
    public const int UnlimitedCoins = int.MaxValue / 4;
    private int slots = NewSlots, stackSize = NewStackSize;
    /// <summary>Read-only view of each item's total across all stacks; play changes it only through the inventory's methods, and a load or update through <see cref="SavedItems"/>.</summary>
    [JsonIgnore] public ItemTotals Items { get; }
    /// <summary>
    /// The totals as saved under the name "Items", for loading and network copies.
    /// Assigning copies the totals in; the stacks then need <see cref="RebuildStacks"/> unless <see cref="Layout"/> is set from the same source.
    /// Typed as the dictionary itself, which the JSON writer walks without allocating, as updates do for every filled inventory thirty times a second.
    /// </summary>
    [JsonInclude, JsonPropertyName("Items")] internal Dictionary<string, int> SavedItems { get => items; set { if (!ReferenceEquals(value, items)) { items.Clear(); foreach (var pair in value) items[pair.Key] = pair.Value; } } }
    /// <summary>Physical stack positions; <see cref="Items"/> holds each item's total across them.</summary>
    public List<Slot> Layout { get; set; } = new();
    /// <summary>What workers bring here, in the order it was named; empty for anything.</summary>
    public FilterList InputFilter { get; set; } = new();
    /// <summary>What workers take from here, in the order it was named; empty for anything.</summary>
    public FilterList OutputFilter { get; set; } = new();
    /// <summary>Number of cells. Changing it adds empty cells or moves stacks out of removed ones.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int Slots { get => slots; set { if (value == slots) return; slots = value; RebuildStacks(); } }
    /// <summary>Most units of one item per cell. Changing it splits or merges stacks to fit.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int StackSize { get => stackSize; set { if (value == stackSize) return; stackSize = value; RebuildStacks(); } }
    /// <summary>
    /// Most coins per cell: 1000, or <see cref="UnlimitedCoins"/> in a trading post's wage slot.
    /// Read first, so the stacks that setting <see cref="Slots"/> rebuilds on load keep a wage slot's coins in its one cell.
    /// </summary>
    [JsonPropertyOrder(-1), JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int CoinStack { get; set; } = NewCoinStack;
    /// <summary>Most units the inventory holds across all its stacks, or 0 for no limit beyond its slots.</summary>
    public int Capacity { get; set; }
    /// <summary>Physical cell a UI action targets when an item id appears in several cells.</summary>
    [JsonIgnore] public int PreferredSlot { get; set; } = -1;
    /// <summary>While set, as for a Ctrl batch or Store all, units are taken only from stacks not bound to the hotbar.</summary>
    [JsonIgnore] public bool SparesOnly { get; set; }
    /// <summary>Units of a cell a removal may take now: those nobody holds, and none of a stack bound to the hotbar while <see cref="SparesOnly"/> is set.</summary>
    private int Takeable(Slot s) => SparesOnly && s.Hotbar != 0 ? 0 : s.Free;
    private readonly Dictionary<string, int> layoutRemainder = new();
    public const string Coins = "coins";
    public Inventory() { Items = new ItemTotals(items); PadLayout(); }
    /// <summary>Called whenever the items held or the filters change, for an owner that keeps a count of them up to date.</summary>
    public Action? Changed;
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
    /// <summary>Whether a player holds a stack from here.</summary>
    public bool Holding => heldUnits > 0;
    /// <summary>Units of an item that nobody holds, which are all that can be taken out.</summary>
    public int Free(string item) {
        int free = items.GetValueOrDefault(item);
        if (heldUnits > 0 || SparesOnly) foreach (var s in Layout) if (s.Item == item) free -= s.Count - Takeable(s);
        return free;
    }
    /// <summary>Empty with the settings of a new inventory, so it saves the same as one.</summary>
    [JsonIgnore] public bool Fresh => items.Count == 0 && CarriedBy == Carrier.None && slots == NewSlots && stackSize == NewStackSize && CoinStack == NewCoinStack && Capacity == 0 && InputFilter.Count == 0 && OutputFilter.Count == 0 && Layout.Count == NewSlots && UsedSlots == 0;

    /// <summary>Items that take a slot each in any container: every tool, filled from the data when it loads.</summary>
    public static readonly HashSet<string> Unstacked = new();
    /// <summary>Coins stack to the container's coin stack, packed crates and tools never stack; every other item uses the container's stack size.</summary>
    public int StackOf(string item) => item == Coins ? CoinStack : item.StartsWith(World.PackedPrefix) || Unstacked.Contains(item) ? 1 : StackSize;
    /// <summary>
    /// Units of an item that still fit in the one stack a station's input slot holds, as far as the box has room.
    /// Below zero when the box holds more than a stack of it.
    /// </summary>
    public int SlotRoom(string item) => Math.Min(Room(item), Math.Max(StackSize, StackOf(item)) - this[item]);
    /// <summary>Units of an item that still fit: empty slots plus the unfilled part of its last stack.</summary>
    public int Room(string item) => Room(item, replacing: false);

    private int Room(string item, bool replacing) {
        int stack = StackOf(item), room = 0, used = 0;
        for (int i = 0; i < Math.Min(Slots, Layout.Count); i++) {
            var slot = Layout[i];
            if (slot.Item == "") room += stack;
            else { used++; if (slot.Item == item) room += Math.Max(0, stack - slot.Count); }
        }
        room = Math.Max(0, room);
        if (Capacity > 0 && !replacing) room = Math.Min(room, Math.Max(0, Capacity - Count));
        return used > Slots ? 0 : Math.Min(room, CarryRoom(item));
    }

    /// <summary>Whether these goods, counted by item, all fit in this inventory at once: each item tops up its own stacks, and the rest takes empty slots.</summary>
    public bool Fits(Dictionary<string, int> goods) {
        int limit = Math.Min(Slots, Layout.Count), empty = 0;
        for (int i = 0; i < limit; i++) if (Layout[i].Item == "") empty++;
        foreach (var (item, count) in goods) {
            if (count > CarryRoom(item)) return false;
            int stack = StackOf(item), spare = 0;
            for (int i = 0; i < limit; i++) if (Layout[i].Item == item) spare += stack - Layout[i].Count;
            empty -= (Math.Max(0, count - spare) + stack - 1) / stack;
        }
        int limited = CarriedBy == Carrier.None ? 0 : goods.Where(pair => CountsTowardCarryLimit(pair.Key)).Sum(pair => pair.Value);
        int incoming = goods.Values.Sum();
        return WithinCarryLimit(limited, incoming) && empty >= 0 && (Capacity == 0 || Count + incoming <= Capacity);
    }

    /// <summary>The first item this box holds less of than one batch takes, or null.</summary>
    public string? Short(Dictionary<string, int> batch) {
        foreach (var (item, needed) in batch) if (this[item] < needed) return item;
        return null;
    }

    /// <summary>Slots to show: every stack, plus one single-item slot per unit of capacity left.</summary>
    public int ShownSlots {
        get {
            if (Capacity <= 0) return Slots;
            int last = 0;
            for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item != "") last = i + 1;
            int left = ArmsFull ? 0 : Math.Max(0, Capacity - Count);
            return Math.Min(Slots, Math.Max(last, UsedSlots + left));
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
    /// Brings the stacks in line with the item totals, for totals that arrived without matching stacks, as in a save edited by hand.
    /// Surviving stacks keep their cells, new units fill empty cells, and stacks beyond the slot count move into free cells.
    /// </summary>
    public void RebuildStacks() {
        Layout ??= new();
        PadLayout();
        // Holds end, as after loading nobody holds anything.
        foreach (var s in Layout) { s.Held = 0; s.HeldBy = 0; }
        heldUnits = 0;
        var remaining = layoutRemainder; remaining.Clear(); foreach (var pair in items) remaining[pair.Key] = pair.Value;
        for (int i = 0; i < Layout.Count; i++) {
            var s = Layout[i];
            if (s.Item == "") { s.Empty(); continue; }
            if (s.Count <= 0 || !remaining.TryGetValue(s.Item, out int qty)) { s.Empty(); continue; }
            s.Count = Math.Min(Math.Min(s.Count, qty), StackOf(s.Item)); remaining[s.Item] = qty - s.Count;
            if (s.Count == 0) s.Empty();
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
        // Empty cells past the slots, such as those of a smaller inventory made from a default one, are dropped.
        while (Layout.Count > Slots && Layout[^1].Item == "") Layout.RemoveAt(Layout.Count - 1);
    }

    private void PadLayout() { while (Layout.Count < Slots) Layout.Add(new Slot()); }

    /// <summary>Removes every item.</summary>
    public void Clear() {
        items.Clear(); heldUnits = 0;
        foreach (var slot in Layout) { slot.Empty(); slot.Held = 0; slot.HeldBy = 0; }
        PadLayout();
        Changed?.Invoke();
    }

    /// <summary>
    /// How much a drop of <paramref name="quantity"/> from cell <paramref name="source"/> of <paramref name="from"/> on one cell takes: what the cell has room for, or, on another item, what fits anywhere.
    /// Within one inventory a drop on another item swaps the two, which takes a whole stack and nothing less.
    /// </summary>
    public int RoomAt(int target, Inventory from, int source, int quantity) {
        if ((uint)target >= (uint)Layout.Count || (uint)source >= (uint)from.Layout.Count) return 0;
        Slot there = Layout[target], held = from.Layout[source];
        if (there.Item == "" || there.Item == held.Item) return MergeRoom(there, held.Item, from);
        return !ReferenceEquals(from, this) ? Room(held.Item) : quantity >= held.Count ? int.MaxValue : 0;
    }

    /// <summary>How much of an item from <paramref name="from"/> a cell holding nothing or the same item can take.</summary>
    private int MergeRoom(Slot there, string item, Inventory from) => Math.Min(ReferenceEquals(from, this) ? int.MaxValue : Room(item), StackOf(item) - (there.Item == "" ? 0 : there.Count));

    /// <summary>Moves an exact source cell to a destination cell; matching stacks merge up to their stack limit, otherwise cells swap.</summary>
    public int MoveSlotTo(int source, Inventory destination, int target, int quantity = int.MaxValue) {
        if ((uint)source >= (uint)Layout.Count || (uint)target >= (uint)destination.Layout.Count) return 0;
        Slot a = Layout[source], b = destination.Layout[target]; if (a.Item == "" || quantity <= 0 || a.Held > 0 || b.Held > 0) return 0;
        bool same = ReferenceEquals(this, destination);
        if (same && source == target) return 0;
        if (b.Item == "" || b.Item == a.Item) {
            int targetCount = b.Item == "" ? 0 : b.Count;
            int moved = Math.Min(Math.Min(quantity, a.Count), destination.MergeRoom(b, a.Item, this));
            if (moved > 0) {
                if (b.Item == "") b.Item = a.Item;
                b.Count = targetCount + moved; a.Count -= moved;
                // A whole stack moved onto another in the same inventory takes its hotbar slot along.
                if (a.Count == 0) { if (same && a.Hotbar != 0) b.Hotbar = a.Hotbar; a.Empty(); }
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
        Changed?.Invoke();
    }

    /// <summary>Marks units of a cell as a player's held stack; returns how many, at most what the cell has that nobody holds.</summary>
    public int Hold(int slot, int quantity, int player) {
        if ((uint)slot >= (uint)Layout.Count) return 0;
        var s = Layout[slot];
        if (s.Item == "" || s.HeldByOther(player)) return 0;
        int n = Math.Clamp(quantity, 0, s.Free);
        if (n == 0) return 0;
        s.Held += n; s.HeldBy = player; heldUnits += n;
        Changed?.Invoke();
        return n;
    }

    /// <summary>Lets go of what a player holds in a cell, so it can be taken out again.</summary>
    public void Release(int slot) {
        if ((uint)slot >= (uint)Layout.Count || Layout[slot] is not { Held: > 0 } s) return;
        heldUnits -= s.Held; s.Held = 0; s.HeldBy = 0;
        Changed?.Invoke();
    }

    /// <summary>Units of an item that players other than <paramref name="player"/> hold here.</summary>
    public int HeldByOthers(string item, int player) {
        if (heldUnits == 0) return 0;
        int held = 0;
        foreach (var s in Layout) if (s.Item == item && s.HeldByOther(player)) held += s.Held;
        return held;
    }

    /// <summary>Stacks loose items and orders player inventory by item name for predictable access.</summary>
    public void Sort() {
        Layout.RemoveAll(s => s.Item == "");
        Layout.Sort((a, b) => string.CompareOrdinal(a.Item, b.Item));
        for (int i = 0; i < Layout.Count - 1;) {
            var a = Layout[i]; var b = Layout[i + 1];
            if (a.Item == b.Item && a.Count < StackOf(a.Item) && a.Held == 0 && b.Held == 0) {
                int n = Math.Min(StackOf(a.Item) - a.Count, b.Count); a.Count += n; b.Count -= n;
                // The stack merged in takes its hotbar slot along, as a stack moved onto another does.
                if (b.Count == 0) { if (b.Hotbar != 0) a.Hotbar = b.Hotbar; Layout.RemoveAt(i + 1); } else i++;
            } else i++;
        }
        PadLayout();
    }

    /// <summary>Lets an item in or out through a filter, or stops naming it there.</summary>
    public void Filter(string item, bool output, bool allowed) {
        var filter = output ? OutputFilter : InputFilter;
        if (allowed) filter.Add(item); else filter.Remove(item);
        Changed?.Invoke();
    }
    /// <summary>Empties both filters, so anything goes in and out.</summary>
    public void ClearFilters() { InputFilter.Clear(); OutputFilter.Clear(); Changed?.Invoke(); }
    public bool Accepts(string item) => InputFilter.Count == 0 || Names(InputFilter, item);
    public bool AllowsWithdrawal(string item) => OutputFilter.Count == 0 || Names(OutputFilter, item);
    /// <summary>Whether the input filter asks for this item by name, so automation prefers this storage over unrestricted ones.</summary>
    public bool Wants(string item) => InputFilter.Count > 0 && Names(InputFilter, item);
    /// <summary>A filter names an item itself, or for a packed crate the kind of crate it is (<see cref="World.CrateOf"/>).</summary>
    private static bool Names(FilterList filter, string item) {
        if (filter.Contains(item)) return true;
        var kind = World.PackedKind(item);
        if (kind.IsEmpty) return false;
        foreach (string named in filter) if (kind.SequenceEqual(named)) return true;
        return false;
    }

    public int Add(string item, int quantity) {
        int amount = Math.Min(Math.Max(0, quantity), Room(item));
        if (amount > 0) {
            items[item] = this[item] + amount; var left = amount;
            int limit = Math.Min(Slots, Layout.Count);
            for (int i = 0; i < limit; i++) { var s = Layout[i]; if (s.Item == item && s.Count < StackOf(item)) { int n = Math.Min(left, StackOf(item) - s.Count); s.Count += n; left -= n; if (left == 0) break; } }
            for (int i = 0; i < limit && left > 0; i++) if (Layout[i].Item == "") { int n = Math.Min(left, StackOf(item)); Layout[i].Item = item; Layout[i].Count = n; left -= n; }
            Changed?.Invoke();
        }
        return amount;
    }

    /// <summary>
    /// Hotbar slots whose bound stack was used up here, spent rather than moved out, each with its item, for the world to bind to the next stack of it (<see cref="World.BindWaitingHotbars"/>).
    /// Made on first use, as only a backpack has bindings.
    /// </summary>
    [JsonIgnore] public List<(int Hotbar, string Item)>? UsedUp { get; private set; }

    /// <summary>Spends up to <paramref name="quantity"/> units nobody holds, from <see cref="PreferredSlot"/> first, as eating, building or crafting does.</summary>
    public int Remove(string item, int quantity) => Take(item, quantity, true);

    /// <summary>Takes out units nobody holds, from <see cref="PreferredSlot"/> first; a bound stack <paramref name="spent"/> to nothing is noted in <see cref="UsedUp"/>.</summary>
    private int Take(string item, int quantity, bool spent) {
        int amount = Math.Min(Math.Max(0, quantity), Free(item));
        if (amount == this[item]) items.Remove(item);
        else items[item] -= amount;
        int left = amount;
        if ((uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == item) left -= RemoveFromSlot(Layout[PreferredSlot], left, spent);
        for (int i = Layout.Count - 1; i >= 0 && left > 0; i--) if (i != PreferredSlot && Layout[i].Item == item) left -= RemoveFromSlot(Layout[i], left, spent);
        if (amount > 0) Changed?.Invoke();
        return amount;
    }

    private int RemoveFromSlot(Slot slot, int quantity, bool spent) {
        int removed = Math.Min(quantity, Takeable(slot)); slot.Count -= removed;
        if (slot.Count > 0) return removed;
        if (spent && slot.Hotbar != 0) (UsedUp ??= new()).Add((slot.Hotbar, slot.Item));
        slot.Empty();
        return removed;
    }

    /// <summary>Changes the first matching stack to another item while keeping that exact cell and its quantity.</summary>
    public bool Replace(string oldItem, string newItem) {
        if (oldItem == newItem) return this[oldItem] > 0;
        int slotIndex = (uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == oldItem && Layout[PreferredSlot].Held == 0 ? PreferredSlot : -1;
        if (slotIndex < 0) for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == oldItem && Layout[i].Held == 0) { slotIndex = i; break; }
        if (slotIndex < 0) return false;
        Slot slot = Layout[slotIndex]; int quantity = slot.Count;
        if (quantity > StackOf(newItem)) return false;
        slot.Item = newItem;
        items[oldItem] -= quantity; if (items[oldItem] <= 0) items.Remove(oldItem);
        items[newItem] = items.GetValueOrDefault(newItem) + quantity;
        Changed?.Invoke();
        return true;
    }

    internal bool CanReplaceOne(string oldItem, string newItem) {
        if (Free(oldItem) <= 0) return false;
        if (oldItem == newItem || Room(newItem, replacing: true) > 0) return true;
        foreach (var slot in Layout) if (slot.Item == oldItem && slot.Count == 1 && Takeable(slot) > 0) return true;
        return false;
    }

    /// <summary>The units that can become an item that takes one cell per unit.</summary>
    internal int RoomForFilled(string empty, string filled) {
        if (StackOf(empty) == 1) return Free(empty);
        int room = Room(filled, replacing: true), count = 0;
        for (int i = Math.Min(Slots, Layout.Count) - 1; i >= 0; i--) {
            var slot = Layout[i];
            if (slot.Item != empty) continue;
            int available = Takeable(slot);
            if (available == 0) continue;
            int split = Math.Min(room, Math.Max(0, Math.Min(available, slot.Count - 1)));
            count += split;
            room -= split;
            if (split + 1 == slot.Count && available == slot.Count) count++;
        }
        return count;
    }

    /// <summary>Changes one vessel, preserving its cell whenever the whole cell changes.</summary>
    public bool ReplaceOne(string oldItem, string newItem) {
        if ((uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == oldItem) {
            if (Takeable(Layout[PreferredSlot]) == 0) return false;
            if (Layout[PreferredSlot].Count == 1) return Replace(oldItem, newItem);
        } else {
            for (int i = 0; i < Layout.Count; i++) {
                if (Layout[i].Item != oldItem || Layout[i].Count != 1 || Takeable(Layout[i]) <= 0) continue;
                int previous = PreferredSlot;
                PreferredSlot = i;
                try { return Replace(oldItem, newItem); } finally { PreferredSlot = previous; }
            }
        }
        if (oldItem == newItem || Free(oldItem) == 0 || Room(newItem, replacing: true) == 0) return false;
        Take(oldItem, 1, false);
        Add(newItem, 1);
        return true;
    }

    /// <summary>Whether the other empty barrels can fit elsewhere when one barrel changes fill in its own cell.</summary>
    internal bool CanSplitBarrelStack(int at) {
        var stack = Layout[at];
        return stack.Count == 1 || Room(stack.Item, replacing: true) >= StackOf(stack.Item) - 1;
    }

    /// <summary>Changes the chosen barrel's fill in its cell and returns the rest of its stack to the backpack.</summary>
    internal void SetBarrelFill(int at, string item) {
        var stack = Layout[at];
        string before = stack.Item;
        if (before == item) return;
        int rest = stack.Count - 1;
        int elsewhere = this[before] - stack.Count;
        if (elsewhere == 0) items.Remove(before); else items[before] = elsewhere;
        stack.Item = item; stack.Count = 1;
        items[item] = this[item] + 1;
        if (rest > 0) Add(before, rest);
        else Changed?.Invoke();
    }

    /// <summary>Whether units nobody holds cover the cost.</summary>
    public bool Has(Dictionary<string, int> cost) {
        foreach (var pair in cost) if (Free(pair.Key) < pair.Value) return false;
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
        int amount = Math.Min(Free(item), Math.Min(Math.Max(0, quantity), destination.Room(item)));
        Take(item, amount, false);
        return destination.Add(item, amount);
    }
}

/// <summary>A filter's entries in the order they were named, each once: naming one again leaves it where it is.</summary>
public sealed class FilterList : Collection<string> {
    /// <summary>Walks the entries without allocating, as worker routing does on every tick.</summary>
    public new List<string>.Enumerator GetEnumerator() => ((List<string>)Items).GetEnumerator();
    protected override void InsertItem(int index, string item) { if (!Contains(item)) base.InsertItem(index, item); }
    protected override void SetItem(int index, string item) { if (!Contains(item)) base.SetItem(index, item); }
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

/// <summary>
/// A list to read but not change. A <c>foreach</c> over it allocates nothing, unlike one over an interface such as <c>IReadOnlyList</c>.
/// LINQ works on it too, for code outside the frame and the tick.
/// </summary>
public readonly struct ListView<T>(List<T> list) : IReadOnlyList<T> {
    public int Count => list.Count;
    public T this[int index] => list[index];
    public bool Contains(T item) => list.Contains(item);
    public T? Find(Predicate<T> match) => list.Find(match);
    public List<T>.Enumerator GetEnumerator() => list.GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => list.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => list.GetEnumerator();
}

/// <summary>An inventory to read but not change; <see cref="World.Transfer(InventoryView, Inventory, string, int)"/> takes one as an end of a move.</summary>
public readonly struct InventoryView(Inventory inventory) {
    internal Inventory Inventory => inventory;
    public int this[string item] => inventory[item];
    public int Count => inventory.Count;
    public int UsedSlots => inventory.UsedSlots;
    public int Slots => inventory.Slots;
    public ItemTotals Items => inventory.Items;
    public ListView<Inventory.Slot> Layout => new(inventory.Layout);
    public int StackOf(string item) => inventory.StackOf(item);
    public int Room(string item) => inventory.Room(item);
    public bool Has(Dictionary<string, int> cost) => inventory.Has(cost);
}
