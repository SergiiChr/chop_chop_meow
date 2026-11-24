using System;
using System.Linq;
using System.Text.Json.Nodes;
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
        Test(CheckWateredTreeKeepsGrowingAfterBecomingAnAdult, data);
        Test(CheckValleyTreesKeepGrowing, data);
        Test(CheckGatheringLeavesGivesAcornsThatRefillOneAtATime, data);
        Test(CheckAGrowingTreeKeepsItsAgeOnLoadAndCanBeFelledEarly, data);
        Test(CheckSaveRefusesAnInvalidRememberedTreeStage, data);
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
        Drop(world, "acorn");
        Require(world.GatherBlock(spot) == "Nothing to plant", "trying to dig without an acorn should say Nothing to plant");
        Give(world, "acorn");
        world.Gather(spot, 1);
        Require(world.Find(spot.Id) == spot && world.Backpack["acorn"] == 1, "a short press should leave the outline and acorn");
        var tree = FinishPlanting(world, spot);
        Require(world.Backpack["acorn"] == 0 && world.Backpack["steel_shovel"] > 0 && tree.Remaining == 0,
            "planting should spend one acorn and leave the shovel and a tree with no wood");
    }

    private static void CheckWateredTreeKeepsGrowingAfterBecomingAnAdult(GameData data) {
        var world = EmptyWorld(data);
        var tree = FinishPlanting(world, MarkPlantingGround(world));
        PourIntoTree(world, tree, Water.Full, Water.Empty);
        RunUntil(world, world.Time + data.DaySeconds * 12);
        Require(tree.Remaining == 0 && tree.Inventory[Water.Liquid] == 3, "three liters should leave the acorn dormant");
        PourIntoTree(world, tree, Bucket.AtFill(6), Bucket.AtFill(4));
        float watered = world.Time;
        RunUntil(world, watered + data.DaySeconds - 1);
        Require(tree.Remaining == 0, "the tree should have no wood until the first full day after watering");
        int[] wood = [4, 8, 12, 16, 21, 25, 29, 33, 37, 42, 46, 50];
        for (int day = 0; day < wood.Length; day++) {
            RunUntil(world, watered + data.DaySeconds * (day + 1));
            Require(tree.Remaining == wood[day], $"the tree should hold {wood[day]} wood after {day + 1} days, found {tree.Remaining}");
            if (day < 9) Require(world.ThingsOf(ThingKind.Leaves).Count == 0, "a young tree should have no leaf spot");
        }
        var patches = world.ThingsOf(ThingKind.Leaves);
        Require(patches.Count == 1 && patches[0].Pos.DistanceTo(tree.Pos) < 2 && patches[0].Remaining == 12,
            "an oak should make one leaf spot when it reaches adulthood");
    }

    private static void CheckValleyTreesKeepGrowing(GameData data) {
        var world = new World(data);
        var tree = world.ThingsOf(ThingKind.Tree)[0];
        RunUntil(world, world.Time + 3 * data.DaySeconds);
        Require(tree.Remaining == 54, "an existing tree with 42 wood should reach 54 after three more days");
    }

    private static void CheckGatheringLeavesGivesAcornsThatRefillOneAtATime(GameData data) {
        var world = EmptyWorld(data);
        var patch = world.Add(ThingKind.Leaves, PlantingGround, 12);
        world.Player = patch.Pos;
        Give(world, "rake", 1);
        world.Gather(patch, data.GatherSeconds);
        int first = world.Backpack["acorn"];
        Require(first is 1 or 2 && world.Backpack["leaves"] == 4, "gathering leaves should pick up the spot's one or two acorns too");
        float started = world.Time;
        RunUntil(world, started + data.DaySeconds * 10 - 1);
        world.Gather(patch, data.GatherSeconds);
        Require(world.Backpack["acorn"] == first, "acorns should take ten full days to refill");
        RunUntil(world, started + data.DaySeconds * 10);
        world.Gather(patch, data.GatherSeconds);
        Require(world.Backpack["acorn"] == first + 1, "one acorn should return after ten days");
        RunUntil(world, started + data.DaySeconds * 20);
        world.Gather(patch, data.GatherSeconds);
        Require(world.Backpack["acorn"] == first + 2, "the next acorn should take another ten days");
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

    private static void CheckSaveRefusesAnInvalidRememberedTreeStage(GameData data) {
        var world = EmptyWorld(data);
        var tree = FinishPlanting(world, MarkPlantingGround(world));
        world.Look();
        world.Player = tree.Pos + Vector2.Right * (data.SightRadius + 2);
        world.Look();
        var saved = JsonNode.Parse(world.Serialize())!;
        var memory = saved["Remembered"]?[tree.Id.ToString()];
        Require(memory != null && (int?)memory["TreeStage"] == 1, "the save should keep the last seen tree as a nub");
        memory!["TreeStage"] = -1;
        bool refused = false;
        try { world.Deserialize(saved.ToJsonString()); } catch (InvalidOperationException error) { refused = error.Message.Contains("Unsupported"); }
        Require(refused, "the save should refuse an invalid remembered growth stage");
    }
}
