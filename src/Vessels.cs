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
        for (; poured < vessels && from[item] > 0 && liters > 0; poured++) {
            int pour = Math.Min(held, liters);
            from.Replace(item, v.States[held - pour]); to.Add(v.Liquid, pour); liters -= pour;
        }
        return poured;
    }

    /// <summary>Pours up to <paramref name="liters"/> of a liquid from one box's vessels into another, least full first so part-drunk ones empty; returns the liters poured.</summary>
    private static int PourLiters(Inventory from, Inventory to, VesselDef v, int liters) {
        int poured = 0;
        for (int held = 1; held <= v.Capacity; held++)
            while (from[v.States[held]] > 0 && poured < liters && to.Room(v.Liquid) > 0) {
                int pour = Math.Min(Math.Min(held, liters - poured), to.Room(v.Liquid));
                from.Replace(v.States[held], v.States[held - pour]); to.Add(v.Liquid, pour); poured += pour;
            }
        return poured;
    }

    /// <summary>Fills one box's vessels with up to <paramref name="liters"/> of the liquid another holds, fullest first so they fill up; returns the liters filled.</summary>
    private static int Fill(Inventory from, Inventory to, VesselDef v, int liters) {
        int filled = 0;
        // A vessel topped up only part way is fuller than every one still to come, so each is filled once.
        for (int held = v.Capacity - 1; held >= 0; held--)
            while (to[v.States[held]] > 0 && filled < liters && from[v.Liquid] > 0) {
                int add = Math.Min(Math.Min(v.Capacity - held, liters - filled), from[v.Liquid]);
                to.Replace(v.States[held], v.States[held + add]); from.Remove(v.Liquid, add); filled += add;
            }
        return filled;
    }

    /// <summary>The liters of a liquid a box's vessels have room for.</summary>
    private static int VesselRoom(Inventory box, VesselDef v) {
        int room = 0;
        for (int held = 0; held < v.Capacity; held++) room += box[v.States[held]] * (v.Capacity - held);
        return room;
    }

    /// <summary>The vessel whose liquid an item pours into a box, or null: the item holds some, and the box is a station's input slots and its recipe takes that liquid.</summary>
    private VesselDef? PoursInto(Thing? t, Inventory to, string item) =>
        t != null && to == t.Inventory && Data.VesselOf(item) is VesselDef v && v.LitersIn(item) > 0 && Pending(t, v.Liquid) ? v : null;

    /// <summary>
    /// Moves items between two boxes, by hand or by a worker; <paramref name="quantity"/> counts liters of a liquid and vessels of anything else.
    /// A liquid fills vessels in the box it goes to, unless that box is a station that takes it;
    /// a vessel dropped into a station that takes its liquid pours what it holds there and stays behind, emptier.
    /// </summary>
    /// <param name="into">The thing that owns <paramref name="to"/>, or null for the backpack or a worker's bag.</param>
    private int Carry(Inventory from, Inventory to, Thing? into, string item, int quantity) {
        if (Data.VesselFor(item) is VesselDef held && !(into != null && to == into.Inventory && Pending(into, item))) return Fill(from, to, held, quantity);
        if (PoursInto(into, to, item) is VesselDef poured) return PourVessels(from, item, to, poured, quantity, int.MaxValue);
        return from.TransferTo(to, item, quantity);
    }

    /// <summary>Why a liquid cannot leave a box for the backpack or another box now, or null: it goes only into vessels there with room for it.</summary>
    public string? FillBlock(Inventory from, Inventory to, string item) =>
        Data.VesselFor(item) is VesselDef v && from[item] > 0 && VesselRoom(to, v) == 0 ? NeedVessel(v) : null;

    /// <summary>The refusal when a vessel's liquid has nowhere to go, such as "You need an empty water vessel."</summary>
    private string NeedVessel(VesselDef v) => $"You need an empty {Lower(Data.Name(v.Liquid))} vessel.";

    /// <summary>Why the backpack's vessels cannot take every liquid a box holds, or null.</summary>
    private string? NoVesselRoom(Inventory box) {
        foreach (var v in Data.Vessels) if (box[v.Liquid] > VesselRoom(S.Backpack, v)) return NeedVessel(v);
        return null;
    }

    /// <summary>How many of a recipe input the backpack holds: a liquid counts the liters in its vessels.</summary>
    public int Carried(string input) {
        if (Data.VesselFor(input) is not VesselDef v) return S.Backpack[input];
        int liters = 0;
        for (int held = 1; held <= v.Capacity; held++) liters += S.Backpack[v.States[held]] * held;
        return liters;
    }

    /// <summary>Whether the backpack holds a batch's inputs, a liquid in its vessels.</summary>
    public bool CarriesBatch(Dictionary<string, int> cost) {
        foreach (var (item, needed) in cost) if (Carried(item) < needed) return false;
        return true;
    }

    /// <summary>Fills the backpack's vessels from every liquid a box holds; what finds no room is spilled, and said, such as " 2L of water spilled."</summary>
    private string PourOut(Inventory box) {
        string spilled = "";
        foreach (var v in Data.Vessels) {
            if (box[v.Liquid] == 0) continue;
            Fill(box, S.Backpack, v, box[v.Liquid]);
            if (box.Remove(v.Liquid, box[v.Liquid]) is > 0 and int lost) spilled += $" {lost}L of {Lower(Data.Name(v.Liquid))} spilled.";
        }
        return spilled;
    }

    /// <summary>The nearest storage with vessels that hold a liquid, fullest first, and that fill; or null.</summary>
    private (string Item, Thing Source)? LiquidSource(VesselDef v, Vector2 from) {
        for (int held = v.Capacity; held >= 1; held--)
            if (SourceFor(v.States[held], from) is Thing source) return (v.States[held], source);
        return null;
    }

    /// <summary>A hauler's vessels with room at a station that holds leftover liquid for them: filled there, then stored like any load.</summary>
    private bool FillsLeftover(Worker w, Thing? dest, string item, float dt) {
        if (dest == null || Data.VesselOf(item) is not VesselDef v || item == v.Full || dest.Inventory[v.Liquid] == 0 || Pending(dest, v.Liquid)) return false;
        w.Status = Phrase("Taking ", Lower(Data.Name(v.Liquid)), " out");
        if (Walk(w, dest.Pos, dt)) { Fill(dest.Inventory, w.Bag, v, dest.Inventory[v.Liquid]); w.Destination = 0; }
        return true;
    }
}
