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
}
