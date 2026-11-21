using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A chest's or placed crate's part of the Haul job: it carries out what its output filter lets go to the nearest place
/// asking for it, then fills it with what its input filter names.
/// Every carry ranks with an input short of a batch, so in a haul group it comes before topping up a station.
/// </summary>
internal sealed class StoreHaul : PlaceHaul {
    public StoreHaul(World world) : base(world) { }

    public override void Carries(Worker w, Thing t, ref Carry best) {
        foreach (var (item, _) in t.Inventory.Items) {
            int free = t.Inventory.Free(item);
            if (free == 0 || !t.Inventory.AllowsWithdrawal(item) || world.AskerFor(item, t) is not Thing to || world.Intake(to, item, false) is not (Inventory into, int room)) continue;
            // Vessels that pour there go as many as its liters of room take.
            if (world.PoursInto(to, into, item) is VesselDef pours) room = (room + pours.LitersIn(item) - 1) / pours.LitersIn(item);
            Offer(ref best, new Carry { Place = t, From = t, To = to, Item = item, Count = Math.Min(free, room), Rule = 1 });
        }
        if (FillSource(t, w.Pos) is (string wanted, Thing from, int count)) Offer(ref best, new Carry { Place = t, From = from, To = t, Item = wanted, Count = count, Rule = 1 });
    }

    /// <summary>
    /// What a chest's or placed crate's hauler brings it: something its input filter names, from the nearest storage or station that gives it out, as much as it has room for.
    /// Storage that asks for the item itself keeps it, and a station keeps what its recipe takes and any liquid.
    /// </summary>
    private (string Item, Thing Source, int Count)? FillSource(Thing store, Vector2 origin) {
        if (store.Inventory.InputFilter.Count == 0) return null;
        string item = ""; Thing? best = null; float nearest = 0; int count = 0;
        foreach (var kind in World.Stores) Nearer(kind, false);
        foreach (var kind in world.StationKinds) Nearer(kind, true);
        return best == null ? null : (item, best, count);

        void Nearer(ThingKind kind, bool station) {
            foreach (var t in world.ThingsOf(kind)) {
                float distance = t.Pos.DistanceSquaredTo(origin);
                if (t == store || best != null && distance >= nearest) continue;
                foreach (var (held, _) in t.Inventory.Items) {
                    int free = t.Inventory.Free(held);
                    // A liquid leaves a station only in vessels, which its own hauler brings.
                    if (free == 0 || !store.Inventory.Wants(held) || !t.Inventory.AllowsWithdrawal(held) || (station ? world.Pending(t, held) : t.Inventory.Wants(held))
                        || world.Data.VesselFor(held) != null || world.Intake(store, held, false) is not (_, int room)) continue;
                    item = held; best = t; nearest = distance; count = Math.Min(free, room);
                    break;
                }
            }
        }
    }
}
