using System;
using System.Collections.Generic;
using ChopChopMeow;

/// <summary>
/// Steps that write the raw save or a player's goods and needs.
/// Each is for a state real play cannot reach or cannot reach quickly enough, and says why.
/// Outside them, only <c>AddWorker</c> and old-save fixtures use the raw save.
/// Checks also set fields directly on things and workers they placed and on players they build as save text.
/// Placing the player with <c>world.Player</c> and holding a player's input are allowed too.
/// </summary>
internal static class SetupSteps {
    /// <summary>Puts goods in the backpack: gathering or buying each would take a day of ticks, and many goods are not sold.</summary>
    public static int Give(World world, string item, int count = 1) => Give(world.S.Actor, item, count);

    /// <summary>Puts goods in one player's backpack, for the same reason.</summary>
    public static int Give(PlayerState player, string item, int count = 1) => player.Backpack.Add(item, count);

    /// <summary>Takes goods out of the backpack to clear the ground for the next check: the game can only move them somewhere else.</summary>
    public static int Drop(World world, string item, int count = int.MaxValue) => world.S.Backpack.Remove(item, count);

    /// <summary>Empties the backpack: the game can only move goods somewhere else.</summary>
    public static void EmptyBackpack(World world) => world.S.Backpack.Clear();

    /// <summary>Sets the backpack's size so a check can fill it with a few goods: in the game it is always 24 slots.</summary>
    public static void SetBackpackSlots(World world, int slots) => world.S.Backpack.Slots = slots;

    /// <summary>Sets coins exactly: wages and sales only change them by amounts the check does not control.</summary>
    public static void SetCoins(World world, int coins) {
        Drop(world, Inventory.Coins);
        Give(world, Inventory.Coins, coins);
    }

    /// <summary>Sets needs: hunger and thirst only fall together with the clock and health falls only with them, so ticking cannot set one alone.</summary>
    public static void SetNeeds(World world, float? hunger = null, float? thirst = null, float? health = null) => SetNeeds(world.S.Actor, hunger, thirst, health);

    /// <summary>Sets one player's needs, for the same reason.</summary>
    public static void SetNeeds(PlayerState player, float? hunger = null, float? thirst = null, float? health = null) {
        if (hunger is float h) player.Hunger = h;
        if (thirst is float t) player.Thirst = t;
        if (health is float life) player.Health = life;
    }

    /// <summary>Tops up hunger and thirst for a run that is about something else, which would otherwise need a supply of food.</summary>
    public static void Nourished(World world) => SetNeeds(world, 100, 100);

    /// <summary>Restores every need for a run that is about something else.</summary>
    public static void Fed(World world) => SetNeeds(world, 100, 100, 100);

    /// <summary>Tops up every player, each with their own needs that only eating restores.</summary>
    public static void NourishEveryone(World world) { foreach (var p in world.Players) SetNeeds(p, 100, 100); }

    /// <summary>Puts a wolf where the check needs it: real ones arrive at random out of sight.</summary>
    public static Wolf PlaceWolf(World world, Wolf wolf) { world.S.Wolves.Add(wolf); return wolf; }

    /// <summary>Takes wolves away, which the game does only at dawn or on a respawn.</summary>
    public static void RemoveWolves(World world, Predicate<Wolf> match) => world.S.Wolves.RemoveAll(match);

    /// <summary>Removes things from the map: the game has no way to clear a building or a tree off the ground.</summary>
    public static void RemoveThings(World world, Predicate<Thing> match) => world.S.Things.RemoveAll(match);

    /// <summary>Sets a cart on the road with an exact value and trip time, which a real sale's changing price would not give.</summary>
    public static TradeOrder PlaceOrder(World world, TradeOrder order) { world.S.Orders.Add(order); return order; }

    /// <summary>Pins the player down as a wolf's leap does: the real leap also kills within seconds, and this check needs them alive.</summary>
    public static void SetMauled(World world, bool mauled) => world.S.Mauled = mauled;

    /// <summary>Calls the wolves off a pinned player, since a real mauling ends in death; runs in allocation checks, so it loops by index.</summary>
    public static void FreePlayer(World world) {
        SetMauled(world, false);
        for (int i = 0; i < world.Wolves.Count; i++) { var w = world.Wolves[i]; w.Leap = 0; w.Feeding = w.Pinning = false; }
    }

    /// <summary>Opens a mine at once: the real way is days of a crew digging, hauling and framing at the cliff, for each ore.</summary>
    public static void OpenMine(Thing mine) => mine.MineStage = MineStage.Open;

    /// <summary>Starts a respawn timer of a set length: dying always sets the full length and drops the backpack.</summary>
    public static void SetRespawn(World world, float seconds) => SetRespawn(world.S.Actor, seconds);

    /// <summary>Starts one player's respawn timer, for the same reason.</summary>
    public static void SetRespawn(PlayerState player, float seconds) => player.RespawnIn = seconds;

    /// <summary>Marks goals as done: reaching them for real takes a whole playthrough.</summary>
    public static void Achieve(World world, params IEnumerable<string> milestones) => world.S.Milestones.UnionWith(milestones);

    /// <summary>Sets the day's takings exactly: a real sale's price moves with demand.</summary>
    public static void SetDayRevenue(World world, int coins) => world.S.DayRevenue = coins;

    /// <summary>Sets exact market values, so a load that restores them cannot pass on a default.</summary>
    public static void SetMarket(World world, string item, float price, float demand) { world.S.Prices[item] = price; world.S.Demand[item] = demand; }

    /// <summary>Moves the clock without ticks: two real days would refill the market a check is watching, so only the age of the news changes.</summary>
    public static void SkipClock(World world, float seconds) => world.S.Time += seconds;

    /// <summary>Puts an item on a hotbar slot: the real way is a drag from the open backpack, which these checks are not about.</summary>
    public static void SetHotbarSlot(World world, int slot, string item) => world.S.HotbarSlots[slot] = item;
}
