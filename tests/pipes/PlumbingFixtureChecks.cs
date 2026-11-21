using ChopChopMeow;
using Godot;
using static Checks;
using static PlumbingSteps;
using static SetupSteps;
using static WorldSteps;

public static class PlumbingFixtureChecks {
    public static void Run(GameData data) {
        Test(CheckTheAnvilMakesStackablePipesWithASteelHammer, data);
        Test(CheckAPlumbedTableFillsOnlyTheCurrentRecipesWater, data);
        Test(CheckOnlyGrowingCropsUseTheSprinklers, data);
        Test(CheckPlumbingComesBackWithBarrelsAndDismantledBuildings, data);
    }

    private static void CheckTheAnvilMakesStackablePipesWithASteelHammer(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, world.Player + Vector2.Right);
        Give(world, "steel_bar", 2);
        world.SetRecipe(anvil, "steel_pipe");
        world.LoadInput(anvil, "steel_bar", 2);
        SafeTick(world, 12);
        Require(anvil.Inventory["steel_pipe"] == 0, "An anvil needs a steel hammer to form pipes.");
        Give(world, "steel_hammer");
        world.SlotTool(anvil, "steel_hammer");
        SafeTick(world, 14);
        Require(anvil.Inventory["steel_pipe"] == 2 && anvil.Inventory["steel_bar"] == 0,
            "Two steel bars should make two pipes in one output stack.");
    }

    private static void CheckAPlumbedTableFillsOnlyTheCurrentRecipesWater(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        barrel.Inventory.Add(Water.Liquid, 50);
        var table = ExtendSouth(world, ThingKind.Cooktable);
        world.SetRecipe(table, "berry_jam");
        PumpFully(world, pump);
        SafeTick(world, 3);
        Require(table.Inventory[Water.Liquid] == 0, "A tap should stay shut for a recipe that takes no water.");
        world.SetRecipe(table, "dough");
        world.SetRecipe(table, "");
        SafeTick(world, 2);
        Require(table.Inventory[Water.Liquid] == 0, "A table with no batches or mark should close the tap for its last recipe.");
        world.SetRecipe(table, "dough");
        PumpFully(world, pump);
        SafeTick(world, 3);
        Require(table.Inventory[Water.Liquid] == 3, "The dough recipe should draw its three liters, then shut the tap.");
    }

    private static void CheckOnlyGrowingCropsUseTheSprinklers(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        barrel.Inventory.Add(Water.Liquid, 50);
        var farm = ExtendSouth(world, ThingKind.Farm);
        PumpFully(world, pump); SafeTick(world, 3);
        Require(farm.Inventory[Water.Liquid] == 0, "An unsown field should take no water.");
        farm.Inventory.Add("wheat_seeds", 1); farm.Inventory.Add("fertilizer", 1);
        PumpFully(world, pump); SafeTick(world, 4);
        Require(farm.FarmStage == FarmStage.Growing && world.Farms.WaterLevel(farm) > 9 && world.Farms.WaterLevel(farm) <= 10,
            "Sprinklers should fill the growing field's ten-liter reserve and water its wheat.");
    }

    private static void CheckPlumbingComesBackWithBarrelsAndDismantledBuildings(GameData data) {
        var world = EmptyWorld(data);
        var barrel = world.Add(ThingKind.Barrel, world.Player + Vector2.Right);
        InstallPlumbing(world, barrel);
        Require(world.PickUpBarrel(barrel) && world.Backpack["steel_pipe"] == 10 && world.Backpack[Barrel.Item] == 1,
            "Taking up a fitted barrel should return the barrel and ten pipes.");
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        Require(world.Transfer(world.Backpack, table.PlumbingSlot!, "steel_pipe", 7) == 7 && !table.PlumbingInstalled,
            "A partially fitted building should retain the pipes without starting plumbing.");
        Require(world.Construction.Dismantle(table) && world.Backpack["steel_pipe"] == 10,
            "Taking down a partly fitted table should return all seven pipes.");
    }
}
