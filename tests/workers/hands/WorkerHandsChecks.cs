using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;

/// <summary>Checks of a worker's hands and tool slot as a player takes from and gives to them.</summary>
public static class WorkerHandsChecks {
    public static void Run(GameData data) {
        Test(CheckPlayerTakesWhatAWorkerCarries, data);
        Test(CheckPlayerGivesAWorkerALoadToCarryOff, data);
        Test(CheckPlayerTakesAndGivesAWorkersTool, data);
        Test(CheckAWorkerKeepsALoadAPlayerHoldsUntilTheyLetGo, data);
        Test(CheckAToolHeldFromAWorkerIsLetGoOnLoad, data);
        Test(CheckAWorkerDeliversWhatAPlayerDoesNotHold, data);
    }

    private static void CheckPlayerTakesWhatAWorkerCarries(GameData data) {
        var world = EmptyWorld(data);
        var said = new System.Collections.Generic.List<string>();
        world.Notify += said.Add;
        var worker = AddWorker(world, world.Player + new Vector2(40, 0));
        worker.Bag.Add("wood", 12);
        world.Hold(worker.Bag, 0, 12);
        Require(world.Held == null && said.Contains("Walk closer first."), "a player far from a worker should not pick up what they carry, and be told to walk closer");
        world.Player = worker.Pos + Vector2.One;
        world.Hold(worker.Bag, 0, 12);
        Require(world.Held is { Item: "wood", Count: 12 } && worker.Bag["wood"] == 12, "a player beside a worker should pick up their load, which stays in the worker's hands while held");
        world.FromHeldStack(() => world.Transfer(worker.Bag, world.Backpack, "wood", 12));
        Require(world.Backpack["wood"] == 12 && worker.Bag.Count == 0 && world.Held == null, "putting the held load in the backpack should take it out of the worker's hands");
    }

    private static void CheckPlayerGivesAWorkerALoadToCarryOff(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var worker = AddWorker(world, chest.Pos + new Vector2(6, 0));
        world.Player = worker.Pos + Vector2.One;
        Give(world, "stone", 20);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "stone"), 20);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.Bag, "stone", 20));
        Require(worker.Bag["stone"] == 12 && world.Backpack["stone"] == 8 && world.Held is { Count: 8 }, $"a worker should take 12 of a held stack of 20 and leave 8 held: {worker.Bag["stone"]} taken");
        world.Release();
        Work(world, 30, () => chest.Inventory["stone"] > 0);
        Require(chest.Inventory["stone"] == 12 && worker.Bag.Count == 0, "a worker with no jobs should carry what they are given to storage");
    }

    private static void CheckPlayerTakesAndGivesAWorkersTool(GameData data) {
        var world = EmptyWorld(data);
        var said = new System.Collections.Generic.List<string>();
        world.Notify += said.Add;
        var worker = AddWorker(world, new Vector2(20, 20));
        worker.Tool = "steel_axe";
        world.Player = worker.Pos + Vector2.One;
        world.Hold(worker.ToolSlot, 0, 1);
        world.FromHeldStack(() => world.Transfer(worker.ToolSlot, world.Backpack, "steel_axe", 1));
        Require(worker.Tool == "" && world.Backpack["steel_axe"] == 1, "a player should take the tool out of a worker's tool slot");
        Give(world, "wood", 1); Give(world, "steel_pick", 1);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "wood"), 1);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.ToolSlot, "wood", 1));
        Require(worker.Tool == "" && said.Contains("A worker's tool slot only takes tools."), "a worker's tool slot should refuse wood and say why");
        world.Hold(world.Backpack, SlotOf(world.Backpack, "steel_pick"), 1);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.ToolSlot, "steel_pick", 1));
        Require(worker.Tool == "steel_pick" && world.Backpack["steel_pick"] == 0, "a player should put a tool into a worker's empty tool slot");
        world.Hold(world.Backpack, SlotOf(world.Backpack, "steel_axe"), 1);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.ToolSlot, "steel_axe", 1));
        Require(worker.Tool == "steel_pick" && world.Backpack["steel_axe"] == 1, "a worker's tool slot should hold one tool");
    }

    private static void CheckAWorkerKeepsALoadAPlayerHoldsUntilTheyLetGo(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var worker = AddWorker(world, chest.Pos + new Vector2(1, 0));
        worker.Bag.Add("wood", 12);
        world.Player = worker.Pos + Vector2.One;
        world.Hold(worker.Bag, 0, 12);
        Work(world, 5);
        Require(chest.Inventory["wood"] == 0 && worker.Bag["wood"] == 12, "a worker should not store a load a player holds on their cursor");
        world.Release();
        Work(world, 5, () => chest.Inventory["wood"] > 0);
        Require(chest.Inventory["wood"] == 12, "a worker should store the load once the player lets go of it");
    }

    /// <summary>The autosave can run while a player holds a worker's tool on the cursor; after loading, the slot works as any other.</summary>
    private static void CheckAToolHeldFromAWorkerIsLetGoOnLoad(GameData data) {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, world.Player + Vector2.One);
        worker.Tool = "steel_axe";
        world.Hold(worker.ToolSlot, 0, 1);
        Require(world.Held is { Item: "steel_axe" }, "the player should hold the worker's axe");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var back = loaded.Workers.Single(w => w.Id == worker.Id);
        var rack = TakeEverything(loaded, loaded.Add(ThingKind.Toolstation, back.Pos + new Vector2(3, 0)));
        Work(loaded, 30, () => rack.Inventory["steel_axe"] == 1);
        Require(rack.Inventory["steel_axe"] == 1 && back.Tool == "" && back.ToolSlot.Room("steel_pick") == 1,
            $"after loading, a worker with no job should put the axe back and have room for another tool: rack {rack.Inventory["steel_axe"]}, tool \"{back.Tool}\"");
    }

    private static void CheckAWorkerDeliversWhatAPlayerDoesNotHold(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + new Vector2(3, 0)));
        var worker = AddWorker(world, world.Player + Vector2.One);
        worker.Bag.Add("wood", 6); worker.Bag.Add("stone", 6);
        world.Hold(worker.Bag, worker.Bag.Layout.FindIndex(s => s.Item == "wood"), 6);
        Work(world, 20, () => chest.Inventory["stone"] > 0);
        Require(chest.Inventory["stone"] == 6 && worker.Bag["wood"] == 6, "a worker should store the stone while the player holds the wood");
    }
}
