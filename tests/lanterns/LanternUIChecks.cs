using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunLanternUIChecks() {
        await TestClean(CheckLanternBadgesShowLiveFirebugsInsteadOfStackSize);
        await TestClean(CheckThePackingStationSeparatesFirebugsFromLanternQuantity);
        await TestClean(CheckCtrlRefillUsesOlderFirebugsAcrossStacks);
        await TestClean(CheckADoubleClickTakesTheLanternInHandAndPutsOutTheTorch);
        await TestClean(CheckAPlacedLanternOpensWithRightClickAndTakesFirebugsGradually);
    }

    private async Task CheckLanternBadgesShowLiveFirebugsInsteadOfStackSize() {
        string empty = Hosted.Lanterns.NewEmpty(), charged = Hosted.Lanterns.NewEmpty();
        Give(Hosted, empty); Give(Hosted, charged); GiveFirebugs(Hosted, 3);
        Require(Hosted.Lanterns.Refill(Lanterns.Id(charged), count: 3), "three live firebugs should fit in the lantern");
        charged = Hosted.Lanterns.Carried(Lanterns.Id(charged))!;
        string previous = world.HotbarItem(8);
        SetHotbarSlot(Hosted, 8, charged);
        await KeyStroke(Key.Tab);
        await Until(() => bag.Visible && hotbar.Counts[8].Text == "3", "the hotbar showing three live firebugs", 3);
        var full = BagCellOf(charged).GetNode<Badge>("count");
        var dark = BagCell(c => c.TooltipText.StartsWith(Lanterns.EmptyName, System.StringComparison.Ordinal)).GetNode<Badge>("count");
        Require(full.Text == "3" && dark.Text == "0" && full.GetThemeColor("font_color") == Style.FirebugGreen
            && hotbar.Counts[8].GetThemeColor("font_color") == Style.FirebugGreen, "firebug badges should be green even though each lantern is a single item");
        foreach (var badge in new[] { full, dark, hotbar.Counts[8] }) {
            var icon = (Control)badge.GetParent();
            Vector2 center = badge.GetRect().GetCenter();
            Require(center.X > icon.Size.X / 2 && center.Y < icon.Size.Y / 2,
                "a green firebug count should sit at the upper right of its lantern icon");
            Require(badge.GetThemeStylebox("normal") is StyleBoxFlat ring && ring.BorderColor == Style.FirebugGreen,
                "the firebug count should also have a green outline, distinct from the ordinary count badge");
        }
        Require(!BagCellOf("steel_axe").GetNode<Badge>("count").Visible, "a single tool should not show a quantity badge");
        await CloseWithEsc();
        Drop(Hosted, empty); Drop(Hosted, charged); SetHotbarSlot(Hosted, 8, previous);
        await Frames();
    }

    private async Task CheckThePackingStationSeparatesFirebugsFromLanternQuantity() {
        var at = SpotsNearby().First(p => p.DistanceTo(world.Player) < 3.2f && Uncovered(view.Screen(p, .3f))
            && world.Construction.PlacementBlock(ThingKind.Lantern, p) == null);
        var packer = Hosted.Add(ThingKind.Packer, at);
        Hosted.SetPacking(packer, Lanterns.Item);
        string empty = Hosted.Lanterns.NewEmpty(), charged = Hosted.Lanterns.NewEmpty();
        Give(Hosted, empty); Give(Hosted, charged); GiveFirebugs(Hosted, 4);
        Require(Hosted.Lanterns.Refill(Lanterns.Id(charged), count: 4), "four firebugs should fit in the lantern");
        charged = Hosted.Lanterns.Carried(Lanterns.Id(charged))!;
        Require(Hosted.LoadInput(packer, charged, 1) == 1, "the filled lantern should go into the packing station");
        await Until(() => view.Pick(view.Screen(at, .3f))?.Id == packer.Id, "the packing station being visible", 4);
        await Click(view.Screen(at, .3f));
        var slot = OnPage("input_lantern")!;
        var quantity = slot.GetNode<Badge>("count");
        var firebugs = slot.GetNodeOrNull<Badge>("firebug_count");
        Require(!quantity.Visible && firebugs is { Text: "4", Visible: true }
            && firebugs.GetThemeColor("font_color") == Style.FirebugGreen,
            "a single lantern at the packing station should show only its green firebug count");
        Require(Hosted.LoadInput(packer, empty, 1) == 1, "the empty lantern should join the filled lantern");
        await Until(() => quantity.Visible && quantity.Text == "2", "the separate quantity of two lanterns", 4);
        Require(firebugs!.Text == "4" && firebugs.GetRect().GetCenter().Y < quantity.GetRect().GetCenter().Y,
            "the first lantern's firebug count should stay above the lantern quantity");
        await PickUp(slot);
        Require(cursor.Stack?.Item == charged, "the station should hand out the lantern whose firebug count it shows");
        await Until(() => firebugs.Text == "0", "the free second lantern showing once the first is on the cursor", 3);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == packer);
        await Frames();
    }

    private async Task CheckCtrlRefillUsesOlderFirebugsAcrossStacks() {
        float caught = Hosted.Time;
        Require(GiveFirebugs(Hosted, 1) == 1, "an older firebug should fit in the backpack");
        RunUntil(Hosted, caught + Hosted.Data.DaySeconds);
        Hosted.Hold(Hosted.Backpack, SlotOf(Hosted.Backpack, Firebugs.Item), 1);
        Require(GiveFirebugs(Hosted, 1) == 1, "holding the older stack should leave the younger firebug in a separate stack");
        Hosted.Release();
        string lantern = Hosted.Lanterns.FillForReview(Hosted.Lanterns.NewEmpty(), 9);
        Give(Hosted, lantern);
        await KeyStroke(Key.Tab);
        await RightClick(BagCellOf(lantern));
        var young = BagCell(c => c.TooltipText.Contains("Average life left: 71 h") || c.TooltipText.Contains("Average life left: 72 h"));
        await Holding(Key.Ctrl, () => Click(young));
        await Until(() => world.Backpack[Firebugs.Item] == 1 && panel.HeadingTitle.Text == "Lantern"
            && OnPage("lantern_firebug_count") is Label { Text: "10/10" }, "the lantern filling with one bug from the batch", 3);
        string life = BagCellOf(Firebugs.Item).TooltipText;
        Require(life.Contains("Average life left: 71 h") || life.Contains("Average life left: 72 h"),
            "Ctrl-refill should take the oldest eligible firebug and leave the younger one, even when the younger one was clicked");
        await CloseWithEsc();
        Drop(Hosted, Hosted.Lanterns.Carried(Lanterns.Id(lantern))!); Drop(Hosted, Firebugs.Item);
        await Frames();
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
        Require(panel.Page == Page.Lantern && world.Me.LanternInHand == 0 && world.TorchLit,
            "right-clicking a lantern in the backpack should only open its refill page");
        await DoubleClick(BagCellOf(lantern));
        var wear = view.PlayerRig.Lanterns!;
        await Until(() => Lanterns.InHand(world.Me) == lantern && !world.TorchLit && !panel.Visible && !bag.Visible
            && wear.Hand.Light.IsVisibleInTree(), "the lantern in hand replacing the lit torch", 3);
        Require(world.Backpack["torch"] >= torch, "taking the lantern in hand should put out the torch without consuming it");

        string previous = world.HotbarItem(8);
        Give(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, "bug_net");
        await KeyStroke(Key.Key9);
        await ClickGround(MouseButton.Right);
        await Until(() => view.PlayerAnimation == ActorAction.NetSwing && !wear.Hand.Light.IsVisibleInTree()
            && wear.Worn.Any(lamp => lamp?.Light.IsVisibleInTree() == true), "the unbound lantern lighting the way from the belt during the net swing", 3);
        await Until(() => view.PlayerAnimation != ActorAction.NetSwing && wear.Hand.Light.IsVisibleInTree()
            && !wear.Worn.Any(lamp => lamp?.Light.IsVisibleInTree() == true), "the lantern returning to the hand after the net swing", 3);
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCellOf(lantern));
        await Until(() => world.Me.LanternInHand == 0 && !wear.Hand.Light.IsVisibleInTree(), "the second double-click stowing the lantern", 3);

        Drop(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, previous);
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
        await Until(() => world.Lanterns.BugCount(lantern.LanternItem) == 1 && panel.HeadingTitle.Text == "Lantern", "the first firebug filling and renaming the lantern", 4);
        Require(slot.TooltipText.StartsWith("Average life left", System.StringComparison.Ordinal),
            "checking the lantern's firebug stack should show its average remaining life");
        await RightClick(slot);
        await Until(() => world.Lanterns.BugCount(lantern.LanternItem) == 2 && OnPage("lantern_firebug_count") is Label { Text: "2/10" }, "the second firebug updating the count", 4);
        await Click(slot);
        await Until(() => world.Lanterns.BugCount(lantern.LanternItem) == 10 && world.Backpack[Firebugs.Item] == bugsBefore + 2 && OnPage("lantern_firebug_count") is Label { Text: "10/10" },
            "depositing eight more firebugs and leaving the surplus in the backpack", 4);
        await Click(slot);
        Require(world.Lanterns.BugCount(lantern.LanternItem) == 10 && cursor.Stack?.Count == heldBefore - 10, "the full lantern should reject the remaining held firebugs");
        await Click(FindButton(panel, "Pick up")!);
        await Until(() => world.Find(lantern.Id) == null && world.Lanterns.Carried(Lanterns.Id(item)) != null && !panel.Visible,
            "picking up the filled lantern from its ground panel", 4);
        Drop(Hosted, Hosted.Lanterns.Carried(Lanterns.Id(item))!); Drop(Hosted, Firebugs.Item, 2);
        await Frames();
    }
}
