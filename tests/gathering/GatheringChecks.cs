using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;
using static GatheringSteps;

/// <summary>Checks of gathering: the player's and a worker's gathering of trees, ore, leaves, berries and loose pieces, digging up stumps and bushes, and where a gatherer's load goes.</summary>
public static class GatheringChecks {
    public static void Run(GameData data) {
        Test(CheckWorkerGathersFourPiecesACycleUntilTwelveAreInHand, data);
        Test(CheckGathererGoesOnToTheNextSpotUntilTheirHandsAreFull, data);
        Test(CheckCarriedGoodsGoToTheNearestOfAStationAndFilteredStorage, data);
        Test(CheckFuelGoesStraightIntoAFuelSlotThatBurnsIt, data);
        Test(CheckWorkerGathersWoodAndRetargetsWhenFilterChanges, data);
        Test(CheckToolsGateMiningAndFelling, data);
        Test(CheckSteelPickaxeYieldsFullStone, data);
        Test(CheckPickaxeHitsSometimesChipRock, data);
        Test(CheckLoosePiecesNeedNoToolOrGatherTime, data);
        Test(CheckValleyHasLooseStonesAndSticksToPickUp, data);
        Test(CheckRakeClearsLeavesWhichFallAgain, data);
        Test(CheckBerryBushesStartAlikeAndRegrowWhateverIsPicked, data);
        Test(CheckTinNeedsAPickaxe, data);
        Test(CheckWorkerStateFollowsGatheringAndReassignment, data);
        Test(CheckWorkerGoesToNextKnownTreeThenWaitsUntilOneIsSeen, data);
        Test(CheckPickerGoesToNextKnownLooseCoalThenWaits, data);
        Test(CheckMinerWorksDepositsBeforePickingUpChips, data);
        Test(CheckGathererWithoutAToolPicksUpLoosePieces, data);
        Test(CheckFelledTreeLeavesAStumpThatNeedsAShovel, data);
        Test(CheckFelledTreeFallsAwayFromWhoeverLandedTheLastBlow, data);
        Test(CheckShovelDigsUpAStumpForThreeWood, data);
        Test(CheckShovelDigsUpABushForThreeLeafPatches, data);
        Test(CheckDiggingWaitsForRoomInTheBackpack, data);
        Test(CheckBuildingOverStumpsAndBushesClearsThem, data);
        Test(CheckNewValleyGrowsBushesOnOpenGround, data);
        Test(CheckWoodcutterDigsUpStumpsWhileAShovelHangs, data);
        Test(CheckWoodcutterWithoutAShovelGoesOnChopping, data);
        Test(CheckBushDiggerGoesToTheNextBushNotToLeaves, data);
        Test(CheckLeafRakerLeavesBushesAlone, data);
        Test(CheckBottlesRefillFromWaterAndCanBeDrunkDirectly, data);
        Test(CheckGuestHoldingOnToATreeChopsIntoTheirOwnBackpack, data);
    }

    /// <summary>A steel axe makes a cycle 2 s / 1.8, and a cycle yields 4 pieces as it does for the player.</summary>
    private static void CheckWorkerGathersFourPiecesACycleUntilTwelveAreInHand(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var chest = world.Add(ThingKind.Chest, new Vector2(18, 10));
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_axe";
        Work(world, 2);
        Require(cutter.Bag["wood"] == 4, $"a woodcutter with a steel axe should hold 4 wood after one cycle of 1.1 s, not {cutter.Bag["wood"]}");
        Work(world, 1.75f);
        Require(cutter.Bag["wood"] == 12, $"a woodcutter should hold 12 wood after three cycles, not {cutter.Bag["wood"]}");
        Work(world, 60, () => chest.Inventory["wood"] > 0);
        Require(chest.Inventory["wood"] == 12 && tree.Remaining == 3988, $"a woodcutter should carry 12 wood at a time: {chest.Inventory["wood"]} stored, {4000 - tree.Remaining} cut");
    }

    private static void CheckGathererGoesOnToTheNextSpotUntilTheirHandsAreFull(GameData data) {
        var world = EmptyWorld(data);
        var first = world.Add(ThingKind.LooseCoal, new Vector2(10, 10), 1);
        world.Add(ThingKind.LooseCoal, new Vector2(12, 10), 1);
        world.Add(ThingKind.LooseCoal, new Vector2(14, 10), 1);
        var chest = world.Add(ThingKind.Chest, new Vector2(18, 10));
        world.Player = new Vector2(12, 12); world.Look();
        Staff(world, first, JobKind.Gather);
        Work(world, 60, () => chest.Inventory["coal"] > 0);
        Require(chest.Inventory["coal"] == 3, $"a picker should bring all three known pieces of coal in one load, not {chest.Inventory["coal"]}");
    }

    private static void CheckCarriedGoodsGoToTheNearestOfAStationAndFilteredStorage(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var near = Bench(world, new Vector2(12, 10), "planks");
        var asking = world.Add(ThingKind.Chest, new Vector2(20, 10));
        asking.Inventory.InputFilter.Add("wood");
        var open = world.Add(ThingKind.Chest, new Vector2(11, 12));
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_axe";
        Work(world, 60, () => near.Inventory["wood"] > 0);
        Require(near.Inventory["wood"] == 12 && open.Inventory["wood"] == 0, "wood should go to the table that saws it before a nearer chest with no filter");
        near.Pos = new Vector2(40, 10);
        Work(world, 60, () => asking.Inventory["wood"] > 0);
        Require(asking.Inventory["wood"] == 12 && near.Inventory["wood"] == 12, "wood should go to a chest asking for it when that is nearer than the table");
    }

    private static void CheckFuelGoesStraightIntoAFuelSlotThatBurnsIt(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(13, 10));
        world.SetRecipe(furnace, "bread");
        world.Add(ThingKind.Chest, new Vector2(11, 12));
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_axe";
        Work(world, 60, () => furnace.Burner!["wood"] > 0);
        Require(furnace.Burner!["wood"] == 12, $"wood should go into the fuel slot of a furnace that burns it: the slot holds {furnace.Burner["wood"]}");
    }

    private static void CheckWorkerGathersWoodAndRetargetsWhenFilterChanges(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 5);
        var general = world.Add(ThingKind.Chest, new Vector2(8.5f, 10));
        var filtered = world.Add(ThingKind.Chest, new Vector2(13, 10));
        filtered.Inventory.InputFilter.Add("wood");
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        // Two gather cycles at the player's pace: 4 wood, then the last one.
        SafeTick(world, data.GatherSeconds * 2 / data.Items[worker.Tool].Speed + .3f);
        Require(worker.Bag["wood"] == 5 && tree.Remaining == 0,
                "gathering worker should collect actual wood from its assigned resource");
        SafeTick(world, .1f);
        Require(worker.Destination == filtered.Id,
                "worker should route gathered wood to the explicitly accepting chest");

        // The worker already chose this destination and is walking with a load. A filter edit must
        // be applied before the eventual deposit, with the carrying bag kept intact during reroute.
        filtered.Inventory.InputFilter.Clear();
        filtered.Inventory.InputFilter.Add("stone");
        SafeTick(world, .1f);
        Require(worker.Destination == general.Id && worker.Bag["wood"] == 5,
                "worker should retarget in transit when the destination filter changes");
        for (int i = 0; i < 32 && general.Inventory["wood"] == 0; i++) SafeTick(world, Step);
        Require(general.Inventory["wood"] == 5, "retargeted load should reach the newly accepting chest");
        Require(filtered.Inventory["wood"] == 0, "retargeted load should not leak into the old chest");
        Require(worker.Bag["wood"] == 0, "retargeted load should leave the worker's bag empty");
    }

    private static void CheckToolsGateMiningAndFelling(GameData data) {
        var world = EmptyWorld(data);
        var iron = world.Add(ThingKind.Iron, new Vector2(10, 10), 20);
        Achieve(world, "steel_pick");
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true,
                "the player must still carry a usable pickaxe to mine after previously forging one");
        Require(world.GatherBlock(iron, true) == null,
                "a worker's tools come from a tool station, so what the player carries does not block them");
        Give(world, "steel_pick", 1);
        Require(world.GatherBlock(iron) == null,
                "carrying a steel pickaxe should unlock player mining");
        Drop(world, "steel_pick", 1);
        Give(world, "steel_axe", 1);
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true && world.GatherBlock(world.Add(ThingKind.Tree, new Vector2(12, 12), 5)) == null,
                "an axe should fell trees but not mine ore");
        var boulder = world.Add(ThingKind.Stone, new Vector2(60, 20), 20);
        var at = world.Player;
        world.Player = boulder.Pos + new Vector2(1, 0);
        world.Gather(boulder, data.GatherSeconds * 3);
        Require(world.GatherBlock(boulder)?.Contains("pickaxe") == true && world.Backpack["stone"] == 0 && boulder.Remaining == 20,
                $"a boulder should give no stone by hand and ask for a pickaxe: {world.GatherBlock(boulder)}");
        world.Player = at;
    }

    private static void CheckSteelPickaxeYieldsFullStone(GameData data) {
        var world = EmptyWorld(data);
        var stone = world.Add(ThingKind.Stone, world.Player, 20);
        Give(world, "steel_pick", 1);

        world.Gather(stone, data.GatherSeconds / data.Items["steel_pick"].Speed);

        Require(world.Backpack["stone"] == 4,
                "a steel pickaxe should yield the full stone amount at its faster pace");
    }

    /// <summary>About one pickaxe hit in twenty on ore knocks a loose piece of it off nearby.</summary>
    private static void CheckPickaxeHitsSometimesChipRock(GameData data) {
        var world = EmptyWorld(data);
        var deposits = new[] { world.Add(ThingKind.Iron, new Vector2(20, 20), World.DepositOre), world.Add(ThingKind.Iron, new Vector2(40, 20), World.DepositOre) };
        Give(world, "steel_pick", 1);
        int hits = 0;
        foreach (var deposit in deposits) {
            world.Player = deposit.Pos + new Vector2(1, 0);
            while (deposit.Remaining > 0) { world.Gather(deposit, data.GatherSeconds); hits++; Drop(world, "iron_ore", world.Backpack["iron_ore"]); }
        }
        var chips = world.Things.Where(t => t.Kind == ThingKind.IronChip).ToList();
        Require(hits == 300, $"each 600-ore deposit should take 150 hits of 4: {hits}");
        Require(chips.Count is >= 5 and <= 30, $"about one hit in twenty should chip ore off, 15 of 300 on average: {chips.Count}");
        Require(chips.All(c => deposits.Any(d => d.Pos.DistanceTo(c.Pos) <= GatherRules.ChipReach) && world.CanStand(c.Pos)), "a chip should land where it can be reached, within 2 tiles of its deposit");
        PickUp(world, chips[0]);
        Require(world.Backpack["iron_ore"] == 1, "a chip should give one ore");
    }

    private static void CheckLoosePiecesNeedNoToolOrGatherTime(GameData data) {
        var world = new World(data);
        var piece = world.Things.First(t => t.Kind == ThingKind.LooseCoal);
        Drop(world, "steel_axe");
        Require(world.GatherBlock(piece) == null, "loose coal should need no tool");
        PickUp(world, piece);
        Require(world.Backpack["coal"] == 2, "a loose coal piece should give coal");

        world = EmptyWorld(data);
        world.Add(ThingKind.Chest, new Vector2(12, 10));
        var worker = AddWorker(world, new Vector2(10, 10));
        var assigned = world.Add(ThingKind.LooseCoal, new Vector2(10.5f, 10), 2);
        world.Assign(worker, assigned);
        SafeTick(world, Step);
        Require(world.Find(assigned.Id) == null && worker.Bag["coal"] == 2,
                "a worker should pick up a loose piece at once, and it should not linger as an empty pile");
    }

    /// <summary>Loose pieces need no tool and no gather time: the whole piece goes into the backpack, or a worker's bag at once.</summary>
    /// <summary>A new valley has loose stones and sticks on open ground, each picked up whole as one stone or one wood.</summary>
    private static void CheckValleyHasLooseStonesAndSticksToPickUp(GameData data) {
        var world = new World(data);
        var stones = world.Things.Where(t => t.Kind == ThingKind.LooseStone).ToList();
        var sticks = world.Things.Where(t => t.Kind == ThingKind.Stick).ToList();
        Require(stones.Count >= 20 && sticks.Count >= 10, $"the valley should have loose stones and sticks lying about: {stones.Count} stones, {sticks.Count} sticks");
        Require(stones.Concat(sticks).All(t => Terrain.Open(t.Pos)), "loose stones and sticks should lie on open ground");
        int stone = world.Backpack["stone"], wood = world.Backpack["wood"];
        PickUp(world, stones[0]); PickUp(world, sticks[0]);
        Require(world.Backpack["stone"] == stone + 1 && world.Backpack["wood"] == wood + 1, "a loose stone should give one stone and a stick one wood");
        Require(world.Find(stones[0].Id) == null && world.Find(sticks[0].Id) == null, "a picked-up stone or stick should be gone");
    }

    /// <summary>Leaves lie under oaks, come three times as fast with a rake as by hand, and fall again every day.</summary>
    private static void CheckRakeClearsLeavesWhichFallAgain(GameData data) {
        var fresh = new World(data);
        var oaks = fresh.Things.Where(t => t.Kind == ThingKind.Tree && t.Id % 3 == 0).Select(t => t.Pos + World.LeafOffset).ToHashSet();
        var patches = fresh.Things.Where(t => t.Kind == ThingKind.Leaves).ToList();
        Require(patches.Count > oaks.Count * .8f && patches.All(p => oaks.Contains(p.Pos)), "oaks should drop patches of leaves beside their trunks");
        var world = EmptyWorld(data);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 10), World.LeafPatch);
        world.Player = leaves.Pos;
        Require(world.GatherBlock(leaves) == null, "leaves should be gathered by hand without a rake");
        world.Gather(leaves, data.GatherSeconds); world.Gather(leaves, data.GatherSeconds);
        Require(world.Backpack["leaves"] == 0, "leaves by hand should take longer than two rake strokes");
        world.Gather(leaves, data.GatherSeconds);
        Require(world.Backpack["leaves"] == 4, "leaves by hand should come in three times a rake stroke's time");
        Give(world, "rake", 1);
        world.Gather(leaves, data.GatherSeconds);
        Require(world.Backpack["leaves"] == 8, "a rake should gather leaves in one stroke's time");
        GatherUntil(world, leaves, "leaves", World.LeafPatch);
        Require(leaves.Remaining == 0 && world.GatherBlock(leaves) != null, "a raked patch should be empty until it refills");
        SafeTick(world, data.DaySeconds - world.Time % data.DaySeconds + 1);
        Require(leaves.Remaining == World.LeafPatch, "leaves should fall again by the next day");

        var station = world.Add(ThingKind.Toolstation, new Vector2(14, 10));
        station.Inventory.Add("rake", 1);
        world.Add(ThingKind.Chest, new Vector2(12, 12));
        var worker = AddWorker(world, station.Pos);
        world.Assign(worker, leaves);
        world.Player = new Vector2(30, 30);
        SafeTick(world, 20);
        Require(worker.Tool == "rake" && leaves.Remaining < World.LeafPatch, "a worker should fetch the rake and rake leaves");
    }

    private static void CheckBerryBushesStartAlikeAndRegrowWhateverIsPicked(GameData data) {
        var world = new World(data);
        var bushes = world.Things.Where(t => t.Kind == ThingKind.Berries).ToList();
        Require(bushes.All(b => b.Remaining == 24), "every bush should start with 24 berries, as many as it regrows");
        var bush = bushes[0];
        world.Player = bush.Pos;
        world.Gather(bush, data.GatherSeconds);
        Require(bush.Remaining == 20, "one gather should pick 4 berries");
        RunUntil(world, world.Time + data.BerryRegrowSeconds - 2);
        Require(bush.Remaining == 20, "a bush should not fruit again before its hours have passed");
        RunUntil(world, world.Time + 4);
        Require(bush.Remaining == 24, "a bush picked only in part should be full again once its hours have passed");
    }

    /// <summary>Tin is mined with a pickaxe, from a cliff with two deposits and no loose pieces.</summary>
    private static void CheckTinNeedsAPickaxe(GameData data) {
        var world = EmptyWorld(data);
        var fresh = new World(data);
        var cliff = Cliff.Of(ThingKind.Tin);
        Require(fresh.Things.Count(t => t.Kind == ThingKind.Tin && cliff.DistanceTo(t.Pos) < 3) == 2,
                "the tin cliff should have two deposits and no loose pieces");
        var tin = world.Add(ThingKind.Tin, Site(0, 0), 400);
        world.Player = tin.Pos;
        Require(world.GatherBlock(tin)?.Contains("pickaxe") == true, "tin should need a pickaxe");
        Give(world, "steel_pick", 1);
        GatherUntil(world, tin, "tin_ore", 4);
        Require(world.Backpack["tin_ore"] == 4, "a pickaxe should mine tin ore");
    }

    private static void CheckWorkerStateFollowsGatheringAndReassignment(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 12);
        world.Add(ThingKind.Chest, new Vector2(12, 10));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Gathering,
                "a worker at its resource should enter gathering state through World.Tick");

        world.Assign(worker, null);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "reassigning a working worker to idle should clear its transient work state");
        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "an available worker should remain idle after its next simulation tick");
    }

    private static void CheckWorkerGoesToNextKnownTreeThenWaitsUntilOneIsSeen(GameData data) {
        var (world, said, post) = KnownSpots(data);
        var at = Terrain.Spawn;
        var spent = world.Things.Single(t => t.Kind == ThingKind.Tree && t.Pos == at + new Vector2(1, 0));
        var seen = world.Things.Single(t => t.Kind == ThingKind.Tree && t.Pos == at + new Vector2(6, 0));
        var unseen = world.Things.Single(t => t.Kind == ThingKind.Tree && t.Pos == at + new Vector2(3, 0));
        var worker = AddWorker(world, spent.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, spent);
        for (int i = 0; i < 600 && worker.Assignment != seen.Id; i++) world.Tick(.1f);
        Require(worker.Assignment == seen.Id, "a worker whose tree is felled should move to the nearest tree a player has seen");
        Require(worker.Jobs.Count == 1, "the worker should keep one duty");
        Require(((Duty)worker.Jobs[0]).Thing == seen.Id, "the worker's duty should be the seen tree");
        Require(unseen.Remaining == 5, "the worker should not go to a closer tree under black fog");
        Require(world.Remembered(seen.Id) != null && !world.Sees(seen.Pos), "the next tree should be one known from memory, now out of sight");

        for (int i = 0; i < 600 && worker.Status.IndexOf("waiting at the trading post", StringComparison.Ordinal) < 0; i++) world.Tick(.1f);
        for (int i = 0; i < 100; i++) world.Tick(.1f);
        Require(seen.Remaining == 0, "the tree the worker knows should be gone");
        Require(worker.Assignment == seen.Id, "the worker should keep the assignment");
        Require(worker.Pos.DistanceTo(post.Pos + Vector2.One) < 1.5f,
            "a worker who knows no other tree should wait at the trading post");
        Require(said.Count(m => m.Contains("knows no other tree", StringComparison.Ordinal)) == 1, "a worker who knows no other tree should say so once");

        // The look comes at the end of a tick, so the worker acts on it the tick after.
        world.Player = at; world.Tick(.1f); world.Tick(.1f);
        Require(worker.Assignment == unseen.Id, "once a player sees another tree, the waiting worker should go to it");
    }

    private static void CheckPickerGoesToNextKnownLooseCoalThenWaits(GameData data) {
        var (world, _, post) = KnownSpots(data);
        var at = Terrain.Spawn;
        var coal = world.Things.Single(t => t.Kind == ThingKind.LooseCoal && t.Pos == at + new Vector2(0, 3));
        var moreCoal = world.Things.Single(t => t.Kind == ThingKind.LooseCoal && t.Pos == at + new Vector2(4, 4));
        var picker = AddWorker(world, coal.Pos);
        world.Assign(picker, coal);
        for (int i = 0; i < 20 && world.Find(coal.Id) != null; i++) world.Tick(.1f);
        Require(world.Find(coal.Id) == null && picker.Assignment == moreCoal.Id, "a worker who picks up the last of a loose coal should go to the next known one, not a closer one under black fog");
        for (int i = 0; i < 300 && world.Find(moreCoal.Id) != null; i++) world.Tick(.1f);
        for (int i = 0; i < 100; i++) world.Tick(.1f);
        Require(world.Find(moreCoal.Id) == null, "a worker who picked up the last known loose coal should have it gone");
        Require(picker.Jobs.Count == 1, "the worker should keep one duty");
        Require(((Duty)picker.Jobs[0]).Kind == ThingKind.LooseCoal, "the worker should keep the loose coal duty");
        Require(picker.Pos.DistanceTo(post.Pos + Vector2.One) < 1.5f, "the worker should wait at the trading post");
    }

    /// <summary>A miner moves on to the next known deposit of the same ore, and picks up its loose chips only once no deposit is left.</summary>
    private static void CheckMinerWorksDepositsBeforePickingUpChips(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        var chest = world.Add(ThingKind.Chest, at + new Vector2(2, 2));
        world.Add(ThingKind.Toolstation, at + new Vector2(-2, 2)).Inventory.Add("steel_pick", 1);
        var near = world.Add(ThingKind.Iron, at + new Vector2(1, 0), 4);
        var chip = world.Add(ThingKind.IronChip, at + new Vector2(2, -1.5f), 1);
        var far = world.Add(ThingKind.Iron, at + new Vector2(6, 0), 4);
        world.Player = at; world.Look();
        var miner = AddWorker(world, near.Pos);
        world.Assign(miner, near);
        bool chipFirst = false;
        for (int i = 0; i < 1200 && world.Find(chip.Id) != null; i++) { world.Tick(.1f); chipFirst |= world.Find(chip.Id) == null && far.Remaining > 0; }
        Require(!chipFirst, "a miner should work the next known deposit before picking up chips");
        Require(far.Remaining == 0 && world.Find(chip.Id) == null, "a miner out of deposits should pick up the known chips");
        for (int i = 0; i < 300 && miner.Bag.Count > 0; i++) world.Tick(.1f);
        Require(chest.Inventory["iron_ore"] >= 9, $"the ore and the chip should end up in storage: {chest.Inventory["iron_ore"]}");
    }

    /// <summary>A miner with no pickaxe to be had picks up known loose ore instead of waiting at the deposit.</summary>
    private static void CheckGathererWithoutAToolPicksUpLoosePieces(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        var chest = world.Add(ThingKind.Chest, at + new Vector2(2, 2));
        var deposit = world.Add(ThingKind.Iron, at + new Vector2(1, 0), 40);
        var chip = world.Add(ThingKind.IronChip, at + new Vector2(-2, -1.5f), 1);
        world.Player = at; world.Look();
        var miner = AddWorker(world, deposit.Pos);
        world.Assign(miner, deposit);
        for (int i = 0; i < 300 && chest.Inventory["iron_ore"] == 0; i++) world.Tick(.1f);
        Require(world.Find(chip.Id) == null && chest.Inventory["iron_ore"] == 1 && deposit.Remaining == 40, "a miner without a pickaxe should pick up the loose ore and leave the deposit");
    }

    private static void CheckFelledTreeLeavesAStumpThatNeedsAShovel(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4);
        Give(world, "steel_axe");
        HoldGather(world, tree, 10);
        var stump = world.Things.SingleOrDefault(t => t.Kind == ThingKind.Stump);
        Require(world.Find(tree.Id) == null && stump != null && stump.Pos == tree.Pos, "a felled tree should leave a stump where it stood");
        Require(stump.OfTree == tree.Id, "a stump should keep on record which tree left it, since that tree's size shapes it");
        Require(world.Backpack["wood"] == 4, $"felling the tree should give its 4 wood: {world.Backpack["wood"]}");
        Require(world.GatherBlock(stump)?.Contains("shovel") == true, $"a stump should ask for a shovel: {world.GatherBlock(stump)}");
        HoldGather(world, stump, 10);
        Require(world.Find(stump.Id) != null && stump.Progress == 0 && world.Backpack["wood"] == 4, "a stump should stay in the ground untouched without a shovel");
    }

    private static void CheckFelledTreeFallsAwayFromWhoeverLandedTheLastBlow(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4);
        Give(world, "steel_axe");
        HoldGather(world, tree, 10);
        var felled = world.Things.Single(t => t.Kind == ThingKind.Stump);
        // The player chops from the tree's east side, so it falls west.
        Require(Vector2.FromAngle(felled.FellToward).DistanceTo(Vector2.Left) < .01f, $"a tree the player fells from its east side should fall west: {Vector2.FromAngle(felled.FellToward)}");
        var lot = Woodlot(data);
        Vector2 cutter = Vector2.Zero;
        Work(lot.World, 300, () => { cutter = lot.Cutter.Pos; return lot.World.Things.Any(t => t.Kind == ThingKind.Stump); });
        var stump = lot.World.Things.Single(t => t.Kind == ThingKind.Stump);
        Require(Vector2.FromAngle(stump.FellToward).Dot((stump.Pos - cutter).Normalized()) > .999f, $"a tree a woodcutter fells should fall away from them: {Vector2.FromAngle(stump.FellToward)} from {cutter}");
    }

    private static void CheckShovelDigsUpAStumpForThreeWood(GameData data) {
        var world = EmptyWorld(data);
        var stump = world.Add(ThingKind.Stump, new Vector2(10, 10), World.StumpWood);
        Give(world, "steel_shovel");
        var heard = Listen(world);
        HoldGather(world, stump, 1);
        Require(world.Find(stump.Id) != null && stump.Progress > 0 && world.Backpack["wood"] == 0, "a stump should take more than a second of digging and give nothing until it is out");
        HoldGather(world, stump, 20);
        Require(world.Find(stump.Id) == null, "a shovel should dig a stump out of the ground for good");
        Require(world.Backpack["wood"] == 3, $"a dug up stump should give 3 wood: {world.Backpack["wood"]}");
        Require(heard.Contains("dig_up"), $"a stump coming out of the ground should be heard: {string.Join(",", heard)}");
    }

    private static void CheckShovelDigsUpABushForThreeLeafPatches(GameData data) {
        var world = EmptyWorld(data);
        var bush = world.Add(ThingKind.Bush, new Vector2(10, 10), World.BushLeaves);
        Require(world.GatherBlock(bush)?.Contains("shovel") == true, $"a bush should ask for a shovel: {world.GatherBlock(bush)}");
        HoldGather(world, bush, 10);
        Require(world.Find(bush.Id) != null && world.Backpack["leaves"] == 0, "a bush should stay in the ground without a shovel");
        Give(world, "steel_shovel");
        HoldGather(world, bush, 20);
        Require(world.Find(bush.Id) == null, "a shovel should dig a bush out of the ground for good");
        // An oak's patch holds 12 leaves.
        Require(world.Backpack["leaves"] == 36, $"a dug up bush should give 36 leaves, three leaf patches' worth: {world.Backpack["leaves"]}");
    }

    private static void CheckDiggingWaitsForRoomInTheBackpack(GameData data) {
        var world = EmptyWorld(data);
        var bush = world.Add(ThingKind.Bush, new Vector2(10, 10), World.BushLeaves);
        SetBackpackSlots(world, 1);
        Give(world, "steel_shovel");
        Require(world.GatherBlock(bush)?.Contains("room") == true, $"a full backpack should say it has no room for the leaves: {world.GatherBlock(bush)}");
        HoldGather(world, bush, 20);
        Require(world.Find(bush.Id) != null && bush.Remaining == 36 && bush.Progress == 0, "a bush should stay whole while the backpack has no room for all of it");
        SetBackpackSlots(world, 2);
        HoldGather(world, bush, 20);
        Require(world.Find(bush.Id) == null && world.Backpack["leaves"] == 36, "the bush should come out once there is room");
    }

    private static void CheckBuildingOverStumpsAndBushesClearsThem(GameData data) {
        var world = EmptyWorld(data);
        var at = new Vector2(20, 20);
        var stump = world.Add(ThingKind.Stump, at + new Vector2(.3f, 0), World.StumpWood);
        var bush = world.Add(ThingKind.Bush, at + new Vector2(-.4f, .2f), World.BushLeaves);
        var apart = world.Add(ThingKind.Bush, at + new Vector2(4, 0), World.BushLeaves);
        Give(world, data.Buildings[ThingKind.Chest].Cost);
        BuildAt(world, ThingKind.Chest, at);
        Require(world.Find(stump.Id) == null && world.Find(bush.Id) == null, "a building should go up over a stump and a bush and clear them away");
        Require(world.Find(apart.Id) != null, "a bush away from the building should stay");
        Require(world.Backpack["wood"] == 0 && world.Backpack["leaves"] == 0, "what a building clears away should give nothing");
    }

    private static void CheckNewValleyGrowsBushesOnOpenGround(GameData data) {
        var world = new World(data);
        var bushes = world.Things.Where(t => t.Kind == ThingKind.Bush).ToList();
        Require(bushes.Count > 200, $"a new valley should have a couple of hundred bushes: {bushes.Count}");
        Require(bushes.All(b => b.Remaining == 36), "every bush should hold 36 leaves");
        Require(bushes.All(b => world.CanStand(b.Pos) && b.Pos.DistanceTo(Terrain.Spawn) >= 6), "bushes should grow on open ground away from the clearing");
        var crowded = bushes.FirstOrDefault(b => world.Things.Any(t => t != b && t.Pos.DistanceTo(b.Pos) < .9f));
        Require(crowded == null, $"a bush should not grow into another thing: {crowded?.Pos}");
    }

    private static void CheckWoodcutterDigsUpStumpsWhileAShovelHangs(GameData data) {
        var lot = Woodlot(data);
        lot.Rack.Inventory.Add("steel_shovel", 1);
        Work(lot.World, 300, () => lot.Cutter.Assignment == lot.Next.Id);
        Require(lot.Cutter.Assignment == lot.Next.Id, $"the woodcutter should get to the next tree: {lot.Cutter.Status}");
        Require(lot.World.Things.All(t => t.Kind != ThingKind.Stump), "a woodcutter with a shovel to take should dig up the stump before the next tree");
        // The felled tree held 5 wood and its stump 3 more; the woodcutter tops their hands up to 12 at the next tree before storing.
        Work(lot.World, 60, () => lot.Chest.Inventory["wood"] > 0);
        Require(lot.Chest.Inventory["wood"] == 12 && lot.Next.Remaining == 396, $"the tree's 5 wood, its stump's 3 and 4 from the next tree should reach storage in one load: {lot.Chest.Inventory["wood"]}");
    }

    private static void CheckWoodcutterWithoutAShovelGoesOnChopping(GameData data) {
        var lot = Woodlot(data);
        Work(lot.World, 300, () => lot.Next.Remaining < 400);
        Require(lot.Next.Remaining < 400, $"a woodcutter with no shovel to take should chop the next tree: {lot.Cutter.Status}");
        Require(lot.World.Things.Count(t => t.Kind == ThingKind.Stump) == 1, "the stump should stay in the ground without a shovel");
        Work(lot.World, 60, () => lot.Chest.Inventory["wood"] > 0);
        Require(lot.Chest.Inventory["wood"] == 12, $"only chopped wood should reach storage, 12 at a time: {lot.Chest.Inventory["wood"]}");
    }

    private static void CheckBushDiggerGoesToTheNextBushNotToLeaves(GameData data) {
        var (world, bush, leaves, chest, rack) = Thicket(data);
        var next = world.Add(ThingKind.Bush, bush.Pos + new Vector2(6, 0), World.BushLeaves);
        world.Look();
        rack.Inventory.Add("steel_shovel", 1);
        Hire(world, bush);
        Work(world, 300, () => chest.Inventory["leaves"] == 72);
        Require(world.Find(bush.Id) == null && world.Find(next.Id) == null, "a worker digging bushes should go on to the next bush");
        Require(chest.Inventory["leaves"] == 72, $"two bushes should bring 72 leaves to storage: {chest.Inventory["leaves"]}");
        Require(leaves.Remaining == 12, $"a worker digging bushes should leave the nearer leaf patch alone: {leaves.Remaining}");
    }

    private static void CheckLeafRakerLeavesBushesAlone(GameData data) {
        var (world, bush, leaves, chest, rack) = Thicket(data);
        rack.Inventory.Add("steel_shovel", 1);
        Hire(world, leaves);
        Work(world, 300, () => chest.Inventory["leaves"] == 12);
        Work(world, 60);
        Require(chest.Inventory["leaves"] == 12, $"a raker should bring the patch's 12 leaves to storage: {chest.Inventory["leaves"]}");
        Require(world.Find(bush.Id) != null && bush.Progress == 0, "a worker raking leaves should leave the bush beside the patch alone");
    }

    private static void CheckBottlesRefillFromWaterAndCanBeDrunkDirectly(GameData data) {
        var world = new World(data, false);
        var lake = new Thing { Kind = ThingKind.Water };
        SetBackpackSlots(world, 1);
        Give(world, "empty_bottle", 1);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.RefilledBottle, "gathering with an empty bottle should refill it");
        Require(world.Backpack["water"] == 1, "refilling should give one full bottle");
        Require(world.Backpack.Count == 1, "refilling should not create bottles");
        Require(world.Backpack.UsedSlots == 1, "refilling should reuse its slot");
        Require(Water.CollectWater(world, lake) == WaterCollectResult.NoChange, "a full bottle should not refill into an extra bottle");

        var priority = new World(data, false);
        Give(priority, "water_1", 1);
        Give(priority, "empty_bottle", 1);
        Water.CollectWater(priority, lake);
        Require(priority.Backpack["water"] == 1, "an empty bottle should refill first");
        Require(priority.Backpack["water_1"] == 1, "the partial bottle should stay partial");
        Require(priority.Backpack["empty_bottle"] == 0, "no empty bottle should remain after the refill");

        Drop(world, "water", 1);
        SetNeeds(world, thirst: 25);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceLiter, "gathering without a bottle should drink directly from the source");
        Require(world.Thirst > 58.32f, "a direct source liter should restore at least one third thirst");
        Require(world.Thirst < 58.34f, "a direct source liter should restore no more than one third thirst");
        Require(world.Backpack.Count == 0, "a direct source liter should not create a bottle");
    }

    /// <summary>A guest holding on to a tree chops its wood into their own backpack and leaves the host's alone.</summary>
    private static void CheckGuestHoldingOnToATreeChopsIntoTheirOwnBackpack(GameData data) {
        var (world, guest, _) = Hosted(data);
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(4, 0), 40);
        world.As(guest, () => world.MoveTo(tree.Pos + new Vector2(.8f, 0)));
        guest.GatherTarget = tree.Id;
        FedPlayersTick(world, data.GatherSeconds * 2 + .5f);
        Require(guest.Backpack["wood"] >= 4, "a guest holding on to a tree should chop wood into their own backpack");
        Require(world.Backpack["wood"] == 0, "a guest holding on to a tree should put nothing in the host's backpack");
        Require(tree.Remaining < 40, "a guest holding on to a tree should cut the tree down");
    }
}
