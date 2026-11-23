using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of the goals: the starter chain, the end goals in the pause menu and the ending.</summary>
    private async Task RunGoalUIChecks() {
        await TestClean(CheckEndGoalsShowInThePauseMenuAndTheEndingOnce);
    }

    /// <summary>
    /// The starter goals alone do not end the game, and the pause menu then shows no closing words under them; it lists
    /// the end goals, mines and the profit goal show the ending, and keep playing closes it for good.
    /// </summary>
    private async Task CheckEndGoalsShowInThePauseMenuAndTheEndingOnce() {
        Achieve(Hosted, new[] { "campfire", "hut", "bench", "sold", "furnace", "steel_bar", "steel_hammer", "steel_pick", "steel_parts", "assigned" });
        await Frames(3);
        Require(world.GoalComplete, $"The starter goal chain must be complete before the check that it alone does not end the game, not on goal {world.Goal}.");
        Require(!ending.Visible && !view.Paused, "The starter goal chain alone must not end the game.");
        await KeyStroke(Key.Escape);
        Require(HasLabel(menu, "END GOALS"), "The pause menu must have an end goals heading.");
        Require(!HasLabel(menu, "starter goals are done"), "The pause menu must show no closing text once the starter goals are done.");
        Require(HasLabel(menu, "A working iron mine"), "The pause menu must list the iron mine goal.");
        Require(HasLabel(menu, $"{world.Data.Goals.DailyProfit} coins profit a day"),
            "The pause menu must list the daily profit goal.");
        await KeyStroke(Key.Escape);
        foreach (var ore in Cliff.Ores) OpenMine(Hosted.Add(ThingKind.Mine, Cliff.Of(ore).MineSpot));
        Achieve(Hosted, "profit_week");
        await Frames(3);
        Require(ending.Visible && view.Paused, "Reaching the end goals must show the ending and pause the world.");
        await Click((Button)ending.FindChild("keep_playing", true, false)); await Frames(3);
        Require(!ending.Visible, "Keep playing must close the ending.");
        Require(!view.Paused, "Keep playing must resume the game.");
        Require(world.EndingShown, "Keep playing must remember that the ending was shown.");
        await Frames(3);
        Require(!ending.Visible, "The ending must not reappear after the player chose to keep playing.");
        RemoveThings(Hosted, t => t.Kind == ThingKind.Mine);
    }
}
