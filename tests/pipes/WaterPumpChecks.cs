using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

public static class WaterPumpChecks {
    public static void Run(GameData data) {
        Test(CheckRepeatedStrokesRaiseTheFlowToFiveLitersPerSecond, data);
        Test(CheckFullPressureRunsDownInTenSeconds, data);
        Test(CheckAWorkerPumpsFromTheBank, data);
        Test(CheckAPumpOperatorReturnsToHigherPriorityWork, data);
    }

    private static void CheckFullPressureRunsDownInTenSeconds(GameData data) {
        var world = EmptyWorld(data);
        var pump = world.Add(ThingKind.Pump, world.Player + Vector2.Right);
        for (int i = 0; i < 10; i++) { world.Pump(pump); world.Tick(1); }
        Require(pump.Pressure == 100, "Ten consecutive handle strokes should reach full pressure.");
        world.Tick(5);
        Require(pump.Pressure == 50 && pump.FlowRate == 2.5f, "Five seconds after the operator stops, flow should halve.");
        world.Tick(5);
        Require(pump.Pressure == 0 && pump.FlowRate == 0, "Ten seconds after the operator stops, the pump should stop supplying water.");
    }

    private static void CheckAWorkerPumpsFromTheBank(GameData data) {
        var world = WorkDay(data);
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = Terrain.TileAt(bank + Vector2.Down * 1.1f);
        world.Player = (Vector2)intake + new Vector2(0, -3);
        Give(world, "steel_bar", 4); Give(world, "stone", 10); Give(world, "wood_parts", 4);
        Require(world.Construction.Build(ThingKind.Pump, world.Player + Vector2.Up * 2) == null,
            "A pump on dry land must be refused without spending the materials.");
        Require(world.Construction.Build(ThingKind.Pump, intake, Mathf.Pi) == null,
            "The operator needs a dry spot to work the handle.");
        var pump = world.Construction.Build(ThingKind.Pump, intake);
        Require(pump != null, "A pump should go into shallow water with its handle on the bank.");
        Require(world.Backpack["steel_bar"] == 0 && world.Backpack["stone"] == 0 && world.Backpack["wood_parts"] == 0,
            "A pump should take four steel bars, ten stone and four wooden parts.");
        var worker = AddWorker(world, (Vector2)intake + World.BellowsStand);
        world.Assign(worker, pump);
        SafeTick(world, 12);
        Require(worker.WorkState == WorkerWorkState.Pumping && pump.Pressure == 100 && pump.FlowRate == 5,
            "An assigned worker should reach the handle on dry land and pump to 5 L/s.");
    }

    private static void CheckAPumpOperatorReturnsToHigherPriorityWork(GameData data) {
        var world = WorkDay(data);
        var shore = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var pump = world.Add(ThingKind.Pump, Terrain.TileAt(shore + Vector2.Down * 1.1f));
        var anvil = world.Add(ThingKind.Anvil, pump.ToWorld(World.BellowsStand) + Vector2.Up * 3);
        SlotPlainestTools(world, anvil);
        world.SetRecipe(anvil, "steel_parts");
        var operatorAtBank = AddWorker(world, pump.ToWorld(World.BellowsStand));
        world.SetDuty(operatorAtBank, anvil, JobKind.Craft, true);
        world.SetDuty(operatorAtBank, pump, JobKind.Pump, true);
        Work(world, 5, () => pump.Pressure > 0);
        Require(pump.Pressure > 0, "With the anvil empty, the worker should take their next job at the pump.");
        anvil.Inventory.Add("steel_bar", 1);
        Work(world, 15, () => anvil.Inventory["steel_parts"] > 0);
        Require(anvil.Inventory["steel_parts"] == 1, "After a stroke the worker should return to the ready anvil.");
    }

    private static void CheckRepeatedStrokesRaiseTheFlowToFiveLitersPerSecond(GameData data) {
        var world = EmptyWorld(data);
        var pump = world.Add(ThingKind.Pump, world.Player + Vector2.Right);
        for (int stroke = 1; stroke <= 10; stroke++) {
            world.Pump(pump);
            Require(pump.Pressure == stroke * 10 && Mathf.IsEqualApprox(pump.FlowRate, stroke * .5f),
                $"After {stroke} strokes the gauge should read {stroke * 10}% and flow should be {stroke * .5f} L/s.");
        }
        world.Pump(pump);
        Require(pump.Pressure == 100 && pump.FlowRate == 5, "A full pump must stay at 5 L/s.");
        var distant = world.Add(ThingKind.Pump, world.Player + Vector2.Right * 12);
        world.Pump(distant);
        Require(distant.Pressure == 0, "A player must reach a pump to move its handle.");
    }
}
