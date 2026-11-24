using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunNettleUIChecks() {
        await TestClean(CheckItemTextShowsUnderItsNameInTheBackpackAndOnTheHotbar);
        await TestClean(CheckRecipeMapShowsItemTextAndTheMalletRopeNeeds);
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
        await KeyStroke(Key.C);
        await Click(Named("open_map")!);
        await Click((Control)recipeMap.NodeOf("firebug")!.FindChild("slot", true, false));
        var details = (Node)recipeMap.FindChild("map_details", true, false);
        Require(((Label)recipeMap.FindChild("detail_text", true, false)).Text == "Glows even after death." && HasLabel(details, "Catch with a bug net after dark"), "A click on the firebug must show what the player says of it and how it is come by.");
        await Click((Control)recipeMap.NodeOf("nettle_rope")!.FindChild("slot", true, false));
        // Ten seconds, halved by the bench, at the mallet's speed of 1.
        Require(HasLabel(details, "wooden mallet · 5 s") && recipeMap.FindChild("detail_text", true, false) == null, "Nettle rope must name the wooden mallet it needs and show no text of its own.");
        await KeyStroke(Key.Escape);
        await CloseWithEsc();
    }
}
