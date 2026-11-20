using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A water haul carries liquid in the worker's tool slot throughout the trip.</summary>
internal static class BarrelJobs {
    public static readonly Vector2 LadderApproach = new(1.94f, .77f);
    public static readonly Vector2 SpoutApproach = new(0, .6f);
    public const float ClimbSeconds = 3.3f, PourSeconds = 2, FillSeconds = 1.35f;
    public static bool Vessel(string item) => Bucket.Is(item) || Water.IsBottle(item);

    public static bool OnLadderOrRoof(Worker w, Thing target) => target.Kind == ThingKind.Hut && w.WaterTarget == target.Id
        && w.WaterAction is WorkerWaterAction.ClimbUp or WorkerWaterAction.Pour or WorkerWaterAction.ClimbDown;
}

public sealed partial class World {
    private bool WaterHaul(Worker w, JobEntry job, float dt) {
        Thing? target = job is Duty duty ? WaterTarget(duty) : null;
        if (job is HaulGroup group) {
            foreach (var haul in group.Hauls) {
                target = WaterTarget(haul);
                if (target != null) break;
            }
        }
        if (target == null) return false;

        if (!BarrelJobs.Vessel(w.Tool)) {
            if (!StationsHold(Bucket.Item)) { w.Status = "Needs a bucket or bottle in the tool slot"; return false; }
            if (Equip(w, Bucket.Item, target, dt) != Kit.Ready) return true;
        }
        if (w.ToolSlot.Free(w.Tool) == 0) { w.Status = "Waiting for the player to let go"; return true; }

        var vessel = Data.VesselOf(w.Tool)!;
        w.Assignment = target.Id;
        w.Doing = JobKind.Haul;
        if (vessel.LitersIn(w.Tool) == 0) return FetchWater(w, target, dt);
        w.Status = "Bringing water";
        var approach = target.Kind == ThingKind.Hut ? BarrelJobs.LadderApproach : BarrelJobs.SpoutApproach;
        if (ReachWaterPoint(w, target.Pos + approach, dt)) {
            StartWater(w, target.Kind == ThingKind.Hut ? WorkerWaterAction.ClimbUp : WorkerWaterAction.Pour, target);
        }
        return true;
    }

    private Thing? WaterTarget(Duty duty) =>
        Find(duty.Thing) is { Kind: ThingKind.Barrel or ThingKind.Hut } place && Barrels.Room(place) > 0 ? place : null;

    private bool FetchWater(Worker w, Thing target, float dt) {
        Thing? source = NearestWaterFor(w, target);
        if (source == null) { w.Status = "Needs a lake or full barrel"; return false; }
        w.Status = "Fetching water";
        var at = source.Kind == ThingKind.Water ? Terrain.WaterPoint(w.Pos) : source.Pos + BarrelJobs.SpoutApproach;
        if (ReachWaterPoint(w, at, dt)) StartWater(w, WorkerWaterAction.Fill, source);
        return true;
    }

    private bool ReachWaterPoint(Worker w, Vector2 at, float dt) {
        if (!Walk(w, at, dt, .08f)) return false;
        if (w.Pos.DistanceSquaredTo(at) < .025f) return true;
        w.Status = "Path blocked · clear a route";
        w.WorkState = WorkerWorkState.Blocked;
        return false;
    }

    private Thing? NearestWaterFor(Worker w, Thing target) {
        var nearest = Nearest(ThingKind.Water, w.Pos);
        float distance = nearest == null ? float.MaxValue : Terrain.WaterPoint(w.Pos).DistanceSquaredTo(w.Pos);
        foreach (var t in S.Things.Of(ThingKind.Barrel)) {
            if (t == target || t.Inventory.Free(Water.Liquid) != Barrel.Capacity || !t.Inventory.AllowsWithdrawal(Water.Liquid)) continue;
            float d = t.Pos.DistanceSquaredTo(w.Pos);
            if (d < distance) { nearest = t; distance = d; }
        }
        return nearest;
    }

    private static void StartWater(Worker w, WorkerWaterAction action, Thing target) {
        w.WaterAction = action;
        w.WaterTarget = target.Id;
        w.WaterProgress = 0;
    }

    private bool FinishWaterAction(Worker w, float dt) {
        if (w.WaterAction == WorkerWaterAction.None) return false;
        if (Find(w.WaterTarget) is not Thing target) { StopWater(w); return false; }
        float duration = w.WaterAction switch {
            WorkerWaterAction.Fill => BarrelJobs.FillSeconds,
            WorkerWaterAction.Pour => BarrelJobs.PourSeconds,
            _ => BarrelJobs.ClimbSeconds
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

    private static void StopWater(Worker w) {
        w.WaterAction = WorkerWaterAction.None;
        w.WaterTarget = 0;
        w.WaterProgress = 0;
    }

    private void FillTool(Worker w, Thing source) {
        if (Data.VesselOf(w.Tool) is not VesselDef v || w.ToolSlot.Free(w.Tool) == 0) return;
        int held = v.LitersIn(w.Tool);
        int added = source.Kind == ThingKind.Water ? v.Capacity - held : Barrels.TakeWater(source, v.Capacity - held);
        w.Tool = v.States[held + added];
    }

    private void PourTool(Worker w, Thing target) {
        if (Data.VesselOf(w.Tool) is not VesselDef v || w.ToolSlot.Free(w.Tool) == 0) return;
        int held = v.LitersIn(w.Tool);
        int poured = barrels.AddWater(target, held);
        w.Tool = v.States[held - poured];
    }
}
