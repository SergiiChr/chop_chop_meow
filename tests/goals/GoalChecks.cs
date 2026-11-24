using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;

/// <summary>Checks of the starter goals: what meets them.</summary>
public static class GoalChecks {
    public static void Run(GameData data) {
        Test(CheckADeliveriesJobMeetsTheGoalOfAssigningAJob, data);
        Test(CheckSteelToolsGoalWaitsForBothAHammerAndAPickaxe, data);
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

    /// <summary>
    /// The steel tools goal, after the first steel bar, stays open with only a steel pickaxe had and is met once a steel
    /// hammer is had too, bought or made.
    /// </summary>
    private static void CheckSteelToolsGoalWaitsForBothAHammerAndAPickaxe(GameData data) {
        var world = EmptyWorld(data);
        Achieve(world, new[] { "campfire", "hut", "bench", "sold", "furnace", "steel_bar" });
        world.Tick(.01f);
        Require(World.ObjectiveFor(world.Goal).Title == "Steel tools", $"the goal after the first steel bar should be Steel tools, not {World.ObjectiveFor(world.Goal).Title}");
        Achieve(world, "steel_pick");
        world.Tick(.01f);
        Require(World.ObjectiveFor(world.Goal).Title == "Steel tools", "a steel pickaxe alone should leave the steel tools goal open");
        Achieve(world, "steel_hammer");
        world.Tick(.01f);
        Require(World.ObjectiveFor(world.Goal).Title == "Too soft", $"a steel pickaxe and a steel hammer should meet the steel tools goal, not leave it on {World.ObjectiveFor(world.Goal).Title}");
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
