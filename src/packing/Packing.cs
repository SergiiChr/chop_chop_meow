using System;

namespace ChopChopMeow;

/// <summary>Packing stations: one chosen good goes into empty crates one item at a time while someone works the station.</summary>
public sealed partial class World {
    /// <summary>Whether a packing station can be set to pack an item: anything a crate holds, other than crates and coins.</summary>
    public bool Packable(string item) => Data.Items.ContainsKey(item) && item is not ("crate" or Inventory.Coins) && !Barrel.Is(item);

    /// <summary>Room in one packing input, keeping space for an empty crate when there is none.</summary>
    internal static int PackingRoom(Thing station, string item) {
        var box = station.Inventory;
        int room = box.SlotRoom(item);
        if (item != "crate" && box["crate"] == 0) room = Math.Min(room, box.Room(item) - box.StackOf(item));
        return Math.Max(0, room);
    }

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
        PackingItem(t) != null && (t.Output!.First() is string crate ? OpenCrate(t, crate) : t.Inventory.Free("crate") > 0);

    /// <summary>The good ready to pack, which may be a particular lantern from the input.</summary>
    private static string? PackingItem(Thing t) => t.Packs == Lanterns.Item ? t.Inventory.FirstLantern(free: true)
        : t.Packs != "" && t.Inventory.Free(t.Packs) > 0 ? t.Packs : null;

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
        string item = PackingItem(t)!;
        string? crate = t.Output!.First();
        var box = crate == null ? NewCrate() : Packed(crate)!;
        if (t.Inventory.TransferTo(box, item, 1) == 0) return;
        if (crate != null) {
            if (!OpenCrate(t, crate)) t.Rounds++;
        } else {
            t.Inventory.Remove("crate", 1);
            t.Output.Add(Pack(box), 1);
        }
    }
}
