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
        world.Lanterns.ToggleInHand(Lanterns.Id(item));
        var placed = world.Lanterns.Place(item, world.Player + Vector2.Right * 2)!;
        Require(world.Me.LanternInHand == 0, "setting a lantern on the ground should unequip it");
        Require(placed != null && world.Lanterns.Refill(Lanterns.Id(item), placed, 2), "an empty lantern can be placed and filled with two bugs");
        RunUntil(world, caught + 840);
        GiveFirebugs(world, 3);
        Require(world.Lanterns.Refill(Lanterns.Id(item), placed, 3) && world.Lanterns.BugCount(placed.LanternItem) == 5,
            "added bugs should join those still alive in a lit lantern");
        string saved = world.Serialize();
        RunUntil(world, caught + 2520);
        Require(world.Lanterns.BugCount(placed.LanternItem) == 3, "only the first catch should die after its third day");
        world.Deserialize(saved);
        var restored = world.Find(placed.Id)!;
        Require(world.Lanterns.BugCount(restored.LanternItem) == 5, "loading should restore the saved bugs and their ages");
        RunUntil(world, caught + 2519);
        Require(world.Lanterns.BugCount(restored.LanternItem) == 5, "the first catch should live for three full in-game days");
        world.Tick(1);
        Require(world.Lanterns.BugCount(restored.LanternItem) == 3, "the first catch should still die before the second after a load");
        RunUntil(world, caught + 3360);
        Require(world.Lanterns.BugCount(restored.LanternItem) == 0 && world.Find(restored.Id) != null,
            "the last bugs die on their own third day and leave a reusable dark lantern");
    }

    private static void CheckLanternTakesTheOldestFirebugsAndLeavesRoomForMoreAsTheyDie(GameData data) {
        var world = new World(data, false) { QuietNights = true };
        float caught = world.Time;
        string empty = world.Lanterns.NewEmpty(); int id = Lanterns.Id(empty);
        Give(world, empty); GiveFirebugs(world, 3);
        RunUntil(world, caught + 840);
        GiveFirebugs(world, 9);
        world.Lanterns.ToggleInHand(id);
        Require(world.Lanterns.Refill(id, count: 4) && world.Backpack[Firebugs.Item] == 8,
            "four bugs should go into the lantern instead of taking the whole stack");
        Require(world.Lanterns.FirebugStack(Lanterns.InHand(world.Me)) == (4, 54 * 60),
            "three firebugs with 48 hours left and one with 72 should average 54 hours inside the lantern");
        Require(world.Lanterns.Refill(id, count: 8) && world.Backpack[Firebugs.Item] == 2 && world.Lanterns.BugCount(Lanterns.InHand(world.Me)) == 10,
            "a refill should take only what fits and the same lantern should stay in hand");
        Require(!world.Lanterns.Refill(id), "a full lantern has no more room");
        RunUntil(world, caught + 2520);
        Require(world.Lanterns.BugCount(Lanterns.InHand(world.Me)) == 7 && world.Backpack[Firebugs.Item] == 2,
            "the oldest bugs go into the lantern first and die first");
        Require(world.Lanterns.Refill(id) && world.Lanterns.BugCount(Lanterns.InHand(world.Me)) == 9 && world.Backpack[Firebugs.Item] == 0,
            "a lantern that loses a few bugs can be topped up straight away");
    }
}
