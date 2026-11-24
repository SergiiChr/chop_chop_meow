using ChopChopMeow;
using static SetupSteps;
using static WorldSteps;

public static class ReplantingAllocationChecks {
    public static void CheckWoodcuttingStaysWithinAllocationBudget(GameData data) {
        var settlement = Settlement(data);
        // Warm up workers' trips and the status and cost caches.
        // During the measured ticks a store can see a new item, and felled trees and acorn drops create things and memories.
        AllocationChecks.Check("tick: woodcutting in a working settlement", () => {
            settlement.Tick(1 / 60f);
            Fed(settlement);
        }, maxBytesPerRun: 20, warmup: 1200);
    }
}
