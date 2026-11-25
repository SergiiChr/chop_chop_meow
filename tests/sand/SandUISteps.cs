using System;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Advances several days in the network check while keeping both players fed and watered.</summary>
    private static void RunBeachClock(World world, float time) {
        while (world.Time < time) {
            NourishEveryone(world);
            world.Tick(Math.Min(15, time - world.Time), true);
        }
    }
}
