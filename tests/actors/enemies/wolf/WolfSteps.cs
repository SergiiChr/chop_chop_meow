using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Steps that set up a night with wolves for the combat checks.</summary>
internal static class WolfSteps {
    /// <summary>A night with a steel axe in the backpack.</summary>
    public static World AxeNight(GameData data) {
        var world = NightWorld(data);
        Give(world, "steel_axe", 1);
        return world;
    }

    /// <summary>
    /// A wolf standing <paramref name="from"/> the player and facing them, held where it stands for five seconds as a
    /// howling wolf is, so a check can swing at a body that does not move.
    /// </summary>
    public static Wolf StillWolf(World world, Vector2 from) =>
        PlaceWolf(world, new Wolf { Pos = world.Player + from, Heading = (-from).Angle(), Howl = 5, HowlLength = 5 });

    /// <summary>Places a wolf already in flight toward the player from the given bearing.</summary>
    public static Wolf AirborneWolf(World world, float degrees) {
        var from = Vector2.FromAngle(Mathf.DegToRad(degrees));
        return PlaceWolf(world, new Wolf {
            Pos = world.Player + from * 2.6f,
            Heading = (-from).Angle(),
            SnapTo = world.Player + from * .59f,
            Leap = World.LeapSeconds
        });
    }

    /// <summary>The health one swing of a steel axe along <paramref name="aim"/> takes off a wolf standing still <paramref name="wolfAt"/> from the player.</summary>
    public static float TakenByAnAxe(GameData data, Vector2 wolfAt, Vector2 aim) {
        var world = AxeNight(data);
        var wolf = StillWolf(world, wolfAt);
        world.Attack(aim);
        world.Tick(.5f);
        return data.WolfHealth - wolf.Health;
    }

    /// <summary>Ticks a night in steps short enough to follow a snap and a throat leap.</summary>
    public static void SnapTick(World world, float seconds) {
        for (float t = 0; t < seconds - 1e-4f; t += SnapStep) { Nourished(world); world.Tick(SnapStep); }
    }
    private const float SnapStep = .02f;

    /// <summary>Walks in <paramref name="way"/> for the given seconds while the wolf has time to attack.</summary>
    public static void WalkThroughSnap(World world, Vector2 way, float seconds) {
        for (float t = 0; t < seconds - 1e-4f; t += SnapStep) {
            Nourished(world);
            world.MovePlayer(way * SnapStep * world.PlayerSpeed);
            world.Tick(SnapStep);
        }
    }

    /// <summary>A torch night with wolves in follow mode, settled on their circle; returns one of them.</summary>
    public static (World World, Wolf Wolf) Followed(GameData data, int count) {
        var world = TorchNight(data);
        var wolves = Enumerable.Range(0, count).Select(i => PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(World.StalkDistance, 0).Rotated(i * Mathf.Tau / count), Follow = FollowStage.Circling })).ToArray();
        WolfTick(world, 1, wolves);
        return (world, wolves[0]);
    }

    /// <summary>
    /// A torch night with wolves <paramref name="off"/> metres east of the player, a few degrees apart, that the player has
    /// just waved the torch at.
    /// </summary>
    public static (World World, Wolf[] Wolves) WavedAtUpClose(GameData data, int count, float off) {
        var world = TorchNight(data);
        var wolves = Enumerable.Range(0, count).Select(i => PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(off, 0).Rotated(Mathf.DegToRad(30 * i - 15 * (count - 1))) })).ToArray();
        world.UseTorch(Vector2.Right);
        return (world, wolves);
    }

    /// <summary>Puts the player a given distance from a wolf, on the line between them, as if they had walked at it.</summary>
    public static void WalkTo(World world, Wolf wolf, float distance) => world.Player = wolf.Pos + (world.Player - wolf.Pos).Normalized() * distance;

    /// <summary>Starts entering just as a wolf is about to bite at the doorstep.</summary>
    public static Wolf StepIntoBite(World world, Thing hut, Vector2 from, PlayerState? who = null) {
        who ??= world.Me;
        var wolf = PlaceWolf(world, new Wolf { Pos = who.Pos + from, Target = who.Id });
        SnapTick(world, who.Health <= World.TakedownHealth ? .64f : .5f);
        Require(WolfSnap.InAir(wolf), "a wolf should be in flight as the player starts entering");
        world.As(who, () => Require(world.Doorways.Enter(hut, false), "the player should step into the cabin during the leap"));
        return wolf;
    }

    /// <summary>A night with the player caught while stepping into a cabin.</summary>
    public static (World World, Thing Hut, Wolf Wolf) TuggedAtNight(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        var wolf = StepIntoBite(world, hut, Vector2.Right * .8f);
        UntilTugged(world);
        return (world, hut, wolf);
    }

    /// <summary>
    /// Ticks until a wolf has a player, the acting one by default, by the waist at a cabin's door, for 0.7 seconds at most:
    /// a wolf at biting distance gathers itself and leaps before its jaws take hold.
    /// </summary>
    public static void UntilTugged(World world, PlayerState? who = null) {
        who ??= world.Me;
        for (float time = 0; time < .7f && who.Door is not (Doorway.Tugged or Doorway.TuggedOff); time += .05f) world.Tick(.05f);
    }
}
