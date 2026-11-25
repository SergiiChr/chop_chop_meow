using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunStrawBedUIChecks() {
        await TestClean(CheckWASDWakesAStrawBedSleeper);
    }

    private async Task CheckWASDWakesAStrawBedSleeper() {
        await CloseWithEsc();
        RunUntilMorning(Hosted);
        var home = Hosted.Player;
        var bed = Hosted.Add(ThingKind.StrawBed, Terrain.Spawn + new Vector2(-4.4f, 3.8f));
        Warp(Hosted, bed.Pos + Vector2.Right * 1.5f);
        await Until(() => view.Pick(view.Screen(bed.Pos, .06f))?.Id == bed.Id, "the straw bed within reach of the pointer", 2);
        await Click(view.Screen(bed.Pos, .06f));
        foreach (Key key in new[] { Key.W, Key.A, Key.S, Key.D }) {
            await Until(() => Named("sleep") is Button { Text: "Sleep" }, "the straw bed's Sleep button", 2);
            await Click(Named("sleep")!);
            await Until(() => world.Sleep.Place == bed.Id && Named("sleep") is Button { Text: "Wake" }, "sleeping on the straw bed", 2);
            await Seconds(.3);
            await KeyStroke(key);
            await Until(() => Hosted.Sleep.Place == 0 && world.Sleep.Place == 0, $"{key} waking the straw-bed sleeper", 2);
            Require(world.Player.DistanceTo(bed.Pos) < .1f, "A tap to wake should leave the player at the bed to finish rising.");
        }
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == bed);
        Warp(Hosted, home);
        await Frames(4);
    }
}
