using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Liquids a station holds: poured in from full vessels, which stay behind empty, and taken out only into empty ones.</summary>
public sealed partial class World {
    /// <summary>Pours up to <paramref name="count"/> full vessels from one box into another as their liquid; each vessel stays in its own cell, empty.</summary>
    private static int Pour(Inventory from, Inventory to, VesselDef v, int count) {
        count = Math.Min(Math.Min(count, from[v.Full]), to.Room(v.Liquid));
        for (int i = 0; i < count; i++) from.Replace(v.Full, v.Empty);
        to.Add(v.Liquid, count);
        return count;
    }

    /// <summary>Fills up to <paramref name="count"/> empty vessels in one box from the liquid another holds.</summary>
    private static int Fill(Inventory from, Inventory to, VesselDef v, int count) {
        count = Math.Min(Math.Min(count, from[v.Liquid]), to[v.Empty]);
        for (int i = 0; i < count; i++) to.Replace(v.Empty, v.Full);
        from.Remove(v.Liquid, count);
        return count;
    }

    /// <summary>The vessel whose liquid a full one pours into a box, or null: the box must be a station's cups whose recipe takes that liquid.</summary>
    private VesselDef? PoursInto(Thing? t, Inventory to, string item) => t != null && to == t.Inventory && Data.FullVessel(item) is VesselDef v && Pending(t, v.Liquid) ? v : null;

    /// <summary>
    /// Moves items between two boxes, by hand or by a worker.
    /// A liquid fills empty vessels in the box it goes to, unless that box is a station that takes it;
    /// a full vessel dropped into a station that takes its liquid pours it there and stays behind, empty.
    /// </summary>
    /// <param name="into">The thing that owns <paramref name="to"/>, or null for the backpack or a worker's bag.</param>
    private int Carry(Inventory from, Inventory to, Thing? into, string item, int quantity, bool automated = false) {
        if (Data.VesselFor(item) is VesselDef held && !(into != null && to == into.Inventory && Pending(into, item))) return Fill(from, to, held, quantity);
        if (PoursInto(into, to, item) is VesselDef poured) return Pour(from, to, poured, quantity);
        return from.TransferTo(to, item, quantity, automated);
    }

    /// <summary>Why a liquid cannot leave a box for the backpack or another box now, or null: it fills only an empty vessel there.</summary>
    public string? FillBlock(Inventory from, Inventory to, string item) =>
        Data.VesselFor(item) is VesselDef v && from[item] > 0 && to[v.Empty] == 0 ? $"You need {A(Lower(Data.Name(v.Empty)))} to take the {Lower(Data.Name(item))}." : null;

    /// <summary>The vessel an item is a part-full or empty one of, when a station's recipe takes that vessel's liquid, or null.</summary>
    private VesselDef? NotFullVessel(Thing t, string item) => Data.VesselOf(item) is VesselDef v && item != v.Full && Pending(t, v.Liquid) ? v : null;

    /// <summary>Why a station's recipe will not take a vessel: only a full one pours.</summary>
    public string VesselWarning(VesselDef v) => VesselWarningStart + Lower(Data.Name(v.Full));
    /// <summary>How every <see cref="VesselWarning"/> starts, which the interface shows above the backpack.</summary>
    public const string VesselWarningStart = "Requires full ";

    /// <summary>The vessel the backpack holds only part-full or empty ones of while a batch's liquid needs more full ones, or null.</summary>
    private VesselDef? ShortOfFullVessels(Dictionary<string, int> cost) {
        foreach (var (item, needed) in cost)
            if (Data.VesselFor(item) is VesselDef v && S.Backpack[v.Full] < needed && (S.Backpack[v.Empty] > 0 || v.Partial.Exists(p => S.Backpack[p] > 0))) return v;
        return null;
    }

    /// <summary>How many of a recipe input the backpack holds: a liquid counts the full vessels that pour it.</summary>
    public int Carried(string input) => S.Backpack[Data.CarriedAs(input)];

    /// <summary>Whether the backpack holds a batch's inputs, a liquid as full vessels.</summary>
    public bool CarriesBatch(Dictionary<string, int> cost) {
        foreach (var (item, needed) in cost) if (Carried(item) < needed) return false;
        return true;
    }

    /// <summary>Fills the backpack's empty vessels from every liquid a box holds; what finds no vessel is spilled.</summary>
    private void PourOut(Inventory box) {
        foreach (var v in Data.Vessels)
            if (box[v.Liquid] > 0) { Fill(box, S.Backpack, v, box[v.Liquid]); box.Remove(v.Liquid, box[v.Liquid]); }
    }

    /// <summary>
    /// Liquid a station holds that its recipe no longer takes, with storage that gives out empty vessels for it and takes full ones back, or null.
    /// A hauler brings the empty vessels, fills them there and stores them.
    /// </summary>
    private (VesselDef Vessel, Thing Empties)? LiquidToClear(Thing t, Vector2 from) {
        foreach (var v in Data.Vessels)
            if (t.Inventory[v.Liquid] > 0 && !Pending(t, v.Liquid) && DepositFor(v.Full, t.Pos) != null && SourceFor(v.Empty, from) is Thing empties) return (v, empties);
        return null;
    }

    /// <summary>A hauler's empty vessels at a station that holds leftover liquid for them: filled there, then stored like any load.</summary>
    private bool FillsLeftover(Worker w, Thing? dest, string item, float dt) {
        if (dest == null || Data.VesselOf(item) is not VesselDef v || item != v.Empty || dest.Inventory[v.Liquid] == 0 || Pending(dest, v.Liquid)) return false;
        w.Status = Phrase("Taking ", Lower(Data.Name(v.Liquid)), " out");
        if (Walk(w, dest.Pos, dt)) { Fill(dest.Inventory, w.Bag, v, w.Bag[item]); w.Destination = 0; }
        return true;
    }
}
