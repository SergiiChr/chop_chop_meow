using ChopChopMeow;
using Godot;
using static Checks;
using static SandSteps;
using static SetupSteps;
using static WorldSteps;

public static class SandChecks {
    public static void Run(GameData data) {
        Test(CheckShovelDigsTenSandFromOneSquare, data);
        Test(CheckSandStaysStrippedUntilTheNextHighTideEvenAcrossASave, data);
        Test(CheckTideCountdownAlternatesAtTheMoonExtremes, data);
        Test(CheckSandAtTheInlandEdgeIsDugAndRefilled, data);
        Test(CheckTheWholeSandYieldMustFit, data);
        Test(CheckWalkReachesTheSandExposedAtLowTide, data);
        Test(CheckStrippedSandStaysWithinTickBudget, data);
    }

    private static void CheckShovelDigsTenSandFromOneSquare(GameData data) {
        var world = EmptyWorld(data);
        world.Player = SandSquare + Vector2.Left;
        RunUntil(world, 4 * data.DaySeconds);
        Require(!world.Holes.Begin(SandSquare, 1), "digging a dry sand square should require a selected shovel");
        EquipBeachShovel(world);

        DigSand(world, 2);
        Require(world.Backpack["sand"] == 10 && world.ThingsOf(ThingKind.Hole).Count == 0,
            "a sand square should give ten sand without leaving a planting hole");
        Require(!world.Holes.Begin(SandSquare, 3), "a stripped square should give no more sand");
        Require(world.Holes.Begin(SandSquare + Vector2.Left, 4), "digging one square should leave its neighbour intact");
    }

    private static void CheckSandStaysStrippedUntilTheNextHighTideEvenAcrossASave(GameData data) {
        var world = EmptyWorld(data);
        EquipBeachShovel(world);
        RunUntil(world, 4 * data.DaySeconds);
        DigSand(world, 1);
        var tile = Terrain.TileAt(SandSquare);
        string saved = world.Serialize();

        RunUntil(world, 5 * data.DaySeconds);
        Require(world.Beach.Stripped(tile), "dawn alone should leave the sand stripped");
        RunUntil(world, 8 * data.DaySeconds);
        Require(!world.Beach.Stripped(tile) && world.Backpack["sand"] == 10,
            "the full-moon tide should refill the square and leave the sand the player gathered");
        Require(!world.Holes.Begin(SandSquare, 2), "a refilled square should remain out of reach of the shovel under water");

        world.Deserialize(saved);
        Require(world.Beach.Stripped(tile), "loading should bring back the saved stripped square");
        RunUntil(world, 12 * data.DaySeconds);
        DigSand(world, 3);
        Require(world.Backpack["sand"] == 20, "the shovel should gather the refilled sand when the tide recedes");

        world.Deserialize(saved);
        world.Tick(8 * data.DaySeconds, true);
        Require(!world.Beach.Stripped(tile), "advancing past high tide should still refill the square");
    }

    private static void CheckSandAtTheInlandEdgeIsDugAndRefilled(GameData data) {
        var world = EmptyWorld(data);
        var edge = new Vector2(198, 133);
        EquipBeachShovel(world);
        world.Player = edge + Vector2.Left;
        Require(!world.Holes.Begin(edge, 1), "the tide should prevent digging sand while the square is flooded");
        RunUntil(world, 4 * data.DaySeconds);

        DigSand(world, edge, 2);
        var tile = Terrain.TileAt(edge);
        Require(world.Beach.Stripped(tile) && world.ThingsOf(ThingKind.Hole).Count == 0,
            "digging at the inland end of the beach should still strip sand");
        RunUntil(world, 8 * data.DaySeconds);
        Require(!world.Beach.Stripped(tile), "high tide should reach and refill the inland end of the beach");
    }

    private static void CheckTideCountdownAlternatesAtTheMoonExtremes(GameData data) {
        var world = EmptyWorld(data);
        foreach (var (elapsed, remaining, high) in new[] {
            (2f, 2f, false), (3.75f, .25f, false), (4f, 4f, true), (7.25f, .75f, true), (8f, 4f, false)
        }) {
            RunUntil(world, elapsed * data.DaySeconds);
            var next = world.Beach.NextTide;
            Require(next.High == high && Mathf.IsEqualApprox(next.Days, remaining),
                $"at day {elapsed}, the next extreme should be {(high ? "high" : "low")} tide in {remaining} days, got {next}");
        }
    }

    private static void CheckTheWholeSandYieldMustFit(GameData data) {
        var world = EmptyWorld(data);
        EquipBeachShovel(world);
        RunUntil(world, 4 * data.DaySeconds);
        SetBackpackSlots(world, 2);
        Give(world, "sand", world.Backpack.StackOf("sand") - 9);

        int before = world.Backpack["sand"];
        Require(!world.Holes.Begin(SandSquare, 1), "the shovel should stop when the full yield cannot fit");
        Require(!world.Beach.Stripped(Terrain.TileAt(SandSquare)) && world.Backpack["sand"] == before,
            "a dig that cannot fit should leave the backpack and square unchanged");
    }

    private static void CheckWalkReachesTheSandExposedAtLowTide(GameData data) {
        var world = EmptyWorld(data);
        var exposed = Terrain.Point(Terrain.Waterline(0) + .6f, 0);
        var pastSand = exposed + Terrain.Seaward;
        var oldEdge = Terrain.Point(Terrain.Coast(-40.8f) - 1.005f, -40.8f);
        Require(world.CanStand(oldEdge), "the new low-tide line should keep the original beach walkable at its bends");
        Require(world.CanStand(exposed), "the exposed beach should stay walkable while the sea is high");
        RunUntil(world, 4 * data.DaySeconds);
        world.Player = exposed - Terrain.Seaward;
        world.MovePlayer(Terrain.Seaward);
        Require(world.Player.IsEqualApprox(exposed), "walking should reach the sand uncovered past the old coast");
        Require(!world.CanStand(pastSand), "walking should stop where the shallow beach ends");
    }

    private static void CheckStrippedSandStaysWithinTickBudget(GameData data) {
        var world = EmptyWorld(data);
        EquipBeachShovel(world);
        RunUntil(world, 4 * data.DaySeconds);
        DigSand(world, 1);
        AllocationChecks.Check("tick: stripped beach", () => world.Tick(1 / 60f));
    }
}
