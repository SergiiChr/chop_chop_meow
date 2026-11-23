using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Marks a planting spot with a held acorn, digs it and pours a bucket into the resulting tree.</summary>
    private async Task<Thing> PlantAndWater(Vector2 at, string bucket) {
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Acorn")));
        var screen = view.Screen(at);
        await Hover(screen);
        Require(view.PlacementGhost != null, "holding an acorn over open ground should show a placement outline");
        await Click(screen);
        await Until(() => cursor.Empty && world.ThingsOf(ThingKind.PlantingSpot).Any(t => t.Pos == at), "the unplanted outline appearing");
        Require(world.Backpack["acorn"] == 1, "putting down the outline should keep the acorn");
        var point = RightPress(at, .025f);
        try {
            await Until(() => view.PlayerAnimation == ActorAction.Dig, "the shovel digging at the outline", 6);
            await Until(() => world.ThingsOf(ThingKind.Tree).Any(t => t.Pos == at), "the outline turning into a planted acorn", 10);
        } finally { RightRelease(point); }
        await Frames();
        var tree = world.ThingsOf(ThingKind.Tree).First(t => t.Pos == at);
        await ClickOn(tree);
        await Until(() => HasLabel(panel, "Needs 5 L"), "the planted tree asking for five liters");
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(bucket))), OnPage($"input_{Water.Liquid}")!);
        await Until(() => HasLabel(panel, "Growth") && world.Backpack[Bucket.Item] > 0, "the tree starting to grow after the bucket is poured");
        return tree;
    }
}
