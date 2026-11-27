using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlumbingSteps;
using static SetupSteps;
using static WorldSteps;

public static class BuildingPlumbingChecks {
    public static void Run(GameData data) {
        Test(CheckTheAnvilMakesStackablePipesWithASteelHammer, data);
        Test(CheckACookingTableDrawsWaterForItsNextBatch, data);
        Test(CheckTheLastDoughBatchDoesNotDrawMoreWater, data);
        Test(CheckExploredBuildingsRememberInstalledPlumbing, data);
        Test(CheckFullPlotsSpendTheirShareInEveryCropStage, data);
        Test(CheckPlumbingComesBackWithBarrelsAndDismantledBuildings, data);
    }

    private static void CheckTheAnvilMakesStackablePipesWithASteelHammer(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, world.Player + Vector2.Right);
        Give(world, "steel_bar", 2);
        world.QueueBatch(anvil, "steel_pipe", 2);
        SafeTick(world, 12);
        Require(anvil.Inventory["steel_pipe"] == 0, "An anvil needs a steel hammer to form pipes.");
        Give(world, "steel_hammer");
        world.SlotTool(anvil, "steel_hammer");
        SafeTick(world, 14);
        Require(anvil.Inventory["steel_pipe"] == 2 && anvil.Inventory["steel_bar"] == 0,
            "Two steel bars should make two pipes in one output stack.");
    }

    private static void CheckACookingTableDrawsWaterForItsNextBatch(GameData data) {
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

    private static void CheckTheLastDoughBatchDoesNotDrawMoreWater(GameData data) {
        var (world, pump, _) = PumpAndBarrel(data);
        var table = ExtendSouth(world, ThingKind.Cooktable);
        world.Player = table.Pos + Vector2.Down;
        Give(world, "flour"); GiveVessel(world, Water.Empty, Water.Liquid, 3);
        world.QueueBatch(table, "dough");
        world.Tick(.1f);
        Require(table.Processing && table.Batches == 0 && table.Inventory[Water.Liquid] == 0, "The last dough batch should have spent its ingredients.");
        world.SetRecipe(table, "");
        PumpFully(world, pump); SafeTick(world, 3);
        Require(table.Inventory[Water.Liquid] == 0 && !table.ReceivingWater, "A final batch already kneading should leave all incoming water for other buildings.");
    }

    private static void CheckExploredBuildingsRememberInstalledPlumbing(GameData data) {
        var world = EmptyWorld(data);
        var barrel = world.Add(ThingKind.Barrel, world.Player + Vector2.Right);
        InstallPlumbing(world, barrel); world.Look();
        world.Player = barrel.Pos + Vector2.Right * (data.SightRadius + 3);
        world.Look();
        var loaded = EmptyWorld(data); loaded.Deserialize(world.Serialize());
        var memory = Known(loaded).Single(t => t.Id == barrel.Id);
        Require(memory.ShowsPlumbing && !memory.PlumbingInstalled, "An explored barrel should remember its feed without retaining its Plumbing slot inventory.");
        var live = loaded.Find(barrel.Id)!;
        loaded.Player = live.Pos + Vector2.Down;
        loaded.Transfer(live.PlumbingSlot!, loaded.Backpack, "steel_pipe", 10); loaded.Look();
        loaded.Player = world.Player; loaded.Look();
        Require(!Known(loaded).Single(t => t.Id == live.Id).ShowsPlumbing, "Seeing the bare barrel again should clear its remembered plumbing.");
    }

    private static void CheckFullPlotsSpendTheirShareInEveryCropStage(GameData data) {
        foreach (var stage in new[] { FarmStage.Fallow, FarmStage.Growing, FarmStage.Ripe }) {
            var (world, pump, barrel) = PumpAndBarrel(data);
            var farm = ExtendSouth(world, ThingKind.Farm);
            farm.FarmStage = stage;
            farm.Inventory.Add(Water.Liquid, Farms.LitersPerDay - 1);

            PumpFully(world, pump); world.Tick(1);
            Require(!farm.ReceivingWater, $"The {stage} plot should wait while the pump fills the line.");
            PumpFully(world, pump); world.Tick(1);
            Require(farm.ReceivingWater && world.Farms.WaterRoom(farm) == 0 && barrel.Inventory[Water.Liquid] == 2,
                $"Of four delivered liters, the {stage} plot should spray two, keep its last liter of reserve and leave two for the barrel.");

            PumpFully(world, pump); world.Tick(.25f);
            Require(farm.ReceivingWater && farm.PipedWaterRemainder == 0 && barrel.PipedWaterRemainder == .625f,
                $"The full {stage} plot should spend its half of the flow, even for a fraction of a liter.");
            world.Construction.TakeUpPipe(JunctionTile + Vector2I.Down);
            world.Tick(.1f);
            Require(farm.ReceivingWater && world.PipeReading(SouthTankTile).Liters == 0,
                $"The full {stage} plot should spray the last liter standing in its inlet.");
            world.Tick(.1f);
            Require(!farm.ReceivingWater && world.Farms.WaterLevel(farm) is > 9 and <= 10 && farm.FarmStage == stage,
                $"The {stage} sprinklers should stop when the inlet runs dry, with the soil still wet.");
        }
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
