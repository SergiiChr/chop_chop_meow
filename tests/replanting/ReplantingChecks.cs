using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static ReplantingSteps;

public static class ReplantingChecks {
    public static void Run(GameData data) {
        Test(CheckAxeHitsDropAcornsLikePickaxeHitsDropChips, data);
        Test(CheckMarkingIsFreeAndPlantingUsesOneAcornAndAShovel, data);
        Test(CheckPlantedAcornNeedsFiveLitersThenGrowsThroughTenStages, data);
        Test(CheckAGrowingTreeKeepsItsAgeOnLoadAndCanBeFelledEarly, data);
    }

    private static void CheckAxeHitsDropAcornsLikePickaxeHitsDropChips(GameData data) {
        var woods = EmptyWorld(data);
        var quarry = EmptyWorld(data);
        var at = new Vector2(20, 20);
        var tree = woods.Add(ThingKind.Tree, at, 1200);
        var rock = quarry.Add(ThingKind.Stone, at, 1200);
        Give(woods, "steel_axe", 1);
        Give(quarry, "steel_pick", 1);
        woods.Player = quarry.Player = at + Vector2.Right;
        for (int hit = 0; hit < 300; hit++) {
            woods.Gather(tree, data.GatherSeconds);
            quarry.Gather(rock, data.GatherSeconds);
            Drop(woods, "wood", woods.Backpack["wood"]);
            Drop(quarry, "stone", quarry.Backpack["stone"]);
        }
        var acorns = woods.Things.Where(t => t.Kind == ThingKind.Acorn).ToArray();
        int chips = quarry.ThingsOf(ThingKind.LooseStone).Count;
        Require(chips > 0 && acorns.Length == chips, $"axe and pickaxe hits should drop pieces at the same rate: {acorns.Length} acorns, {chips} chips");
        Require(acorns.All(t => t.Pos.DistanceTo(at) <= 2 && woods.CanStand(t.Pos)), "acorns should land on open ground close to the tree");
        woods.Player = acorns[0].Pos;
        woods.Gather(acorns[0], 0);
        Require(woods.Backpack["acorn"] == 1, "one click should pick up one acorn");
    }

    private static void CheckMarkingIsFreeAndPlantingUsesOneAcornAndAShovel(GameData data) {
        var world = EmptyWorld(data);
        var spot = MarkPlantingGround(world);
        Require(world.Backpack["acorn"] == 1 && world.ThingsOf(ThingKind.Tree).Count == 0, "marking should spend nothing and leave only an outline");
        world.Gather(spot, 10);
        Require(world.Find(spot.Id) == spot && world.Backpack["acorn"] == 1, "planting without a shovel should do nothing");
        Give(world, "steel_shovel", 1);
        world.Gather(spot, 1);
        Require(world.Find(spot.Id) == spot && world.Backpack["acorn"] == 1, "a short press should leave the outline and acorn");
        var tree = FinishPlanting(world, spot);
        Require(world.Backpack["acorn"] == 0 && world.Backpack["steel_shovel"] > 0 && tree.Remaining == 0,
            "planting should spend one acorn and leave the shovel and a tree with no wood");
    }

    private static void CheckPlantedAcornNeedsFiveLitersThenGrowsThroughTenStages(GameData data) {
        var world = EmptyWorld(data);
        var tree = FinishPlanting(world, MarkPlantingGround(world));
        PourIntoTree(world, tree, Water.Full, Water.Empty);
        RunUntil(world, world.Time + data.DaySeconds * 12);
        Require(tree.Remaining == 0 && tree.Inventory[Water.Liquid] == 3, "three liters should leave the acorn dormant");
        PourIntoTree(world, tree, Bucket.AtFill(6), Bucket.AtFill(4));
        float watered = world.Time;
        RunUntil(world, watered + data.DaySeconds - 1);
        Require(tree.Remaining == 0, "the tree should have no wood until the first full day after watering");
        int[] wood = [4, 8, 12, 16, 21, 25, 29, 33, 37, 42];
        for (int day = 0; day < wood.Length; day++) {
            RunUntil(world, watered + data.DaySeconds * (day + 1));
            Require(tree.Remaining == wood[day], $"the tree should hold {wood[day]} wood after {day + 1} days, found {tree.Remaining}");
        }
    }

    private static void CheckAGrowingTreeKeepsItsAgeOnLoadAndCanBeFelledEarly(GameData data) {
        var world = EmptyWorld(data);
        var tree = FinishPlanting(world, MarkPlantingGround(world));
        PourIntoTree(world, tree, Bucket.AtFill(5), Bucket.Item);
        float watered = world.Time;
        RunUntil(world, watered + data.DaySeconds * 2.5f);
        string saved = world.Serialize();
        RunUntil(world, watered + data.DaySeconds * 10);
        world.Deserialize(saved);
        var loaded = world.Find(tree.Id)!;
        Require(loaded.Remaining == 8, "loading should restore two completed days of growth");
        RunUntil(world, watered + data.DaySeconds * 3);
        Require(loaded.Remaining == 12, "the third stage should finish half a day after loading");
        Give(world, "steel_axe", 1);
        int wood = world.Backpack["wood"];
        for (int i = 0; i < 3; i++) world.Gather(loaded, data.GatherSeconds);
        Require(world.Backpack["wood"] == wood + 12 && world.Find(loaded.Id) == null, "felling a young tree should give its current wood and remove it");
        RunUntil(world, watered + data.DaySeconds * 10);
        Require(world.ThingsOf(ThingKind.Tree).Count == 0 && world.ThingsOf(ThingKind.Stump).Count == 1,
            "a young tree felled for its wood should stay felled");
    }
}
