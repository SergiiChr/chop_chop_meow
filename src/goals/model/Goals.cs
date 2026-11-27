using System;
using System.Linq;

namespace ChopChopMeow;

public enum GoalId { Campfire, Cabin, Bench, Furnace, Worker, Profit, Trade, SteelBar, SteelTools, SteelParts, Mines, Diary }

public enum GoalStatus { Hidden, Active, Completed }

public sealed record Goal(GoalId Id, string Title, string Description, params string[] Items);

/// <summary>The settlement's goal log, read from what its players have seen, built, made and traded; the diary's goal is each player's own.</summary>
public sealed class Goals(World world) {
    internal const string OreSeen = "ore_seen";

    private static readonly Goal[] entries = [
        new(GoalId.Campfire, "Light in the dark", "Build and light a campfire. Fire keeps wolves back until five or more are near."),
        new(GoalId.Cabin, "Safe place", "Build a cabin for a peace of mind."),
        new(GoalId.Bench, "A start", "Build a crafting bench."),
        new(GoalId.Furnace, "Stone and embers", "Build a stone furnace."),
        new(GoalId.Worker, "More hands on deck", "Hire a worker at the trading post and assign a job."),
        new(GoalId.Profit, "NEET", "", Inventory.Coins),
        new(GoalId.Trade, "Road to riches", "Build a trading post and sell a crate of goods.", "crate"),
        new(GoalId.SteelBar, "Hotter than fire", "Build a reinforced furnace and produce your first steel bar.", "steel_bar"),
        new(GoalId.SteelTools, "Steel tools", "Purchase a hammer and a pickaxe from the trading post.", "steel_hammer", "steel_pick"),
        new(GoalId.SteelParts, "Too soft", "Build an anvil and produce your first steel parts.", "steel_parts"),
        new(GoalId.Mines, "Infinite power", "", Cliff.Ores.Select(World.ResourceItem).ToArray()),
        new(GoalId.Diary, "A success story", "Find all the diary pages."),
    ];
    public static ReadOnlySpan<Goal> Entries => entries;

    /// <summary>A hidden goal stays out of the log until its trigger has been reached.</summary>
    public GoalStatus Status(GoalId id) {
        bool unlocked = id switch {
            GoalId.Trade => world.Reached("bench"),
            GoalId.SteelBar => world.Reached("furnace"),
            GoalId.SteelTools => world.Reached(OreSeen),
            GoalId.SteelParts => world.HadItself("steel_bar"),
            GoalId.Mines => Status(GoalId.SteelParts) == GoalStatus.Completed,
            GoalId.Diary => Diary.Started(world.Me),
            _ => true,
        };
        if (!unlocked) return GoalStatus.Hidden;
        bool completed = id switch {
            GoalId.Campfire => world.Reached("campfire"),
            GoalId.Cabin => world.Reached("hut"),
            GoalId.Bench => world.Reached("bench"),
            GoalId.Furnace => world.Reached("furnace"),
            GoalId.Worker => world.Reached("assigned"),
            GoalId.Profit => world.ProfitGoalMet,
            GoalId.Trade => world.Reached("sold"),
            GoalId.SteelBar => world.Reached("steel_bar"),
            GoalId.SteelTools => world.HadItself("steel_hammer") && world.HadItself("steel_pick"),
            GoalId.SteelParts => world.Reached("steel_parts"),
            GoalId.Mines => MinesComplete,
            GoalId.Diary => Diary.FoundDays(world.Me) == DiaryDay.All.Length,
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };
        return completed ? GoalStatus.Completed : GoalStatus.Active;
    }

    /// <summary>Every kind of ore in the cliffs has an open mine.</summary>
    public bool MinesComplete {
        get {
            foreach (var ore in Cliff.Ores) if (!world.Mines.Working(ore)) return false;
            return true;
        }
    }

    public string Description(Goal goal) {
        if (goal.Id != GoalId.Profit) return goal.Description;
        var target = world.Data.Goals;
        return FormattableString.Invariant(
            $"Earn at least {target.DailyProfit:#,0} coins in profit each day for {target.ProfitDays} days in a row.\nProfit is the sale coins minus the wages for that day.");
    }
}
