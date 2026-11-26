using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunLanternUIChecks() {
        await TestClean(CheckADoubleClickTakesTheLanternInHandAndPutsOutTheTorch);
        await TestClean(CheckAPlacedLanternOpensWithRightClickAndTakesFirebugsGradually);
    }

    private async Task CheckADoubleClickTakesTheLanternInHandAndPutsOutTheTorch() {
        string empty = Hosted.Lanterns.NewEmpty();
        Require(Give(Hosted, empty) == 1 && GiveFirebugs(Hosted, 1) == 1, "an empty lantern and a firebug should fit in the backpack");
        Require(Hosted.Lanterns.Refill(Lanterns.Id(empty), count: 1), "adding a firebug should light the lantern");
        string lantern = Hosted.Lanterns.Carried(Lanterns.Id(empty))!;
        int torch = Give(Hosted, "torch");
        Hosted.UseTorch(Vector2.Zero);
        await Until(() => world.TorchLit, "the torch being lit before the lantern is taken in hand", 5);
        await KeyStroke(Key.Tab);
        await RightClick(BagCellOf(lantern));
        Require(panel.Page == Page.Lantern && world.InHand == "" && world.TorchLit,
            "right-clicking a lantern in the backpack should only open its refill page");
        await DoubleClick(BagCellOf(lantern));
        await Until(() => world.InHand == lantern && !world.TorchLit && !panel.Visible && !bag.Visible,
            "the lantern in hand replacing the lit torch", 3);
        Require(world.Backpack["torch"] >= torch, "taking the lantern in hand should put out the torch without consuming it");
        await KeyStroke(Key.Key1);
        Drop(Hosted, lantern); Drop(Hosted, "torch", torch);
        await Frames();
    }

    private async Task CheckAPlacedLanternOpensWithRightClickAndTakesFirebugsGradually() {
        string item = Hosted.Lanterns.NewEmpty();
        int bugsBefore = Hosted.Backpack[Firebugs.Item];
        Require(Give(Hosted, item) == 1 && GiveFirebugs(Hosted, 12) == 12, "an empty lantern and twelve fresh firebugs should fit");
        await KeyStroke(Key.Tab);
        await RightClick(BagCell(c => c.TooltipText.StartsWith(Lanterns.EmptyName, System.StringComparison.Ordinal)));
        Require(panel.HeadingTitle.Text == "Empty lantern", "opening the carried empty lantern should show its name");
        await Click(FindButton(panel, "Put it down")!);
        var home = world.Player;
        var spot = SpotsNearby().First(p => p.DistanceTo(home) < 3.2f && Uncovered(view.Screen(p))
            && world.Construction.PlacementBlock(ThingKind.Lantern, p, item) == null);
        await Click(view.Screen(spot));
        await Until(() => world.ThingsOf(ThingKind.Lantern).Any(t => t.Pos == spot), "the empty lantern appearing on the ground", 4);
        var lantern = world.ThingsOf(ThingKind.Lantern).First(t => t.Pos == spot);
        await Until(() => view.Pick(view.Screen(spot, .25f))?.Id == lantern.Id, "the lantern becoming clickable", 4);
        await RightClick(spot, .25f);
        Require(panel.Selection.Thing == lantern.Id && panel.HeadingTitle.Text == "Empty lantern", "a right click on a placed empty lantern should show its panel");
        var slot = Named("lantern_firebugs")!;
        await PickUp(BagCellOf(Firebugs.Item));
        int heldBefore = cursor.Stack?.Count ?? 0;
        Require(heldBefore >= 12, "the cursor should hold enough firebugs for a fill and an overflow");
        await RightClick(slot);
        await Until(() => world.Lanterns.BugCount(lantern) == 1 && panel.HeadingTitle.Text == "Lantern", "the first firebug filling and renaming the lantern", 4);
        await RightClick(slot);
        await Until(() => world.Lanterns.BugCount(lantern) == 2 && OnPage("lantern_firebug_count") is Label { Text: "2/10" }, "the second firebug updating the count", 4);
        await Click(slot);
        await Until(() => world.Lanterns.BugCount(lantern) == 10 && world.Backpack[Firebugs.Item] == bugsBefore + 2 && OnPage("lantern_firebug_count") is Label { Text: "10/10" },
            "depositing eight more firebugs and leaving the surplus in the backpack", 4);
        await Click(slot);
        Require(world.Lanterns.BugCount(lantern) == 10 && cursor.Stack?.Count == heldBefore - 10, "the full lantern should reject the remaining held firebugs");
        await Click(FindButton(panel, "Pick up")!);
        await Until(() => world.Find(lantern.Id) == null && world.Lanterns.Carried(Lanterns.Id(item)) != null && !panel.Visible,
            "picking up the filled lantern from its ground panel", 4);
        Drop(Hosted, Hosted.Lanterns.Carried(Lanterns.Id(item))!); Drop(Hosted, Firebugs.Item, 2);
        await Frames();
    }
}
