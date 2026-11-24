using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class ReplantingSteps {
    public static readonly Vector2 PlantingGround = new(20, 20);

    public static Thing MarkDigSite(World world) {
        if (world.Backpack["steel_shovel"] == 0) Give(world, "steel_shovel", 1);
        world.Player = PlantingGround + Vector2.Right * 2;
        Require(world.Holes.Mark(PlantingGround), "a carried shovel should mark the open ground for digging");
        world.Player = PlantingGround + Vector2.Right;
        return world.ThingsOf(ThingKind.PlantingSpot)[0];
    }

    public static Thing DigHole(World world, Thing marker) {
        world.Gather(marker, 4);
        var holes = world.ThingsOf(ThingKind.Hole);
        Require(world.Find(marker.Id) == null && holes.Count == 1 && holes[0].Pos == marker.Pos,
            "digging should replace the outline with a hole");
        return holes[0];
    }

    public static Thing PlantAnAcorn(World world) {
        var hole = DigHole(world, MarkDigSite(world));
        Give(world, "acorn");
        Require(world.Transfer(world.Backpack, hole.Inventory, "acorn", 1) == 1, "putting an acorn in the hole should plant it");
        return world.ThingsOf(ThingKind.Tree).Single();
    }

    public static void PourIntoTree(World world, Thing tree, string vessel, string left) {
        Give(world, vessel, 1);
        Require(world.Transfer(world.Backpack, tree.Inventory, vessel, 1) == 1 && world.Backpack[left] > 0,
            $"the tree should take water and leave {world.Data.Name(left)} in the backpack");
    }
}
