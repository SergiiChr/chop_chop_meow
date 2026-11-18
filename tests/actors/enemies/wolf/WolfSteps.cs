using System.Linq;
using ChopChopMeow;
using Godot;
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

    /// <summary>The health one swing of a steel axe along <paramref name="aim"/> takes off a wolf standing still <paramref name="wolfAt"/> from the player.</summary>
    public static float TakenByAnAxe(GameData data, Vector2 wolfAt, Vector2 aim) {
        var world = AxeNight(data);
        var wolf = StillWolf(world, wolfAt);
        world.Attack(aim);
        world.Tick(.5f);
        return data.WolfHealth - wolf.Health;
    }

    /// <summary>Ticks a night in steps short enough to follow a snap, 0.08 seconds of which is in the air.</summary>
    public static void SnapTick(World world, float seconds) {
        for (float t = 0; t < seconds - 1e-4f; t += SnapStep) { Nourished(world); world.Tick(SnapStep); }
    }
    private const float SnapStep = .02f;

    /// <summary>
    /// The player's health after one snap of a wolf running beside them: they walk or sprint east with the wolf at biting
    /// distance behind, and with <paramref name="turns"/> turn north as it leaves the ground.
    /// </summary>
    public static float HealthAfterASnap(GameData data, bool sprinting, bool turns) {
        var world = NightWorld(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-1.05f, 0) });
        var way = Vector2.Right;
        world.Me.Sprinting = sprinting;
        for (float t = 0; t < 1 && (t == 0 || wolf.Snap > 0); t += SnapStep) {
            if (turns && WolfSnap.InAir(wolf)) way = Vector2.Up;
            Nourished(world); world.MovePlayer(way * SnapStep * data.MoveSpeed(sprinting)); world.Tick(SnapStep);
        }
        Checks.Require(wolf.Snap == 0 && wolf.Attack > 0, "the wolf should have snapped once within a second");
        return world.Health;
    }

    /// <summary>A torch night with wolves in follow mode, settled on their circle; returns one of them.</summary>
    public static (World World, Wolf Wolf) Followed(GameData data, int count) {
        var world = TorchNight(data);
        var wolves = Enumerable.Range(0, count).Select(i => PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(World.StalkDistance, 0).Rotated(i * Mathf.Tau / count), Follow = FollowStage.Circling })).ToArray();
        WolfTick(world, 1, wolves);
        return (world, wolves[0]);
    }

    /// <summary>Puts the player a given distance from a wolf, on the line between them, as if they had walked at it.</summary>
    public static void WalkTo(World world, Wolf wolf, float distance) => world.Player = wolf.Pos + (world.Player - wolf.Pos).Normalized() * distance;
}
