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
    private static readonly string GuestWater = Bucket.AtFill(5);

    private NetScene ReplantingNetScene => new("replanting", null, HostSeesTheGuestsTreeGrow, GuestPlantsAndWatersATree);

    private async Task HostSeesTheGuestsTreeGrow(PlayerState guest) {
        var before = guest.Pos;
        RemoveThings(Hosted, t => t.Pos.DistanceTo(GuestTree) < 10);
        Warp(guest, GuestDigsFrom);
        Give(guest, "acorn"); Give(guest, "steel_shovel"); Give(guest, GuestWater);
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
        await Until(() => world.Player.DistanceTo(GuestDigsFrom) < .1f && world.Backpack[GuestWater] == 1,
            "the guest reaching the planting site with water");
        await KeyStroke(Key.Tab);
        var tree = await PlantAndWater(GuestTree, GuestWater);
        await Until(() => tree.Remaining == 4, "the guest's window showing a day's tree growth");
        await Until(() => world.Player.DistanceTo(GuestTree) > 8, "the guest back near the clearing");
        await CloseWithEsc();
    }
}
