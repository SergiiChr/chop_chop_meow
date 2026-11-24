using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Checks goal discovery in a fresh game, then completion and the ending after the other progression checks.</summary>
    private Task RunGoalUIChecks(bool freshGame) => freshGame
        ? TestClean(CheckFreshGoalLogHidesUnknownGoalsAndLinksItems)
        : TestClean(CheckEndGoalsShowInThePauseMenuAndTheEndingOnce);

    private async Task CheckFreshGoalLogHidesUnknownGoalsAndLinksItems() {
        await KeyStroke(Key.Escape);
        string[] available = ["Light in the dark", "Safe place", "A start", "Stone and embers", "More hands on deck", "NEET"];
        foreach (string title in available)
            Require(FindButton(menu, title) is { } row && row.IsVisibleInTree(), $"The new settlement should show {title}.");
        string[] undiscovered = ["Road to riches", "Hotter than fire", "Steel tools", "Too soft", "Infinite power"];
        foreach (string title in undiscovered)
            Require(FindButton(menu, title) == null, $"The new settlement should hide {title}.");
        Require(HasLabel(menu, "Explore to unlock more goals."), "The log should prompt players to explore while goals are hidden.");

        await Click(FindButton(menu, "NEET")!);
        Require(menu.GoalDetails.Visible && HasLabel(menu.GoalDetails, "Earn at least 1,300 coins in profit each day for 7 days in a row."),
            "Clicking NEET should open the full profit description.");
        int revenue = Hosted.DayRevenue;
        SetDayRevenue(Hosted, 125);
        await Until(() => HasLabel(menu.GoalDetails, "Today's sales 125"), "sales updating in the open goal", 2);
        SetDayRevenue(Hosted, revenue);
        var coins = (Button)menu.GoalDetails.FindChild("goal_item_coins", true, false);
        Require(coins.Icon != null && coins.TooltipText == "Coins", "Coins should be a hoverable icon with the item name.");
        await Click(coins);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "coins" && recipeMap.NodeOf("coins")!.Lit,
            "the goal icon opening the map with coins highlighted", 2);
        var rows = (ScrollContainer)recipeMap.FindChild("map_rows", true, false);
        Require(rows.GetGlobalRect().Encloses(recipeMap.NodeOf("coins")!.GetGlobalRect()),
            "The selected item should be scrolled into view.");
        await KeyStroke(Key.Escape);
        Require(!recipeMap.Visible && menu.GoalDetails.Visible && view.Paused, "Closing the map should return to the paused goal details.");
        await KeyStroke(Key.Escape);
        await KeyStroke(Key.Escape);
    }

    /// <summary>Building the mines and meeting the profit target shows the ending once, with a choice to keep playing.</summary>
    private async Task CheckEndGoalsShowInThePauseMenuAndTheEndingOnce() {
        Achieve(Hosted, new[] { "campfire", "hut", "bench", "sold", "furnace", "steel_bar", "steel_hammer", "steel_pick", "steel_parts", "assigned" });
        await Frames(3);
        Require(!ending.Visible && !view.Paused, "Completing the starter goals must leave the game running.");
        await KeyStroke(Key.Escape);
        var completed = (Button)menu.FindChild("goal_completed", true, false);
        Require(FindButton(menu, "A start") is { } bench && !bench.IsVisibleInTree(), "The Completed section must start collapsed.");
        await Click(completed);
        Require(FindButton(menu, "A start")!.IsVisibleInTree(), "Expanding Completed must show completed goal titles.");
        await Click(completed);
        Require(!FindButton(menu, "A start")!.IsVisibleInTree(), "Collapsing Completed must hide its goal titles again.");
        await Click(FindButton(menu, "Infinite power")!);
        Require(menu.GoalDetails.Visible && HasLabel(menu.GoalDetails, "Build an iron mine.") &&
            HasLabel(menu.GoalDetails, "Build a coal mine.") && HasLabel(menu.GoalDetails, "Build a tin mine."),
            "Infinite power must open its details with all three mines.");
        var ironMark = (Label)menu.GoalDetails.FindChild("goal_mine_iron", true, false);
        var coalMark = (Label)menu.GoalDetails.FindChild("goal_mine_coal", true, false);
        Require(ironMark.Text == "○", "An unopened iron mine must be unchecked.");
        OpenMine(Hosted.Add(ThingKind.Mine, Cliff.Of(ThingKind.Iron).MineSpot));
        await Until(() => ironMark.Text == "✓", "the iron mine's completion showing in the goal", 2);
        Require(coalMark.Text == "○", "Opening an iron mine must leave the coal mine unchecked.");
        await KeyStroke(Key.Escape);
        Require(menu.Visible && !menu.GoalDetails.Visible, "Escape from goal details must go back to the goal log.");
        await KeyStroke(Key.Escape);
        foreach (var ore in new[] { ThingKind.Coal, ThingKind.Tin }) OpenMine(Hosted.Add(ThingKind.Mine, Cliff.Of(ore).MineSpot));
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
