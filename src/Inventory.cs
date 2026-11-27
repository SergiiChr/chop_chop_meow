using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// <summary>The units of a kind that expires, each with its own time, held units first and the rest soonest first; null for plain units.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<Item>? Units { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Held { get; set; }
        /// <summary>The id of the player holding <see cref="Held"/>, or 0.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int HeldBy { get; set; }
        /// <summary>In a backpack, the number (1–9) of the hotbar slot bound to this stack, or 0; it goes when the stack does, and follows it into a stack it merges with.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Hotbar { get; set; }
        /// <summary>Units in the cell that no player holds.</summary>
        [JsonIgnore] public int Free => Count - Held;
        /// <summary>Empties the cell, which ends its hotbar binding.</summary>
        public void Empty() { Item = ""; Count = 0; Hotbar = 0; Units = null; }
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
    public List<Slot> Layout {
        get {
            if (layout != null) return layout;
            layout = new List<Slot>(Slots);
            PadLayout();
            return layout;
        }
        set => layout = value;
    }
    // Most world things never hold anything. Their empty inventory does not need to make slots until used.
    private List<Slot>? layout;
    /// <summary>What workers bring here, in the order it was named, or <see cref="Everything"/> alone; see <see cref="Gated"/> for an empty one.</summary>
    public FilterList InputFilter { get; set; } = new();
    /// <summary>What workers take from here, in the order it was named, or <see cref="Everything"/> alone; see <see cref="Gated"/> for an empty one.</summary>
    public FilterList OutputFilter { get; set; } = new();
    /// <summary>
    /// Storage with a filter window, where an empty filter lets nothing through and <see cref="Everything"/> lets all.
    /// Any other box, such as a station's slots or a worker's hands, lets anything through an empty filter.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Gated { get; set; }
    /// <summary>The filter entry that names every item; it stands alone in its row.</summary>
    public const string Everything = "everything";
    /// <summary>A debug chest has room for as many stacks as are put in it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool UnlimitedSlots { get; set; }
    /// <summary>Number of cells. Changing it adds empty cells or moves stacks out of removed ones.</summary>
    [JsonIgnore] public int Slots { get => slots; set { if (value == slots) return; slots = value; RebuildStacks(); } }
    /// <summary>Most units of one item per cell. Changing it splits or merges stacks to fit.</summary>
    [JsonIgnore] public int StackSize { get => stackSize; set { if (value == stackSize) return; stackSize = value; RebuildStacks(); } }
    // Saves and updates carry the layout with its sizes. Applying them must not rebuild it or end its holds.
    [JsonInclude, JsonPropertyName(nameof(Slots)), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    internal int SavedSlots { get => slots; set => slots = value; }
    [JsonInclude, JsonPropertyName(nameof(StackSize)), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    internal int SavedStackSize { get => stackSize; set => stackSize = value; }
    /// <summary>
    /// Most coins per cell: 1000, or <see cref="UnlimitedCoins"/> in a trading post's wage slot.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int CoinStack { get; set; } = NewCoinStack;
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
    public Inventory() => Items = new ItemTotals(items);
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
    [JsonIgnore] public bool Fresh => items.Count == 0 && CarriedBy == Carrier.None && slots == NewSlots && stackSize == NewStackSize && CoinStack == NewCoinStack && Capacity == 0 && !UnlimitedSlots && !Gated && InputFilter.Count == 0 && OutputFilter.Count == 0 && (layout == null || layout.Count == NewSlots && UsedSlots == 0);

    /// <summary>Items that take a slot each in any container: every tool, filled from the data when it loads.</summary>
    public static readonly HashSet<string> Unstacked = new();
    /// <summary>Kinds whose units each keep the time they expire, filled from the data when it loads.</summary>
    public static readonly HashSet<string> Expiring = new();
    /// <summary>Whether a cell of this item keeps its units as objects: units that expire, or an object found by its key.</summary>
    private static bool HasUnits(string item) => Expiring.Contains(item) || ChopChopMeow.Item.IsObject(item);
    /// <summary>Coins use the container's coin stack, while packed crates, tools and lanterns each take a cell.</summary>
    public int StackOf(string item) => item == Coins ? CoinStack : Unstacked.Contains(item) || ChopChopMeow.Item.IsObject(item) ? 1 : StackSize;
    /// <summary>Units of a kind in this box, its objects included, such as every lantern.</summary>
    internal int CountOfKind(string kind) {
        int count = 0;
        foreach (var pair in items) if (ChopChopMeow.Item.IsKind(pair.Key, kind)) count += pair.Value;
        return count;
    }
    /// <summary>One of this box's items of a kind, its objects included, preferring one that is not held.</summary>
    internal string? FirstOfKind(string kind, bool free = false, bool withdrawal = false) {
        if (items.Count == 0) return null;
        string? held = null;
        foreach (var slot in Layout) {
            if (slot.Item == "" || !ChopChopMeow.Item.IsKind(slot.Item, kind) || withdrawal && !AllowsWithdrawal(slot.Item, slot.Units?[0])) continue;
            if (slot.Free > 0) return slot.Item;
            held ??= slot.Item;
        }
        return free ? null : held;
    }
    /// <summary>
    /// Units of an item that still fit in the one stack a station's input slot holds, as far as the box has room.
    /// Below zero when the box holds more than a stack of it.
    /// </summary>
    public int SlotRoom(string item) => Math.Min(Room(item), Math.Max(StackSize, StackOf(item)) - this[item]);
    /// <summary>Units of an item that still fit: empty slots plus the unfilled part of its last stack.</summary>
    public int Room(string item) => Room(item, replacing: false);

    private int Room(string item, bool replacing) {
        if (UnlimitedSlots) return int.MaxValue - Count;
        int stack = StackOf(item), room = 0, used = 0;
        for (int i = 0; i < Math.Min(Slots, Layout.Count); i++) {
            var slot = Layout[i];
            if (slot.Item == "") room += stack;
            else { used++; if (TopsUp(slot, item)) room += Math.Max(0, stack - slot.Count); }
        }
        room = Math.Max(0, room);
        if (Capacity > 0 && !replacing) room = Math.Min(room, Math.Max(0, Capacity - Count));
        return used > Slots ? 0 : Math.Min(room, CarryRoom(item));
    }

    /// <summary>Whether these goods, counted by item, all fit in this inventory at once: each item tops up its own stacks, and the rest takes empty slots.</summary>
    public bool Fits(Dictionary<string, int> goods) {
        if (UnlimitedSlots) {
            long extra = 0;
            foreach (int count in goods.Values) extra += count;
            return extra <= int.MaxValue - Count;
        }
        int limit = Math.Min(Slots, Layout.Count), empty = 0;
        for (int i = 0; i < limit; i++) if (Layout[i].Item == "") empty++;
        foreach (var (item, count) in goods) {
            if (count > CarryRoom(item)) return false;
            int stack = StackOf(item), spare = 0;
            for (int i = 0; i < limit; i++) if (TopsUp(Layout[i], item)) spare += stack - Layout[i].Count;
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
        if (layout == null && items.Count == 0) return;
        PadLayout();
        // Holds end, as after loading nobody holds anything.
        foreach (var s in Layout) {
            s.Held = 0; s.HeldBy = 0; s.Units?.Sort(Item.Soonest);
            if (s.Units != null) foreach (var unit in s.Units) unit?.Contents?.RebuildStacks();
        }
        heldUnits = 0;
        List<Item>? overflow = null;
        var remaining = layoutRemainder; remaining.Clear(); foreach (var pair in items) remaining[pair.Key] = pair.Value;
        for (int i = 0; i < Layout.Count; i++) {
            var s = Layout[i];
            if (s.Item == "") { s.Empty(); continue; }
            if (s.Count <= 0 || !remaining.TryGetValue(s.Item, out int qty)) { s.Empty(); continue; }
            s.Count = Math.Min(Math.Min(s.Count, qty), StackOf(s.Item)); remaining[s.Item] = qty - s.Count;
            if (s.Units is { } units && units.Count > s.Count) {
                overflow ??= new List<Item>();
                for (int n = s.Count; n < units.Count; n++) overflow.Add(units[n]);
                units.RemoveRange(s.Count, units.Count - s.Count);
            }
            if (s.Count == 0) s.Empty();
        }
        foreach (var (item, qty) in remaining) for (int left = qty; left > 0;) {
            int empty = -1;
            for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == "") { empty = i; break; }
            if (empty < 0) { Layout.Add(new Slot()); empty = Layout.Count - 1; }
            int n = Math.Min(StackOf(item), left);
            Layout[empty].Item = item; Layout[empty].Count = n;
            if (overflow != null) {
                int moved = 0;
                for (int u = 0; u < overflow.Count && moved < n;) {
                    if (overflow[u].Kind != item) { u++; continue; }
                    (Layout[empty].Units ??= new()).Add(overflow[u]); overflow.RemoveAt(u); moved++;
                }
                Layout[empty].Units?.Sort(Item.Soonest);
            }
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
        KeepSpareSlot();
    }

    private void PadLayout() { while (Layout.Count < Slots) Layout.Add(new Slot()); }

    /// <summary>Leaves a cell to drop the next stack into, without moving any stack already in the debug chest.</summary>
    private void KeepSpareSlot() {
        if (!UnlimitedSlots) return;
        if (Layout.Count == 0 || Layout[^1].Item != "") Layout.Add(new Slot());
        slots = Layout.Count;
    }

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
                ShiftUnits(a, b, moved);
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

    /// <summary>Lowers an item's total by <paramref name="count"/>, and drops it once none is left.</summary>
    private void Lower(string item, int count) {
        int left = this[item] - count;
        if (left <= 0) items.Remove(item); else items[item] = left;
    }

    private void SyncAggregate() {
        items.Clear(); foreach (var s in Layout) if (s.Item != "") items[s.Item] = items.GetValueOrDefault(s.Item) + s.Count;
        KeepSpareSlot();
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
                int n = Math.Min(StackOf(a.Item) - a.Count, b.Count);
                ShiftUnits(b, a, n);
                a.Count += n; b.Count -= n;
                // The stack merged in takes its hotbar slot along, as a stack moved onto another does.
                if (b.Count == 0) { if (b.Hotbar != 0) a.Hotbar = b.Hotbar; Layout.RemoveAt(i + 1); } else i++;
            } else i++;
        }
        PadLayout();
    }

    /// <summary>Lets an item in or out through a filter, or stops naming it there; <see cref="Everything"/> takes the row's items off, and an item takes it off.</summary>
    public void Filter(string item, bool output, bool allowed) {
        var filter = output ? OutputFilter : InputFilter;
        if (allowed && item == Everything) filter.Clear();
        else if (allowed) filter.Remove(Everything);
        if (allowed) filter.Add(item); else filter.Remove(item);
        Changed?.Invoke();
    }
    /// <summary>Empties both filters, so nothing goes in or out of storage.</summary>
    public void ClearFilters() { InputFilter.Clear(); OutputFilter.Clear(); Changed?.Invoke(); }
    /// <summary>Makes this box storage that takes nothing in and lets everything out, as a new chest does.</summary>
    public void GateNew() { Gated = true; InputFilter.Clear(); OutputFilter.Clear(); OutputFilter.Add(Everything); }
    /// <param name="unit">The object the item's key finds in the box it comes from, such as a packed crate, whose goods its filter entry depends on.</param>
    public bool Accepts(string item, Item? unit = null) => Passes(InputFilter, item, unit);
    public bool AllowsWithdrawal(string item) => LetsAll(OutputFilter) || Names(OutputFilter, item, Object(item));
    /// <param name="unit">The object the item's key finds in this box.</param>
    public bool AllowsWithdrawal(string item, Item? unit) => Passes(OutputFilter, item, unit);
    /// <summary>Whether the input filter asks for this item by name, so automation prefers this storage over one taking everything.</summary>
    /// <param name="unit">The object the item's key finds in the box it comes from.</param>
    public bool Wants(string item, Item? unit = null) => Names(InputFilter, item, unit);
    /// <summary>Whether a stack takes more of an item: a stack of it, unless its units expire and a player holds part of it, as the held ones must stay in front.</summary>
    private static bool TopsUp(Slot slot, string item) => slot.Item == item && (slot.Held == 0 || !HasUnits(item));
    /// <summary>Whether the input filter names any item, which a chest's hauler fills it with; <see cref="Everything"/> names none.</summary>
    public bool NamesItems => InputFilter.Count > 0 && !InputFilter.Contains(Everything);
    private bool Passes(FilterList filter, string item, Item? unit) => LetsAll(filter) || Names(filter, item, unit);
    /// <summary>
    /// Whether a filter lets every item through: <see cref="Everything"/> in storage, or an empty one elsewhere.
    /// </summary>
    private bool LetsAll(FilterList filter) => Gated ? filter.Contains(Everything) : filter.Count == 0;
    /// <summary>A filter names an item itself, the kind of an object that is always one, such as a lantern, or a packed crate of the good it holds.</summary>
    private static bool Names(FilterList filter, string item, Item? unit) {
        if (World.PackedGood(unit) is string good) {
            foreach (string named in filter) if (World.NamesCrateOf(named, good)) return true;
            return false;
        }
        // A container holding a liquid goes under its entry for that liquid at any fill, never under its own, which takes it empty.
        if (World.Holding(unit) is var (container, liquid)) {
            foreach (string named in filter) if (World.NamesHolding(named, container, liquid)) return true;
            return false;
        }
        // Only an object that is always one, such as a lantern, goes under its kind: a container holding something has no entry of its own.
        return filter.Contains(item) || ChopChopMeow.Item.IsKey(item) && filter.Contains(ChopChopMeow.Item.KindOf(item)) && ChopChopMeow.Item.IsObject(ChopChopMeow.Item.KindOf(item));
    }

    /// <summary>Adds up to <paramref name="quantity"/> units of an item and returns how many fit.</summary>
    /// <exception cref="ArgumentException">For units that are objects of their own: add those with <see cref="AddUnits"/>, <see cref="Add(ChopChopMeow.Item)"/> or <see cref="TransferTo"/>.</exception>
    public int Add(string item, int quantity) =>
        HasUnits(item) ? throw new ArgumentException($"Each {item} is an object of its own: add them as units.", nameof(item)) : Add(item, quantity, default);

    /// <summary>Adds an object found by its key, such as a lantern, and returns whether it fit.</summary>
    internal bool Add(Item unit) => Add(unit.Key, 1, new ReadOnlySpan<Item>(ref unit)) == 1;

    /// <summary>The object a cell holding this key keeps, or null for a plain item or one not here.</summary>
    internal Item? Object(string key) {
        if (!ChopChopMeow.Item.IsKey(key) || !items.ContainsKey(key)) return null;
        foreach (var slot in Layout) if (slot.Item == key) return slot.Units![0];
        return null;
    }

    /// <summary>
    /// Turns the cell of an object that holds nothing now, such as an unpacked crate, into a plain unit of its kind,
    /// keeping the cell and its hold; returns whether it did.
    /// </summary>
    internal bool Unpack(string key) {
        foreach (var slot in Layout) if (slot.Item == key && slot.Units is [{ Emptied: true }]) { MakePlain(slot); Changed?.Invoke(); return true; }
        return false;
    }

    /// <summary>
    /// Turns an emptied object's cell into a plain unit of its kind; the object keeps the cell's list for the next cell
    /// that takes it.
    /// </summary>
    private void MakePlain(Slot slot) {
        var units = slot.Units!;
        var unit = units[0];
        Lower(slot.Item, 1);
        items[unit.Kind] = this[unit.Kind] + 1;
        slot.Item = unit.Kind; slot.Units = null;
        units.Clear();
        unit.Cell = units;
    }

    /// <summary>Takes out the object found by this key, unless a player holds it.</summary>
    internal Item? TakeObject(string key) {
        Span<Item> taken = [null!];
        return TakeUnits(key, 1, taken, true) == 1 ? taken[0] : null;
    }

    /// <summary>Adds units of one kind that expires, each keeping its own time, and returns how many fit.</summary>
    internal int AddUnits(ReadOnlySpan<Item> units) => units.IsEmpty ? 0 : Add(units[0].Kind, units.Length, units);

    private int Add(string item, int quantity, ReadOnlySpan<Item> units) {
        int amount = Math.Min(Math.Max(0, quantity), Room(item));
        if (amount > 0) {
            items[item] = this[item] + amount; var left = amount;
            int limit = Math.Min(Slots, Layout.Count);
            int size = StackOf(item);
            for (int i = 0; i < limit && left > 0; i++) {
                var slot = Layout[i];
                if (!TopsUp(slot, item) || slot.Count >= size) continue;
                int n = Math.Min(left, size - slot.Count);
                if (!units.IsEmpty) AddUnitsTo(slot, units.Slice(amount - left, n));
                slot.Count += n; left -= n;
            }
            for (int i = 0; left > 0 && (i < limit || UnlimitedSlots); i++) {
                if (i == Layout.Count) Layout.Add(new Slot());
                var slot = Layout[i];
                if (slot.Item != "") continue;
                int n = Math.Min(left, size);
                if (!units.IsEmpty) AddUnitsTo(slot, units.Slice(amount - left, n));
                slot.Item = item; slot.Count = n; left -= n;
            }
            KeepSpareSlot();
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
        if (HasUnits(item)) return TakeUnits(item, quantity, default, spent);
        int amount = Math.Min(Math.Max(0, quantity), Free(item));
        Lower(item, amount);
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
        if (HasUnits(oldItem) || HasUnits(newItem)) return false;
        int slotIndex = (uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == oldItem && Layout[PreferredSlot].Held == 0 ? PreferredSlot : -1;
        if (slotIndex < 0) for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == oldItem && Layout[i].Held == 0) { slotIndex = i; break; }
        if (slotIndex < 0) return false;
        Slot slot = Layout[slotIndex]; int quantity = slot.Count;
        if (quantity > StackOf(newItem)) return false;
        slot.Item = newItem;
        Lower(oldItem, quantity);
        items[newItem] = items.GetValueOrDefault(newItem) + quantity;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// The units of an empty bottle or bucket that could each become a filled one, an object taking a cell of its own.
    /// </summary>
    internal int RoomForFilled(string empty) {
        if (StackOf(empty) == 1) return Free(empty);
        int room = Room(AnyFilled, replacing: true), count = 0;
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

    /// <summary>
    /// A filled bottle's or bucket's key as a check of room sees it: an object that takes a cell of its own.
    /// </summary>
    private static readonly string AnyFilled = ChopChopMeow.Item.KeyOf(Bucket.Item, 0);

    /// <summary>Whether the rest of a cell's stack fits in other cells, as when one unit of it becomes an object in that cell.</summary>
    internal bool RoomForRest(int at) {
        if ((uint)at >= (uint)Layout.Count || Layout[at] is not { Item: not "" } slot) return false;
        return slot.Count == 1 || Room(slot.Item, replacing: true) - (StackOf(slot.Item) - slot.Count) >= slot.Count - 1;
    }

    /// <summary>
    /// Turns one unit of an empty container, from <see cref="PreferredSlot"/> first, into an object holding something.
    /// A unit alone in its cell becomes the object there, keeping its hotbar slot; one from a stack goes to a free cell.
    /// </summary>
    /// <param name="cell">
    /// The cell whose unit fills, or -1 for any: the object takes that cell and the rest of its stack moves to others.
    /// </param>
    internal bool FillOne(string empty, Item filled, int cell = -1) {
        bool keepCell = cell >= 0;
        int at = -1;
        // A unit that changes in its own cell is not taken out, so a hotbar binding does not keep it from filling there.
        if (keepCell) at = cell < Layout.Count && Layout[cell].Item == empty && Layout[cell].Free > 0 ? cell : -1;
        else {
            if ((uint)PreferredSlot < (uint)Layout.Count && Layout[PreferredSlot].Item == empty && Takeable(Layout[PreferredSlot]) > 0) at = PreferredSlot;
            if (at < 0) for (int i = 0; i < Layout.Count; i++) if (Layout[i].Item == empty && Takeable(Layout[i]) > 0 && (at < 0 || Layout[i].Count == 1)) at = i;
        }
        if (at < 0) return false;
        var slot = Layout[at];
        int rest = slot.Count - 1;
        if (rest > 0 && !(keepCell ? RoomForRest(at) : Room(filled.Key, replacing: true) > 0)) return false;
        if (rest > 0 && !keepCell) {
            Take(empty, 1, false);
            return Add(filled);
        }
        Lower(empty, slot.Count);
        slot.Item = filled.Key; slot.Count = 1; slot.Units = filled.TakeCell();
        slot.Units.Add(filled);
        items[filled.Key] = 1;
        if (rest > 0) Add(empty, rest);
        else Changed?.Invoke();
        return true;
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
        if (automated && !(LetsAll(OutputFilter) && destination.LetsAll(destination.InputFilter))) {
            var unit = Object(item);
            if (!AllowsWithdrawal(item, unit) || !destination.Accepts(item, unit)) return 0;
        }
        int amount = Math.Min(Free(item), Math.Min(Math.Max(0, quantity), destination.Room(item)));
        if (HasUnits(item)) {
            var units = ArrayPool<Item>.Shared.Rent(amount);
            try {
                int taken = TakeUnits(item, amount, units, false);
                return destination.Add(item, taken, units.AsSpan(0, taken));
            } finally { Array.Clear(units); ArrayPool<Item>.Shared.Return(units); }
        }
        Take(item, amount, false);
        return destination.Add(item, amount);
    }

    /// <summary>The average time the units in one cell expire, or 0 for a cell of plain units.</summary>
    internal float AverageExpiry(int slot) =>
        (uint)slot < (uint)Layout.Count && Layout[slot].Units is { } units ? AverageExpiry(CollectionsMarshal.AsSpan(units)) : 0;

    /// <summary>The average time this box's units of a kind expire, such as a station's one input, or 0 for none.</summary>
    internal float AverageExpiry(string kind) {
        double total = 0;
        int count = 0;
        foreach (var slot in Layout) if (slot.Item == kind && slot.Units is { } units) {
            foreach (var unit in units) total += unit.Expires;
            count += units.Count;
        }
        return count == 0 ? 0 : (float)(total / count);
    }

    /// <summary>The average time some units expire, or 0 for none.</summary>
    internal static float AverageExpiry(ReadOnlySpan<Item> units) {
        double total = 0;
        foreach (var unit in units) total += unit.Expires;
        return units.IsEmpty ? 0 : (float)(total / units.Length);
    }

    /// <summary>Takes out the soonest-to-expire free units of a kind into <paramref name="taken"/>, starting with the slot the player clicked.</summary>
    private int TakeUnits(string kind, int quantity, Span<Item> taken, bool spent) {
        if (quantity <= 0 || this[kind] == 0) return 0;
        int removed = 0;
        if ((uint)PreferredSlot < (uint)Layout.Count) {
            var preferred = Layout[PreferredSlot];
            if (preferred.Item == kind) TakeUnitsFrom(preferred, Math.Min(quantity, Takeable(preferred)), taken, ref removed, spent);
        }
        while (removed < quantity) {
            Slot? soonest = null;
            float expires = float.PositiveInfinity;
            foreach (var slot in Layout) {
                if (slot.Item != kind || Takeable(slot) <= 0 || slot.Units![slot.Held].Expires >= expires) continue;
                soonest = slot;
                expires = slot.Units[slot.Held].Expires;
            }
            if (soonest == null) break;
            TakeUnitsFrom(soonest, 1, taken, ref removed, spent);
        }
        if (removed > 0) {
            Lower(kind, removed);
            Changed?.Invoke();
        }
        return removed;
    }

    private void TakeUnitsFrom(Slot slot, int count, Span<Item> taken, ref int removed, bool spent) {
        if (count == 0) return;
        var units = slot.Units!;
        if (!taken.IsEmpty) for (int i = 0; i < count; i++) taken[removed + i] = units[slot.Held + i];
        units.RemoveRange(slot.Held, count);
        removed += count;
        RemoveFromSlot(slot, count, spent);
    }

    private static void AddUnitsTo(Slot slot, ReadOnlySpan<Item> added) {
        if (added.IsEmpty) return;
        var units = slot.Units ??= added[0].TakeCell();
        foreach (var unit in added) units.Add(unit);
        // The held units are reserved at the front. Anything that joins the stack while held sorts behind them.
        units.Sort(slot.Held, units.Count - slot.Held, Item.Soonest);
    }

    /// <summary>Moves the soonest-to-expire units of an unheld stack when two cells merge.</summary>
    private static void ShiftUnits(Slot from, Slot to, int count) {
        if (from.Units is not { } units) return;
        AddUnitsTo(to, CollectionsMarshal.AsSpan(units)[..count]);
        units.RemoveRange(0, count);
    }

    /// <summary>Removes the units that have expired, those inside the objects here included, returning the next time any in this inventory does.</summary>
    internal float Expire(float now) {
        if (items.Count == 0) return float.PositiveInfinity;
        bool changed = false;
        float next = float.PositiveInfinity;
        foreach (var slot in Layout) {
            if (slot.Units is not { } units) continue;
            int keep = 0, held = 0;
            for (int i = 0; i < units.Count; i++) {
                var unit = units[i];
                if (unit.Contents != null) next = Math.Min(next, unit.Contents.Expire(now));
                if (unit.Expires != 0 && unit.Expires <= now) continue;
                units[keep++] = unit;
                if (i < slot.Held) held++;
                if (unit.Expires != 0) next = Math.Min(next, unit.Expires);
            }
            int gone = units.Count - keep;
            if (gone == 0) {
                if (units is [{ Emptied: true }]) { MakePlain(slot); changed = true; }
                continue;
            }
            heldUnits -= slot.Held - held;
            slot.Held = held;
            if (held == 0) slot.HeldBy = 0;
            units.RemoveRange(keep, gone);
            slot.Count -= gone;
            Lower(slot.Item, gone);
            changed = true;
            if (slot.Count == 0) {
                if (slot.Hotbar != 0) (UsedUp ??= new()).Add((slot.Hotbar, slot.Item));
                slot.Empty();
            }
        }
        if (changed) Changed?.Invoke();
        return next;
    }

    /// <summary>
    /// Every saved unit of a kind that expires has one valid, finite time in its own stack, every object sits alone in the
    /// cell its key names with valid contents, and no plain stack keeps units.
    /// </summary>
    internal bool ValidUnits(Func<Item, bool> validObject) {
        int kept = 0;
        foreach (var (item, count) in items) if (HasUnits(item)) kept += count;
        if (layout == null) return kept == 0;
        int found = 0;
        foreach (var slot in Layout) {
            if (slot.Item == "" || !HasUnits(slot.Item)) { if (slot.Units != null) return false; continue; }
            if (slot.Units is not { } units || units.Count != slot.Count) return false;
            if (ChopChopMeow.Item.IsKey(slot.Item)) {
                if (slot.Count != 1 || units[0] is not { } unit || unit.Key != slot.Item || unit.Expires != 0 || !validObject(unit)) return false;
            } else foreach (var unit in units) if (unit == null || unit.Kind != slot.Item || unit.Id != 0 || unit.Contents != null || !float.IsFinite(unit.Expires) || unit.Expires <= 0) return false;
            found += slot.Count;
        }
        return found == kept;
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
