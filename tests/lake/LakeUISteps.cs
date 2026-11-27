using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

/// <summary>Steps of the UI checks of the lake.</summary>
public partial class Game {
    /// <summary>
    /// A wolf pins the player at a spot until they lie on their back, which gives how high the player's rig and the wolf's model
    /// are drawn and where the wolf stands; then the wolf goes and the player is let up.
    /// </summary>
    private async Task<(float Player, float Wolf, Vector2 WolfAt)> PinnedAt(Vector2 at) {
        Warp(Hosted, at); await Frames(4);
        SetMauled(Hosted, true);
        var pinning = PlaceWolf(Hosted, new Wolf { Pos = at + Vector2.Right * .8f, Feeding = true, Pinning = true });
        await Seconds(ActorAnimation.FallBackSeconds + .3f);
        Require(view.PlayerAnimation == ActorAction.Mauled, $"A pinned player must be down on their back: {view.PlayerAnimation}.");
        var shown = world.Wolves.Where(w => w.Pinning).Select(view.ModelOf).SingleOrDefault();
        Require(shown != null, "The window must draw the one wolf pinning the player.");
        var player = view.PlayerRig.Root.Position;
        var wolf = shown!.Root.Position;
        RemoveWolves(Hosted, w => w == pinning);
        SetMauled(Hosted, false); await Frames(4);
        return (player.Y, wolf.Y, new Vector2(wolf.X, wolf.Z));
    }
}
