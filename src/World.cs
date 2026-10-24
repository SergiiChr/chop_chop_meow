using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

public sealed class Thing
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public int Remaining { get; set; }
    public Inventory Inventory { get; set; } = new();
    public string Recipe { get; set; } = "";
    public string Fuel { get; set; } = "wood";
    public float Progress { get; set; }
    public bool Processing { get; set; }
    public float Heat { get; set; }
    public float Burn { get; set; }
    public float Regrow { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
    [JsonIgnore] public bool IsResource => IsLoose || Kind is "tree" or "stone" or "iron" or "coal" or "berries" or "water" or "wolf_corpse";
    /// <summary>Loose pieces are picked up whole in one go and vanish, instead of being worked down like a deposit.</summary>
    [JsonIgnore] public bool IsLoose => Kind is "loose_iron" or "loose_coal";
    [JsonIgnore] public bool IsStorage => Kind is "chest" or "grave";
}
/// <summary>An impassable rock face whose ore deposits sit at its foot, with loose pieces scattered toward the clearing.</summary>
public readonly record struct Cliff(string Ore, Vector2 Center, Vector2 Face, float HalfLength)
{
    public const float Depth = 1.4f;
    public Vector2 Along => new(-Face.Y, Face.X);
    /// <summary>Distance to the cliff's spine, so the whole wall blocks movement rather than only its middle.</summary>
    public float DistanceTo(Vector2 p) => p.DistanceTo(Center + Along * Math.Clamp((p - Center).Dot(Along), -HalfLength, HalfLength));
}
public enum WorkerWorkState { Idle, Gathering, Pumping, Hammering, Blocked }

public sealed class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public int Assignment { get; set; }
    public string Job { get; set; } = "idle";
    public string Status { get; set; } = "Available";
    public Inventory Bag { get; set; } = new() { Slots = 1, StackSize = 5 };
    public int Destination { get; set; }
    public float Timer { get; set; }
    public float WageDebt { get; set; }
    [JsonIgnore] public List<Vector2> Route { get; } = new();
    [JsonIgnore] public Vector2 RouteTarget { get; set; } = new(-100, -100);
    [JsonIgnore] public WorkerWorkState WorkState { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
public sealed class TradeOrder
{
    public string Item { get; set; } = "";
    public int Quantity { get; set; }
    public bool Buying { get; set; }
    public float Value { get; set; }
    public float Elapsed { get; set; }
    public int Signpost { get; set; }
}
public sealed class Wolf
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Health { get; set; } = 4;
    public float Attack { get; set; }
    /// <summary>Seconds left in a throat leap; zero while the wolf runs on the ground.</summary>
    public float Leap { get; set; }
    /// <summary>True once the wolf has reached the downed player and is feeding.</summary>
    public bool Feeding { get; set; }
    /// <summary>The wolf whose leap landed; it holds the player's throat.</summary>
    public bool Pinning { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
public sealed class SaveState
{
    public int Version { get; set; } = 2;
    public List<Thing> Things { get; set; } = new();
    public List<Worker> Workers { get; set; } = new();
    public List<TradeOrder> Orders { get; set; } = new();
    public List<Wolf> Wolves { get; set; } = new();
    public Inventory Backpack { get; set; } = new() { Slots = 24 };
    public Dictionary<string, float> Demand { get; set; } = new();
    public HashSet<string> Milestones { get; set; } = new();
    public float PlayerX { get; set; } = 23;
    public float PlayerY { get; set; } = 24;
    public float Time { get; set; } = 55;
    public float Coins { get; set; }
    public float Health { get; set; } = 100;
    public float Hunger { get; set; } = 100;
    public float Thirst { get; set; } = 100;
    public int NextId { get; set; } = 1;
    public int Goal { get; set; }
    public int WageDay { get; set; } = 1;
    public List<string> HotbarSlots { get; set; } = new() { "iron_axe", "iron_pick", "chest", "campfire", "furnace", "anvil", "reinforced", "berries", "water" };
    public int ActiveHotbar { get; set; }
    public float RespawnIn { get; set; }
    /// <summary>A wolf has pinned the player; it stays set through the following death until respawn.</summary>
    public bool Mauled { get; set; }
    public float PlaySeconds { get; set; }
    public bool EndingShown { get; set; }
}

/// <summary>Authoritative single-player simulation, independent of rendering and UI.</summary>
public sealed class World
{
    public readonly GameData Data;
    public SaveState S { get; private set; } = new();
    public Action<string>? Notify;
    public Action<ActorAction, Vector2>? PlayerActed;
    public const int MapSize = 48;
    public static readonly Vector2 Spawn = new(23, 24);
    public static readonly Vector2 Pond = new(14, 22);
    public const float PondRadius = 4.5f;
    public const float LeapSeconds = .4f;
    // A wolf leaps from within LeapRange and lands PinDistance away, where its jaws reach the throat of the downed player.
    private const float LeapRange = 3.5f, PinDistance = 1.45f, FeastSeat = 1.1f, FeastSpeed = 5.5f;
    // Both faces turn toward the camera so the deposits at their feet stay in view.
    public static readonly Cliff[] Cliffs = { new("iron", new(31, 4), new(0, 1), 4.5f), new("coal", new(4, 40), new(1, 0), 4.5f) };
    public static Cliff CliffOf(string ore) => Cliffs.First(c => c.Ore == ore);
    private float gatherClock;
    private float attackClock;
    private float nightSpawn;
    public World(GameData data, bool generate = true) { Data = data; if (generate) NewGame(); }
    public Vector2 Player { get => new(S.PlayerX, S.PlayerY); set { S.PlayerX = value.X; S.PlayerY = value.Y; } }
    public int Day => (int)(S.Time / Data.DaySeconds) + 1;
    public float DayPhase => S.Time % Data.DaySeconds / Data.DaySeconds;
    public bool Night => DayPhase >= Data.NightStarts;
    public bool Dead => S.RespawnIn > 0;
    /// <summary>Dead or pinned by a wolf: the player cannot move or act.</summary>
    public bool Downed => Dead || S.Mauled;
    public bool Near(Thing t, float radius = 3.2f) => !Downed && Player.DistanceTo(t.Pos) <= radius;
    public Thing? Find(int id) => S.Things.FirstOrDefault(t => t.Id == id);
    public void Say(string message) => Notify?.Invoke(message);
    public Thing Add(string kind, Vector2 pos, int remaining = 0)
    {
        var t = new Thing { Id = S.NextId++, Kind = kind, Pos = pos, Remaining = remaining };
        if (kind == "furnace") t.Recipe = "iron_bar";
        if (kind == "reinforced") { t.Recipe = "steel_bar"; t.Fuel = "coal"; }
        S.Things.Add(t); return t;
    }
    public void NewGame()
    {
        S = new SaveState();
        S.Backpack.Add("iron_axe", 1); S.Backpack.Add("berries", 10); S.Backpack.Add("wood", 6);
        var rng = new Random(4271);
        foreach (var cliff in Cliffs)
        {
            foreach (float along in new[] { -2.8f, 0, 2.8f }) Add(cliff.Ore, cliff.Center + cliff.Face * 2.6f + cliff.Along * along, 400);
            // Loose pieces stay on the cliff's side of the map but roll a little further out than the deposits.
            // Iron is the tool-free bootstrap: 20 ore covers the anvil and pickaxe but not a bellows furnace too.
            (string kind, int count) = cliff.Ore == "iron" ? ("loose_iron", 10) : ("loose_coal", 6);
            for (int placed = 0; placed < count;)
            {
                var p = cliff.Center + cliff.Face * (4.2f + (float)rng.NextDouble() * 4.5f) + cliff.Along * ((float)rng.NextDouble() * 2 - 1) * (cliff.HalfLength + 1.5f);
                if (!CanStand(p) || p.DistanceTo(Pond) < PondRadius + 1 || S.Things.Any(t => t.Pos.DistanceTo(p) < 1.2f)) continue;
                Add(kind, p, 2); placed++;
            }
        }
        for (int i = 0; i < 110; i++)
        {
            var p = new Vector2(rng.Next(3, 45), rng.Next(3, 45));
            if (p.DistanceTo(Spawn) < 8 || p.DistanceTo(Pond) < PondRadius + 3.5f || Cliffs.Any(c => c.DistanceTo(p) < Cliff.Depth + 2.5f) || Math.Abs(p.Y - 24) < 1.5f) continue;
            // Full-size trees need room for their crowns, so the woods stay walkable and readable.
            if (S.Things.Any(t => t.Pos.DistanceTo(p) < 3.6f)) continue;
            Add("tree", p, 42);
        }
        Add("tree", new(28, 24), 42);
        Vector2[] stones = { new(19, 28), new(22, 30), new(26, 30), new(24, 33), new(18, 32), new(29, 33), new(21, 35) };
        foreach (var p in stones) Add("stone", p, 64);
        foreach (var p in new Vector2[] { new(13, 28), new(16, 30), new(12, 32), new(15, 34), new(10, 29) }) Add("berries", p, 34);
        Add("water", Pond + (Spawn - Pond).Normalized() * (PondRadius + .2f), 9999);
        S.Things.RemoveAll(t => t.Kind == "tree" && S.Things.Any(other => other.Id != t.Id && other.IsResource && other.Kind != "tree" && other.Pos.DistanceTo(t.Pos) < 1.3f));
        Say("Welcome to Bramblewick. Gather wood and stone; build a sheltered workshop.");
    }
    public bool CanStand(Vector2 p)
    {
        if (p.X < 1 || p.Y < 1 || p.X > 46 || p.Y > 46 || p.DistanceTo(Pond) < PondRadius || Cliffs.Any(c => c.DistanceTo(p) < Cliff.Depth)) return false;
        return !S.Things.Any(t => !t.IsResource && t.Kind is not "campfire" and not "grave" and not "signpost" && t.Pos.DistanceTo(p) < Footprint(t.Kind));
    }
    public static float Footprint(string kind) => kind switch { "hut" => 1.55f, "reinforced" => .8f, "furnace" => .65f, "chest" or "anvil" => .45f, _ => .35f };
    public void MovePlayer(Vector2 delta)
    {
        if (Downed) return;
        var p = Player + delta;
        if (CanStand(p)) Player = p;
        else { if (CanStand(Player + new Vector2(delta.X, 0))) Player += new Vector2(delta.X, 0); if (CanStand(Player + new Vector2(0, delta.Y))) Player += new Vector2(0, delta.Y); }
    }
    public string? GatherBlock(Thing t, bool worker = false)
    {
        if (!t.IsResource) return "Select a natural resource.";
        if (t.Remaining <= 0) return "Depleted. Berries regrow; look for another deposit.";
        if (t.Kind == "tree" && !worker && S.Backpack["iron_axe"] == 0 && S.Backpack["steel_tools"] == 0) return "An iron axe is needed. Recover your belongings or forge one.";
        if (t.Kind is "coal" or "iron" && (worker ? !S.Milestones.Contains("pick") : S.Backpack["iron_pick"] == 0 && S.Backpack["steel_tools"] == 0)) return "Forge an iron pickaxe. Loose rusty ore nearby can be picked up by hand.";
        return null;
    }
    public static string ResourceItem(Thing t) => t.Kind switch { "tree" => "wood", "iron" or "loose_iron" => "iron_ore", "loose_coal" => "coal", "wolf_corpse" => "raw_meat", _ => t.Kind };
    public void Gather(Thing t, float dt)
    {
        if (!Near(t)) { gatherClock = 0; return; }
        var blocked = GatherBlock(t);
        if (blocked != null) { gatherClock = 0; return; }
        if (t.IsLoose) { PickUp(t); return; }
        gatherClock += dt;
        float seconds = Data.GatherSeconds * (S.Backpack["steel_tools"] > 0 ? .55f : 1);
        if (gatherClock < seconds) return;
        gatherClock = 0;
        bool small = t.Kind == "wolf_corpse" || t.Kind == "stone" && S.Backpack["iron_pick"] == 0 && S.Backpack["steel_tools"] == 0;
        int amount = Math.Min(t.Remaining, small ? 2 : 4);
        amount = S.Backpack.Add(ResourceItem(t), amount); t.Remaining -= amount;
        if (amount == 0) Say("Backpack full. Store or sell some supplies.");
        else Say($"+{amount} {Data.Name(ResourceItem(t))} · {t.Remaining} left here.");
        if (t.Kind == "water") { S.Thirst = Math.Min(100, S.Thirst + 15); t.Remaining = 9999; }
        if (amount > 0) S.Milestones.Add("gather");
    }
    private void PickUp(Thing t)
    {
        string item = ResourceItem(t);
        int amount = S.Backpack.Add(item, t.Remaining); t.Remaining -= amount;
        if (amount == 0) { Say("Backpack full. Store or sell some supplies."); return; }
        if (t.Remaining <= 0) S.Things.Remove(t);
        Say($"+{amount} {Data.Name(item)}."); S.Milestones.Add("gather");
    }
    public void ResetGather() => gatherClock = 0;
    public string? PlacementBlock(string kind, Vector2 p)
    {
        if (!Data.Buildings.ContainsKey(kind)) return "Unknown building.";
        if (Downed) return "You are still recovering.";
        if (Player.DistanceTo(p) > 5.5f) return "Build within 5 tiles of yourself.";
        if (Player.DistanceTo(p) < 1.2f || Spawn.DistanceTo(p) < 1.2f) return "Leave your feet and the arrival point clear.";
        if (!CanStand(p) || p.DistanceTo(Pond) < PondRadius + .8f) return "Choose dry, open ground.";
        if (S.Things.Any(t => t.Pos.DistanceTo(p) < (t.IsResource ? Footprint(kind) + .65f : Footprint(kind) + Footprint(t.Kind) + .45f) && (t.Remaining > 0 || !t.IsResource))) return "Leave room around resources and buildings.";
        if (!S.Backpack.Has(Data.Buildings[kind].Cost)) return "Missing materials in your backpack.";
        return null;
    }
    public Thing? Build(string kind, Vector2 p)
    {
        var blocked = PlacementBlock(kind, p);
        if (blocked != null) { Say(blocked); return null; }
        S.Backpack.Spend(Data.Buildings[kind].Cost);
        var t = Add(kind, p); if (kind == "campfire") t.Burn = 90;
        S.Milestones.Add(kind); Say($"Built {Data.Name(kind)}."); return t;
    }
    public static bool IsFood(string item) => item is "berries" or "raw_meat" or "cooked_meat";
    public void Consume(string item)
    {
        if (Downed) return;
        if (item == "raw_meat") { Say("Raw meat must be cooked in a furnace first."); return; }
        if (S.Backpack.Remove(item, 1) == 0) { Say($"No {Data.Name(item)} in your backpack."); return; }
        if (item == "berries") { S.Hunger = Math.Min(100, S.Hunger + 18); S.Health = Math.Min(100, S.Health + 5); }
        if (item == "cooked_meat") { S.Hunger = Math.Min(100, S.Hunger + 40); S.Health = Math.Min(100, S.Health + 10); }
        if (item == "water") S.Thirst = Math.Min(100, S.Thirst + 28);
    }
    public void Pump(Thing t)
    {
        if (!Near(t) || t.Kind != "reinforced") return;
        t.Heat = Math.Min(100, t.Heat + 24); PlayerActed?.Invoke(ActorAction.Pump, t.Pos); Say($"Bellows: {t.Heat:0}° / 80° needed for steel.");
    }
    public void FuelFire(Thing t)
    {
        if (!Near(t)) return;
        int n = S.Backpack.Remove("wood", 1);
        if (n > 0) { t.Burn += 90; Say("Campfire fed: +90 seconds of light."); } else Say("You need wood to feed the fire.");
    }
    public void SetRecipe(Thing t, string recipe)
    {
        if (t.Processing) { Say("Wait until the current batch finishes."); return; }
        if (Data.Recipes.TryGetValue(recipe, out var r) && r.Station == t.Kind) { t.Recipe = recipe; t.Progress = 0; }
    }
    public Dictionary<string, int> BatchCost(Thing t)
    {
        var cost = new Dictionary<string, int>();
        if (!Data.Recipes.TryGetValue(t.Recipe, out var r)) return cost;
        foreach (var p in r.Inputs) cost[p.Key] = p.Value;
        if (t.Kind is "furnace" or "reinforced")
        {
            string fuel = t.Kind == "reinforced" ? "coal" : t.Fuel is "wood" or "coal" ? t.Fuel : "wood";
            cost[fuel] = cost.GetValueOrDefault(fuel) + 1;
        }
        return cost;
    }
    public void LoadBatch(Thing t, int batches = 1)
    {
        if (!Near(t)) { Say("Walk closer to load this building."); return; }
        var cost = BatchCost(t);
        if (cost.Count == 0) { Say("Choose a recipe first."); return; }
        for (int i = 0; i < batches; i++)
        {
            if (!S.Backpack.Has(cost)) { if (i == 0) Say("Missing batch ingredients or fuel in backpack."); break; }
            if (cost.Any(pair => t.Inventory[pair.Key] + pair.Value > t.Inventory.StackSize)) { Say("Input slots are full."); break; }
            foreach (var pair in cost) S.Backpack.TransferTo(t.Inventory, pair.Key, pair.Value);
        }
    }
    /// <summary>Manual loading of one input slot, which holds at most one stack.</summary>
    public int LoadInput(Thing t, string item, int quantity)
    {
        if (!Near(t)) { Say("Walk closer to load this building."); return 0; }
        if (!BatchCost(t).ContainsKey(item)) { Say($"{Data.Name(item)} is not used by this recipe."); return 0; }
        int moved = S.Backpack.TransferTo(t.Inventory, item, Math.Min(quantity, t.Inventory.StackSize - t.Inventory[item]));
        if (moved == 0) Say(S.Backpack[item] == 0 ? $"No {Data.Name(item)} in your backpack." : $"The {Data.Name(item)} slot is full.");
        return moved;
    }
    public void Collect(Thing t)
    {
        if (!Near(t)) { Say("Walk closer to collect items."); return; }
        foreach (var item in Leftovers(t).ToArray()) t.Inventory.TransferTo(S.Backpack, item, 999);
    }
    /// <summary>Finished goods plus anything a previous recipe or fuel choice left behind.</summary>
    public IEnumerable<string> Leftovers(Thing t)
    {
        var cost = BatchCost(t);
        return t.Inventory.Items.Keys.Where(item => !cost.ContainsKey(item));
    }
    public void Sleep(Thing hut)
    {
        if (!Near(hut)) return;
        if (!Night) { Say("You can sleep at night. Keep food and water for the morning."); return; }
        // Time, wages, trades, hunger and production advance together; no frozen night exploit.
        float seconds = Data.DaySeconds - S.Time % Data.DaySeconds + 1;
        while (seconds > 0) { float dt = Math.Min(1, seconds); Tick(dt, true); seconds -= dt; }
        S.Wolves.Clear(); Say("Dawn breaks. Your workers continued through the night.");
    }
    public bool Protected(Vector2 p) => S.Things.Any(t => (t.Kind == "campfire" && t.Burn > 0 && t.Pos.DistanceTo(p) < 5.3f) || (t.Kind == "hut" && t.Pos.DistanceTo(p) < 2.7f));
    public void Attack()
    {
        if (attackClock > 0 || Downed) return;
        attackClock = .6f;
        var wolf = S.Wolves.OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
        ActorAction action = S.Backpack["steel_tools"] > 0 ? ActorAction.SteelChop : S.Backpack["iron_axe"] > 0 ? ActorAction.Chop : ActorAction.Attack;
        Vector2 target = wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f ? wolf.Pos : Player;
        PlayerActed?.Invoke(action, target);
        if (wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f)
        {
            wolf.Health -= S.Backpack["steel_tools"] > 0 ? 3 : S.Backpack["iron_axe"] > 0 ? 1.5f : .5f;
            wolf.Pos += (wolf.Pos - Player).Normalized() * 1.2f;
            if (wolf.Health <= 0)
            {
                S.Wolves.Remove(wolf); Add("wolf_corpse", wolf.Pos.Clamp(Vector2.One * 2, Vector2.One * 45), 4);
                Say("The wolf falls. Butcher the carcass for meat and cook it in a furnace.");
            }
        }
    }
    public void Die()
    {
        // After a mauling the box lands past the feet, away from the wolf at the throat, so the feeding stays visible.
        var killer = S.Mauled ? S.Wolves.FirstOrDefault(w => w.Pinning) : null;
        var box = Add("grave", killer == null ? Player : Player + (Player - killer.Pos).Normalized() * 1.5f); box.Inventory = S.Backpack;
        S.Backpack = new Inventory { Slots = 24 }; S.RespawnIn = Data.RespawnSeconds;
        if (S.Mauled)
        {
            // The pack eats every scrap of food and stays to feed until the player respawns.
            foreach (var item in box.Inventory.Items.Keys.Where(IsFood).ToArray()) box.Inventory.Remove(item, box.Inventory[item]);
            Say($"The wolves brought you down and ate all your food. The rest is in a recovery box. Respawning in {Data.RespawnSeconds:0} seconds.");
            return;
        }
        S.Wolves.Clear();
        Say($"You collapsed. Your belongings are in a recovery box where you fell. Respawning in {Data.RespawnSeconds:0} seconds.");
    }
    private void Respawn()
    {
        S.Health = 100; S.Hunger = 65; S.Thirst = 65; Player = Spawn; S.Mauled = false; S.Wolves.Clear();
        Say("You wake in the clearing. Recover your belongings from the box.");
    }
    // Only sales move demand, and purchases always cost more than the best sale price, so a buy-then-sell round trip never profits.
    public float UnitPrice(string item, bool buying) => Data.Items[item].Price * (buying ? 1.25f : S.Demand.GetValueOrDefault(item, 1));
    public bool Trade(Thing sign, string item, int quantity, bool buying)
    {
        if (!Near(sign) || sign.Kind != "signpost" || !Data.Items.ContainsKey(item) || quantity <= 0) return false;
        if (S.Orders.Count >= 3) { Say("All three village carts are busy."); return false; }
        float demand = Math.Min(1, S.Demand.GetValueOrDefault(item, 1)), price = buying ? UnitPrice(item, true) * quantity : 0;
        // A sale lowers demand unit by unit, so a bulk sale is not paid entirely at the pre-sale price.
        for (int i = 0; !buying && i < quantity; i++) { price += Data.Items[item].Price * demand; demand = Math.Max(.25f, demand - .045f); }
        if (buying && S.Coins < price) { Say("Not enough coins."); return false; }
        if (!buying && S.Backpack[item] < quantity) { Say("Not enough items in your backpack."); return false; }
        if (buying) S.Coins -= price; else S.Backpack.Remove(item, quantity);
        S.Orders.Add(new TradeOrder { Item = item, Quantity = quantity, Buying = buying, Value = price, Signpost = sign.Id });
        if (!buying) S.Demand[item] = demand;
        Say($"Cart dispatched: {(buying ? "buying" : "selling")} {quantity} {Data.Name(item)}. Returns in 30 seconds."); return true;
    }
    public Worker? Hire(Thing sign)
    {
        if (!Near(sign) || sign.Kind != "signpost") return null;
        if (S.Workers.Count >= 8) { Say("The POC supports eight workers."); return null; }
        if (S.Coins < Data.HireCost) { Say($"Hiring costs {Data.HireCost:0} coins; wages are {Data.DailyWage:0} per dawn."); return null; }
        S.Coins -= Data.HireCost;
        string[] names = { "Rowan", "Hazel", "Milo", "Fern", "Jasper", "Willow", "Clover", "Ash" };
        string name = names.FirstOrDefault(n => S.Workers.All(w => w.Name != n)) ?? "Neighbor";
        var w = new Worker { Id = S.NextId++, Name = name, Pos = sign.Pos + new Vector2(1, 0) };
        S.Workers.Add(w); S.Milestones.Add("hired"); Say($"{name} joined. Select them → Work → click a resource, or Assign at a building."); return w;
    }
    public void Assign(Worker w, Thing? target)
    {
        w.Assignment = target?.Id ?? 0; w.Job = target == null ? "idle" : target.IsResource ? "gather" : "building"; w.WorkState = WorkerWorkState.Idle;
        w.Destination = 0; w.Timer = 0; w.Status = target == null ? "Available" : "Starting work";
        if (target != null) S.Milestones.Add("assigned");
    }
    public void Fire(Worker w)
    {
        if (w.Bag.Count > 0) { var box = Add("grave", w.Pos); box.Inventory = w.Bag; }
        S.Workers.Remove(w); Say($"{w.Name} has left. Carried items were left in a recovery box.");
    }
    // Only chests take part in automation; recovery boxes hold the player's or a dismissed worker's belongings.
    public Thing? DepositFor(string item, Vector2 origin, int exclude = 0) => S.Things
        .Where(t => t.Kind == "chest" && t.Id != exclude && t.Inventory.Room(item) > 0 && t.Inventory.Accepts(item))
        .OrderBy(t => t.Inventory.InputFilter.Contains(item) ? 0 : 1).ThenBy(t => t.Pos.DistanceSquaredTo(origin)).FirstOrDefault();
    public Thing? SourceFor(string item, Vector2 origin, int exclude = 0) => S.Things
        .Where(t => t.Kind == "chest" && t.Id != exclude && t.Inventory[item] > 0 && t.Inventory.AllowsWithdrawal(item))
        .OrderBy(t => t.Pos.DistanceSquaredTo(origin)).FirstOrDefault();
    private List<Vector2> FindRoute(Vector2 from, Vector2 destination)
    {
        var start = new Vector2I((int)MathF.Round(from.X), (int)MathF.Round(from.Y));
        var queue = new Queue<Vector2I>(); queue.Enqueue(start);
        var previous = new Dictionary<Vector2I, Vector2I> { [start] = start };
        Vector2I? end = null;
        Vector2I[] steps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (((Vector2)current).DistanceTo(destination) <= 1.15f) { end = current; break; }
            foreach (var step in steps)
            {
                var next = current + step;
                if (previous.ContainsKey(next) || !CanStand(next)) continue;
                if (step.X != 0 && step.Y != 0 && (!CanStand(current + new Vector2I(step.X, 0)) || !CanStand(current + new Vector2I(0, step.Y)))) continue;
                previous[next] = current; queue.Enqueue(next);
            }
        }
        var result = new List<Vector2>();
        if (end == null) return result;
        var node = end.Value;
        while (node != start) { result.Add(node); node = previous[node]; }
        result.Reverse(); return result;
    }
    private bool Walk(Worker w, Vector2 destination, float dt)
    {
        if (w.Pos.DistanceTo(destination) < 1.2f) return true;
        if (w.RouteTarget.DistanceTo(destination) > .2f) { w.Route.Clear(); w.RouteTarget = destination; }
        var next = w.Pos.MoveToward(destination, dt * 2.25f);
        if (w.Route.Count == 0 && CanStand(next)) { w.Pos = next; return false; }
        if (w.Route.Count > 0 && !CanStand(w.Route[0])) w.Route.Clear();
        if (w.Route.Count == 0) w.Route.AddRange(FindRoute(w.Pos, destination));
        if (w.Route.Count == 0) { w.Status = "Path blocked · clear a route"; w.WorkState = WorkerWorkState.Blocked; return false; }
        w.Pos = w.Pos.MoveToward(w.Route[0], dt * 2.25f);
        if (w.Pos.DistanceTo(w.Route[0]) < .06f) w.Route.RemoveAt(0);
        return false;
    }
    private void WorkerTick(Worker w, float dt)
    {
        w.WorkState = WorkerWorkState.Idle;
        if (w.WageDebt > 0) { w.Status = "On strike · wages owed"; return; }
        var target = Find(w.Assignment);
        if (w.Bag.Count > 0)
        {
            string item = w.Bag.Items.Keys.First();
            var dest = Find(w.Destination);
            // Re-evaluate rules every tick, including filters changed while a worker is carrying goods.
            if (dest == null || dest.Inventory.Room(item) == 0 || !dest.Inventory.Accepts(item) || (dest.Id == target?.Id && w.Job != "building")) dest = DepositFor(item, w.Pos);
            if (dest == null) { w.Status = $"Holding {Data.Name(item)} · no accepting storage"; return; }
            w.Destination = dest.Id; w.Status = $"Carrying {Data.Name(item)} → {Data.Name(dest.Kind)}";
            if (Walk(w, dest.Pos, dt)) { w.Bag.TransferTo(dest.Inventory, item, 99, true); w.Destination = 0; }
            return;
        }
        if (target == null) { w.Status = "Available"; return; }
        if (target.IsResource)
        {
            string? blocked = GatherBlock(target, true);
            if (blocked != null) { w.Status = blocked; return; }
            string item = ResourceItem(target);
            if (DepositFor(item, target.Pos) == null) { w.Status = "Needs an accepting storage chest"; return; }
            w.Status = $"Gathering {Data.Name(item)}";
            if (!Walk(w, target.Pos, dt)) return;
            w.WorkState = WorkerWorkState.Gathering;
            w.Timer += dt;
            if (target.IsLoose || w.Timer >= Data.GatherSeconds * 1.6f)
            {
                w.Timer = 0; target.Remaining -= w.Bag.Add(item, target.Remaining);
                if (target.Kind == "water") target.Remaining = 9999;
                if (target.IsLoose && target.Remaining <= 0) S.Things.Remove(target);
            }
            return;
        }
        var coworkers = S.Workers.Where(other => other.Assignment == target.Id && other.WageDebt <= 0).OrderBy(other => other.Id).ToList();
        if (target.Kind == "reinforced" && coworkers.Count >= 2 && coworkers[0] == w)
        {
            w.Status = "Operating bellows"; if (Walk(w, target.Pos + new Vector2(.7f, .2f), dt)) { w.WorkState = WorkerWorkState.Pumping; target.Heat = Math.Min(100, target.Heat + dt * 18); }
            return;
        }
        if (target.Kind == "campfire")
        {
            if (target.Burn > 120 || target.Inventory["wood"] > 0) { w.Status = "Tending fire"; Walk(w, target.Pos, dt); return; }
            var source = SourceFor("wood", w.Pos);
            if (source == null) { w.Status = "Needs wood in output-enabled storage"; return; }
            if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, "wood", 3, true); w.Destination = target.Id; }
            w.Status = "Fetching firewood"; return;
        }
        if (!Data.Recipes.ContainsKey(target.Recipe)) { w.Status = "Choose a building recipe"; return; }
        if (Leftovers(target).FirstOrDefault() is string leftover)
        {
            var dest = DepositFor(leftover, target.Pos);
            if (dest == null) { w.Status = "Output blocked · needs accepting storage"; return; }
            w.Status = "Collecting finished goods";
            if (Walk(w, target.Pos, dt)) { target.Inventory.TransferTo(w.Bag, leftover, w.Bag.StackSize, true); w.Destination = dest.Id; }
            return;
        }
        var costs = BatchCost(target);
        foreach (var pair in costs)
        {
            if (target.Inventory[pair.Key] >= pair.Value * 2) continue;
            var source = SourceFor(pair.Key, w.Pos);
            if (source == null) continue;
            w.Status = $"Fetching {Data.Name(pair.Key)}";
            if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, pair.Key, pair.Value * 2 - target.Inventory[pair.Key], true); w.Destination = target.Id; }
            return;
        }
        w.Status = target.Kind == "reinforced" ? "Pumping bellows between loads" : "Working at station";
        if (Walk(w, target.Pos, dt))
        {
            if (target.Kind == "reinforced") { w.WorkState = WorkerWorkState.Pumping; target.Heat = Math.Min(100, target.Heat + dt * 16); }
            else if (target.Kind == "anvil") w.WorkState = WorkerWorkState.Hammering;
        }
    }
    public void PayWages()
    {
        foreach (var w in S.Workers)
        {
            // A striking worker does not work, so no further wages are added until the strike is settled.
            if (w.WageDebt > 0) continue;
            if (S.Coins >= Data.DailyWage) S.Coins -= Data.DailyWage;
            else { float due = w.WageDebt = Data.DailyWage; Say($"{w.Name} is on strike: {due:0} coins owed. Pay or dismiss them at the signpost."); }
        }
    }
    public void SettleDebt(Worker w)
    {
        if (S.Coins < w.WageDebt) { Say("Not enough coins to settle wages."); return; }
        S.Coins -= w.WageDebt; w.WageDebt = 0;
    }
    public void Tick(float dt, bool sleeping = false)
    {
        S.Time += dt; attackClock = Math.Max(0, attackClock - dt);
        if (!sleeping) S.PlaySeconds += dt;
        if (Dead) { S.RespawnIn = Math.Max(0, S.RespawnIn - dt); if (!Dead) Respawn(); }
        else
        {
            S.Hunger = Math.Max(0, S.Hunger - dt * Data.HungerPerSecond); S.Thirst = Math.Max(0, S.Thirst - dt * Data.ThirstPerSecond);
            if (S.Hunger <= 0 || S.Thirst <= 0) S.Health -= dt * 1.4f;
            else if (S.Hunger > 45 && S.Thirst > 45) S.Health = Math.Min(100, S.Health + dt * .12f);
        }
        if (Day > S.WageDay) { PayWages(); S.WageDay = Day; Say($"Day {Day}. Wages settled where funds allow."); }
        foreach (var item in S.Demand.Keys.ToArray()) S.Demand[item] = Mathf.MoveToward(S.Demand[item], 1, dt * .0009f);
        foreach (var order in S.Orders.ToArray())
        {
            order.Elapsed += dt; if (order.Elapsed < Data.TradeSeconds) continue;
            if (order.Buying)
            {
                int overflow = order.Quantity - S.Backpack.Add(order.Item, order.Quantity);
                if (overflow > 0)
                {
                    var delivery = Add("grave", Find(order.Signpost)?.Pos + Vector2.One ?? Spawn);
                    delivery.Inventory.Slots = overflow / delivery.Inventory.StackSize + 1;
                    delivery.Inventory.Add(order.Item, overflow);
                }
            }
            else { S.Coins += order.Value; if (order.Item == "steel_tools") S.Milestones.Add("sold_steel"); }
            S.Orders.Remove(order); Say(order.Buying ? $"Cart returned with {order.Quantity} {Data.Name(order.Item)}." : $"Cart returned with {order.Value:0.0} coins.");
        }
        S.Things.RemoveAll(t => t.Kind == "grave" && t.Inventory.Count == 0 || t.Kind == "wolf_corpse" && t.Remaining <= 0);
        foreach (var t in S.Things.ToArray())
        {
            // A carcass keeps its age so the death animation plays once and not again after loading.
            if (t.Kind == "wolf_corpse") t.Progress = Math.Min(60, t.Progress + dt);
            if (t.Kind == "berries" && t.Remaining == 0) { t.Regrow += dt; if (t.Regrow >= 90) { t.Remaining = 24; t.Regrow = 0; } }
            if (t.Kind == "campfire")
            {
                t.Burn = Math.Max(0, t.Burn - dt);
                if (t.Burn < 30 && t.Inventory.Remove("wood", 1) > 0) t.Burn += 90;
            }
            if (t.Kind == "reinforced") t.Heat = Math.Max(0, t.Heat - dt * 3.2f);
            if (!Data.Recipes.TryGetValue(t.Recipe, out var recipe)) continue;
            bool hot = t.Kind != "reinforced" || t.Heat >= 80;
            bool atAnvil = t.Kind != "anvil" || Near(t) || S.Workers.Any(w => w.Assignment == t.Id && w.WageDebt == 0 && w.Pos.DistanceTo(t.Pos) < 1.8f);
            if (!t.Processing && hot && atAnvil && t.Inventory.Has(BatchCost(t)))
            {
                t.Inventory.Spend(BatchCost(t)); t.Processing = true; t.Progress = 0;
            }
            if (t.Processing && hot && atAnvil)
            {
                t.Progress = Math.Min(recipe.Seconds, t.Progress + dt);
                if (t.Progress >= recipe.Seconds)
                {
                    // Manual loading or another hauler can fill the station while a batch cooks.
                    // Retain the completed batch until the one-stack output slot has room rather than discarding output.
                    if (Math.Min(t.Inventory.Room(recipe.Output), t.Inventory.StackSize - t.Inventory[recipe.Output]) < recipe.Amount) continue;
                    t.Inventory.Add(recipe.Output, recipe.Amount); t.Processing = false; t.Progress = 0;
                    S.Milestones.Add(recipe.Output); if (recipe.Output == "iron_pick") S.Milestones.Add("pick");
                }
            }
        }
        foreach (var w in S.Workers.ToArray()) WorkerTick(w, dt);
        for (int i = 0; i < S.Workers.Count; i++) for (int j = i + 1; j < S.Workers.Count; j++)
        {
            var a = S.Workers[i]; var b = S.Workers[j]; var offset = a.Pos - b.Pos;
            if (offset.LengthSquared() < .64f)
            {
                var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
                if (CanStand(a.Pos + push)) a.Pos += push;
                if (CanStand(b.Pos - push)) b.Pos -= push;
            }
        }
        if (!Night) { S.Wolves.Clear(); nightSpawn = 0; if (!Dead) S.Mauled = false; }
        else if (!sleeping) TickWolves(dt);
        if (!Dead && S.Health <= 0) Die();
        UpdateGoal();
    }
    private void TickWolves(float dt)
    {
        if (!Dead)
        {
            nightSpawn -= dt;
            if (nightSpawn <= 0 && S.Wolves.Count < Math.Min(6, 2 + Day))
            {
                nightSpawn = 15; float angle = S.Time * 2.4f;
                var p = Player + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 11;
                p = p.Clamp(Vector2.One * 2, Vector2.One * 45); S.Wolves.Add(new Wolf { Pos = p, Health = 4 + Day * .4f });
                Say("A wolf is prowling nearby. Stay by fire or shelter; Space swings your axe.");
            }
        }
        // A wounded player away from fire and shelter invites a leap for the throat.
        if (!Downed && S.Health <= 50 && !Protected(Player) && S.Wolves.All(w => w.Leap <= 0))
        {
            var hunter = S.Wolves.Where(w => !Protected(w.Pos) && w.Pos.DistanceTo(Player) < LeapRange).OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
            if (hunter != null) { hunter.Leap = LeapSeconds; Say("A wolf leaps for your throat!"); }
        }
        float speed = Math.Min(3.8f, 3 + Day * .1f);
        foreach (var wolf in S.Wolves)
        {
            wolf.Attack -= dt;
            if (wolf.Leap > 0)
            {
                // Closing the remaining gap evenly makes every leap last LeapSeconds.
                var landing = Player + (wolf.Pos - Player).Normalized() * PinDistance;
                wolf.Pos = wolf.Pos.MoveToward(landing, wolf.Pos.DistanceTo(landing) * Math.Min(1, dt / wolf.Leap));
                wolf.Leap = Math.Max(0, wolf.Leap - dt);
                if (wolf.Leap == 0) { wolf.Feeding = wolf.Pinning = true; S.Mauled = true; Say("You are pinned. The pack closes in."); }
            }
            else if (S.Mauled)
            {
                if (wolf.Feeding) continue;
                var seat = Player + (wolf.Pos - Player).Normalized() * FeastSeat;
                wolf.Pos = wolf.Pos.MoveToward(seat, dt * FeastSpeed);
                wolf.Feeding = wolf.Pos.DistanceTo(seat) < .05f;
            }
            else if (Protected(wolf.Pos) || Protected(Player)) wolf.Pos = wolf.Pos.MoveToward(wolf.Pos + (wolf.Pos - Player).Normalized() * 3, dt * 3);
            else
            {
                wolf.Pos = wolf.Pos.MoveToward(Player, dt * speed);
                if (wolf.Pos.DistanceTo(Player) < 1.1f && wolf.Attack <= 0) { wolf.Attack = 1; S.Health -= 12 + Day; }
            }
        }
        // Feeding wolves crowd around the player without stacking on one spot.
        for (int i = 0; i < S.Wolves.Count; i++) for (int j = i + 1; j < S.Wolves.Count; j++)
        {
            var a = S.Wolves[i]; var b = S.Wolves[j]; var offset = a.Pos - b.Pos;
            if (a.Leap > 0 || b.Leap > 0 || offset.LengthSquared() >= .5f) continue;
            var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
            a.Pos += push; b.Pos -= push;
        }
        if (S.Mauled && !Dead) S.Health -= dt * (16 + 8 * S.Wolves.Count(w => w.Feeding));
    }
    private static readonly string[] Milestones = { "gather", "furnace", "iron_bar", "anvil", "pick", "reinforced", "steel_bar", "steel_tools", "sold_steel", "hired", "assigned" };
    public bool GoalComplete => S.Goal >= Milestones.Length;
    private void UpdateGoal()
    {
        while (!GoalComplete && S.Milestones.Contains(Milestones[S.Goal])) S.Goal++;
    }
    public static int GoalCount => Milestones.Length;
    public (string Title, string Hint) Objective => ObjectiveFor(S.Goal);
    public static (string Title, string Hint) ObjectiveFor(int goal) => goal switch
    {
        0 => ("A place to begin", "Hold right mouse near trees and stones. You carry an iron axe. B opens building plans."),
        1 => ("Stone and embers", "Build a stone furnace: 16 stone + 6 wood. A cabin and campfire make nights safer."),
        2 => ("The first iron", "Pick up loose rusty ore below the iron cliff. Load the furnace with ore + wood. Collect iron bars."),
        3 => ("A proper workshop", "Build an anvil: 4 iron bars + 6 stone. Keep smelting for a pickaxe and bellows furnace."),
        4 => ("Beneath the surface", "At the anvil, forge an iron pickaxe: 3 bars + 2 wood. It opens coal and iron deposits."),
        5 => ("Hotter than fire", "Mine coal at the foot of the dark cliff; the landmark guide points the way. Build a bellows furnace: 20 stone, 5 iron bars, 8 wood."),
        6 => ("Make steel", "Load iron + mined coal into the bellows furnace. Pump regularly to stay above 80°. Each bar costs 2 iron + 3 coal incl. fuel."),
        7 => ("Tools for the village", "At the anvil choose Steel tools. Load 2 steel bars + 2 wood, then collect your tools."),
        8 => ("The road to Bramblewick", "Build a village signpost. Sell steel tools. The cart returns with payment after 30 seconds."),
        9 => ("Many hands", "At the signpost hire a worker for 24 coins. Reserve 8 coins per worker for each dawn."),
        10 => ("Your first automation", "Build storage. Select a worker → Work → click a resource. Or select a furnace → Assign."),
        _ => ("A settlement of your own", "POC goal reached! Expand production. Filter chests, assign furnace crews, and keep wages paid.")
    };
    public string Serialize() => JsonSerializer.Serialize(S, GameData.Json);
    public void Deserialize(string json)
    {
        var loaded = JsonSerializer.Deserialize<SaveState>(json, GameData.Json) ?? throw new InvalidOperationException("Invalid save.");
        if (loaded.Version != 2 || loaded.Things == null || loaded.Backpack == null || loaded.Workers == null || loaded.Orders == null || loaded.Wolves == null || loaded.Demand == null || loaded.Milestones == null) throw new InvalidOperationException("Unsupported or incomplete save.");
        // Edited or older saves may hold missing, extra or unknown hotbar entries.
        var slots = loaded.HotbarSlots ?? new();
        loaded.HotbarSlots = Enumerable.Range(0, 9).Select(i => slots.ElementAtOrDefault(i) is string id && (Data.Items.ContainsKey(id) || Data.Buildings.ContainsKey(id)) ? id : "").ToList();
        loaded.ActiveHotbar = Math.Clamp(loaded.ActiveHotbar, 0, 8);
        S = loaded; gatherClock = 0; Say("Settlement loaded.");
    }
}
