using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;
using static ProgressionSteps;

/// <summary>
/// Checks of the way from a fresh start to the first hired worker: that everything can be reached, and a playthrough
/// of it with real recipes and trade delays.
/// </summary>
public static class ProgressionChecks {
    public static void Run(GameData data) {
        Test(CheckEverythingIsReachableFromFreshStart, data);
        Test(CheckPlayerProgressesFromRawMaterialsToFirstAutomation, data);
    }

    /// <summary>
    /// Every item and building must be reachable from a fresh start with only gathering, recipes and berry trade, and the tools that unlock the loop must never be lost.
    /// Trees need an axe, deposits a pickaxe and beach sand a shovel. Stone, berries, water and loose pieces need no tools.
    /// </summary>
    private static void CheckEverythingIsReachableFromFreshStart(GameData data) {
        var fresh = new World(data);
        var have = new HashSet<string>(fresh.Backpack.Items.Keys) { "stone", "berries", Water.Liquid, "raw_meat", "nettle" };
        var built = new HashSet<ThingKind>();
        bool HasTool(string tool, int tier = 0) => have.Any(id => data.Items[id].Tool == tool && data.Items[id].Tier >= tier);
        for (bool grew = true; grew;) {
            grew = false;
            if (HasTool("axe")) { grew |= have.Add("wood"); grew |= have.Add("acorn"); }
            foreach (var ore in new[] { "iron_ore", "coal", "tin_ore" }) if (HasTool("pickaxe")) grew |= have.Add(ore);
            // Crates of berries sold at a signpost buy what the village sells, the first pickaxe among it.
            if (built.Contains(ThingKind.Signpost) && have.Contains("crate") && have.Contains("berries")) foreach (var item in data.Market.Buys.Keys) grew |= have.Add(item);
            if (HasTool("rake")) grew |= have.Add("leaves");
            if (HasTool("net")) grew |= have.Add("firebug");
            if (HasTool("shovel")) grew |= have.Add("sand");
            // A full vessel bought pours its liquid, and a vessel filled with its liquid is a full one.
            foreach (var (id, def) in data.Items) {
                if (def.FullOf is not string liquid) continue;
                if (have.Contains(id)) grew |= have.Add(liquid);
                if (have.Contains(data.VesselFor(liquid)!.Empty) && have.Contains(liquid)) grew |= have.Add(id);
            }
            foreach (var (id, b) in data.Buildings) if (b.Cost.Keys.All(have.Contains)) grew |= built.Add(id);
            if (built.Contains(ThingKind.Pile) && have.Any(data.IsFood)) grew |= have.Add("fertilizer");
            // Seeds are bought with berry money to start the first plot.
            if (built.Contains(ThingKind.Farm) && data.Farm.Sow.Keys.All(id => have.Contains(id) || data.Market.Buys.ContainsKey(id))) foreach (var item in data.Farm.Yield.Keys) grew |= have.Add(item);
            foreach (var r in data.Recipes.Values) {
                bool hasTool = r.Tool == "" || have.Any(id => r.MadeWith(data.Items[id]));
                bool byHand = r.Hand && hasTool;
                bool atStation = r.Station is ThingKind station && built.Contains(station)
                    && (data.FixedTool(station, r.Tool) is string item ? have.Contains(item) : data.Buildings[station].ToolSlots.Length == 0 || hasTool);
                bool where = byHand || atStation;
                if (where && r.Inputs.Keys.All(have.Contains)) grew |= have.Add(r.Output);
            }
        }
        var unreachable = data.Items.Keys.Where(id => id != Inventory.Coins && !data.Market.Buys.ContainsKey(id) && !have.Contains(id)).Concat(data.Buildings.Keys.Where(kind => !built.Contains(kind)).Select(kind => kind.Id())).ToList();
        Require(unreachable.Count == 0, $"everything should be reachable from a fresh start: {string.Join(", ", unreachable)}");
        // The village never takes the tools that make crates, so selling cannot strand the player.
        Require(!data.Market.Sells.Keys.Any(id => id is "stone_chisel" or "wood_hammer"), "bootstrap tools must not be sellable");
        // The first pickaxe, and a lost axe, are bought with berry money; a crate needs only planks and a mallet.
        Require(data.Market.Sells.ContainsKey("berries"), "berries must be sellable");
        Require(data.Market.Buys.ContainsKey("steel_axe"), "an axe must be buyable");
        Require(data.Market.Buys.ContainsKey("steel_pick"), "a pickaxe must be buyable");
        Require(data.Market.Buys.ContainsKey("steel_hammer"), "a steel hammer must be buyable: the anvil needs one to make the steel parts it is made of");
        Require(fresh.LotPrice("steel_pick") <= 4 * CrateSells(data, "berries"), "four crates of berries should pay for the first pickaxe");
    }

    /// <summary>Plays from raw materials to a first hired worker: each stage hands the next what the player built with real recipes and trade delays.</summary>
    private static void CheckPlayerProgressesFromRawMaterialsToFirstAutomation(GameData data) {
        var world = EmptyWorld(data);
        float startTime = world.Time;
        Give(world, "steel_axe", 1);
        Give(world, "berries", 10);
        Give(world, "wood", 6);

        var tree = world.Add(ThingKind.Tree, Site(8, 8), 300);
        var stone = world.Add(ThingKind.Stone, Site(8, 14), 300);
        var bush = world.Add(ThingKind.Berries, Site(14, 8), 90);
        var iron = world.Add(ThingKind.Iron, Site(12, 14), 120);
        var coal = world.Add(ThingKind.Coal, Site(16, 8), 120);
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true && world.GatherBlock(coal)?.Contains("pickaxe") == true, "deposits should wait for a pickaxe the player does not start with");
        var bench = MakeCratesFromGatheredMaterials(world, tree, stone, bush);
        var sign = BuyFirstPickaxeWithBerryCrates(world, iron);
        BuySteelHammerWithBerryCrates(world, sign);
        MakeSteelToolsFromMinedOre(world, stone, iron, coal, bench);
        var offer = SellSteelToolForTheFirstHire(world, sign);
        SupplyOpeningCampWithHiredWorker(world, sign, offer, tree, stone);
        world.Tick(.01f, true);
        // The diary's pages turn up by chance, and two of them wait for the caves.
        foreach (var goal in Goals.Entries)
            if (goal.Id is not (GoalId.Profit or GoalId.Mines or GoalId.Diary))
                Require(world.Goals.Status(goal.Id) == GoalStatus.Completed, $"progression to first automation should complete {goal.Title}");
        GD.Print($"Progression simulation elapsed: {world.Time - startTime:0} seconds.");
    }
}
