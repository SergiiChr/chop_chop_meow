using System.Threading.Tasks;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private Task RunHudUIChecks() => TestClean(CheckTheCornerShowsTheDayAndTime);

    /// <summary>The right corner shows the clock, leaving the needs, bank and pack elsewhere.</summary>
    private async Task CheckTheCornerShowsTheDayAndTime() {
        await Frames();
        Require(corner.Clock.IsVisibleInTree() && HasLabel(corner.Clock, $"Day {world.Day} · {(int)world.Hour}:"),
            "The corner clock should show the current day and time.");
        foreach (string label in new[] { "Health", "Food", "Water", "coins", "Pack" })
            Require(!HasLabel(corner, label), $"The right corner should leave {label} off the clock display.");
    }
}
