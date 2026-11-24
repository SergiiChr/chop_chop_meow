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
        await Until(() => ShowsTreeStatus("Planted") && HasLabel(panel, "Needs 5 L") && ShowsTreeWood("0"),
            "the planted tree showing its status, wood count, and water needed", 4);
        Require(!HasLabel(panel, "Pour from a filled bottle or bucket"), "the water amount should be enough text beside the slot");
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(bucket))), OnPage($"input_{Water.Liquid}")!);
        await Until(() => ShowsTreeStatus("Growing") && world.Backpack[Bucket.Item] > 0
            && OnPage($"input_{Water.Liquid}") == null && !bag.Visible,
            "the tree showing Growing and closing its watering controls after the bucket is poured", 4);
        Require(!HasLabel(panel, "/10 days"), "the tree panel should leave out the day count");
        return tree;
    }

    private bool ShowsTreeWood(string amount) => OnPage("tree_wood") is ItemSlot { Visible: true } slot
        && slot.Icon == Icons.Of("wood")
        && slot.GetChildren().OfType<Badge>().Any(badge => badge.IsVisibleInTree() && badge.Text == amount);

    private bool ShowsTreeStatus(string status) => panel.FindChildren("*", "Label", true, false).OfType<Label>()
        .Any(label => label.IsVisibleInTree() && label.Text == status);
}
