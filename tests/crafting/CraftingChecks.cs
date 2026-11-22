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

/// <summary>Checks of crafting: recipes at stations and by hand, the mark, the queue and batches, the tools a recipe needs, and the workers who craft or pump a station.</summary>
public static class CraftingChecks {
    public static void Run(GameData data) {
        Test(CheckMarkFollowsTheRecipeAskedFor, data);
        Test(CheckStationWaitsWhileAnotherRecipesGoodsSitInItsSlots, data);
        Test(CheckStationTurnsToAnotherRecipeOnlyWhenFree, data);
        Test(CheckMarkMovesOnlyOnAnIdleEmptyStation, data);
        Test(CheckTurningToAnotherRecipeTakesTheFuelOut, data);
        Test(CheckIdleStationStaysOnTheRecipeAskedLast, data);
        Test(CheckDoubleClickLoadsAWholeBatchOnTopOfWhatIsThere, data);
        Test(CheckQueuingSeveralBatchesLoadsEachOfThem, data);
        Test(CheckFurnacesBurnOneFuelAndWaitForSteelHeat, data);
        Test(CheckPlayerWorksAStationOnlyWithinAMetreOfItsEdge, data);
        Test(CheckAnvilNeedsHammering, data);
        Test(CheckAnvilHammersOnlyWithASteelHammer, data);
        Test(CheckAnvilHammersABarIntoTwoSheets, data);
        Test(CheckFinishedBatchWaitsForOutputRoom, data);
        Test(CheckStationBatchesAndMarkSurviveALoad, data);
        Test(CheckStationLoadsMidBatchAndFinishesIt, data);
        Test(CheckHandCraftingNeedsChiselAndTakesTime, data);
        Test(CheckBenchWorksWithACarriedOrSlottedToolWhicheverIsFaster, data);
        Test(CheckRecipeMapHangsItemsUnderWhatTheyAreMadeOutOf, data);
        Test(CheckStoneFurnaceSmeltsTinOreIntoBars, data);
        Test(CheckTinCansNeedASteelHammer, data);
        Test(CheckSteelChiselCarvesAMortarAndWorksTwiceAsFast, data);
        Test(CheckCookingTableCrushesBerriesAndCooksJam, data);
        Test(CheckCanneryAndFurnaceTakeTheirFoodRecipes, data);
        Test(CheckCookingTableGrindsWithACarriedMortar, data);
        Test(CheckCrushingBerriesByHandNeedsACarriedMortar, data);
        Test(CheckAWorkerBesideAStationWalksRoundItToWorkIt, data);
        Test(CheckAWorkerWhoseWorkSpotIsBlockedWorksFromAsNearAsTheyGet, data);
        Test(CheckOnlyFirstFurnaceWorkerPumpsBellows, data);
        Test(CheckPackingStationTakesOnlyItsGoodAndPacksCrates, data);
        Test(CheckBenchKeepsMakingToolsWithNowhereElseToPutThem, data);
    }

    /// <summary>
    /// Asking a marked station for another recipe turns its mark to that recipe too, so it goes on making it once the batches asked for are done.
    /// A batch leaves the count as it starts, and with no mark the station stops.
    /// </summary>
    private static void CheckMarkFollowsTheRecipeAskedFor(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        Require(furnace.Keep == "iron_bar" && furnace.Recipe == "iron_bar", "a stone furnace should start out keeping to iron");
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "raw_meat", 4); Give(world, "coal", 4);
        world.QueueBatch(furnace, "burnt_steak");
        Require(furnace.Keep == "burnt_steak" && world.Waiting(furnace, "burnt_steak") == 1 && furnace.Inventory["raw_meat"] == 2 && furnace.Burner!["coal"] == 1,
            "asking a marked station for another recipe should move its mark there and load a batch with one fuel");
        world.Tick(.1f);
        Require(furnace.Processing && world.Waiting(furnace, "burnt_steak") == 0, "a batch should leave the count as it starts");
        world.Unqueue(furnace, "burnt_steak", 1);
        Require(furnace.Processing && furnace.Recipe == "burnt_steak", "taking batches off should leave the one in progress");
        world.LoadInput(furnace, "raw_meat", 2); world.LoadFuel(furnace, "coal", 1);
        SafeTick(world, data.Recipes["burnt_steak"].Seconds * 2 + 1);
        Require(furnace.Inventory["burnt_steak"] == 4, "with the batch asked for done, the furnace should go on searing under its mark");
        world.SetRecipe(furnace, "");
        Give(world, "raw_meat", 2); world.LoadInput(furnace, "raw_meat", 2); world.LoadFuel(furnace, "coal", 1);
        SafeTick(world, data.Recipes["burnt_steak"].Seconds + 1);
        Require(furnace.Recipe == "" && furnace.Inventory["burnt_steak"] == 4 && furnace.Inventory["raw_meat"] == 2, "with nothing asked for or marked the furnace should stop");
    }

    /// <summary>A station starts no batch while its slots hold something another recipe left, as a hauler's late delivery can; taking it out lets the station go on.</summary>
    private static void CheckStationWaitsWhileAnotherRecipesGoodsSitInItsSlots(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "iron_ore", 2); Give(world, "coal", 1);
        world.LoadInput(furnace, "iron_ore", 2); world.LoadFuel(furnace, "coal", 1);
        furnace.Inventory.Add("burnt_steak", 1);
        SafeTick(world, data.Recipes["iron_bar"].Seconds + 1);
        Require(!furnace.Processing && furnace.Inventory["iron_bar"] == 0 && furnace.Inventory["iron_ore"] == 2, "a furnace on iron should start nothing while a steak sits in its slots");
        world.Collect(furnace);
        SafeTick(world, data.Recipes["iron_bar"].Seconds + 1);
        Require(world.Backpack["burnt_steak"] == 1 && furnace.Inventory["iron_bar"] == 1, "with the steak taken out the furnace should smelt its iron");
    }

    /// <summary>
    /// A station counts batches of one recipe: asking for another is refused while a batch is worked.
    /// Once it is done, asking drops the batches still waiting and empties the station's boxes into the backpack, unless the backpack has no room.
    /// </summary>
    private static void CheckStationTurnsToAnotherRecipeOnlyWhenFree(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.SetRecipe(furnace, "");
        world.Player = furnace.Pos + Vector2.One;
        // Ore for one of the two batches, so the second waits.
        Give(world, "tin_ore", 2); Give(world, "coal", 2); Give(world, "wood", 2);
        world.QueueBatch(furnace, "tin_bar", 2);
        world.Tick(.1f);
        world.QueueBatch(furnace, "charcoal");
        Require(world.SwitchBlock(furnace, "charcoal") != null && furnace.Recipe == "tin_bar" && world.Waiting(furnace, "tin_bar") == 1 && world.Backpack["wood"] == 2,
            "asking for another recipe while a batch is worked should change nothing");
        world.QueueBatch(furnace, "tin_bar");
        Require(world.Waiting(furnace, "tin_bar") == 2, "more batches of the recipe being worked should still count up");
        SafeTick(world, data.Recipes["tin_bar"].Seconds + 1);
        Require(!furnace.Processing && furnace.Inventory["tin_bar"] == 1 && world.Waiting(furnace, "tin_bar") == 2, "the furnace should stand with its bar and two batches waiting for ore");
        world.QueueBatch(furnace, "charcoal");
        Require(furnace.Recipe == "charcoal" && world.Waiting(furnace, "charcoal") == 1 && world.Waiting(furnace, "tin_bar") == 0 && furnace.Keep == "charcoal",
            "asking for another recipe between batches should turn the station and its mark to it and drop the batches that waited");
        Require(world.Backpack["tin_bar"] == 1 && furnace.Inventory["tin_bar"] == 0 && furnace.Inventory["wood"] == 2 && furnace.Burner!["coal"] == 1,
            "turning to another recipe should take the boxes' contents into the backpack and load the new batch with a fuel");
        // Stone in every free slot leaves no room for the furnace's wood and coal.
        Give(world, "stone", world.Backpack.Slots * world.Backpack.StackOf("stone"));
        world.QueueBatch(furnace, "iron_bar");
        Require(world.SwitchBlock(furnace, "iron_bar") != null && furnace.Recipe == "charcoal" && world.Waiting(furnace, "charcoal") == 1 && furnace.Inventory["wood"] == 2,
            "with no room in the backpack for what its boxes hold the station should stay on its recipe");
        // One log and one coal in place of two stacks of stone: the furnace's wood and coal top those stacks up, with no slot free.
        Drop(world, "stone", world.Backpack.StackOf("stone") * 2); Give(world, "wood", 1); Give(world, "coal", 1);
        world.QueueBatch(furnace, "iron_bar");
        // The furnace's coal joins the one in the backpack, and one of the two goes back in as the iron's fuel.
        Require(furnace.Recipe == "iron_bar" && world.Backpack["wood"] == 3 && world.Backpack["coal"] == 1 && furnace.Burner!["coal"] == 1,
            "wood and coal that top up stacks in a full backpack should let the station turn");
    }

    /// <summary>
    /// A mark moves to another recipe only on a station with empty slots and no batch in the works.
    /// Taking it off and marking the recipe the station is on always work, and a station with no mark stays on its recipe.
    /// </summary>
    private static void CheckMarkMovesOnlyOnAnIdleEmptyStation(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "iron_ore", 2); world.LoadInput(furnace, "iron_ore", 2);
        world.SetRecipe(furnace, "tin_bar");
        Require(world.MarkBlock(furnace, "tin_bar") != null && furnace.Keep == "iron_bar", "a mark should not move to another recipe while the slots hold something");
        world.SetRecipe(furnace, "");
        Require(furnace.Keep == "" && furnace.Recipe == "" && world.Selected(furnace) == "iron_bar", "taking the mark off should leave the station on its recipe, with nothing to make");
        world.SetRecipe(furnace, "iron_bar");
        Require(furnace.Keep == "iron_bar", "marking the recipe the station is on should work with its slots full");
        // Three batches wait for ore: the backpack has none to load.
        world.QueueBatch(furnace, "iron_bar", 3);
        world.Transfer(furnace.Inventory, world.Backpack, "iron_ore", 2);
        world.SetRecipe(furnace, "tin_bar");
        Require(furnace.Keep == "tin_bar" && furnace.Recipe == "tin_bar" && world.Waiting(furnace, "iron_bar") == 0,
            "with empty slots and no batch the mark should move, turn the station and drop the batches that waited");
        Give(world, "tin_ore", 2); Give(world, "coal", 1); world.LoadInput(furnace, "tin_ore", 2); world.LoadFuel(furnace, "coal", 1);
        world.Tick(.1f);
        world.SetRecipe(furnace, "iron_bar");
        Require(furnace.Processing && world.MarkBlock(furnace, "iron_bar") != null && furnace.Keep == "tin_bar", "a mark should not move while a batch is worked");
        var bench = world.Add(ThingKind.Bench, new Vector2(24, 20));
        Require(bench.Keep == "" && bench.Recipe == "" && world.Selected(bench) == "torch", "a bench nobody marked should be on the first recipe of its board, the torch");
    }

    /// <summary>A turn to another recipe takes the fuel out of the fuel slot too, into the backpack; what a batch burned is gone.</summary>
    private static void CheckTurningToAnotherRecipeTakesTheFuelOut(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.SetRecipe(furnace, "");
        world.Player = furnace.Pos + Vector2.One;
        // Of 5 logs, 2 go in as the batch, 1 as its fuel and 2 more as fuel by hand.
        Give(world, "wood", 5);
        world.QueueBatch(furnace, "charcoal"); world.LoadFuel(furnace, "wood", 2);
        SafeTick(world, data.Recipes["charcoal"].Seconds + 1);
        Require(furnace.Inventory["charcoal"] == 1 && furnace.Burner!["wood"] == 2, "the batch should burn one log and leave two in the fuel slot");
        world.QueueBatch(furnace, "iron_bar");
        Require(furnace.Recipe == "iron_bar" && furnace.Burner!["wood"] == 0 && world.Backpack["wood"] == 2, "turning to iron should take the two logs out of the fuel slot into the backpack");
    }

    /// <summary>A station with no mark that has run out of batches stays on the recipe asked for last: more of it counts up and leaves its slots alone, even with a full backpack.</summary>
    private static void CheckIdleStationStaysOnTheRecipeAskedLast(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.SetRecipe(furnace, "");
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "tin_ore", 2); Give(world, "coal", 1);
        // Asking marks the recipe; the mark comes off again, so only the batch asked for is made.
        world.QueueBatch(furnace, "tin_bar"); world.SetRecipe(furnace, "");
        SafeTick(world, data.Recipes["tin_bar"].Seconds + 1);
        Require(furnace.Recipe == "" && furnace.Inventory["tin_bar"] == 1, "the furnace should stand idle with its bar once the batch asked for is done");
        Give(world, "stone", world.Backpack.Slots * world.Backpack.StackOf("stone"));
        world.QueueBatch(furnace, "tin_bar");
        Require(world.Waiting(furnace, "tin_bar") == 1 && furnace.Keep == "tin_bar" && furnace.Inventory["tin_bar"] == 1 && world.Backpack["tin_bar"] == 0,
            "asking again for the recipe made last should count up, mark it and leave the bar in its slot, with no room asked of the backpack");
        world.QueueBatch(furnace, "charcoal");
        Require(world.SwitchBlock(furnace, "charcoal") != null && world.Waiting(furnace, "tin_bar") == 1, "another recipe should still be refused while the backpack has no room for the bar");
    }

    /// <summary>A double-click loads a whole batch and one fuel from the backpack, whatever the station already holds.</summary>
    private static void CheckDoubleClickLoadsAWholeBatchOnTopOfWhatIsThere(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "tin_ore", 3); Give(world, "coal", 2);
        world.SetRecipe(furnace, "tin_bar");
        world.LoadInput(furnace, "tin_ore", 1); world.LoadFuel(furnace, "coal", 1);
        world.QueueBatch(furnace, "tin_bar");
        Require(furnace.Inventory["tin_ore"] == 3 && furnace.Burner!["coal"] == 2 && world.Backpack["tin_ore"] == 0 && world.Backpack["coal"] == 0,
            "a double-click should load a whole batch of inputs and one fuel on top of what the station holds");
    }

    /// <summary>Asking for several batches at once loads inputs and a fuel for each, and only for the batches the count still has room for.</summary>
    private static void CheckQueuingSeveralBatchesLoadsEachOfThem(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "tin_ore", 12); Give(world, "coal", 7);
        world.QueueBatch(furnace, "tin_bar", 5);
        Require(world.Waiting(furnace, "tin_bar") == 5 && furnace.Inventory["tin_ore"] == 10 && furnace.Burner!["coal"] == 5
            && world.Backpack["tin_ore"] == 2 && world.Backpack["coal"] == 2,
            "asking for 5 batches should load 5 batches of inputs and 5 fuel");
        // 92 more take the last ore and coal from the backpack: 12 ore and 7 coal are in the furnace.
        world.QueueBatch(furnace, "tin_bar", 92);
        Give(world, "tin_ore", 8); Give(world, "coal", 3);
        world.QueueBatch(furnace, "tin_bar", 5);
        Require(world.Waiting(furnace, "tin_bar") == 99 && furnace.Inventory["tin_ore"] == 16 && furnace.Burner!["coal"] == 9,
            "asking for 5 batches with room for 2 should load only 2 batches of inputs and 2 fuel");
    }

    private static void CheckFurnacesBurnOneFuelAndWaitForSteelHeat(GameData data) {
        var world = EmptyWorld(data);
        var basic = world.Add(ThingKind.Furnace, new Vector2(10, 10));
        Require(basic.Burner != null, "basic furnace should have its own fuel slot");
        world.SetRecipe(basic, "iron_bar");
        Require(world.FuelBlock(basic, "wood") != null && world.FuelBlock(basic, "coal") == null && world.FuelBlock(basic, "charcoal") == null,
            "smelting iron should burn coal or charcoal and refuse wood");
        world.SetRecipe(basic, "burnt_steak");
        Require(world.FuelBlock(basic, "wood") == null && !world.BatchCost(basic).ContainsKey("wood"),
            "searing meat should burn wood, which does not count as an ingredient");
        basic.Burner!.Add("coal", 4);
        Require(world.FuelOf(basic) == "coal" && world.FuelBlock(basic, "wood") != null,
                "basic furnace should burn the coal in its fuel slot and take one kind of fuel at a time");

        var reinforced = world.Add(ThingKind.Reinforced, new Vector2(15, 10));
        reinforced.Burner!.Add("charcoal", 4); // Even malformed or edited state must not make charcoal a steel fuel.
        Require(world.FuelOf(reinforced) == null && world.FuelBlock(reinforced, "wood") != null && world.FuelBlock(reinforced, "charcoal") != null,
                "the reinforced furnace should only ever burn coal, even with other fuel in its slot");
        reinforced.Burner.Remove("charcoal", 4);
        var steelCost = world.BatchCost(reinforced);
        Require(steelCost["coal"] == 2, "a steel batch should need two coal");
        Require(steelCost["iron_bar"] == 2, "a steel batch should need two iron bars");
        Require(steelCost.Count == 2, "a steel batch should need nothing else");
        reinforced.Inventory.Add("iron_bar", 2);
        reinforced.Inventory.Add("coal", 2);
        world.Player = reinforced.Pos;
        reinforced.Heat = 100;
        SafeTick(world, Step);
        Require(!reinforced.Processing && reinforced.Inventory["iron_bar"] == 2,
                "a steel batch must wait for fuel in the fuel slot, even with coal among its ingredients");
        reinforced.Heat = 0;
        reinforced.Burner!.Add("coal", 1);
        SafeTick(world, 12);
        Require(!reinforced.Processing, "the reinforced furnace must not start a steel batch while below the steel heat");
        Require(reinforced.Progress == 0, "the reinforced furnace must make no progress while below the steel heat");
        Require(reinforced.Inventory["iron_bar"] == 2, "the reinforced furnace must keep the batch's iron bars while below the steel heat");
        Require(reinforced.Inventory["coal"] == 2, "the reinforced furnace must keep the batch's coal while below the steel heat");
        Require(reinforced.Burner["coal"] == 1, "the reinforced furnace must keep its fuel coal while below the steel heat");
        reinforced.Heat = 100;
        SafeTick(world, Step);
        Require(reinforced.Processing, "once hot enough, the reinforced furnace should start the batch");
        Require(reinforced.Inventory["iron_bar"] == 0, "once hot enough, the reinforced furnace should consume the batch's iron bars");
        Require(reinforced.Inventory["coal"] == 0, "once hot enough, the reinforced furnace should consume the batch's coal");
        Require(reinforced.Burner["coal"] == 0, "once hot enough, the reinforced furnace should consume one coal of fuel");
        reinforced.Burner.Add("coal", 3);
        SafeTick(world, 30);
        Require(reinforced.Processing, "a cooled forge should keep its batch running");
        Require(reinforced.Progress < data.Recipes["steel_bar"].Seconds,
            "a cooled forge's steel batch should stop progressing");
        Require(reinforced.Burner["coal"] == 1, "a cooled forge should keep burning coal from its fuel slot");
    }

    /// <summary>The anvil's edge is 0.45 m from its middle and the reinforced furnace's 0.8 m.</summary>
    private static void CheckPlayerWorksAStationOnlyWithinAMetreOfItsEdge(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, new Vector2(10, 10));
        anvil.Inventory.Add("steel_bar", 3); anvil.Tools!.Add("steel_hammer", 1);
        world.Player = anvil.Pos + new Vector2(1.6f, 0);
        for (int i = 0; i < 4; i++) world.Tick(.25f);
        Require(!anvil.Processing, "a player standing 1.15 m from the anvil's edge must not hammer it");
        world.Player = anvil.Pos + new Vector2(1.35f, 0);
        for (int i = 0; i < 4; i++) world.Tick(.25f);
        Require(anvil.Processing && anvil.Progress > 0, "a player standing 0.9 m from the anvil's edge should hammer it");

        var forge = world.Add(ThingKind.Reinforced, new Vector2(20, 10));
        world.Player = forge.Pos + new Vector2(1.95f, 0);
        world.Pump(forge);
        Require(forge.Heat == 0, "a player 1.15 m from the furnace's edge must not pump its bellows");
        world.Player = forge.Pos + new Vector2(1.7f, 0);
        world.Pump(forge);
        Require(forge.Heat > 0, "a player 0.9 m from the furnace's edge should pump its bellows");
    }

    private static void CheckAnvilNeedsHammering(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, new Vector2(10, 10));
        anvil.Inventory.Add("steel_bar", 3); anvil.Tools!.Add("steel_hammer", 1);
        world.Player = anvil.Pos + new Vector2(1.2f, 0);
        for (int i = 0; i < 8; i++) { world.MovePlayer(new Vector2(i % 2 == 0 ? .1f : -.1f, 0)); world.Tick(.25f); }
        Require(!anvil.Processing && anvil.Inventory["steel_bar"] == 3, "an anvil must not start while the player only walks past it");
        world.Tick(.25f);
        Require(anvil.Processing && anvil.Progress > 0, "a player standing at the anvil should hammer the batch");
        float progress = anvil.Progress;
        world.Player = anvil.Pos + new Vector2(1.2f, 0);
        for (int i = 0; i < 4; i++) { world.Gather(anvil, .25f); world.Tick(.25f); }
        Require(anvil.Progress == progress, "a busy player must not advance the anvil");

        // A paid smith stays to hammer a running batch; one on strike beside the anvil does not count.
        var chest = world.Add(ThingKind.Chest, new Vector2(24, 10));
        chest.Inventory.Add("steel_bar", 6);
        var worker = AddWorker(world, anvil.Pos + Vector2.One);
        world.Assign(worker, anvil);
        world.Player = new Vector2(30, 30);
        worker.WageDebt = 8;
        SafeTick(world, 1);
        Require(anvil.Progress == progress, "a striking worker next to the anvil must not advance it");
        worker.WageDebt = 0;
        SafeTick(world, 1);
        Require(worker.WorkState == WorkerWorkState.Hammering && anvil.Progress > progress, "a lone smith should hammer the running batch before fetching the next load");
        // The smith hammers what the anvil holds, brings the bars left in the chest, hammers those, and only then has parts to carry out.
        Work(world, 120, () => chest.Inventory["steel_parts"] >= 1);
        Require(chest.Inventory["steel_parts"] >= 1, "a worker hammering at the anvil should finish the batches and store the parts");
    }

    /// <summary>
    /// The anvil's recipes need a steel hammer, slotted or carried by the player working it, and it hammers at the
    /// steel hammer's 1.8 times; its slot refuses a wooden mallet, and a smith with no hammer slotted says what is missing.
    /// </summary>
    private static void CheckAnvilHammersOnlyWithASteelHammer(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, new Vector2(10, 10));
        anvil.Inventory.Add("steel_bar", 2);
        world.Player = anvil.Pos + new Vector2(1.2f, 0);
        SafeTick(world, 2);
        Require(!anvil.Processing, "an anvil should not start without a hammer");
        Give(world, "wood_hammer", 1);
        Require(!world.SlotTool(anvil, "wood_hammer") && world.Backpack["wood_hammer"] == 1, "the anvil's hammer slot should refuse a wooden mallet");
        SafeTick(world, 2);
        Require(!anvil.Processing, "an anvil should not start with only a wooden mallet carried");
        Give(world, "steel_hammer", 1);
        SafeTick(world, 1);
        Require(anvil.Processing && MathF.Abs(anvil.Progress - 1.8f) < .1f, $"a player carrying a steel hammer should hammer at 1.8 times: progress={anvil.Progress:0.00} after a second");

        var worker = AddWorker(world, anvil.Pos + Vector2.One);
        world.Assign(worker, anvil);
        world.Player = new Vector2(30, 30);
        float progress = anvil.Progress;
        SafeTick(world, 1);
        Require(anvil.Progress == progress && worker.Status == "Needs a steel hammer in the anvil", $"a smith with no hammer slotted should wait and say so: status={worker.Status}");
        world.Player = anvil.Pos + new Vector2(1.2f, 0);
        Require(world.SlotTool(anvil, "steel_hammer"), "a steel hammer should fit the anvil's hammer slot");
        world.Player = new Vector2(30, 30);
        Work(world, 10, () => worker.WorkState == WorkerWorkState.Hammering && anvil.Progress > progress);
        Require(worker.WorkState == WorkerWorkState.Hammering && anvil.Progress > progress,
            $"a smith should hammer with the slotted steel hammer: status={worker.Status}, state={worker.WorkState}, progress={anvil.Progress:0.00}, processing={anvil.Processing}");
    }

    /// <summary>The anvil hammers one steel bar into two steel sheets and one tin bar into two tin sheets.</summary>
    private static void CheckAnvilHammersABarIntoTwoSheets(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, new Vector2(10, 10));
        anvil.Tools!.Add("steel_hammer", 1);
        world.Player = anvil.Pos + new Vector2(1.2f, 0);
        Give(world, "steel_bar", 1); Give(world, "tin_bar", 1);
        world.QueueBatch(anvil, "steel_sheet");
        CompleteBatches(world, anvil, "steel_sheet", 2);
        world.Collect(anvil);
        world.QueueBatch(anvil, "tin_sheet");
        CompleteBatches(world, anvil, "tin_sheet", 2);
        world.Collect(anvil);
        Require(world.Backpack["steel_sheet"] == 2 && world.Backpack["tin_sheet"] == 2 && world.Backpack["steel_bar"] == 0 && world.Backpack["tin_bar"] == 0,
            "the anvil should hammer each bar into two sheets of its metal");
    }

    private static void CheckFinishedBatchWaitsForOutputRoom(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(10, 10));
        furnace.Inventory.Slots = 2;
        world.Player = furnace.Pos;
        Give(world, "iron_ore", 2);
        Give(world, "coal", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f);
        Require(furnace.Processing && furnace.Inventory.Count == 0,
                "furnace should consume a valid batch before cooking begins");

        furnace.Inventory.Add("stone", 128);
        float cookSeconds = data.Recipes["iron_bar"].Seconds;
        world.Tick(cookSeconds + 1);
        Require(furnace.Processing, "a completed batch must stay pending when the station has no output capacity");
        Require(Math.Abs(furnace.Progress - cookSeconds) < .01f, "a pending batch must remain at full progress");
        Require(furnace.Inventory["iron_bar"] == 0, "a pending batch must not add output without capacity");

        furnace.Inventory.Remove("stone", 64);
        world.Tick(.25f);
        Require(!furnace.Processing, "pending output should finish the batch as soon as one slot becomes available");
        Require(furnace.Progress == 0, "pending output should reset the progress");
        Require(furnace.Inventory["iron_bar"] == 1, "pending output should be added exactly once");

        furnace.Inventory.Slots = 18; furnace.Inventory.Remove("stone", 64);
        furnace.Inventory.Add("iron_bar", furnace.Inventory.StackSize - 1);
        Give(world, "iron_ore", 2); Give(world, "coal", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f); world.Tick(cookSeconds + 1);
        Require(furnace.Processing && furnace.Inventory["iron_bar"] == furnace.Inventory.StackSize,
                "a full one-stack output slot must hold the next batch at full progress");
        Require(world.LoadInput(furnace, "iron_ore", 999) == 0 && world.LoadInput(furnace, "stone", 1) == 0,
                "manual loading must reject empty backpacks and items the recipe does not use");
        Give(world, "iron_ore", 100);
        Require(world.LoadInput(furnace, "iron_ore", 999) == furnace.Inventory.StackSize && world.LoadInput(furnace, "iron_ore", 1) == 0,
                "an input slot must hold exactly one stack");
    }

    private static void CheckStationBatchesAndMarkSurviveALoad(GameData data) {
        var old = EmptyWorld(data);
        var anvil = old.Add(ThingKind.Anvil, new Vector2(10, 10));
        old.SetRecipe(anvil, "");
        var furnace = old.Add(ThingKind.Furnace, new Vector2(14, 10));
        old.Player = furnace.Pos + Vector2.One;
        old.QueueBatch(furnace, "charcoal", 3);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(old.Serialize());
        Require(loaded.Find(anvil.Id)!.Keep == "" && loaded.Find(anvil.Id)!.Recipe == "", "an anvil the player unmarked should load unmarked");
        var station = loaded.Find(furnace.Id)!;
        Require(loaded.Waiting(station, "charcoal") == 3 && station.Recipe == "charcoal" && station.Keep == "charcoal",
            "a station should load with the batches asked of it, on their recipe, and the mark that moved there with them");
    }

    /// <summary>A station saved part way through a batch loads still making it, with the batches asked of it waiting behind.</summary>
    private static void CheckStationLoadsMidBatchAndFinishesIt(GameData data) {
        var old = EmptyWorld(data);
        var furnace = old.Add(ThingKind.Furnace, new Vector2(14, 10));
        old.Player = furnace.Pos + Vector2.One;
        // Two batches take 4 wood, and 2 more burn under them.
        Give(old, "wood", 6);
        old.QueueBatch(furnace, "charcoal", 2);
        old.Tick(.1f);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(old.Serialize());
        var station = loaded.Find(furnace.Id)!;
        Require(station.Processing && station.Recipe == "charcoal" && loaded.Waiting(station, "charcoal") == 1, "a station saved mid-batch should load making that batch with the other one waiting");
        SafeTick(loaded, data.Recipes["charcoal"].Seconds * 2 + 1);
        Require(station.Inventory["charcoal"] == 2 && !station.Processing, "the loaded batch should finish and the waiting one follow, with no wood left for a third");
    }

    private static void CheckHandCraftingNeedsChiselAndTakesTime(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 3);
        Require(world.CraftBlock("planks")?.Contains("chisel") == true && !world.Craft("planks"), "wood crafting by hand needs a chisel");
        Require(world.CraftBlock("crate") != null && world.CraftBlock("steel_pick") != null, "crates and metal tools are bench work");
        Give(world, "stone", 2);
        Require(world.Craft("stone_chisel") && world.Backpack["stone"] == 0, "a stone chisel should need only 2 stone and take them at once");
        SafeTick(world, data.Recipes["stone_chisel"].Seconds - .5f);
        Require(world.Backpack["stone_chisel"] == 0, "hand crafting should take its time");
        SafeTick(world, 1);
        Require(world.Backpack["stone_chisel"] == 1 && world.BestTool("chisel") == "stone_chisel", "the chisel should arrive once its time is up");
        Require(world.Backpack["wood"] == 3, "the check starts with three wood");
        Require(world.Craft("planks"), "planks should queue with a chisel");
        Require(world.Craft("wood_parts", 5) && world.Backpack["wood"] == 0, "queueing five wood parts should queue as many as the wood allows");
        Require(world.Crafting.Count == 2 && World.Waiting(world.Crafting, "wood_parts") == 2, "the queue should hold one entry per recipe with its count");
        for (int i = 0; i < 60 * data.Recipes["planks"].Seconds; i++) { world.MovePlayer(new Vector2(i % 2 == 0 ? .05f : -.05f, 0)); SafeTick(world, Step); }
        Require(world.Backpack["planks"] == 0 && world.CraftTime == 0, "crafting by hand should pause while the player walks");
        SafeTick(world, data.Recipes["planks"].Seconds + World.CraftSettle + .1f);
        Require(world.Backpack["planks"] == 4 && world.Making == "wood_parts" && World.Waiting(world.Crafting, "wood_parts") == 1,
            "standing still should finish the planks and start the next recipe, one at a time");
        world.CancelCraft("wood_parts", 5);
        Require(world.Crafting.Count == 0 && world.Backpack["wood"] == 1, "taking batches off should return the waiting one's wood");
        Require(world.Making == "wood_parts", "the batch being made cannot be taken off");
        SafeTick(world, data.Recipes["wood_parts"].Seconds);
        Require(world.Backpack["wood_parts"] == 2 && world.Making == "", "the started batch should finish");
        world.Craft("wood_parts");
        Drop(world, "stone_chisel", 1);
        SafeTick(world, 20);
        Require(world.Backpack["wood_parts"] == 2 && world.CraftRate == 0, "a queued recipe should wait while its tool is not carried");
        Give(world, "stone_chisel", 1);
        SafeTick(world, data.Recipes["wood_parts"].Seconds);
        Require(world.Backpack["wood_parts"] == 4, "it should continue once the tool is back");
    }

    private static void CheckBenchWorksWithACarriedOrSlottedToolWhicheverIsFaster(GameData data) {
        var world = EmptyWorld(data);
        var bench = world.Add(ThingKind.Bench, new Vector2(10, 10));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        Give(world, "wood", 4); Give(world, "wood_hammer", 1);
        world.SetRecipe(bench, "planks");
        world.LoadBatch(bench, 2);
        SafeTick(world, 6);
        Require(!bench.Processing && bench.Inventory["planks"] == 0, "a bench recipe should wait while its tool is neither slotted nor carried");
        Give(world, "stone_chisel", 1);
        SafeTick(world, Step);
        SafeTick(world, data.Recipes["planks"].Seconds / data.BenchSpeed);
        Require(bench.Inventory["planks"] == 4 && world.Backpack["stone_chisel"] == 1, "the player should work a bench with a carried chisel, twice as fast as crafting by hand");
        Require(!world.SlotTool(bench, "wood_hammer") || world.BenchTool(bench, "chisel") == null, "a hammer must not fill the chisel slot");
        Require(world.SlotTool(bench, "stone_chisel"), "the chisel should go into its slot");
        Require(world.Backpack["stone_chisel"] == 0, "the chisel should leave the backpack");
        Require(!world.SlotTool(bench, "stone_chisel"), "a second chisel should not fit the one slot");
        SafeTick(world, data.Recipes["planks"].Seconds / data.BenchSpeed + Step);
        Require(bench.Inventory["planks"] == 8, "a bench should work with its slotted chisel");
        var worker = AddWorker(world, bench.Pos + Vector2.One);
        var chest = world.Add(ThingKind.Chest, new Vector2(14, 10));
        chest.Inventory.Add("wood", 2);
        world.UnslotTool(bench, "stone_chisel");
        world.Assign(worker, bench);
        world.Player = new Vector2(30, 30);
        SafeTick(world, 12);
        Require(worker.Status.Contains("chisel") && world.Backpack["stone_chisel"] == 1 && bench.Inventory["planks"] + chest.Inventory["planks"] == 8, "a bench worker should wait for a slotted tool and say which one the bench is missing");
        bench.Tools!.Add("stone_chisel", 1);
        SafeTick(world, 40);
        Require(chest.Inventory["planks"] >= 16 && chest.Inventory["wood"] == 0, "a bench worker should fetch wood, work the bench and store the planks");

        world = EmptyWorld(data);
        bench = world.Add(ThingKind.Bench, new Vector2(10, 10));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        Give(world, "planks", 12); Give(world, "wood_hammer", 1); Give(world, "steel_hammer", 1);
        Require(world.SlotTool(bench, "wood_hammer"), "the mallet should go into the hammer slot");
        world.SetRecipe(bench, "crate");
        world.LoadBatch(bench, 2);
        Require(world.ToolInUse(bench, "hammer", 0) == "steel_hammer", "the player should work with the carried steel hammer over the slotted mallet");
        // A crate takes 10 s by hand: 5 s at the bench with the mallet, 2.8 s with the steel hammer.
        SafeTick(world, 3.5f);
        Require(bench.Inventory["crate"] == 1, "a bench should work at the speed of a carried steel hammer when only a mallet is slotted");
    }

    private static void CheckRecipeMapHangsItemsUnderWhatTheyAreMadeOutOf(GameData data) {
        Require(data.MadeFrom("wood") == null && data.MadeFrom("coal") == null, "gathered resources should start a chain");
        Require(data.MadeFrom("rake") == "steel_parts" && data.MadeFrom("meat_stew") == "raw_meat" && data.MadeFrom("steel_bar") == "iron_bar", "a product should hang under its first input");
        Require(data.MadeFrom("water") == "empty_bottle" && data.MadeFrom("wheat") == "wheat_seeds" && data.MadeFrom("fertilizer") == "leaves", "what no recipe makes should hang under what it comes out of");
        Require(data.MadeOutOf("steel_parts").Order().SequenceEqual(new[] { "rake", "steel_axe", "steel_chisel", "steel_hammer", "steel_pick", "steel_shovel" }), "steel parts should lead to the six steel tools");
        Require(data.MadeOutOf("wood").SequenceEqual(new[] { "planks", "wood_parts", "torch", "charcoal" }), "the longest chains out of an item should come first");
        Require(data.RecipeOf("crate")?.Group == RecipeGroup.Containers && data.RecipeOf("stone_chisel")?.Group == RecipeGroup.Tools && data.RecipeOf("planks")?.Group == RecipeGroup.Materials && data.RecipeOf("bread")?.Group == RecipeGroup.Food, "recipes should be grouped by what they make");
    }

    /// <summary>Tin ore is smelted like iron: the stone furnace turns 2 tin ore into a tin bar.</summary>
    private static void CheckStoneFurnaceSmeltsTinOreIntoBars(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "tin_ore", 4);
        var furnace = world.Add(ThingKind.Furnace, Site(3, 0));
        world.Player = furnace.Pos + new Vector2(1.2f, 0);
        Give(world, "coal", 2);
        world.SetRecipe(furnace, "tin_bar");
        world.LoadBatch(furnace, 2);
        SafeTick(world, data.Recipes["tin_bar"].Seconds * 2 + 1);
        world.Collect(furnace);
        Require(world.Backpack["tin_bar"] == 2 && world.Backpack["tin_ore"] == 0, "the stone furnace should smelt 2 tin ore into a tin bar");
    }

    private static void CheckTinCansNeedASteelHammer(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "tin_sheet", 2);
        var bench = BenchLeftWithAMallet(world, "tin_can");
        Require(!bench.Processing, "tin cans should wait while only a wooden mallet is slotted");
        Require(bench.Inventory["tin_can"] == 0, "tin cans should not be made with a wooden mallet");
        Require(world.BenchTool(bench, "hammer", 2) == null,
            "a wooden mallet should not count as a hammer for tin cans");
        SwapMalletForSteelHammer(world, bench);
        SafeTick(world, data.Recipes["tin_can"].Seconds / data.BenchSpeed / data.Items["steel_hammer"].Speed + 1);
        Require(bench.Inventory["tin_can"] == 4, "a steel hammer should turn one tin sheet into four cans");
    }

    private static void CheckSteelChiselCarvesAMortarAndWorksTwiceAsFast(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "stone", 3); Give(world, "stone_chisel", 1);
        Require(world.CraftBlock("mortar") == "Needs a steel chisel in your backpack.", "a stone chisel should not carve a mortar");
        Give(world, "steel_chisel", 1);
        Require(world.BestTool("chisel") == "steel_chisel" && world.Craft("mortar"), "a steel chisel should carve a mortar from stone by hand");
        SafeTick(world, data.Recipes["mortar"].Seconds / 2 + .25f);
        Require(world.Backpack["mortar"] == 1, "a steel chisel should work twice as fast");
        Give(world, "wood", 1);
        Require(world.Craft("planks"), "a steel chisel should do everything a stone chisel does");
        SafeTick(world, data.Recipes["planks"].Seconds / 2 + .25f);
        Require(world.Backpack["planks"] == 4, "planks should come twice as fast with a steel chisel");
    }

    /// <summary>
    /// The cooking table's mortar recipes only advance while someone works the slotted mortar, its fire recipes cook unattended on their fuel, and dough needs no fire.
    /// The canning station seals food into tin cans over its fire.
    /// </summary>
    private static void CheckCookingTableCrushesBerriesAndCooksJam(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(20, 20));
        world.Player = table.Pos + new Vector2(1, 0);
        Give(world, "mortar", 1); Give(world, "stone_chisel", 1); Give(world, "berries", 8);
        Require(!world.SlotTool(table, "stone_chisel"), "the cooking table should refuse a chisel");
        Require(world.SlotTool(table, "mortar"), "the cooking table should slot a mortar");
        Require(world.BenchTool(table, "mortar") == "mortar",
            "the cooking table's slotted mortar should count as its tool");
        world.SetRecipe(table, "crushed_berries");
        Require(!world.BatchCost(table).ContainsKey("wood"), "crushing berries should burn no fuel");
        world.LoadBatch(table);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 30);
        Require(table.Inventory["crushed_berries"] == 0, "the mortar should not work without someone at it");
        var cook = AddWorker(world, table.Pos + new Vector2(0, 1));
        world.Assign(cook, table);
        SafeTick(world, 15);
        Require(table.Inventory["crushed_berries"] == 4, "a worker should work the mortar into crushed berries");
        world.Assign(cook, null);

        world.Player = table.Pos + new Vector2(1, 0);
        world.Collect(table);
        world.SetRecipe(table, "berry_jam");
        Give(world, "coal", 1);
        world.LoadBatch(table);
        Require(world.BatchCost(table).SequenceEqual(new Dictionary<string, int> { ["crushed_berries"] = 2 }) && world.FuelOf(table) == "coal", "jam should take crushed berries and burn the coal it is loaded with");
        world.Player = new Vector2(40, 40);
        SafeTick(world, data.Recipes["berry_jam"].Seconds + 1);
        Require(table.Inventory["berry_jam"] == 1 && table.Burner!["coal"] == 0, "jam should cook on its own over the fire");
        world.Player = table.Pos + new Vector2(1, 0);
        world.Collect(table);
        world.SetRecipe(table, "dough");
        Require(world.BatchCost(table).Keys.Order().SequenceEqual(new[] { "flour", Water.Liquid }), "dough should need no fire");
    }

    private static void CheckCanneryAndFurnaceTakeTheirFoodRecipes(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(40, 40);
        var cannery = world.Add(ThingKind.Cannery, new Vector2(24, 20));
        world.SetRecipe(cannery, "canned_meat_stew");
        cannery.Inventory.Add("meat_stew", 1); cannery.Inventory.Add("tin_can", 1); cannery.Burner!.Add("wood", 1);
        SafeTick(world, data.Recipes["canned_meat_stew"].Seconds + 1);
        Require(cannery.Inventory["canned_meat_stew"] == 1, "the canning station should seal stew into a can");
        Require(cannery.Inventory.Count == 1, "the canning station should use up its ingredients");
        Require(cannery.Burner.Count == 0, "the canning station should burn its wood");

        var furnace = world.Add(ThingKind.Furnace, new Vector2(28, 20));
        world.SetRecipe(furnace, "bread");
        Require(world.BatchCost(furnace).Keys.SequenceEqual(new[] { "dough" }) && world.FuelBlock(furnace, "wood") == null, "bread should bake from dough over the furnace's fire");
    }

    /// <summary>By hand, the mortar is carried like any other tool.</summary>
    private static void CheckCookingTableGrindsWithACarriedMortar(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(20, 20));
        world.Player = table.Pos + new Vector2(1, 0);
        Give(world, "mortar", 1); Give(world, "berries", 4);
        world.SetRecipe(table, "crushed_berries");
        world.LoadBatch(table);
        SafeTick(world, data.Recipes["crushed_berries"].Seconds + 1);
        Require(table.Inventory["crushed_berries"] == 4 && world.Backpack["mortar"] == 1, "the player should crush berries at the table with the mortar they carry");
    }

    private static void CheckCrushingBerriesByHandNeedsACarriedMortar(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "mortar", 1); Give(world, "berries", 4);
        Require(world.CraftBlock("crushed_berries") == null, "berries should be crushed by hand with a carried mortar");
        Drop(world, "mortar", 1);
        Require(world.CraftBlock("crushed_berries") != null, "crushing berries by hand should need the mortar");
    }

    /// <summary>
    /// From a player's save: a packer stood on the far side of their packing station, close enough to the packing spot to count as there for the path search,
    /// with the station between them; they stood still with "Path blocked".
    /// </summary>
    private static void CheckAWorkerBesideAStationWalksRoundItToWorkIt(GameData data) {
        var world = EmptyWorld(data);
        var packer = world.Add(ThingKind.Packer, Terrain.Spawn + new Vector2(6, 6));
        world.SetPacking(packer, "berries");
        packer.Inventory.Add("berries", 12); packer.Inventory.Add("crate", 2);
        var worker = Staff(world, packer, JobKind.Craft);
        worker.Pos = packer.Pos + new Vector2(-.49f, .5f);
        Work(world, 10, () => worker.WorkState == WorkerWorkState.Hammering);
        Require(worker.WorkState == WorkerWorkState.Hammering, $"a packer beside the far side of the station should walk round it and pack, not \"{worker.Status}\"");
    }

    /// <summary>A chest stands on the spot a pumper works the bellows from; they pump from as near to it as they get, rather than walking to and fro.</summary>
    private static void CheckAWorkerWhoseWorkSpotIsBlockedWorksFromAsNearAsTheyGet(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, Terrain.Spawn + new Vector2(6, 6));
        forge.Inventory.Add("iron_bar", 20); forge.Inventory.Add("coal", 20); forge.Burner!.Add("coal", 10);
        world.Add(ThingKind.Chest, forge.Pos + World.BellowsStand);
        var pumper = Staff(world, forge, JobKind.Bellows);
        Work(world, 10, () => pumper.WorkState == WorkerWorkState.Pumping);
        Require(pumper.WorkState == WorkerWorkState.Pumping, $"a pumper whose spot a chest stands on should pump from beside it, not \"{pumper.Status}\"");
    }

    private static void CheckOnlyFirstFurnaceWorkerPumpsBellows(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        // Bellows are pumped only while there is something to smelt.
        furnace.Inventory.Add("iron_bar", 20); furnace.Inventory.Add("coal", 20); furnace.Burner!.Add("coal", 10);
        var bellowsWorker = AddWorker(world, furnace.Pos + World.BellowsStand);
        var secondWorker = AddWorker(world, furnace.Pos);
        world.Assign(bellowsWorker, furnace);
        world.Assign(secondWorker, furnace);

        world.Tick(.25f);
        Require(bellowsWorker.WorkState == WorkerWorkState.Pumping,
                "the assigned furnace bellows operator should enter pumping state through World.Tick");

        // Only the bellows operator pumps: extra crew with nothing to fetch never joins.
        var thirdWorker = AddWorker(world, furnace.Pos);
        world.Assign(thirdWorker, furnace);
        for (int i = 0; i < 40; i++) {
            world.Tick(.25f);
            Require(world.Workers.Count(w => w.WorkState == WorkerWorkState.Pumping) <= 1,
                    "at most one worker should pump the bellows at a time");
        }
        Require(bellowsWorker.WorkState == WorkerWorkState.Pumping && bellowsWorker.Assignment == furnace.Id && furnace.Heat > 0,
                "the first assigned worker should keep operating the bellows with extra crew assigned, heating the furnace");
    }

    /// <summary>A packing station packs only while worked, one item at a time, names the crate after its good, and holds a full crate until it is carried out.</summary>
    private static void CheckPackingStationTakesOnlyItsGoodAndPacksCrates(GameData data) {
        var world = EmptyWorld(data);
        var packer = world.Add(ThingKind.Packer, new Vector2(20, 20));
        world.Player = packer.Pos + new Vector2(1, 0);
        world.SetPacking(packer, "bread");
        Require(packer.Packs == "bread", "a packing station should be set to pack bread");
        Require(world.Holds(packer, "bread"), "a packing station should take its own good");
        Require(world.Holds(packer, "crate"), "a packing station should take empty crates");
        Require(!world.Holds(packer, "berries"), "a packing station should refuse another good");
        packer.Inventory.Add("bread", 13); packer.Inventory.Add("crate", 2);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 10);
        Require(packer.Output!.Count == 0 && packer.Inventory["bread"] == 13, "a packing station should not pack while nobody works it");
        world.Player = packer.Pos + new Vector2(1, 0);
        SafeTick(world, data.PackSeconds * data.CrateCapacity + 1);
        string crate = packer.Output.First()!;
        Require(crate.StartsWith(World.CrateOf("bread") + "_"), $"the player working it should name the crate after its good: {crate}");
        Require(world.Packed(crate)!["bread"] == data.CrateCapacity, $"the player working it should pack a crate of twelve bread: {crate}");
        Require(packer.Inventory["bread"] == 1, $"the packing station should use up one bread: {crate}");
        Require(packer.Inventory["crate"] == 1, $"the packing station should use up one empty crate: {crate}");
        SafeTick(world, 10);
        Require(packer.Output.Count == 1 && packer.Inventory["bread"] == 1, "a full crate should wait in the output and stop packing until it is carried out");
    }

    /// <summary>
    /// A settlement by the lake, run only by workers, sells each end product by cart within a few days.
    /// Workers mine, chop, rake, pick and fetch water; they smelt, forge, hammer, grind, bake, cook and can; and they make crates, pack them and stock the post.
    /// </summary>
    /// <summary>With no storage taking pickaxes, the bench keeps its finished pickaxes and its worker goes on making more.</summary>
    private static void CheckBenchKeepsMakingToolsWithNowhereElseToPutThem(GameData data) {
        var world = EmptyWorld(data);
        var bench = world.Add(ThingKind.Bench, new Vector2(20, 20));
        world.SetRecipe(bench, "steel_pick");
        bench.Tools!.Add("steel_hammer", 1);
        // Piles take only the one resource they hold, so nothing takes a pickaxe.
        world.Add(ThingKind.Pile, bench.Pos + new Vector2(4, 0)).Inventory.Add("steel_parts", 20);
        world.Add(ThingKind.Pile, bench.Pos + new Vector2(8, 0)).Inventory.Add("wood_parts", 10);
        var worker = AddWorker(world, bench.Pos + new Vector2(0, 1));
        worker.Wage = 18;
        world.SetDuty(worker, bench, JobKind.Craft, true); world.SetDuty(worker, bench, JobKind.Haul, true);
        Work(world, 180, () => bench.Inventory["steel_pick"] >= 3);
        Require(bench.Inventory["steel_pick"] >= 3, $"the bench should keep making pickaxes it has nowhere else to put: {bench.Inventory["steel_pick"]} made");
    }
}
