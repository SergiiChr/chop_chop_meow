using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>How much the recipe map shows of an entry.</summary>
public enum MapState { Undiscovered, NotMade, Made }

/// <summary>
/// Which recipe map entries the settlement knows: what it has made or built, and what a station or a goal in the
/// Goal Log shows along with everything needed to make it.
/// </summary>
public sealed class MapDiscovery {
    private readonly World world;
    private readonly HashSet<string> stationChains = new();
    /// <summary>The chains of every tool the crafting menu lists, which show once a crafting bench is built.</summary>
    private readonly HashSet<string> toolChains = new();
    /// <summary>
    /// The goals whose chain holds each entry, so an entry shows once any of them is in the Goal Log.
    /// </summary>
    private readonly Dictionary<string, List<GoalId>> goalChains = new();

    public MapDiscovery(World world) {
        this.world = world;
        foreach (var station in world.StationKinds) AddChain(station.Id(), stationChains);
        foreach (var recipe in world.Data.Recipes.Values)
            if (recipe.Group == RecipeGroup.Tools) AddChain(recipe.Output, toolChains);
        foreach (var goal in Goals.Entries) {
            var chain = new HashSet<string>();
            foreach (string item in goal.Items) AddChain(item, chain);
            foreach (string item in chain) {
                if (!goalChains.TryGetValue(item, out var goals)) goalChains[item] = goals = new();
                goals.Add(goal.Id);
            }
        }
    }

    /// <summary>
    /// Whether the settlement has made the entry, has been shown it by a station or a goal, or has yet to discover it.
    /// </summary>
    public MapState Of(string item) =>
        Made(item) ? MapState.Made : InShownChain(item) ? MapState.NotMade : MapState.Undiscovered;

    /// <summary>Building plans count as made once built, and items once had.</summary>
    private bool Made(string item) =>
        world.Data.IsBuilding(item) ? world.Reached(item) : world.Data.Items.ContainsKey(item) && world.Had(item);

    /// <summary>
    /// Whether a station's chain, a tool's chain once a crafting bench is built, or the chain of a goal in the Goal Log
    /// holds the entry.
    /// </summary>
    private bool InShownChain(string item) {
        if (stationChains.Contains(item)) return true;
        if (toolChains.Contains(item) && world.Reached(ThingKind.Bench.Id())) return true;
        if (!goalChains.TryGetValue(item, out var goals)) return false;
        foreach (var goal in goals) if (world.Goals.Status(goal) != GoalStatus.Hidden) return true;
        return false;
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
