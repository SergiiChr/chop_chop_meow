using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The network scene of goal popups: the host lights a campfire goal before the guest joins, and the guest's window
    /// pops up none of the goals the game already had.
    /// </summary>
    private NetScene GoalNetScene => new("goals", () => Achieve(Hosted, "campfire"), _ => Task.CompletedTask, GuestSeesNoPopupForGoalsTheGameHad);

    private async Task GuestSeesNoPopupForGoalsTheGameHad() {
        Require(world.Goals.Status(GoalId.Campfire) == GoalStatus.Completed, "Joining must bring over the host's completed campfire goal.");
        // Longer than the popup's own refresh, so a popup for what joining brought over would have started.
        await Seconds(.5);
        Require(!goalPopup.Visible && !sounds.Heard("goal_completed", 0) && !sounds.Heard("goal_new", 0),
            $"Joining must pop up none of the goals the game had, not \"{goalPopup.Heading} {goalPopup.Title}\".");
    }
}
