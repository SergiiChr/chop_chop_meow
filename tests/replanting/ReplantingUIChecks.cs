using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunReplantingUIChecks() {
        await TestClean(CheckDiggingWithAShovelComesBeforePlantingAndWatering);
        await TestClean(CheckHotbarShovelShowsADigOutline);
        await TestClean(CheckLeavesBesideAnOldTreeCanStillBeGathered);
    }

    private async Task CheckDiggingWithAShovelComesBeforePlantingAndWatering() {
        var home = Hosted.Player;
        string bucket = Bucket.AtFill(5);
        Give(Hosted, "steel_shovel");
        Give(Hosted, bucket);
        await Frames();
        await KeyStroke(Key.Tab);
        var at = SpotsNearby().First(p => world.Construction.PlacementBlock(ThingKind.PlantingSpot, p) == null
            && Uncovered(view.Screen(p)));
        await DoubleClick(BagCell(c => c.TooltipText.StartsWith("Steel shovel")));
        await Click(view.Screen(at));
        Require(world.ThingsOf(ThingKind.PlantingSpot).Count == 0, "left-clicking open ground should leave no marker");
        await StopBeforeFinishingTheHole(at);
        var hole = await DigHole(at);
        Require(world.Backpack["acorn"] == 0 && hole.Inventory.Count == 0, "digging should leave an empty hole without any acorn");
        Give(Hosted, "acorn");
        await Frames();
        var tree = await PlantAndWater(hole, bucket);
        Require(world.Backpack["acorn"] == 0, "planting the tree should spend the acorn");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t.Id == tree.Id);
        Drop(Hosted, "steel_shovel", 1);
        Drop(Hosted, Bucket.Item, 1);
        Warp(Hosted, home);
        await Frames();
    }

    private async Task CheckHotbarShovelShowsADigOutline() {
        Give(Hosted, "steel_shovel");
        SetHotbarSlot(Hosted, 8, "steel_shovel");
        SetHotbarSlot(world, 8, "steel_shovel");
        await Frames();
        await KeyStroke(Key.Key9);
        var at = SpotsNearby().First(p => world.Construction.PlacementBlock(ThingKind.PlantingSpot, p) == null
            && Uncovered(view.Screen(p)));
        await Hover(view.Screen(at));
        Require(view.PlacementGhost != null, "selecting the hotbar shovel should show the dig outline");
        await KeyStroke(Key.Escape);
        Require(view.PlacementGhost == null, "escaping should put away the outline");
        SetHotbarSlot(Hosted, 8, "");
        SetHotbarSlot(world, 8, "");
        Drop(Hosted, "steel_shovel");
        await Frames();
    }

    private async Task CheckLeavesBesideAnOldTreeCanStillBeGathered() {
        var home = Hosted.Player;
        var at = SpotsNearby().First(p => world.Construction.PlacementBlock(ThingKind.PlantingSpot, p) == null
            && Uncovered(view.Screen(p + World.LeafOffset)));
        var tree = Hosted.Add(ThingKind.Tree, at, 336);
        tree.TreeStage = 81;
        var patch = Hosted.Add(ThingKind.Leaves, at + World.LeafOffset, 12);
        int acorns = world.Backpack["acorn"], leaves = world.Backpack["leaves"];
        Give(Hosted, "rake");
        await Frames();
        await ClickOn(tree);
        Require(ShowsTreeStatus("Adult tree") && ShowsResourceStock("wood", "336"), "an older tree should show its adult status and wood");
        var woodSlot = OnPage("resource_wood")!;
        int wood = world.Backpack["wood"];
        await Click(woodSlot);
        await RightClick(woodSlot);
        Require(ShowsResourceStock("wood", "336") && cursor.Empty && world.Backpack["wood"] == wood,
            "clicking the wood count should leave the tree and backpack as they were");
        await CloseWithEsc();
        await ClickOn(patch);
        Require(panel.Selection.Thing == patch.Id, "the old tree's larger trunk should leave its leaf spot selectable");
        Require(ShowsResourceStock("leaves", "12") && ShowsResourceStock("acorn", LeafLitter.Acorns(patch).ToString()),
            "a leaf spot should show separate counts for leaves and acorns");
        var point = RightPress(patch.Pos, .08f);
        try {
            await Until(() => world.Backpack["acorn"] > acorns && world.Backpack["leaves"] > leaves,
                "gathering leaves and acorns from the spot next to an old tree", 8);
            await Until(() => ShowsResourceStock("acorn", "0"), "the acorn count reaching zero", 2);
        } finally { RightRelease(point); }
        await CloseWithEsc();
        Drop(Hosted, "rake", 1);
        Drop(Hosted, "acorn", Hosted.Backpack["acorn"] - acorns);
        Drop(Hosted, "leaves", Hosted.Backpack["leaves"] - leaves);
        // Remove the old tree out of sight so the next check does not inherit its long falling animation.
        Warp(Hosted, home + Vector2.One * 24);
        await Frames(2);
        RemoveThings(Hosted, t => t.Id == tree.Id || t.Id == patch.Id);
        Warp(Hosted, home);
        await Frames();
    }
}
