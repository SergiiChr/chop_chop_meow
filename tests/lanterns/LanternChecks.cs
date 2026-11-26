using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

internal static class LanternChecks {
    public static void Run(GameData data) {
        Test(CheckPlacedLanternKeepsEachFirebugsLifetimeAcrossSave, data);
        Test(CheckLanternTakesTheOldestFirebugsAndLeavesRoomForMoreAsTheyDie, data);
    }

    private static void CheckPlacedLanternKeepsEachFirebugsLifetimeAcrossSave(GameData data) {
        var world = new World(data, false) { QuietNights = true };
        world.Player = Terrain.Spawn + new Vector2(4, 3);
        float caught = world.Time;
        string item = world.Lanterns.NewEmpty();
        Give(world, item); GiveFirebugs(world, 2);
        var placed = world.Lanterns.Place(item, world.Player + Vector2.Right * 2)!;
        Require(placed != null && world.Lanterns.Refill(Lanterns.Id(item), placed, 2), "an empty lantern can be placed and filled with two bugs");
        RunUntil(world, caught + 840);
        GiveFirebugs(world, 3);
        Require(world.Lanterns.Refill(Lanterns.Id(item), placed, 3) && world.Lanterns.BugCount(placed) == 5,
            "added bugs should join those still alive in a lit lantern");
        string saved = world.Serialize();
        RunUntil(world, caught + 2520);
        Require(world.Lanterns.BugCount(placed) == 3, "only the first catch should die after its third day");
        world.Deserialize(saved);
        var restored = world.Find(placed.Id)!;
        Require(world.Lanterns.BugCount(restored) == 5, "loading should restore the saved bugs and their ages");
        RunUntil(world, caught + 2519);
        Require(world.Lanterns.BugCount(restored) == 5, "the first catch should live for three full in-game days");
        world.Tick(1);
        Require(world.Lanterns.BugCount(restored) == 3, "the first catch should still die before the second after a load");
        RunUntil(world, caught + 3360);
        Require(world.Lanterns.BugCount(restored) == 0 && world.Find(restored.Id) != null,
            "the last bugs die on their own third day and leave a reusable dark lantern");
    }

    private static void CheckLanternTakesTheOldestFirebugsAndLeavesRoomForMoreAsTheyDie(GameData data) {
        var world = new World(data, false) { QuietNights = true };
        float caught = world.Time;
        string empty = world.Lanterns.NewEmpty(); int id = Lanterns.Id(empty);
        Give(world, empty); GiveFirebugs(world, 3);
        RunUntil(world, caught + 840);
        GiveFirebugs(world, 9);
        world.SelectInHand(empty);
        Require(world.Lanterns.Refill(id, count: 4) && world.Backpack[Firebugs.Item] == 8,
            "four bugs should go into the lantern instead of taking the whole stack");
        Require(world.Lanterns.Refill(id, count: 8) && world.Backpack[Firebugs.Item] == 2 && world.Lanterns.BugCount(world.InHand) == 10,
            "a refill should take only what fits and the same lantern should stay in hand");
        Require(!world.Lanterns.Refill(id), "a full lantern has no more room");
        RunUntil(world, caught + 2520);
        Require(world.Lanterns.BugCount(world.InHand) == 7 && world.Backpack[Firebugs.Item] == 2,
            "the oldest bugs go into the lantern first and die first");
        Require(world.Lanterns.Refill(id) && world.Lanterns.BugCount(world.InHand) == 9 && world.Backpack[Firebugs.Item] == 0,
            "a lantern that loses a few bugs can be topped up straight away");
    }
}
