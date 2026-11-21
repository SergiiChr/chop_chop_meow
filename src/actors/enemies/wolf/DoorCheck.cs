using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Wolves in <paramref name="world"/> at the door of a cabin with someone inside. One that hunted a player who got inside,
/// or smells the cabin as it passes, comes to the door and scratches at it. It goes away from a locked door; an unlocked
/// one it pries open with its snout, looks in, launches at the last player who came in and takes them out by the waist,
/// which <see cref="DoorTug"/> carries on. Other wolves that come to the same door wait behind it and join in once the
/// player is out.
/// </summary>
public sealed class DoorCheck(World world) {
    /// <summary>Seconds a wolf scratches at a door, pries its edge, pushes it open, stares in and takes to launch itself at a player inside.</summary>
    public const float ScratchSeconds = 3.6f, PrySeconds = 1.5f, OpenSeconds = 2.5f, StareSeconds = 2, LaunchSeconds = .4f;
    /// <summary>How many pries a wolf levers a door open in, and the seconds into each at which the door rattles off its snout.</summary>
    public const int Pries = 3;
    public const float RattleFrom = .1f;
    /// <summary>How far out from a cabin's door point wolves wait behind the one at the door, and how far apart.</summary>
    public const float WaitBehind = 2.4f, WaitApart = .9f;
    /// <summary>How near its spot at a door, in metres, a wolf has arrived.</summary>
    private const float AtSpot = .15f;
    /// <summary>How far toward the free edge of a cabin's door from its door point a wolf stands to scratch at it, and how near that spot it stops.</summary>
    public const float ScratchAside = .2f;
    private const float AtDoorSpot = .05f;
    /// <summary>
    /// How far out from where it scratches a wolf comes round to before it walks straight up to the door, how far to a side
    /// of that line it may be to walk straight in, and the share of its trot it walks up at.
    /// </summary>
    private const float ApproachFrom = .9f, ApproachLane = .3f, ApproachPace = .45f;

    /// <summary>Seconds a stage of a wolf's door check lasts; a wolf comes for as long as the way takes.</summary>
    public static float Seconds(DoorCheckStage stage) => stage switch {
        DoorCheckStage.Scratching => ScratchSeconds,
        DoorCheckStage.Prying => PrySeconds,
        DoorCheckStage.Opening => OpenSeconds,
        DoorCheckStage.Staring => StareSeconds,
        DoorCheckStage.Launching => LaunchSeconds,
        DoorCheckStage.TakingOut => DoorTug.TugSeconds,
        _ => 0
    };

    /// <summary>The wolf at a cabin's door, from its first scratch until it has gone or let go of the player it took out, or null.</summary>
    public Wolf? AtDoor(int cabin) {
        foreach (var w in world.S.Wolves) if (w.DoorCheck >= DoorCheckStage.Scratching && w.DoorCheckCabin == cabin) return w;
        return null;
    }

    /// <summary>Whether a wolf has its snout in a cabin's door or is further in, when the key no longer turns and nobody steps in past it.</summary>
    public bool Forcing(int cabin) => AtDoor(cabin) is { DoorCheck: >= DoorCheckStage.Prying };

    /// <summary>
    /// Whether a wolf is at an unlocked door, from its first scratch until it has gone or let go of whom it took out.
    /// The first scratch wakes whoever sleeps, and nobody sleeps while the wolf is there.
    /// </summary>
    public bool Waking {
        get {
            foreach (var w in world.S.Wolves)
                if (w.DoorCheck >= DoorCheckStage.Scratching && world.Find(w.DoorCheckCabin) is { Locked: false }) return true;
            return false;
        }
    }

    /// <summary>Whether a cabin draws wolves to its door: someone is inside, and no campfire lights the door.</summary>
    private bool Draws(Thing cabin) => world.Doorways.Inside(cabin.Id) > 0 && !world.Protected(Doorways.DoorPoint(cabin));

    /// <summary>Whether someone is inside a cabin or stepping into it.</summary>
    private bool Occupied(int cabin) {
        foreach (var p in world.S.Players) if (p.Sheltered == cabin || p.DoorCabin == cabin && p.Door == Doorway.Entering) return true;
        return false;
    }

    /// <summary>
    /// A wolf hunting nobody that comes within smell of a cabin with someone inside goes to its door, without a howl; returns
    /// whether it did. It leaves alone each cabin whose door it gave up on, until it has been out of smell range of that one.
    /// </summary>
    public bool Smell(Wolf wolf) {
        float radius = world.Data.SmellRadius;
        for (int i = wolf.Tried.Count - 1; i >= 0; i--)
            if (world.Find(wolf.Tried[i]) is not Thing left || left.Pos.DistanceTo(wolf.Pos) > radius) wolf.Tried.RemoveAt(i);
        Thing? nearest = null;
        foreach (var t in world.S.Things.Of(ThingKind.Hut))
            if (!wolf.Tried.Contains(t.Id) && t.Pos.DistanceTo(wolf.Pos) <= radius && Draws(t) && (nearest == null || t.Pos.DistanceSquaredTo(wolf.Pos) < nearest.Pos.DistanceSquaredTo(wolf.Pos))) nearest = t;
        if (nearest == null) return false;
        Start(wolf, nearest);
        return true;
    }

    /// <summary>Sends a wolf whose player got inside a cabin to its door; false for a cabin it leaves alone or one a campfire lights.</summary>
    public bool Follow(Wolf wolf, Thing? cabin) {
        if (cabin == null || cabin.Kind != ThingKind.Hut || wolf.Tried.Contains(cabin.Id) || world.Protected(Doorways.DoorPoint(cabin))) return false;
        Start(wolf, cabin);
        return true;
    }

    private static void Start(Wolf wolf, Thing cabin) {
        WolfPatrols.Patrol(wolf, wolf.Heading);
        wolf.DoorCheck = DoorCheckStage.Coming; wolf.DoorCheckCabin = cabin.Id;
    }

    /// <summary>Moves a wolf's door check on a stage.</summary>
    private static void Stage(Wolf wolf, DoorCheckStage stage) { wolf.DoorCheck = stage; wolf.DoorCheckTime = 0; }

    /// <summary>The wolf whose turn it is at a cabin's door: the one at it, else the nearest one coming.</summary>
    private Wolf? Lead(int cabin, Vector2 door) {
        Wolf? lead = null;
        foreach (var w in world.S.Wolves) {
            if (w.DoorCheckCabin != cabin || w.DoorCheck == DoorCheckStage.None) continue;
            if (w.DoorCheck >= DoorCheckStage.Scratching) return w;
            if (lead == null || w.Pos.DistanceSquaredTo(door) < lead.Pos.DistanceSquaredTo(door)) lead = w;
        }
        return lead;
    }

    /// <summary>Where a wolf waits behind the one at a cabin's door: out from the door, each next one further to a side.</summary>
    private Vector2 WaitSpot(Wolf wolf, Wolf lead, Vector2 door, Vector2 outward) {
        int before = 0;
        foreach (var w in world.S.Wolves) {
            if (w == wolf) break;
            if (w != lead && w.DoorCheck == DoorCheckStage.Coming && w.DoorCheckCabin == wolf.DoorCheckCabin) before++;
        }
        // Seen facing the door, the first waits to the left, the next to the right, the next further left, and so on.
        float aside = ((before + 1) / 2 + .5f) * WaitApart;
        return door + outward * WaitBehind + BodySide.Out(-outward, before % 2 == 0 ? BodySide.Left : BodySide.Right) * aside;
    }

    /// <summary>Moves a wolf's door check at a cabin on by <paramref name="dt"/> seconds.</summary>
    public void Tick(Wolf wolf, float dt) {
        if (world.Find(wolf.DoorCheckCabin) is not { Kind: ThingKind.Hut } cabin || !Occupied(cabin.Id) || world.Protected(Doorways.DoorPoint(cabin)) || wolf.Stagger > 0) { End(wolf); return; }
        var door = Doorways.DoorPoint(cabin);
        // Until its snout is in the door, a wolf turns to a player out in the open within its sight.
        if (wolf.DoorCheck <= DoorCheckStage.Scratching && world.WolfPatrols.Smell(wolf, world.Data.WolfSightRange)) return;
        float before = wolf.DoorCheckTime, step = dt * world.Data.WolfTrot;
        switch (wolf.DoorCheck) {
            case DoorCheckStage.Coming:
                var lead = Lead(cabin.Id, door);
                var outward = Doorways.OutOfDoor.Rotated(cabin.Rotation);
                if (lead == wolf) {
                    // It comes round to face the door, then walks straight up to it, so it gets there facing it and at a walk.
                    var asideWay = Doorways.AsideWay.Rotated(cabin.Rotation);
                    var scratch = door + asideWay * ScratchAside;
                    float aside = (wolf.Pos - scratch).Dot(asideWay), outFrom = (wolf.Pos - scratch).Dot(outward);
                    // With no way to the door it gives up on the cabin.
                    if (Math.Abs(aside) < ApproachLane && outFrom > 0 && outFrom < ApproachFrom + ApproachLane) {
                        if (!world.Travel(wolf, scratch, step * ApproachPace, step * ApproachPace)) GiveUp(wolf, cabin);
                        else if (wolf.Pos.DistanceTo(scratch) < AtDoorSpot) Stage(wolf, DoorCheckStage.Scratching);
                    } else if (!world.Travel(wolf, scratch + outward * ApproachFrom, step, step)) GiveUp(wolf, cabin);
                } else if (lead != null && WaitSpot(wolf, lead, door, outward) is var spot && wolf.Pos.DistanceTo(spot) >= AtSpot && world.CanStand(spot)) world.Travel(wolf, spot, step, step);
                break;
            case DoorCheckStage.Scratching:
                if (before == 0) world.Play("door_scratch", door);
                wolf.DoorCheckTime += dt;
                if (wolf.DoorCheckTime < ScratchSeconds) break;
                if (cabin.Locked) GoAway(cabin); else Stage(wolf, DoorCheckStage.Prying);
                break;
            case DoorCheckStage.Prying or DoorCheckStage.Opening or DoorCheckStage.Staring or DoorCheckStage.Launching:
                if (wolf.DoorCheck == DoorCheckStage.Staring && before == 0) world.Play("wolf_growl_approaching", door);
                wolf.DoorCheckTime += dt;
                if (wolf.DoorCheck == DoorCheckStage.Prying && Rattled(wolf.DoorCheckTime) > Rattled(before)) world.Play("door_rattle", door);
                if (wolf.DoorCheckTime < Seconds(wolf.DoorCheck)) break;
                if (wolf.DoorCheck == DoorCheckStage.Launching) TakeOut(wolf, cabin); else Stage(wolf, wolf.DoorCheck + 1);
                break;
        }
    }

    /// <summary>Which pry a wolf is in this many seconds into its prying, from 0, and how far through it, 0 to 1.</summary>
    public static (int Pry, float Through) PryPhase(float prying) {
        float pries = Math.Clamp(prying / PrySeconds, 0, 1) * Pries, pry = MathF.Min(MathF.Floor(pries), Pries - 1);
        return ((int)pry, pries - pry);
    }

    /// <summary>How many times the door has rattled this many seconds into a wolf's prying: once in each pry.</summary>
    private static int Rattled(float prying) {
        var (pry, through) = PryPhase(prying);
        return pry + (through * PrySeconds / Pries >= RattleFrom ? 1 : 0);
    }

    /// <summary>Ends a wolf's door check: it goes back to patrolling, the way it was heading.</summary>
    private static void End(Wolf wolf) => WolfPatrols.Patrol(wolf, wolf.Heading);

    /// <summary>Sends every wolf at a locked cabin's door away from it.</summary>
    private void GoAway(Thing cabin) {
        foreach (var w in world.S.Wolves)
            if (w.DoorCheck != DoorCheckStage.None && w.DoorCheckCabin == cabin.Id) GiveUp(w, cabin);
    }

    /// <summary>A wolf gives up on a cabin's door and walks on away from it; it leaves that cabin alone until it has been out of smell range of it.</summary>
    private static void GiveUp(Wolf wolf, Thing cabin) {
        var away = wolf.Pos - cabin.Pos;
        WolfPatrols.Patrol(wolf, (away == Vector2.Zero ? Doorways.OutOfDoor.Rotated(cabin.Rotation) : away).Angle());
        if (!wolf.Tried.Contains(cabin.Id)) wolf.Tried.Add(cabin.Id);
    }

    /// <summary>
    /// A wolf that launched itself into a cabin takes the last player who came in by the waist and out through the door,
    /// biting as it takes hold; the wolves waiting behind it go for that player too. With nobody inside, it gives up.
    /// </summary>
    private void TakeOut(Wolf wolf, Thing cabin) {
        PlayerState? victim = null;
        foreach (var p in world.S.Players) if (p.Sheltered == cabin.Id && (victim == null || p.DoorQueued > victim.DoorQueued)) victim = p;
        if (victim == null) { End(wolf); return; }
        // Every wolf at the door drops its door check for the hunt; the one that takes the player out goes on to its last stage.
        foreach (var w in world.S.Wolves) {
            if (w.DoorCheck == DoorCheckStage.None || w.DoorCheckCabin != cabin.Id) continue;
            WolfPatrols.Patrol(w, w.Heading);
            w.Target = victim.Id;
        }
        using (world.ActAs(victim)) {
            victim.Sheltered = 0; world.Player = Doorways.DoorPoint(cabin); victim.Warp++;
            victim.Health -= world.Data.WolfBite;
            world.Play("wolf_bite", world.Player);
            world.DoorTug.Begin(wolf, Doorway.Tugged, cabin.Id);
        }
        wolf.DoorCheck = DoorCheckStage.TakingOut; wolf.DoorCheckCabin = cabin.Id; wolf.Attack = 1;
    }
}
