using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Simulation checks for the shovel: digging up stumps and bushes, by the player and by workers.</summary>
public static class DigChecks {
    public static void Run(GameData data) {
        CheckFelledTreeLeavesAStumpThatNeedsAShovel(data);
        CheckShovelDigsUpAStumpForThreeWood(data);
        CheckShovelDigsUpABushForThreeLeafPatches(data);
        CheckDiggingWaitsForRoomInTheBackpack(data);
        CheckBuildingOverStumpsAndBushesClearsThem(data);
        CheckNewValleyGrowsBushesOnOpenGround(data);
        CheckWoodcutterDigsUpStumpsWhileAShovelHangs(data);
        CheckWoodcutterWithoutAShovelGoesOnChopping(data);
        CheckBushDiggerGoesToTheNextBushNotToLeaves(data);
        CheckLeafRakerLeavesBushesAlone(data);
    }

    private static void CheckFelledTreeLeavesAStumpThatNeedsAShovel(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4);
        Give(world, "steel_axe");
        HoldGather(world, tree, 10);
        var stump = world.Things.SingleOrDefault(t => t.Kind == ThingKind.Stump);
        Require(world.Find(tree.Id) == null && stump != null && stump.Pos == tree.Pos, "a felled tree should leave a stump where it stood");
        Require(stump.OfTree == tree.Id, "a stump should keep on record which tree left it, since that tree's size shapes it");
        Require(world.Backpack["wood"] == 4, $"felling the tree should give its 4 wood: {world.Backpack["wood"]}");
        Require(world.GatherBlock(stump)?.Contains("shovel") == true, $"a stump should ask for a shovel: {world.GatherBlock(stump)}");
        HoldGather(world, stump, 10);
        Require(world.Find(stump.Id) != null && stump.Progress == 0 && world.Backpack["wood"] == 4, "a stump should stay in the ground untouched without a shovel");
    }

    private static void CheckShovelDigsUpAStumpForThreeWood(GameData data) {
        var world = EmptyWorld(data);
        var stump = world.Add(ThingKind.Stump, new Vector2(10, 10), World.StumpWood);
        Give(world, "steel_shovel");
        var heard = Listen(world);
        HoldGather(world, stump, 1);
        Require(world.Find(stump.Id) != null && stump.Progress > 0 && world.Backpack["wood"] == 0, "a stump should take more than a second of digging and give nothing until it is out");
        HoldGather(world, stump, 20);
        Require(world.Find(stump.Id) == null, "a shovel should dig a stump out of the ground for good");
        Require(world.Backpack["wood"] == 3, $"a dug up stump should give 3 wood: {world.Backpack["wood"]}");
        Require(heard.Contains("dig_up"), $"a stump coming out of the ground should be heard: {string.Join(",", heard)}");
    }

    private static void CheckShovelDigsUpABushForThreeLeafPatches(GameData data) {
        var world = EmptyWorld(data);
        var bush = world.Add(ThingKind.Bush, new Vector2(10, 10), World.BushLeaves);
        Require(world.GatherBlock(bush)?.Contains("shovel") == true, $"a bush should ask for a shovel: {world.GatherBlock(bush)}");
        HoldGather(world, bush, 10);
        Require(world.Find(bush.Id) != null && world.Backpack["leaves"] == 0, "a bush should stay in the ground without a shovel");
        Give(world, "steel_shovel");
        HoldGather(world, bush, 20);
        Require(world.Find(bush.Id) == null, "a shovel should dig a bush out of the ground for good");
        // An oak's patch holds 12 leaves.
        Require(world.Backpack["leaves"] == 36, $"a dug up bush should give 36 leaves, three leaf patches' worth: {world.Backpack["leaves"]}");
    }

    private static void CheckDiggingWaitsForRoomInTheBackpack(GameData data) {
        var world = EmptyWorld(data);
        var bush = world.Add(ThingKind.Bush, new Vector2(10, 10), World.BushLeaves);
        SetBackpackSlots(world, 1);
        Give(world, "steel_shovel");
        Require(world.GatherBlock(bush)?.Contains("room") == true, $"a full backpack should say it has no room for the leaves: {world.GatherBlock(bush)}");
        HoldGather(world, bush, 20);
        Require(world.Find(bush.Id) != null && bush.Remaining == 36 && bush.Progress == 0, "a bush should stay whole while the backpack has no room for all of it");
        SetBackpackSlots(world, 2);
        HoldGather(world, bush, 20);
        Require(world.Find(bush.Id) == null && world.Backpack["leaves"] == 36, "the bush should come out once there is room");
    }

    private static void CheckBuildingOverStumpsAndBushesClearsThem(GameData data) {
        var world = EmptyWorld(data);
        var at = new Vector2(20, 20);
        var stump = world.Add(ThingKind.Stump, at + new Vector2(.3f, 0), World.StumpWood);
        var bush = world.Add(ThingKind.Bush, at + new Vector2(-.4f, .2f), World.BushLeaves);
        var apart = world.Add(ThingKind.Bush, at + new Vector2(4, 0), World.BushLeaves);
        Give(world, data.Buildings[ThingKind.Chest].Cost);
        BuildAt(world, ThingKind.Chest, at);
        Require(world.Find(stump.Id) == null && world.Find(bush.Id) == null, "a building should go up over a stump and a bush and clear them away");
        Require(world.Find(apart.Id) != null, "a bush away from the building should stay");
        Require(world.Backpack["wood"] == 0 && world.Backpack["leaves"] == 0, "what a building clears away should give nothing");
    }

    private static void CheckNewValleyGrowsBushesOnOpenGround(GameData data) {
        var world = new World(data);
        var bushes = world.Things.Where(t => t.Kind == ThingKind.Bush).ToList();
        Require(bushes.Count > 200, $"a new valley should have a couple of hundred bushes: {bushes.Count}");
        Require(bushes.All(b => b.Remaining == 36), "every bush should hold 36 leaves");
        Require(bushes.All(b => world.CanStand(b.Pos) && b.Pos.DistanceTo(Terrain.Spawn) >= 6), "bushes should grow on open ground away from the clearing");
        var crowded = bushes.FirstOrDefault(b => world.Things.Any(t => t != b && t.Pos.DistanceTo(b.Pos) < .9f));
        Require(crowded == null, $"a bush should not grow into another thing: {crowded?.Pos}");
    }

    private static void CheckWoodcutterDigsUpStumpsWhileAShovelHangs(GameData data) {
        var lot = Woodlot(data);
        lot.Rack.Inventory.Add("steel_shovel", 1);
        Work(lot.World, 300, () => lot.Cutter.Assignment == lot.Next.Id);
        Require(lot.Cutter.Assignment == lot.Next.Id, $"the woodcutter should get to the next tree: {lot.Cutter.Status}");
        Require(lot.World.Things.All(t => t.Kind != ThingKind.Stump), "a woodcutter with a shovel to take should dig up the stump before the next tree");
        // The felled tree held 5 wood, and its stump 3 more.
        Require(lot.Chest.Inventory["wood"] == 8, $"the tree's 5 wood and its stump's 3 should reach storage: {lot.Chest.Inventory["wood"]}");
    }

    private static void CheckWoodcutterWithoutAShovelGoesOnChopping(GameData data) {
        var lot = Woodlot(data);
        Work(lot.World, 300, () => lot.Next.Remaining < 400);
        Require(lot.Next.Remaining < 400, $"a woodcutter with no shovel to take should chop the next tree: {lot.Cutter.Status}");
        Require(lot.World.Things.Count(t => t.Kind == ThingKind.Stump) == 1, "the stump should stay in the ground without a shovel");
        Require(lot.Chest.Inventory["wood"] >= 5 && lot.Chest.Inventory["wood"] % 5 == 0, $"only chopped wood should reach storage, 5 at a time: {lot.Chest.Inventory["wood"]}");
    }

    private static void CheckBushDiggerGoesToTheNextBushNotToLeaves(GameData data) {
        var (world, bush, leaves, chest, rack) = Thicket(data);
        var next = world.Add(ThingKind.Bush, bush.Pos + new Vector2(6, 0), World.BushLeaves);
        world.Look();
        rack.Inventory.Add("steel_shovel", 1);
        Hire(world, bush);
        Work(world, 300, () => chest.Inventory["leaves"] == 72);
        Require(world.Find(bush.Id) == null && world.Find(next.Id) == null, "a worker digging bushes should go on to the next bush");
        Require(chest.Inventory["leaves"] == 72, $"two bushes should bring 72 leaves to storage: {chest.Inventory["leaves"]}");
        Require(leaves.Remaining == 12, $"a worker digging bushes should leave the nearer leaf patch alone: {leaves.Remaining}");
    }

    private static void CheckLeafRakerLeavesBushesAlone(GameData data) {
        var (world, bush, leaves, chest, rack) = Thicket(data);
        rack.Inventory.Add("steel_shovel", 1);
        Hire(world, leaves);
        Work(world, 300, () => chest.Inventory["leaves"] == 12);
        Work(world, 60);
        Require(chest.Inventory["leaves"] == 12, $"a raker should bring the patch's 12 leaves to storage: {chest.Inventory["leaves"]}");
        Require(world.Find(bush.Id) != null && bush.Progress == 0, "a worker raking leaves should leave the bush beside the patch alone");
    }
}
