using ChopChopMeow;
using Godot;
using static Checks;
using static PlumbingSteps;
using static SetupSteps;
using static SteelPipeSteps;
using static WorldSteps;

public static class JamPlumbingChecks {
    public static void Run(GameData data) {
        Test(CheckConnectedBarrelsEqualizeByPressure, data);
        Test(CheckJamFeedsACanneryWithoutMixingIntoWater, data);
        Test(CheckABarrelCarriesOnlyOneLiquid, data);
    }

    private static void CheckConnectedBarrelsEqualizeByPressure(GameData data) {
        var world = EmptyWorld(data);
        var high = world.Add(ThingKind.Barrel, PumpTile);
        var low = world.Add(ThingKind.Barrel, JunctionTile);
        InstallPlumbing(world, high); InstallPlumbing(world, low);
        LayPipe(world, PumpTile); LayPipe(world, JunctionTile);
        high.Inventory.Add(BerryJam.Liquid, 30); low.Inventory.Add(BerryJam.Liquid, 10);
        world.Tick(.5f);
        Require(high.Inventory[BerryJam.Liquid] == 29 && low.Inventory[BerryJam.Liquid] == 11,
            "A 2 L/s pressure difference should move one liter in half a second.");
        SafeTick(world, 45);
        Require(high.Inventory[BerryJam.Liquid] == 20 && low.Inventory[BerryJam.Liquid] == 20,
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
}
