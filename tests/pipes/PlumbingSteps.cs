using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static SteelPipeSteps;
using static WorldSteps;

internal static class PlumbingSteps {
    public static readonly Vector2I PumpTile = new(80, 80);
    public static readonly Vector2I JunctionTile = new(81, 80);
    public static readonly Vector2I EastTankTile = new(83, 80);
    public static readonly Vector2I SouthTankTile = new(81, 82);

    /// <summary>Lays a line from a pump to a barrel and returns both buildings.</summary>
    public static (World World, Thing Pump, Thing Barrel) PumpAndBarrel(GameData data, bool installed = true) {
        var world = EmptyWorld(data);
        var pump = world.Add(ThingKind.Pump, PumpTile);
        var barrel = world.Add(ThingKind.Barrel, EastTankTile);
        if (installed) InstallPlumbing(world, barrel);
        LayPipe(world, PumpTile);
        LayPipe(world, JunctionTile, shape: PipeShape.Cross);
        LayPipe(world, EastTankTile + Vector2I.Left);
        LayPipe(world, EastTankTile);
        return (world, pump, barrel);
    }

    /// <summary>Extends the line at its cross to reach a second receiving building.</summary>
    public static Thing ExtendSouth(World world, ThingKind kind) {
        LayPipe(world, JunctionTile + Vector2I.Down, Mathf.Pi / 2);
        LayPipe(world, SouthTankTile, Mathf.Pi / 2);
        var receiver = world.Add(kind, SouthTankTile);
        InstallPlumbing(world, receiver);
        return receiver;
    }

    /// <summary>Fits a building's plumbing by moving ten steel pipes from the backpack into its slot.</summary>
    public static void InstallPlumbing(World world, Thing receiver) {
        world.Player = receiver.Pos + Vector2.Down;
        Give(world, "steel_pipe", 10);
        Require(receiver.PlumbingSlot != null
            && world.Transfer(world.Backpack, receiver.PlumbingSlot, "steel_pipe", 10) == 10
            && receiver.PlumbingInstalled, "Ten steel pipes should install the building's plumbing.");
    }

    /// <summary>Works the pump to full pressure from beside it.</summary>
    public static void PumpFully(World world, Thing pump) {
        world.Player = pump.Pos + Vector2.Down;
        for (int i = 0; i < 10; i++) world.Pump(pump);
        Require(pump.FlowRate == 5, "The pump should be supplying five liters per second.");
    }
}
