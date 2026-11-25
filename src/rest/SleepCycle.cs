using System;
using System.Diagnostics;

namespace ChopChopMeow;

/// <summary>Each player's sleep and the shared speed-up, which runs while all are asleep and no sleeper hears a wolf.</summary>
public sealed class SleepCycle(World world) {
    /// <summary>Hunger and thirst both above this let sleep heal.</summary>
    public const float FedNeed = 45;
    /// <summary>Health gained per second asleep while hunger and thirst are both above <see cref="FedNeed"/>.</summary>
    public const float HealPerSecond = .12f;
    private const float SnapshotRoom = .15f;
    private const string Scratching = "You are awakened by the noise outside.";
    private const int TargetRate = 12;
    private const float SleepStep = 1f / World.TicksPerSecond;
    private static readonly TimeSpan WorkBudget = TimeSpan.FromMilliseconds(4);
    private bool speedingUp;
    /// <summary>Whether the speed-up or the skip to dawn is running steps of its own: they stay silent and do not count as time played.</summary>
    internal bool AdvancingTime { get; private set; }
    internal bool Recovering { get; private set; }
    /// <summary>The place the acting player sleeps at, or zero while awake.</summary>
    public int SleepingAt => world.S.Actor.SleepingAt;
    internal bool OnStrawBed => world.Find(SleepingAt)?.Kind == ThingKind.StrawBed;

    private float Dawn => world.Day * world.Data.DaySeconds;
    public float SecondsToDawn => Dawn - world.Time;

    private float SecondsFed(float hunger, float thirst, float threshold) =>
        Math.Max(0, Math.Min((hunger - threshold) / world.Data.HungerPerSecond, (thirst - threshold) / world.Data.ThirstPerSecond));

    /// <summary>Needs at the next dawn or at the earlier moment a need runs out, if the player slept now.</summary>
    public (float Seconds, float Health, float Hunger, float Thirst) Preview(Thing place) {
        var p = world.S.Actor;
        bool cabin = p.Sheltered == place.Id && place.Kind == ThingKind.Hut;
        float seconds = SecondsToDawn;
        float thirst = cabin ? world.BedtimeWater.Preview(p, 0, fillBottles: false).Bedtime : p.Thirst;
        float life = place.Kind == ThingKind.StrawBed ? Math.Max(0, StrawBed.Left(world, place)) : seconds;
        float duration = Math.Min(Math.Min(seconds, life), SecondsFed(p.Hunger, thirst, 0));
        float loss = duration * world.Data.ThirstPerSecond;
        float thirstAtWake = cabin ? world.BedtimeWater.Preview(p, loss, duration == seconds || OthersAsleep(p)).Dawn
            : Math.Max(0, thirst - loss);
        return (duration, Healed(p, duration, thirst), Math.Max(0, p.Hunger - duration * world.Data.HungerPerSecond), thirstAtWake);
    }

    private float Healed(PlayerState p, float dt, float thirst) =>
        Math.Min(100, p.Health + Math.Min(dt, SecondsFed(p.Hunger, thirst, FedNeed)) * HealPerSecond);

    /// <summary>Why the acting player cannot go to sleep at this place right now.</summary>
    public string? Block(Thing place) {
        var p = world.S.Actor;
        if (world.Occupied) return "You are busy.";
        if (place.Kind == ThingKind.Hut) {
            if (p.Sheltered != place.Id) return "Enter the cabin first.";
            if (!place.Locked && world.DoorCheck.AtDoor(place.Id) != null) return Scratching;
        } else if (place.Kind == ThingKind.StrawBed) {
            if (p.Sheltered != 0 || !StrawBed.Reachable(world, place)) return "Walk closer to the straw bed.";
            foreach (var other in world.Players) if (other.SleepingAt == place.Id) return "Someone is sleeping here.";
        } else return "You need a cabin or a straw bed.";
        if (p.Hunger <= 0) return "You are too hungry to sleep.";
        if (p.Thirst <= 0 && world.BedtimeWater.Preview(p, 0).Bedtime <= 0) return "You are too thirsty to sleep.";
        return null;
    }

    /// <summary>Puts the acting player to sleep without waiting for anyone else.</summary>
    public void Start(Thing place) {
        if (world.Find(place.Id) != place) return;
        if (Block(place) is string reason) { world.Say(reason); return; }
        if (world.Forwarded(Verb.Sleep, place.Id)) return;
        var p = world.S.Actor;
        world.Release(); world.ResetGather(); GroundHoles.Clear(p);
        p.SleepingAt = place.Id;
        p.Sprinting = p.Braking = false;
        p.SleepReadyAt = place.Kind == ThingKind.StrawBed ? world.Time + StrawBed.LieDownSeconds + SnapshotRoom : world.Time;
        if (place.Kind == ThingKind.StrawBed) { p.Pos = place.Pos; p.Warp++; } else world.BedtimeWater.DrinkInTurn(p, place);
        p.HearsWolves = false;
        ListenForWolves(p);
    }

    /// <summary>Wakes the acting player on request.</summary>
    public void Wake() {
        if (SleepingAt == 0 || world.Forwarded(Verb.Wake)) return;
        WakeActor("You wake up.");
    }

    internal void Heal(float dt) {
        var p = world.S.Actor;
        if (p.SleepingAt != 0 && p.Health < 100) p.Health = Healed(p, dt, p.Thirst);
    }

    internal void WakeActor(string reason) {
        var p = world.S.Actor;
        if (p.SleepingAt == 0) return;
        if (world.Find(p.Sheltered) is Thing cabin) world.BedtimeWater.DrinkInTurn(p, cabin);
        p.SleepingAt = 0;
        world.Say(reason);
    }

    internal void WakeIn(Thing place, string reason) {
        foreach (var p in world.Players)
            if (p.SleepingAt == place.Id) using (world.ActAs(p)) WakeActor(reason);
    }

    /// <summary>Checks what sleepers hear and wakes them at dawn even if time ran normally.</summary>
    internal void AfterTick(bool dawn) {
        if (dawn) {
            if (!SomeoneAsleep()) return;
            var cabins = world.BedtimeWater.Occupants();
            if (!speedingUp && !Recovering) BedtimeWater.FillBottles(cabins);
            BedtimeWater.ServeThirst(cabins);
            foreach (var p in world.Players) p.SleepingAt = 0;
            AnnounceDawn();
        } else foreach (var p in world.Players) {
            if (p.SleepingAt == 0) continue;
            ListenForWolves(p);
            if (SleepingCabin(p) is { Locked: false } cabin && world.DoorCheck.AtDoor(cabin.Id) != null)
                using (world.ActAs(p)) WakeActor(Scratching);
        }
    }

    /// <summary>Outside a locked cabin, a wolf within sight radius can be heard in light or darkness.</summary>
    private void ListenForWolves(PlayerState sleeper) {
        if (SleepingCabin(sleeper) is { Locked: true }) { sleeper.HearsWolves = false; return; }
        using var _ = world.ActAs(sleeper);
        bool hears = false;
        foreach (var wolf in world.Wolves) if (world.Sees(wolf.Pos)) { hears = true; break; }
        if (hears && !sleeper.HearsWolves) world.Say("You hear something");
        sleeper.HearsWolves = hears;
    }

    private void AnnounceDawn() => world.SayToAll(world.Workers.Count > 0
        ? "Dawn breaks. Your workers are walking back from Bramblewick." : "Dawn breaks.");

    private bool SomeoneAsleep() {
        foreach (var p in world.Players) if (p.SleepingAt != 0) return true;
        return false;
    }

    private bool OthersAsleep(PlayerState player) {
        foreach (var p in world.Players) if (p != player && p.SleepingAt == 0) return false;
        return true;
    }

    private Thing? SleepingCabin(PlayerState sleeper) => sleeper.SleepingAt == sleeper.Sheltered ? world.Find(sleeper.Sheltered) : null;

    private bool CanAccelerate() {
        if (world.Players.Count == 0) return false;
        // Let the last sleeper finish lying down and leave time for the next server snapshot.
        foreach (var p in world.Players) if (p.SleepingAt == 0 || p.SleepReadyAt > world.Time || p.HearsWolves) return false;
        return true;
    }

    /// <summary>Adds ordinary simulation steps to a frame, leaving time for the next network update or draw.</summary>
    internal void Accelerate(float elapsed) {
        if (!CanAccelerate()) { speedingUp = false; return; }
        if (elapsed <= 0) return;
        long began = Stopwatch.GetTimestamp();
        if (!speedingUp) {
            BedtimeWater.FillBottles(world.BedtimeWater.Occupants());
            speedingUp = true;
        }
        // Do not accumulate work during a slow frame: do what fits now and leave the next frame its own budget.
        float extra = Math.Min(elapsed, SleepStep) * (TargetRate - 1);
        AdvancingTime = true;
        try {
            for (int i = 1; i < TargetRate && extra > .0001f && CanAccelerate()
                && Stopwatch.GetElapsedTime(began) < WorkBudget; i++) {
                float step = Math.Min(SleepStep, Math.Min(extra, SecondsToDawn));
                world.TickAcceleratedSleep(step);
                extra -= step;
            }
        } finally {
            AdvancingTime = false;
        }
        if (!CanAccelerate()) speedingUp = false;
    }

    /// <summary>Advances to dawn from the recovery screen when nobody is able to play.</summary>
    internal void SkipToDawnAfterDeath() {
        float dawn = Dawn;
        BedtimeWater.FillBottles(world.BedtimeWater.Occupants());
        Recovering = AdvancingTime = true;
        try {
            while (world.Time < dawn) {
                float dt = Math.Min(1, dawn - world.Time);
                world.Tick(dt, sleeping: true);
            }
        } finally {
            Recovering = AdvancingTime = false;
        }
        world.Look();
        AnnounceDawn();
    }
}
