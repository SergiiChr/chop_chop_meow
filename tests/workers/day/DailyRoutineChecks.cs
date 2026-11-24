using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

/// <summary>Checks of the crew's day: signing out at dusk down the valley and walking back at dawn.</summary>
public static class DailyRoutineChecks {
    public static void Run(GameData data) {
        Test(CheckCrewSignsOutAtDuskKeepingLoadsAndJobs, data);
        Test(CheckWorkersWalkOffDownTheValleyAtWalkingPace, data);
        Test(CheckCrewWalksBackAtDawnPaidAndKeepsItsJobs, data);
    }

    private static void CheckCrewSignsOutAtDuskKeepingLoadsAndJobs(GameData data) {
        var crew = StartDayCrew(data);
        var (world, tree, _, _, forge, cutter, pumper, _) = crew;
        var sign = SignOutCrew(crew);
        Require(world.Workers.All(w => w.Home), "at dusk every worker should sign out");
        Require(world.Workers.All(w => !world.Sees(w)), "a signed-out worker should be out of sight");
        Require(world.Workers.All(w => w.Commute == data.TradeSeconds / 2), "a signed-out worker should walk off for as long as a cart's trip");
        // Walking arrives within reach of the signpost, so the walk-off starts up to that far from it.
        Require(cutter.Pos.DistanceTo(sign.Pos + Vector2.One + Terrain.Seaward * World.TripDistance) < 1.25f,
                "a worker should sign out a cart's trip down the valley from the signpost");
        Require(cutter.Bag["wood"] == 2, "signing out should keep carried items");
        Require(cutter.Assignment == tree.Id, "signing out should keep the cutter's assignment");
        Require(pumper.Assignment == forge.Id, "signing out should keep the pumper's assignment");
        Require(world.FetchBlock(sign) == "Your workers have signed out until morning.", "nobody should run for prices after signing out");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Workers.All(w => w.Home), "signed-out workers should stay signed out after loading");
    }

    /// <summary>A worker signing out at dusk and a runner off for prices walk down the valley as fast as a worker walks, not stretched over the whole trip.</summary>
    private static void CheckWorkersWalkOffDownTheValleyAtWalkingPace(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(30, 20));
        var walker = AddWorker(world, sign.Pos + Vector2.One);
        RunUntil(world, data.DuskStarts * data.DaySeconds + .2f);
        var from = walker.Pos;
        SafeTick(world, 1);
        Require(Math.Abs(walker.Pos.DistanceTo(from) - World.WorkerSpeed) < .3f, $"a worker heading home should walk at walking pace, not {walker.Pos.DistanceTo(from):0.00} tiles a second");
        world = EmptyWorld(data);
        sign = world.Add(ThingKind.Signpost, new Vector2(30, 20));
        var runner = AddWorker(world, sign.Pos + Vector2.One);
        world.Assign(runner, sign);
        Require(world.FetchPrices(sign), "a morning runner should set off for prices");
        from = runner.Pos;
        SafeTick(world, 1);
        Require(Math.Abs(runner.Pos.DistanceTo(from) - World.WorkerSpeed) < .3f, $"a runner off for prices should walk at walking pace, not {runner.Pos.DistanceTo(from):0.00} tiles a second");
    }

    private static void CheckCrewWalksBackAtDawnPaidAndKeepsItsJobs(GameData data) {
        var crew = StartDayCrew(data);
        var (world, tree, chest, _, forge, cutter, pumper, hauler) = crew;
        var sign = SignOutCrew(crew);
        RunUntil(world, data.DaySeconds - 1);
        world.SetFilter(chest, Inventory.Everything, false, true);
        SafeTick(world, 1 + data.TradeSeconds / 4);
        Require(cutter.Commute > 0, "at dawn a worker should fade in on the walk");
        Require(!cutter.Home, "at dawn a worker should no longer be home");
        Require(cutter.Pos.DistanceTo(sign.Pos + Vector2.One) < World.TripDistance,
            "at dawn a worker should walk back up the valley from nearby");
        SafeTick(world, 25);
        Require(world.Workers.All(w => !w.Home && w.WageDebt == 0),
            "at dawn paid workers should all be back and owed nothing");
        Require(cutter.Pos.DistanceTo(sign.Pos) > 5, "at dawn the cutter should walk back to work");
        Require(pumper.Pos.DistanceTo(sign.Pos) > 5, "at dawn the pumper should walk back to work");
        Require(cutter.Assignment == tree.Id, "a night away should keep the cutter's assignment");
        Require(pumper.Assignment == forge.Id, "a night away should keep the pumper's assignment");
        Require(hauler.Assignment == forge.Id, "a night away should keep the hauler's assignment");
    }
}
