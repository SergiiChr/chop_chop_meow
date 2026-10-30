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
        Require(packer.Packs == "bread", "a packing station should be set to pack bread");
        Require(world.Holds(packer, "bread"), "a packing station should take its own good");
        Require(world.Holds(packer, "crate"), "a packing station should take empty crates");
        Require(!world.Holds(packer, "berries"), "a packing station should refuse another good");
        packer.Inventory.Add("bread", 13); packer.Inventory.Add("crate", 2);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 10);
        Require(packer.Output!.Count == 0 && packer.Inventory["bread"] == 13, "a packing station should not pack while nobody works it");
        world.Player = packer.Pos + new Vector2(1, 0);
        SafeTick(world, data.PackSeconds * data.CrateCapacity + 1);
        string crate = packer.Output.First()!;
        Require(crate.StartsWith(World.CrateOf("bread") + "_"), $"the player working it should name the crate after its good: {crate}");
        Require(world.Packed(crate)!["bread"] == data.CrateCapacity, $"the player working it should pack a crate of twelve bread: {crate}");
        Require(packer.Inventory["bread"] == 1, $"the packing station should use up one bread: {crate}");
        Require(packer.Inventory["crate"] == 1, $"the packing station should use up one empty crate: {crate}");
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
        Require(world.Holds(pile, otherBread), "a pile of bread crates should hold another bread crate");
        Require(pile.Inventory.Accepts(otherBread),
            "a pile of bread crates should accept another bread crate into its inventory");
        Require(!world.Holds(pile, berryCrate), "a pile of bread crates should not hold a crate of berries");

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
        world.Transfer(world.Backpack, sign.Inventory, crate, 1);
        SafeTick(world, 1);
        Require(world.Orders.Count == 0, "the cart should wait while market slots are still free");
        for (int i = 1; i < World.MarketSlots; i++) world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("berries", 12)), 1);
        SafeTick(world, 1);
        Require(world.Orders.Count == 1, "a full post set to sell on its own should send one cart");
        Require(world.Orders[0].Crates == World.MarketSlots, "the cart should carry every crate of the full post");
        Require(sign.Inventory.Count == 0, "the post should be empty once the cart leaves");
    }

    /// <summary>
    /// A settlement by the lake, run only by workers, sells each end product by cart within a few days.
    /// Workers mine, chop, rake, pick and fetch water; they smelt, forge, hammer, grind, bake, cook and can; and they make crates, pack them and stock the post.
    /// </summary>
    private static void CheckWorkersRunEveryChain(GameData data) {
        var world = EmptyWorld(data);
        var site = new ChainSite(world);
        var meat = site.AddSettlement(Products);
        site.AddGatherers();
        site.AddCrateBenches();
        site.AddCannedStewChain();
        var farms = site.AddBreadChain();
        site.AddSteelPickChain();
        site.AddPackers(Products);

        var sold = new HashSet<string>();
        bool resown = false;
        var seen = new HashSet<TradeOrder>();
        float days = 0, limit = 6 * data.DaySeconds;
        for (; days < limit && !(Products.All(sold.Contains) && resown); days += 5) {
            meat.Inventory.Add("raw_meat", 64 - meat.Inventory["raw_meat"]);
            SafeTick(world, 5);
            foreach (var order in world.Orders) if (!order.Buying && seen.Add(order)) sold.UnionWith(order.Goods.Keys);
            resown |= farms.Any(f => f.FarmStage == FarmStage.Growing && f.Progress < world.GrowSeconds - 60);
        }
        GD.Print($"Production chains: sold {string.Join(", ", sold)} in {days / data.DaySeconds:0.0} days with {world.Workers.Count} workers; farm sown again: {resown}");
        string statuses = string.Join("\n", world.Workers.Select(w => $"  {w.Name} at {world.Find(w.Assignment)?.Kind}: {w.Status}"));
        Require(Products.All(sold.Contains), $"workers should sell every product by cart; sold {string.Join(", ", sold)}.\n{statuses}");
        Require(resown, $"the farm should be sown again from its own seeds and compost fertilizer.\n{statuses}");
        Require(world.Workers.All(w => w.WageDebt <= 0), "the fixture pays every wage");
    }
}
