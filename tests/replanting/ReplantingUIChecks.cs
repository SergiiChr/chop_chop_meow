using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunReplantingUIChecks() {
        await TestClean(CheckHeldAcornMarksGroundAndCanBePlantedAndWatered);
        await TestClean(CheckLeavesBesideAnOldTreeCanStillBeGathered);
    }

    private async Task CheckHeldAcornMarksGroundAndCanBePlantedAndWatered() {
        var home = Hosted.Player;
        string bucket = Bucket.AtFill(5);
        Give(Hosted, "acorn");
        Give(Hosted, "steel_shovel");
        Give(Hosted, bucket);
        await Frames();
        await KeyStroke(Key.Tab);
        var at = SpotsNearby().First(p => world.Construction.PlacementBlock(ThingKind.PlantingSpot, p) == null
            && Uncovered(view.Screen(p)));
        var tree = await PlantAndWater(at, bucket);
        Require(world.Backpack["acorn"] == 0, "planting the tree should spend the acorn");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t.Id == tree.Id);
        Drop(Hosted, "steel_shovel", 1);
        Drop(Hosted, Bucket.Item, 1);
        Warp(Hosted, home);
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
        Require(ShowsTreeStatus("Adult tree") && ShowsTreeWood("336"), "an older tree should show its adult status and wood");
        var woodSlot = OnPage("tree_wood")!;
        int wood = world.Backpack["wood"];
        await Click(woodSlot);
        await RightClick(woodSlot);
        Require(ShowsTreeWood("336") && cursor.Empty && world.Backpack["wood"] == wood,
            "clicking the wood count should leave the tree and backpack as they were");
        await CloseWithEsc();
        await ClickOn(patch);
        Require(panel.Selection.Thing == patch.Id, "the old tree's larger trunk should leave its leaf spot selectable");
        var point = RightPress(patch.Pos, .08f);
        try {
            await Until(() => world.Backpack["acorn"] > acorns && world.Backpack["leaves"] > leaves,
                "gathering leaves and acorns from the spot next to an old tree", 8);
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
