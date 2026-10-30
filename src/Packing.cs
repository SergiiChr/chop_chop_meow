using System;

namespace ChopChopMeow;

/// <summary>Packing stations: one chosen good goes into empty crates one item at a time while someone works the station.</summary>
public sealed partial class World {
    /// <summary>Whether a packing station can be set to pack an item: anything a crate holds, other than crates and coins.</summary>
    public bool Packable(string item) => Data.Items.ContainsKey(item) && item is not ("crate" or Inventory.Coins);

    public void SetPacking(Thing t, string item) {
        if (t.Kind != "packer" || !Packable(item) || item == t.Packs || Forwarded("set_packing", t.Id, item: item)) return;
        t.Packs = item; t.Progress = 0;
    }

    /// <summary>Whether the crate on a station still takes its good: a crate of that good with room left.</summary>
    public bool OpenCrate(Thing t, string crate) {
        var kind = PackedKind(crate);
        return t.Packs != "" && kind.Length == PackedPrefix.Length + t.Packs.Length && kind.EndsWith(t.Packs) && Packed(crate)?.Room(t.Packs) > 0;
    }

    /// <summary>Whether the station has a good and somewhere to put it: an open crate, or an empty one to start.</summary>
    public bool CanPack(Thing t) =>
        t.Packs != "" && t.Inventory[t.Packs] > 0 && (t.Output!.First() is string crate ? OpenCrate(t, crate) : t.Inventory["crate"] > 0);

    /// <summary>
    /// Packs one item every <see cref="GameData.PackSeconds"/> while the player or a paid worker works the station.
    /// The first item opens an empty crate; a full crate stays in the output until it is carried out.
    /// </summary>
    private void PackTick(Thing t, float dt) {
        t.Processing = CanPack(t) && Hammered(t);
        if (!t.Processing) return;
        t.Progress += dt;
        if (t.Progress < Data.PackSeconds) return;
        t.Progress = 0;
        t.Inventory.Remove(t.Packs, 1);
        if (t.Output!.First() is string crate) { Packed(crate)!.Add(t.Packs, 1); return; }
        t.Inventory.Remove("crate", 1);
        var box = NewCrate(); box.Add(t.Packs, 1);
        t.Output.Add(Pack(box), 1);
    }

    /// <summary>
    /// A packer's hauler carries out a finished crate and anything the station no longer packs, then keeps a crate's worth of goods and two empty crates there.
    /// A packer working the station stays at it while there is something to pack.
    /// </summary>
    private bool PackWork(Worker w, Thing t, float dt) {
        bool packs = HasDuty(w, t.Id, "craft") && CanPack(t);
        if (HasDuty(w, t.Id, "haul")) {
            if (t.Output!.First() is string crate && !OpenCrate(t, crate)) {
                if (DepositFor(crate, t.Pos) is not Thing dest) { w.Status = "Full crate waiting · needs accepting storage"; return false; }
                w.Status = "Carrying out a packed crate";
                if (Walk(w, t.Pos, dt)) { t.Output.TransferTo(w.Bag, crate, 1, true); w.Destination = dest.Id; }
                return true;
            }
            foreach (var pair in t.Inventory.Items) {
                if (pair.Key == t.Packs || pair.Key == "crate" || DepositFor(pair.Key, t.Pos) is not Thing store) continue;
                w.Status = Phrase("Carrying out ", Data.Name(pair.Key));
                // Returns straight after the move, so the changed inventory is not enumerated further.
                if (Walk(w, t.Pos, dt)) { t.Inventory.TransferTo(w.Bag, pair.Key, w.Bag.StackSize, true); w.Destination = store.Id; }
                return true;
            }
            // A lone worker who also packs fetches only once the station runs dry, crates first when there is none to pack into.
            if (!packs && (t.Inventory["crate"] == 0 && Fetch("crate", 2) || t.Packs != "" && Fetch(t.Packs, Data.CrateCapacity) || Fetch("crate", 2))) return true;
        }
        w.Status = t.Packs == "" ? "Choose what to pack" : packs ? "Packing crates" : "Waiting for goods and crates";
        if (!packs) return false;
        if (Walk(w, t.Pos + PackStand, dt, .1f)) w.WorkState = WorkerWorkState.Hammering;
        return true;

        bool Fetch(string item, int keep) {
            if (t.Inventory[item] >= keep || SourceFor(item, w.Pos) is not Thing source) return false;
            w.Status = Phrase("Fetching ", Data.Name(item));
            if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, item, keep - t.Inventory[item], true); w.Destination = t.Id; }
            return true;
        }
    }
}
