using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A placed barrel's or a cabin's roof barrel's part of the Haul job, and the watering of a dry farm plot: the water comes in
/// the worker's tool slot throughout the trip, from the lake or a full barrel, rather than as a carry in hand.
/// </summary>
internal sealed class BarrelHaul : PlaceHaul {
    private readonly Barrels barrels;

    public BarrelHaul(World world, Barrels barrels) : base(world) => this.barrels = barrels;

    public static readonly Vector2 LadderApproach = new(1.94f, .77f);
    public static readonly Vector2 SpoutApproach = new(0, .6f);
    public const float ClimbSeconds = 3.3f, PourSeconds = 2, FillSeconds = 1.35f;
    internal const string WaterTool = "water vessel";
    private static readonly Vector2[] FarmStands = {
        new(-2.1f, -2.1f), new(2.1f, -2.1f), new(2.1f, 2.1f), new(-2.1f, 2.1f)
    };
    internal static int FarmStops => FarmStands.Length;
    internal static Vector2 FarmPourPoint(Thing farm, int stop) =>
        farm.Pos + FarmStands[stop] - FarmStands[stop].Normalized() * .55f;

    public static bool Vessel(string item) => Bucket.Is(item) || Water.IsBottle(item);

    public static bool OnLadderOrRoof(Worker w, Thing target) => target.Kind == ThingKind.Hut && w.WaterTarget == target.Id
        && w.WaterAction is WorkerWaterAction.ClimbUp or WorkerWaterAction.Pour or WorkerWaterAction.ClimbDown;

    /// <summary>Water is no carry in hand: <see cref="Haul"/> brings it once no other place of the job has a carry.</summary>
    public override void Carries(Worker w, Thing place, ref Carry best) { }

    public override string Wait(Thing place) => place.Kind == ThingKind.Hut && Barrel.In(place.Inventory) == null ? "Needs a steel barrel in the cabin"
        : Barrels.Room(place) == 0 ? "The barrel is full" : base.Wait(place);

    /// <summary>Fills the barrels of a haul job with water, carrying on a walk already started.</summary>
    internal bool Haul(Worker w, JobEntry job, float dt) {
        if (w.WaterWalk is { } walk && Vessel(w.Tool) && Hauling.Covers(job, walk.Target) && world.Find(walk.Target.Id) == walk.Target
            && NeedsWater(walk.Target) && ToolFits(walk.Target, w.Tool) && ValidWalk(w, walk.Target, walk.Source)) {
            return Follow(w, walk.Target, walk.Source, walk.Stand, dt);
        }
        w.WaterWalk = null;
        if (job is Duty duty) return HaulFor(w, duty, dt);
        foreach (var haul in ((HaulGroup)job).Hauls) if (HaulFor(w, haul, dt)) return true;
        return false;
    }

    private bool NeedsWater(Thing target) => target.Kind == ThingKind.Farm
        ? target.FarmStage == FarmStage.Growing && world.Farms.Dry(target) && !WateredByCrew(target)
        : Barrels.Room(target) > 0;

    private bool WateredByCrew(Thing farm) {
        foreach (var worker in world.Workers) if (worker.WaterTarget == farm.Id) return true;
        return false;
    }

    private static bool ToolFits(Thing target, string item) => target.Kind == ThingKind.Farm ? Bucket.Is(item) : Vessel(item);

    private bool HaulFor(Worker w, Duty duty, float dt) {
        if (world.Find(duty.Thing) is not { Kind: ThingKind.Barrel or ThingKind.Hut or ThingKind.Farm } target || !NeedsWater(target)) return false;
        var stand = target.Pos + (target.Kind == ThingKind.Farm ? FarmStands[0] : target.Kind == ThingKind.Hut ? LadderApproach : SpoutApproach);
        if (!world.CanStand(stand)) return Blocked(w);
        if (!ToolFits(target, w.Tool)) {
            string need = target.Kind == ThingKind.Farm ? Bucket.Item : WaterTool;
            if (!world.StationsHold(need)) {
                w.Status = target.Kind == ThingKind.Farm ? "Needs a steel bucket in the tool slot" : "Needs a bucket or bottle in the tool slot";
                return false;
            }
            var kit = world.Equip(w, need, target, dt);
            if (kit != World.Kit.Ready) return kit == World.Kit.Busy;
        }
        Thing? source = null;
        if (NeedsFill(target, w.Tool)) {
            if (NearestWaterFor(w, target) is not (Thing from, Vector2 at)) { w.Status = "Needs an accessible lake shore or full barrel"; return false; }
            source = from; stand = at;
        }
        w.WaterWalk = (target, source, stand);
        return Follow(w, target, source, stand, dt);
    }

    private bool NeedsFill(Thing target, string item) =>
        target.Kind == ThingKind.Farm ? Bucket.Liters(item) < Bucket.Capacity : world.Data.VesselOf(item)!.LitersIn(item) == 0;

    private bool ValidWalk(Worker w, Thing target, Thing? source) {
        bool refill = NeedsFill(target, w.Tool);
        return source == null ? !refill : refill && world.Find(source.Id) == source && GivesWater(source);
    }

    private bool Follow(Worker w, Thing target, Thing? source, Vector2 stand, float dt) {
        w.Assignment = target.Id; w.Doing = JobKind.Haul;
        if (w.ToolSlot.Free(w.Tool) == 0) { w.Status = "Waiting for the player to let go"; return true; }
        w.Status = source == null ? "Bringing water" : "Fetching water";
        if (world.Walk(w, stand, dt, .08f, exact: true)) {
            if (!world.CanStand(stand)) return Blocked(w);
            var action = source != null ? WorkerWaterAction.Fill : target.Kind == ThingKind.Hut ? WorkerWaterAction.ClimbUp : WorkerWaterAction.Pour;
            StartWater(w, action, source ?? target);
        }
        if (w.WorkState == WorkerWorkState.Blocked) return Blocked(w);
        return true;
    }

    private static bool Blocked(Worker w) {
        w.WaterWalk = null;
        w.Status = World.BlockedPath;
        w.WorkState = WorkerWorkState.Idle;
        return false;
    }

    private static bool GivesWater(Thing source) => source.Kind == ThingKind.Water
        || source.Inventory.Free(Water.Liquid) == Barrel.Capacity && source.Inventory.AllowsWithdrawal(Water.Liquid);

    private (Thing Source, Vector2 At)? NearestWaterFor(Worker w, Thing target) {
        Thing? source = world.Nearest(ThingKind.Water, w.Pos);
        Vector2? shore = source == null ? null : OpenShorePoint(w.Pos);
        Vector2 at = shore.GetValueOrDefault();
        float distance = shore == null ? float.MaxValue : at.DistanceSquaredTo(w.Pos);
        if (shore == null) source = null;
        foreach (var t in world.ThingsOf(ThingKind.Barrel)) {
            if (t == target || !GivesWater(t)) continue;
            var spout = t.Pos + SpoutApproach;
            float d = t.Pos.DistanceSquaredTo(w.Pos);
            if (d < distance && world.CanStand(spout)) { source = t; at = spout; distance = d; }
        }
        return source == null ? null : (source, at);
    }

    /// <summary>Finds dry ground at the water's edge on either side of a blocked approach.</summary>
    private Vector2? OpenShorePoint(Vector2 from) {
        var shore = Terrain.WaterPoint(from);
        if (world.CanStand(shore)) return shore;
        var radial = shore - Terrain.Lake;
        // Samples about one meter apart along the shore, starting with the closest pair.
        const float step = 1f / Terrain.LakeRadius;
        for (float angle = step; angle <= Mathf.Pi; angle += step) {
            var left = Terrain.WaterPoint(Terrain.Lake + radial.Rotated(angle));
            var right = Terrain.WaterPoint(Terrain.Lake + radial.Rotated(-angle));
            bool leftOpen = world.CanStand(left), rightOpen = world.CanStand(right);
            if (leftOpen && (!rightOpen || left.DistanceSquaredTo(from) <= right.DistanceSquaredTo(from))) return left;
            if (rightOpen) return right;
        }
        return null;
    }

    private static void StartWater(Worker w, WorkerWaterAction action, Thing target) {
        w.WaterWalk = null;
        w.WaterAction = action;
        w.WaterTarget = target.Id;
        w.WaterProgress = 0;
    }

    internal bool FinishAction(Worker w, float dt) {
        if (w.WaterAction == WorkerWaterAction.None) return false;
        if (world.Find(w.WaterTarget) is not Thing target) { StopWater(w); return false; }
        if (target.Kind == ThingKind.Farm) return WaterFarm(w, target, dt);
        float duration = w.WaterAction switch {
            WorkerWaterAction.Fill => FillSeconds,
            WorkerWaterAction.Pour => PourSeconds,
            _ => ClimbSeconds
        };
        w.Status = w.WaterAction switch {
            WorkerWaterAction.Fill => "Filling the water vessel",
            WorkerWaterAction.ClimbUp => "Climbing to the roof barrel",
            WorkerWaterAction.Pour => "Pouring water",
            _ => "Climbing down"
        };
        w.WaterProgress = Math.Min(1, w.WaterProgress + dt / duration);
        if (w.WaterProgress < 1) return true;
        if (w.WaterAction == WorkerWaterAction.ClimbUp) { StartWater(w, WorkerWaterAction.Pour, target); return true; }
        if (w.WaterAction == WorkerWaterAction.Fill) FillTool(w, target);
        if (w.WaterAction == WorkerWaterAction.Pour) {
            PourTool(w, target);
            if (target.Kind == ThingKind.Hut) { StartWater(w, WorkerWaterAction.ClimbDown, target); return true; }
        }
        if (w.WaterAction != WorkerWaterAction.Fill) w.InHand = null;
        StopWater(w);
        return true;
    }

    private bool WaterFarm(Worker w, Thing farm, float dt) {
        int held = Bucket.Liters(w.Tool), room = world.Farms.WaterRoom(farm);
        if (held <= 0 || room == 0 || farm.FarmStage != FarmStage.Growing || !World.HasDuty(w, farm.Id, JobKind.Haul)) return FinishFarm(w);
        if (w.ToolSlot.Free(w.Tool) == 0) { w.Status = "Waiting for the player to let go"; return true; }
        w.Status = "Watering the farm plot";
        if (w.WaterAction == WorkerWaterAction.WalkFarm) {
            if (world.Walk(w, farm.Pos + FarmStands[w.FarmWaterStop], dt, .08f, exact: true))
                StartWater(w, WorkerWaterAction.Pour, farm);
            if (w.WorkState == WorkerWorkState.Blocked) return FinishFarm(w);
            return true;
        }
        w.WaterProgress = Math.Min(1, w.WaterProgress + dt / PourSeconds);
        if (w.WaterProgress < 1) return true;
        int stopsLeft = FarmStops - w.FarmWaterStop;
        int share = (held + stopsLeft - 1) / stopsLeft;
        int poured = farm.Inventory.Add(Water.Liquid, Math.Min(room, share));
        w.Tool = Bucket.AtFill(held - poured);
        if (held == poured || ++w.FarmWaterStop == FarmStops) return FinishFarm(w);
        StartWater(w, WorkerWaterAction.WalkFarm, farm);
        return true;
    }

    private static bool FinishFarm(Worker w) {
        w.InHand = null;
        StopWater(w);
        return false;
    }

    private static void StopWater(Worker w) {
        w.WaterAction = WorkerWaterAction.None;
        w.WaterTarget = 0;
        w.WaterProgress = 0;
        w.FarmWaterStop = 0;
    }

    private void FillTool(Worker w, Thing source) {
        if (world.Data.VesselOf(w.Tool) is not VesselDef v || w.ToolSlot.Free(w.Tool) == 0) return;
        int held = v.LitersIn(w.Tool);
        int added = source.Kind == ThingKind.Water ? v.Capacity - held : Barrels.TakeWater(source, v.Capacity - held);
        w.Tool = v.States[held + added];
    }

    private void PourTool(Worker w, Thing target) {
        if (world.Data.VesselOf(w.Tool) is not VesselDef v || w.ToolSlot.Free(w.Tool) == 0) return;
        int held = v.LitersIn(w.Tool);
        int poured = barrels.AddWater(target, held);
        w.Tool = v.States[held - poured];
    }
}
