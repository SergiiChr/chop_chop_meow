using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static PlayerSteps;

internal static class ReplantingSteps {
    public static readonly Vector2 PlantingGround = new(20, 20);

    public static Thing MarkPlantingGround(World world) {
        Give(world, "acorn", 1);
        world.Player = PlantingGround + Vector2.Right * 2;
        world.Hold(world.Backpack, SlotOf(world.Backpack, "acorn"), 1);
        Require(world.Trees.Mark(PlantingGround), "holding an acorn should mark the open ground for planting");
        world.Release();
        world.Player = PlantingGround + Vector2.Right;
        return world.ThingsOf(ThingKind.PlantingSpot)[0];
    }

    public static Thing FinishPlanting(World world, Thing spot) {
        if (world.Backpack["steel_shovel"] == 0) Give(world, "steel_shovel", 1);
        world.Gather(spot, 4);
        var trees = world.ThingsOf(ThingKind.Tree);
        Require(world.Find(spot.Id) == null && trees.Count == 1 && trees[0].Pos == spot.Pos, "digging should replace the outline with a planted tree");
        return trees[0];
    }

    public static void PourIntoTree(World world, Thing tree, string vessel, string left) {
        Give(world, vessel, 1);
        Require(world.Transfer(world.Backpack, tree.Inventory, vessel, 1) == 1 && world.Backpack[left] > 0,
            $"the tree should take water and leave {world.Data.Name(left)} in the backpack");
    }
}
