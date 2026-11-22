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
    /// Resources need their tool first: trees an axe, deposits a pickaxe; stone, berries, water and loose pieces need none.
    /// </summary>
    private static void CheckEverythingIsReachableFromFreshStart(GameData data) {
        var fresh = new World(data);
        var have = new HashSet<string>(fresh.Backpack.Items.Keys) { "stone", "berries", "water", "water_1", "water_2", "raw_meat" };
        var built = new HashSet<ThingKind>();
        bool HasTool(string tool, int tier = 0) => have.Any(id => data.Items[id].Tool == tool && data.Items[id].Tier >= tier);
        for (bool grew = true; grew;) {
            grew = false;
            if (HasTool("axe")) grew |= have.Add("wood");
            foreach (var ore in new[] { "iron_ore", "coal", "tin_ore" }) if (HasTool("pickaxe")) grew |= have.Add(ore);
            // Crates of berries sold at a signpost buy what the village sells, the first pickaxe among it.
            if (built.Contains(ThingKind.Signpost) && have.Contains("crate") && have.Contains("berries")) foreach (var item in data.Market.Buys.Keys) grew |= have.Add(item);
            if (HasTool("rake")) grew |= have.Add("leaves");
            // Crafting the vessel and reaching its liquid opens every fill state.
            foreach (var vessel in data.Vessels) {
                if (have.Contains(vessel.Full)) grew |= have.Add(vessel.Liquid);
                if (have.Contains(vessel.Empty) && have.Contains(vessel.Liquid)) foreach (string fill in vessel.States) grew |= have.Add(fill);
            }
            if (have.Contains(Barrel.Item) && have.Contains(Water.Liquid)) foreach (string fill in Barrel.Fills) grew |= have.Add(fill);
            foreach (var (id, b) in data.Buildings) if (b.Cost.Keys.All(have.Contains)) grew |= built.Add(id);
            if (built.Contains(ThingKind.Pile) && have.Any(data.IsFood)) grew |= have.Add("fertilizer");
            // Seeds are bought with berry money to start the first plot.
            if (built.Contains(ThingKind.Farm) && data.Farm.Sow.Keys.All(id => have.Contains(id) || data.Market.Buys.ContainsKey(id))) foreach (var item in data.Farm.Yield.Keys) grew |= have.Add(item);
            foreach (var r in data.Recipes.Values) {
                bool where = r.Hand && (r.Tool == "" || HasTool(r.Tool, r.Tier)) || r.Station is ThingKind station && built.Contains(station) && (World.SlotRules(station).Length == 0 || r.Tool == "" || HasTool(r.Tool, r.Tier));
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
        var bench = CheckGatheredMaterialsMakeCrates(world, tree, stone, bush);
        var sign = CheckBerryCratesBuyFirstPickaxe(world, iron);
        CheckBerryCratesBuySteelHammer(world, sign);
        CheckMinedOreBecomesSteelTools(world, stone, iron, coal, bench);
        var offer = CheckSoldSteelToolFundsHire(world, sign);
        CheckHiredWorkerSuppliesOpeningCamp(world, sign, offer, tree, stone);
        world.Tick(.01f, true);
        Require(world.GoalComplete, "objective chain should reach the first automation milestone");
        GD.Print($"Progression simulation elapsed: {world.Time - startTime:0} seconds.");
    }

    /// <summary>A stone chisel from loose stones opens wood crafting by hand, which leads to the bench, its mallet and crates.</summary>
    private static Thing CheckGatheredMaterialsMakeCrates(World world, Thing tree, Thing stone, Thing bush) {
        GatherUntil(world, tree, "wood", 76);
        Require(world.GatherBlock(stone)?.Contains("pickaxe") == true, "a boulder should wait for a pickaxe the player does not start with");
        // A chisel and the trading post that sells the first pickaxe take 6 stone, picked up loose.
        for (int i = 0; i < 6; i++) PickUp(world, world.Add(ThingKind.LooseStone, Site(4 + i, 4), 1));
        // Four crates of berries buy the first pickaxe and two more the steel hammer.
        // A seventh goes with the first steel tool sold.
        GatherUntil(world, bush, "berries", 84);

        CraftByHand(world, "stone_chisel", 1);
        CraftByHand(world, "planks", 6);
        // Two more for the second crate of the first steel tool sale, which carries the seventh crate of berries.
        CraftByHand(world, "planks", 7);
        // Three more for the two crates of berries that buy the steel hammer.
        CraftByHand(world, "planks", 3);
        CraftByHand(world, "wood_parts", 2);
        CraftByHand(world, "wood_hammer", 1);
        Require(world.Backpack["planks"] == 64, "hand crafting should turn wood into planks");
        Require(world.Backpack["wood_parts"] == 3, "hand crafting should turn wood into parts");
        Require(world.Backpack["wood_hammer"] == 1, "hand crafting should turn wood into a mallet");
        var bench = BuildAt(world, ThingKind.Bench, Site(28, 28));
        Require(world.SlotTool(bench, "wood_hammer") && world.BenchTool(bench, "hammer") == "wood_hammer", "the mallet should go into the bench's hammer slot");
        world.SetRecipe(bench, "crate");
        world.LoadBatch(bench, 6);
        CompleteBatches(world, bench, "crate", 6);
        world.Collect(bench);
        Require(world.Backpack["crate"] == 6 && world.Reached("crate"), "the bench should make crates from planks with the mallet slotted");
        return bench;
    }

    /// <summary>The first pickaxe is bought with berry money: four crates of berries pay for it.</summary>
    private static Thing CheckBerryCratesBuyFirstPickaxe(World world, Thing iron) {
        var data = world.Data;
        var sign = BuildAt(world, ThingKind.Signpost, Site(34, 25));
        for (int i = 0; i < 4; i++) LoadCrate(world, sign, "berries", data.CrateCapacity);
        Require(world.Sell(sign), "signpost should dispatch crates of berries");
        SafeTick(world, data.TradeSeconds + .2f);
        world.Transfer(sign.Wages!, world.Backpack, Inventory.Coins, sign.Wages![Inventory.Coins]);
        world.Transfer(sign.Output!, world.Backpack, Inventory.Coins, sign.Output![Inventory.Coins]);
        Require(world.Reached("sold") && world.Coins >= world.LotPrice("steel_pick"), $"four crates of berries should pay for a steel pickaxe: coins={world.Coins}");
        Require(world.Buy(sign, "steel_pick", 1), "the village should sell a steel pickaxe");
        SafeTick(world, data.TradeSeconds + .2f);
        Require(world.Transfer(sign.Output, world.Backpack, "steel_pick", 1) == 1,
            "the bought pickaxe should arrive at the signpost");
        Require(world.Reached("pick"), "the bought pickaxe should meet the pick milestone");
        Require(world.GatherBlock(iron) == null, "the bought pickaxe should unlock mining deposits");
        return sign;
    }

    /// <summary>
    /// Two more crates of berries buy the steel hammer the anvil needs, which only the village sells before there are
    /// steel parts.
    /// </summary>
    private static void CheckBerryCratesBuySteelHammer(World world, Thing sign) {
        var data = world.Data;
        for (int i = 0; i < 2; i++) LoadCrate(world, sign, "berries", data.CrateCapacity);
        Require(world.Sell(sign), "signpost should dispatch two more crates of berries");
        SafeTick(world, data.TradeSeconds + .2f);
        world.Transfer(sign.Wages!, world.Backpack, Inventory.Coins, sign.Wages![Inventory.Coins]);
        world.Transfer(sign.Output!, world.Backpack, Inventory.Coins, sign.Output![Inventory.Coins]);
        Require(world.Coins >= world.LotPrice("steel_hammer"), $"two more crates of berries should pay for a steel hammer: coins={world.Coins}");
        Require(world.Buy(sign, "steel_hammer", 1), "the village should sell a steel hammer");
        SafeTick(world, data.TradeSeconds + .2f);
        Require(world.Transfer(sign.Output, world.Backpack, "steel_hammer", 1) == 1, "the bought steel hammer should arrive at the signpost");
    }

    private static void CheckMinedOreBecomesSteelTools(World world, Thing stone, Thing iron, Thing coal, Thing bench) {
        GatherUntil(world, stone, "stone", 52);
        GatherUntil(world, iron, "iron_ore", 22);
        GatherUntil(world, coal, "coal", 17);
        // Smelting and the first steel sale run on into the night.
        // So the furnaces, the anvil and the trading post stand by two campfires, stocked to burn through it.
        foreach (var site in new[] { Site(25, 26.5f), Site(31, 28) }) {
            var fire = BuildAt(world, ThingKind.Campfire, site);
            Require(world.Transfer(world.Backpack, fire.Inventory, "wood", 3) == 3, "a campfire should take wood to burn through the night");
        }
        var furnace = BuildAt(world, ThingKind.Furnace, Site(25, 25));
        world.LoadBatch(furnace, 11);
        Require(furnace.Inventory["iron_ore"] == 22 && furnace.Burner!["coal"] == 11,
                "loading the furnace should transfer the recipe inputs, and one fuel per batch into its fuel slot");
        CompleteBatches(world, furnace, "iron_bar", 11);
        world.Player = furnace.Pos;
        world.Collect(furnace);
        Require(world.Backpack["iron_bar"] == 11, $"player should collect bars actually smelted by the furnace from mined ore: {string.Join(", ", world.Backpack.Items.Select(p => $"{p.Key}={p.Value}"))}");

        var reinforced = BuildAt(world, ThingKind.Reinforced, Site(31, 25));
        world.Player = reinforced.Pos;
        int bars = world.Backpack["iron_bar"];
        world.LoadBatch(reinforced, 1);
        Require(reinforced.Inventory["iron_bar"] == 2, "the reinforced furnace should load iron");
        Require(reinforced.Inventory["coal"] == 2, "the reinforced furnace should load coal");
        Require(reinforced.Burner!["coal"] == 1, "the reinforced furnace should load coal fuel into its fuel slot");
        CompleteBatches(world, reinforced, "steel_bar", 2, true);
        world.Player = reinforced.Pos;
        world.Collect(reinforced);
        Require(world.Backpack["steel_bar"] == 2 && world.Backpack["iron_bar"] == bars - 2,
                "one smelt of 2 iron bars and coal should make two steel bars from real inputs");

        var anvil = BuildAt(world, ThingKind.Anvil, Site(28, 25));
        world.Player = anvil.Pos + Vector2.One;
        Require(world.SlotTool(anvil, "steel_hammer"), "the bought steel hammer should go into the anvil's hammer slot");
        world.LoadBatch(anvil, 2);
        CompleteBatches(world, anvil, "steel_parts", 2);
        world.Collect(anvil);
        world.Player = bench.Pos + Vector2.One;
        world.SetRecipe(bench, "steel_pick");
        world.LoadBatch(bench);
        CompleteBatches(world, bench, "steel_pick", 1);
        world.Collect(bench);
        world.SetRecipe(bench, "crate");
        world.LoadBatch(bench, 2);
        CompleteBatches(world, bench, "crate", 2);
        world.Collect(bench);
        Require(world.Backpack["steel_pick"] == 2, "the bench should craft a steel pickaxe from steel parts");
        Require(world.Backpack["crate"] == 2, "the bench should craft crates from planks");
        Require(world.Reached("steel_tool"), "the steel tool milestone should be reached");
    }

    private static HireOffer CheckSoldSteelToolFundsHire(World world, Thing sign) {
        var data = world.Data;
        // The seventh crate of berries goes along, so the sale covers the hire and the worker's first wage.
        LoadCrate(world, sign, "berries", data.CrateCapacity);
        LoadCrate(world, sign, "steel_pick", 1);
        Require(world.Backpack["crate"] == 0, "both crates the bench made should be on the cart");
        Require(world.Sell(sign), "signpost should dispatch a crate of steel tools and one of berries");
        float beforeReturn = world.Coins;
        SafeTick(world, data.TradeSeconds - .1f);
        Require(world.Coins == beforeReturn && world.Orders.Count == 1,
                "steel-tool payment should remain in transit until the trade delay elapses");
        SafeTick(world, .2f);
        world.Transfer(sign.Wages!, world.Backpack, Inventory.Coins, sign.Wages![Inventory.Coins]);
        var offer = data.HireOffers[1];
        Require(world.Coins >= offer.Upfront && world.Reached("sold_steel"),
                $"returned cart should fund hire and mark the sale: coins={world.Coins:0.0}, upfront={offer.Upfront:0.0}, soldSteel={world.Reached("sold_steel")}, health={world.Health:0.0}, time={world.Time:0.0}, backpackSteel={world.Backpack["steel_pick"]}");
        return offer;
    }

    private static void CheckHiredWorkerSuppliesOpeningCamp(World world, Thing sign, HireOffer offer, Thing tree, Thing stone) {
        var data = world.Data;
        var worker = world.Hire(sign, offer);
        Require(worker != null, "sale proceeds should fund a new worker");
        // Selling for the first pickaxe takes the opening day, so a worker hired at night starts in the morning; the night's wolves are not what this checks.
        for (int i = 0; i < 4000 && !world.OnDuty(worker!); i++) { SafeTick(world, Step); RemoveWolves(world, _ => true); }
        var chest = BuildAt(world, ThingKind.Chest, Site(37, 25));
        // A woodcutter takes an axe from a tool station, so the player racks theirs: a station is 8 planks and 4 stone.
        GatherUntil(world, tree, "wood", 2);
        GatherUntil(world, stone, "stone", 4);
        CraftByHand(world, "planks", 2);
        var station = BuildAt(world, ThingKind.Toolstation, Site(37, 28));
        Require(world.Transfer(world.Backpack, station.Inventory, "steel_axe", 1) == 1, "the player should rack their axe in the tool station");
        var workerTree = world.Things.FirstOrDefault(t => t.Kind == ThingKind.Tree && t.Remaining > 0);
        Require(workerTree != null, "a remaining tree should be available for the hired worker");
        world.Assign(worker!, workerTree);
        Require(worker!.Assignment == workerTree!.Id, "player should assign the hired worker to the resource");
        Require(worker.Jobs.Count == 1, "the hired worker should have one duty");
        Require(World.HasDuty(worker, workerTree.Id, JobKind.Gather), "the hired worker's duty should be gathering from that resource");
        Require(world.Reached("assigned"), "assigning a worker should reach the assigned milestone");
        for (int i = 0; i < 480 && chest.Inventory["wood"] == 0; i++) SafeTick(world, Step);
        Require(chest.Inventory["wood"] > 0 && worker.Bag["wood"] == 0,
                "hired worker should gather actual wood and deliver it to a built chest");
        // Reuse the worker's real gathered output after lending them the player's axe.
        for (int i = 0; i < 8000 && chest.Inventory["wood"] < 26; i++) SafeTick(world, Step);
        world.Player = chest.Pos;
        Require(world.Transfer(chest.Inventory, world.Backpack, "wood", 26) == 26, "worker output should fund the shelter and fire");
        GatherUntil(world, stone, "stone", 12);
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(Terrain.Spawn), 9999);
        world.Player = Terrain.WaterPoint(Terrain.Spawn);
        world.Gather(lake, data.GatherSeconds);
        world.Player = Site(3, 3);
        Require(world.Construction.Build(ThingKind.Hut, Site(5, 3)) != null, "gathered materials should build the opening shelter");
        Require(world.Construction.Build(ThingKind.Campfire, Site(3, 6)) != null, "gathered materials should build the opening fire");
    }
}
