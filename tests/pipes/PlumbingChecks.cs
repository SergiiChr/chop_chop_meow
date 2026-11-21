using ChopChopMeow;
using Godot;
using static Checks;
using static PlumbingSteps;
using static SetupSteps;
using static SteelPipeSteps;

public static class PlumbingChecks {
    public static void Run(GameData data) {
        Test(CheckBranchesShareByReceiver, data);
        Test(CheckDisconnectingAPipeStopsDeliveryAndItsSpray, data);
        Test(CheckAnUnmountedCabinDoesNotTakeAShare, data);
        Test(CheckTwoPumpsReachOneTankWithoutMultiplyingTheirWater, data);
        Test(CheckFractionalDeliverySurvivesLoading, data);
        Test(CheckClosedPlumbingStaysPressurizedAndFull, data);
        Test(CheckSeparatedPipesKeepTheirWaterThroughLoading, data);
    }

    private static void CheckBranchesShareByReceiver(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        var second = ExtendSouth(world, ThingKind.Barrel);
        PumpFully(world, pump);
        world.Tick(1);
        PumpFully(world, pump);
        world.Tick(1);
        Require(barrel.Inventory[Water.Liquid] == 2 && second.Inventory[Water.Liquid] == 2,
            "Ten liters must fill six pipe tiles and give two liters to each connected barrel.");
        Require(barrel.ReceivingWater && second.ReceivingWater, "Both barrels should show active delivery.");
    }

    private static void CheckDisconnectingAPipeStopsDeliveryAndItsSpray(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        PumpFully(world, pump);
        world.Tick(1);
        Require(barrel.Inventory[Water.Liquid] == 1 && barrel.ReceivingWater, "A connected barrel should fill after its four pipes.");
        var tile = EastTankTile + Vector2I.Left;
        Require(world.Construction.Remove(Removal.OfPipe(tile)), "The link to the barrel should come out.");

        PumpFully(world, pump);
        world.Tick(1);
        Require(barrel.Inventory[Water.Liquid] == 2 && world.PipeReading(EastTankTile) == (0, 0),
            "A disconnected barrel must draw the liter in its own pipe and receive no more from the pump.");
        LayPipe(world, tile, Mathf.Pi / 2);
        world.Tick(.5f);
        Require(barrel.Inventory[Water.Liquid] == 2 && !barrel.ReceivingWater,
            "A pipe laid across the line must leave the barrel disconnected after its stored water is gone.");
    }

    private static void CheckAnUnmountedCabinDoesNotTakeAShare(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        var cabin = ExtendSouth(world, ThingKind.Hut);
        PumpFully(world, pump);
        world.Tick(1);
        PumpFully(world, pump);
        world.Tick(1);
        Require(barrel.Inventory[Water.Liquid] == 4 && cabin.Inventory.Count == 0 && !cabin.ReceivingWater,
            "A connected cabin with no roof barrel must leave the water for the placed barrel.");

        Give(world, Barrel.Item);
        Require(world.Transfer(world.Backpack, cabin.Inventory, Barrel.Item, 1) == 1,
            "A player can mount a roof barrel on the connected cabin.");
        PumpFully(world, pump);
        world.Tick(1);
        PumpFully(world, pump);
        world.Tick(1);
        Require(barrel.Inventory[Water.Liquid] == 9 && cabin.Inventory[Barrel.AtFill(5)] == 1,
            "The mounted cabin barrel should now share the pump's water.");
    }

    private static void CheckTwoPumpsReachOneTankWithoutMultiplyingTheirWater(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        var second = world.Add(ThingKind.Pump, JunctionTile + Vector2I.Up * 2);
        LayPipe(world, JunctionTile + Vector2I.Up, Mathf.Pi / 2);
        LayPipe(world, Terrain.TileAt(second.Pos), Mathf.Pi / 2);
        PumpFully(world, pump);
        PumpFully(world, second);
        world.Tick(1);
        Require(barrel.Inventory[Water.Liquid] == 4,
            "Two pumps delivering five liters each must fill six pipe tiles and give four liters to the barrel.");
    }

    private static void CheckFractionalDeliverySurvivesLoading(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        PumpFully(world, pump);
        world.Tick(.875f);
        Require(barrel.Inventory[Water.Liquid] == 0 && barrel.ReceivingWater,
            "Three eighths of a liter after filling the pipe tiles should show flow without adding a whole liter.");
        string save = world.Serialize();
        world.Tick(.125f);
        Require(barrel.Inventory[Water.Liquid] == 1, "Two partial deliveries should add one whole liter.");

        world.Deserialize(save);
        world.Tick(.125f);
        Require(world.Find(barrel.Id)?.Inventory[Water.Liquid] == 1,
            "After loading, the next partial delivery must still add one whole liter.");
    }

    private static void CheckClosedPlumbingStaysPressurizedAndFull(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data);
        for (int second = 0; second < 11; second++) {
            PumpFully(world, pump);
            world.Tick(1);
        }
        Require(barrel.Inventory[Water.Liquid] == 50 && !barrel.ReceivingWater,
            "A full barrel cannot accept any more water.");
        Require(world.PipeReading(EastTankTile) == (1, 100),
            "The pipe under the full barrel should hold one liter at the pump's full pressure.");
        world.Tick(10);
        Require(world.PipeReading(EastTankTile) == (1, 0),
            "The closed pipe must retain its water as the pump's pressure drops to zero.");
    }

    private static void CheckSeparatedPipesKeepTheirWaterThroughLoading(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data, installed: false);
        PumpFully(world, pump);
        world.Tick(1);
        world.Tick(10);
        string save = world.Serialize();

        Require(world.Construction.Remove(Removal.OfPipe(EastTankTile + Vector2I.Left)), "The wet link should come out.");
        Require(world.PipeReading(EastTankTile) == (1, 0) && world.PipeReading(JunctionTile) == (1, 0),
            "Both sections should keep their own standing water after a pipe between them is removed.");
        InstallPlumbing(world, barrel);
        world.Tick(.125f);
        Require(barrel.Inventory[Water.Liquid] == 1 && world.PipeReading(EastTankTile) == (0, 0)
            && world.PipeReading(JunctionTile) == (1, 0),
            "Demand should drain only the water in the separated section attached to the barrel.");

        world.Deserialize(save);
        Require(world.PipeReading(EastTankTile) == (1, 0) && world.PipeReading(JunctionTile) == (1, 0),
            "Loading should restore the standing water in the saved pipe line with the pump off.");
    }
}
