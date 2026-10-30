using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
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
    public Worker Employ(Thing place, params string[] jobs) {
        var w = AddWorker(world, place.Pos + new Vector2(0, 1.2f));
        w.Wage = 18;
        foreach (string job in jobs) world.SetDuty(w, place, job, true);
        return w;
    }

    /// <summary>
    /// The player's cabin far away, the trading post that sells each product by cart, the tool rack and a fire.
    /// Storage follows: a chest asking for each material, a pile of empty bottles and three open piles.
    /// Returns the chest that takes raw meat.
    /// </summary>
    public Thing AddSettlement(string[] products) {
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(10, 10));
        world.Player = hut.Pos; world.S.Sheltered = hut.Id;
        var sign = Place(ThingKind.Signpost);
        sign.Wages!.Add(Inventory.Coins, 1000);
        world.S.Backpack.Add(Inventory.Coins, 5000);
        foreach (string product in products) world.SetFilter(sign, World.CrateOf(product), false, true);
        world.SetAutoSell(sign, true);
        var rack = Place(ThingKind.Toolstation);
        rack.Inventory.Add("steel_axe", 2); rack.Inventory.Add("steel_pick", 3); rack.Inventory.Add("rake", 1);
        Place(ThingKind.Campfire);

        foreach (string item in new[] { "wood", "planks", "crate", "iron_ore", "coal", "tin_ore", "iron_bar", "tin_bar", "steel_bar", "steel_parts", "wood_parts", "tin_can",
                     "raw_meat", "meat_stew", "canned_meat_stew", "leaves", "wheat", "wheat_seeds", "grass", "fertilizer", "flour", "dough", "bread", "berries", "steel_pick" }) Store(item);
        var empties = Place(ThingKind.Pile); empties.Inventory.Add("empty_bottle", 8);
        Place(ThingKind.Pile); Place(ThingKind.Pile); Place(ThingKind.Pile);
        return world.S.Things.Single(t => t.Kind == ThingKind.Chest && t.Inventory.InputFilter.Contains("raw_meat"));
    }

    /// <summary>Raw materials: deposits, a tree, berries, leaves and the lake, each gathered by a worker.</summary>
    public void AddGatherers() {
        Employ(world.Add(ThingKind.Tree, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Tree, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Iron, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Coal, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Tin, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Berries, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Leaves, Spot(), 9999), "gather");
        Employ(world.Add(ThingKind.Water, Terrain.WaterPoint(origin), 9999), "gather");
    }

    /// <summary>Crates from wood.</summary>
    public void AddCrateBenches() {
        var planks = Place(ThingKind.Bench, "planks"); planks.Tools!.Add("steel_chisel", 1); Employ(planks, "craft", "haul");
        var crates = Place(ThingKind.Bench, "crate"); crates.Tools!.Add("steel_hammer", 1); Employ(crates, "craft", "haul");
    }

    /// <summary>Canned meat stew: stew from meat and water, cans from tin, sealed over a wood fire.</summary>
    public void AddCannedStewChain() {
        Employ(Place(ThingKind.Cooktable, "meat_stew"), "haul");
        Employ(Place(ThingKind.Furnace, "tin_bar"), "haul");
        var cans = Place(ThingKind.Bench, "tin_can"); cans.Tools!.Add("steel_hammer", 1); Employ(cans, "craft", "haul");
        Employ(Place(ThingKind.Cannery, "canned_meat_stew"), "haul");
    }

    /// <summary>Bread: compost from raked leaves, wheat from the farm, flour, dough and the oven. Returns the two farms.</summary>
    public Thing[] AddBreadChain() {
        var compost = Place(ThingKind.Pile); world.SetCompost(compost, true); world.SetSkipFood(compost, true); Employ(compost, "tend");
        var farms = new[] { Place(ThingKind.Farm), Place(ThingKind.Farm) };
        foreach (var farm in farms) { farm.FarmStage = FarmStage.Growing; farm.Progress = world.GrowSeconds - 30; Employ(farm, "farm", "haul"); }
        var flour = Place(ThingKind.Cooktable, "flour"); flour.Tools!.Add("mortar", 1); Employ(flour, "craft", "haul");
        Employ(Place(ThingKind.Cooktable, "dough"), "haul");
        Employ(Place(ThingKind.Furnace, "bread"), "haul");
        return farms;
    }

    /// <summary>Steel pickaxes: iron bars, steel at the forge with one on the bellows and one hauling, parts at the anvil, handles and the pickaxe at benches.</summary>
    public void AddSteelPickChain() {
        Employ(Place(ThingKind.Furnace, "iron_bar"), "haul");
        var forge = Place(ThingKind.Reinforced);
        Employ(forge, "bellows"); Employ(forge, "haul");
        Employ(Place(ThingKind.Anvil, "steel_parts"), "craft", "haul");
        var handles = Place(ThingKind.Bench, "wood_parts"); handles.Tools!.Add("steel_chisel", 1); Employ(handles, "craft", "haul");
        var picks = Place(ThingKind.Bench, "steel_pick"); picks.Tools!.Add("steel_hammer", 1); Employ(picks, "craft", "haul");
    }

    /// <summary>One packing station for each product.</summary>
    public void AddPackers(string[] products) {
        foreach (string product in products) { var packer = Place(ThingKind.Packer); world.SetPacking(packer, product); Employ(packer, "craft", "haul"); }
    }
}
