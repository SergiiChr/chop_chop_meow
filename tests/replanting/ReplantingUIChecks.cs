using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunReplantingUIChecks() => await TestClean(CheckHeldAcornMarksGroundAndCanBePlantedAndWatered);

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
}
