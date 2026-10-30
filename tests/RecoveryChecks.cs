using System;
using System.Collections.Generic;
using Godot;
using ChopChopMeow;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

/// <summary>Checks the no-cabin recovery path and its world-time effects.</summary>
public static class RecoveryChecks {
    public static void Run(GameData data) {
        CheckSkipToMorningNeedsNoCabinAndNoOtherLivingPlayer(data);
        CheckSkipAdvancesWorld(data);
    }

    private static void CheckSkipToMorningNeedsNoCabinAndNoOtherLivingPlayer(GameData data) {
        var world = new World(data, false);
        world.Die();
        Require(world.CanSkipMorning, "a downed player without a cabin should be able to skip to morning");
        world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(40, 0));
        Require(!world.CanSkipMorning, "a cabin anywhere in the world should block the no-shelter skip");

        RemoveThings(world, t => t.Kind == ThingKind.Hut);
        var other = world.AddGuest(2, null);
        Require(!world.CanSkipMorning, "another living player should prevent skipping the shared night");
        SetRespawn(other, 20);
        Require(world.CanSkipMorning, "the skip should be available once every other player is downed");
    }

    private static void CheckSkipAdvancesWorld(GameData data) {
        var world = new World(data, false);
        RunUntilPhase(world, data.NightStarts + .01f);
        float timeToDawn = data.DaySeconds - world.Time % data.DaySeconds + 1;
        SetRespawn(world, timeToDawn / 2);
        SetNeeds(world, 90, 90);
        var sign = world.Add(ThingKind.Signpost, Terrain.Spawn + new Vector2(4, 0));
        var worker = AddWorker(world, sign.Pos);
        worker.Wage = 5;
        PlaceOrder(world, new TradeOrder {
            Goods = new Dictionary<string, int> { ["berries"] = 1 },
            Value = 20,
            Signpost = sign.Id,
            Elapsed = data.TradeSeconds - .1f
        });
        var furnace = world.Add(ThingKind.Furnace, Terrain.Spawn + new Vector2(-4, 0));
        furnace.Inventory.Add("iron_ore", 2); furnace.Burner!.Add("wood", 1);
        furnace.Processing = true; furnace.Progress = data.Recipes["iron_bar"].Seconds - .1f;

        float priorTime = world.Time;
        world.SkipMorning();

        Require(world.Time > priorTime && world.Hour >= 6,
                "skipping should advance normal world time to dawn");
        Require(!world.Dead, "a skipped night should end the respawn");
        Require(world.Health == 100, "the player should wake at full health");
        Require(world.RespawnIn == 0, "the respawn timer should be cleared after the skip");
        Require(world.Hunger < 65 && world.Thirst < 65,
                "survival needs should keep draining after the ordinary respawn during the skip");
        Require(world.Orders.Count == 0 && sign.Wages![Inventory.Coins] == 15,
                "a returning trade should settle during the skip and fund dawn wages");
        Require(worker.WageDebt == 0 && sign.Wages![Inventory.Coins] == 15,
                "dawn should charge wages through the regular wage slot");
        Require(furnace.Inventory["iron_bar"] > 0,
                "furnace production should continue while the night advances");
    }
}
