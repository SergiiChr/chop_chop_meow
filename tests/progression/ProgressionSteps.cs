using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static PlayerSteps;
using static WorldSteps;

/// <summary>Steps of the playthrough from a fresh start: its stages, and selling at the trading post and buying from it.</summary>
internal static class ProgressionSteps {
    /// <summary>Puts full crates of an item on a trading post's cart, sells them and takes the coins once the cart is back.</summary>
    public static void SellCrates(World world, Thing sign, string item, int crates) {
        for (int i = 0; i < crates; i++) LoadCrate(world, sign, item, world.Data.CrateCapacity);
        Require(world.Sell(sign), $"the trading post should send {crates} crates of {item}");
        SafeTick(world, world.Data.TradeSeconds + .2f);
        CollectCoins(world, sign);
    }

    /// <summary>Takes the coins in a trading post's wage and output slots into the backpack.</summary>
    public static void CollectCoins(World world, Thing sign) {
        world.Transfer(sign.Wages!, world.Backpack, Inventory.Coins, sign.Wages![Inventory.Coins]);
        world.Transfer(sign.Output!, world.Backpack, Inventory.Coins, sign.Output![Inventory.Coins]);
    }

    /// <summary>Buys one of an item at a trading post and takes it into the backpack once the cart brings it.</summary>
    public static void BuyOne(World world, Thing sign, string item) {
        Require(world.Coins >= world.LotPrice(item), $"the backpack should hold the {world.LotPrice(item):0.00} coins one {item} costs, not {world.Coins:0.00}");
        Require(world.Buy(sign, item, 1), $"the village should sell a {item}");
        SafeTick(world, world.Data.TradeSeconds + .2f);
        Require(world.Transfer(sign.Output!, world.Backpack, item, 1) == 1, $"the bought {item} should arrive at the trading post");
    }

    /// <summary>A stone chisel from loose stones opens wood crafting by hand, which leads to the bench, its mallet and crates.</summary>
    public static Thing MakeCratesFromGatheredMaterials(World world, Thing tree, Thing stone, Thing bush) {
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
    public static Thing BuyFirstPickaxeWithBerryCrates(World world, Thing iron) {
        var sign = BuildAt(world, ThingKind.Signpost, Site(34, 25));
        SellCrates(world, sign, "berries", 4);
        Require(world.Reached("sold"), "selling crates of berries should meet the sold milestone");
        BuyOne(world, sign, "steel_pick");
        Require(world.GatherBlock(iron) == null, "the bought pickaxe should unlock mining deposits");
        return sign;
    }

    /// <summary>
    /// Two more crates of berries buy the steel hammer the anvil needs, which only the village sells before there are
    /// steel parts.
    /// </summary>
    public static void BuySteelHammerWithBerryCrates(World world, Thing sign) {
        SellCrates(world, sign, "berries", 2);
        BuyOne(world, sign, "steel_hammer");
    }

    /// <summary>
    /// Mined ore and coal become iron bars at a stone furnace and steel at a reinforced one, and the bought steel hammer
    /// hammers steel parts at the anvil for a steel pickaxe at the bench, which also makes two more crates.
    /// </summary>
    public static void MakeSteelToolsFromMinedOre(World world, Thing stone, Thing iron, Thing coal, Thing bench) {
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
    }

    /// <summary>A crate of the steel pickaxe and one of berries sell for enough to hire a worker and pay their first wage.</summary>
    public static HireOffer SellSteelToolForTheFirstHire(World world, Thing sign) {
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
        CollectCoins(world, sign);
        var offer = data.HireOffers[1];
        Require(world.Coins >= offer.Upfront,
                $"returned cart should fund hire: coins={world.Coins:0.0}, upfront={offer.Upfront:0.0}, health={world.Health:0.0}, time={world.Time:0.0}, backpackSteel={world.Backpack["steel_pick"]}");
        return offer;
    }

    /// <summary>
    /// The hired worker, given the player's axe in a tool station, fells wood into a chest, which builds the opening
    /// cabin and campfire.
    /// </summary>
    public static void SupplyOpeningCampWithHiredWorker(World world, Thing sign, HireOffer offer, Thing tree, Thing stone) {
        var data = world.Data;
        var worker = world.Hire(sign, offer);
        Require(worker != null, "sale proceeds should fund a new worker");
        // Selling for the first pickaxe takes the opening day, so a worker hired at night starts in the morning; the night's wolves are not what this checks.
        for (int i = 0; i < 4000 && !world.OnDuty(worker!); i++) { SafeTick(world, Step); RemoveWolves(world, _ => true); }
        var chest = TakeEverything(world, BuildAt(world, ThingKind.Chest, Site(37, 25)));
        // A woodcutter takes an axe from a tool station, so the player racks theirs: a station is 8 planks and 4 stone.
        GatherUntil(world, tree, "wood", 2);
        GatherUntil(world, stone, "stone", 4);
        CraftByHand(world, "planks", 2);
        var station = TakeEverything(world, BuildAt(world, ThingKind.Toolstation, Site(37, 28)));
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
