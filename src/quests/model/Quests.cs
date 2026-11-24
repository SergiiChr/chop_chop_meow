using System;

namespace ChopChopMeow;

public enum QuestId { Campfire, Cabin, Bench, Furnace, Worker, Profit, Trade, SteelBar, SteelTools, SteelParts, Mines }

public enum QuestStatus { Hidden, Active, Completed }

public sealed record Quest(QuestId Id, string Title, string Description, params string[] Items);

/// <summary>The settlement's quest log, read from what its players have seen, built, made and traded.</summary>
public sealed class Quests(World world) {
    internal const string OreSeen = "ore_seen";

    private static readonly Quest[] entries = [
        new(QuestId.Campfire, "Light in the dark", "Build and light a campfire. Fire keeps wolves away."),
        new(QuestId.Cabin, "Safe place", "Build a cabin for a peace of mind."),
        new(QuestId.Bench, "A start", "Build a crafting bench."),
        new(QuestId.Furnace, "Stone and embers", "Build a stone furnace."),
        new(QuestId.Worker, "More hands on deck", "Hire a worker at the trading post and assign a job."),
        new(QuestId.Profit, "NEET", "Achieve seven days of profit.", Inventory.Coins),
        new(QuestId.Trade, "Road to riches", "Build a trading post and sell a crate of goods.", "crate"),
        new(QuestId.SteelBar, "Hotter than fire", "Build a reinforced furnace and produce your first steel bar.", "steel_bar"),
        new(QuestId.SteelTools, "Steel tools", "Purchase a hammer and a pickaxe from the trading post.", "steel_hammer", "steel_pick"),
        new(QuestId.SteelParts, "Too soft", "Build an anvil and produce your first steel parts.", "steel_parts"),
        new(QuestId.Mines, "Infinite power", "• Build an iron mine.\n• Build a coal mine.\n• Build a tin mine.", "iron_ore", "coal", "tin_ore"),
    ];
    public static ReadOnlySpan<Quest> Entries => entries;

    /// <summary>A hidden quest stays out of the log until its trigger has been reached.</summary>
    public QuestStatus Status(QuestId id) {
        bool unlocked = id switch {
            QuestId.Trade => world.Reached("bench"),
            QuestId.SteelBar => world.Reached("furnace"),
            QuestId.SteelTools => world.Reached(OreSeen),
            QuestId.SteelParts => world.HadItself("steel_bar"),
            QuestId.Mines => Status(QuestId.SteelParts) == QuestStatus.Completed,
            _ => true,
        };
        if (!unlocked) return QuestStatus.Hidden;
        bool completed = id switch {
            QuestId.Campfire => world.Reached("campfire"),
            QuestId.Cabin => world.Reached("hut"),
            QuestId.Bench => world.Reached("bench"),
            QuestId.Furnace => world.Reached("furnace"),
            QuestId.Worker => world.Reached("assigned"),
            QuestId.Profit => world.ProfitGoalMet,
            QuestId.Trade => world.Reached("sold"),
            QuestId.SteelBar => world.Reached("steel_bar"),
            QuestId.SteelTools => world.HadItself("steel_hammer") && world.HadItself("steel_pick"),
            QuestId.SteelParts => world.Reached("steel_parts"),
            QuestId.Mines => MinesComplete,
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };
        return completed ? QuestStatus.Completed : QuestStatus.Active;
    }

    /// <summary>Every kind of ore in the cliffs has an open mine.</summary>
    public bool MinesComplete {
        get {
            foreach (var ore in Cliff.Ores) if (!world.Mines.Working(ore)) return false;
            return true;
        }
    }

    public string Description(Quest quest) {
        if (quest.Id != QuestId.Profit) return quest.Description;
        var target = world.Data.Goals;
        return FormattableString.Invariant(
            $"{quest.Description}\nEarn at least {target.DailyProfit:#,0} coins in profit each day for {target.ProfitDays} days in a row.\nProfit is the sale coins minus the wages for that day.");
    }
}
