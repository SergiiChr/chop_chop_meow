using System;
using Godot;

namespace ChopChopMeow;

public enum GlassResult { None, Success, Melted }

/// <summary>Glass batches at a reinforced furnace with a glass blower at the front and a bellows operator behind.</summary>
internal sealed class GlassBlowing(World world) {
    internal const string Item = "glass_jar", Tool = "glass blowing pipe";
    private const string NeedPipe = "Needs a steel pipe in the Glass blowing pipe slot";
    internal const float Seconds = 10, ResultSeconds = 1, OverheatSeconds = .5f;
    private const float Reach = .45f;
    internal static readonly float MinHeat = World.FurnaceHeat(1300), MaxHeat = World.FurnaceHeat(1600);
    internal static readonly Vector2 BlowStand = new(0, 1.7f);

    internal static bool IsRecipe(Thing t) => t.Kind == ThingKind.Reinforced && t.Recipe == Item;
    internal static bool AtPump(World world, Thing t) => world.Near(t, Reach, t.ToWorld(World.BellowsStand));
    internal static bool InRange(float heat) => heat >= MinHeat && heat <= MaxHeat;

    /// <summary>The furnace whose bellows the player stands at, even when its page is closed.</summary>
    internal static Thing? PumpAtPlayer(World world) {
        Thing? closest = null;
        float distance = float.PositiveInfinity;
        foreach (var forge in world.ThingsOf(ThingKind.Reinforced)) {
            if (!IsRecipe(forge) || !AtPump(world, forge)) continue;
            float next = world.Player.DistanceSquaredTo(forge.ToWorld(World.BellowsStand));
            if (next < distance || next == distance && (closest == null || forge.Id < closest.Id)) {
                closest = forge;
                distance = next;
            }
        }
        return closest;
    }

    /// <summary>Whole degrees without rounding an out-of-range reading into the working range.</summary>
    internal static int Celsius(float heat) {
        float degrees = heat * 1450 / World.SteelHeat;
        return heat < MinHeat ? (int)MathF.Floor(degrees) : heat > MaxHeat ? (int)MathF.Ceiling(degrees) : (int)MathF.Round(degrees);
    }

    /// <summary>A finished batch leaves its pipe and pose in place for the end of the motion.</summary>
    internal bool Resolve(Thing forge, float dt) {
        // When the output was full, this is an already formed jar waiting to be put down.
        if (forge.Processing) world.FinishStationBatch(forge, world.Data.Recipes[Item]);
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
            // The blower already finished the jar. Only room to put it down is missing.
            world.FinishStationBatch(forge, recipe);
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
        if (forge.Progress < recipe.Seconds) return;
        world.FinishStationBatch(forge, recipe);
        Finish(forge, GlassResult.Success);
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
            if (forge.GlassBlower != -w.Id) { w.Status = "Waiting for the current glass batch to finish"; return false; }
            w.WorkState = WorkerWorkState.Blowing;
            w.Status = forge.GlassResult == GlassResult.Success ? "Glass jar finished" : "Glass melted";
            return true;
        }
        if (!IsRecipe(forge)) { w.Status = "Furnace is not making glass jars"; return false; }
        if (WorkBlock(forge) is string reason) { w.Status = reason; return false; }
        int other = Blower(forge);
        if (other != 0 && other != -w.Id) { w.Status = "The glass blowing pipe is in use"; return false; }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Blowing glass";
        if (world.Walk(w, forge.ToWorld(BlowStand), dt, .1f, exact: true)) w.WorkState = WorkerWorkState.Blowing;
        return true;
    }

    internal bool Pump(Worker w, Thing forge, float dt) {
        if (forge.GlassResult != GlassResult.None && forge.GlassBlower == -w.Id) { w.Status = "Finishing a glass batch"; return false; }
        if (WorkBlock(forge) is string reason) { w.Status = reason; return false; }
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
    /// <summary>Why a worker cannot start or continue at this furnace, or null when ready.</summary>
    private string? WorkBlock(Thing forge) {
        if (!HasPipe(forge)) return NeedPipe;
        if (forge.Processing) return forge.Progress < Seconds ? null : "Glass jar waiting for room in the output slot";
        if (world.Stocked(forge)) return null;
        if (!forge.Inventory.Has(world.BatchCost(forge))) return "Needs sand";
        return world.FuelOf(forge) == null ? "Needs coal in the fuel slot" : "Nothing to make";
    }

    private int Blower(Thing forge) {
        int player = PlayerAt(forge, BlowStand, still: true);
        return player != 0 ? player : WorkerAt(forge, JobKind.BlowGlass, WorkerWorkState.Blowing);
    }

    private int PlayerAt(Thing forge, Vector2 offset, bool still) {
        Vector2 spot = forge.ToWorld(offset);
        foreach (var p in world.Players) {
            if (p.SleepingAt != 0 || p.Sheltered != 0 || p.Door != Doorway.None || p.RespawnIn != 0 || p.Mauled) continue;
            bool canBlow = !p.Busy && p.Still >= World.CraftSettle;
            if (still && !canBlow) continue;
            var pos = new Vector2(p.PlayerX, p.PlayerY);
            float distance = pos.DistanceSquaredTo(spot);
            if (distance <= Reach * Reach && ClosestWorkSpot(forge, pos, distance, canBlow)) return p.Id;
        }
        return 0;
    }

    /// <summary>One player can operate only the nearest work spot, even when several furnaces are in reach.</summary>
    private bool ClosestWorkSpot(Thing forge, Vector2 pos, float distance, bool canBlow) {
        foreach (var other in world.ThingsOf(ThingKind.Reinforced)) {
            if (other == forge || other.GlassResult == GlassResult.None && !IsRecipe(other)) continue;
            float next = pos.DistanceSquaredTo(other.ToWorld(World.BellowsStand));
            if (canBlow) next = Math.Min(next, pos.DistanceSquaredTo(other.ToWorld(BlowStand)));
            if ((next < distance || next == distance && other.Id < forge.Id)
                && (other.GlassResult != GlassResult.None || WorkBlock(other) == null)) return false;
        }
        return true;
    }

    private int WorkerAt(Thing forge, JobKind job, WorkerWorkState action) {
        foreach (var w in world.Workers)
            if (w.Assignment == forge.Id && w.Doing == job && w.WorkState == action && w.WageDebt <= 0
                && world.OnDuty(w) && World.HasDuty(w, forge.Id, job)) return -w.Id;
        return 0;
    }

    internal static string Status(World world, Thing forge) {
        if (forge.Processing && forge.Progress >= Seconds) return "Glass jar finished · make room in the output slot";
        if (forge.GlassResult == GlassResult.Melted) return "Glass melted. Starting again with a fresh batch.";
        if (forge.GlassResult == GlassResult.Success) return "Glass jar finished";
        if (forge.Tools?["steel_pipe"] != 1) return "Put one steel pipe in the Glass blowing pipe slot.";
        if (forge.GlassBlower == 0 && !world.CanStandDry(forge.ToWorld(BlowStand))) return "Clear ground in front of the furnace for the glass blower.";
        if (forge.GlassBlower == 0 && !world.CanStandDry(forge.ToWorld(World.BellowsStand))) return "Clear ground behind the furnace for the bellows.";
        if (forge.Heat > MaxHeat) return "Too hot · stop pumping";
        if (forge.Heat < MinHeat) return "Too cold · pump the bellows";
        return forge.GlassBlower == 0 ? "Needs a glass blower and a bellows operator." : "Blowing glass";
    }

    internal static bool Valid(Thing forge) {
        if (forge.Kind != ThingKind.Reinforced)
            return forge.GlassBlower == 0 && forge.GlassOverheat == 0 && forge.GlassResult == GlassResult.None && forge.GlassResultLeft == 0;
        if (forge.Tools is not Inventory tools || tools.Slots != 1 || tools.StackSize != 1
            || tools.Count > 1 || tools.Count != tools["steel_pipe"]) return false;
        if (forge.GlassResult != GlassResult.None)
            return forge.GlassResult is GlassResult.Success or GlassResult.Melted && forge.GlassResultLeft is > 0 and <= ResultSeconds
                && forge.GlassOverheat == 0
                && (!forge.Processing || forge.GlassResult == GlassResult.Success && IsRecipe(forge) && forge.Progress == Seconds);
        if (forge.GlassResultLeft != 0) return false;
        return IsRecipe(forge) && forge.Processing
            ? forge.GlassOverheat is >= 0 and <= OverheatSeconds + .00001f && forge.Progress is >= 0 and <= Seconds
            : forge.GlassBlower == 0 && forge.GlassOverheat == 0;
    }
}
