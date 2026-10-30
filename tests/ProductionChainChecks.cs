using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>
/// Checks that workers alone run every end product from raw resource to a sold cart: canned meat stew, bread, steel pickaxes and berries.
/// The fixture builds the stations, stocks the tool rack, plants a ripening farm and keeps one chest of raw meat full, standing in for a meat source the game does not have yet.
/// The player stays in a cabin far away, so nothing is worked, carried, packed or sold by hand.
/// </summary>
public static class ProductionChainChecks {
    private static readonly string[] Products = { "canned_meat_stew", "bread", "steel_pick", "berries" };

    public static void Run(GameData data) {
        CheckPackingStationTakesOnlyItsGoodAndPacksCrates(data);
        CheckPostsTakeOnlyAskedForCratesAndSendTheCartWhenFull(data);
        CheckWorkersRunEveryChain(data);
    }

    /// <summary>
    /// A packing station packs only while worked, one item at a time, names the crate after its good, and holds a full crate until it is carried out.
    /// Its hauler stocks goods and crates from storage and carries the full crate to the nearest storage asking for crates of that good.
    /// </summary>
    private static void CheckPackingStationTakesOnlyItsGoodAndPacksCrates(GameData data) {
        var world = EmptyWorld(data);
        var packer = world.Add(ThingKind.Packer, new Vector2(20, 20));
        world.Player = packer.Pos + new Vector2(1, 0);
        world.SetPacking(packer, "bread");
        Require(packer.Packs == "bread" && world.Holds(packer, "bread") && world.Holds(packer, "crate") && !world.Holds(packer, "berries"), "a packing station should take only its good and empty crates");
        packer.Inventory.Add("bread", 13); packer.Inventory.Add("crate", 2);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 10);
        Require(packer.Output!.Count == 0 && packer.Inventory["bread"] == 13, "a packing station should not pack while nobody works it");
        world.Player = packer.Pos + new Vector2(1, 0);
        SafeTick(world, data.PackSeconds * data.CrateCapacity + 1);
        string crate = packer.Output.First()!;
        Require(crate.StartsWith(World.CrateOf("bread") + "_") && world.Packed(crate)!["bread"] == data.CrateCapacity && packer.Inventory["bread"] == 1 && packer.Inventory["crate"] == 1,
                $"the player working it should pack a crate of twelve bread, named after its good: {crate}");
        SafeTick(world, 10);
        Require(packer.Output.Count == 1 && packer.Inventory["bread"] == 1, "a full crate should wait in the output and stop packing until it is carried out");

        world.Player = new Vector2(40, 40);
        var general = world.Add(ThingKind.Chest, packer.Pos + new Vector2(2, 0));
        var crates = world.Add(ThingKind.Chest, packer.Pos + new Vector2(6, 0));
        crates.Inventory.InputFilter.Add(World.CrateOf("bread"));
        general.Inventory.Add("bread", 30); general.Inventory.Add("crate", 4);
        var worker = AddWorker(world, packer.Pos + new Vector2(0, 1));
        worker.Wage = 18;
        world.Assign(worker, packer);
        for (float waited = 0; waited < 40 && crates.Inventory[crate] == 0; waited += .1f) world.Tick(.1f);
        Require(crates.Inventory[crate] == 1 && general.Inventory[crate] == 0, "the hauler should carry the full crate past general storage to the chest asking for crates of bread");
        bool Packing() => packer.Output.Count == 1 && world.Packed(packer.Output.First()!)!["bread"] > 1;
        for (float waited = 0; waited < 40 && !Packing(); waited += .1f) world.Tick(.1f);
        Require(Packing() && packer.Inventory["crate"] >= 1,
                "the worker should restock bread and crates and pack the next crate");

        // A pile of packed crates takes more crates of the same good, but no crate of another good.
        var pile = world.Add(ThingKind.Pile, new Vector2(30, 30));
        pile.Inventory.Add(crate, 1); crates.Inventory.Remove(crate, 1);
        world.Tick(.01f);
        string otherBread = PackCrate(world, ("bread", 2)), berryCrate = PackCrate(world, ("berries", 2));
        Require(world.Holds(pile, otherBread) && pile.Inventory.Accepts(otherBread) && !world.Holds(pile, berryCrate), "a pile of bread crates should take another bread crate but not a crate of berries");

        // Changing the good leaves the old goods for the hauler to carry out.
        world.SetPacking(packer, "berries");
        SafeTick(world, 20);
        Require(packer.Inventory["bread"] == 0, "after switching to berries, the hauler should carry the leftover bread out");
    }

    /// <summary>
    /// A trading post is a delivery target only for the crates its filter asks for, and only when it is closer than other storage asking for them.
    /// Set to sell on its own, it sends the cart once every market slot holds a crate.
    /// </summary>
    private static void CheckPostsTakeOnlyAskedForCratesAndSendTheCartWhenFull(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(20, 20));
        var chest = world.Add(ThingKind.Chest, new Vector2(30, 20));
        world.Player = sign.Pos;
        string crate = PackCrate(world, ("berries", 12));
        Require(world.DepositFor(crate, sign.Pos) == chest, "a post without a filter should not take deliveries");
        world.SetFilter(sign, World.CrateOf("berries"), false, true);
        Require(world.DepositFor(crate, sign.Pos) == sign, "a post asking for crates of berries should take them");
        Require(world.DepositFor("berries", sign.Pos) == chest, "a post should never take loose goods");
        chest.Inventory.InputFilter.Add(World.CrateOf("berries"));
        Require(world.DepositFor(crate, chest.Pos) == chest && world.DepositFor(crate, sign.Pos) == sign, "between storage asking for the same crates, the nearer one should win");

        world.SetAutoSell(sign, true);
        world.S.Backpack.TransferTo(sign.Inventory, crate, 1);
        SafeTick(world, 1);
        Require(world.S.Orders.Count == 0, "the cart should wait while market slots are still free");
        for (int i = 1; i < World.MarketSlots; i++) world.S.Backpack.TransferTo(sign.Inventory, PackCrate(world, ("berries", 12)), 1);
        SafeTick(world, 1);
        Require(world.S.Orders.Count == 1 && world.S.Orders[0].Crates == World.MarketSlots && sign.Inventory.Count == 0, "a full post set to sell on its own should send the cart");
    }

    /// <summary>
    /// A settlement by the lake, run only by workers, sells each end product by cart within a few days.
    /// Workers mine, chop, rake, pick and fetch water; they smelt, forge, hammer, grind, bake, cook and can; and they make crates, pack them and stock the post.
    /// </summary>
    private static void CheckWorkersRunEveryChain(GameData data) {
        var world = EmptyWorld(data);
        var origin = Terrain.Lake + Terrain.Seaward * 24 + Terrain.Sideways * -6;
        int next = 0;
        // Buildings on a grid three tiles apart, leaving lanes to walk between them.
        Vector2 Spot() { var p = origin + new Vector2(next % 9 * 3, next / 9 * 3); next++; Require(Terrain.Open(p), $"fixture spot {p} should be open ground"); return p; }
        Thing Place(ThingKind kind, string recipe = "") { var t = world.Add(kind, Spot()); if (recipe != "") world.SetRecipe(t, recipe); return t; }
        void Store(string item) => Place(ThingKind.Chest).Inventory.InputFilter.Add(item);
        Worker Hire(Thing place, params string[] jobs) {
            var w = AddWorker(world, place.Pos + new Vector2(0, 1.2f));
            w.Wage = 18;
            foreach (string job in jobs) world.SetDuty(w, place, job, true);
            return w;
        }

        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(10, 10));
        world.Player = hut.Pos; world.S.Sheltered = hut.Id;
        var sign = Place(ThingKind.Signpost);
        sign.Wages!.Add(Inventory.Coins, 1000);
        world.S.Backpack.Add(Inventory.Coins, 5000);
        foreach (string product in Products) world.SetFilter(sign, World.CrateOf(product), false, true);
        world.SetAutoSell(sign, true);
        var rack = Place(ThingKind.Toolstation);
        rack.Inventory.Add("steel_axe", 2); rack.Inventory.Add("steel_pick", 3); rack.Inventory.Add("rake", 1);
        Place(ThingKind.Campfire);

        // Storage: a chest asking for each material, piles for bottles that take a slot each, and two open piles for anything else.
        foreach (string item in new[] { "wood", "planks", "crate", "iron_ore", "coal", "tin_ore", "iron_bar", "tin_bar", "steel_bar", "steel_parts", "wood_parts", "tin_can",
                     "raw_meat", "meat_stew", "canned_meat_stew", "leaves", "wheat", "wheat_seeds", "grass", "fertilizer", "flour", "dough", "bread", "berries", "steel_pick" }) Store(item);
        var empties = Place(ThingKind.Pile); empties.Inventory.Add("empty_bottle", 8);
        Place(ThingKind.Pile); Place(ThingKind.Pile); Place(ThingKind.Pile);
        var meat = world.S.Things.Single(t => t.Kind == ThingKind.Chest && t.Inventory.InputFilter.Contains("raw_meat"));

        // Raw materials: deposits, a tree, berries, leaves and the lake.
        Hire(world.Add(ThingKind.Tree, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Tree, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Iron, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Coal, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Tin, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Berries, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Leaves, Spot(), 9999), "gather");
        Hire(world.Add(ThingKind.Water, Terrain.WaterPoint(origin), 9999), "gather");

        // Crates from wood.
        var planks = Place(ThingKind.Bench, "planks"); planks.Tools!.Add("steel_chisel", 1); Hire(planks, "craft", "haul");
        var crates = Place(ThingKind.Bench, "crate"); crates.Tools!.Add("steel_hammer", 1); Hire(crates, "craft", "haul");

        // Canned meat stew: stew from meat and water, cans from tin, sealed over a wood fire.
        Hire(Place(ThingKind.Cooktable, "meat_stew"), "haul");
        Hire(Place(ThingKind.Furnace, "tin_bar"), "haul");
        var cans = Place(ThingKind.Bench, "tin_can"); cans.Tools!.Add("steel_hammer", 1); Hire(cans, "craft", "haul");
        Hire(Place(ThingKind.Cannery, "canned_meat_stew"), "haul");

        // Bread: compost from raked leaves, wheat from the farm, flour, dough and the oven.
        var compost = Place(ThingKind.Pile); world.SetCompost(compost, true); world.SetSkipFood(compost, true); Hire(compost, "tend");
        var farms = new[] { Place(ThingKind.Farm), Place(ThingKind.Farm) };
        foreach (var farm in farms) { farm.FarmStage = FarmStage.Growing; farm.Progress = world.GrowSeconds - 30; Hire(farm, "farm", "haul"); }
        var flour = Place(ThingKind.Cooktable, "flour"); flour.Tools!.Add("mortar", 1); Hire(flour, "craft", "haul");
        Hire(Place(ThingKind.Cooktable, "dough"), "haul");
        Hire(Place(ThingKind.Furnace, "bread"), "haul");

        // Steel pickaxes: iron bars, steel at the forge with one on the bellows and one hauling, parts at the anvil, handles and the pickaxe at benches.
        Hire(Place(ThingKind.Furnace, "iron_bar"), "haul");
        var forge = Place(ThingKind.Reinforced);
        Hire(forge, "bellows"); Hire(forge, "haul");
        Hire(Place(ThingKind.Anvil, "steel_parts"), "craft", "haul");
        var handles = Place(ThingKind.Bench, "wood_parts"); handles.Tools!.Add("steel_chisel", 1); Hire(handles, "craft", "haul");
        var picks = Place(ThingKind.Bench, "steel_pick"); picks.Tools!.Add("steel_hammer", 1); Hire(picks, "craft", "haul");

        // One packing station for each product.
        foreach (string product in Products) { var packer = Place(ThingKind.Packer); world.SetPacking(packer, product); Hire(packer, "craft", "haul"); }

        var sold = new HashSet<string>();
        bool resown = false;
        var seen = new HashSet<TradeOrder>();
        float days = 0, limit = 6 * data.DaySeconds;
        for (; days < limit && !(Products.All(sold.Contains) && resown); days += 5) {
            meat.Inventory.Add("raw_meat", 64 - meat.Inventory["raw_meat"]);
            SafeTick(world, 5);
            foreach (var order in world.S.Orders) if (!order.Buying && seen.Add(order)) sold.UnionWith(order.Goods.Keys);
            resown |= farms.Any(f => f.FarmStage == FarmStage.Growing && f.Progress < world.GrowSeconds - 60);
        }
        GD.Print($"Production chains: sold {string.Join(", ", sold)} in {days / data.DaySeconds:0.0} days with {world.S.Workers.Count} workers; farm sown again: {resown}");
        string statuses = string.Join("\n", world.S.Workers.Select(w => $"  {w.Name} at {world.Find(w.Assignment)?.Kind}: {w.Status}"));
        Require(Products.All(sold.Contains), $"workers should sell every product by cart; sold {string.Join(", ", sold)}.\n{statuses}");
        Require(resown, $"the farm should be sown again from its own seeds and compost fertilizer.\n{statuses}");
        Require(world.S.Workers.All(w => w.WageDebt <= 0), "the fixture pays every wage");
    }
}
