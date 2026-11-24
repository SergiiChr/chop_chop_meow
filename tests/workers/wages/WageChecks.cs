using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

/// <summary>Checks of wages: an unpaid worker strikes until paid.</summary>
public static class WageChecks {
    public static void Run(GameData data) {
        Test(CheckUnpaidWorkerStrikes, data);
        Test(CheckStrikeResetsWorkState, data);
    }

    /// <summary>A worker the player cannot pay at dawn strikes for that one wage until the debt is paid in full.</summary>
    private static void CheckUnpaidWorkerStrikes(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        RunUntil(world, data.DaySeconds * world.Day - .1f);
        var cheap = data.HireOffers.OrderBy(o => o.Upfront).First();
        SetCoins(world, (int)cheap.Upfront);
        var worker = world.Hire(sign, cheap);
        Require(worker != null && worker.Wage == cheap.Wage, "player should be able to hire an individually tracked worker");
        var workTarget = world.Add(ThingKind.Tree, new Vector2(12, 10), 6);
        worker!.Tool = "steel_axe";
        world.Assign(worker, workTarget);
        SetCoins(world, 0);
        SafeTick(world, .2f);
        Require(worker!.WageDebt == cheap.Wage, "an unpaid dawn wage should leave the worker owed their wage");
        Require(workTarget.Remaining == 6, "a worker on strike should not work the target");
        Require(worker.Bag.Count == 0, "a worker on strike should carry nothing");
        SetCoins(world, (int)(cheap.Wage - 1));
        world.SettleDebt(worker);
        Require(worker.WageDebt == cheap.Wage, "insufficient funds must not partly clear wage debt");
        SetCoins(world, (int)cheap.Wage);
        world.SettleDebt(worker);
        Require(worker.WageDebt == 0, "paying the full owed wage should clear the strike debt");
        SetCoins(world, 0);
        RunUntilDawn(world);
        RunUntilDawn(world);
        Require(worker.WageDebt == cheap.Wage, "a striking worker should not keep adding wages to the debt");
    }

    private static void CheckStrikeResetsWorkState(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 12);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(12, 10)));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        worker.WorkState = WorkerWorkState.Gathering;
        worker.WageDebt = worker.Wage;

        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "a worker on strike should have no active work animation state");
    }
}
