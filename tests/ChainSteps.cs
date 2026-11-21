using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static CabinSteps;
using static PlayerSteps;
using static SetupSteps;
using static WorkerSteps;

/// <summary>
/// Builds the lakeside settlement of the production chain check one chain at a time.
/// Buildings go on a grid three tiles apart, leaving lanes to walk between them.
/// </summary>
internal sealed class ChainSite(World world) {
    private readonly Vector2 origin = Terrain.Lake + Terrain.Seaward * 24 + Terrain.Sideways * -6;
    private int next;

    /// <summary>The next free spot on the grid, which must be open ground.</summary>
    public Vector2 Spot() {
        var p = origin + new Vector2(next % 9 * 3, next / 9 * 3); next++;
        Require(Terrain.Open(p), $"fixture spot {p} should be open ground");
        return p;
    }

    public Thing Place(ThingKind kind, string recipe = "") {
        var t = world.Add(kind, Spot());
        if (recipe != "") world.SetRecipe(t, recipe);
        return t;
    }

    public void Store(string item) => Place(ThingKind.Chest).Inventory.InputFilter.Add(item);

    /// <summary>Adds a worker at a building with the given duties.</summary>
    public Worker Employ(Thing place, params JobKind[] jobs) => Staff(world, place, jobs);

    /// <summary>
    /// The player's cabin far away, the trading post that sells each product by cart, the tool rack and a fire.
    /// Storage follows: a chest asking for each made material, a pile of empty bottles and three open piles.
    /// Returns the chest that takes raw meat.
    /// </summary>
    public Thing AddSettlement(string[] products) {
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(10, 10));
        StepInside(world, hut);
        var sign = Place(ThingKind.Signpost);
        sign.Wages!.Add(Inventory.Coins, 1000);
        Give(world, Inventory.Coins, 5000);
        foreach (string product in products) world.SetFilter(sign, World.CrateOf(product), false, true);
        world.SetAutoSell(sign, true);
        // A tool station holds four tools, so the crew's six hang on two.
        var rack = Place(ThingKind.Toolstation);
        rack.Inventory.Add("steel_axe", 2); rack.Inventory.Add("steel_pick", 2);
        var spare = Place(ThingKind.Toolstation);
        spare.Inventory.Add("steel_pick", 1); spare.Inventory.Add("rake", 1); spare.Inventory.Add(Bucket.Item, 2);
        Place(ThingKind.Campfire);

        foreach (string item in new[] { "planks", "crate", "iron_bar", "tin_bar", "steel_bar", "steel_parts", "wood_parts", "tin_can",
                     "raw_meat", "meat_stew", "canned_meat_stew", "wheat", "wheat_seeds", "grass", "fertilizer", "flour", "dough", "bread", "steel_pick" }) Store(item);
        var empties = Place(ThingKind.Pile); empties.Inventory.Add("empty_bottle", 8);
        Place(ThingKind.Pile); Place(ThingKind.Pile); Place(ThingKind.Pile);
        return world.Things.Single(t => t.Kind == ThingKind.Chest && t.Inventory.InputFilter.Contains("raw_meat"));
    }

    /// <summary>
    /// Raw materials: deposits, two trees, berries, leaves and the lake, each gathered by a worker.
    /// A chest asking for each material stands beside its source: a load goes to the nearest of that chest and the stations that use it, so the chest fills first and haulers stock the stations from it.
    /// </summary>
    public void AddGatherers() {
        Store("wood");
        Employ(world.Add(ThingKind.Tree, Spot(), 9999), JobKind.Gather);
        Employ(world.Add(ThingKind.Tree, Spot(), 9999), JobKind.Gather);
        foreach (var (item, kind) in new[] { ("iron_ore", ThingKind.Iron), ("coal", ThingKind.Coal), ("tin_ore", ThingKind.Tin), ("berries", ThingKind.Berries), ("leaves", ThingKind.Leaves) }) {
            Store(item);
            Employ(world.Add(kind, Spot(), 9999), JobKind.Gather);
        }
        Employ(world.Add(ThingKind.Water, Terrain.WaterPoint(origin), 9999), JobKind.Gather);
    }

    /// <summary>Crates from wood.</summary>
    public void AddCrateBenches() {
        var planks = Place(ThingKind.Bench, "planks"); planks.Tools!.Add("steel_chisel", 1); Employ(planks, JobKind.Craft, JobKind.Haul);
        var crates = Place(ThingKind.Bench, "crate"); crates.Tools!.Add("steel_hammer", 1); Employ(crates, JobKind.Craft, JobKind.Haul);
    }

    /// <summary>Canned meat stew: stew from meat and water, cans from tin, sealed over a wood fire.</summary>
    public void AddCannedStewChain() {
        Employ(Place(ThingKind.Cooktable, "meat_stew"), JobKind.Haul);
        Employ(Place(ThingKind.Furnace, "tin_bar"), JobKind.Haul);
        var cans = Place(ThingKind.Bench, "tin_can"); cans.Tools!.Add("steel_hammer", 1); Employ(cans, JobKind.Craft, JobKind.Haul);
        Employ(Place(ThingKind.Cannery, "canned_meat_stew"), JobKind.Haul);
    }

    /// <summary>Bread: compost from raked leaves, wheat from the farm, flour, dough and the oven. Returns the two farms.</summary>
    public Thing[] AddBreadChain() {
        var compost = Place(ThingKind.Pile); world.SetCompost(compost, true); world.SetSkipFood(compost, true); Employ(compost, JobKind.Tend);
        var farms = new[] { Place(ThingKind.Farm), Place(ThingKind.Farm) };
        foreach (var farm in farms) {
            farm.FarmStage = FarmStage.Growing;
            farm.Progress = world.Farms.GrowSeconds - 30;
            farm.Inventory.Add(Water.Liquid, 10);
            Employ(farm, JobKind.Farm, JobKind.Haul);
        }
        var flour = Place(ThingKind.Cooktable, "flour"); flour.Tools!.Add("mortar", 1); Employ(flour, JobKind.Craft, JobKind.Haul);
        Employ(Place(ThingKind.Cooktable, "dough"), JobKind.Craft, JobKind.Haul);
        Employ(Place(ThingKind.Furnace, "bread"), JobKind.Haul);
        return farms;
    }

    /// <summary>Steel pickaxes: iron bars, steel at the forge with one on the bellows and one hauling, parts at the anvil, handles and the pickaxe at benches.</summary>
    public void AddSteelPickChain() {
        Employ(Place(ThingKind.Furnace, "iron_bar"), JobKind.Haul);
        var forge = Place(ThingKind.Reinforced);
        Employ(forge, JobKind.Bellows); Employ(forge, JobKind.Haul);
        Employ(Place(ThingKind.Anvil), JobKind.Craft, JobKind.Haul);
        var handles = Place(ThingKind.Bench, "wood_parts"); handles.Tools!.Add("steel_chisel", 1); Employ(handles, JobKind.Craft, JobKind.Haul);
        var picks = Place(ThingKind.Bench, "steel_pick"); picks.Tools!.Add("steel_hammer", 1); Employ(picks, JobKind.Craft, JobKind.Haul);
    }

    /// <summary>One packing station for each product.</summary>
    public void AddPackers(string[] products) {
        foreach (string product in products) { var packer = Place(ThingKind.Packer); world.SetPacking(packer, product); Employ(packer, JobKind.Craft, JobKind.Haul); }
    }
}
