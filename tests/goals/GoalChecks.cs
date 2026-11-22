using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the starter goals: what meets them.</summary>
public static class GoalChecks {
    public static void Run(GameData data) {
        Test(CheckADeliveriesJobMeetsTheGoalOfAssigningAJob, data);
    }

    /// <summary>
    /// A worker given the trading post's deliveries, by being assigned there or by having the job ticked, meets the
    /// starter goal of assigning a job, as a job anywhere else does.
    /// </summary>
    private static void CheckADeliveriesJobMeetsTheGoalOfAssigningAJob(GameData data) {
        foreach (bool ticked in new[] { false, true }) {
            var world = EmptyWorld(data);
            var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
            var worker = AddWorker(world, sign.Pos + Vector2.One);
            Require(!world.Reached("assigned"), "a worker with no job should not meet the goal of assigning one");
            if (ticked) world.SetDuty(worker, sign, JobKind.Deliveries, true); else world.Assign(worker, sign);
            Require(World.HasDuty(worker, sign.Id, JobKind.Deliveries) && world.Reached("assigned"),
                $"a worker {(ticked ? "given deliveries by ticking the job" : "assigned to the trading post")} should meet the goal of assigning a job");
        }
    }
}
