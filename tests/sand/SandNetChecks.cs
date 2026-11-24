using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SandSteps;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private NetScene SandNetScene => new("sand", null, HostSeesTheGuestsSandRefill, GuestDigsBeachSand);

    private async Task HostSeesTheGuestsSandRefill(PlayerState guest) {
        var home = guest.Pos;
        float cycle = Hosted.Data.DaySeconds * Hosted.Data.MoonCycleDays;
        float low = (MathF.Floor(Hosted.Time / cycle) + .5f) * cycle;
        if (low <= Hosted.Time) low += cycle;
        RunBeachClock(Hosted, low);
        RemoveThings(Hosted, t => t.Pos.DistanceTo(SandSquare) < 4);
        if (guest.Backpack["steel_shovel"] == 0) Give(guest, "steel_shovel");
        Warp(guest, SandSquare + Vector2.Left);
        var tile = Terrain.TileAt(SandSquare);
        await Until(() => world.Beach.Stripped(tile), "the host's window showing the square the guest stripped");
        Require(guest.Backpack["sand"] == 10 && Hosted.Backpack["sand"] == 0,
            "digging should give ten sand only to the guest");
        await Signaled("guest-saw-stripped-sand");
        RunBeachClock(Hosted, low + cycle / 2);
        await Until(() => !world.Beach.Stripped(tile), "the host's window seeing the high tide refill the square");
        await Signaled("guest-saw-refilled-sand");
        Warp(guest, home);
    }

    private async Task GuestDigsBeachSand() {
        await CloseWithEsc();
        var stand = SandSquare + Vector2.Left;
        await Until(() => world.Player.DistanceTo(stand) < .1f && world.Backpack["steel_shovel"] > 0,
            "the guest reaching the beach with a shovel");
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCell(c => c.TooltipText.StartsWith("Steel shovel")));
        var tile = Terrain.TileAt(SandSquare);
        await Hover(view.Screen(SandSquare + new Vector2(.2f, -.15f)));
        Require(view.SandTarget == tile && view.PlacementGhost == null,
            "the shovel should highlight the pointed sand square without showing a hole outline");
        var point = RightPress(SandSquare, .025f);
        try {
            await Until(() => world.Beach.Stripped(tile) && world.Backpack["sand"] == 10,
                "the guest receiving sand and seeing the square stripped", 14);
        } finally { RightRelease(point); }
        Require(world.ThingsOf(ThingKind.Hole).All(t => t.Pos != SandSquare), "the dig should leave no planting hole");
        Signal("guest-saw-stripped-sand");
        await Until(() => !world.Beach.Stripped(tile), "the guest seeing high tide refill the stripped square");
        Signal("guest-saw-refilled-sand");
        await Until(() => world.Player.DistanceTo(stand) > 10, "the guest returning from the beach");
    }
}
