using System.Text.Json;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

internal static class DebugChestChecks {
    internal static void Run(GameData data) => Test(CheckADebugChestKeepsTakingNewStacksAfterLoading, data);

    private static void CheckADebugChestKeepsTakingNewStacksAfterLoading(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + new Vector2(2, 0));
        chest.Inventory = JsonSerializer.Deserialize<Inventory>("""{"Slots":5,"UnlimitedSlots":true,"Gated":true}""")!;
        Require(Give(world, "wood", 390) == 390, "the player should have more wood than fits in an ordinary chest");
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 390) == 390,
            "a debug chest should take more than five full stacks");
        world.Deserialize(world.Serialize());
        chest = world.Find(chest.Id)!;
        Require(chest.Inventory["wood"] == 390, "saving and loading should keep the supplies");
        Require(Give(world, "stone", 128) == 128 && world.Transfer(world.Backpack, chest.Inventory, "stone", 128) == 128,
            "the debug chest should still have room for new kinds of supplies after loading");
    }
}
