using System;
using System.Collections.Generic;
using System.Text;

namespace ChopChopMeow;

/// <summary>How panels word durations and costs; each writes into a reused buffer, so live text can use it.</summary>
public static class PageText {
    /// <summary>Seconds as a short duration: seconds up to a minute and a half, then minutes, then hours.</summary>
    public static StringBuilder AppendDuration(StringBuilder text, float seconds) {
        if (seconds < 90) return text.Append((int)MathF.Ceiling(seconds)).Append(" s");
        if (seconds < 5400) return text.Append((int)MathF.Round(seconds / 60)).Append(" min");
        return text.Append((int)MathF.Round(seconds / 3600)).Append(" h");
    }

    /// <summary>Burn time as in-game hours and minutes.</summary>
    public static StringBuilder AppendGameHours(World world, StringBuilder text, float seconds) {
        int minutes = world.Data.GameMinutes(seconds);
        return text.Append(minutes / 60).Append(" h ").Append(minutes % 60).Append(" min");
    }

    /// <summary>Each input with how many the backpack holds of how many are needed.</summary>
    public static void AppendNeeds(World world, StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{world.Data.Name(item)} {world.Backpack[item]}/{amount}");
        }
    }

    public static string Needs(World world, Dictionary<string, int> cost) => LiveText.Of(text => AppendNeeds(world, text, cost));

    /// <summary>Each input with how many it takes.</summary>
    public static string Cost(World world, Dictionary<string, int> cost) {
        var text = new StringBuilder();
        foreach (var (item, amount) in cost) text.Append(text.Length > 0 ? "  ·  " : "").Append($"{amount} {world.Data.Name(item)}");
        return text.ToString();
    }
}
