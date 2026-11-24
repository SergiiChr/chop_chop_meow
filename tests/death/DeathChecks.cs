using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of death: the recovery box, respawning and what a save keeps.</summary>
public static class DeathChecks {
    public static void Run(GameData data) {
        Test(CheckRecoveryBoxesStayPrivate, data);
        Test(CheckDeathLeavesRecoveryBoxAndRespawnsAndSavesKeepState, data);
    }

    private static void CheckRecoveryBoxesStayPrivate(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(12, 10);
        Give(world, "wood", 5);
        var tree = world.Add(ThingKind.Tree, new Vector2(13, 10), 30);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 10)));
        var worker = AddWorker(world, new Vector2(14, 10));
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        world.Die();
        var box = world.Things.Last(t => t.Kind == ThingKind.Grave);
        SafeTick(world, 25);
        Require(box.Inventory["wood"] == 5 && chest.Inventory["wood"] > 0,
                "workers must store gathered goods in chests, not in the player's recovery box");

        var furnace = world.Add(ThingKind.Furnace, new Vector2(13, 13));
        world.Player = box.Pos;
        Give(world, "iron_ore", 4);
        world.Die();
        var oreBox = world.Things.Last(t => t.Kind == ThingKind.Grave);
        world.Assign(worker, furnace);
        SafeTick(world, 15);
        Require(oreBox.Inventory["iron_ore"] == 4, "workers must not haul inputs out of a recovery box");

        SafeTick(world, data.RespawnSeconds);
        world.Player = box.Pos;
        foreach (var id in box.Inventory.Items.Keys.ToArray()) world.Transfer(box.Inventory, world.Backpack, id, 999);
        SafeTick(world, .1f);
        Require(world.Things.All(t => t.Id != box.Id), "an emptied recovery box should disappear");
    }

    private static void CheckDeathLeavesRecoveryBoxAndRespawnsAndSavesKeepState(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(9, 12);
        Give(world, "iron_ore", 7);
        Give(world, "berries", 3);
        world.Die();
        var grave = world.Things.LastOrDefault(t => t.Kind == ThingKind.Grave);
        Require(grave != null, "death should leave a recovery box");
        Require(grave.Pos == new Vector2(9, 12), "death should place the box at the death location");
        Require(grave.Inventory["iron_ore"] == 7, "death should put carried items in the box");
        Require(world.Dead, "death should mark the player dead");
        Require(world.RespawnIn == data.RespawnSeconds, "death should start the full respawn timer");
        Require(world.Backpack.Count == 0, "death should replace the emptied backpack");
        world.MovePlayer(Vector2.One);
        Require(world.Player == new Vector2(9, 12), "a collapsed player must not move before respawning");
        SafeTick(world, data.RespawnSeconds - .5f);
        Require(world.Dead && world.Player == new Vector2(9, 12), "the player must stay down for the whole respawn timer");
        SafeTick(world, 1);
        Require(!world.Dead, "the player should come back once the timer ends");
        Require(world.Player == Terrain.Spawn, "the player should respawn at the clearing");
        Require(world.Health == 100, "the player should respawn healthy");

        var saved = EmptyWorld(data);
        var furnace = saved.Add(ThingKind.Furnace, new Vector2(14, 17));
        furnace.Inventory.Add("wood", 2);
        furnace.Processing = true;
        furnace.Progress = 2.5f;
        PlaceOrder(saved, new TradeOrder { Goods = new() { ["wood"] = 4 }, Buying = true, Value = 7.5f, Elapsed = 17, Signpost = 31 });
        SetMarket(saved, "wood", 1.1f, 1.4f);
        var worker = AddWorker(saved, new Vector2(15, 18));
        worker.Bag.Add("stone", 2);
        worker.Destination = furnace.Id;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(saved.Serialize());
        var loadedFurnace = loaded.Find(furnace.Id);
        Require(loadedFurnace != null, "save/load should keep the furnace");
        Require(loadedFurnace.Processing, "save/load should keep the batch running");
        Require(Math.Abs(loadedFurnace.Progress - 2.5f) < .01f, "save/load should keep the batch's progress");
        Require(loaded.Orders.Count == 1, "save/load should preserve the order");
        Require(Math.Abs(loaded.Orders[0].Elapsed - 17) < .01f, "save/load should preserve the order's elapsed cart time");
        Require(loaded.Orders[0].Goods["wood"] == 4, "save/load should preserve the order's goods");
        Require(Math.Abs(loaded.KnownPrice("wood") - 1.1f) < .01f, "save/load should preserve market prices");
        Require(loaded.Workers.Count == 1, "save/load should keep the worker");
        Require(loaded.Workers[0].Bag["stone"] == 2, "save/load should preserve worker cargo");
        Require(loaded.Workers[0].Destination == furnace.Id, "save/load should preserve the worker's destination");
        Require(Math.Abs(loaded.Demand("wood") - 1.4f) < .01f,
                "save/load should preserve market demand");
    }
}
