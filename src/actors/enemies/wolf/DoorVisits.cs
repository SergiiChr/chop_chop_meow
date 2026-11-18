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
public sealed class DoorVisits(World world) {
    /// <summary>Seconds a wolf scratches at a door, pries its edge, pushes it open, stares in and takes to launch itself at a player inside.</summary>
    public const float ScratchSeconds = 3.6f, PrySeconds = 1.5f, OpenSeconds = 2.5f, StareSeconds = 2, LaunchSeconds = .4f;
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

    /// <summary>Seconds a stage of a wolf's visit to a door lasts; a wolf comes for as long as the way takes.</summary>
    public static float Seconds(WolfVisit stage) => stage switch {
        WolfVisit.Scratching => ScratchSeconds,
        WolfVisit.Prying => PrySeconds,
        WolfVisit.Opening => OpenSeconds,
        WolfVisit.Staring => StareSeconds,
        WolfVisit.Launching => LaunchSeconds,
        WolfVisit.TakingOut => DoorTug.TugSeconds,
        _ => 0
    };

    /// <summary>The wolf at a cabin's door, from its first scratch until it has gone or let go of the player it took out, or null.</summary>
    public Wolf? AtDoor(int cabin) {
        foreach (var w in world.S.Wolves) if (w.Visit >= WolfVisit.Scratching && w.VisitCabin == cabin) return w;
        return null;
    }

    /// <summary>Whether a wolf has its snout in a cabin's door or is further in, when the key no longer turns and nobody steps in past it.</summary>
    public bool Forcing(int cabin) => AtDoor(cabin) is { Visit: >= WolfVisit.Prying };

    /// <summary>Whether a wolf scratches at an unlocked door, which wakes whoever sleeps behind it.</summary>
    public bool Waking {
        get {
            foreach (var w in world.S.Wolves)
                if (w.Visit == WolfVisit.Scratching && world.Find(w.VisitCabin) is { Locked: false }) return true;
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
    /// whether it did. It leaves alone the cabin whose locked door it last left, until it has been out of smell range of it.
    /// </summary>
    public bool Smell(Wolf wolf) {
        float radius = world.Data.SmellRadius;
        if (wolf.Tried != 0 && (world.Find(wolf.Tried) is not Thing left || left.Pos.DistanceTo(wolf.Pos) > radius)) wolf.Tried = 0;
        Thing? nearest = null;
        foreach (var t in world.S.Things.Of(ThingKind.Hut))
            if (t.Id != wolf.Tried && t.Pos.DistanceTo(wolf.Pos) <= radius && Draws(t) && (nearest == null || t.Pos.DistanceSquaredTo(wolf.Pos) < nearest.Pos.DistanceSquaredTo(wolf.Pos))) nearest = t;
        if (nearest == null) return false;
        Start(wolf, nearest);
        return true;
    }

    /// <summary>Sends a wolf whose player got inside a cabin to its door; false for a cabin it leaves alone or one a campfire lights.</summary>
    public bool Follow(Wolf wolf, Thing? cabin) {
        if (cabin == null || cabin.Kind != ThingKind.Hut || cabin.Id == wolf.Tried || world.Protected(Doorways.DoorPoint(cabin))) return false;
        Start(wolf, cabin);
        return true;
    }

    private static void Start(Wolf wolf, Thing cabin) {
        WolfPatrols.Patrol(wolf, wolf.Heading);
        wolf.Visit = WolfVisit.Coming; wolf.VisitCabin = cabin.Id;
    }

    /// <summary>Moves a wolf's visit on a stage.</summary>
    private static void Stage(Wolf wolf, WolfVisit stage) { wolf.Visit = stage; wolf.VisitTime = 0; }

    /// <summary>The wolf whose turn it is at a cabin's door: the one at it, else the nearest one coming.</summary>
    private Wolf? Lead(int cabin, Vector2 door) {
        Wolf? lead = null;
        foreach (var w in world.S.Wolves) {
            if (w.VisitCabin != cabin || w.Visit == WolfVisit.None) continue;
            if (w.Visit >= WolfVisit.Scratching) return w;
            if (lead == null || w.Pos.DistanceSquaredTo(door) < lead.Pos.DistanceSquaredTo(door)) lead = w;
        }
        return lead;
    }

    /// <summary>Where a wolf waits behind the one at a cabin's door: out from the door, each next one further to a side.</summary>
    private Vector2 WaitSpot(Wolf wolf, Wolf lead, Vector2 door) {
        int before = 0;
        foreach (var w in world.S.Wolves) {
            if (w == wolf) break;
            if (w != lead && w.Visit == WolfVisit.Coming && w.VisitCabin == wolf.VisitCabin) before++;
        }
        // The first waits to the left, the next to the right, the next further left, and so on.
        float aside = ((before + 1) / 2 + .5f) * WaitApart * (before % 2 == 0 ? -1 : 1);
        return door + Doorways.OutOfDoor * WaitBehind + Doorways.AsideWay * aside;
    }

    /// <summary>Moves a wolf's visit to a cabin's door on by <paramref name="dt"/> seconds.</summary>
    public void Tick(Wolf wolf, float dt) {
        if (world.Find(wolf.VisitCabin) is not { Kind: ThingKind.Hut } cabin || !Occupied(cabin.Id) || world.Protected(Doorways.DoorPoint(cabin)) || wolf.Stagger > 0) { End(wolf); return; }
        var door = Doorways.DoorPoint(cabin);
        // Until its snout is in the door, a wolf turns to a player out in the open within its sight.
        if (wolf.Visit <= WolfVisit.Scratching && world.WolfPatrols.Smell(wolf, world.Data.WolfSightRange)) return;
        float before = wolf.VisitTime, step = dt * world.Data.WolfTrot;
        switch (wolf.Visit) {
            case WolfVisit.Coming:
                var lead = Lead(cabin.Id, door);
                if (lead == wolf) {
                    // It comes round to face the door, then walks straight up to it, so it gets there facing it and at a walk.
                    var scratch = door + Doorways.AsideWay * ScratchAside;
                    float aside = (wolf.Pos - scratch).Dot(Doorways.AsideWay), outFrom = (wolf.Pos - scratch).Dot(Doorways.OutOfDoor);
                    if (Math.Abs(aside) < ApproachLane && outFrom > 0 && outFrom < ApproachFrom + ApproachLane) {
                        world.Travel(wolf, scratch, step * ApproachPace, step * ApproachPace);
                        if (wolf.Pos.DistanceTo(scratch) < AtDoorSpot) Stage(wolf, WolfVisit.Scratching);
                    } else world.Travel(wolf, scratch + Doorways.OutOfDoor * ApproachFrom, step, step);
                } else if (lead != null && WaitSpot(wolf, lead, door) is var spot && wolf.Pos.DistanceTo(spot) >= AtSpot && world.CanStand(spot)) world.Travel(wolf, spot, step, step);
                break;
            case WolfVisit.Scratching:
                if (before == 0) world.Play("door_scratch", door);
                wolf.VisitTime += dt;
                if (wolf.VisitTime < ScratchSeconds) break;
                if (cabin.Locked) GoAway(cabin); else Stage(wolf, WolfVisit.Prying);
                break;
            case WolfVisit.Prying or WolfVisit.Opening or WolfVisit.Staring or WolfVisit.Launching:
                if (wolf.Visit == WolfVisit.Staring && before == 0) world.Play("wolf_growl_approaching", door);
                wolf.VisitTime += dt;
                if (wolf.VisitTime < Seconds(wolf.Visit)) break;
                if (wolf.Visit == WolfVisit.Launching) TakeOut(wolf, cabin); else Stage(wolf, wolf.Visit + 1);
                break;
        }
    }

    /// <summary>Ends a wolf's visit to a door: it goes back to patrolling, the way it was heading.</summary>
    private static void End(Wolf wolf) => WolfPatrols.Patrol(wolf, wolf.Heading);

    /// <summary>Sends every wolf at a locked cabin's door away from it; each leaves that cabin alone until it has been out of smell range of it.</summary>
    private void GoAway(Thing cabin) {
        foreach (var w in world.S.Wolves) {
            if (w.Visit == WolfVisit.None || w.VisitCabin != cabin.Id) continue;
            var away = w.Pos - cabin.Pos;
            WolfPatrols.Patrol(w, (away == Vector2.Zero ? Doorways.OutOfDoor : away).Angle());
            w.Tried = cabin.Id;
        }
    }

    /// <summary>
    /// A wolf that launched itself into a cabin takes the last player who came in by the waist and out through the door,
    /// biting as it takes hold; the wolves waiting behind it go for that player too. With nobody inside, it gives up.
    /// </summary>
    private void TakeOut(Wolf wolf, Thing cabin) {
        PlayerState? victim = null;
        foreach (var p in world.S.Players) if (p.Sheltered == cabin.Id && (victim == null || p.DoorQueued > victim.DoorQueued)) victim = p;
        if (victim == null) { End(wolf); return; }
        using (world.ActAs(victim)) {
            victim.Sheltered = 0; world.Player = Doorways.DoorPoint(cabin); victim.Warp++;
            Doorways.Stage(victim, Doorway.Tugged, cabin.Id);
            victim.Health -= world.Data.WolfBite;
            world.Play("wolf_bite", world.Player); world.Play("tug_scream", world.Player);
        }
        foreach (var w in world.S.Wolves) {
            if (w.Visit == WolfVisit.None || w.VisitCabin != cabin.Id) continue;
            WolfPatrols.Patrol(w, w.Heading);
            w.Target = victim.Id;
        }
        wolf.Visit = WolfVisit.TakingOut; wolf.VisitCabin = cabin.Id; wolf.Tugging = true; wolf.Attack = 1;
    }
}
