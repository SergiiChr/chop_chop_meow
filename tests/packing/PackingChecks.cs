using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

internal static class PackingChecks {
    public static void Run(GameData data) {
        Test(CheckPackingInputsEachHoldOneStackByHand, data);
        Test(CheckChestHaulerStopsDeliveringWhenPackingInputsFill, data);
    }

    private static void CheckPackingInputsEachHoldOneStackByHand(GameData data) {
        var world = EmptyWorld(data);
        var station = world.Add(ThingKind.Packer, world.Player + Vector2.Down * 2);
        world.SetPacking(station, "wood");
        Give(world, "wood", 90);
        Give(world, "crate", 90);
        Require(world.Transfer(world.Backpack, station.Inventory, "wood", 90) == 64,
            "a bulk transfer should stop after one stack of the good");
        Require(world.Transfer(world.Backpack, station.Inventory, "crate", 90) == 64,
            "the empty crate input should have its own one-stack limit");
        Require(world.LoadInput(station, "crate", 1) == 0 && world.Backpack["crate"] >= 26,
            "another crate should stay with the player while the crate input is full");
    }

    private static void CheckChestHaulerStopsDeliveringWhenPackingInputsFill(GameData data) {
        var world = FundedWorld(data);
        var at = Terrain.Spawn;
        var station = world.Add(ThingKind.Packer, at + Vector2.Right * 3);
        world.SetPacking(station, "wood");
        var chest = world.Add(ThingKind.Chest, at);
        chest.Inventory.Add("wood", 90);
        chest.Inventory.Add("crate", 90);
        world.SetDuty(AddWorker(world, at), chest, JobKind.Haul, true);
        Work(world, 70);
        Require(station.Inventory["wood"] == 64 && chest.Inventory["wood"] == 26,
            $"the hauler should deliver 64 wood and leave 26 in the chest, found {station.Inventory["wood"]} at the station");
        Require(station.Inventory["crate"] == 64 && chest.Inventory["crate"] == 26,
            $"the hauler should deliver 64 empty crates and leave 26 in the chest, found {station.Inventory["crate"]} at the station");
    }
}
