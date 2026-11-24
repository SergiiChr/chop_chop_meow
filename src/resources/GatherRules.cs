using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>
/// What a resource is to a gatherer: worked down over many gathers, an ore deposit at a cliff, a loose piece picked up whole in one go,
/// something rooted that is dug out of the ground whole, a plant plucked whole after one gather's time,
/// or a creature caught whole in one go.
/// </summary>
public enum GatherShape { Patch, Deposit, Loose, Dug, Plucked, Caught }

/// <summary>How one kind of resource is gathered.</summary>
/// <param name="Item">What gathering it yields.</param>
/// <param name="Needs">The tool kind gathering it takes, or null when bare hands do.</param>
/// <param name="Helps">A tool kind that only speeds gathering it up, or null.</param>
/// <param name="ByHand">How many times longer a gather takes without a tool that speeds it up.</param>
/// <param name="Chip">The loose piece a tool hit now and then knocks off it, or null.</param>
/// <param name="Shape">How it is taken: worked down, an ore deposit, a loose piece, dug up whole, plucked whole or caught.</param>
/// <param name="Job">The gathering job it belongs to when that is not every spot yielding its item, or null.</param>
/// <param name="Sting">Health a second that gathering it takes from a player who carries no tool of the <paramref name="Spares"/> kind.</param>
/// <param name="Spares">The tool kind that keeps a gatherer from being stung, or null: a player may do without it, and a worker always takes one.</param>
public readonly record struct GatherRule(string Item, string? Needs = null, string? Helps = null, float ByHand = 1, ThingKind? Chip = null, GatherShape Shape = GatherShape.Patch, string? Job = null,
    float Sting = 0, string? Spares = null) {
    /// <summary>The job a gatherer of this resource does: they move on only to spots of the same job.</summary>
    public string JobName => Job ?? Item;
    /// <summary>The tool kind a worker has to hold to gather it, or null when bare hands do.</summary>
    public string? WorkerNeeds => Needs ?? Spares;
    /// <summary>Whether a worker takes all of it the moment they reach it, with no gather's time.</summary>
    public bool TakenAtOnce => Shape is GatherShape.Loose or GatherShape.Caught;
    /// <summary>Whether it leaves the world once nothing is left of it.</summary>
    public bool GoneWhenSpent => Shape is GatherShape.Loose or GatherShape.Plucked or GatherShape.Caught;
    /// <summary>Whether it moves about, so no player remembers where one is and a worker goes to any that is out.</summary>
    public bool Roams => Shape == GatherShape.Caught;
}

/// <summary>Every gathering rule in one table, so a new resource is one line here.</summary>
public static class GatherRules {
    /// <summary>Chance that one axe or pickaxe hit knocks a loose piece off its target.</summary>
    public const float ChipChance = .05f;
    /// <summary>Farthest a chip lands from the middle of what it came off.</summary>
    public const float ChipReach = 2;
    /// <summary>Seconds of digging at a speed of 1 that bring a stump or a bush out of the ground.</summary>
    public const float DigSeconds = 6;

    private static readonly Dictionary<ThingKind, GatherRule> All = new() {
        [ThingKind.Tree] = new("wood", Needs: "axe", Chip: ThingKind.Acorn),
        [ThingKind.Stone] = new("stone", Needs: "pickaxe", Chip: ThingKind.LooseStone),
        [ThingKind.LeafSpot] = new("leaves", Helps: "rake", ByHand: 3),
        [ThingKind.Berries] = new("berries"),
        [ThingKind.Water] = new(Water.Full),
        [ThingKind.WolfCorpse] = new("raw_meat"),
        [ThingKind.Iron] = new("iron_ore", Needs: "pickaxe", Chip: ThingKind.IronChip, Shape: GatherShape.Deposit),
        [ThingKind.Coal] = new("coal", Needs: "pickaxe", Chip: ThingKind.LooseCoal, Shape: GatherShape.Deposit),
        [ThingKind.Tin] = new("tin_ore", Needs: "pickaxe", Chip: ThingKind.TinChip, Shape: GatherShape.Deposit),
        [ThingKind.LooseStone] = new("stone", Shape: GatherShape.Loose),
        [ThingKind.Stick] = new("wood", Shape: GatherShape.Loose),
        [ThingKind.Acorn] = new("acorn", Shape: GatherShape.Loose),
        [ThingKind.LooseCoal] = new("coal", Shape: GatherShape.Loose),
        [ThingKind.IronChip] = new("iron_ore", Shape: GatherShape.Loose),
        [ThingKind.TinChip] = new("tin_ore", Shape: GatherShape.Loose),
        // A woodcutter with a shovel to hand digs up stumps too, while bushes are a job apart from raking leaves.
        [ThingKind.Stump] = new("wood", Needs: "shovel", Shape: GatherShape.Dug),
        [ThingKind.Bush] = new("leaves", Needs: "shovel", Shape: GatherShape.Dug, Job: "bushes"),
        // A nettle takes 3 seconds to pick, whatever is carried.
        [ThingKind.Nettle] = new("nettle", ByHand: 1.5f, Shape: GatherShape.Plucked, Sting: 1, Spares: "shovel"),
        // A player catches a firebug with a swing of the net; a worker with a net takes the one they reach.
        [ThingKind.Firebug] = new("firebug", Needs: "net", Shape: GatherShape.Caught),
    };

    // The same rules by kind, since every resource check in the tick and the view asks for one.
    private static readonly GatherRule?[] ByKind = ByKindOf(All);

    private static GatherRule?[] ByKindOf(Dictionary<ThingKind, GatherRule> rules) {
        var byKind = new GatherRule?[Kinds.All.Length];
        foreach (var (kind, rule) in rules) byKind[(int)kind] = rule;
        return byKind;
    }

    /// <summary>The rule for gathering a kind, or null for anything that is not gathered.</summary>
    public static GatherRule? Of(ThingKind kind) => ByKind[(int)kind];
}
