using ChopChopMeow;
using static Checks;
using static PlumbingSteps;
using static SetupSteps;

public static class PlumbingAllocationChecks {
    public static void CheckPumpingIntoTwoBarrelsStaysWithinAllocationBudget(GameData data) {
        var (plumbed, pump, tank) = PumpAndBarrel(data);
        var branch = ExtendSouth(plumbed, ThingKind.Barrel);
        PumpFully(plumbed, pump);
        int tick = 0;
        // Drawing from both tanks keeps water flowing past the junction throughout the measured ticks.
        AllocationChecks.Check("tick: pumping into two barrels", () => {
            if (tick++ % 60 == 0) WaterPumps.Stroke(pump);
            plumbed.Tick(1 / 60f); Fed(plumbed);
            if (tank.Inventory[Water.Liquid] > 2) tank.Inventory.Remove(Water.Liquid, 1);
            if (branch.Inventory[Water.Liquid] > 2) branch.Inventory.Remove(Water.Liquid, 1);
        }, warmup: 180);
        Require(tank.ReceivingWater && branch.ReceivingWater, "Both tanks should still receive water after the measured ticks.");
    }
}
