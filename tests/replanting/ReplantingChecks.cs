using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static ReplantingSteps;
using static WorkerSteps;

public static class ReplantingChecks {
    public static void Run(GameData data) {
        Test(CheckAxeHitsDropAcornsLikePickaxeHitsDropChips, data);
        Test(CheckAShovelDigsAHoleBeforePlantingAnAcorn, data);
        Test(CheckReleasingTheShovelRestartsTheDig, data);
        Test(CheckEmptyHoleLastsUntilDawnAcrossASave, data);
        Test(CheckWateredTreeKeepsGrowingAfterBecomingAnAdult, data);
        Test(CheckValleyTreesKeepGrowing, data);
        Test(CheckDueTreesSpreadTheirGrowthAcrossUpdates, data);
        Test(CheckWoodcutterSkipsYoungTrees, data);
        Test(CheckGatheringLeavesGivesAcornsThatRefillOneAtATime, data);
        Test(CheckAGrowingTreeKeepsItsAgeOnLoadAndCanBeFelledEarly, data);
        Test(CheckSaveRefusesAnInvalidRememberedTreeStage, data);
        Test(CheckSaveRefusesAnObsoleteDigOutline, data);
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

    private static void CheckAShovelDigsAHoleBeforePlantingAnAcorn(GameData data) {
        var world = EmptyWorld(data);
        world.Player = PlantingGround + Vector2.Right;
        Give(world, "acorn");
        Require(!world.Holes.Begin(PlantingGround, 1), "digging should require a selected shovel");
        Drop(world, "acorn");
        var hole = DigHole(world);
        var far = PlantingGround + Vector2.Right * 8;
        Require(world.Construction.PlacementBlock(ThingKind.Hole, far) == null && !world.Holes.Begin(far, 2),
            "distant open ground should be eligible, but shovel work should start only on arrival");
        Require(hole.Inventory.Count == 0 && world.ThingsOf(ThingKind.Tree).Count == 0,
            "digging without acorns should leave an empty hole");
        Give(world, "wood");
        Require(world.Transfer(world.Backpack, hole.Inventory, "wood", 1) == 0 && hole.Inventory.Count == 0,
            "a hole should refuse wood");
        Give(world, "acorn", 2);
        Require(world.Transfer(world.Backpack, hole.Inventory, "acorn", 2) == 1 && world.Backpack["acorn"] == 1,
            "the hole should accept exactly one acorn from a stack");
        var tree = world.ThingsOf(ThingKind.Tree).Single();
        Require(world.Find(hole.Id) == null && tree.Pos == PlantingGround && TreePlanting.NeedsWater(tree)
            && tree.Remaining == 0 && world.Backpack["steel_shovel"] == 1,
            "planting should replace the hole with an unwatered nub and leave the shovel in the backpack");
        Require(world.Transfer(world.Backpack, hole.Inventory, "acorn", 1) == 0 && world.Backpack["acorn"] == 1,
            "an old reference to the hole should not plant a second acorn");
    }

    private static void CheckReleasingTheShovelRestartsTheDig(GameData data) {
        var world = EmptyWorld(data);
        EquipShovel(world);
        Require(world.Holes.Begin(PlantingGround, 1), "the selected shovel should start digging");
        world.Tick(2);
        Require(!world.Holes.Finish(1), "two seconds should not be enough shovel work");
        world.Holes.Cancel(1);
        Require(!world.ThingsOf(ThingKind.Hole).Any(),
            "letting go should leave the ground as it was");
        Require(world.Holes.Begin(PlantingGround, 2), "another hold should start a fresh dig");
        world.Tick(2);
        Require(!world.Holes.Finish(2), "the second hold should start again from zero");
        world.Tick(4);
        Require(world.Holes.Ready(2) && world.ThingsOf(ThingKind.Hole).Count == 0,
            "a finished dig should wait for the player's confirmation before creating a hole");
        Require(!world.Holes.Finish(1) && world.Holes.Finish(2), "only the current hold can finish the hole");
    }

    private static void CheckEmptyHoleLastsUntilDawnAcrossASave(GameData data) {
        var world = EmptyWorld(data);
        var hole = DigHole(world);
        RunUntil(world, data.DaySeconds - 1);
        Require(world.Find(hole.Id) == hole, "an empty hole should still be there just before dawn");
        string saved = world.Serialize();
        RunUntilDawn(world);
        Require(world.Find(hole.Id) == null, "an unused hole should disappear at dawn");
        world.Deserialize(saved);
        Require(world.Find(hole.Id) is { Kind: ThingKind.Hole, Inventory.Count: 0 }, "the saved hole should load empty");
        RunUntilDawn(world);
        Require(world.Find(hole.Id) == null, "a loaded empty hole should disappear at dawn too");
    }

    private static void CheckWateredTreeKeepsGrowingAfterBecomingAnAdult(GameData data) {
        var world = EmptyWorld(data);
        var tree = PlantAnAcorn(world);
        PourIntoTree(world, tree, Water.Full, Water.Empty);
        RunUntil(world, world.Time + data.DaySeconds * 12);
        Require(tree.Remaining == 0 && tree.Inventory[Water.Liquid] == 3, "three liters should leave the acorn dormant");
        PourIntoTree(world, tree, Bucket.AtFill(6), Bucket.AtFill(4));
        float watered = world.Time;
        RunUntil(world, watered + data.DaySeconds - 1);
        Require(tree.Remaining == 0, "the tree should have no wood until the first full day after watering");
        int[] wood = [4, 8, 12, 16, 21, 25, 29, 33, 37, 42, 46, 49];
        for (int day = 0; day < wood.Length; day++) {
            RunUntil(world, watered + data.DaySeconds * (day + 1));
            Require(tree.Remaining == wood[day], $"the tree should hold {wood[day]} wood after {day + 1} days, found {tree.Remaining}");
            if (day < 9) Require(world.ThingsOf(ThingKind.LeafSpot).Count == 0, "a young tree should have no leaf spot");
        }
        var patches = world.ThingsOf(ThingKind.LeafSpot);
        Require(patches.Count == 1 && patches[0].Pos.DistanceTo(tree.Pos) < 2 && patches[0].Remaining == 12,
            "an oak should make one leaf spot when it reaches adulthood");
        RunUntil(world, watered + 20 * data.DaySeconds);
        Require(tree.Remaining == 71, "after another ten days a tree should hold 71 wood");
        RunUntil(world, watered + 40 * data.DaySeconds);
        Require(tree.Remaining == 100, "doubling the age again should add the same 29 wood over twice as many days");
        var adult = new Thing { Kind = ThingKind.Tree, Id = tree.Id };
        float heightRatio = WorldModels.Hits(tree)[1].Top / WorldModels.Hits(adult)[1].Top;
        Require(Mathf.IsEqualApprox(heightRatio, 2.3862944f), "a forty-day tree should scale by the logarithmic growth curve");
    }

    private static void CheckValleyTreesKeepGrowing(GameData data) {
        var world = new World(data);
        float began = world.Time;
        var trees = world.ThingsOf(ThingKind.Tree);
        RunUntil(world, began + .5f * data.DaySeconds);
        int grown = trees.Count(tree => tree.Remaining > 42);
        Require(grown > 0 && grown < trees.Count, "some standing trees should grow before the others");
        RunUntil(world, began + 3 * data.DaySeconds);
        Require(trees.All(tree => tree.Remaining == 53), "each standing tree should gain eleven wood over three days");
    }

    private static void CheckDueTreesSpreadTheirGrowthAcrossUpdates(GameData data) {
        var world = EmptyWorld(data);
        for (int i = 0; i < 64; i++) {
            var tree = world.Add(ThingKind.Tree, PlantingGround + new Vector2(i % 8, i / 8) * 3, 42);
            tree.NextTreeStageAt = world.Time + 1;
        }
        world.Deserialize(world.Serialize());
        var trees = world.ThingsOf(ThingKind.Tree);
        world.Tick(1, true);
        int grown = trees.Count(tree => tree.Remaining == 46);
        Require(grown > 0 && grown < trees.Count, "saved trees due together should grow across several updates");
        world.Tick(.1f, true);
        Require(trees.All(tree => tree.Remaining == 46), "later updates should finish the queued growth");
    }

    private static void CheckWoodcutterSkipsYoungTrees(GameData data) {
        var lot = Woodlot(data);
        var young = lot.World.Add(ThingKind.Tree, new Vector2(13, 10), 21);
        young.TreeStage = 6;
        lot.World.Look();
        Work(lot.World, 30, () => lot.Next.Remaining < 400);
        Require(lot.World.Find(young.Id) == young && young.Remaining == 21 && lot.Next.Remaining < 400,
            "the woodcutter should leave the nearer young tree untouched and chop the adult tree");

        lot.World.SetDuty(lot.Cutter, young, JobKind.Gather, true);
        lot.World.Assign(lot.Cutter, young);
        Require(!World.HasDuty(lot.Cutter, young.Id) && World.HasDuty(lot.Cutter, lot.Next.Id),
            "assigning a young tree should leave the woodcutter's existing duty in place");
    }

    private static void CheckGatheringLeavesGivesAcornsThatRefillOneAtATime(GameData data) {
        var world = EmptyWorld(data);
        var patch = world.Add(ThingKind.LeafSpot, PlantingGround, 12);
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

        var saved = JsonNode.Parse(world.Serialize())!;
        Require((string?)saved["Things"]?[0]?["Kind"] == "leaves", "leaf spots should keep the existing save id");
        world.Deserialize(saved.ToJsonString());
        Require(world.Find(patch.Id) is { Kind: ThingKind.LeafSpot }, "the existing leaves id should load as a leaf spot");
    }

    private static void CheckAGrowingTreeKeepsItsAgeOnLoadAndCanBeFelledEarly(GameData data) {
        var world = EmptyWorld(data);
        var tree = PlantAnAcorn(world);
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
        var tree = PlantAnAcorn(world);
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

    private static void CheckSaveRefusesAnObsoleteDigOutline(GameData data) {
        var world = EmptyWorld(data);
        DigHole(world);
        var save = JsonNode.Parse(world.Serialize())!;
        save["Things"]![0]!["Kind"] = "planting_spot";
        try {
            world.Deserialize(save.ToJsonString());
        } catch (JsonException error) {
            Require(error.Message.Contains("planting_spot"), "the load error should name the unsupported outline");
            return;
        }
        Require(false, "loading should refuse a saved dig outline");
    }
}
