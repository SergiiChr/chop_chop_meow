using System;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>Deterministic checks for the playable simulation, independent of the rendered scene.</summary>
public static class SimulationChecks
{
    private const float Step = .25f;

    public static void Run(GameData data)
    {
        CheckInventoryFilters(data);
        CheckStorageRouting(data);
        CheckWorkerGatherAndRetarget(data);
        CheckWorkerDetoursAroundPond(data);
        CheckCampfireWorkerFuel(data);
        CheckWorkerFurnaceCrew(data);
        CheckTradeAndWages(data);
        CheckMarketRoundTrip(data);
        CheckRecoveryBoxesStayPrivate(data);
        CheckRecipeSwitchLeftovers(data);
        CheckDeathAndSaveState(data);
        CheckFuelAndHeatGates(data);
        CheckProcessingOutputCapacity(data);
        CheckToolGatesAndPlacement(data);
        CheckNightAndSurvival(data);
        CheckSleepAdvancesWorld(data);
        CheckActualProgression(data);
        GD.Print("SELF-TEST PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Simulation check failed: {message}");
    }

    private static World EmptyWorld(GameData data)
    {
        var world = new World(data, false);
        world.S.Hunger = 100;
        world.S.Thirst = 100;
        world.S.Health = 100;
        world.S.Time = 55;
        return world;
    }

    private static void SafeTick(World world, float seconds)
    {
        world.S.Hunger = 100;
        world.S.Thirst = 100;
        while (seconds > 0)
        {
            float dt = Math.Min(Step, seconds);
            world.Tick(dt);
            seconds -= dt;
            world.S.Hunger = 100;
            world.S.Thirst = 100;
        }
    }

    private static Worker AddWorker(World world, Vector2 position)
    {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.S.Workers.Count + 1}", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    private static void CheckInventoryFilters(GameData data)
    {
        var source = new Inventory { Slots = 1 };
        var destination = new Inventory { Slots = 1 };
        source.Add("iron_bar", 4);
        source.OutputFilter.Add("wood");
        destination.InputFilter.Add("wood");
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated withdrawal must respect the source output filter");
        Require(source["iron_bar"] == 4 && destination["iron_bar"] == 0,
                "blocked automated withdrawal must leave both inventories unchanged");

        source.OutputFilter.Clear();
        source.OutputFilter.Add("iron_bar");
        destination.InputFilter.Clear();
        destination.InputFilter.Add("wood");
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated delivery must respect the destination input filter");
        destination.InputFilter.Add("iron_bar");
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 3,
                "matching source and destination filters should permit transfer");
        Require(source["iron_bar"] == 1 && destination["iron_bar"] == 3,
                "permitted transfer should move exactly the requested quantity");

        destination.InputFilter.Clear();
        Require(source.TransferTo(destination, "iron_bar", 1) == 1,
                "manual transfers should not be blocked by automation filters");
    }

    private static void CheckStorageRouting(GameData data)
    {
        var world = EmptyWorld(data);
        var nearestGeneral = world.Add("chest", new Vector2(11, 10));
        var explicitWood = world.Add("chest", new Vector2(17, 10));
        explicitWood.Inventory.InputFilter.Add("wood");
        Require(world.DepositFor("wood", new Vector2(10, 10))?.Id == explicitWood.Id,
                "explicitly accepting storage should outrank a nearer general chest");
        Require(world.DepositFor("stone", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "an unrestricted chest should accept items not claimed by a filtered chest");

        nearestGeneral.Inventory.Add("wood", 4);
        nearestGeneral.Inventory.OutputFilter.Add("stone");
        Require(world.SourceFor("wood", new Vector2(10, 10)) == null,
                "workers must not withdraw items forbidden by the source output filter");
        nearestGeneral.Inventory.OutputFilter.Add("wood");
        Require(world.SourceFor("wood", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "workers should use a chest once its output filter allows withdrawal");
    }

    private static void CheckWorkerGatherAndRetarget(GameData data)
    {
        var world = EmptyWorld(data);
        var tree = world.Add("tree", new Vector2(10, 10), 5);
        var general = world.Add("chest", new Vector2(8.5f, 10));
        var filtered = world.Add("chest", new Vector2(13, 10));
        filtered.Inventory.InputFilter.Add("wood");
        var worker = AddWorker(world, tree.Pos);
        world.Assign(worker, tree);

        SafeTick(world, data.GatherSeconds * 1.6f + .3f);
        Require(worker.Bag["wood"] == 5 && tree.Remaining == 0,
                "gathering worker should collect actual wood from its assigned resource");
        SafeTick(world, .1f);
        Require(worker.Destination == filtered.Id,
                "worker should route gathered wood to the explicitly accepting chest");

        // The worker already chose this destination and is walking with a load. A filter edit must
        // be applied before the eventual deposit, with the carrying bag kept intact during reroute.
        filtered.Inventory.InputFilter.Clear();
        filtered.Inventory.InputFilter.Add("stone");
        SafeTick(world, .1f);
        Require(worker.Destination == general.Id && worker.Bag["wood"] == 5,
                "worker should retarget in transit when the destination filter changes");
        for (int i = 0; i < 32 && general.Inventory["wood"] == 0; i++) SafeTick(world, .25f);
        Require(general.Inventory["wood"] == 5 && filtered.Inventory["wood"] == 0 && worker.Bag["wood"] == 0,
                "retargeted load should reach the newly accepting chest without leaking into the old one");
    }

    private static void CheckWorkerFurnaceCrew(GameData data)
    {
        var world = EmptyWorld(data);
        var furnace = world.Add("reinforced", new Vector2(20, 20));
        var chest = world.Add("chest", new Vector2(23, 20));
        chest.Inventory.Add("iron_bar", 4);
        chest.Inventory.Add("coal", 6);
        var bellowsWorker = AddWorker(world, furnace.Pos + new Vector2(.7f, .2f));
        var hauler = AddWorker(world, chest.Pos);
        world.Assign(bellowsWorker, furnace);
        world.Assign(hauler, furnace);

        bool sawHauling = false;
        for (int i = 0; i < 600 && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] == 0; i++)
        {
            SafeTick(world, .25f);
            sawHauling |= hauler.Destination == furnace.Id || hauler.Bag.Count > 0;
        }

        Require(bellowsWorker.Assignment == furnace.Id && furnace.Heat > 0,
                "the first of two reinforced-furnace workers should operate the bellows");
        Require(sawHauling && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] > 0,
                "the second furnace worker should haul recipe inputs, collect actual steel, and return it to storage");
        Require(chest.Inventory["iron_bar"] < 4 && chest.Inventory["coal"] < 6,
                "hauling should remove ingredients from the source chest");
    }

    private static void CheckWorkerDetoursAroundPond(GameData data)
    {
        var world = EmptyWorld(data);
        var tree = world.Add("tree", World.Pond + new Vector2(World.PondRadius + 2, 0), 12);
        var chest = world.Add("chest", World.Pond - new Vector2(World.PondRadius + 1.5f, 0));
        var worker = AddWorker(world, tree.Pos);
        world.Assign(worker, tree);

        bool stayedOutOfPond = true;
        for (int i = 0; i < 700 && chest.Inventory["wood"] == 0; i++)
        {
            SafeTick(world, .25f);
            stayedOutOfPond &= worker.Pos.DistanceTo(World.Pond) >= World.PondRadius;
        }

        Require(stayedOutOfPond,
                "worker pathing should route around the pond without entering its blocked radius");
        Require(chest.Inventory["wood"] >= worker.Bag.StackSize && worker.Bag["wood"] == 0,
                "worker assigned across the pond should eventually gather wood and deliver it to accepting storage");
    }

    private static void CheckCampfireWorkerFuel(GameData data)
    {
        var world = EmptyWorld(data);
        var fire = world.Add("campfire", new Vector2(20, 20));
        var chest = world.Add("chest", new Vector2(24, 20));
        chest.Inventory.Add("wood", 6);
        chest.Inventory.OutputFilter.Add("stone");
        var worker = AddWorker(world, fire.Pos);
        world.Assign(worker, fire);

        SafeTick(world, 3);
        Require(chest.Inventory["wood"] == 6 && fire.Inventory["wood"] == 0,
                "campfire worker must respect the source chest output filter when fetching fuel");

        chest.Inventory.OutputFilter.Add("wood");
        bool sawFirewoodInTransit = false;
        for (int i = 0; i < 120 && chest.Inventory["wood"] == 6; i++)
        {
            SafeTick(world, .25f);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        for (int i = 0; i < 80 && (worker.Bag["wood"] > 0 || fire.Inventory["wood"] == 0); i++)
        {
            SafeTick(world, .25f);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        Require(sawFirewoodInTransit && chest.Inventory["wood"] == 3,
                "campfire worker should carry three real wood from output-enabled storage");
        SafeTick(world, .25f);
        Require(fire.Inventory["wood"] > 0 && fire.Burn > 0,
                "carried firewood should be deposited into the fire and consumed to sustain its burn");
        int storedBeforeBurn = fire.Inventory["wood"];
        SafeTick(world, 61);
        Require(fire.Inventory["wood"] < storedBeforeBurn && fire.Burn > 0,
                "campfire should consume stored wood when its burn falls low");
    }

    private static void CheckTradeAndWages(GameData data)
    {
        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        world.Player = sign.Pos;
        world.S.Coins = 100;
        float coinsBeforeBuy = world.S.Coins;
        Require(world.Trade(sign, "wood", 1, true), "market should accept an affordable buy order");
        Require(world.S.Coins < coinsBeforeBuy && world.S.Orders.Count == 1 && world.S.Backpack["wood"] == 0,
                "buy order should reserve coins immediately while goods remain in transit");
        SafeTick(world, 29.9f);
        Require(world.S.Orders.Count == 1 && world.S.Backpack["wood"] == 0,
                "cart must not deliver before the full 30-second trip");
        SafeTick(world, .2f);
        Require(world.S.Orders.Count == 0 && world.S.Backpack["wood"] == 1,
                "cart should deliver the purchased item after its trip");

        world.S.Backpack.Add("wood", 2);
        float coinsBeforeSale = world.S.Coins;
        Require(world.Trade(sign, "wood", 3, false), "market should accept items held by the player");
        Require(world.S.Backpack["wood"] == 0 && world.S.Coins == coinsBeforeSale,
                "sale removes goods at dispatch but does not pay before the cart returns");
        float loweredDemand = world.S.Demand["wood"];
        Require(loweredDemand < 1, "selling should lower demand");
        SafeTick(world, 30.1f);
        Require(world.S.Orders.Count == 0 && world.S.Coins > coinsBeforeSale,
                "sale proceeds should arrive only when the cart returns");
        Require(world.S.Demand["wood"] > loweredDemand && world.S.Demand["wood"] < 1,
                "market demand should gradually recover toward its baseline");

        var worker = world.Hire(sign);
        Require(worker != null, "player should be able to hire an individually tracked worker");
        var workTarget = world.Add("tree", new Vector2(12, 10), 6);
        world.Assign(worker!, workTarget);
        world.S.Coins = 0;
        world.S.Time = data.DaySeconds - .1f;
        SafeTick(world, .2f);
        Require(worker!.WageDebt == data.DailyWage && workTarget.Remaining == 6 && worker.Bag.Count == 0,
                "an unpaid dawn wage should put the worker on strike");
        world.S.Coins = data.DailyWage - 1;
        world.SettleDebt(worker);
        Require(worker.WageDebt == data.DailyWage, "insufficient funds must not partly clear wage debt");
        world.S.Coins = data.DailyWage;
        world.SettleDebt(worker);
        Require(worker.WageDebt == 0, "paying the full owed wage should clear the strike debt");
        world.S.Coins = 0;
        world.S.Time = data.DaySeconds * (world.Day + 1) - .1f;
        SafeTick(world, .2f);
        world.S.Time = data.DaySeconds * (world.Day + 1) - .1f;
        SafeTick(world, .2f);
        Require(worker.WageDebt == data.DailyWage, "a striking worker should not keep adding wages to the debt");
    }

    private static void CheckMarketRoundTrip(GameData data)
    {
        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        world.Player = sign.Pos;
        world.S.Coins = 20000;
        Require(world.Trade(sign, "steel_tools", 100, true), "market should accept a large affordable purchase");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.Trade(sign, "steel_tools", 100, false), "market should accept the purchased goods back");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.S.Coins < 20000, "buying goods and selling them straight back must lose coins");
        float price = data.Items["steel_tools"].Price;
        Require(world.S.Coins < 20000 - 100 * price * 1.25f + 100 * price,
                "a bulk sale must be paid less than its full pre-sale price for every unit");
    }

    private static void CheckRecoveryBoxesStayPrivate(GameData data)
    {
        var world = EmptyWorld(data);
        world.Player = new Vector2(12, 10);
        world.S.Backpack.Add("wood", 5);
        world.Die();
        var box = world.S.Things.Last(t => t.Kind == "grave");
        world.S.Milestones.Add("pick");
        var tree = world.Add("tree", new Vector2(13, 10), 30);
        var chest = world.Add("chest", new Vector2(20, 10));
        var worker = AddWorker(world, new Vector2(14, 10));
        world.Assign(worker, tree);
        SafeTick(world, 25);
        Require(box.Inventory["wood"] == 5 && chest.Inventory["wood"] > 0,
                "workers must store gathered goods in chests, not in the player's recovery box");

        var furnace = world.Add("furnace", new Vector2(13, 13));
        world.Player = box.Pos;
        world.S.Backpack.Add("iron_ore", 4);
        world.Die();
        var oreBox = world.S.Things.Last(t => t.Kind == "grave");
        world.Assign(worker, furnace);
        SafeTick(world, 15);
        Require(oreBox.Inventory["iron_ore"] == 4, "workers must not haul inputs out of a recovery box");

        world.Player = box.Pos;
        foreach (var id in box.Inventory.Items.Keys.ToArray()) box.Inventory.TransferTo(world.S.Backpack, id, 999);
        SafeTick(world, .1f);
        Require(world.S.Things.All(t => t.Id != box.Id), "an emptied recovery box should disappear");
    }

    private static void CheckRecipeSwitchLeftovers(GameData data)
    {
        var world = EmptyWorld(data);
        var anvil = world.Add("anvil", new Vector2(10, 10));
        world.Player = anvil.Pos + Vector2.One;
        world.SetRecipe(anvil, "iron_axe");
        anvil.Inventory.Add("iron_axe", 1);
        world.SetRecipe(anvil, "iron_pick");
        world.Collect(anvil);
        Require(world.S.Backpack["iron_axe"] == 1 && anvil.Inventory["iron_axe"] == 0,
                "collecting should also return goods made by a previous recipe");

        anvil.Inventory.Add("iron_axe", 1);
        var chest = world.Add("chest", new Vector2(14, 10));
        var worker = AddWorker(world, new Vector2(11, 11));
        world.Player = new Vector2(30, 30);
        world.Assign(worker, anvil);
        SafeTick(world, 10);
        Require(chest.Inventory["iron_axe"] == 1, "station workers should haul leftovers from a previous recipe to storage");
    }

    private static void CheckDeathAndSaveState(GameData data)
    {
        var world = EmptyWorld(data);
        world.Player = new Vector2(9, 12);
        world.S.Backpack.Add("iron_ore", 7);
        world.S.Backpack.Add("berries", 3);
        world.Die();
        var grave = world.S.Things.LastOrDefault(t => t.Kind == "grave");
        Require(grave != null && grave.Pos == new Vector2(9, 12) && grave.Inventory["iron_ore"] == 7,
                "death should place carried items in a recoverable box at the death location");
        Require(world.Dead && world.S.RespawnIn == data.RespawnSeconds && world.S.Backpack.Count == 0,
                "death should start the respawn timer and replace the emptied backpack");
        world.MovePlayer(Vector2.One);
        Require(world.Player == new Vector2(9, 12), "a collapsed player must not move before respawning");
        SafeTick(world, data.RespawnSeconds - .5f);
        Require(world.Dead && world.Player == new Vector2(9, 12), "the player must stay down for the whole respawn timer");
        SafeTick(world, 1);
        Require(!world.Dead && world.Player == World.Spawn && world.S.Health == 100,
                "the player should respawn healthy at the clearing once the timer ends");

        var saved = EmptyWorld(data);
        var furnace = saved.Add("furnace", new Vector2(14, 17));
        furnace.Inventory.Add("wood", 2);
        furnace.Processing = true;
        furnace.Progress = 2.5f;
        saved.S.Orders.Add(new TradeOrder { Item = "wood", Quantity = 4, Buying = true, Value = 7.5f, Elapsed = 17, Signpost = 31 });
        saved.S.Demand["wood"] = 1.4f;
        var worker = AddWorker(saved, new Vector2(15, 18));
        worker.Bag.Add("stone", 2);
        worker.Destination = furnace.Id;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(saved.Serialize());
        var loadedFurnace = loaded.Find(furnace.Id);
        Require(loadedFurnace != null && loadedFurnace.Processing && Math.Abs(loadedFurnace.Progress - 2.5f) < .01f,
                "save/load should preserve a batch already in progress");
        Require(loaded.S.Orders.Count == 1 && Math.Abs(loaded.S.Orders[0].Elapsed - 17) < .01f,
                "save/load should preserve an order and its elapsed cart time");
        Require(loaded.S.Workers.Count == 1 && loaded.S.Workers[0].Bag["stone"] == 2 &&
                loaded.S.Workers[0].Destination == furnace.Id,
                "save/load should preserve worker cargo and its current destination");
        Require(Math.Abs(loaded.S.Demand["wood"] - 1.4f) < .01f,
                "save/load should preserve market demand");
    }

    private static void CheckFuelAndHeatGates(GameData data)
    {
        var world = EmptyWorld(data);
        var basic = world.Add("furnace", new Vector2(10, 10));
        var woodCost = world.BatchCost(basic);
        Require(woodCost.TryGetValue("wood", out var basicWood) && basicWood == 1 && !woodCost.ContainsKey("coal"),
                "basic furnace should accept wood as its initial fuel");
        basic.Fuel = "coal";
        var coalCost = world.BatchCost(basic);
        Require(coalCost.TryGetValue("coal", out var basicCoal) && basicCoal == 1 && !coalCost.ContainsKey("wood"),
                "basic furnace should also accept coal as fuel");

        var reinforced = world.Add("reinforced", new Vector2(15, 10));
        reinforced.Fuel = "wood"; // Even malformed or edited state must not make wood a valid steel fuel.
        var steelCost = world.BatchCost(reinforced);
        Require(steelCost.TryGetValue("coal", out var steelCoal) && steelCoal == 3 && !steelCost.ContainsKey("wood"),
                "reinforced steel batches should always require coal, including fuel, even if Fuel is tampered with");
        reinforced.Inventory.Add("iron_bar", 2);
        reinforced.Inventory.Add("coal", 3);
        world.Player = reinforced.Pos;
        SafeTick(world, 12);
        Require(!reinforced.Processing && reinforced.Progress == 0 && reinforced.Inventory["iron_bar"] == 2 && reinforced.Inventory["coal"] == 3,
                "reinforced furnace must leave a complete steel batch untouched while below 80 degrees");
        reinforced.Heat = 100;
        SafeTick(world, .25f);
        Require(reinforced.Processing && reinforced.Inventory["iron_bar"] == 0 && reinforced.Inventory["coal"] == 0,
                "once hot enough, the reinforced furnace should consume the retained batch inputs");
    }

    private static void CheckProcessingOutputCapacity(GameData data)
    {
        var world = EmptyWorld(data);
        var furnace = world.Add("furnace", new Vector2(10, 10));
        furnace.Inventory.Slots = 2;
        world.Player = furnace.Pos;
        world.S.Backpack.Add("iron_ore", 2);
        world.S.Backpack.Add("wood", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f);
        Require(furnace.Processing && furnace.Inventory.Count == 0,
                "furnace should consume a valid batch before cooking begins");

        furnace.Inventory.Add("stone", 128);
        float cookSeconds = data.Recipes["iron_bar"].Seconds;
        world.Tick(cookSeconds + 1);
        Require(furnace.Processing && Math.Abs(furnace.Progress - cookSeconds) < .01f && furnace.Inventory["iron_bar"] == 0,
                "a completed batch must remain pending at full progress when the station has no output capacity");

        furnace.Inventory.Remove("stone", 64);
        world.Tick(.25f);
        Require(!furnace.Processing && furnace.Progress == 0 && furnace.Inventory["iron_bar"] == 1,
                "pending output should be added exactly once as soon as one slot becomes available");
    }

    private static void CheckToolGatesAndPlacement(GameData data)
    {
        var world = EmptyWorld(data);
        var iron = world.Add("iron", new Vector2(10, 10), 20);
        world.S.Milestones.Add("pick");
        Require(world.GatherBlock(iron) != null,
                "the player must still carry a usable pickaxe to mine after previously forging one");
        Require(world.GatherBlock(iron, true) == null,
                "a worker may mine after the settlement has unlocked pickaxe technology");
        world.S.Backpack.Add("iron_pick", 1);
        Require(world.GatherBlock(iron) == null,
                "carrying an iron pickaxe should unlock player mining");
        world.S.Backpack.Remove("iron_pick", 1);
        world.S.Backpack.Add("steel_tools", 1);
        Require(world.GatherBlock(iron) == null,
                "carrying steel tools should also unlock player mining");

        world.S.Backpack.Add("wood", 8);
        Require(world.Build("chest", World.Spawn) == null,
                "building placement must reject the player arrival tile");
        var chest = world.Build("chest", World.Spawn + new Vector2(2, 0));
        Require(chest != null && chest.Kind == "chest" && world.S.Backpack["wood"] == 0,
                "building placement should accept a nearby clear tile and spend its cost");
    }

    private static void CheckNightAndSurvival(GameData data)
    {
        var dark = EmptyWorld(data);
        dark.Player = new Vector2(10, 10);
        dark.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        dark.S.Health = 100;
        dark.S.Wolves.Add(new Wolf { Pos = dark.Player + new Vector2(.8f, 0) });
        SafeTick(dark, .2f);
        Require(dark.S.Health < 100, "an unprotected player near a wolf should take damage at night");

        var lit = EmptyWorld(data);
        lit.Player = new Vector2(10, 10);
        lit.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        lit.S.Health = 100;
        var fire = lit.Add("campfire", lit.Player + new Vector2(3, 0));
        fire.Burn = 90;
        var wolf = new Wolf { Pos = lit.Player + new Vector2(1.3f, 0) };
        lit.S.Wolves.Add(wolf);
        SafeTick(lit, .2f);
        Require(lit.S.Health == 100 && wolf.Pos.DistanceTo(lit.Player) > 1.3f,
                "active campfire light should repel a nearby wolf without harming the player");
        lit.S.Time = data.DaySeconds * .2f;
        SafeTick(lit, .1f);
        Require(lit.S.Wolves.Count == 0,
                "the day cycle should clear wolves when daylight returns");

        foreach (var cause in new[] { "hunger", "thirst" })
        {
            var world = EmptyWorld(data);
            world.Player = new Vector2(12, 13);
            world.S.Health = 1;
            if (cause == "hunger") world.S.Hunger = 0; else world.S.Thirst = 0;
            world.S.Backpack.Add("berries", 2);
            world.S.Backpack.Add("water", 1);
            world.Tick(1);
            var recovery = world.S.Things.LastOrDefault(t => t.Kind == "grave");
            Require(recovery != null && recovery.Pos == new Vector2(12, 13) && recovery.Inventory["berries"] == 2 && recovery.Inventory["water"] == 1,
                    $"{cause} death should recover the player's carried items at the collapse location");
            SafeTick(world, data.RespawnSeconds + .1f);
            Require(world.Player == World.Spawn && world.S.Health == 100,
                    $"{cause} death should respawn the player in a healthy state");
        }
    }

    private static void CheckSleepAdvancesWorld(GameData data)
    {
        var world = EmptyWorld(data);
        world.Player = World.Spawn;
        world.S.Time = data.DaySeconds * (data.NightStarts + .01f);
        world.S.WageDay = world.Day;
        world.S.Coins = data.DailyWage + 3;
        var hut = world.Add("hut", world.Player + new Vector2(2, 0));
        var sign = world.Add("signpost", world.Player + new Vector2(1, 2));
        var furnace = world.Add("furnace", world.Player + new Vector2(-2, 1));
        furnace.Processing = true;
        var order = new TradeOrder { Item = "wood", Quantity = 1, Buying = false, Value = 12, Signpost = sign.Id };
        world.S.Orders.Add(order);
        var worker = AddWorker(world, sign.Pos);
        world.S.Wolves.Add(new Wolf { Pos = world.Player + new Vector2(8, 0) });
        float priorTime = world.S.Time;
        float priorHunger = world.S.Hunger;
        world.Sleep(hut);
        Require(world.S.Time > priorTime && world.Day > 1,
                "sleep should advance the world clock through dawn");
        Require(world.S.Orders.Count == 0 && world.S.Coins == 15,
                "trade settlement and daily wages should both resolve during sleep");
        Require(worker.WageDebt == 0,
                "a funded worker should receive the normal daily wage during sleep");
        Require(!furnace.Processing && furnace.Inventory["iron_bar"] > 0,
                "furnace production should continue while the player sleeps");
        Require(world.S.Wolves.Count == 0 && world.S.Hunger < priorHunger,
                "nighttime sleep should advance survival needs and clear wolves at dawn");
    }

    private static void GatherUntil(World world, Thing resource, string item, int quantity)
    {
        world.Player = resource.Pos;
        int attempts = 0;
        while (world.S.Backpack[item] < quantity && attempts++ < 200)
        {
            int before = world.S.Backpack[item];
            world.Gather(resource, world.Data.GatherSeconds);
            Require(world.S.Backpack[item] > before, $"gathering {item} should produce raw material");
            SafeTick(world, world.Data.GatherSeconds);
        }
        Require(world.S.Backpack[item] >= quantity, $"gathered enough {item} for the progression");
    }

    private static Thing BuildAt(World world, string kind, Vector2 position)
    {
        world.Player = position + new Vector2(-2, 0);
        var built = world.Build(kind, position);
        Require(built != null, $"build {kind} at a clear nearby location");
        return built!;
    }

    private static void CompleteBatches(World world, Thing station, string output, int quantity, bool pump = false)
    {
        int limit = 3000;
        while (station.Inventory[output] < quantity && limit-- > 0)
        {
            if (pump && station.Heat < 90) world.Pump(station);
            SafeTick(world, .25f);
        }
        Require(limit > 0 && station.Inventory[output] >= quantity,
                $"station should complete {quantity} {output} batch(es): kind={station.Kind}, recipe={station.Recipe}, output={station.Inventory[output]}, processing={station.Processing}, progress={station.Progress:0.0}, player={world.Player}, stationPos={station.Pos}, health={world.S.Health:0.0}, time={world.S.Time:0.0}");
    }

    private static void CheckActualProgression(GameData data)
    {
        var world = EmptyWorld(data);
        float startTime = world.S.Time;
        world.S.Backpack.Add("iron_axe", 1);
        world.S.Backpack.Add("berries", 10);
        world.S.Backpack.Add("wood", 6);

        var tree = world.Add("tree", new Vector2(8, 8), 300);
        var stone = world.Add("stone", new Vector2(8, 14), 300);
        var looseIron = world.Add("loose_iron", new Vector2(12, 8), 120);
        GatherUntil(world, tree, "wood", 58);
        GatherUntil(world, stone, "stone", 58);
        GatherUntil(world, looseIron, "iron_ore", 14);

        var furnace = BuildAt(world, "furnace", new Vector2(25, 25));
        world.LoadBatch(furnace, 7);
        Require(furnace.Inventory["iron_ore"] == 14 && furnace.Inventory["wood"] == 7,
                "loading the furnace should transfer the recipe inputs and one fuel per batch");
        CompleteBatches(world, furnace, "iron_bar", 7);
        world.Player = furnace.Pos;
        world.Collect(furnace);
        Require(world.S.Backpack["iron_bar"] == 7, "player should collect bars actually smelted by the furnace");

        var anvil = BuildAt(world, "anvil", new Vector2(28, 25));
        world.SetRecipe(anvil, "iron_pick");
        world.LoadBatch(anvil);
        CompleteBatches(world, anvil, "iron_pick", 1);
        world.Player = anvil.Pos;
        world.Collect(anvil);
        Require(world.S.Backpack["iron_pick"] == 1 && world.S.Milestones.Contains("pick"),
                "forging and collecting an iron pick should unlock mining deposits");

        var iron = world.Add("iron", new Vector2(12, 14), 120);
        var coal = world.Add("coal", new Vector2(16, 8), 120);
        GatherUntil(world, iron, "iron_ore", 18);
        GatherUntil(world, coal, "coal", 6);
        Require(world.S.Backpack["iron_ore"] >= 18 && world.S.Backpack["coal"] >= 6,
                "iron and coal deposits should yield enough resources after the pick is forged");

        world.Player = furnace.Pos;
        world.LoadBatch(furnace, 9);
        CompleteBatches(world, furnace, "iron_bar", 9);
        world.Player = furnace.Pos;
        world.Collect(furnace);
        Require(world.S.Backpack["iron_bar"] == 9,
                "mined iron should be smelted into the later bars needed for steel production");

        var reinforced = BuildAt(world, "reinforced", new Vector2(31, 25));
        BuildAt(world, "campfire", new Vector2(31, 28));
        world.Player = reinforced.Pos;
        world.LoadBatch(reinforced, 2);
        Require(reinforced.Inventory["iron_bar"] == 4 && reinforced.Inventory["coal"] == 6,
                "reinforced furnace should load iron plus mined coal and coal fuel");
        CompleteBatches(world, reinforced, "steel_bar", 2, true);
        world.Player = reinforced.Pos;
        world.Collect(reinforced);
        Require(world.S.Backpack["steel_bar"] == 2,
                "player should pump and collect two steel bars made from real inputs");

        world.SetRecipe(anvil, "steel_tools");
        world.LoadBatch(anvil);
        CompleteBatches(world, anvil, "steel_tools", 1);
        world.Player = anvil.Pos;
        world.Collect(anvil);
        Require(world.S.Backpack["steel_tools"] == 1,
                "anvil should forge steel tools from collected steel bars and wood");

        var sign = BuildAt(world, "signpost", new Vector2(34, 25));
        world.Player = sign.Pos;
        Require(world.Trade(sign, "steel_tools", 1, false), "signpost should dispatch a steel-tool sale");
        float beforeReturn = world.S.Coins;
        SafeTick(world, data.TradeSeconds - .1f);
        Require(world.S.Coins == beforeReturn && world.S.Orders.Count == 1,
                "steel-tool payment should remain in transit until the trade delay elapses");
        SafeTick(world, .2f);
        Require(world.S.Coins >= data.HireCost && world.S.Milestones.Contains("sold_steel"),
                $"returned cart should fund hire and mark the sale: coins={world.S.Coins:0.0}, hireCost={data.HireCost:0.0}, soldSteel={world.S.Milestones.Contains("sold_steel")}, health={world.S.Health:0.0}, time={world.S.Time:0.0}, backpackSteel={world.S.Backpack["steel_tools"]}");

        var worker = world.Hire(sign);
        Require(worker != null, "sale proceeds should fund a new worker");
        var chest = BuildAt(world, "chest", new Vector2(37, 25));
        var workerTree = world.S.Things.FirstOrDefault(t => t.Kind == "tree" && t.Remaining > 0);
        Require(workerTree != null, "a remaining tree should be available for the hired worker");
        world.Assign(worker!, workerTree);
        Require(worker!.Assignment == workerTree!.Id && worker.Job == "gather" && world.S.Milestones.Contains("assigned"),
                "player should assign the hired worker to gather from a resource");
        for (int i = 0; i < 180 && chest.Inventory["wood"] == 0; i++) SafeTick(world, .25f);
        Require(chest.Inventory["wood"] > 0 && worker.Bag["wood"] == 0,
                "hired worker should gather actual wood and deliver it to a built chest");
        Require(world.S.Goal == 11, "objective chain should reach the first automation milestone");
        GD.Print($"Progression simulation elapsed: {world.S.Time - startTime:0} seconds.");
    }
}
