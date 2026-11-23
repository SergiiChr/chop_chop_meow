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
    /// <summary>Square yards laid out on the ground, walkable and snapping edge to edge.</summary>
    public static bool IsZone(ThingKind kind) => kind is ThingKind.Pile or ThingKind.Farm;
    private static readonly ThingKind[] Zones = { ThingKind.Pile, ThingKind.Farm };
    public const float ZoneSize = 3;
    internal static bool IsQuarterTurn(float rotation) => rotation is 0 or Mathf.Pi / 2 or Mathf.Pi or 3 * Mathf.Pi / 2;
    internal static bool ValidRotation(ThingKind kind, float rotation) => rotation == 0 ||
        kind != ThingKind.Mine && IsQuarterTurn(rotation);
    /// <summary>
    /// Snaps a mine to its cliff, a yard to a neighboring yard's grid, and gravel paths and pipes to tile centers.
    /// </summary>
    public Vector2 Snap(ThingKind kind, Vector2 p) {
        if (kind == ThingKind.Mine) return Mines.Snap(p);
        if (kind is ThingKind.GravelPath or ThingKind.SteelPiping) return Terrain.TileAt(p);
        if (!IsZone(kind) || world.Nearest(Zones, p) is not Thing near) return p;
        float half = World.Footprint(kind), other = World.Footprint(near.Kind);
        float touch = half + other;
        var delta = p - near.Pos;
        if (half == other)
            return delta.LengthSquared() <= touch * touch * 1.6f * 1.6f ? near.Pos + (delta / touch).Round() * touch : p;

        float step = 2 * Math.Min(half, other), offset = Math.Abs(half - other);
        float Aligned(float axis) => Math.Clamp(Mathf.Round((axis - offset) / step) * step + offset, -touch, touch);
        var across = new Vector2(MathF.CopySign(touch, delta.X), Aligned(delta.Y));
        var along = new Vector2(Aligned(delta.X), MathF.CopySign(touch, delta.Y));
        var snapped = across.DistanceSquaredTo(delta) <= along.DistanceSquaredTo(delta) ? across : along;
        return snapped.DistanceSquaredTo(delta) <= 1.8f * 1.8f ? near.Pos + snapped : p;
    }
    /// <summary>
    /// The room a building keeps from a resource's middle beyond its own footprint, and a yard beyond its own edge.
    /// </summary>
    internal const float ResourceRoom = .65f, YardResourceRoom = .3f;
    /// <summary>
    /// Why a building, or a crate from the backpack when <paramref name="item"/> names one, cannot go here.
    /// </summary>
    public string? PlacementBlock(ThingKind kind, Vector2 p, string? item = null, float rotation = 0, PipeShape pipeShape = PipeShape.Straight) {
        bool portable = kind is ThingKind.Crate or ThingKind.Barrel;
        if (!portable && !world.Data.Buildings.ContainsKey(kind)) return "Unknown building.";
        if (!ValidRotation(kind, rotation)) return "That building cannot go up at this angle.";
        if (world.Occupied) return "You are busy.";
        if (world.S.Sheltered != 0) return "Leave the cabin first.";
        if (kind == ThingKind.SteelPiping) {
            if (!Enum.IsDefined(pipeShape)) return "Choose a pipe shape.";
            var tile = Terrain.TileAt(p);
            if (!Terrain.OnMap(tile)) return "Choose a tile on the map.";
            if (TooFar(tile) is string pipeFar) return pipeFar;
            if (world.SteelPipes.Laid(tile)) return "A steel pipe is already laid here.";
            return Unaffordable(kind);
        }
        if (WolfPatrols.NearDen(p, Den.Clearance)) return "Leave the wolf den's mouth clear.";
        if (kind == ThingKind.Mine) return world.Mines.Block(p);
        if (kind == ThingKind.GravelPath) return world.GravelPaths.Block(Terrain.TileAt(p));
        if (IsZone(kind)) return ZoneBlock(kind, p);
        if (TooFar(p) is string far) return far;
        if (world.Player.DistanceTo(p) < 1.2f || Terrain.Spawn.DistanceTo(p) < 1.2f) return "Leave your feet and the arrival point clear.";
        if (kind == ThingKind.Pump) {
            if (!Terrain.InLake(p)) return "Place the intake in the freshwater lake’s shallows.";
            if (!world.CanStand(p + World.BellowsStand.Rotated(rotation))) return "Turn the handle toward open ground on the bank.";
        } else if (!world.CanStand(p) || Terrain.InLake(p, .8f)) return "Choose dry, open ground.";
        foreach (var t in world.S.Things)
            if (!World.BuiltOver(t.Kind) && (t.Remaining > 0 || !t.IsResource) && TooClose(t, kind, p))
                return "Leave room around resources and buildings.";
        if (kind == ThingKind.Hut) {
            var door = p + Doorways.DoorOffset.Rotated(rotation);
            if (!world.CanStand(door) || Terrain.InLake(door, .8f) || world.S.Things.Any(t => !World.BuiltOver(t.Kind) && !t.IsResource && t.Pos.DistanceTo(door) < World.Footprint(t.Kind) + DoorRoom))
                return "Leave open ground in front of the door.";
            if (!world.CanStand(p + BarrelHaul.LadderApproach.Rotated(rotation))) return "Leave dry, open ground at the cabin's ladder.";
        }
        foreach (var hut in world.S.Things.Of(ThingKind.Hut)) {
            if (Doorways.DoorPoint(hut).DistanceTo(p) < World.Footprint(kind) + DoorRoom) return "Leave the cabin's door clear.";
            if (hut.ToWorld(BarrelHaul.LadderApproach).DistanceTo(p) < World.Footprint(kind) + DoorRoom) return "Leave the cabin's ladder clear.";
        }
        foreach (var pump in world.ThingsOf(ThingKind.Pump))
            if (pump.ToWorld(World.BellowsStand).DistanceTo(p) < World.Footprint(kind) + DoorRoom) return "Leave the pump handle clear.";
        if (!portable) return Unaffordable(kind);
        if (item == null || (world.MarkedSource ?? world.S.Backpack)[item] > 0) return null;
        return kind == ThingKind.Crate ? "That crate is no longer in your backpack." : "That barrel is no longer available.";
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
    private static bool TooClose(Thing t, ThingKind kind, Vector2 at) {
        float margin = World.Footprint(kind) + (t.IsResource ? ResourceRoom : World.Footprint(t.Kind) + .45f);
        var delta = t.Pos - at;
        return t.Kind == ThingKind.Farm ? Math.Abs(delta.X) < margin && Math.Abs(delta.Y) < margin
            : delta.LengthSquared() < margin * margin;
    }

    private string? ZoneBlock(ThingKind kind, Vector2 p) {
        if (TooFar(p) is string far) return far;
        float half = World.Footprint(kind);
        // The farm's outer edge is a full tile beyond its outermost whole-tile centers.
        float sample = kind == ThingKind.Farm ? half : ZoneSize / 2 - .5f;
        for (float x = -sample; x <= sample; x++)
            for (float y = -sample; y <= sample; y++) {
                var q = p + new Vector2(x, y);
                if (!world.CanStand(q) || Terrain.InLake(q, .8f) || Terrain.Spawn.DistanceTo(q) < 1) return "Choose dry, open ground.";
            }
        // Yards may touch edge to edge; anything else keeps its usual room from the square.
        foreach (var t in world.S.Things) {
            if (!IsZone(t.Kind) && t.IsResource && t.Remaining <= 0 || World.BuiltOver(t.Kind)) continue;
            float margin = IsZone(t.Kind) ? World.Footprint(t.Kind) - .05f : t.IsResource ? YardResourceRoom : World.Footprint(t.Kind) + .2f;
            if (Math.Abs(t.Pos.X - p.X) < half + margin && Math.Abs(t.Pos.Y - p.Y) < half + margin) return "Leave room around resources and buildings.";
        }
        return Unaffordable(kind);
    }
    /// <summary>
    /// Builds what <see cref="PlacementBlock"/> allows.
    /// Returns a new building, or null after laying a gravel path or steel pipe tile or refusing placement.
    /// </summary>
    public Thing? Build(ThingKind kind, Vector2 p, float rotation = 0, PipeShape pipeShape = PipeShape.Straight) {
        if (!world.Data.Buildings.ContainsKey(kind)) return null;
        p = Snap(kind, p);
        var blocked = PlacementBlock(kind, p, rotation: rotation, pipeShape: pipeShape);
        if (blocked != null) { world.Say(blocked); return null; }
        if (world.Busy() || world.Forwarded(Verb.Build, item: kind.Id(), at: p, rotation: rotation, pipeShape: pipeShape)) return null;
        world.S.Backpack.Spend(world.Data.Buildings[kind].Cost);
        world.S.Milestones.Add(kind.Id());
        if (kind == ThingKind.GravelPath) { world.GravelPaths.Lay(Terrain.TileAt(p)); world.Play("place_building", p); return null; }
        if (kind == ThingKind.SteelPiping) { world.SteelPipes.Lay(Terrain.TileAt(p), rotation, pipeShape); world.Play("place_building", p); return null; }
        var t = world.Add(kind, p, rotation: rotation); world.ClearGround(t); world.Play("place_building", p);
        if (kind == ThingKind.Campfire) { t.Burn = world.Data.CampfireSeconds; world.Play("start_fire", p); }
        if (kind == ThingKind.Mine) { world.Say($"Marked the {Mines.Name(t)} site. Dig the entrance with a pickaxe, or assign a worker to."); world.Made?.Invoke(world.S.Actor.Id, t.Id); }
        return t;
    }
    /// <summary>
    /// Why a building cannot come down now, or null: someone shelters in it, or a cart is out from it.
    /// </summary>
    private string? DismantleBlock(Thing t) {
        foreach (var w in world.Workers) if (BarrelHaul.OnLadderOrRoof(w, t)) return "Wait for the worker to come down the ladder.";
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
    /// What the remove selector points at: a visible pipe in the underground view, or a building or gravel path on
    /// the surface.
    /// </summary>
    public Removal RemovalAt(Thing? picked, Vector2 at, bool underground = false) {
        if (underground) {
            var pipe = Terrain.TileAt(at);
            return world.Sees(pipe) && world.SteelPipes.Laid(pipe) ? Removal.OfPipe(pipe) : Removal.None;
        }
        if (picked is { } building && world.Data.Buildings.ContainsKey(building.Kind)) return Removal.Of(building);
        return world.GravelPaths.TileUnder(at) is Vector2I tile ? Removal.Of(tile) : Removal.None;
    }
    /// <summary>Whether what the remove selector points at is within the acting player's reach.</summary>
    public bool InReach(Removal removal) {
        if (removal.Pipe is Vector2I pipe) return PipeRemovalBlock(pipe) == null;
        if (world.Find(removal.Building) is Thing building) return world.Reaches(building);
        return removal.Tile is Vector2I tile && world.GravelPaths.TakeUpBlock(tile) == null;
    }
    /// <summary>Takes down the building, gravel path or steel pipe the remove selector points at.</summary>
    public bool Remove(Removal removal) {
        if (removal.Pipe is Vector2I pipe) return TakeUpPipe(pipe);
        if (world.Find(removal.Building) is Thing building) return Dismantle(building);
        return removal.Tile is Vector2I tile && world.GravelPaths.TakeUp(tile);
    }

    private string? PipeRemovalBlock(Vector2I tile) {
        if (!world.SteelPipes.Laid(tile)) return "No steel pipe here.";
        return world.Reaches(tile) ? null : "Walk closer first.";
    }

    /// <summary>Takes up a steel pipe in reach and returns its materials to the player or a recovery box.</summary>
    internal bool TakeUpPipe(Vector2I tile) {
        if (PipeRemovalBlock(tile) is string refused) { world.Say(refused); return false; }
        if (!world.Data.Buildings.TryGetValue(ThingKind.SteelPiping, out var plan)) return false;
        if (world.Busy() || world.Forwarded(Verb.TakeUpPipe, at: tile)) return true;
        world.SteelPipes.Remove(tile);
        world.Play("take_down_building", tile);
        if (!GiveBack(plan.Cost, tile)) world.Say("The steel pipe did not fit in your backpack. It is in a box where it lay.");
        return true;
    }
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
