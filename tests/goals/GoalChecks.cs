using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;

/// <summary>Checks of discovering and completing goals.</summary>
public static class GoalChecks {
    public static void Run(GameData data) {
        Test(CheckADeliveriesJobMeetsTheGoalOfAssigningAJob, data);
        Test(CheckSeeingOreUnlocksSteelToolsAndSurvivesLoading, data);
        Test(CheckSteelToolsGoalWaitsForBothAHammerAndAPickaxe, data);
        Test(CheckGoalsUnlockAndCompleteIndependently, data);
        Test(CheckSettlementRemembersWhatItHasHad, data);
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
        Require(world.Goals.Status(GoalId.SteelTools) == GoalStatus.Hidden, "loose coal and an unseen deposit should leave Steel tools hidden");
        var home = world.Player;
        world.Player = ore.Pos;
        world.Look();
        Require(world.Goals.Status(GoalId.SteelTools) == GoalStatus.Active, "seeing a mineable deposit should reveal Steel tools immediately");
        world.Player = home;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Goals.Status(GoalId.SteelTools) == GoalStatus.Active, "loading out of sight of ore should keep Steel tools visible");
    }

    private static void CheckSteelToolsGoalWaitsForBothAHammerAndAPickaxe(GameData data) {
        var world = EmptyWorld(data);
        world.Add(ThingKind.Coal, world.Player + Vector2.Right * 2, 40);
        world.Look();
        Achieve(world, "steel_pick");
        Require(world.Goals.Status(GoalId.SteelTools) == GoalStatus.Active, "a steel pickaxe alone should leave Steel tools open");
        Achieve(world, "steel_hammer");
        Require(world.Goals.Status(GoalId.SteelTools) == GoalStatus.Completed, "having had both tools should complete Steel tools");
    }

    private static void CheckGoalsUnlockAndCompleteIndependently(GameData data) {
        var world = EmptyWorld(data);
        foreach (var id in new[] { GoalId.Trade, GoalId.SteelBar, GoalId.SteelParts, GoalId.Mines })
            Require(world.Goals.Status(id) == GoalStatus.Hidden, $"{id} should be hidden in a fresh settlement");

        Achieve(world, "bench", "furnace");
        Require(world.Goals.Status(GoalId.Campfire) == GoalStatus.Active, "building stations should leave the campfire goal open");
        Require(world.Goals.Status(GoalId.Bench) == GoalStatus.Completed, "the bench goal can be completed before the campfire");
        Require(world.Goals.Status(GoalId.Trade) == GoalStatus.Active, "the bench should reveal Road to riches");
        Require(world.Goals.Status(GoalId.SteelBar) == GoalStatus.Active, "the furnace should reveal Hotter than fire");

        Achieve(world, "steel_bar");
        Require(world.Goals.Status(GoalId.SteelParts) == GoalStatus.Active, "the first steel bar should reveal Too soft");
        Require(world.Goals.Status(GoalId.Mines) == GoalStatus.Hidden, "Infinite power should stay hidden before the first steel parts");
        Achieve(world, "steel_parts");
        Require(world.Goals.Status(GoalId.Mines) == GoalStatus.Active, "completing Too soft should reveal Infinite power");
        foreach (var ore in new[] { ThingKind.Iron, ThingKind.Coal }) OpenMine(world.Add(ThingKind.Mine, Cliff.Of(ore).MineSpot));
        Require(world.Goals.Status(GoalId.Mines) == GoalStatus.Active, "iron and coal mines should leave the tin mine to build");
        OpenMine(world.Add(ThingKind.Mine, Cliff.Of(ThingKind.Tin).MineSpot));
        Require(world.Goals.Status(GoalId.Mines) == GoalStatus.Completed, "open iron, coal and tin mines should complete Infinite power");
    }

    private static void CheckSettlementRemembersWhatItHasHad(GameData data) {
        var fresh = new World(data);
        Require(fresh.Had("steel_axe") && fresh.Had("berries") && !fresh.Had("stone") && !fresh.Had("stone_chisel") && !fresh.Had("planks"), "a new settlement should know only what it starts with");
        Require(fresh.Had("steel_parts") && fresh.Had("iron_ore") && fresh.Had("coal") && fresh.Had("tin_bar") && fresh.Had("tin_ore"), "what the starting axe and bottle are made of should count as had along with them");
        var world = EmptyWorld(data);
        var tin = world.Add(ThingKind.Tin, Site(0, 0), 400);
        world.Player = tin.Pos;
        Give(world, "steel_pick", 1);
        GatherUntil(world, tin, "tin_ore", 4);
        Require(world.Had("tin_ore") && !world.Had("steel_pick"), "gathering should mark the ore as had, and a tool only handed over should not be");
        Give(world, "stone", 2);
        Require(world.Craft("stone_chisel"), "two stone should queue a stone chisel");
        SafeTick(world, data.Recipes["stone_chisel"].Seconds + 1);
        Require(world.Had("stone_chisel") && world.Had("stone"), "crafting should mark the product as had, and the stone it is made of with it");
        Require(!world.Had("iron_ore"), "ore nobody dug and nothing was made of should not count as had");
        Achieve(world, "iron_bar");
        Require(world.Had("iron_bar") && world.Had("iron_ore") && !world.Had("steel_bar"), "what the settlement's milestones say it made should count as had with its ingredients, and what is made of it should not");
        string save = world.Serialize();
        var loose = world.Add(ThingKind.LooseCoal, Site(2, 0), 1);
        world.Player = loose.Pos; world.Gather(loose, Step);
        var loaded = new World(data, false); loaded.Deserialize(save);
        Require(world.Had("coal") && loaded.Had("tin_ore") && !loaded.Had("coal"), "a save should keep what the settlement had when it was written");
    }
}
