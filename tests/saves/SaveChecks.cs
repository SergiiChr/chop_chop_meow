using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Checks of saves: what a save the game cannot take is refused with.</summary>
public static class SaveChecks {
    public static void Run(GameData data) {
        Test(CheckSaveWithUnknownItemRefused, data);
    }

    /// <summary>A save naming an item the game does not know, in the backpack or as a worker's tool, is refused with a clear reason instead of failing mid-game.</summary>
    private static void CheckSaveWithUnknownItemRefused(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "tin_can", 2);
        var worker = AddWorker(world, new Vector2(12, 12));
        worker.Tool = "steel_hammer"; worker.Bag.Add("wood", 2);
        // An anvil does not make pickaxes, which are made at the bench.
        world.Add(ThingKind.Anvil, new Vector2(10, 10));
        string json = world.Serialize();
        EmptyWorld(data).Deserialize(json);
        foreach (var edit in new[] { ("\"tin_can\"", "\"unobtainium\""), ("\"steel_hammer\": 1", "\"unobtainium\": 1"), ("\"wood\": 2", "\"unobtainium\": 2"), ("\"Keep\": \"steel_parts\"", "\"Keep\": \"steel_pick\""), ("\"Goal\": 0", "\"Goal\": -1") }) {
            bool refused = false;
            try { EmptyWorld(data).Deserialize(Patch(json, edit)); } catch (InvalidOperationException error) { refused = error.Message.Contains("unknown items"); }
            Require(refused, $"a save with {edit.Item2} in place of {edit.Item1} should be refused with a clear reason");
        }
    }
}
