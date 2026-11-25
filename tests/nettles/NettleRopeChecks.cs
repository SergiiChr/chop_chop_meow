using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;
using static NettleSteps;

/// <summary>Checks of nettle rope: made at the bench with a wooden mallet only, by a player or a worker, and never traded.</summary>
public static class NettleRopeChecks {
    public static void Run(GameData data) {
        Test(CheckMalletAtTheBenchMakesARopeOfANettle, data);
        Test(CheckSteelHammerAloneMakesNoRope, data);
        Test(CheckCarriedMalletMakesRopeAtABenchWithASteelHammer, data);
        Test(CheckSlottedMalletMakesRopeForAPlayerCarryingASteelHammer, data);
        Test(CheckBenchWorkerMakesRopeOnlyWithAMalletSlotted, data);
        Test(CheckRopeIsNotMadeByHand, data);
        Test(CheckOnlyTheMalletIsSaidToMakeRope, data);
        Test(CheckNettlesAndFirebugGoodsAreNotTraded, data);
    }

    private static void CheckOnlyTheMalletIsSaidToMakeRope(GameData data) {
        Require(data.UsedFor("wood_hammer").Contains("nettle_rope"), "a wooden mallet should be said to be used for nettle rope");
        Require(!data.UsedFor("steel_hammer").Contains("nettle_rope"), "a steel hammer should not be said to be used for nettle rope");
        Require(data.UsedFor("steel_hammer").Contains("bug_net"), "a steel hammer should still be said to be used for a bug net");
    }

    private static void CheckMalletAtTheBenchMakesARopeOfANettle(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "nettle", 1);
        var bench = BenchLeftWithAMallet(world, "nettle_rope");
        Require(bench.Inventory["nettle_rope"] == 1 && bench.Inventory["nettle"] == 0 && world.Backpack["nettle"] == 0, $"one nettle should make one rope at a bench with a mallet: {bench.Inventory["nettle_rope"]}");
    }

    private static void CheckSteelHammerAloneMakesNoRope(GameData data) {
        var (world, bench, said) = RopeBench(data, slotted: "steel_hammer");
        Require(!world.HasToolFor(bench, data.Recipes["nettle_rope"]), "a steel hammer should not count as the tool for nettle rope");
        world.QueueBatch(bench, "nettle_rope");
        Require(said.Contains("Steel hammer will damage the fibers."), $"asking for rope with only a steel hammer should say why not: {string.Join(" | ", said)}");
        Require(world.Waiting(bench, "nettle_rope") == 0 && bench.Inventory["nettle"] == 0 && world.Backpack["nettle"] == 1, "a refused rope should queue nothing and leave the nettle in the backpack");
        said.Clear();
        world.LoadBatch(bench);
        Require(said.Contains("Steel hammer will damage the fibers.") && bench.Inventory["nettle"] == 0, "loading a batch of rope with only a steel hammer should be refused the same way");
        SafeTick(world, 20);
        Require(bench.Inventory["nettle_rope"] == 0 && !bench.Processing, "a bench with only a steel hammer should start no rope");
    }

    /// <summary>A rope takes 10 s over the bench's speed of 2; a steel hammer's 1.8 would have made it in 2.8 s.</summary>
    private static void CheckCarriedMalletMakesRopeAtABenchWithASteelHammer(GameData data) {
        var (world, bench, said) = RopeBench(data, slotted: "steel_hammer");
        Give(world, "wood_hammer");
        world.QueueBatch(bench, "nettle_rope");
        Require(!said.Contains("Steel hammer will damage the fibers."), "a carried mallet should keep the steel hammer's warning away");
        SafeTick(world, 4);
        Require(bench.Processing && bench.Inventory["nettle_rope"] == 0, "the rope should be under way at the mallet's pace after 4 s, not made at the steel hammer's");
        SafeTick(world, 1.5f);
        Require(bench.Inventory["nettle_rope"] == 1, "a carried mallet should make the rope in 5 s at a bench whose slot holds a steel hammer");
    }

    private static void CheckSlottedMalletMakesRopeForAPlayerCarryingASteelHammer(GameData data) {
        var (world, bench, said) = RopeBench(data, slotted: "wood_hammer");
        Give(world, "steel_hammer");
        world.QueueBatch(bench, "nettle_rope");
        Require(!said.Contains("Steel hammer will damage the fibers."), "a slotted mallet should keep the steel hammer's warning away");
        SafeTick(world, 4);
        Require(bench.Processing && bench.Inventory["nettle_rope"] == 0, "the rope should be under way at the slotted mallet's pace after 4 s, not made at the carried steel hammer's");
        SafeTick(world, 1.5f);
        Require(bench.Inventory["nettle_rope"] == 1, "the slotted mallet should make the rope in 5 s for a player carrying a steel hammer");
    }

    private static void CheckBenchWorkerMakesRopeOnlyWithAMalletSlotted(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, Site(0, 4), "nettle_rope", "steel_hammer");
        bench.Inventory.Add("nettle", 1);
        var maker = Staff(world, bench, JobKind.Craft);
        Work(world, 20);
        Require(bench.Inventory["nettle_rope"] == 0 && maker.Status.Contains("wooden mallet"), $"a worker at a bench with a steel hammer should wait for a wooden mallet: {maker.Status}");
        bench.Tools!.Clear(); bench.Tools.Add("wood_hammer", 1);
        Work(world, 30, () => bench.Inventory["nettle_rope"] > 0);
        Require(bench.Inventory["nettle_rope"] == 1, $"a worker should make rope once a mallet is slotted: {maker.Status}");
    }

    private static void CheckRopeIsNotMadeByHand(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "nettle", 1); Give(world, "wood_hammer");
        Require(!world.RecipesAt(null).Contains("nettle_rope") && !world.CanCraft("nettle_rope"), "nettle rope should be made only at the bench");
    }

    private static void CheckNettlesAndFirebugGoodsAreNotTraded(GameData data) {
        foreach (string item in new[] { "nettle", "nettle_rope", "bug_net", "firebug" })
            Require(!data.Market.Goods.ContainsKey(item), $"the village should neither buy nor sell {item}");
    }
}
