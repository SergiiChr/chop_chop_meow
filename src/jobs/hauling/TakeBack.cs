using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Where a worker takes a load that no place asking for it and no storage taking everything takes: the nearest building
/// whose output filter lets the item out, into the box it gives out from, up to one stack of the item.
/// Storage has the output filter the player sets on it, so a new chest or crate takes back anything.
/// A building with output slots has one made from what it outputs: a station its recipe's product, a farm plot its yield,
/// an open mine its ore and stone, and a packing station crates of its good.
/// </summary>
internal sealed class TakeBack(World world) {
    /// <summary>Storage whose output filter counts: a barrel or the trading post's market output gives out without taking back.</summary>
    private static readonly ThingKind[] Stores = { ThingKind.Chest, ThingKind.Pile, ThingKind.Crate, ThingKind.Toolstation };
    private static readonly ThingKind[] Outputs = { ThingKind.Farm, ThingKind.Mine, ThingKind.Packer };

    /// <summary>
    /// Takes a load nothing else takes to a building that takes it back, as far as that has room.
    /// With none, the worker holds it and says so; tells whether the worker is busy with the load.
    /// </summary>
    public bool Bring(Worker w, Thing? place, string item, float dt) {
        if (place == null || Box(place, item) is not (Inventory box, int room)) {
            w.Destination = 0; w.Status = world.Phrase("Holding ", world.Data.Name(item), " · no accepting storage");
            return false;
        }
        w.Destination = place.Id; w.Status = world.Phrase("Putting back ", world.Data.Name(item), " → ", world.Data.Name(place.Kind));
        if (!world.Walk(w, place.Pos, dt)) return true;
        w.Bag.TransferTo(box, item, Math.Min(w.Bag[item], room));
        w.Destination = 0;
        if (w.Bag.Count == 0) World.Emptied(w);
        return true;
    }

    /// <summary>The box a building gives an item out from by its output filter, and the room there up to one stack of it, or null.</summary>
    public (Inventory Box, int Room)? Box(Thing t, string item) {
        var box = Outbox(t, item);
        int room = box?.SlotRoom(item) ?? 0;
        return box != null && room > 0 ? (box, room) : null;
    }

    /// <summary>The nearest building that takes back a load of an item, or null.</summary>
    public Thing? Nearest(string item, Vector2 origin) {
        Thing? best = null; float nearest = 0;
        foreach (var kind in world.StationKinds) Nearer(kind);
        foreach (var kind in Outputs) Nearer(kind);
        foreach (var kind in Stores) Nearer(kind);
        return best;

        void Nearer(ThingKind kind) {
            foreach (var t in world.ThingsOf(kind)) {
                float distance = t.Pos.DistanceSquaredTo(origin);
                if (best != null && distance >= nearest || Box(t, item) == null) continue;
                best = t; nearest = distance;
            }
        }
    }

    /// <summary>A building's output filter: the one set on storage, which also has to hold the item, or for a building with output slots what it outputs.</summary>
    private Inventory? Outbox(Thing t, string item) => t.Kind switch {
        _ when world.IsStation(t.Kind) => world.Product(t) == item ? t.Inventory : null,
        ThingKind.Farm => world.Data.Farm.Yield.ContainsKey(item) ? t.Output : null,
        // A mine's output shows on its page only once the mine opens.
        ThingKind.Mine => t.MineStage == MineStage.Open && (item == Mines.Ore(t) || item == "stone") ? t.Output : null,
        ThingKind.Packer => t.Packs != "" && CrateOfGood(item, t.Packs) ? t.Output : null,
        ThingKind.Chest or ThingKind.Pile or ThingKind.Crate or ThingKind.Toolstation => t.Inventory.AllowsWithdrawal(item) && world.Holds(t, item) && !World.Nests(t, item) ? t.Inventory : null,
        _ => null
    };

    /// <summary>Whether an item is a packed crate of one good, compared without building the crate's name.</summary>
    private static bool CrateOfGood(string item, string good) {
        var kind = World.PackedKind(item);
        return kind.Length == World.PackedPrefix.Length + good.Length && kind.EndsWith(good, StringComparison.Ordinal);
    }
}
