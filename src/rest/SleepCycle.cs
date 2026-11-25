using System;

namespace ChopChopMeow;

/// <summary>Each player's sleep, and the shared fast-forward that starts only when everyone is asleep.</summary>
public sealed class SleepCycle(World world) {
    /// <summary>Health gained per second asleep while both needs are above the healing threshold.</summary>
    public const float FedNeed = 45, HealPerSecond = .12f;
    private const string Scratching = "You are awakened by the noise outside.";
    internal bool FastForwarding { get; private set; }
    public int Place => world.S.Actor.SleepingAt;

    private float Dawn => world.Day * world.Data.DaySeconds;
    public float SecondsToDawn => Dawn - world.Time;

    private float SecondsFed(float hunger, float thirst, float threshold) =>
        Math.Max(0, Math.Min((hunger - threshold) / world.Data.HungerPerSecond, (thirst - threshold) / world.Data.ThirstPerSecond));

    /// <summary>Needs at the next dawn or at the earlier moment a need runs out, if the player slept now.</summary>
    public (float Seconds, float Health, float Hunger, float Thirst) Preview(Thing place) {
        var p = world.S.Actor;
        bool cabin = p.Sheltered == place.Id && place.Kind == ThingKind.Hut;
        float seconds = SecondsToDawn;
        var water = cabin ? world.BedtimeWater.Preview(p, seconds * world.Data.ThirstPerSecond)
            : (p.Thirst, Math.Max(0, p.Thirst - seconds * world.Data.ThirstPerSecond));
        float thirst = p.SleepingAt == place.Id ? p.Thirst : water.Item1;
        float life = place.Kind == ThingKind.StrawBed ? Math.Max(0, StrawBed.Left(world, place)) : seconds;
        float duration = Math.Min(Math.Min(seconds, life), SecondsFed(p.Hunger, thirst, 0));
        return (duration, Healed(p, duration, thirst), Math.Max(0, p.Hunger - duration * world.Data.HungerPerSecond),
            duration == seconds ? water.Item2 : Math.Max(0, thirst - duration * world.Data.ThirstPerSecond));
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
            if (p.Sheltered != 0 || !world.Reaches(place)) return "Walk closer to the straw bed.";
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
        if (place.Kind == ThingKind.StrawBed) { p.Pos = place.Pos; p.Warp++; }
        else world.BedtimeWater.DrinkAtBedtime(p, place);
        FastForward();
    }

    /// <summary>Wakes the acting player on request.</summary>
    public void Wake() {
        if (Place == 0 || world.Forwarded(Verb.Wake)) return;
        WakeActor("You wake up.");
    }

    internal void Heal(float dt) {
        var p = world.S.Actor;
        if (p.SleepingAt != 0 && p.Health < 100) p.Health = Healed(p, dt, p.Thirst);
    }

    internal void WakeActor(string reason) {
        var p = world.S.Actor;
        if (p.SleepingAt == 0) return;
        if (world.Find(p.Sheltered) is Thing cabin) BedtimeWater.Drink(p, cabin);
        p.SleepingAt = 0;
        world.Say(reason);
    }

    internal void WakeIn(Thing place, string reason) {
        foreach (var p in world.Players)
            if (p.SleepingAt == place.Id) using (world.ActAs(p)) WakeActor(reason);
    }

    /// <summary>Checks for a wolf at a sleeper's door and wakes sleepers at dawn even if time ran normally.</summary>
    internal void AfterTick(bool dawn) {
        if (dawn) {
            if (!SomeoneAsleep()) return;
            var cabins = world.BedtimeWater.Occupants();
            if (!FastForwarding) BedtimeWater.FillBottles(cabins);
            BedtimeWater.ServeThirst(cabins);
            foreach (var p in world.Players) p.SleepingAt = 0;
            AnnounceDawn();
        } else foreach (var p in world.Players)
            if (p.SleepingAt != 0 && p.SleepingAt == p.Sheltered && world.Find(p.Sheltered) is { Locked: false }
                && world.DoorCheck.AtDoor(p.Sheltered) != null)
                using (world.ActAs(p)) WakeActor(Scratching);
    }

    private void AnnounceDawn() => world.SayToAll(world.Workers.Count > 0
        ? "Dawn breaks. Your workers are walking back from Bramblewick." : "Dawn breaks.");

    private bool SomeoneAsleep() {
        foreach (var p in world.Players) if (p.SleepingAt != 0) return true;
        return false;
    }

    private bool EveryoneAsleep() {
        if (world.Players.Count == 0) return false;
        foreach (var p in world.Players) if (p.SleepingAt == 0) return false;
        return true;
    }

    /// <summary>Runs the normal world toward dawn in one-second steps, stopping if anyone wakes.</summary>
    internal void FastForward(bool recovery = false) {
        if (FastForwarding || !recovery && !EveryoneAsleep()) return;
        float dawn = Dawn;
        BedtimeWater.FillBottles(world.BedtimeWater.Occupants());
        FastForwarding = true;
        try {
            while (world.Time < dawn && (recovery || EveryoneAsleep())) {
                float dt = Math.Min(1, dawn - world.Time);
                world.Tick(dt, sleeping: true);
            }
        } finally {
            FastForwarding = false;
        }
        world.Look();
        if (recovery && world.Time >= dawn) AnnounceDawn();
    }
}
