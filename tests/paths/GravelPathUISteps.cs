using System.Threading.Tasks;
using Godot;
using static SetupSteps;

namespace ChopChopMeow;

/// <summary>Steps of the UI checks of gravel paths.</summary>
public partial class Game {
    /// <summary>
    /// Tiles a second the player walks with D held, from a standing start at <paramref name="start"/>: measured over
    /// three tiles once they are up to speed.
    /// </summary>
    private async Task<float> WalkingSpeed(Vector2 start) {
        Warp(Hosted, start); await Frames(4);
        float speed = 0;
        await Holding(Key.D, async () => {
            await Until(() => world.Player.DistanceTo(start) > 1, "the player getting up to speed", 2);
            // By the game's own clock, which moves the player: a frame that stalls counts for a tenth of a second there, and for all of it on a real clock.
            var from = world.Player; float began = runTime;
            await Until(() => world.Player.DistanceTo(start) > 4, "the player walking four tiles", 3);
            speed = world.Player.DistanceTo(from) / (runTime - began);
        });
        await Frames();
        return speed;
    }
}
