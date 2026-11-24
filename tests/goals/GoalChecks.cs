using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of discovering and completing quests.</summary>
public static class GoalChecks {
    public static void Run(GameData data) {
        Test(CheckADeliveriesJobMeetsTheGoalOfAssigningAJob, data);
        Test(CheckSeeingOreUnlocksSteelToolsAndSurvivesLoading, data);
        Test(CheckSteelToolsGoalWaitsForBothAHammerAndAPickaxe, data);
        Test(CheckQuestsUnlockAndCompleteIndependently, data);
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

    private static void CheckSeeingOreUnlocksSteelToolsAndSurvivesLoading(GameData data) {
        var world = EmptyWorld(data);
        var ore = world.Add(ThingKind.Iron, world.Player + Vector2.Right * (data.SightRadius + 5), 40);
        world.Add(ThingKind.LooseCoal, world.Player + Vector2.Right * 2, 1);
        world.Look();
        Require(world.Quests.Status(QuestId.SteelTools) == QuestStatus.Hidden, "loose coal and an unseen deposit should leave Steel tools hidden");
        var home = world.Player;
        world.Player = ore.Pos;
        world.Look();
        Require(world.Quests.Status(QuestId.SteelTools) == QuestStatus.Active, "seeing a mineable deposit should reveal Steel tools immediately");
        world.Player = home;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Quests.Status(QuestId.SteelTools) == QuestStatus.Active, "loading out of sight of ore should keep Steel tools visible");
    }

    private static void CheckSteelToolsGoalWaitsForBothAHammerAndAPickaxe(GameData data) {
        var world = EmptyWorld(data);
        world.Add(ThingKind.Coal, world.Player + Vector2.Right * 2, 40);
        world.Look();
        Achieve(world, "steel_pick");
        Require(world.Quests.Status(QuestId.SteelTools) == QuestStatus.Active, "a steel pickaxe alone should leave Steel tools open");
        Achieve(world, "steel_hammer");
        Require(world.Quests.Status(QuestId.SteelTools) == QuestStatus.Completed, "having had both tools should complete Steel tools");
    }

    private static void CheckQuestsUnlockAndCompleteIndependently(GameData data) {
        var world = EmptyWorld(data);
        foreach (var id in new[] { QuestId.Trade, QuestId.SteelBar, QuestId.SteelParts, QuestId.Mines })
            Require(world.Quests.Status(id) == QuestStatus.Hidden, $"{id} should be hidden in a fresh settlement");

        Achieve(world, "bench", "furnace");
        Require(world.Quests.Status(QuestId.Campfire) == QuestStatus.Active, "building stations should leave the campfire quest open");
        Require(world.Quests.Status(QuestId.Bench) == QuestStatus.Completed, "the bench quest can be completed before the campfire");
        Require(world.Quests.Status(QuestId.Trade) == QuestStatus.Active, "the bench should reveal Road to riches");
        Require(world.Quests.Status(QuestId.SteelBar) == QuestStatus.Active, "the furnace should reveal Hotter than fire");

        Achieve(world, "steel_bar");
        Require(world.Quests.Status(QuestId.SteelParts) == QuestStatus.Active, "the first steel bar should reveal Too soft");
        Require(world.Quests.Status(QuestId.Mines) == QuestStatus.Hidden, "Infinite power should stay hidden before the first steel parts");
        Achieve(world, "steel_parts");
        Require(world.Quests.Status(QuestId.Mines) == QuestStatus.Active, "completing Too soft should reveal Infinite power");
        foreach (var ore in new[] { ThingKind.Iron, ThingKind.Coal }) OpenMine(world.Add(ThingKind.Mine, Cliff.Of(ore).MineSpot));
        Require(world.Quests.Status(QuestId.Mines) == QuestStatus.Active, "iron and coal mines should leave the tin mine to build");
        OpenMine(world.Add(ThingKind.Mine, Cliff.Of(ThingKind.Tin).MineSpot));
        Require(world.Quests.Status(QuestId.Mines) == QuestStatus.Completed, "open iron, coal and tin mines should complete Infinite power");
    }
}
