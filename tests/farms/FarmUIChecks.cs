using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFarmUIChecks() => await TestClean(CheckFarmPageSaysWhenTheWheatRipensAndOnlyThenHarvest);

    private async Task CheckFarmPageSaysWhenTheWheatRipensAndOnlyThenHarvest() {
        var farm = Hosted.Add(ThingKind.Farm, world.Player + new Vector2(-3, 3));
        farm.FarmStage = FarmStage.Growing; farm.Progress = Hosted.Farms.GrowSeconds - 2.5f * world.Data.DaySeconds;
        string bucket = Bucket.AtFill(5);
        Give(Hosted, bucket);
        var nearCorner = farm.Pos + new Vector2(2.5f, 2.5f);
        await Frames();
        await Click(view.Screen(nearCorner, .03f));
        await Until(() => HasLabel(panel, "Needs water"), "a dry plot asking for water", 2);
        var water = (ItemSlot)OnPage($"input_{Water.Liquid}")!;
        var level = (ProgressBar)OnPage("farm_water")!;
        Require(water.Icon == Icons.Of(Water.Liquid) && level.Value == 0, "the farm starts with an empty water bar and droplet slot");
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(bucket))), water);
        await Until(() => level.Value > 4.9 && world.Backpack[Bucket.Item] > 0, "pouring the bucket into the plot fills its water bar", 2);
        Drop(Hosted, Bucket.Item, 1);
        await Until(() => HasLabel(panel, "Growing, 2 days until ripe"), "a growing plot's page saying it ripens in 2 days", 2);
        Require(!HasLabel(panel, "harvest"), "A growing plot's page must not mention harvesting.");
        farm.FarmStage = FarmStage.Ripe; farm.Progress = 0;
        await Until(() => HasLabel(panel, "Ripe") && HasLabel(panel, "harvest"), "a ripe plot's page offering the harvest", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == farm);
    }
}
