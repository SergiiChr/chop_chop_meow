using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Putting buildings and yards up in <paramref name="world"/> and taking buildings down: where a plan snaps to, why it
/// or a crate cannot go somewhere, what building spends and what taking down gives back.
/// </summary>
public sealed class Construction(World world) {
    /// <summary>Square 3×3 yards laid out on the ground, walkable and snapping edge to edge into grids.</summary>
    public static bool IsZone(ThingKind kind) => kind is ThingKind.Pile or ThingKind.Farm;
    private static readonly ThingKind[] Zones = { ThingKind.Pile, ThingKind.Farm };
    public const float ZoneSize = 3;
    /// <summary>
    /// Snaps a mine to its cliff, a yard to the grid of the nearest yard close enough to touch it and a gravel path to
    /// the middle of its tile.
    /// </summary>
    public Vector2 Snap(ThingKind kind, Vector2 p) {
        if (kind == ThingKind.Mine) return World.SnapMine(p);
        if (kind == ThingKind.GravelPath) return Terrain.TileAt(p);
        if (!IsZone(kind) || world.Nearest(Zones, p) is not Thing near || near.Pos.DistanceTo(p) > ZoneSize * 1.6f) return p;
        return near.Pos + ((p - near.Pos) / ZoneSize).Round() * ZoneSize;
    }
    /// <summary>
    /// The room a building keeps from a resource's middle beyond its own footprint, and a yard beyond its own edge.
    /// </summary>
    internal const float ResourceRoom = .65f, YardResourceRoom = .3f;
    /// <summary>
    /// Why a building, or a crate from the backpack when <paramref name="item"/> names one, cannot go here.
    /// </summary>
    public string? PlacementBlock(ThingKind kind, Vector2 p, string? item = null) {
        bool crate = kind == ThingKind.Crate;
        if (!crate && !world.Data.Buildings.ContainsKey(kind)) return "Unknown building.";
        if (world.Occupied) return "You are busy.";
        if (world.S.Sheltered != 0) return "Leave the cabin first.";
        if (WolfPatrols.NearDen(p, Den.Clearance)) return "Leave the wolf den's mouth clear.";
        if (kind == ThingKind.Mine) return world.MineBlock(p);
        if (kind == ThingKind.GravelPath) return world.GravelPaths.Block(Terrain.TileAt(p));
        if (IsZone(kind)) return ZoneBlock(kind, p);
        if (TooFar(p) is string far) return far;
        if (world.Player.DistanceTo(p) < 1.2f || Terrain.Spawn.DistanceTo(p) < 1.2f) return "Leave your feet and the arrival point clear.";
        if (!world.CanStand(p) || Terrain.InLake(p, .8f)) return "Choose dry, open ground.";
        foreach (var t in world.S.Things)
            if (!World.BuiltOver(t.Kind) && t.Pos.DistanceTo(p) < (t.IsResource ? World.Footprint(kind) + ResourceRoom : World.Footprint(kind) + World.Footprint(t.Kind) + .45f) && (t.Remaining > 0 || !t.IsResource)) return "Leave room around resources and buildings.";
        if (kind == ThingKind.Hut) {
            var door = Doorways.DoorPoint(new Thing { Pos = p });
            if (!world.CanStand(door) || Terrain.InLake(door, .8f) || world.S.Things.Any(t => !World.BuiltOver(t.Kind) && !t.IsResource && t.Pos.DistanceTo(door) < World.Footprint(t.Kind) + DoorRoom))
                return "Leave open ground in front of the door.";
        }
        foreach (var hut in world.S.Things.Of(ThingKind.Hut)) if (Doorways.DoorPoint(hut).DistanceTo(p) < World.Footprint(kind) + DoorRoom) return "Leave the cabin's door clear.";
        if (!crate) return Unaffordable(kind);
        return item != null && (world.MarkedSource ?? world.S.Backpack)[item] == 0 ? "That crate is no longer in your backpack." : null;
    }
    /// <summary>
    /// What refuses a building the acting player's backpack does not hold the cost of, or null when it does.
    /// </summary>
    internal string? Unaffordable(ThingKind kind) => world.S.Backpack.Has(world.Data.Buildings[kind].Cost) ? null : "Missing materials in your backpack.";
    /// <summary>
    /// What refuses a plan further from the acting player than they can place one, or null within that.
    /// </summary>
    internal string? TooFar(Vector2 p) => world.Player.DistanceTo(p) > 5.5f ? "Build within 5 tiles of yourself." : null;
    /// <summary>
    /// How far past their own footprint buildings keep from a cabin's door spot, where players step in and out.
    /// </summary>
    private const float DoorRoom = .45f;
    private string? ZoneBlock(ThingKind kind, Vector2 p) {
        if (TooFar(p) is string far) return far;
        // Every tile the yard covers.
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++) {
                var q = p + new Vector2(x, y);
                if (!world.CanStand(q) || Terrain.InLake(q, .8f) || Terrain.Spawn.DistanceTo(q) < 1) return "Choose dry, open ground.";
            }
        float half = ZoneSize / 2;
        // Yards may touch edge to edge; anything else keeps its usual room from the square.
        foreach (var t in world.S.Things) {
            if (!IsZone(t.Kind) && t.IsResource && t.Remaining <= 0 || World.BuiltOver(t.Kind)) continue;
            float margin = IsZone(t.Kind) ? half - .05f : t.IsResource ? YardResourceRoom : World.Footprint(t.Kind) + .2f;
            if (Math.Abs(t.Pos.X - p.X) < half + margin && Math.Abs(t.Pos.Y - p.Y) < half + margin) return "Leave room around resources and buildings.";
        }
        return Unaffordable(kind);
    }
    /// <summary>
    /// Builds what <see cref="PlacementBlock"/> allows and returns it; a gravel path tile is laid and is no thing, so
    /// it returns null as a refusal does.
    /// </summary>
    public Thing? Build(ThingKind kind, Vector2 p) {
        p = Snap(kind, p);
        var blocked = PlacementBlock(kind, p);
        if (blocked != null) { world.Say(blocked); return null; }
        if (world.Busy() || world.Forwarded(Verb.Build, item: kind.Id(), at: p)) return null;
        world.S.Backpack.Spend(world.Data.Buildings[kind].Cost);
        world.S.Milestones.Add(kind.Id());
        if (kind == ThingKind.GravelPath) { world.GravelPaths.Lay(Terrain.TileAt(p)); world.Play("place_building", p); return null; }
        var t = world.Add(kind, p); world.ClearGround(t); world.Play("place_building", p);
        if (kind == ThingKind.Campfire) { t.Burn = world.Data.CampfireSeconds; world.Play("start_fire", p); }
        if (kind == ThingKind.Mine) { world.Say($"Marked the {World.MineName(t)} site. Dig the entrance with a pickaxe, or assign a worker to."); world.Made?.Invoke(world.S.Actor.Id, t.Id); }
        return t;
    }
    /// <summary>
    /// Why a building cannot come down now, or null: someone shelters in it, or a cart is out from it.
    /// </summary>
    private string? DismantleBlock(Thing t) {
        foreach (var p in world.S.Players) if (p.Sheltered == t.Id || p.Door != Doorway.None && p.DoorCabin == t.Id) return "Someone is inside. It can come down once they step out.";
        foreach (var order in world.S.Orders) if (order.Signpost == t.Id) return "A cart is out from this trading post. Wait for it to come back.";
        return null;
    }
    /// <summary>
    /// Takes a building down: its cost, anything delivered to build it and everything it holds go to the backpack,
    /// and what does not fit waits in a recovery box where it stood.
    /// </summary>
    public bool Dismantle(Thing t) {
        if (!world.Data.Buildings.TryGetValue(t.Kind, out var def)) return false;
        if (!world.Reaches(t)) { world.Say("Walk closer first."); return false; }
        if (DismantleBlock(t) is string refused) { world.Say(refused); return false; }
        if (world.Busy() || world.Forwarded(Verb.Dismantle, t.Id)) return true;
        if (world.HeldByOthers(t)) return false;
        // Liquid fills the backpack's vessels, and the rest is spilled.
        string spilled = "";
        foreach (var box in World.Boxes(t)) if (box != null) spilled += world.PourOut(box);
        var back = new Dictionary<string, int>(def.Cost);
        if (t.Kind == ThingKind.Mine && t.MineStage >= MineStage.Supplied) foreach (var (item, amount) in world.Data.Mine.Materials) back[item] = back.GetValueOrDefault(item) + amount;
        foreach (var box in World.Boxes(t)) if (box != null) foreach (var (item, amount) in box.Items) back[item] = back.GetValueOrDefault(item) + amount;
        world.S.Things.Remove(t); world.Play("take_down_building", t.Pos);
        if (t.Kind == ThingKind.Toolstation) world.ToolsChanged();
        world.Say((GiveBack(back, t.Pos) ? $"The {world.Lower(def.Name)} is down. Everything is back in your backpack." : $"The {world.Lower(def.Name)} is down. What did not fit in your backpack is in a box where it stood.") + spilled);
        return true;
    }
    /// <summary>
    /// What the remove selector points at on the ground at <paramref name="at"/> with <paramref name="picked"/> under
    /// the
    /// pointer: that thing when it is a building, else the gravel path tile whose gravel the point is on.
    /// </summary>
    public Removal RemovalAt(Thing? picked, Vector2 at) {
        if (picked is { } building && world.Data.Buildings.ContainsKey(building.Kind)) return Removal.Of(building);
        return world.GravelPaths.TileUnder(at) is Vector2I tile ? Removal.Of(tile) : Removal.None;
    }
    /// <summary>Whether what the remove selector points at is within the acting player's reach.</summary>
    public bool InReach(Removal removal) =>
        world.Find(removal.Building) is Thing building ? world.Reaches(building) : removal.Tile is Vector2I tile && world.GravelPaths.TakeUpBlock(tile) == null;
    /// <summary>
    /// Takes down what the remove selector points at, at once: a building as <see cref="Dismantle"/> does, a gravel
    /// path tile as <see cref="GravelPaths.TakeUp"/>.
    /// </summary>
    public bool Remove(Removal removal) =>
        world.Find(removal.Building) is Thing building ? Dismantle(building) : removal.Tile is Vector2I tile && world.GravelPaths.TakeUp(tile);
    /// <summary>
    /// Puts items into the acting player's backpack and what does not fit into a recovery box at <paramref name="at"/>;
    /// true when all of it fitted.
    /// </summary>
    internal bool GiveBack(Dictionary<string, int> items, Vector2 at) {
        Inventory? rest = null;
        foreach (var (item, amount) in items) {
            int left = amount - world.S.Backpack.Add(item, amount);
            if (left <= 0) continue;
            if (rest == null) { rest = world.Add(ThingKind.Grave, at).Inventory; rest.Slots = 0; }
            rest.Slots += (left + rest.StackOf(item) - 1) / rest.StackOf(item); rest.Add(item, left);
        }
        return rest == null;
    }
}
