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
    /// Pours up to <paramref name="vessels"/> vessels of one fill from one box into another, each all it holds while room and <paramref name="liters"/> last.
    /// Each vessel stays in its own cell, emptier; returns how many vessels poured.
    /// </summary>
    private static int PourVessels(Inventory from, string item, Inventory to, VesselDef v, int vessels, int liters) {
        int held = v.LitersIn(item), poured = 0;
        liters = Math.Min(liters, to.Room(v.Liquid));
        for (; poured < vessels && from.Free(item) > 0 && liters > 0; poured++) {
            int pour = Math.Min(held, liters);
            if (!from.ReplaceOne(item, v.States[held - pour])) break;
            to.Add(v.Liquid, pour); liters -= pour;
        }
        return poured;
    }

    /// <summary>Pours up to <paramref name="liters"/> of a liquid from one box's vessels into another, least full first so part-drunk ones empty; returns the liters poured.</summary>
    private static int PourLiters(Inventory from, Inventory to, VesselDef v, int liters) {
        int poured = 0;
        for (int held = 1; held <= v.Capacity; held++)
            while (from.Free(v.States[held]) > 0 && poured < liters && to.Room(v.Liquid) > 0) {
                int pour = Math.Min(Math.Min(held, liters - poured), to.Room(v.Liquid));
                if (!from.ReplaceOne(v.States[held], v.States[held - pour])) break;
                to.Add(v.Liquid, pour); poured += pour;
            }
        return poured;
    }

    /// <summary>Fills one box's vessels with up to <paramref name="liters"/> of the liquid another holds, fullest first so they fill up; returns the liters filled.</summary>
    private static int Fill(Inventory from, Inventory to, VesselDef v, int liters) {
        int filled = 0;
        // A vessel topped up only part way is fuller than every one still to come, so each is filled once.
        for (int held = v.Capacity - 1; held >= 0; held--)
            while (to.Free(v.States[held]) > 0 && filled < liters && from.Free(v.Liquid) > 0) {
                int add = Math.Min(Math.Min(v.Capacity - held, liters - filled), from.Free(v.Liquid));
                if (!to.ReplaceOne(v.States[held], v.States[held + add])) break;
                from.Remove(v.Liquid, add); filled += add;
            }
        return filled;
    }

    /// <summary>The liters of a liquid a box's vessels have room for.</summary>
    private static int VesselRoom(Inventory box, VesselDef v) {
        int room = box.RoomForFilled(v.Empty, v.Full) * v.Capacity;
        for (int held = 1; held < v.Capacity; held++) room += box.Free(v.States[held]) * (v.Capacity - held);
        return room;
    }

    /// <summary>The vessel whose liquid an item pours into a box, or null: the item holds some, and the box is a station's input slots and its recipe takes that liquid.</summary>
    internal VesselDef? PoursInto(Thing? t, Inventory to, string item) =>
        t != null && to == t.Inventory && Data.VesselOf(item) is VesselDef v && v.LitersIn(item) > 0 && TakesLiquid(t, v.Liquid) ? v : null;

    private bool TakesLiquid(Thing t, string liquid) =>
        t.Kind is ThingKind.Barrel or ThingKind.Farm || TreePlanting.NeedsWater(t) ? liquid == Water.Liquid : Pending(t, liquid);

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
            room = Math.Min(room, Barrels.CanPour(Data, item) ? Farms.WaterRoom(into) : to.SlotRoom(item));
        if (into?.Kind == ThingKind.Packer && to == into.Inventory) room = Math.Min(room, PackingRoom(into, item));
        // Each of a station's slots holds one stack, its output slot too; a vessel pours into the stack of its liquid.
        if (into != null && IsStation(into.Kind) && to == into.Inventory) room = Math.Min(room, to.SlotRoom(PoursInto(into, to, item)?.Liquid ?? item));
        if (Data.VesselFor(item) != null) {
            bool takes = into != null && to == into.Inventory && TakesLiquid(into, item);
            if (takes && from[item] > 0) return from.TransferTo(to, item, Math.Min(quantity, room));
            int moved = 0;
            foreach (var vessel in Data.Vessels) {
                if (vessel.Liquid != item) continue;
                moved += takes ? PourLiters(from, to, vessel, Math.Min(quantity, room) - moved) : Fill(from, to, vessel, quantity - moved);
                if (moved >= quantity || takes && moved >= room) break;
            }
            return moved;
        }
        if (PoursInto(into, to, item) is VesselDef poured) return PourVessels(from, item, to, poured, quantity, room);
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
        foreach (var vessel in Data.Vessels) {
            if (vessel.Liquid != input) continue;
            for (int held = 1; held <= vessel.Capacity; held++) liters += S.Backpack[vessel.States[held]] * held;
        }
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

    /// <summary>
    /// The first of a vessel's fills, from <paramref name="first"/> liters toward <paramref name="last"/>, that any storage or station gives out, and the nearest place that does.
    /// One pass over them, where <see cref="SourceFor"/> would take one for each fill.
    /// </summary>
    internal (string Item, Thing Source)? NearestFill(VesselDef v, int first, int last, Vector2 origin) {
        int step = last >= first ? 1 : -1, ranks = Math.Abs(last - first), rank = ranks + 1;
        Thing? best = null; float nearest = 0;
        foreach (var kind in Stores) Nearer(kind, false);
        foreach (var kind in stationKinds) Nearer(kind, true);
        return best == null ? null : (v.States[first + rank * step], best);

        void Nearer(ThingKind kind, bool station) {
            foreach (var t in S.Things.Of(kind))
                for (int r = 0; r <= Math.Min(rank, ranks); r++) {
                    string item = v.States[first + r * step];
                    if (t.Inventory[item] <= 0 || !t.Inventory.AllowsWithdrawal(item) || station && Pending(t, item)) continue;
                    float distance = t.Pos.DistanceSquaredTo(origin);
                    if (r < rank || distance < nearest) { best = t; rank = r; nearest = distance; }
                    break;
                }
        }
    }

    /// <summary>A hauler's vessels with room at a station that holds leftover liquid for them: filled there, then stored like any load.</summary>
    private bool FillsLeftover(Worker w, Thing? dest, string item, float dt) {
        if (dest == null || Data.VesselOf(item) is not VesselDef v || item == v.Full || dest.Inventory[v.Liquid] == 0 || Pending(dest, v.Liquid)) return false;
        w.Status = Phrase("Taking ", Lower(Data.Name(v.Liquid)), " out");
        if (Walk(w, dest.Pos, dt)) { Carry(dest.Inventory, w.Bag, null, v.Liquid, dest.Inventory[v.Liquid]); w.Destination = 0; }
        return true;
    }
}
