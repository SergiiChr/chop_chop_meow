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
            if (turns && wolf.Snap > 0 && !WolfSnap.Winding(wolf)) way = Vector2.Up;
            Nourished(world); world.MovePlayer(way * SnapStep * data.MoveSpeed(sprinting)); world.Tick(SnapStep);
        }
        Checks.Require(wolf.Snap == 0 && wolf.Attack > 0, "the wolf should have snapped once within a second");
        return world.Health;
    }
}
