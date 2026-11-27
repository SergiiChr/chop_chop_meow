using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Liquids a station holds, in liters: poured in from vessels, which stay with whoever carried them, emptier,
/// and taken out only into vessels with room for them.
/// </summary>
public sealed partial class World {
    /// <summary>
    /// Sets a container's fill in its cell: the object a key finds, or one unit of a plain empty container, which becomes an
    /// object in its own cell, or in a free one when it came from a stack; emptied, it turns back into a plain container.
    /// Returns the item it is now, or null when no cell had room.
    /// </summary>
    /// <param name="keepCell">A unit from a stack becomes the object in that cell, and the rest of the stack moves to others.</param>
    internal string? SetFill(Inventory box, string item, string liquid, int liters, bool keepCell = false) {
        string container = Item.KindOf(item);
        if (box.Object(item) is { } unit) {
            unit.Contents!.Clear();
            if (liters <= 0) {
                box.Unpack(item);
                emptied.Push(unit);
                return container;
            }
            unit.Contents.Add(liquid, liters);
            return item;
        }
        if (liters <= 0) return item;
        int capacity = container == Barrel.Item ? Barrel.Capacity : Data.VesselOf(container, liquid)!.Capacity;
        var filled = Reused(container, capacity) ?? Vessels.Holding(container, S.NextId++, capacity, liquid, 0);
        filled.Contents!.Add(liquid, liters);
        if (box.FillOne(container, filled, keepCell)) return filled.Key;
        filled.Contents.Clear();
        emptied.Push(filled);
        return null;
    }

    /// <summary>
    /// Vessels emptied since the world loaded, which nothing holds any more: filling one again reuses it, number and all, so a
    /// bottle going round the lake and the cooking table makes nothing new once each has been filled.
    /// </summary>
    private readonly Stack<Item> emptied = new();

    /// <summary>An emptied vessel of this kind and capacity to fill again, or null.</summary>
    private Item? Reused(string container, int capacity) {
        foreach (var unit in emptied)
            if (unit.Kind == container && unit.Contents!.Capacity == capacity) {
                // A stack pops only its top, so the one found is taken out by moving the others aside.
                while (emptied.Peek() != unit) reshelved.Push(emptied.Pop());
                emptied.Pop();
                while (reshelved.Count > 0) emptied.Push(reshelved.Pop());
                return unit;
            }
        return null;
    }
    private readonly Stack<Item> reshelved = new();

    /// <summary>A full vessel standing for any of its kind, kept to ask where one would go without making a new one; it lies in no box.</summary>
    internal Item FullVessel(VesselDef v) {
        if (!fullVessels.TryGetValue(v, out var full)) fullVessels[v] = full = Vessels.Holding(v.Empty, 0, v.Capacity, v.Liquid, v.Capacity);
        return full;
    }
    private readonly Dictionary<VesselDef, Item> fullVessels = new();

    /// <summary>Whether a cell holds a container like this one: of its kind, holding as many liters of the same liquid, and free to take.</summary>
    private static bool Alike(Inventory.Slot slot, string container, string liquid, int liters) =>
        slot.Free > 0 && slot.Units is [{ } unit] && unit.Kind == container && Vessels.LiquidIn(unit) == liquid && Vessels.LitersIn(unit) == liters;

    /// <summary>The free units of an item a load can take together: for a container holding a liquid, every one like it in the box, as one stack of them was.</summary>
    internal static int FreeLike(Inventory box, string item) {
        var unit = box.Object(item);
        if (Holding(unit) is not var (container, liquid)) return box.Free(item);
        int liters = Vessels.LitersIn(unit), count = 0;
        foreach (var slot in box.Layout) if (Alike(slot, container, liquid, liters)) count++;
        return count;
    }

    /// <summary>Moves up to <paramref name="count"/> of an item, and for a container holding a liquid the ones like it in the box after it, as one stack of them moved.</summary>
    internal int TransferLike(Inventory from, Inventory to, string item, int count, bool automated) {
        var unit = from.Object(item);
        if (Holding(unit) is not var (container, liquid)) return from.TransferTo(to, item, count, automated);
        int liters = Vessels.LitersIn(unit), moved = from.TransferTo(to, item, 1, automated);
        if (moved == 0) return 0;
        alike.Clear();
        foreach (var slot in from.Layout) if (Alike(slot, container, liquid, liters)) alike.Add(slot.Item);
        foreach (string other in alike) if (moved < count) moved += from.TransferTo(to, other, 1, automated);
        return moved;
    }
    private readonly List<string> alike = new();

    /// <summary>Whether an item in a box is food: food itself, or a vessel holding a liquid eaten from it, such as a bottle of jam.</summary>
    internal bool IsFood(Inventory box, string item) => Data.IsFood(item) || FoodVessel(item, box.Object(item));
    /// <summary>Whether an object is a vessel holding a liquid eaten from it, such as a bottle of jam.</summary>
    private bool FoodVessel(string item, Item? unit) => Vessels.LiquidIn(unit) is string liquid && Data.VesselOf(item, liquid)?.Hunger > 0;

    /// <summary>Spends items, leaving each refillable vessel empty in its cell.</summary>
    private int SpendContents(Inventory box, string item, int count) {
        if (box.Object(item) is not { Contents: not null } vessel || Data.VesselOf(item) == null) return box.Remove(item, count);
        return SetFill(box, item, Vessels.LiquidIn(vessel)!, 0) != null ? 1 : 0;
    }

    /// <summary>Pours what one vessel from a box holds into another box, while room and <paramref name="liters"/> last; it stays in its cell, emptier. Returns whether it poured.</summary>
    private int PourVessel(Inventory from, string item, Inventory to, VesselDef v, int liters) {
        int held = Vessels.LitersIn(from.Object(item)), pour = Math.Min(held, Math.Min(liters, to.Room(v.Liquid)));
        if (pour <= 0 || from.Free(item) == 0 || SetFill(from, item, v.Liquid, held - pour) == null) return 0;
        to.Add(v.Liquid, pour);
        return 1;
    }

    /// <summary>A box's vessels holding a liquid, least full first, into <paramref name="found"/>.</summary>
    private static void HoldersOf(Inventory box, VesselDef v, List<(string Item, int Liters)> found) {
        found.Clear();
        foreach (var slot in box.Layout)
            if (slot.Units is [{ Contents: not null } unit] && unit.Kind == v.Empty && Vessels.LiquidIn(unit) == v.Liquid && slot.Free > 0) found.Add((slot.Item, Vessels.LitersIn(unit)));
        found.Sort(LeastFull);
    }
    private static readonly Comparer<(string Item, int Liters)> LeastFull = Comparer<(string Item, int Liters)>.Create((a, b) => a.Liters.CompareTo(b.Liters));
    private readonly List<(string Item, int Liters)> holders = new();

    /// <summary>Pours up to <paramref name="liters"/> of a liquid from one box's vessels into another, least full first so part-drunk ones empty; returns the liters poured.</summary>
    private int PourLiters(Inventory from, Inventory to, VesselDef v, int liters) {
        int poured = 0;
        HoldersOf(from, v, holders);
        foreach (var (item, held) in holders) {
            if (poured >= liters || to.Room(v.Liquid) == 0) break;
            int pour = Math.Min(Math.Min(held, liters - poured), to.Room(v.Liquid));
            if (SetFill(from, item, v.Liquid, held - pour) == null) break;
            to.Add(v.Liquid, pour); poured += pour;
        }
        return poured;
    }

    /// <summary>Fills one box's vessels with up to <paramref name="liters"/> of the liquid another holds, fullest first so they fill up, then empty ones; returns the liters filled.</summary>
    private int Fill(Inventory from, Inventory to, VesselDef v, int liters) {
        int filled = 0;
        HoldersOf(to, v, holders);
        for (int i = holders.Count - 1; i >= 0 && filled < liters && from.Free(v.Liquid) > 0; i--) {
            var (item, held) = holders[i];
            int add = Math.Min(Math.Min(v.Capacity - held, liters - filled), from.Free(v.Liquid));
            if (add <= 0 || SetFill(to, item, v.Liquid, held + add) == null) continue;
            from.Remove(v.Liquid, add); filled += add;
        }
        while (filled < liters && from.Free(v.Liquid) > 0 && to.Free(v.Empty) > 0) {
            int add = Math.Min(Math.Min(v.Capacity, liters - filled), from.Free(v.Liquid));
            if (SetFill(to, v.Empty, v.Liquid, add) == null) break;
            from.Remove(v.Liquid, add); filled += add;
        }
        return filled;
    }

    /// <summary>The liters of a liquid a box's vessels have room for.</summary>
    private int VesselRoom(Inventory box, VesselDef v) {
        int room = box.RoomForFilled(v.Empty) * v.Capacity;
        HoldersOf(box, v, holders);
        foreach (var (_, held) in holders) room += v.Capacity - held;
        return room;
    }

    /// <summary>
    /// The vessel whose liquid an item pours into a box, or null: the item holds some, and the box is a station's input slots
    /// and its recipe takes that liquid.
    /// </summary>
    /// <param name="unit">The object the item's key finds in the box it comes from.</param>
    internal VesselDef? PoursInto(Thing? t, Inventory to, string item, Item? unit) =>
        t != null && to == t.Inventory && Vessels.LiquidIn(unit) is string liquid && Data.VesselOf(item, liquid) is VesselDef v && TakesLiquid(t, v.Liquid) ? v : null;

    private bool TakesLiquid(Thing t, string liquid) =>
        t.Kind == ThingKind.Barrel ? Barrels.CanHold(t, liquid)
        : t.Kind == ThingKind.Farm || TreePlanting.NeedsWater(t) ? liquid == Water.Liquid : Pending(t, liquid);

    /// <summary>
    /// Moves items between two boxes, by hand or by a worker; <paramref name="quantity"/> counts liters of a liquid and vessels of anything else.
    /// Every move of a liquid goes through here.
    /// A liquid fills vessels in the box it goes to, unless that box is a station that takes it: there it comes as it is, or poured from the vessels that hold it.
    /// A vessel dropped into a station that takes its liquid pours what it holds there and stays behind, emptier.
    /// </summary>
    /// <param name="into">The thing that owns <paramref name="to"/>, or null for the backpack or a worker's bag.</param>
    /// <param name="room">Most units, or liters of a liquid, that may land in <paramref name="to"/>, such as what a station's input slot has left.</param>
    private int Carry(Inventory from, Inventory to, Thing? into, string item, int quantity, int room = int.MaxValue) {
        if (into?.Kind == ThingKind.Farm && to == into.Inventory)
            room = Math.Min(room, Barrels.CanPourWater(Data, item, from.Object(item)) ? Farms.WaterRoom(into) : to.SlotRoom(item));
        if (into?.Kind == ThingKind.Packer && to == into.Inventory) room = Math.Min(room, PackingRoom(into, item));
        // Each of a station's slots holds one stack, its output slot too; a vessel pours into the stack of its liquid.
        if (into != null && IsStation(into.Kind) && to == into.Inventory) room = Math.Min(room, to.SlotRoom(PoursInto(into, to, item, from.Object(item))?.Liquid ?? item));
        if (Data.VesselFor(item) != null) {
            bool takes = into != null && to == into.Inventory && TakesLiquid(into, item);
            if (takes && from[item] > 0) return from.TransferTo(to, item, Math.Min(quantity, room));
            int moved = 0;
            foreach (var vessel in Data.Vessels) {
                if (vessel.Liquid != item) continue;
                moved += takes ? PourLiters(from, to, vessel, Math.Min(quantity, room) - moved) : Fill(from, to, vessel, quantity - moved);
                if (moved >= quantity || takes && moved >= room) break;
            }
            if (!takes && moved > 0) Got(item);
            return moved;
        }
        if (PoursInto(into, to, item, from.Object(item)) is VesselDef poured) return PourVessel(from, item, to, poured, room);
        return from.TransferTo(to, item, Math.Min(quantity, room));
    }

    /// <summary>Why a liquid cannot leave a box for the backpack or another box now, or null: it goes only into vessels there with room for it.</summary>
    public string? FillBlock(Inventory from, Inventory to, string item, Thing? target = null) =>
        Data.VesselFor(item) is VesselDef v && from[item] > 0 && (target == null || to != target.Inventory || !TakesLiquid(target, item))
        && LiquidVesselRoom(to, item) == 0 ? NeedVessel(v) : null;

    private int LiquidVesselRoom(Inventory to, string liquid) {
        int room = 0;
        foreach (var vessel in Data.Vessels) if (vessel.Liquid == liquid) room += VesselRoom(to, vessel);
        return room;
    }

    /// <summary>The refusal when a vessel's liquid has nowhere to go, such as "You need an empty water vessel."</summary>
    private string NeedVessel(VesselDef v) => $"You need an empty {Lower(Data.Name(v.Liquid))} vessel.";

    /// <summary>Why the backpack's vessels cannot take every liquid a box holds, or null.</summary>
    private string? NoVesselRoom(Dictionary<string, int> goods) {
        foreach (var v in Data.Vessels) if (goods.GetValueOrDefault(v.Liquid) > LiquidVesselRoom(S.Backpack, v.Liquid)) return NeedVessel(v);
        return null;
    }

    /// <summary>How many of a recipe input the backpack holds: a liquid counts the liters in its vessels.</summary>
    public int Carried(string input) {
        if (Data.VesselFor(input) == null) return S.Backpack[input];
        int liters = 0;
        foreach (var slot in S.Backpack.Layout) if (slot.Units is [{ Contents: not null } unit] && Vessels.LiquidIn(unit) == input && Data.VesselOf(unit.Kind) != null) liters += Vessels.LitersIn(unit);
        return liters;
    }

    /// <summary>Whether the backpack holds a batch's inputs, a liquid in its vessels.</summary>
    public bool CarriesBatch(Dictionary<string, int> cost) {
        foreach (var (item, needed) in cost) if (Carried(item) < needed) return false;
        return true;
    }

    /// <summary>Fills the backpack's vessels from every liquid a box holds; what finds no room is spilled, and said, such as " 2L of water spilled."</summary>
    internal string PourOut(Inventory box) {
        string spilled = "";
        foreach (var v in Data.Vessels) {
            if (box[v.Liquid] == 0) continue;
            Carry(box, S.Backpack, null, v.Liquid, box[v.Liquid]);
            if (box.Remove(v.Liquid, box[v.Liquid]) is > 0 and int lost) spilled += $" {lost}L of {Lower(Data.Name(v.Liquid))} spilled.";
        }
        return spilled;
    }

    /// <summary>The nearest storage with vessels that hold a liquid, fullest first, and that fill; or null.</summary>
    internal (string Item, Thing Source)? LiquidSource(VesselDef v, Vector2 from) => NearestFill(v, v.Capacity, 1, from);
    /// <summary>The liters of a vessel's liquid an item holds: 0 for its empty container, -1 for anything else.</summary>
    private static int FillOf(Inventory box, string item, VesselDef v) =>
        item == v.Empty ? 0 : box.Object(item) is { Contents: not null } unit && unit.Kind == v.Empty && Vessels.LiquidIn(unit) == v.Liquid ? Vessels.LitersIn(unit) : -1;

    /// <summary>
    /// The first of a vessel's fills, from <paramref name="first"/> liters toward <paramref name="last"/>, that any storage or station gives out, and the nearest place that does.
    /// One pass over them, where <see cref="SourceFor"/> would take one for each fill.
    /// </summary>
    internal (string Item, Thing Source)? NearestFill(VesselDef v, int first, int last, Vector2 origin) {
        int step = last >= first ? 1 : -1, ranks = Math.Abs(last - first), rank = ranks + 1;
        Thing? best = null; string? bestItem = null; float nearest = 0;
        foreach (var kind in Stores) Nearer(kind, false);
        foreach (var kind in stationKinds) Nearer(kind, true);
        return best == null || bestItem == null ? null : (bestItem, best);

        void Nearer(ThingKind kind, bool station) {
            foreach (var t in S.Things.Of(kind))
                foreach (var (item, _) in t.Inventory.Items) {
                    int liters = FillOf(t.Inventory, item, v), r = (liters - first) * step;
                    if (liters < 0 || r < 0 || r > ranks || r > rank || t.Inventory.Free(item) <= 0 || !t.Inventory.AllowsWithdrawal(item) || station && Pending(t, item)) continue;
                    float distance = t.Pos.DistanceSquaredTo(origin);
                    if (r < rank || distance < nearest) { best = t; bestItem = item; rank = r; nearest = distance; }
                }
        }
    }

    /// <summary>Whether a vessel from <paramref name="box"/> can take more of a station's leftover liquid there.</summary>
    internal bool CanDrain(Thing place, Inventory box, string item, string liquid) {
        if (!IsStation(place.Kind) || place.Inventory.Free(liquid) == 0 || Pending(place, liquid)) return false;
        foreach (var vessel in Data.Vessels)
            if (vessel.Liquid == liquid && FillOf(box, item, vessel) is >= 0 and int held && held < vessel.Capacity) return true;
        return false;
    }

    /// <summary>A hauler's vessels with room at a station that holds leftover liquid for them: filled there, then stored like any load.</summary>
    private bool FillsLeftover(Worker w, Thing? dest, string item, float dt) {
        if (w.DrainingLiquid is not string liquid) return false;
        if (dest == null || !CanDrain(dest, w.Bag, item, liquid)) { w.DrainingLiquid = null; return false; }
        w.Status = Phrase("Taking ", Lower(Data.Name(liquid)), " out");
        if (Walk(w, dest.Pos, dt)) {
            Carry(dest.Inventory, w.Bag, null, liquid, dest.Inventory[liquid]);
            w.DrainingLiquid = null;
            w.Destination = 0;
        }
        return true;
    }
}
