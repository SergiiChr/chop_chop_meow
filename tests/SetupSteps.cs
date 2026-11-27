using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;

/// <summary>
/// Steps that write the raw save or a player's goods and needs.
/// Each is for a state real play cannot reach or cannot reach quickly enough, and says why.
/// <c>./build.py check</c> fails on the raw save anywhere else in the checks.
/// Checks also set fields directly on things and workers they placed and on players they build as save text.
/// Placing the player with <c>world.Player</c>, or <see cref="Warp"/> on a server's world, and holding a player's input are allowed too.
/// </summary>
internal static class SetupSteps {
    /// <summary>Whether a backpack holds what a new guest starts with and nothing else.</summary>
    public static bool HoldsNewcomerKit(InventoryView bag) => bag.Count == 2 && bag["steel_axe"] == 1 && bag["empty_bottle"] == 1;

    public static bool HoldsNewcomerKit(Inventory bag) => HoldsNewcomerKit(new InventoryView(bag));

    /// <summary>Puts goods in the backpack: gathering or buying each would take a day of ticks, and many goods are not sold.</summary>
    public static int Give(World world, string item, int count = 1) => Give(world.S.Actor, item, count);

    /// <summary>Reveals map entries without crafting a chain and returns how to remove only these fixture discoveries.</summary>
    public static Action TemporarilyDiscover(World world, params string[] items) {
        var known = world.S.Obtained;
        var added = new List<string>(items.Length);
        foreach (string item in items) if (known.Add(item)) added.Add(item);
        return () => { foreach (string item in added) known.Remove(item); };
    }

    /// <summary>Puts freshly caught firebugs in the backpack, without finding and catching each at night.</summary>
    public static int GiveFirebugs(World world, int count) => world.Firebugs.Catch(world.S.Backpack, count);

    /// <summary>Puts goods in one player's backpack, for the same reason.</summary>
    public static int Give(PlayerState player, string item, int count = 1) => player.Backpack.Add(item, count);

    /// <summary>Puts a list of goods in the backpack, such as a building's cost.</summary>
    public static void Give(World world, IReadOnlyDictionary<string, int> goods) { foreach (var (item, count) in goods) Give(world, item, count); }

    /// <summary>Puts newly caught firebugs in a guest's backpack for a network check of what happens after a catch.</summary>
    public static int GiveFirebugs(World world, PlayerState player, int count) => world.Firebugs.Catch(player.Backpack, count);

    /// <summary>Takes goods out of the backpack to clear the ground for the next check: the game can only move them somewhere else.</summary>
    public static int Drop(World world, string item, int count = int.MaxValue) => Drop(world.S.Actor, item, count);

    /// <summary>Takes goods out of one player's backpack, for the same reason.</summary>
    public static int Drop(PlayerState player, string item, int count = int.MaxValue) => player.Backpack.Remove(item, count);

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

    /// <summary>Moves the player on a server's world as a respawn does: a window walks its own player, and only takes a move the server counts as one.</summary>
    public static void Warp(World world, Vector2 at) => Warp(world.Me, at);

    /// <summary>Moves a named player on the server, so that player's window takes the new position.</summary>
    public static void Warp(PlayerState player, Vector2 at) { player.Pos = at; player.Warp++; }

    /// <summary>Puts a wolf where the check needs it: real ones come out of the dens. One placed without a target hunts the local player, as if it had smelled them.</summary>
    public static Wolf PlaceWolf(World world, Wolf wolf) { if (wolf.Target == 0) wolf.Target = world.Me.Id; world.S.Wolves.Add(wolf); return wolf; }

    /// <summary>Puts a wolf out patrolling where the check needs it, hunting nobody until it smells someone.</summary>
    public static Wolf PatrolWolf(World world, Wolf wolf) { world.S.Wolves.Add(wolf); return wolf; }

    /// <summary>Keeps every den shut tonight, at dusk and through every hour after, so a check meets only the wolves it places.</summary>
    public static void ShutDens(World world) { world.S.DensOut = (1 << WolfPatrols.Dens.Length) - 1; world.S.DenHours = 24; }

    /// <summary>Takes wolves away, which the game does only at dawn or on a respawn.</summary>
    public static void RemoveWolves(World world, Predicate<Wolf> match) => world.S.Wolves.RemoveAll(match);

    /// <summary>Removes things from the map: the game has no way to clear a building or a tree off the ground.</summary>
    public static void RemoveThings(World world, Predicate<Thing> match) => world.S.Things.RemoveAll(match);

    /// <summary>
    /// Plants a tree among the tallest or among the shortest.
    /// A tree's height comes from its id, which a check cannot choose, so trees are planted and pulled up until one fits.
    /// </summary>
    public static Thing PlantTree(World world, Vector2 at, bool tall) {
        while (true) {
            var tree = world.Add(ThingKind.Tree, at, 42);
            float height = TreePlanting.Tallness(tree.Id);
            if (tall ? height > .85f : height < .15f) return tree;
            RemoveThings(world, t => t == tree);
        }
    }

    /// <summary>Fells a tree at once, away from the player, and returns the stump it leaves: a woodcutter out of the player's sight would take minutes over it.</summary>
    public static Thing Fell(World world, Thing tree) => world.Fell(tree, world.Player);

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

    /// <summary>Puts every part of the diary in a player's diary: two of them wait for the caves, which no play reaches yet.</summary>
    public static void TakeEveryFind(PlayerState player) {
        foreach (var def in DiaryFindDef.All) if (!player.DiaryFinds.Contains(def.Find)) player.DiaryFinds.Add(def.Find);
    }

    /// <summary>Lays a page fallen off a tree for a player: in play one falls one chop in twenty, which a check of the window cannot wait for.</summary>
    public static Thing DropPage(World world, PlayerState player, Vector2 at) => world.Diary.Drop(player, at);

    /// <summary>Brings up a find's pop-up for a player: in play it turns up one gather in twenty, which a check of the window cannot wait for.</summary>
    public static void TurnUpFind(PlayerState player, DiaryFind find) => player.DiaryFinding = find;

    /// <summary>Marks goals as done: reaching them for real takes a whole playthrough.</summary>
    public static void Achieve(World world, params IEnumerable<string> milestones) => world.S.Milestones.UnionWith(milestones);

    /// <summary>Sets the day's takings exactly: a real sale's price moves with demand.</summary>
    public static void SetDayRevenue(World world, int coins) => world.S.DayRevenue = coins;

    /// <summary>Sets exact market values, so a load that restores them cannot pass on a default.</summary>
    public static void SetMarket(World world, string item, float price, float demand) { world.S.Prices[item] = price; world.S.Demand[item] = demand; }

    /// <summary>Moves the clock without running the settlement, so a check can start at a chosen time.</summary>
    public static void SkipClock(World world, float seconds) => world.S.Time += seconds;

    /// <summary>Eats or drinks an item from one backpack cell, as a hotbar slot bound to that stack does.</summary>
    public static void EatFrom(World world, int cell, string item) {
        world.S.Backpack.PreferredSlot = cell;
        try { world.Consume(item); } finally { world.S.Backpack.PreferredSlot = -1; }
    }

    /// <summary>
    /// Names an item or plan on a hotbar slot, ending any binding it had, as an old save's slot does; the slot then waits for a stack of the item to bind.
    /// The real way is putting a held stack on the slot, which these checks are not about.
    /// </summary>
    public static void SetHotbarSlot(World world, int slot, string item) {
        foreach (var s in world.S.Backpack.Layout) if (s.Hotbar == slot + 1) s.Hotbar = 0;
        world.S.HotbarSlots[slot] = item;
    }

    /// <summary>Adds a worker directly at a spot: hiring needs a signpost and coins, starts them at the post and counts as the hiring goal.</summary>
    public static Worker AddWorker(World world, Vector2 position) {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.Workers.Count + 1}", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    /// <summary>Puts the acting player inside a cabin as a saved game has them, without the walk in.</summary>
    public static void SetSheltered(World world, Thing hut) => world.S.Sheltered = hut.Id;
}
