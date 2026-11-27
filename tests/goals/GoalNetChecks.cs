using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The network scene of goal popups: the host reaches the campfire goal before the guest joins, and the guest's window
    /// pops up none of the goals the game already had.
    /// </summary>
    private NetScene GoalNetScene => new("goals", () => Achieve(Hosted, "campfire"), _ => Task.CompletedTask, GuestSeesNoPopupForGoalsTheGameHad);

    private async Task GuestSeesNoPopupForGoalsTheGameHad() {
        Require(world.Goals.Status(GoalId.Campfire) == GoalStatus.Completed, "Joining must bring over the host's completed campfire goal.");
        // The scene starts as soon as the guest has joined: longer than the popup's own refresh, so a popup for what
        // joining brought over would have started.
        await Seconds(.5);
        // The guest's window shows no game before it joins, so any popup it started came with joining.
        Require(goalPopup.Shown == 0, $"Joining must pop up none of the goals the game had, not \"{goalPopup.Heading} {goalPopup.Title}\".");
    }
}
