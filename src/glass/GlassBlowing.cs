using System;
using Godot;

namespace ChopChopMeow;

public enum GlassResult { None, Success, Melted }

/// <summary>A blowpipe at the front of the reinforced furnace, worked with someone tending heat at the rear.</summary>
internal sealed class GlassBlowing(World world) {
    internal const string Item = "glass_jar", Tool = "glass blower";
    internal const float Seconds = 10, ResultSeconds = 1, OverheatSeconds = .5f;
    private const float Reach = .45f;
    internal static readonly float MinHeat = World.FurnaceHeat(1300), MaxHeat = World.FurnaceHeat(1600);
    internal static readonly Vector2 BlowStand = new(0, 1.7f);

    internal static bool IsRecipe(Thing t) => t.Kind == ThingKind.Reinforced && t.Recipe == Item;
    internal static bool AtPump(World world, Thing t) => world.Near(t, Reach, t.ToWorld(World.BellowsStand));
    internal static bool InRange(float heat) => heat >= MinHeat && heat <= MaxHeat;

    /// <summary>The furnace whose bellows the player stands at, even when its page is closed.</summary>
    internal static Thing? PumpAtPlayer(World world) {
        foreach (var forge in world.ThingsOf(ThingKind.Reinforced))
            if (IsRecipe(forge) && AtPump(world, forge)) return forge;
        return null;
    }

    /// <summary>Whole degrees without rounding an out-of-range reading into the working range.</summary>
    internal static int Celsius(float heat) {
        float degrees = heat * 1450 / World.SteelHeat;
        return heat < MinHeat ? (int)MathF.Floor(degrees) : heat > MaxHeat ? (int)MathF.Ceiling(degrees) : (int)MathF.Round(degrees);
    }

    /// <summary>A finished batch leaves its pipe and pose in place for the end of the motion.</summary>
    internal bool Resolve(Thing forge, float dt) {
        forge.GlassResultLeft = Math.Max(0, forge.GlassResultLeft - dt);
        if (forge.GlassResultLeft > .00001f) {
            if (forge.GlassBlower != 0 && Blower(forge) != forge.GlassBlower) forge.GlassBlower = 0;
            return true;
        }
        forge.GlassResultLeft = 0;
        forge.GlassResult = GlassResult.None;
        forge.GlassBlower = 0;
        return false;
    }

    internal void Tick(Thing forge, RecipeDef recipe, float dt, bool wasBurning) {
        if (forge.Processing && forge.Progress >= recipe.Seconds) {
            if (world.FinishStationBatch(forge, recipe)) Finish(forge, GlassResult.Success);
            return;
        }
        if (!HasPipe(forge) || !forge.Processing && !world.Stocked(forge)) {
            forge.GlassBlower = 0;
            if (!forge.Processing) return;
        } else {
            forge.GlassBlower = Blower(forge);
        }
        bool paired = forge.GlassBlower != 0 && (PlayerAt(forge, World.BellowsStand, still: false) != 0 || WorkerAt(forge, JobKind.Bellows, WorkerWorkState.Pumping) != 0);
        if (!forge.Processing) {
            if (!paired || !InRange(forge.Heat)) { forge.GlassBlower = 0; return; }
            world.StartStationBatch(forge, recipe, wasBurning);
        }
        if (forge.Heat > MaxHeat) {
            forge.GlassOverheat += dt;
            if (forge.GlassOverheat > OverheatSeconds + .00001f) Melt(forge);
            return;
        }
        forge.GlassOverheat = 0;
        if (!paired) forge.GlassBlower = 0;
        if (!paired || forge.Heat < MinHeat) return;
        forge.Progress = Math.Min(recipe.Seconds, forge.Progress + dt);
        if (forge.Progress >= recipe.Seconds && world.FinishStationBatch(forge, recipe)) Finish(forge, GlassResult.Success);
    }

    /// <summary>The sand and fuel are already spent. A lost batch still owes the jar that was queued.</summary>
    private static void Melt(Thing forge) {
        if (forge.Batch == BatchFor.Queue) forge.Batches = Math.Min(World.MaxQueued, forge.Batches + 1);
        forge.Processing = false;
        forge.Making = "";
        forge.Progress = 0;
        forge.Rounds++;
        Finish(forge, GlassResult.Melted);
    }

    private static void Finish(Thing forge, GlassResult result) {
        forge.GlassOverheat = 0;
        forge.GlassResult = result;
        forge.GlassResultLeft = ResultSeconds;
    }

    internal bool Blow(Worker w, Thing forge, float dt) {
        if (forge.GlassResult != GlassResult.None) {
            if (forge.GlassBlower != -w.Id) return false;
            w.WorkState = WorkerWorkState.Blowing;
            w.Status = forge.GlassResult == GlassResult.Success ? "Glass jar finished" : "Glass melted";
            return true;
        }
        if (!IsRecipe(forge) || !ReadyToWork(forge)) return false;
        int other = Blower(forge);
        if (other != 0 && other != -w.Id) { w.Status = "The blowpipe is in use"; return false; }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Blowing glass";
        if (world.Walk(w, forge.ToWorld(BlowStand), dt, .1f, exact: true)) w.WorkState = WorkerWorkState.Blowing;
        return true;
    }

    internal bool Pump(Worker w, Thing forge, float dt) {
        if (!ReadyToWork(forge) || forge.GlassResult != GlassResult.None && forge.GlassBlower == -w.Id) return false;
        int other = WorkerAt(forge, JobKind.Bellows, WorkerWorkState.Pumping);
        if (PlayerAt(forge, World.BellowsStand, still: false) != 0 || other != 0 && other != -w.Id) {
            w.Status = "Bellows are manned";
            return false;
        }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Operating bellows";
        if (world.Walk(w, forge.ToWorld(World.BellowsStand), dt, .1f, exact: true)) {
            w.WorkState = WorkerWorkState.Pumping;
            // The bellows operator brings the glass to the middle of its working range and lets it cool back down when too hot.
            if (forge.Heat < World.SteelHeat) forge.Heat = Math.Min(World.SteelHeat, forge.Heat + dt * 18);
        }
        return true;
    }

    private bool HasPipe(Thing forge) => world.BenchTool(forge, Tool) != null;
    private bool ReadyToWork(Thing forge) => HasPipe(forge) && (forge.Processing && forge.Progress < Seconds || !forge.Processing && world.Stocked(forge));

    private int Blower(Thing forge) {
        int player = PlayerAt(forge, BlowStand, still: true);
        return player != 0 ? player : WorkerAt(forge, JobKind.BlowGlass, WorkerWorkState.Blowing);
    }

    private int PlayerAt(Thing forge, Vector2 offset, bool still) {
        Vector2 spot = forge.ToWorld(offset);
        foreach (var p in world.Players)
            if (p.Sheltered == 0 && p.Door == Doorway.None && p.RespawnIn == 0 && !p.Mauled
                && (!still || !p.Busy && p.Still >= World.CraftSettle)
                && new Vector2(p.PlayerX, p.PlayerY).DistanceSquaredTo(spot) <= Reach * Reach) return p.Id;
        return 0;
    }

    private int WorkerAt(Thing forge, JobKind job, WorkerWorkState action) {
        foreach (var w in world.Workers)
            if (w.Assignment == forge.Id && w.Doing == job && w.WorkState == action && w.WageDebt <= 0
                && world.OnDuty(w) && World.HasDuty(w, forge.Id, job)) return -w.Id;
        return 0;
    }

    internal static string Status(Thing forge) {
        if (forge.GlassResult == GlassResult.Melted) return "Glass melted. Starting again with a fresh batch.";
        if (forge.GlassResult == GlassResult.Success) return "Glass jar finished";
        if (forge.Tools?["steel_pipe"] != 1) return "Put one steel pipe in the Glass blower slot.";
        if (forge.Heat > MaxHeat) return "Too hot · stop pumping";
        if (forge.Heat < MinHeat) return "Too cold · pump the bellows";
        if (forge.GlassBlower == 0) return "Needs a blower and a bellows operator.";
        return "Blowing glass";
    }

    internal static bool Valid(Thing forge) {
        if (forge.Kind != ThingKind.Reinforced)
            return forge.GlassBlower == 0 && forge.GlassOverheat == 0 && forge.GlassResult == GlassResult.None && forge.GlassResultLeft == 0;
        if (forge.Tools is not Inventory tools || tools.Slots != 1 || tools.StackSize != 1
            || tools.Count > 1 || tools.Count != tools["steel_pipe"]) return false;
        if (forge.GlassResult != GlassResult.None)
            return forge.GlassResult is GlassResult.Success or GlassResult.Melted && forge.GlassResultLeft is > 0 and <= ResultSeconds
                && !forge.Processing && forge.GlassOverheat == 0;
        if (forge.GlassResultLeft != 0) return false;
        return IsRecipe(forge) && forge.Processing
            ? forge.GlassOverheat is >= 0 and <= OverheatSeconds + .00001f && forge.Progress is >= 0 and <= Seconds
            : forge.GlassBlower == 0 && forge.GlassOverheat == 0;
    }
}
