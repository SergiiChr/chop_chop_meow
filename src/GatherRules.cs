using System;
using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>What a resource is to a gatherer: worked down over many gathers, an ore deposit at a cliff, or a loose piece picked up whole in one go.</summary>
public enum GatherShape { Patch, Deposit, Loose }

/// <summary>How one kind of resource is gathered.</summary>
/// <param name="Item">What gathering it yields.</param>
/// <param name="Needs">The tool kind gathering it takes, or null when bare hands do.</param>
/// <param name="Helps">A tool kind that only speeds gathering it up, or null.</param>
/// <param name="ByHand">How many times longer a gather takes without the helping tool.</param>
/// <param name="Chip">The loose piece a pickaxe hit now and then knocks off it, or null.</param>
/// <param name="Shape">Whether it is worked down, an ore deposit or a loose piece.</param>
public readonly record struct GatherRule(string Item, string? Needs = null, string? Helps = null, float ByHand = 1, ThingKind? Chip = null, GatherShape Shape = GatherShape.Patch);

/// <summary>Every gathering rule in one table, so a new resource is one line here.</summary>
public static class GatherRules {
    /// <summary>Chance that one pickaxe hit on a boulder or an ore deposit knocks a loose piece off it.</summary>
    public const float ChipChance = .05f;
    /// <summary>Farthest a chip lands from the middle of what it came off.</summary>
    public const float ChipReach = 2;

    private static readonly Dictionary<ThingKind, GatherRule> All = new() {
        [ThingKind.Tree] = new("wood", Needs: "axe"),
        [ThingKind.Stone] = new("stone", Helps: "pickaxe", Chip: ThingKind.LooseStone),
        [ThingKind.Leaves] = new("leaves", Helps: "rake", ByHand: 3),
        [ThingKind.Berries] = new("berries"),
        [ThingKind.Water] = new("water"),
        [ThingKind.WolfCorpse] = new("raw_meat"),
        [ThingKind.Iron] = new("iron_ore", Needs: "pickaxe", Chip: ThingKind.IronChip, Shape: GatherShape.Deposit),
        [ThingKind.Coal] = new("coal", Needs: "pickaxe", Chip: ThingKind.LooseCoal, Shape: GatherShape.Deposit),
        [ThingKind.Tin] = new("tin_ore", Needs: "pickaxe", Chip: ThingKind.TinChip, Shape: GatherShape.Deposit),
        [ThingKind.LooseStone] = new("stone", Shape: GatherShape.Loose),
        [ThingKind.Stick] = new("wood", Shape: GatherShape.Loose),
        [ThingKind.LooseCoal] = new("coal", Shape: GatherShape.Loose),
        [ThingKind.IronChip] = new("iron_ore", Shape: GatherShape.Loose),
        [ThingKind.TinChip] = new("tin_ore", Shape: GatherShape.Loose),
    };

    // The same rules by kind, since every resource check in the tick and the view asks for one.
    private static readonly GatherRule?[] ByKind = ByKindOf(All);

    private static GatherRule?[] ByKindOf(Dictionary<ThingKind, GatherRule> rules) {
        var byKind = new GatherRule?[Enum.GetValues<ThingKind>().Length];
        foreach (var (kind, rule) in rules) byKind[(int)kind] = rule;
        return byKind;
    }

    /// <summary>The rule for gathering a kind, or null for anything that is not gathered.</summary>
    public static GatherRule? Of(ThingKind kind) => ByKind[(int)kind];

    /// <summary>The tool kind that spots yielding an item take, such as an axe for wood, or null when every one is gathered by hand.</summary>
    public static string? ToolFor(string item) {
        foreach (var rule in All.Values) if (rule.Item == item && rule.Needs != null) return rule.Needs;
        return null;
    }
}
