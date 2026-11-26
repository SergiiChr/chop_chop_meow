using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunNettleUIChecks() {
        await TestClean(CheckItemTextShowsUnderItsNameInTheBackpackAndOnTheHotbar);
        await TestClean(CheckRecipeMapShowsItemTextAndTheMalletRopeNeeds);
        await TestClean(CheckRopeCardSaysASteelHammerWillDamageTheFibers);
        await TestClean(CheckADoubleClickOnRopeWithOnlyASteelHammerKeepsTheBenchsBatches);
        await TestClean(CheckPickingANettleByHandGroansUntilItIsPicked);
        await TestClean(CheckPickingANettleWithAShovelMakesNoGroan);
    }

    private async Task CheckItemTextShowsUnderItsNameInTheBackpackAndOnTheHotbar() {
        await CloseWithEsc();
        string before = world.HotbarItem(7);
        Give(Hosted, "nettle", 2); Give(Hosted, "bug_net"); SetHotbarSlot(Hosted, 7, "bug_net");
        await KeyStroke(Key.Tab);
        await Until(() => BagCellOf("nettle").TooltipText.Contains("2 total"), "the nettles showing in the backpack", 2);
        Require(BagCellOf("nettle").TooltipText.Contains("\nIt stings but my grandma used to make ropes out of it.\n"), $"a nettle's tooltip should say what the player says of it on a line of its own: {BagCellOf("nettle").TooltipText}");
        Require(hotbar.Buttons[7].TooltipText.Contains("\nI can catch all kinds of bugs with this.\n"), $"the bug net's hotbar tooltip should say what the player says of it: {hotbar.Buttons[7].TooltipText}");
        Require(!BagCellOf("steel_axe").TooltipText.Contains("\n\n") && BagCellOf("steel_axe").TooltipText.Split('\n').Length == 2, "an item with nothing said of it should keep its two tooltip lines");
        Drop(Hosted, "nettle"); Drop(Hosted, "bug_net"); SetHotbarSlot(Hosted, 7, before);
        await CloseWithEsc();
    }

    private async Task CheckRecipeMapShowsItemTextAndTheMalletRopeNeeds() {
        await CloseWithEsc();
        Achieve(Hosted, "firebug", "nettle_rope", "wood_hammer"); await Frames();
        await Click((Button)hotbar.FindChild("map", true, false));
        await Click((Control)recipeMap.NodeOf("firebug")!.FindChild("slot", true, false));
        var details = (Node)recipeMap.FindChild("map_details", true, false);
        Require(((Label)recipeMap.FindChild("detail_text", true, false)).Text == "Glows even after death." && HasLabel(details, "Catch with a bug net after dark"), "A click on the firebug must show what the player says of it and how it is come by.");
        await Click((Control)recipeMap.NodeOf("nettle_rope")!.FindChild("slot", true, false));
        // Ten seconds, halved by the bench, at the mallet's speed of 1.
        Require(HasLabel(details, "wooden mallet · 5 s") && recipeMap.FindChild("detail_text", true, false) == null, "Nettle rope must name the wooden mallet it needs and show no text of its own.");
        await KeyStroke(Key.Escape);
        await CloseWithEsc();
    }

    private async Task CheckRopeCardSaysASteelHammerWillDamageTheFibers() {
        await CloseWithEsc();
        var bench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1.5f, 0));
        bench.Tools!.Add("steel_hammer", 1);
        await Frames(4);
        await ClickOn(bench);
        await Until(() => Named("craft_nettle_rope") != null, "the bench's page with the rope card", 2);
        Label Lacks() => (Label)Named("craft_nettle_rope")!.FindChild("lacks", true, false);
        await Until(() => Lacks().Visible && Lacks().Text == "Steel hammer will damage the fibers.", "the rope card saying a steel hammer will damage the fibers", 2);
        bench.Tools.Clear();
        await Until(() => Lacks().Visible && Lacks().Text == "No wooden mallet", "the rope card naming the mallet it lacks at a bench with no hammer", 2);
        bench.Tools.Add("wood_hammer", 1);
        await Until(() => !Lacks().Visible, "the rope card showing its inputs once a mallet is slotted", 2);
        RemoveThings(Hosted, t => t == bench);
        await CloseWithEsc();
    }

    private async Task CheckADoubleClickOnRopeWithOnlyASteelHammerKeepsTheBenchsBatches() {
        await CloseWithEsc();
        var bench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1.5f, 0));
        bench.Tools!.Add("steel_hammer", 1);
        // With nothing of a bug net carried, its batches wait at a bench with empty slots, which a mark may turn.
        int wood = world.Backpack["wood"], parts = world.Backpack["wood_parts"];
        Drop(Hosted, "wood", wood); Drop(Hosted, "wood_parts", parts);
        Hosted.QueueBatch(bench, "bug_net", 2);
        Require(Hosted.Waiting(bench, "bug_net") == 2 && bench.Inventory.Count == 0, $"two bug nets should wait at an empty bench: {Hosted.Waiting(bench, "bug_net")}");
        await Frames(4);
        await ClickOn(bench);
        await Until(() => Named("craft_nettle_rope") != null, "the bench's page with the rope card", 2);
        await DoubleClick(Named("craft_nettle_rope")!);
        // Longer than the wait for a second press, after which a refused mark is sent.
        await Frames(40);
        Require(bench.Keep == "bug_net" && Hosted.Waiting(bench, "bug_net") == 2,
            $"A refused double-click on rope must leave the bench on bug nets with both batches waiting: {bench.Keep}, {Hosted.Waiting(bench, "bug_net")}.");
        RemoveThings(Hosted, t => t == bench);
        Give(Hosted, "wood", wood); Give(Hosted, "wood_parts", parts);
        await CloseWithEsc();
    }

    private async Task CheckPickingANettleByHandGroansUntilItIsPicked() {
        await CloseWithEsc();
        int shovels = Drop(Hosted, "steel_shovel");
        var (groaned, brokeOff) = await PickNettleBesideThePlayer();
        Require(groaned && !brokeOff, $"Picking a nettle by hand must groan for as long as it stings: groaned {groaned}, broke off {brokeOff}.");
        await Until(() => !sounds.Holding("sting_groan"), "the groan fading out once the nettle is picked", 1);
        Give(Hosted, "steel_shovel", shovels);
    }

    private async Task CheckPickingANettleWithAShovelMakesNoGroan() {
        await CloseWithEsc();
        int shovels = Drop(Hosted, "steel_shovel");
        Give(Hosted, "steel_shovel");
        int soundsBefore = sounds.Played;
        var (groaned, _) = await PickNettleBesideThePlayer();
        Require(!groaned && !sounds.Heard("sting_groan", soundsBefore), "Picking a nettle with a shovel carried must make no groan, since it does not sting.");
        Drop(Hosted, "steel_shovel"); Give(Hosted, "steel_shovel", shovels);
    }
}
