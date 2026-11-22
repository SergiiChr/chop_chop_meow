using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFarmUIChecks() {
        await TestClean(CheckInspectKeyOpensThePlotFromACorner);
        await TestClean(CheckFarmPageSaysWhenTheWheatRipensAndOnlyThenHarvest);
    }

    private async Task CheckInspectKeyOpensThePlotFromACorner() {
        var farm = Hosted.Add(ThingKind.Farm, world.Player + Vector2.One * Farms.HalfSize);
        await Frames();
        await KeyStroke(Key.E);
        Require(panel.Selection.Thing == farm.Id, "E should open the farm when the player stands at its corner");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == farm);
    }

    /// <summary>A growing plot's page says how long until the wheat is ripe without a word of harvesting, which it offers once ripe.</summary>
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
        await Frames();
        var seed = OnPage("input_wheat_seeds")!.GetGlobalRect();
        var fertilizer = OnPage("input_fertilizer")!.GetGlobalRect();
        var waterSlot = water.GetGlobalRect();
        Require(seed.Position.X == fertilizer.Position.X && fertilizer.Position.X == waterSlot.Position.X
            && seed.End.Y <= fertilizer.Position.Y && fertilizer.End.Y <= waterSlot.Position.Y,
            "seeds, fertilizer and water should line up in a vertical column");
        var wheat = OnPage("output_wheat")!.GetGlobalRect();
        var returnedSeed = OnPage("output_wheat_seeds")!.GetGlobalRect();
        var grass = OnPage("output_grass")!.GetGlobalRect();
        Require(wheat.Position.Y == returnedSeed.Position.Y && returnedSeed.Position.Y == grass.Position.Y
            && wheat.End.X <= returnedSeed.Position.X && returnedSeed.End.X <= grass.Position.X,
            "wheat, seeds and grass should line up in a horizontal row");
        Require(wheat.Size.X == wheat.Size.Y && wheat.Size == returnedSeed.Size && returnedSeed.Size == grass.Size,
            "every harvest output should have the same square slot size");
        Require(water.Icon == Icons.Of(Water.Liquid) && level.Value == 0, "the farm starts with an empty water bar and droplet slot");
        var earth = view.PropOf(farm.Id)!.Root.FindChild("TilledSoil", true, false) as MeshInstance3D;
        Require(earth != null && earth.MaterialOverride.NextPass == null, "The dry tilled rows should show their original earth.");
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(bucket))), water);
        await Until(() => level.Value > 4.9 && world.Backpack[Bucket.Item] > 0, "pouring the bucket into the plot fills its water bar", 2);
        await Until(() => earth!.MaterialOverride.NextPass is ShaderMaterial wet
            && Mathf.IsEqualApprox(wet.GetShaderParameter("water_fraction").AsSingle(), .5f),
            "five liters spreading wet soil patches across half the plot", 2);
        Drop(Hosted, Bucket.Item, 1);
        await Until(() => HasLabel(panel, "Growing, 2 days until ripe"), "a growing plot's page saying it ripens in 2 days", 2);
        Require(!HasLabel(panel, "harvest"), "A growing plot's page must not mention harvesting.");
        farm.FarmStage = FarmStage.Ripe; farm.Progress = 0;
        await Until(() => HasLabel(panel, "Ripe") && HasLabel(panel, "harvest"), "a ripe plot's page offering the harvest", 2);
        var farCrop = view.Screen(farm.Pos - new Vector2(2.3f, 2.3f), .8f);
        Require(view.Pick(farCrop)?.Id == farm.Id, "clicks on the far corner's ripe wheat should reach the plot");
        Require(OnPage($"input_{Water.Liquid}")?.IsVisibleInTree() != true && OnPage("farm_water")?.IsVisibleInTree() != true,
            "a ripe crop should hide both its water slot and its fill bar");
        farm.FarmStage = FarmStage.Fallow;
        await Until(() => HasLabel(panel, "Sows once") && OnPage($"input_{Water.Liquid}")?.IsVisibleInTree() == true
            && OnPage("farm_water")?.IsVisibleInTree() == true, "the water controls returning for the next crop", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == farm);
    }
}
