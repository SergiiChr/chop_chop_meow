namespace ChopChopMeow;

/// <summary>
/// The Craft job: a crafter works a station that someone has to stand at, while it holds a batch or is on one.
/// A station with its own way of being worked, such as the packing station, extends it.
/// </summary>
internal class Crafting {
    protected readonly World world;

    public Crafting(World world) => this.world = world;

    /// <summary>Works the station for a tick, or says what it lacks and tells there is nothing to do.</summary>
    public virtual bool Work(Worker w, Thing station, float dt) {
        if (!world.Data.Recipes.TryGetValue(station.Recipe, out var recipe)) { w.Status = "Nothing queued or marked to make"; return false; }
        if (!world.Worked(station)) { w.Status = "Waiting for the next load"; return false; }
        if (station.Tools != null && recipe.Tool != "" && world.BenchTool(station, recipe.Tool, recipe.Tier) == null) {
            w.Status = world.Phrase("Needs ", world.A(world.Data.ToolName(recipe.Tool, recipe.Tier)), " in the ", world.Lower(world.Data.Name(station.Kind)));
            return false;
        }
        if (World.Stalled(station, recipe)) { w.Status = "Output slot is full"; return false; }
        if (!station.Processing && !world.Stocked(station)) {
            w.Status = Lacks(station, recipe) is string lack ? world.Phrase("Needs ", world.Lower(world.Data.Name(lack)), " at the ", world.Lower(world.Data.Name(station.Kind))) : "Waiting for the next load";
            return false;
        }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Working at station";
        if (world.Walk(w, station.Pos, dt)) w.WorkState = WorkerWorkState.Hammering;
        return true;
    }

    /// <summary>The first input a station is short of for a batch, or the fuel it lacks, named for a status; null when something else keeps it from starting.</summary>
    private string? Lacks(Thing station, RecipeDef recipe) =>
        station.Inventory.Short(recipe.Inputs) ?? (recipe.Fuels.Count > 0 && world.FuelOf(station) == null ? recipe.Fuels[0] : null);
}
