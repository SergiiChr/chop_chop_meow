using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class ReplantingSteps {
    public static readonly Vector2 PlantingGround = new(20, 20);

    public static void EquipShovel(World world) {
        if (world.Backpack["steel_shovel"] == 0) Give(world, "steel_shovel", 1);
        world.SelectInHand("steel_shovel");
        world.Player = PlantingGround + Vector2.Right;
    }

    public static Thing DigHole(World world) {
        EquipShovel(world);
        Require(world.Holes.Begin(PlantingGround, 1), "the selected shovel should begin digging open ground in reach");
        world.Tick(6);
        Require(world.Holes.Finish(1), "holding the dig to completion should leave a hole");
        return world.ThingsOf(ThingKind.Hole).Single();
    }

    public static Thing PlantAnAcorn(World world) {
        var hole = DigHole(world);
        Give(world, "acorn");
        Require(world.Transfer(world.Backpack, hole.Inventory, "acorn", 1) == 1, "putting an acorn in the hole should plant it");
        return world.ThingsOf(ThingKind.Tree).Single();
    }

    /// <summary>Pours a container holding so many liters of water into a tree, which leaves it in the backpack holding <paramref name="left"/> liters.</summary>
    public static void PourIntoTree(World world, Thing tree, string container, int liters, int left) {
        string vessel = GiveVessel(world, container, Water.Liquid, liters);
        Require(world.Transfer(world.Backpack, tree.Inventory, vessel, 1) == 1 && (left > 0 ? Vessels.LitersIn(world.Backpack, vessel) == left : world.Backpack[container] > 0),
            $"the tree should take water and leave the {world.Data.Name(container)} in the backpack holding {left} L");
    }

    /// <summary>Loads a tree due to grow in two seconds, with a building just outside its trunk.</summary>
    public static (World World, Thing Building) TreeReadyToGrowBeside(GameData data, ThingKind kind) {
        var world = WorldSteps.EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, PlantingGround, 42);
        tree.NextTreeStageAt = world.Time + 2;
        float trunk = WorldModels.Hits(tree)[0].Radius;
        var building = world.Add(kind, tree.Pos + Vector2.Right * (trunk + World.Footprint(kind) + .01f));
        world.Deserialize(world.Serialize());
        return (world, world.Find(building.Id)!);
    }
}
