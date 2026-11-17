using System;

namespace ChopChopMeow;

/// <summary>Packing stations: one chosen good goes into empty crates one item at a time while someone works the station.</summary>
public sealed partial class World {
    /// <summary>Whether a packing station can be set to pack an item: anything a crate holds, other than crates and coins.</summary>
    public bool Packable(string item) => Data.Items.ContainsKey(item) && item is not ("crate" or Inventory.Coins);

    public void SetPacking(Thing t, string item) {
        if (t.Kind != ThingKind.Packer || !Packable(item) || item == t.Packs || Busy() || Forwarded(Verb.SetPacking, t.Id, item: item)) return;
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
        if (t.Output!.First() is string crate) {
            Packed(crate)!.Add(t.Packs, 1);
            if (!OpenCrate(t, crate)) t.Rounds++;
            return;
        }
        t.Inventory.Remove("crate", 1);
        var box = NewCrate(); box.Add(t.Packs, 1);
        t.Output.Add(Pack(box), 1);
    }

    /// <summary>A packer works the station while it has its good and a crate to pack it into; each crate filled is a round.</summary>
    private bool PackWork(Worker w, Thing t, float dt) {
        if (!CanPack(t)) { w.Status = t.Packs == "" ? "Choose what to pack" : "Waiting for goods and crates"; return false; }
        if (!HandsFree(w, dt)) return true;
        w.Status = "Packing crates";
        if (Walk(w, t.Pos + PackStand, dt, .1f)) w.WorkState = WorkerWorkState.Hammering;
        return true;
    }
}
