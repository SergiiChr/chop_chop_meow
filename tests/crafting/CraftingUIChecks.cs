using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of crafting: the crafting menu and the recipe map, a station's recipe boards, slots and marks, and the player working a station.</summary>
    private async Task RunCraftingUIChecks() {
        await TestClean(CheckHarvestingBesideAStationStopsHammering);
        await TestClean(CheckIdlePlayerBesideACookingTableGrindsWithItsMortar);
        await TestClean(CheckIdlePlayerBesideABenchKnocksWithItsHammer);
        await TestClean(CheckStrikerBesideAStationLeavesThePlayerHammering);
        await TestClean(CheckAnvilWithoutAHammerLeavesThePlayerIdle);
        await TestClean(CheckIdlePlayerAtAnAnvilSwingsItsSlottedSteelHammerOffTheStand);
        await TestClean(CheckSmithSwingsTheAnvilsSlottedHammerOffTheStand);
        await TestClean(CheckEveryHammerBlowLandsOnTheStationsWorkTop);
        await TestClean(CheckPackingStationListsGoodsAndTakesBackpackClicks);
        await TestClean(CheckCraftingMenuMakesChiselAndTorchByHand);
        await TestClean(CheckBenchLoadsInputsByClickDoubleClickAndSplit);
        await TestClean(CheckOutputSlotTakesBackOnlyWhatTheRecipeMakes);
        await TestClean(CheckRecipeBoardsMarkAndQueueWithModifiers);
        await TestClean(CheckStationShakesWhileBusyThenTurnsToAnotherRecipe);
        await TestClean(CheckStationSlotsShakeWhenTheBackpackHasNoRoomForThem);
        await TestClean(CheckUnmarkedStationStaysOnItsRecipe);
        await TestClean(CheckRefusedDoubleClickKeepsTheWorkerMark);
        await RunRecipeReadyChecks();
        await TestClean(CheckRecipeGroupsCloseAndOpenOnAClick);
        await TestClean(CheckBenchShowsItsToolsBesideThePageAndWhereARecipesToolIs);
        await TestClean(CheckRecipeListKeepsItsPlaceWhenThePageDrawsAgain);
        await TestClean(CheckCraftingLeavesTheRecipeMapOnTheBottomBar);
        await TestClean(CheckRecipeMapOpensFromTheBottomBarAndShowsEveryItemOnce);
        await TestClean(CheckAltClickBackpackItemOpensItsRecipeMapEntry);
        await TestClean(CheckAltClickHotbarItemOpensItsRecipeMapEntry);
        await TestClean(CheckAltClickRecipeShowsTheProductWithoutQueueing);
        await TestClean(CheckAltClickIngredientInTheActiveCraftingRow);
        await TestClean(CheckRecipeMapChipsLightTheirItemAndShowLeadsToTheStation);
        await TestClean(CheckGameGoesOnUnderTheOpenRecipeMap);
        await TestClean(CheckNewAnvilMakesSteelPartsAndLoadsFromTheBackpack);
    }

    /// <summary>A player harvesting beside a running station is busy with the harvest, so their figure stops hammering.</summary>
    private async Task CheckHarvestingBesideAStationStopsHammering() {
        var start = world.Player;
        var farm = Hosted.Add(ThingKind.Farm, start + new Vector2(-3, 3)); farm.FarmStage = FarmStage.Ripe;
        Warp(Hosted, farm.Pos + new Vector2(.9f, -.9f)); await Frames();
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(-.5f, -1.2f)); SlotPlainestTools(Hosted, anvil);
        Give(Hosted, "steel_bar", 1); Hosted.QueueBatch(anvil, "steel_parts");
        await Until(() => view.PlayerAnimation == ActorAction.Hammer, "the idle player hammering at the anvil beside it", 3);
        var point = RightPress(farm.Pos);
        await Until(() => farm.Progress > 0, "holding RMB on a ripe farm plot beside the anvil harvesting by hand", 3);
        await Frames();
        Require(anvil.Processing && world.Near(anvil) && view.PlayerAnimation != ActorAction.Hammer, "A player harvesting beside a running anvil must not be shown hammering.");
        RightRelease(point);
        await Frames();
        RemoveThings(Hosted, t => t == farm || t == anvil); Warp(Hosted, start);
    }

    /// <summary>A player standing idle beside a cooking table that is crushing berries grinds with its mortar, which leaves the counter for their hands.</summary>
    private async Task CheckIdlePlayerBesideACookingTableGrindsWithItsMortar() {
        // The server's position, since the window's copy may not have caught up with the last check's warp.
        int soundsBefore = sounds.Played;
        var table = Hosted.Add(ThingKind.Cooktable, Hosted.Player + new Vector2(1.2f, 0));
        table.Tools!.Add("mortar", 1); Give(Hosted, "berries", 20); Hosted.QueueBatch(table, "crushed_berries", 2);
        await Until(() => table.Processing && view.PlayerAnimation == ActorAction.Grind && sounds.Heard("grind_mortar", soundsBefore), "the idle player grinding at the cooking table beside it, with the pestle heard", 4);
        Require(view.PropOf(table.Id) is StoveProp { Mortar.Visible: false }, "The mortar must be off the counter while the player grinds with it.");
        RemoveThings(Hosted, t => t == table);
    }

    /// <summary>
    /// A player with no hammer of their own, standing idle beside a bench that is making a crate, hammers it with the
    /// bench's own knock, swinging its slotted hammer off the bench top.
    /// </summary>
    private async Task CheckIdlePlayerBesideABenchKnocksWithItsHammer() {
        // With no hammer of their own, the player swings the bench's.
        int steel = Drop(Hosted, "steel_hammer"), wood = Drop(Hosted, "wood_hammer");
        // The server's position, since the window's copy may not have caught up with the last check's warp.
        int soundsBefore = sounds.Played;
        var bench = Hosted.Add(ThingKind.Bench, Hosted.Player + new Vector2(1.2f, 0));
        bench.Tools!.Add("wood_hammer", 1); Give(Hosted, "planks", 6); Hosted.QueueBatch(bench, "crate");
        await Until(() => bench.Processing && view.PlayerAnimation == ActorAction.Hammer && sounds.Heard("bench_work", soundsBefore), "the idle player hammering at the bench beside it with its knock", 4);
        Require(view.PropOf(bench.Id) is BenchProp { Hammer.Shown: null }, "The bench's slotted hammer must be off its top while the player swings it.");
        RemoveThings(Hosted, t => t == bench);
        if (steel > 0) Give(Hosted, "steel_hammer", steel);
        if (wood > 0) Give(Hosted, "wood_hammer", wood);
    }

    /// <summary>A smith who walks out mid-batch is no longer working the station, so the player standing there takes over the hammering.</summary>
    private async Task CheckStrikerBesideAStationLeavesThePlayerHammering() {
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(1.4f, 0)); SlotPlainestTools(Hosted, anvil);
        Give(Hosted, "steel_bar", 4); Hosted.QueueBatch(anvil, "steel_parts", 4);
        var smith = AddWorker(Hosted, anvil.Pos + new Vector2(0, 1.2f)); Hosted.Assign(smith, anvil);
        await Until(() => anvil.Processing && smith.WorkState == WorkerWorkState.Hammering, "the smith hammering a batch at the anvil", 3);
        smith.WageDebt = 1;
        await Until(() => view.PlayerAnimation == ActorAction.Hammer, "the player hammering beside a worker on strike", 2);
        smith.WageDebt = 0; Hosted.Fire(smith);
        RemoveThings(Hosted, t => t == anvil);
    }

    /// <summary>An anvil whose hammer is taken off mid-batch waits for one, so the player beside it with none stops hammering.</summary>
    private async Task CheckAnvilWithoutAHammerLeavesThePlayerIdle() {
        int carried = Drop(Hosted, "steel_hammer");
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(1.4f, 0)); SlotPlainestTools(Hosted, anvil);
        Give(Hosted, "steel_bar", 2); Hosted.QueueBatch(anvil, "steel_parts", 2);
        await Until(() => anvil.Processing && view.PlayerAnimation == ActorAction.Hammer, "the idle player hammering a batch at the anvil beside it", 3);
        anvil.Tools!.Remove("steel_hammer", 1);
        await Until(() => view.PlayerAnimation != ActorAction.Hammer, "the player stopping once the anvil has no hammer", 2);
        Require(anvil.Processing, "The anvil must still be on its batch, waiting for a hammer.");
        RemoveThings(Hosted, t => t == anvil);
        if (carried > 0) Give(Hosted, "steel_hammer", carried);
    }

    /// <summary>
    /// A player carrying only a wooden mallet, idle beside an anvil with a steel hammer slotted, swings the steel hammer,
    /// which is off the stand while they do and back on it once the batch is done.
    /// </summary>
    private async Task CheckIdlePlayerAtAnAnvilSwingsItsSlottedSteelHammerOffTheStand() {
        int carried = Drop(Hosted, "steel_hammer");
        Give(Hosted, "wood_hammer", 1);
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(1.4f, 0)); SlotPlainestTools(Hosted, anvil);
        Give(Hosted, "steel_bar", 1); Hosted.QueueBatch(anvil, "steel_parts");
        await Until(() => view.PlayerAnimation == ActorAction.Hammer && view.PlayerRig.ToolShown == "steel_hammer",
            "the idle player swinging the anvil's slotted steel hammer", 3);
        Require(view.PropOf(anvil.Id) is AnvilProp { Hammer.Shown: null }, "The anvil's slotted hammer must be off the stand while the player swings it.");
        await Until(() => !anvil.Processing && view.PropOf(anvil.Id) is AnvilProp { Hammer.Shown: "steel_hammer" }, "the steel hammer back on the stand once the batch is done", 10);
        RemoveThings(Hosted, t => t == anvil); Drop(Hosted, "wood_hammer", 1);
        if (carried > 0) Give(Hosted, "steel_hammer", carried);
    }

    /// <summary>
    /// A smith hammering at an anvil swings its slotted steel hammer, which is off the stand while they do, even with a
    /// wooden mallet of their own in their tool slot.
    /// </summary>
    private async Task CheckSmithSwingsTheAnvilsSlottedHammerOffTheStand() {
        // Out of the player's reach, so only the smith works it.
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(6, 0)); anvil.Inventory.Add("steel_bar", 4); SlotPlainestTools(Hosted, anvil);
        var smith = AddWorker(Hosted, anvil.Pos + new Vector2(0, 1.2f)); smith.Tool = "wood_hammer"; Hosted.Assign(smith, anvil);
        await Until(() => smith.WorkState == WorkerWorkState.Hammering && view.WorkerTool(smith.Id) == "steel_hammer" && view.PropOf(anvil.Id) is AnvilProp { Hammer.Shown: null },
            "the smith hammering with the anvil's slotted steel hammer, not their mallet, and it off the stand", 3);
        // Let go, the smith leaves their mallet in a box where they stood.
        Hosted.Fire(smith);
        RemoveThings(Hosted, t => t == anvil || t.Kind == ThingKind.Grave && t.Pos.DistanceTo(anvil.Pos) < 3);
    }

    /// <summary>A new packing station opens its list of goods; picking one sets what it packs, Shift on a backpack stack loads it, and the idle player beside it packs.</summary>
    private async Task CheckPackingStationListsGoodsAndTakesBackpackClicks() {
        await CloseWithEsc();
        var packer = Hosted.Add(ThingKind.Packer, world.Player + new Vector2(1.6f, 0));
        Give(Hosted, "berries", 5);
        await Click(view.Screen(packer.Pos, .8f));
        Require(Shows(packer) && Named("pack_berries") != null, "A packing station with nothing chosen must open its list of goods.");
        await Click(Named("pack_berries")!);
        Require(packer.Packs == "berries", "Picking berries must set what the station packs.");
        Require(Named("pack_berries") == null, "Picked berries must leave the list of goods to pack.");
        Require(Named("packed_crate") != null, "The station must show the crate being packed.");
        await Holding(Key.Shift, () => Click(Slots().First(c => c.TooltipText.StartsWith("Berries ·"))));
        await Until(() => packer.Inventory["berries"] > 0, "Shift on berries in the backpack loading them into the packing station", 2);
        int soundsBefore = sounds.Played;
        packer.Inventory.Add("crate", 1);
        await Until(() => view.PlayerAnimation == ActorAction.Pack && sounds.Heard("rustle", soundsBefore), "the idle player packing at the station beside it, rustling through the goods", 4);
        RemoveThings(Hosted, t => t == packer); Drop(Hosted, "berries"); await CloseWithEsc();
    }

    /// <summary>The crafting menu names the chisel a torch lacks, makes the chisel from stone by hand and then the torch from wood.</summary>
    private async Task CheckCraftingMenuMakesChiselAndTorchByHand() {
        await CloseWithEsc(); Drop(Hosted, "stone_chisel"); Give(Hosted, "stone", 2); Give(Hosted, "wood", 3);
        await KeyStroke(Key.C);
        Require(ShownPage() == "Crafting", "C must open the crafting menu.");
        // The page rebuilds when the queue moves on, so the card's line is looked up afresh.
        Label Lacks() => (Label)Named("craft_torch")!.FindChild("lacks", true, false);
        Require(Lacks().Visible && Lacks().Text == "No chisel" && !ShowsChild(Named("craft_torch")!, "inputs"), "A torch must name the chisel it lacks in place of its inputs.");
        Require(PopupLabel("torch", "tool_hint") == "Make one by hand: 2 Stone", "The torch's popup must say how to come by the chisel it lacks.");
        await Click(Named("craft_stone_chisel")!);
        Require(World.Waiting(world.Crafting, "stone_chisel") + (world.Making == "stone_chisel" ? 1 : 0) == 1 && world.Backpack["stone"] == 0, "Clicking a recipe must queue it and take its materials.");
        // Hand crafting only goes on once the player has stood still a moment.
        Hosted.Tick(World.CraftSettle); Hosted.Tick(world.Data.Recipes["stone_chisel"].Seconds);
        await Until(() => world.Backpack["stone_chisel"] == 1 && !Lacks().Visible && ShowsChild(Named("craft_torch")!, "inputs"), "the chisel arriving and the torch showing its inputs again", 2);
        int wood = world.Backpack["wood"];
        await Click(Named("craft_torch")!);
        Hosted.Tick(World.CraftSettle); Hosted.Tick(world.Data.Recipes["torch"].Seconds); await Frames(10);
        Require(world.Backpack["torch"] == 1 && world.Backpack["wood"] == wood - 3, "A torch must be made by hand from 3 wood.");
        await CloseWithEsc(); Drop(Hosted, "torch");
    }

    /// <summary>At a bench, one held unit put on an input slot loads one, a double click on the recipe loads a batch and a right click picks up half back.</summary>
    private async Task CheckBenchLoadsInputsByClickDoubleClickAndSplit() {
        await CloseWithEsc();
        var workbench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1, 0)); Hosted.SetRecipe(workbench, "crate"); Give(Hosted, "planks", 6);
        await ClickOn(workbench);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood planks")));
        await RightClick(Named("input_planks")!);
        await Until(() => workbench.Inventory["planks"] == 1, "a right click with planks held putting one in the input slot", 2);
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty && world.Backpack["planks"] == 5, "Esc letting go of the rest", 2);
        await DoubleClick(Named("craft_crate")!);
        Require(workbench.Inventory["planks"] == 6 && world.Backpack["planks"] == 0, "Double-clicking a bench recipe must load one batch of its inputs.");
        Require(workbench.Keep == "crate", "Double-clicking the marked recipe of an idle bench must keep it marked.");
        await SplitTo(Named("input_planks")!, BagCell(c => c.TooltipText == ""));
        await Until(() => workbench.Inventory["planks"] == 3 && world.Backpack["planks"] == 3, "a right click on a bench input picking up half of it", 2);
        RemoveThings(Hosted, t => t == workbench); Drop(Hosted, "planks", 3); await CloseWithEsc();
    }

    /// <summary>The output slot of a bench on planks takes planks put on it from the backpack, and refuses a stone.</summary>
    private async Task CheckOutputSlotTakesBackOnlyWhatTheRecipeMakes() {
        await CloseWithEsc();
        var bench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1, 0)); Hosted.SetRecipe(bench, "planks"); Give(Hosted, "planks", 4); Give(Hosted, "stone", 1);
        await ClickOn(bench);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood planks")));
        await Click(Named("output_planks")!);
        await Until(() => bench.Inventory["planks"] == 4 && cursor.Empty, "planks put on the bench's output slot going back into it", 2);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Stone")));
        await Click(Named("output_planks")!);
        await Frames(3);
        Require(bench.Inventory["stone"] == 0 && !cursor.Empty, "The output slot of a bench on planks must refuse a stone.");
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty && world.Backpack["stone"] == 1, "Esc letting go of the stone", 2);
        RemoveThings(Hosted, t => t == bench); Drop(Hosted, "stone", 1); await CloseWithEsc();
    }

    /// <summary>
    /// At a station Shift and Ctrl queue and load 5 and 10 with the count on the plate and put the worker mark on the recipe queued,
    /// a click unmarks what workers keep making, a right-click takes batches off, and no queue is drawn below.
    /// By hand a double-click queues just one, and a right-click on a slot in the queue takes a waiting batch off.
    /// </summary>
    private async Task CheckRecipeBoardsMarkAndQueueWithModifiers() {
        var furnace = Hosted.Add(ThingKind.Furnace, world.Player + new Vector2(1, 0));
        await ClickOn(furnace);
        string Queued(string plate) => ((Label)Named(plate)!.FindChild("queued", true, false)).Text;
        Require(((Control)Named("craft_iron_bar")!.FindChild("kept", true, false)).Visible, "A new stone furnace must show iron marked for workers.");
        bool Kept(string plate) => ((Control)Named(plate)!.FindChild("kept", true, false)).Visible;
        await Holding(Key.Shift, () => Click(Named("craft_charcoal")!));
        await Until(() => furnace.Keep == "charcoal" && Named("craft_charcoal") != null && Kept("craft_charcoal") && Queued("craft_charcoal") == "5", "a Shift-click queueing 5 charcoal and moving the worker mark to it", 2);
        await Click(Named("craft_charcoal")!);
        await Until(() => furnace.Keep == "" && !Kept("craft_charcoal"), "a click on the marked recipe taking the mark off", 2);
        await Holding(Key.Ctrl, () => Click(Named("craft_charcoal")!));
        await Until(() => Queued("craft_charcoal") == "15", "a Ctrl-click queueing 10 more charcoal, shown on the plate", 2);
        Require(furnace.Keep == "charcoal" && Kept("craft_charcoal"), "Queueing at a station with no mark must put the mark on the recipe queued.");
        await Holding(Key.Shift, () => RightClick(Named("craft_charcoal")!));
        await Until(() => Queued("craft_charcoal") == "10", "a Shift right-click taking 5 batches off", 2);
        Require(panel.FindChildren("queued_*", "", true, false).Count == 0, "A station must show what waits only as the count on its card.");
        RemoveThings(Hosted, t => t == furnace); await CloseWithEsc();

        Give(Hosted, "stone", 4);
        await KeyStroke(Key.C);
        await DoubleClick(Named("craft_stone_chisel")!);
        await Frames(3);
        Require(world.Backpack["stone"] == 2, "A double-click on a hand recipe must queue just one.");
        await RightClick(Named("craft_stone_chisel")!);
        await Until(() => world.Backpack["stone"] == 4 || world.Making == "stone_chisel", "a right-click taking the queued chisel off unless it is already being made", 2);
        if (world.Making != "") Hosted.Tick(world.Data.Recipes["stone_chisel"].Seconds);
        await Until(() => world.Making == "" && world.Crafting.Count == 0, "the hand queue emptying", 2);
        // Of three chisels one is being made, so the other two wait in one slot under the work.
        Give(Hosted, "stone", 6);
        await Frames();
        int stone = world.Backpack["stone"];
        await Click(Named("craft_stone_chisel")!);
        await Until(() => world.Making == "stone_chisel" && Named("making") != null && Named("queued_stone_chisel") == null, "the page showing the first chisel being made with nothing waiting", 2);
        await Click(Named("craft_stone_chisel")!);
        await Until(() => Named("queued_stone_chisel") != null, "the second chisel waiting in the queue's slot", 2);
        await Click(Named("craft_stone_chisel")!);
        await Until(() => Named("queued_stone_chisel")!.GetChildren().OfType<Badge>().First().Text == "2", "a third chisel counted on the queue's slot", 2);
        await RightClick(Named("queued_stone_chisel")!);
        await Until(() => World.Waiting(world.Crafting, "stone_chisel") == 1 && world.Backpack["stone"] == stone - 4, "a right-click on the queue's slot taking one waiting chisel off and giving its stone back", 2);
        await RightClick(Named("queued_stone_chisel")!);
        await Until(() => world.Crafting.Count == 0 && world.Backpack["stone"] == stone - 2, "a second right-click emptying the queue", 2);
        Hosted.Tick(world.Data.Recipes["stone_chisel"].Seconds);
        await Until(() => world.Making == "", "the chisel in progress being finished", 2);
        await CloseWithEsc(); Drop(Hosted, "stone", world.Backpack["stone"]); Drop(Hosted, "stone_chisel", world.Backpack["stone_chisel"]);
    }

    /// <summary>
    /// At a station, queueing another recipe while a batch is worked shakes the boxes and changes nothing.
    /// Once the batch is done, it takes what the boxes hold into the backpack and turns the station to the new recipe.
    /// </summary>
    private async Task CheckStationShakesWhileBusyThenTurnsToAnotherRecipe() {
        await CloseWithEsc();
        var furnace = Hosted.Add(ThingKind.Furnace, world.Player + new Vector2(1, 0)); Hosted.SetRecipe(furnace, "");
        Give(Hosted, "tin_ore", 2); Give(Hosted, "coal", 1); Give(Hosted, "wood", 2);
        await ClickOn(furnace);
        await DoubleClick(Named("craft_tin_bar")!);
        await Until(() => furnace.Processing && Named("input_tin_ore") != null, "the furnace starting the tin a double-click asked for", 2);
        await Holding(Key.Shift, () => Click(Named("craft_charcoal")!));
        Require(WorkRow().Shaking, "Queueing another recipe while a batch is worked must shake the boxes.");
        await Until(() => !WorkRow().Shaking, "the boxes coming to rest", 2);
        Require(furnace.Recipe == "tin_bar" && world.Waiting(furnace, "charcoal") == 0 && world.Backpack["wood"] == 2, "A furnace in the middle of a batch must stay on its recipe.");
        Hosted.Tick(world.Data.Recipes["tin_bar"].Seconds);
        await Until(() => !furnace.Processing && furnace.Inventory["tin_bar"] == 1, "the tin bar coming out", 2);
        // An idle page refreshes a few times a second; one that drew itself again without its slots would never show the bar's count.
        await Until(() => Named("output_tin_bar")?.GetChildren().OfType<Badge>().First().Text == "1", "the furnace that has run out of batches still showing the bar in its slot", 2);
        await DoubleClick(Named("craft_charcoal")!);
        await Until(() => furnace.Recipe == "charcoal" && world.Backpack["tin_bar"] == 1 && furnace.Inventory["wood"] == 2 && Named("input_wood") != null,
            "a double-click on charcoal taking the bar into the backpack and turning the furnace to charcoal, loaded with its wood", 2);
        RemoveThings(Hosted, t => t == furnace); Drop(Hosted, "tin_bar", 1); await CloseWithEsc();
    }

    /// <summary>
    /// With no room in the backpack for what a station's slots hold, a double-click on another recipe leaves the station as it is, its slots shaking.
    /// So does a click that would move the worker mark to another recipe while the slots hold something.
    /// </summary>
    private async Task CheckStationSlotsShakeWhenTheBackpackHasNoRoomForThem() {
        await CloseWithEsc();
        var furnace = Hosted.Add(ThingKind.Furnace, world.Player + new Vector2(1, 0)); Hosted.SetRecipe(furnace, "charcoal");
        Give(Hosted, "wood", 2); Hosted.LoadInput(furnace, "wood", 2);
        // Stone in every free slot leaves no room for the furnace's wood.
        int stone = Give(Hosted, "stone", world.Backpack.Slots * world.Backpack.StackOf("stone"));
        await ClickOn(furnace);
        await DoubleClick(Named("craft_iron_bar")!);
        Require(WorkRow().Shaking, "A double-click refused for a full backpack must shake the slots.");
        Require(furnace.Recipe == "charcoal" && furnace.Keep == "charcoal" && furnace.Inventory["wood"] == 2 && world.Waiting(furnace, "iron_bar") == 0,
            "A furnace whose slots the backpack has no room for must stay on its recipe with its wood and its mark.");
        await Until(() => !WorkRow().Shaking, "the slots coming to rest", 2);
        await Click(Named("craft_iron_bar")!);
        await Until(() => WorkRow().Shaking, "a click on another recipe shaking the slots of a furnace that holds wood", 2);
        Require(furnace.Keep == "charcoal", "A furnace that holds something must keep its mark where it is.");
        RemoveThings(Hosted, t => t == furnace); Drop(Hosted, "stone", stone); await CloseWithEsc();
    }

    /// <summary>A station whose mark is taken off stays on its recipe: the card keeps an outline and the page its slots.</summary>
    private async Task CheckUnmarkedStationStaysOnItsRecipe() {
        await CloseWithEsc();
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(1, 0));
        await ClickOn(anvil);
        await Click(Named("craft_steel_parts")!);
        await Until(() => anvil.Keep == "" && !((Control)Named("craft_steel_parts")!.FindChild("kept", true, false)).Visible, "a click taking the mark off steel parts", 2);
        Require(Named("input_steel_bar") != null && Named("craft_steel_parts")!.GetThemeStylebox("normal") == Style.PlateSelected,
            "An anvil with no mark must stay on steel parts, its card outlined and its slots on the page.");
        RemoveThings(Hosted, t => t == anvil); await CloseWithEsc();
    }

    /// <summary>A double-click whose batch is refused leaves the worker mark on: at its 99 batches the marked recipe gets back the mark the first press took off.</summary>
    private async Task CheckRefusedDoubleClickKeepsTheWorkerMark() {
        await CloseWithEsc();
        var furnace = Hosted.Add(ThingKind.Furnace, world.Player + new Vector2(1, 0));
        Hosted.QueueBatch(furnace, "iron_bar", World.MaxQueued);
        await ClickOn(furnace);
        await DoubleClick(Named("craft_iron_bar")!);
        await Frames(4);
        Require(furnace.Keep == "iron_bar" && world.Waiting(furnace, "iron_bar") == World.MaxQueued, "A double-click refused at 99 batches must leave the marked recipe marked.");
        RemoveThings(Hosted, t => t == furnace); await CloseWithEsc();
    }

    /// <summary>A recipe group closes on a click on its heading and opens on the next, on the crafting menu and on a station alike.</summary>
    private async Task CheckRecipeGroupsCloseAndOpenOnAClick() {
        await CloseWithEsc();
        await KeyStroke(Key.C);
        Require(Named("craft_planks") != null && Named("craft_stone_chisel") != null, "The crafting menu must open with its groups showing their recipes.");
        await Click(Named("group_Materials")!);
        await Until(() => Named("craft_planks") == null && Named("craft_stone_chisel") != null, "a click on the Materials heading hiding its recipes only", 2);
        var furnace = Hosted.Add(ThingKind.Furnace, world.Player + new Vector2(1, 0));
        await ClickOn(furnace);
        // A new stone furnace keeps making iron, whose group opens with the page.
        Require(Named("craft_iron_bar") != null, "A station's page must open the group of what it is making, closed or not.");
        await Click(Named("group_Materials")!);
        await Until(() => Named("craft_iron_bar") == null && ShowsChild(Named("group_Materials")!, "kept"), "the closed group wearing the hard hat of the recipe it hides", 2);
        await Click(Named("group_Materials")!);
        await Until(() => Named("craft_iron_bar") != null, "a second click opening the group again", 2);
        RemoveThings(Hosted, t => t == furnace); await CloseWithEsc();
    }

    /// <summary>
    /// A bench's tool slots sit in a window of their own beside its page. A recipe's popup says where its tool is:
    /// not available, in the backpack, which works for the player alone, or available once slotted.
    /// </summary>
    private async Task CheckBenchShowsItsToolsBesideThePageAndWhereARecipesToolIs() {
        await CloseWithEsc(); Drop(Hosted, "stone_chisel", world.Backpack["stone_chisel"]);
        var workbench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1, 0));
        await ClickOn(workbench);
        var slot = panel.Aside.FindChild("tool_chisel", true, false) as ItemSlot;
        Require(panel.Aside.Visible && slot != null && HasLabel(panel.Aside, "Available tools") && HasLabel(panel.Aside, "Empty"), "A bench must show its empty tool slots in a window beside its page.");
        Require(panel.Aside.GetGlobalRect().End.X <= panel.GetGlobalRect().Position.X, "The tool window must stand left of the page.");
        string ToolState() => PopupLabel("planks", "tool_state");
        Label Lacks() => (Label)Named("craft_planks")!.FindChild("lacks", true, false);
        Require(ToolState() == "not available" && Lacks().Visible && Lacks().Text == "No chisel", "With no chisel anywhere a bench recipe must say so on its card and in its popup.");
        Give(Hosted, "stone_chisel", 1);
        await Until(() => !Lacks().Visible, "the card showing its inputs once a chisel is carried", 2);
        Require(ToolState() == "in your backpack", "A carried chisel at a bench with an empty slot must show as in the backpack.");
        await Holding(Key.Shift, () => Click(BagCell(c => c.TooltipText.StartsWith("Stone chisel"))));
        await Until(() => world.BenchTool(workbench, "chisel") == "stone_chisel" && HasLabel(panel.Aside, "Stone chisel"), "Shift on the carried chisel slotting it, named in the tool window", 2);
        Require(ToolState() == "available", "A slotted chisel must show as available.");
        RemoveThings(Hosted, t => t == workbench); await CloseWithEsc();
        Require(!panel.Aside.Visible, "The tool window must close with the page.");
    }

    /// <summary>A recipe list that has scrolled keeps its place when a click on a recipe makes the page draw again.</summary>
    private async Task CheckRecipeListKeepsItsPlaceWhenThePageDrawsAgain() {
        await CloseWithEsc();
        // A bench that is making something shows its work under the list; moved down the screen, the page leaves the list too little room for all its recipes.
        var workbench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1, 0)); Hosted.SetRecipe(workbench, "planks");
        await ClickOn(workbench);
        await MoveWindow(panel, new Vector2(0, 150));
        ScrollContainer List() => (ScrollContainer)panel.FindChild("recipe_list", true, false);
        await Until(() => List().GetVScrollBar().MaxValue > List().Size.Y, "the bench's recipes outgrowing the room the page leaves them", 2);
        await Wheel((layer.Transform * List().GetGlobalRect()).GetCenter());
        int scrolled = List().ScrollVertical;
        Require(scrolled > 0, "The wheel must scroll the recipe list.");
        var card = panel.FindChildren("craft_*", "Button", true, false).OfType<Control>().First(c => c.Name != "craft_planks" && List().GetGlobalRect().Encloses(c.GetGlobalRect()));
        string recipe = card.Name.ToString()["craft_".Length..];
        var before = List();
        await Click(card);
        // Every frame until it is back in place, the new list is out of sight or already where the old one was scrolled to.
        for (int frame = 0; frame < 120 && !(List() != before && List().ScrollVertical == scrolled && List().Modulate.A > 0); frame++) {
            Require(List() == before || List().Modulate.A == 0 || List().ScrollVertical == scrolled, "A list drawn again must not show from the top before it goes back to where the old one was scrolled to.");
            await Frames(1);
        }
        Require(workbench.Keep == recipe && List() != before && List().ScrollVertical == scrolled, "The click must mark the recipe for workers, and the list drawn again must go back to where the old one was scrolled to.");
        RemoveThings(Hosted, t => t == workbench); await CloseWithEsc();
    }

    /// <summary>Opening crafting leaves the recipe map on its own button in the bottom bar.</summary>
    private async Task CheckCraftingLeavesTheRecipeMapOnTheBottomBar() {
        await KeyStroke(Key.C);
        Require(ShownPage() == "Crafting" && Named("open_map") == null, "Crafting should open without a recipe map button.");
        Require(hotbar.FindChild("map", true, false) is Button map && map.IsVisibleInTree(), "The bottom bar should still offer the recipe map.");
        await CloseWithEsc();
    }

    /// <summary>The bottom bar opens the map directly from the world, with every item once and those the settlement has had drawn as known.</summary>
    private async Task CheckRecipeMapOpensFromTheBottomBarAndShowsEveryItemOnce() {
        await CloseWithEsc();
        var map = hotbar.FindChild("map", true, false) as Button;
        Require(map != null, "The bottom bar must offer a recipe map icon.");
        await Click(map!);
        Require(recipeMap.Visible && !panel.Visible && !bag.Visible, "The icon must open the recipe map directly from the world.");
        Require(world.Data.Items.Keys.Where(id => !GameData.OffTheMap(id)).All(id => recipeMap.FindChildren($"map_{id}", "", true, false).Count == 1), "Every item must be on the map once.");
        Require(recipeMap.NodeOf("steel_axe")!.Known && !recipeMap.NodeOf("canned_jam")!.Known, "The axe the settlement started with must be drawn as known, and jam nobody has canned as not made yet.");
        Require(HasLabel(recipeMap, "Required tool") && HasLabel(recipeMap, "Additional resources") && HasLabel(recipeMap, "Hunt animals"), "The map must name its marks and where raw meat comes from.");
        await KeyStroke(Key.Escape);
        Require(!recipeMap.Visible && !panel.Visible && !bag.Visible, "Escape must close the map and return to the world.");
    }

    /// <summary>Alt-clicking the backpack's wood opens its details without picking it up and returns to the backpack on Escape.</summary>
    private async Task CheckAltClickBackpackItemOpensItsRecipeMapEntry() {
        Give(Hosted, "wood", 1); await Frames();
        await KeyStroke(Key.Tab);
        int count = world.Carried("wood");
        await Click(BagCellOf("wood"), alt: true);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "wood", "the backpack's wood highlighted on the recipe map", 2);
        Require(HasLabel((Node)recipeMap.FindChild("map_details", true, false), "Chop trees") && cursor.Empty && world.Carried("wood") == count,
            "The map should explain how to get wood without picking up or moving the stack.");
        await KeyStroke(Key.Escape);
        Require(!recipeMap.Visible && bag.Visible, "Escape should return to the open backpack.");
        Drop(Hosted, "wood", 1); await CloseWithEsc();
    }

    /// <summary>Alt-clicking an item on the hotbar shows its place on the map without selecting the slot.</summary>
    private async Task CheckAltClickHotbarItemOpensItsRecipeMapEntry() {
        int axeSlot = Enumerable.Range(0, 9).First(i => world.HotbarItem(i) == "steel_axe");
        bool axeWasSelected = world.ActiveHotbar == axeSlot;
        if (axeWasSelected) await KeyStroke(Key.Key8);
        int active = world.ActiveHotbar;
        Require(active != axeSlot, "Start with a different slot selected so the shortcut cannot select the axe unnoticed.");
        await Click(hotbar.Buttons[axeSlot], alt: true);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "steel_axe" && recipeMap.NodeOf("steel_axe")!.Lit,
            "the hotbar's steel axe highlighted on the recipe map", 2);
        var rows = (ScrollContainer)recipeMap.FindChild("map_rows", true, false);
        var axe = recipeMap.NodeOf("steel_axe")!;
        await Until(() => rows.GetGlobalRect().Encloses(axe.GetGlobalRect()), "the steel axe scrolling fully into view", 2);
        Require(world.ActiveHotbar == active, "Looking up the axe must not select its hotbar slot.");
        await KeyStroke(Key.Escape);
        if (axeWasSelected) await KeyStroke(Key.Key1 + axeSlot);
    }

    /// <summary>Alt-clicking a crafting card shows its product without queueing the recipe.</summary>
    private async Task CheckAltClickRecipeShowsTheProductWithoutQueueing() {
        Give(Hosted, "stone", 2); await Frames();
        await KeyStroke(Key.C);
        int stone = world.Carried("stone");
        int chisels = world.Carried("stone_chisel");
        await Click((Control)Named("craft_stone_chisel")!, alt: true);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "stone_chisel", "the stone chisel highlighted on the recipe map", 2);
        Require(world.Waiting(null, "stone_chisel") == 0 && world.Carried("stone") == stone && world.Carried("stone_chisel") == chisels,
            "Looking up a recipe must not craft it.");
        await KeyStroke(Key.Escape);
        Require(ShownPage() == "Crafting", "Closing the map must return to crafting.");
        Drop(Hosted, "stone", 2); await CloseWithEsc();
    }

    /// <summary>Alt-clicking the stone in the active crafting row shows the stone rather than the chisel being made.</summary>
    private async Task CheckAltClickIngredientInTheActiveCraftingRow() {
        Give(Hosted, "stone", 2); await Frames();
        await KeyStroke(Key.C);
        int chisels = world.Carried("stone_chisel");
        await Click(Named("craft_stone_chisel")!);
        await Until(() => Named("queue_input_stone") != null && world.Making == "stone_chisel", "the chisel's ingredient slot appearing", 2);
        await Click(Named("queue_input_stone")!, alt: true);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "stone", "the active recipe's stone highlighted on the recipe map", 2);
        await KeyStroke(Key.Escape);
        Hosted.Tick(8);
        await Until(() => world.Carried("stone_chisel") == chisels + 1, "the chisel finishing without queueing another", 2);
        Drop(Hosted, "stone_chisel", 1);
        await CloseWithEsc();
    }

    /// <summary>On the recipe map a click on a chip goes to its item and lights it up for a moment, a click on an item shows its details, and Show leaves the map for where it is made.</summary>
    private async Task CheckRecipeMapChipsLightTheirItemAndShowLeadsToTheStation() {
        await CloseWithEsc();
        var workbench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1, 0));
        await Click((Button)hotbar.FindChild("map", true, false));
        var coal = recipeMap.NodeOf("coal")!;
        await Click((Control)recipeMap.NodeOf("steel_bar")!.FindChild("also_coal", true, false));
        Require(coal.Lit, "A click on the steel bar's coal chip must light coal up.");
        await Until(() => !coal.Lit, "the light on coal going out again", RecipeMap.LitSeconds + 2);
        await Click((Control)recipeMap.NodeOf("crate")!.FindChild("slot", true, false));
        Require(recipeMap.Chosen == "crate" && ((Label)recipeMap.FindChild("detail_name", true, false)).Text == "Wood crate" && HasLabel((Node)recipeMap.FindChild("map_details", true, false), "6 Wood planks"), "A click on the crate must show its name and the six planks it requires.");
        await Click((Control)recipeMap.NodeOf("planks")!.FindChild("slot", true, false));
        Require(HasLabel((Node)recipeMap.FindChild("map_details", true, false), "10 s by hand, 5 s at the crafting bench"), "Planks must show their time by hand and at the bench.");
        await Click((Control)recipeMap.NodeOf("tin_can")!.FindChild("slot", true, false));
        // Ten seconds, halved by the bench and divided by the steel hammer's 1.8.
        Require(HasLabel((Node)recipeMap.FindChild("map_details", true, false), "steel hammer · 2.8 s"), "Tin cans must show their time with the steel hammer they require.");
        await Click((Control)recipeMap.NodeOf("crate")!.FindChild("slot", true, false));
        await Click((Control)recipeMap.FindChild("map_show", true, false));
        await Until(() => !recipeMap.Visible && Shows(workbench) && Named("craft_crate") != null, "Show leaving the map for the bench's page with the crate on it", 2);
        RemoveThings(Hosted, t => t == workbench); await CloseWithEsc();
    }

    /// <summary>The game goes on under the open recipe map: time passes and an item first made meanwhile turns to made before, while a held walking key leaves the player in place.</summary>
    private async Task CheckGameGoesOnUnderTheOpenRecipeMap() {
        await CloseWithEsc();
        await KeyStroke(Key.C);
        await Click((Button)hotbar.FindChild("map", true, false));
        var stew = recipeMap.NodeOf("canned_meat_stew")!;
        Require(!stew.Known, "Canned stew nobody has made must be drawn as not made yet.");
        float opened = world.Time;
        await Until(() => !view.Paused && world.Time > opened + .2f, "time passing under the open map", 2);
        Achieve(Hosted, "canned_meat_stew");
        await Until(() => stew.Known, "the stew turning to made before on the open map", 2);
        await Click((Control)stew.FindChild("slot", true, false));
        Require(HasLabel((Node)recipeMap.FindChild("map_details", true, false), "Made before"), "The stew's details must say it was made before.");
        var stood = world.Player;
        await Holding(Key.D, () => Seconds(.3));
        Require(world.Player.DistanceTo(stood) < .01f, "A walking key held under the map must not move the player.");
        await KeyStroke(Key.Escape);
        Require(!recipeMap.Visible && ShownPage() == "Crafting", "Escape must leave the previous panel open.");
        await CloseWithEsc();
    }

    /// <summary>
    /// A new anvil's page shows steel bars in, steel parts out and, beside it, an empty steel hammer slot.
    /// Shift on a wooden mallet leaves it in the backpack with nothing said, Shift on a steel hammer slots it onto the
    /// stand, and Ctrl on a backpack bar loads them; the batch time drops from 5 s to 3 s as soon as a steel hammer comes
    /// into the backpack with the page open.
    /// </summary>
    private async Task CheckNewAnvilMakesSteelPartsAndLoadsFromTheBackpack() {
        var anvil = Hosted.Add(ThingKind.Anvil, world.Player + new Vector2(1, 0));
        int steelHammers = Drop(Hosted, "steel_hammer");
        Give(Hosted, "steel_bar", 2); Give(Hosted, "wood_hammer", 1);
        await ClickOn(anvil);
        int mallets = world.Backpack["wood_hammer"];
        Require(Named("input_steel_bar") != null, "A new anvil must show a slot for steel bars.");
        Require(Named("output_steel_parts") != null, "A new anvil must show a slot for steel parts.");
        var slot = panel.Aside.FindChild("tool_hammer", true, false) as ItemSlot;
        Require(panel.Aside.Visible && slot != null && slot.TooltipText == "Steel hammer slot.",
            $"A new anvil must show an empty slot beside its page named \"Steel hammer slot.\", not \"{slot?.TooltipText}\".");
        Require(view.PropOf(anvil.Id) is AnvilProp { Hammer.Shown: null }, "A new anvil must have no hammer lying on its stand.");
        await Holding(Key.Shift, () => Click(BagCell(c => c.TooltipText.StartsWith("Wooden mallet"))));
        await Frames(3);
        Require(world.BenchTool(anvil, "hammer") == null && world.Backpack["wood_hammer"] == mallets, "The anvil's hammer slot must refuse a wooden mallet.");
        Require(!messages.Shown.ToArray().Any(text => text.Contains("mallet", StringComparison.OrdinalIgnoreCase) || text.Contains("hammer", StringComparison.OrdinalIgnoreCase)),
            $"Shift on a wooden mallet at the anvil must say nothing, not \"{string.Join(" / ", messages.Shown.ToArray())}\".");
        Require((panel.FindChild("batch_time", true, false) as Label)?.Text == "5 s", $"An anvil with no steel hammer at hand must show its 5 s batch time, not {(panel.FindChild("batch_time", true, false) as Label)?.Text}.");
        // A steel hammer coming into the backpack while the page is open speeds the batch up for the player.
        // The steel hammer works 1.8 times as fast: 5 s / 1.8 is 2.8 s, shown as 3 s.
        Give(Hosted, "steel_hammer", 1);
        await Until(() => (panel.FindChild("batch_time", true, false) as Label)?.Text == "3 s", "the open anvil page showing the steel hammer's 3 s batch time", 2);
        await Holding(Key.Shift, () => Click(BagCell(c => c.TooltipText.StartsWith("Steel hammer"))));
        await Until(() => world.BenchTool(anvil, "hammer") == "steel_hammer" && view.PropOf(anvil.Id) is AnvilProp { Hammer.Shown: "steel_hammer" },
            "Shift on a steel hammer in the backpack slotting it into the anvil, where it lies on the stand", 2);
        await Holding(Key.Ctrl, () => Click(BagCell(c => c.TooltipText.StartsWith("Steel bar"))));
        await Until(() => world.Backpack["steel_bar"] == 0, "Ctrl on steel bars in the backpack loading them into the anvil", 2);
        RemoveThings(Hosted, t => t == anvil); Drop(Hosted, "steel_bar"); Drop(Hosted, "wood_hammer", 1); await CloseWithEsc();
        if (steelHammers > 0) Give(Hosted, "steel_hammer", steelHammers);
    }

    /// <summary>
    /// A hammer blow at a station lands the hammer's face on its work top as it is heard, wherever the player stands to
    /// work it: the anvil's face and the bench's top stand between 0.75 m and 0.97 m up.
    /// </summary>
    private async Task CheckEveryHammerBlowLandsOnTheStationsWorkTop() {
        var start = Hosted.Player;
        var rig = view.PlayerRig;
        foreach (var (kind, offset) in new[] { (ThingKind.Anvil, new Vector2(-.5f, -1.2f)), (ThingKind.Bench, new Vector2(1.2f, 0)) }) {
            Warp(Hosted, start); await Frames(3);
            var thing = Hosted.Add(kind, start + offset);
            if (kind == ThingKind.Anvil) { SlotPlainestTools(Hosted, thing); Give(Hosted, "steel_bar", 1); Hosted.QueueBatch(thing, "steel_parts"); }
            if (kind == ThingKind.Bench) { thing.Tools!.Add("wood_hammer", 1); Give(Hosted, "planks", 6); Hosted.QueueBatch(thing, "crate"); }
            string sound = kind == ThingKind.Anvil ? "anvil_work" : "bench_work";
            await Frames(2);
            int soundsBefore = sounds.Played;
            await Until(() => sounds.Heard(sound, soundsBefore) && view.PlayerMark != null, $"the player working a {kind} {offset} away and its blow heard", 8);
            var end = rig.Root.Transform * rig.Holds(ActorRig.HammerFace);
            float gap = SurfaceGap(view.PropOf(thing.Id)!.Body, end);
            Require(gap < .04f && end.Y is > .73f and < .99f, $"A hammer's face must land on the {kind}'s work top, not {gap:0.00} m off the model at {end.Y:0.00} m up.");
            await Frames(2);
            RemoveThings(Hosted, t => t == thing);
        }
        Warp(Hosted, start); await CloseWithEsc(); await Frames(4);
    }
}
