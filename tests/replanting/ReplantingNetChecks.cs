using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private static readonly Vector2 GuestTree = Terrain.Spawn + new Vector2(14, 14);
    private static readonly Vector2 GuestDigsFrom = GuestTree + new Vector2(2, 0);
    /// <summary>The liters of water in the bucket the guest waters their tree with.</summary>
    private const int GuestWater = 5;

    private NetScene ReplantingNetScene => new("replanting", null, HostSeesTheGuestsTreeGrow, GuestPlantsAndWatersATree);

    private async Task HostSeesTheGuestsTreeGrow(PlayerState guest) {
        var before = guest.Pos;
        RemoveThings(Hosted, t => t.Pos.DistanceTo(GuestTree) < 10);
        Warp(guest, GuestDigsFrom);
        Give(guest, "acorn"); Give(guest, "steel_shovel"); GiveVessel(Hosted, guest, Bucket.Item, Water.Liquid, GuestWater);
        Thing? tree = null;
        await Until(() => {
            tree = Hosted.ThingsOf(ThingKind.Tree).FirstOrDefault(t => t.Pos == GuestTree);
            return tree is { NextTreeStageAt: > 0 };
        }, "the server receiving the guest's planting and watering");
        Require(guest.Backpack["acorn"] == 0 && guest.Backpack[Bucket.Item] > 0 && Hosted.Backpack["acorn"] == 0,
            "planting should use the guest's acorn and water");
        float nextStage = tree!.NextTreeStageAt;
        while (Hosted.Time < nextStage) {
            NourishEveryone(Hosted);
            Hosted.Tick(Math.Min(10, nextStage - Hosted.Time), true);
        }
        await Until(() => world.Find(tree.Id)?.Remaining == 4, "the host's window showing the guest's tree grow");
        Warp(guest, before);
    }

    private async Task GuestPlantsAndWatersATree() {
        await CloseWithEsc();
        int previousSlot = world.ActiveHotbar;
        string previousTool = world.InHand;
        await Until(() => world.Player.DistanceTo(GuestDigsFrom) < .1f && Vessels.Count(world.Backpack, Bucket.Item, Water.Liquid, GuestWater) == 1,
            "the guest reaching the planting site with water");
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCell(c => c.TooltipText.StartsWith("Steel shovel")));
        var hole = await DigHole(GuestTree);
        var tree = await PlantAndWater(hole, Vessels.First(world.Backpack, Bucket.Item, Water.Liquid, GuestWater)!);
        await Until(() => tree.Remaining == 4 && ShowsResourceStock("wood", "4") && ShowsTreeStatus("Growing"),
            "the guest's tree panel updating its wood count after a day of growth");
        await Until(() => world.Player.DistanceTo(GuestTree) > 8, "the guest back near the clearing");
        world.SelectHotbar(previousSlot);
        if (previousTool != "") world.SelectInHand(previousTool);
        await CloseWithEsc();
    }
}
