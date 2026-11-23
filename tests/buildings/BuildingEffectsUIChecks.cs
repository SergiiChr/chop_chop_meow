using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private Task RunBuildingEffectsUIChecks() => TestClean(CheckQuarterTurnedForgeEmitsFromItsFlueAndMouth);

    private async Task CheckQuarterTurnedForgeEmitsFromItsFlueAndMouth() {
        var home = world.Player;
        var at = SpotsNearby().First(p => world.CanStand(p) && Uncovered(view.Screen(p, .5f)) && view.Pick(view.Screen(p, .5f)) == null);
        var forge = Hosted.Add(ThingKind.Reinforced, at, rotation: Mathf.Pi / 2);
        forge.Heat = 65;
        await Frames();
        var pool = view.FindChild("Effects", true, false)!.GetChildren().OfType<MeshInstance3D>().ToArray();
        var flue = WorldView.To3(at + new Vector2(.2f, 0), 1.95f);
        await Until(() => pool.Any(p => p.Visible && p.Position.DistanceTo(flue) < .09f),
            "smoke rising from the quarter-turned forge's flue", 2);

        Warp(Hosted, forge.ToWorld(World.BellowsStand.Normalized() * 1.6f));
        await ClickOn(forge);
        int heard = sounds.Played;
        await KeyStroke(Key.Space);
        var mouth = WorldView.To3(at - new Vector2(.5f, 0), .9f);
        await Until(() => sounds.Heard("bellows_pump", heard) && pool.Any(p => p.Visible && p.Position.DistanceTo(mouth) < .18f),
            "embers spilling from the quarter-turned forge's mouth", 2);
        RemoveThings(Hosted, t => t == forge);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames();
    }
}
