using ChopChopMeow;
using Godot;
using static Checks;
using static PlumbingSteps;
using static SetupSteps;
using static SteelPipeSteps;
using static WorldSteps;
using static WorkerSteps;

public static class JamPlumbingChecks {
    public static void Run(GameData data) {
        Test(CheckABarrelFillsAnIdlePipeWithoutTakingItsLiquidBack, data);
        Test(CheckABarrelTopsUpAPartlyFullPipeWithoutWastingWater, data);
        Test(CheckConnectedBarrelsEqualizeByPressure, data);
        Test(CheckJamFeedsACanneryWithoutMixingIntoWater, data);
        Test(CheckABarrelCarriesOnlyOneLiquid, data);
        Test(CheckAFullLineSuppliesTheRestOfACannerysLiter, data);
        Test(CheckFractionalWaterCannotBecomeJamAfterChangingLines, data);
        Test(CheckAWorkerBringsOnlyJamToTheJamBarrel, data);
    }

    private static void CheckABarrelFillsAnIdlePipeWithoutTakingItsLiquidBack(GameData data) {
        var world = EmptyWorld(data);
        var barrel = world.Add(ThingKind.Barrel, PumpTile);
        InstallPlumbing(world, barrel);
        LayPipe(world, PumpTile);
        barrel.Inventory.Add(BerryJam.Liquid, 1);
        world.Tick(.5f);
        Require(barrel.Inventory[BerryJam.Liquid] == 1 && world.PipeReading(PumpTile).Liters == 0,
            "Charging one liter at 1.1 L/s should take more than half a second.");
        world.Tick(.5f);
        Require(barrel.Inventory.Count == 0 && world.PipeReading(PumpTile).Liters == 1
            && world.PipeLiquid(PumpTile) == BerryJam.Liquid, "A barrel should fill an idle pipe with its last liter.");
        string saved = world.Serialize();
        Require(world.Construction.TakeUpPipe(PumpTile), "Taking up the pipe should spill its contents.");
        world.Deserialize(saved);
        SafeTick(world, 3);
        Require(world.Find(barrel.Id)!.Inventory.Count == 0 && world.PipeReading(PumpTile).Liters == 1,
            "The charged line must keep its jam instead of sending it back to its source, even after loading.");
    }

    private static void CheckABarrelTopsUpAPartlyFullPipeWithoutWastingWater(GameData data) {
        var (world, pump, barrel) = PumpAndBarrel(data, installed: false);
        PumpFully(world, pump);
        world.Tick(.75f);
        Require(world.PipeReading(EastTankTile).Liters == .75f, "Leave three quarters of a liter in the pump's last pipe.");
        Require(world.Construction.TakeUpPipe(EastTankTile + Vector2I.Left), "Disconnect the last pipe from the pump.");
        InstallPlumbing(world, barrel);
        barrel.Inventory.Add(Water.Liquid, 10);
        world.Tick(.5f);
        Require(world.PipeReading(EastTankTile).Liters == 1 && barrel.Inventory[Water.Liquid] == 9 && barrel.PipedWaterRemainder == .75f,
            "With no receiver, a barrel should top up the pipe and keep the rest of the liter at its connection.");
    }

    private static void CheckConnectedBarrelsEqualizeByPressure(GameData data) {
        var world = EmptyWorld(data);
        var high = world.Add(ThingKind.Barrel, PumpTile);
        var low = world.Add(ThingKind.Barrel, JunctionTile);
        InstallPlumbing(world, high); InstallPlumbing(world, low);
        LayPipe(world, PumpTile); LayPipe(world, JunctionTile);
        high.Inventory.Add(BerryJam.Liquid, 30); low.Inventory.Add(BerryJam.Liquid, 10);
        world.Tick(.5f);
        Require(high.Inventory[BerryJam.Liquid] == 28 && low.Inventory[BerryJam.Liquid] == 10
            && world.PipeReading(PumpTile).Liters == 1 && world.PipeReading(JunctionTile).Liters == 1,
            "The fuller barrel's first two liters should fill the line.");
        world.Tick(.0625f);
        Require(high.Inventory[BerryJam.Liquid] == 27 && low.Inventory[BerryJam.Liquid] == 11,
            "Only the pressure difference should move jam from the fuller barrel to the emptier one.");
        SafeTick(world, 45);
        Require(high.Inventory[BerryJam.Liquid] == 19 && low.Inventory[BerryJam.Liquid] == 19,
            "The barrels should equalize without creating or losing any jam.");
    }

    private static void CheckJamFeedsACanneryWithoutMixingIntoWater(GameData data) {
        var world = EmptyWorld(data);
        var tankTile = new Vector2I(84, 80);
        var canningTile = tankTile + Vector2I.Left;
        var join = canningTile + Vector2I.Left;
        var pump = world.Add(ThingKind.Pump, PumpTile);
        var barrel = world.Add(ThingKind.Barrel, tankTile);
        var cannery = world.Add(ThingKind.Cannery, canningTile);
        world.SetRecipe(cannery, "canned_jam");
        InstallPlumbing(world, barrel); InstallPlumbing(world, cannery);
        LayPipe(world, PumpTile); LayPipe(world, JunctionTile); LayPipe(world, canningTile); LayPipe(world, tankTile);
        barrel.Inventory.Add(BerryJam.Liquid, 12);
        PumpFully(world, pump);
        SafeTick(world, 3);
        Require(cannery.Inventory[BerryJam.Liquid] == 1 && cannery.Inventory[Water.Liquid] == 0 && barrel.Inventory[BerryJam.Liquid] == 9,
            "One liter should reach the cannery after two liters fill its isolated line.");
        Require(world.PipeLiquid(JunctionTile) == Water.Liquid && world.PipeLiquid(canningTile) == BerryJam.Liquid,
            "The two separate lines must hold their own liquids.");
        world.Player = join + Vector2I.Down;
        Require(world.Construction.PlacementBlock(ThingKind.SteelPiping, join) == "Different liquids cannot share connected pipes.",
            "Laying a pipe that would connect two different liquids must be refused.");
        string saved = world.Serialize();
        Require(world.Construction.TakeUpPipe(canningTile), "Taking up a charged pipe should remove the jam from that tile.");
        world.Deserialize(saved);
        Require(world.PipeReading(canningTile).Liters == 1 && world.PipeLiquid(canningTile) == BerryJam.Liquid,
            "Loading a save must restore both the amount and kind of liquid in its pipes.");
    }

    private static void CheckABarrelCarriesOnlyOneLiquid(GameData data) {
        var world = EmptyWorld(data);
        var barrel = world.Add(ThingKind.Barrel, PumpTile);
        world.Player = barrel.Pos + Vector2.Down;
        Give(world, Water.Full); Give(world, BerryJam.Bottle);
        Require(world.Transfer(world.Backpack, barrel.Inventory, BerryJam.Bottle, 1) == 1,
            "A bottle should pour three liters of jam into an empty barrel.");
        Require(world.Transfer(world.Backpack, barrel.Inventory, Water.Full, 1) == 0
            && barrel.Inventory[BerryJam.Liquid] == 3 && barrel.Inventory[Water.Liquid] == 0,
            "A barrel holding jam must refuse water without losing anything.");
        Require(world.PickUpBarrel(barrel) && world.Backpack[Barrel.AtFill(3, BerryJam.Liquid)] == 1,
            "Picking up the barrel must preserve its contents.");
    }

    private static void CheckAFullLineSuppliesTheRestOfACannerysLiter(GameData data) {
        var world = EmptyWorld(data);
        var tank = world.Add(ThingKind.Barrel, PumpTile);
        var first = world.Add(ThingKind.Cannery, JunctionTile);
        var second = world.Add(ThingKind.Cannery, JunctionTile + Vector2I.Right);
        foreach (var cannery in new[] { first, second }) world.SetRecipe(cannery, "canned_jam");
        foreach (var building in new[] { tank, first, second }) {
            InstallPlumbing(world, building);
            LayPipe(world, building.Pos);
        }
        tank.Inventory.Add(BerryJam.Liquid, 30);
        world.Tick(1);
        Require(first.Inventory[BerryJam.Liquid] == 0 && second.Inventory[BerryJam.Liquid] == 0
            && world.PipeReading(JunctionTile).Liters == 1 && tank.Inventory[BerryJam.Liquid] == 26,
            "The first four liters should fill the three pipes and give each cannery half a liter.");
        world.SetRecipe(second, "canned_meat_stew");
        world.Tick(.125f);
        Require(first.Inventory[BerryJam.Liquid] == 1 && second.Inventory[BerryJam.Liquid] == 0,
            "The first cannery must be able to take its missing half-liter from the full pipe.");
    }

    private static void CheckFractionalWaterCannotBecomeJamAfterChangingLines(GameData data) {
        var (world, pump, receiver) = PumpAndBarrel(data);
        PumpFully(world, pump);
        world.Tick(.90625f);
        Require(receiver.Inventory.Count == 0 && world.PipeReading(EastTankTile).Liters == 1,
            "The pump should fill four pipes and leave a partial delivery at the barrel.");
        Require(world.Construction.TakeUpPipe(EastTankTile + Vector2I.Left) && world.Construction.TakeUpPipe(EastTankTile),
            "Take up the connection and the water in the barrel's inlet.");
        world.ClearFilters(receiver);
        var canneryTile = EastTankTile + Vector2I.Right * 3;
        var tank = world.Add(ThingKind.Barrel, canneryTile + Vector2I.Right);
        var cannery = world.Add(ThingKind.Cannery, canneryTile);
        world.SetRecipe(cannery, "canned_jam");
        InstallPlumbing(world, tank); InstallPlumbing(world, cannery);
        LayPipe(world, tank.Pos); LayPipe(world, cannery.Pos);
        tank.Inventory.Add(BerryJam.Liquid, 30);
        world.Tick(1);
        Give(world, Water.Empty);
        world.Player = cannery.Pos + Vector2.Down;
        Require(world.Transfer(cannery.Inventory, world.Backpack, BerryJam.Liquid, 1) == 1, "Empty the cannery's jam input.");
        world.Player = tank.Pos + Vector2.Down;
        Require(world.PickUpBarrel(tank) && world.Construction.TakeUpPipe(canneryTile + Vector2I.Right),
            "Leave only the one standing liter at the cannery.");
        for (int i = 0; i < 3; i++) LayPipe(world, EastTankTile + Vector2I.Right * i);
        int receiverId = receiver.Id;
        world.Deserialize(world.Serialize());
        world.Tick(.125f);
        var loaded = world.Find(receiverId)!;
        Require(loaded.Inventory.Count == 0, "A half-liter of standing jam must not combine with old water to become one liter.");
    }

    private static void CheckAWorkerBringsOnlyJamToTheJamBarrel(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, PumpTile);
        var barrel = world.Add(ThingKind.Barrel, PumpTile + Vector2I.Right * 4);
        var chest = world.Add(ThingKind.Chest, PumpTile + Vector2I.Down * 3);
        world.SetRecipe(table, "berry_jam");
        table.Inventory.Add(Water.Liquid, 1);
        table.Inventory.Add(BerryJam.Liquid, 1);
        barrel.Inventory.Add(BerryJam.Liquid, 1);
        chest.Inventory.Add(Water.Empty, 1);
        world.SetFilter(chest, Water.Empty, true, true);
        var hauler = AddWorker(world, chest.Pos + Vector2.One);
        world.SetDuty(hauler, table, JobKind.Haul, true);
        Work(world, 30, () => barrel.Inventory[BerryJam.Liquid] == 2);
        Require(barrel.Inventory[BerryJam.Liquid] == 2 && table.Inventory[Water.Liquid] == 1,
            "A worker should fill one bottle with the outgoing jam, put it in the barrel, and leave the unused water.");
    }
}
