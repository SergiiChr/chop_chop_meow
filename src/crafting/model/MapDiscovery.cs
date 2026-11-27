using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>How much the recipe map shows of an entry.</summary>
public enum MapState { Undiscovered, NotMade, Made }

/// <summary>
/// Which recipe map entries the settlement knows: what it has made or built, and what the player can see elsewhere,
/// along with everything needed to make it.
/// The player sees every building in the build menu, everything made by hand in the crafting menu, what built stations
/// make, what they hold, the resources the settlement has seen and what the goals in the Goal Log name.
/// </summary>
public sealed class MapDiscovery {
    private readonly World world;
    /// <summary>The chains of every building and of everything made by hand, which show from a new game.</summary>
    private readonly HashSet<string> alwaysShown = new();
    /// <summary>For each entry, the items whose chain holds it, so it shows once any of them shows.</summary>
    private readonly Dictionary<string, List<string>> shownWith = new();
    /// <summary>The goals that name each item.</summary>
    private readonly Dictionary<string, List<GoalId>> namedBy = new();
    /// <summary>The items gathered from what the settlement has seen, worked out again when what it has seen changes.</summary>
    private readonly HashSet<string> seen = new();
    private (int Remembered, int Explored, float Time) seenAt = (-1, -1, -1);
    private bool sandSeen;

    public MapDiscovery(World world) {
        this.world = world;
        var data = world.Data;
        foreach (var kind in data.Buildings.Keys) AddChain(kind.Id(), alwaysShown);
        foreach (var recipe in data.Recipes.Values) if (recipe.Station == null || recipe.Hand) AddChain(recipe.Output, alwaysShown);
        foreach (string item in data.Items.Keys.Where(item => data.MapEntryOf(item) == item)) {
            var chain = new HashSet<string>();
            AddChain(item, chain);
            foreach (string entry in chain) {
                if (!shownWith.TryGetValue(entry, out var roots)) shownWith[entry] = roots = new();
                roots.Add(item);
            }
        }
        foreach (var goal in Goals.Entries)
            foreach (string item in goal.Items) {
                if (!namedBy.TryGetValue(item, out var goals)) namedBy[item] = goals = new();
                goals.Add(goal.Id);
            }
    }

    /// <summary>
    /// Whether the settlement has made the entry, the player can see it or something it goes into, or it is still to
    /// discover.
    /// </summary>
    public MapState Of(string item) {
        if (Made(item)) return MapState.Made;
        if (alwaysShown.Contains(item)) return MapState.NotMade;
        if (!shownWith.TryGetValue(item, out var roots)) return MapState.Undiscovered;
        RefreshSeen();
        foreach (string root in roots) if (Shown(root)) return MapState.NotMade;
        return MapState.Undiscovered;
    }

    /// <summary>Building plans count as made once built, and items once had.</summary>
    private bool Made(string item) =>
        world.Data.IsBuilding(item) ? world.Reached(item) : world.Data.Items.ContainsKey(item) && world.Had(item);

    /// <summary>
    /// Whether the player sees an item: a built station makes it, they hold it, the settlement has seen where it is
    /// gathered, or a goal in the Goal Log names it.
    /// </summary>
    private bool Shown(string item) {
        if (world.Data.RecipeOf(item)?.Station is ThingKind station && world.Reached(station.Id())) return true;
        foreach (string held in world.Me.Backpack.Items.Keys) if (world.Data.MapEntryOf(held) == item) return true;
        if (seen.Contains(item)) return true;
        if (namedBy.TryGetValue(item, out var goals))
            foreach (var goal in goals) if (world.Goals.Status(goal) != GoalStatus.Hidden) return true;
        return false;
    }

    /// <summary>
    /// Gathers again the items of everything the settlement remembers seeing, of the things in sight it does not remember
    /// such as firebugs, and the beach's sand once explored ground holds some.
    /// </summary>
    private void RefreshSeen() {
        var now = (world.S.Remembered.Count, world.ExploredRevision, world.Time);
        if (now == seenAt) return;
        seenAt = now;
        seen.Clear();
        foreach (var memory in world.S.Remembered.Values) AddSeen(memory.Kind);
        foreach (var thing in world.S.Things) if (!thing.TakesGround && world.Sees(thing.Pos)) AddSeen(thing.Kind);
        for (int tile = 0; !sandSeen && tile < world.S.Explored.Length; tile++)
            sandSeen = world.S.Explored[tile] != 0 && Beach.IsSand(Beach.Position(tile));
        if (sandSeen) seen.Add(Beach.Sand);
    }

    private void AddSeen(ThingKind kind) {
        if (GatherRules.Of(kind) is GatherRule rule) seen.Add(world.Data.MapEntryOf(rule.Item));
    }

    /// <summary>
    /// Adds an entry and, followed all the way down, the build cost, ingredients, fuels and tool it takes.
    /// </summary>
    private void AddChain(string item, HashSet<string> chain) {
        var data = world.Data;
        item = data.MapEntryOf(item);
        if (!chain.Add(item)) return;
        if (data.TryBuilding(item, out var kind)) {
            foreach (string material in MapBuilding.Of(data, kind).Materials.Keys) AddChain(material, chain);
            return;
        }
        if (data.RecipeOf(item) is not RecipeDef recipe) {
            var source = data.Sources[item];
            foreach (string from in source.From) AddChain(from, chain);
            foreach (string also in source.Also) AddChain(also, chain);
            return;
        }
        foreach (string input in recipe.Inputs.Keys) AddChain(input, chain);
        foreach (string fuel in recipe.Fuels) AddChain(fuel, chain);
        if (recipe.Tool != "") AddChain(data.PlainestTool(recipe.Tool, recipe.Tier, recipe.Station), chain);
    }
}
