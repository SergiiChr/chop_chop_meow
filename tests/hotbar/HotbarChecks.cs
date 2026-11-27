using ChopChopMeow;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the hotbar: a slot stays bound to its stack while the player uses it.</summary>
public static class HotbarChecks {
    public static void Run(GameData data) {
        Test(CheckABottleStaysBoundThroughSips, data);
    }

    private static void CheckABottleStaysBoundThroughSips(GameData data) {
        var world = EmptyWorld(data);
        string bottle = GiveWater(world);
        PutOnHotbar(world, SlotOf(world.Backpack, bottle), 8);
        SetNeeds(world, thirst: 20);
        for (int liter = 0; liter < 3; liter++) world.Consume(world.HotbarItem(8));
        Require(world.HotbarItem(8) == Water.Empty, "a bound bottle should stay bound through every liter drunk, empty at the end");
    }
}
