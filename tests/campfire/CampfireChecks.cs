using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the campfire: a worker on Tend fire keeps it fed from storage.</summary>
public static class CampfireChecks {
    public static void Run(GameData data) {
        Test(CheckCampfireWorkerFetchesFuelFromAllowedStorage, data);
    }

    private static void CheckCampfireWorkerFetchesFuelFromAllowedStorage(GameData data) {
        var world = EmptyWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(24, 20)));
        chest.Inventory.Add("wood", 6);
        chest.Inventory.Filter("stone", true, true);
        var worker = AddWorker(world, fire.Pos);
        world.Assign(worker, fire);

        SafeTick(world, 3);
        Require(chest.Inventory["wood"] == 6 && fire.Inventory["wood"] == 0,
                "campfire worker must respect the source chest output filter when fetching fuel");

        chest.Inventory.Filter("wood", true, true);
        bool sawFirewoodInTransit = false;
        for (int i = 0; i < 120 && chest.Inventory["wood"] == 6; i++) {
            SafeTick(world, Step);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        for (int i = 0; i < 80 && (worker.Bag["wood"] > 0 || fire.Inventory["wood"] == 0); i++) {
            SafeTick(world, Step);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        Require(sawFirewoodInTransit && chest.Inventory["wood"] == 3,
                "campfire worker should carry three real wood from output-enabled storage");
        SafeTick(world, Step);
        Require(fire.Inventory["wood"] > 0 && fire.Burn > 0,
                "carried firewood should be deposited into the fire and consumed to sustain its burn");
        int storedBeforeBurn = fire.Inventory["wood"];
        SafeTick(world, fire.Burn - 29);
        Require(fire.Inventory["wood"] < storedBeforeBurn && fire.Burn > 0,
                "campfire should consume stored wood when its burn falls low");
    }
}
