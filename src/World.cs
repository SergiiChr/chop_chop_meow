using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

public sealed class Thing {
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
    /// <summary>A signpost's market output: bought goods arrive here with the returning cart, and sale coins once <see cref="Wages"/> is full.</summary>
    public Inventory? Output { get; set; }
    /// <summary>A signpost's one stack of coins for wages: sale coins land here first, and dawn wages come out of it before any backpack.</summary>
    public Inventory? Wages { get; set; }
    /// <summary>A fuel-burning station's one stack of fuel, apart from what it smelts or cooks; what it holds is what it burns.</summary>
    public Inventory? Burner { get; set; }
    /// <summary>A crafting bench's chisel and hammer slots; automation never touches them.</summary>
    public Inventory? Tools { get; set; }
    /// <summary>Building stages finished, for buildings put up in stages: a mine counts up to <see cref="World.MineOpen"/>.</summary>
    public int Stage { get; set; }
    /// <summary>A pile turned into compost: it takes leaves, grass and food and slowly turns them into fertilizer.</summary>
    public bool Compost { get; set; }
    /// <summary>A compost that workers bring no food to; food put in by hand still rots into fertilizer.</summary>
    public bool SkipFood { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
    [JsonIgnore] public bool IsResource => IsLoose || IsDeposit || Kind is "tree" or "stone" or "berries" or "water" or "wolf_corpse" or "leaves";
    /// <summary>Ore at the foot of a cliff, mined with a pickaxe.</summary>
    [JsonIgnore] public bool IsDeposit => Kind is "iron" or "coal" or "tin";
    /// <summary>Loose pieces are picked up whole in one go and vanish, instead of being worked down like a deposit.</summary>
    [JsonIgnore] public bool IsLoose => Kind is "loose_coal";
    [JsonIgnore] public bool IsStorage => Kind is "chest" or "grave" or "crate" or "toolstation" or "pile";
}
/// <summary>An impassable rock face whose ore deposits sit at its foot, with loose pieces scattered toward the clearing.</summary>
public readonly record struct Cliff(string Ore, Vector2 Center, Vector2 Face, float HalfLength) {
    public const float Depth = 1.4f;
    public Vector2 Along => new(-Face.Y, Face.X);
    /// <summary>Distance to the cliff's spine, so the whole wall blocks movement rather than only its middle.</summary>
    public float DistanceTo(Vector2 p) => p.DistanceTo(Center + Along * Math.Clamp((p - Center).Dot(Along), -HalfLength, HalfLength));
}
/// <summary>One job a worker does at one place, such as working the bellows of a furnace or hauling for it.</summary>
public sealed class Duty {
    public int Thing { get; set; }
    public string Job { get; set; } = "";
}
public enum WorkerWorkState { Idle, Gathering, Pumping, Hammering, Blocked, Resting, Digging, Underground }

public sealed class Worker {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>The place the worker is at for one of their <see cref="Duties"/>; with jobs at several places they take turns.</summary>
    public int Assignment { get; set; }
    public List<Duty> Duties { get; set; } = new();
    public string Status { get; set; } = "Available";
    /// <summary>Units of one stack a worker carries; a miner bringing out a full mine output carries the whole stack.</summary>
    public const int BagStack = 5;
    /// <summary>A worker's hands hold <see cref="BagStack"/> items like a crate: one stack of them or that many different items.</summary>
    public Inventory Bag { get; set; } = new() { Slots = BagStack, StackSize = BagStack, Capacity = BagStack };
    /// <summary>Sizes the hands for a load: a full stack hauled out of a mine or the market, or <see cref="BagStack"/> for a normal load.</summary>
    public void Hands(int load) { Bag.StackSize = load; Bag.Capacity = load; }
    /// <summary>The bag holds fuel for the destination's burner rather than ingredients.</summary>
    public bool Fueling { get; set; }
    /// <summary>The tool taken from a tool station for the current job, or empty; it goes back to a station when the job no longer needs it.</summary>
    public string Tool { get; set; } = "";
    /// <summary>The tool kind the worker last reported missing, so the message is said once rather than every tick.</summary>
    [JsonIgnore] public string Missing { get; set; } = "";
    public int Destination { get; set; }
    public float Timer { get; set; }
    public float WageDebt { get; set; }
    /// <summary>Coins due at each dawn, fixed by the hiring offer.</summary>
    public int Wage { get; set; } = 8;
    /// <summary>Seconds until the worker returns from fetching village prices; while away they are off the map.</summary>
    public float Away { get; set; }
    /// <summary>Signed out for the night: off the map in the village until dawn, still holding their assignment and anything carried.</summary>
    public bool Home { get; set; }
    /// <summary>Seconds into the walk down the valley at dusk, counted back down on the way in at dawn; the view fades the worker with it.</summary>
    public float Commute { get; set; }
    [JsonIgnore] public List<Vector2> Route { get; } = new();
    [JsonIgnore] public Vector2 RouteTarget { get; set; } = new(-100, -100);
    [JsonIgnore] public WorkerWorkState WorkState { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
public sealed class TradeOrder {
    public Dictionary<string, int> Goods { get; set; } = new();
    /// <summary>Single-item orders from older saves; moved into <see cref="Goods"/> on load.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public string? Item { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Quantity { get; set; }
    public bool Buying { get; set; }
    public float Value { get; set; }
    public float Elapsed { get; set; }
    public int Signpost { get; set; }
    /// <summary>Crates on the cart: sold crates on the way out, bought boxes on the way back.</summary>
    public int Crates { get; set; }
    /// <summary>Tells carts apart for other machines; zero until <see cref="World.NumberWolvesAndOrders"/>.</summary>
    public int Id { get; set; }
}
public sealed class Wolf {
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
    /// <summary>The wolf has growled at the player while closing in; it growls only once.</summary>
    [JsonIgnore] public bool Growled { get; set; }
    /// <summary>Seconds a chasing wolf keeps following the player into firelight before it gives up; above zero the wolf gallops.</summary>
    public float Chase { get; set; }
    /// <summary>A torch swing toward this wolf made it keep its distance; it lasts while the torch burns.</summary>
    public bool Scared { get; set; }
    /// <summary>Seconds left running away after a direct torch hit.</summary>
    public float Flee { get; set; }
    /// <summary>Seconds left reeling from a hit, knocked back and holding still before it runs or attacks again.</summary>
    public float Stagger { get; set; }
    /// <summary>Id of the player this wolf hunts; each player draws their own pack.</summary>
    public int Target { get; set; }
    /// <summary>Tells wolves apart for other machines; zero until <see cref="World.NumberWolvesAndOrders"/>.</summary>
    public int Id { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
/// <summary>
/// Everything a save holds. The player properties read and write <see cref="Actor"/>, the player whose action or turn
/// the simulation is running, so the save keeps the host's player at the top level as single player always has.
/// </summary>
public sealed class SaveState {
    /// <summary>Everyone in the world; the first is the save's own player. Guests are saved on their own machines.</summary>
    [JsonIgnore] public List<PlayerState> Players { get; set; }
    [JsonIgnore] public PlayerState Actor { get; set; }
    public SaveState() { Players = new() { new PlayerState() }; Actor = Players[0]; }
    /// <summary>5 aligns mine entrances with their cliff faces; version 4 saves migrate on load.</summary>
    public int Version { get; set; } = 5;
    public List<Thing> Things { get; set; } = new();
    public List<Worker> Workers { get; set; } = new();
    public List<TradeOrder> Orders { get; set; } = new();
    public List<Wolf> Wolves { get; set; } = new();
    public Inventory Backpack { get => Actor.Backpack; set => Actor.Backpack = value; }
    public Dictionary<string, float> Demand { get; set; } = new();
    /// <summary>Sale prices as last reported by someone arriving from the village, and when that was.</summary>
    public Dictionary<string, float> Prices { get; set; } = new();
    /// <summary>Coins from older saves, which kept them outside the backpack; moved into it on load.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float? Coins { get; set; }
    public float PricesTime { get; set; }
    public HashSet<string> Milestones { get; set; } = new();
    public float PlayerX { get => Actor.PlayerX; set => Actor.PlayerX = value; }
    public float PlayerY { get => Actor.PlayerY; set => Actor.PlayerY = value; }
    public float Time { get; set; } = 55;
    public float Health { get => Actor.Health; set => Actor.Health = value; }
    public float Hunger { get => Actor.Hunger; set => Actor.Hunger = value; }
    public float Thirst { get => Actor.Thirst; set => Actor.Thirst = value; }
    public int NextId { get; set; } = 1;
    public int Goal { get; set; }
    public int WageDay { get; set; } = 1;
    public List<string> HotbarSlots { get => Actor.HotbarSlots; set => Actor.HotbarSlots = value; }
    /// <summary>Contents of crates packed into items, keyed by the number in their item id.</summary>
    public Dictionary<int, Inventory> Crates { get; set; } = new();
    public List<string> Crafting { get => Actor.Crafting; set => Actor.Crafting = value; }
    public float CraftTime { get => Actor.CraftTime; set => Actor.CraftTime = value; }
    public float Torch { get => Actor.Torch; set => Actor.Torch = value; }
    public bool TorchLit { get => Actor.TorchLit; set => Actor.TorchLit = value; }
    public float TorchKindling { get => Actor.TorchKindling; set => Actor.TorchKindling = value; }
    public int ActiveHotbar { get => Actor.ActiveHotbar; set => Actor.ActiveHotbar = value; }
    public float RespawnIn { get => Actor.RespawnIn; set => Actor.RespawnIn = value; }
    public bool Mauled { get => Actor.Mauled; set => Actor.Mauled = value; }
    public int Sheltered { get => Actor.Sheltered; set => Actor.Sheltered = value; }
    public float PlaySeconds { get; set; }
    public bool EndingShown { get; set; }
    /// <summary>Coins from sales that came back today; wages are taken off at dawn to give the day's profit.</summary>
    public int DayRevenue { get; set; }
    /// <summary>Profit of each finished day, oldest first.</summary>
    public List<int> Profits { get; set; } = new();
    /// <summary>One byte per map tile, 255 once the player has seen it; serialized as base64.</summary>
    public byte[] Explored { get; set; } = new byte[Terrain.Size * Terrain.Size];
    /// <summary>Things as last seen, kept so the fog shows them unchanged until the player looks again.</summary>
    public Dictionary<int, Thing> Remembered { get; set; } = new();
}

/// <summary>Authoritative simulation, independent of rendering and UI; the host runs it for every player.</summary>
public sealed partial class World {
    public readonly GameData Data;
    public SaveState S { get; private set; } = new();
    public Action<string>? Notify;
    public Action<ActorAction, Vector2>? PlayerActed;
    /// <summary>A named sound effect and where it happens; the simulation only names it.</summary>
    public Action<string, Vector2>? Sound;
    /// <summary>How far down the valley an errand runner walks before fading from view on the way to Bramblewick.</summary>
    public const float TripDistance = 8;
    /// <summary>How far out a cart or runner is, from 0 at the start through 1 at the halfway turn and back to 0.</summary>
    public static float TripLeg(float phase) => phase < .5f ? phase * 2 : (1 - phase) * 2;
    public const float LeapSeconds = .4f;
    // A wolf leaps from within LeapRange and lands PinDistance away, where its jaws reach the throat of the downed player.
    private bool packAttack { get => S.Actor.PackAttack; set => S.Actor.PackAttack = value; }
    private const float LeapRange = 3.5f, PinDistance = 1.45f, FeastSeat = 1.1f, FeastSpeed = 5.5f, ChaseSeconds = 3;
    /// <summary>Minimum reinforced furnace heat: steel only progresses at or above it.</summary>
    public const float SteelHeat = 80;
    /// <summary>Heat as degrees Celsius for display: the steel line sits where steel forms, 1450 °C.</summary>
    public static int Celsius(float heat) => (int)MathF.Round(heat * 1450 / SteelHeat / 10) * 10;
    // Rock faces at the foot of both ranges, facing into the valley; the first iron and coal cliffs are the nearer ones, with loose coal below the coal cliff.
    // Iron and tin have no loose pieces: they wait for a pickaxe.
    public static readonly Cliff[] Cliffs = { FootCliff("iron", 172, 1), FootCliff("coal", 140, -1), FootCliff("tin", 108, 1), FootCliff("iron", 80, -1), FootCliff("coal", 62, 1) };
    private static Cliff FootCliff(string ore, float along, int side) =>
        new(ore, Terrain.Point(along, side * (Terrain.HalfWidth(along, side) + .6f)), Terrain.Sideways * -side, 4.5f);
    public static Cliff CliffOf(string ore) => Cliffs.First(c => c.Ore == ore);
    private float gatherClock { get => S.Actor.GatherClock; set => S.Actor.GatherClock = value; }
    private float attackClock { get => S.Actor.AttackClock; set => S.Actor.AttackClock = value; }
    private float nightSpawn { get => S.Actor.NightSpawn; set => S.Actor.NightSpawn = value; }
    private bool asleep;
    // Furnaces whose batch finished on the previous tick: starting the next batch keeps the fire going rather than lighting it.
    private readonly HashSet<int> stillBurning = new();
    // Set by walking, gathering or pumping and cleared at the end of each tick: a busy player is not hammering.
    private bool playerBusy { get => S.Actor.Busy; set => S.Actor.Busy = value; }
    public World(GameData data, bool generate = true) { Data = data; if (generate) NewGame(); }
    public Vector2 Player { get => new(S.PlayerX, S.PlayerY); set { S.PlayerX = value.X; S.PlayerY = value.Y; } }
    public int Day => (int)(S.Time / Data.DaySeconds) + 1;
    public float DayPhase => S.Time % Data.DaySeconds / Data.DaySeconds;
    public bool Night => DayPhase >= Data.NightStarts;
    /// <summary>Whether the moon rather than the sun is in the sky, from sunset until dawn.</summary>
    public bool MoonUp => DayPhase >= Data.Sunset;
    /// <summary>How far the sun, or the moon after sunset, has crossed the sky, from 0 where it rises to 1 where it sets.</summary>
    public float SkyArc => MoonUp ? (DayPhase - Data.Sunset) / (1 - Data.Sunset) : DayPhase / Data.Sunset;
    /// <summary>How much of the moon is lit, from 0 at new moon to 1 at full; a new game starts near full moon so the first night is not the darkest.</summary>
    public float Moonlight => (1 + MathF.Cos(MathF.Tau * S.Time / Data.DaySeconds / Data.MoonCycleDays)) / 2;
    /// <summary>Clock hour, with the day starting at 06:00.</summary>
    public float Hour => (DayPhase * 24 + 6) % 24;
    /// <summary>From the first fading light until dawn; workers are off duty.</summary>
    public bool Dusk => DayPhase >= Data.DuskStarts;
    public bool Lunch => Hour >= Data.LunchHour && Hour < Data.LunchHour + 1;
    /// <summary>On the map and neither signed out nor at lunch.</summary>
    public bool OnDuty(Worker w) => !w.Home && !Dusk && !Lunch;
    /// <summary>The campfire the crew sits around at lunch: the one closest to them all, or null outside lunch or without a campfire.</summary>
    public Thing? LunchFire { get; private set; }
    /// <summary>A worker in sight on the map; one mining underground is out of sight inside the hill.</summary>
    public bool Sees(Worker w) => !w.Home && w.WorkState != WorkerWorkState.Underground && Sees(w.Pos);
    public bool Dead => S.RespawnIn > 0;
    /// <summary>Dead or pinned by a wolf: the player cannot move or act.</summary>
    public bool Downed => Dead || S.Mauled;
    public bool Sees(Vector2 p) => p.DistanceTo(Player) <= Data.SightRadius;
    /// <summary>Raised whenever newly seen tiles join the explored map.</summary>
    public int ExploredRevision { get; private set; }
    /// <summary>Fills <paramref name="known"/> with things as the player knows them: live within sight, as last seen elsewhere, and absent if never seen.</summary>
    public void CollectKnown(List<Thing> known) {
        known.Clear();
        foreach (var t in S.Things) if (Sees(t.Pos)) known.Add(t);
        foreach (var t in S.Remembered.Values) if (!Sees(t.Pos)) known.Add(t);
    }
    /// <summary>From inside a cabin only that cabin is within reach.</summary>
    public Vector2 GatherAt(Thing t, Vector2 from) => t.Kind == "water" ? Terrain.WaterPoint(from) : t.Pos;
    public bool Near(Thing t, float radius = 3.2f) => !Downed && (S.Sheltered == 0 || S.Sheltered == t.Id) && Player.DistanceTo(GatherAt(t, Player)) <= radius;
    public int Coins => S.Backpack[Inventory.Coins];
    /// <summary>Spends carried coins; coins stored elsewhere do not count.</summary>
    public bool Pay(int amount) => amount <= Coins && S.Backpack.Remove(Inventory.Coins, amount) == amount;
    // Where each id sits in the things list. The list is also changed directly, so every lookup checks the entry and a stale one rebuilds it.
    private readonly Dictionary<int, int> thingIndex = new();
    public Thing? Find(int id) {
        var things = S.Things;
        if (thingIndex.TryGetValue(id, out int at) && at < things.Count && things[at].Id == id) return things[at];
        for (int i = 0; i < things.Count; i++) {
            if (things[i].Id != id) continue;
            thingIndex.Clear();
            for (int j = things.Count - 1; j >= 0; j--) thingIndex[things[j].Id] = j;
            return things[i];
        }
        return null;
    }
    /// <summary>Tells the acting player, or everyone while the tick runs the shared world.</summary>
    public void Say(string message) {
        if (everyone || S.Actor == Me) Notify?.Invoke(message);
        if (everyone || S.Actor != Me) Relay?.Invoke(everyone ? null : S.Actor, message);
    }
    // Sleep fast-forwards hours in one frame, so its events stay silent instead of playing all at once.
    private void Play(string sound, Vector2 at) { if (!asleep) Sound?.Invoke(sound, at); }
    public Thing Add(string kind, Vector2 pos, int remaining = 0) {
        var t = new Thing { Id = S.NextId++, Kind = kind, Pos = pos, Remaining = remaining };
        if (kind == "furnace") t.Recipe = "iron_bar";
        if (kind == "reinforced") { t.Recipe = "steel_bar"; t.Fuel = "coal"; }
        if (kind == "signpost") { t.Inventory.Slots = MarketSlots; t.Output = new() { Slots = MarketSlots }; t.Wages = NewWageSlot(); }
        if (SlotKinds(kind).Length > 0) t.Tools = NewToolSlots(kind);
        if (Burns(kind)) t.Burner = NewBurner();
        if (kind == "crate") t.Inventory = NewCrate();
        if (kind == "mine") { t.Inventory.Slots = Data.Mine.Materials.Values.Sum(n => (n + t.Inventory.StackSize - 1) / t.Inventory.StackSize); t.Output = new() { Slots = 2 }; }
        if (kind == "toolstation") t.Inventory.Slots = 12;
        if (kind == "pile") t.Inventory.Slots = PileStacks;
        if (kind == "chest") t.Inventory.Slots = ChestStacks;
        if (kind == "farm") { t.Inventory.Slots = Data.Farm.Sow.Count; t.Output = new() { Slots = Data.Farm.Yield.Count }; }
        S.Things.Add(t); return t;
    }
    public void NewGame() {
        S = new SaveState();
        S.Backpack.Add("steel_axe", 1); S.Backpack.Add("berries", 10); S.Backpack.Add("wood", 6);
        S.Backpack.Add("empty_bottle", 1);
        var rng = new Random(4271);
        foreach (var cliff in Cliffs) {
            // Two deposits flank the mine's mouth in the middle of the cliff, holding what three did before.
            foreach (float along in new[] { -3.5f, 3.5f }) Add(cliff.Ore, cliff.Center + cliff.Face * 2.6f + cliff.Along * along, 600);
            if (cliff != CliffOf(cliff.Ore) || cliff.Ore != "coal") continue;
            // Loose coal rolls well out past the deposits of the nearer coal cliff, so collecting it takes a search.
            // Iron has no loose pieces: the first pickaxe is bought with berry money.
            for (int placed = 0; placed < 6;) {
                var p = cliff.Center + cliff.Face * (4.2f + (float)rng.NextDouble() * 6.5f) + cliff.Along * ((float)rng.NextDouble() * 2 - 1) * (cliff.HalfLength + 4);
                if (!CanStand(p) || S.Things.Any(t => t.Pos.DistanceTo(p) < 2)) continue;
                Add("loose_coal", p, 2); placed++;
            }
        }
        // Rocks and berry bushes stand alone on the plains, far from the clearing and each other, so more of them means exploring.
        Scatter(rng, "stone", 30, 64, 15, 9, _ => 1, 2000);
        Scatter(rng, "berries", 24, 34, 14, 10, _ => 1, 2000);
        var shore = Terrain.Spawn - Terrain.Lake;
        Add("water", Terrain.Lake + shore.Normalized() * (Terrain.Shore(MathF.Atan2(shore.Y, shore.X)) + .2f), 9999);
        Add("tree", Terrain.Spawn + new Vector2(5, 0), 42);
        // Trees are occasional on the plains and thicken into woods toward the mountains.
        // Full-size trees need room for their crowns, so even the woods stay walkable and readable.
        Scatter(rng, "tree", int.MaxValue, 42, 3.4f, 9, p => Math.Clamp(1.15f - Terrain.FromMountains(p) / 16, .015f, 1), 60000);
        DropLeaves();
        Look();
    }
    /// <summary>Leaves a rake gathers in one go from a patch, which fills again every day.</summary>
    public const int LeafPatch = 12;
    public static readonly Vector2 LeafOffset = new(.9f, .9f);
    /// <summary>Leaf litter around every oak, the broadleaf one of the three tree variants picked by id; the patch sits off the trunk so both can be clicked.</summary>
    private void DropLeaves() {
        foreach (var tree in S.Things.Where(t => t.Kind == "tree" && t.Id % 3 == 0 && Terrain.Open(t.Pos + LeafOffset)).ToList()) Add("leaves", tree.Pos + LeafOffset, LeafPatch);
    }
    /// <summary>Places resources on open ground away from the clearing, at least <paramref name="spacing"/> apart from their own kind.</summary>
    /// <param name="chance">How likely a spot is kept, so density can follow the landscape.</param>
    /// <param name="tries">Random spots to consider; with a low chance this, not the count, sets how many are placed.</param>
    private void Scatter(Random rng, string kind, int count, int remaining, float spacing, float clearing, Func<Vector2, float> chance, int tries) {
        // A coarse grid of what is already placed keeps the spacing checks cheap on the large map.
        var near = new Dictionary<Vector2I, List<Thing>>();
        static Vector2I Cell(Vector2 p) => new((int)(p.X / 16), (int)(p.Y / 16));
        foreach (var t in S.Things) { if (!near.TryGetValue(Cell(t.Pos), out var list)) near[Cell(t.Pos)] = list = new(); list.Add(t); }
        bool Crowded(Vector2 p) {
            var cell = Cell(p);
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    if (near.TryGetValue(cell + new Vector2I(x, y), out var list) && list.Any(t => t.Pos.DistanceTo(p) < (t.Kind == kind ? spacing : 2))) return true;
            return false;
        }
        for (int placed = 0, tried = 0; placed < count && tried < tries; tried++) {
            var p = new Vector2(rng.Next(3, Terrain.Size - 3), rng.Next(3, Terrain.Size - 3));
            if (p.DistanceTo(Terrain.Spawn) < clearing || !Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 3.5f) || Cliffs.Any(c => c.DistanceTo(p) < Cliff.Depth + 2.5f) || InSpur(p, 2.5f)) continue;
            if (rng.NextDouble() > chance(p) || Crowded(p)) continue;
            var t = Add(kind, p, remaining); placed++;
            if (!near.TryGetValue(Cell(p), out var cellList)) near[Cell(p)] = cellList = new();
            cellList.Add(t);
        }
    }
    public bool CanStand(Vector2 p) {
        if (!Open(p)) return false;
        foreach (var t in S.Things) if (Blocks(t, p)) return false;
        return true;
    }
    /// <summary>Walkable ground ignoring buildings: inside the valley, off the water and clear of cliff walls and their spurs.</summary>
    private static bool Open(Vector2 p) {
        if (!Terrain.Open(p)) return false;
        foreach (var c in Cliffs) if (c.DistanceTo(p) < Cliff.Depth) return false;
        return !InSpur(p);
    }
    private static bool Blocks(Thing t, Vector2 p) => !t.IsResource && !IsZone(t.Kind) && t.Kind is not "campfire" and not "grave" and not "signpost" and not "mine" && t.Pos.DistanceTo(p) < Footprint(t.Kind);
    /// <summary>Square 3×3 yards laid out on the ground, walkable and snapping edge to edge into grids.</summary>
    public static bool IsZone(string kind) => kind is "pile" or "farm";
    public const float ZoneSize = 3;
    public const int PileStacks = 14;
    /// <summary>A third of a pile, rounded to the nearest stack.</summary>
    public const int ChestStacks = (PileStacks + 1) / 3;
    /// <summary>Snaps a mine to its cliff and a yard to the grid of the nearest yard close enough to touch it.</summary>
    public Vector2 Snap(string kind, Vector2 p) {
        if (kind == "mine") return SnapMine(p);
        if (!IsZone(kind) || S.Things.Where(t => IsZone(t.Kind)).MinBy(t => t.Pos.DistanceSquaredTo(p)) is not Thing near || near.Pos.DistanceTo(p) > ZoneSize * 1.6f) return p;
        return near.Pos + ((p - near.Pos) / ZoneSize).Round() * ZoneSize;
    }
    /// <summary>Tile centres a yard at <paramref name="p"/> covers.</summary>
    private static IEnumerable<Vector2> ZoneTiles(Vector2 p) => Enumerable.Range(-1, 3).SelectMany(x => Enumerable.Range(-1, 3).Select(y => p + new Vector2(x, y)));
    /// <summary>Where a bellows operator stands: past the handle end of the bellows, behind the forge.</summary>
    public static readonly Vector2 BellowsStand = new(0, -2.1f);
    public static float Footprint(string kind) => kind switch { "pile" or "farm" => 1.5f, "cooktable" => .6f, "cannery" => .5f, "hut" => 1.55f, "reinforced" => .8f, "furnace" => .65f, "bench" => .6f, "chest" or "anvil" or "toolstation" => .45f, _ => .35f };
    /// <summary>The tool kind gathering this needs: an axe for trees, a pickaxe for deposits and a rake for leaves; stone, berries and loose pieces need none.</summary>
    public static string? ToolKind(Thing t) => t.Kind == "tree" ? "axe" : t.IsDeposit ? "pickaxe" : t.Kind == "leaves" ? "rake" : null;
    private string A(string word) => Phrase("aeiou".Contains(word[0]) ? "an " : "a ", word);
    private string Task(Thing t) => t.Kind switch {
        "tree" => "chop wood",
        "leaves" => "rake leaves",
        "mine" => t.Stage == 0 ? "dig the mine" : t.Stage == MineSupplied ? "fit the mine frame" : Phrase("mine ", CliffAt(t.Pos).Ore),
        _ => Phrase("mine ", t.Kind)
    };
    // Status lines are made of a few phrases, names and small numbers, so each line is built once and reused rather than on every tick.
    private readonly Dictionary<(string, string, string, string), string> phrases = new();
    private readonly Dictionary<string, string> lowered = new();
    private static readonly string[] Numbers = Enumerable.Range(0, 1000).Select(n => n.ToString()).ToArray();
    private string Phrase(string a, string b, string c = "", string d = "") {
        if (!phrases.TryGetValue((a, b, c, d), out var text)) phrases[(a, b, c, d)] = text = a + b + c + d;
        return text;
    }
    private string Lower(string text) {
        if (!lowered.TryGetValue(text, out var lower)) lowered[text] = lower = text.ToLowerInvariant();
        return lower;
    }
    /// <summary>A whole number as text, rounded like a <c>0</c> format; small ones come from a table.</summary>
    private static string Number(float value) {
        int n = (int)MathF.Round(value, MidpointRounding.AwayFromZero);
        return n >= 0 && n < Numbers.Length ? Numbers[n] : n.ToString();
    }
    /// <summary>The fastest carried tool of a kind at or above <paramref name="tier"/>, or null when none is carried.</summary>
    public string? BestTool(string tool, Inventory? from = null, int tier = 0) {
        string? best = null; float speed = 0;
        foreach (var pair in (from ?? S.Backpack).Items)
            if (Data.Items.TryGetValue(pair.Key, out var def) && def.Tool == tool && def.Tier >= tier && (best == null || def.Speed > speed)) { best = pair.Key; speed = def.Speed; }
        return best;
    }
    public float Speed(string? tool) => tool == null ? 1 : Data.Items[tool].Speed;
    public void MovePlayer(Vector2 delta) {
        // Inside a cabin the player stays put until they step outside on purpose.
        if (Downed || S.Sheltered != 0) return;
        if (delta == Vector2.Zero) return;
        playerBusy = true;
        var p = Player + delta;
        if (CanStand(p)) Player = p;
        else { if (CanStand(Player + new Vector2(delta.X, 0))) Player += new Vector2(delta.X, 0); if (CanStand(Player + new Vector2(0, delta.Y))) Player += new Vector2(0, delta.Y); }
    }
    public string? GatherBlock(Thing t, bool worker = false) {
        if (t.Kind == "water" && !worker && Water.FindBottle(S.Backpack) == "water") return "Your bottles are full. Take a sip before refilling.";
        if (t.Kind == "farm") return t.Stage switch { FarmRipe => null, FarmGrowing => "The wheat is not ripe yet.", _ => "Nothing grows here yet. Sow the plot with wheat seeds and fertilizer." };
        if (!t.IsResource) return "Select a natural resource.";
        if (t.Remaining <= 0) return t.Kind switch { "berries" => Phrase("Picked clean. The bush fruits again in ", Number(Data.BerryRegrowHours), " hours."), "tree" => "Only a stump is left. Find another tree.", "leaves" => "Raked clean. More leaves fall by tomorrow.", _ => "Depleted. Look for another deposit." };
        // Workers fetch their tools from a tool station instead of needing them carried.
        if (worker || ToolKind(t) is not string kind || BestTool(kind) != null) return null;
        return kind switch {
            "axe" => "An axe is needed. Recover your belongings or buy one at the trading post.",
            "rake" => "Raking leaves needs a rake. Craft one at a bench.",
            _ => "Mining needs a pickaxe. Sell crates of berries at the trading post and buy one."
        };
    }
    public static string ResourceItem(Thing t) => ResourceItem(t.Kind);
    public static string ResourceItem(string kind) => kind switch { "tree" => "wood", "iron" => "iron_ore", "tin" => "tin_ore", "loose_coal" => "coal", "wolf_corpse" => "raw_meat", _ => kind };
    public void Gather(Thing t, float dt) {
        // A loose piece is a single pick-up; anything else the host works on for as long as the player holds on to it.
        if (t.IsLoose ? Forwarded("pick_up", t.Id) : Remote) { S.Actor.GatherTarget = t.IsLoose ? 0 : t.Id; return; }
        S.Actor.GatherTarget = t.Id;
        playerBusy = true;
        if (!Near(t)) { gatherClock = 0; return; }
        var blocked = GatherBlock(t);
        if (blocked != null) { gatherClock = 0; return; }
        if (t.IsLoose) { PickUp(t); return; }
        if (t.Kind == "farm") { Harvest(t, dt); if (t.Stage == FarmFallow) Say("Harvested. Take the wheat, seeds and grass from the plot, and put seeds back in to sow again."); return; }
        gatherClock += dt;
        string? tool = t.Kind == "stone" ? BestTool("pickaxe") : ToolKind(t) is string kind ? BestTool(kind) : null;
        float seconds = Data.GatherSeconds / Speed(tool);
        if (gatherClock < seconds) return;
        gatherClock = 0;
        if (t.Kind == "water") {
            var result = Water.CollectWater(this, t);
            if (result != WaterCollectResult.NoChange) { S.Milestones.Add("water"); Play("gather_water", Player); }
            return;
        }
        bool small = t.Kind == "wolf_corpse" || t.Kind == "stone" && tool == null;
        int amount = Math.Min(t.Remaining, small ? 2 : 4);
        amount = S.Backpack.Add(ResourceItem(t), amount); t.Remaining -= amount;
        if (amount == 0) Say("Backpack full. Store or sell some supplies.");
        if (amount > 0) S.Milestones.Add("gather");
    }
    private void PickUp(Thing t) {
        string item = ResourceItem(t);
        int amount = S.Backpack.Add(item, t.Remaining); t.Remaining -= amount;
        if (amount == 0) { Say("Backpack full. Store or sell some supplies."); return; }
        if (t.Remaining <= 0) S.Things.Remove(t);
        S.Milestones.Add("gather"); Play("wood_rocks_inventory", t.Pos); PlayerActed?.Invoke(ActorAction.PickUp, t.Pos);
    }
    public void ResetGather() { gatherClock = 0; S.Actor.GatherTarget = 0; }
    /// <summary>The player hammers an anvil while standing by it with nothing else to do.</summary>
    /// <remarks>Input calls <see cref="MovePlayer"/> and <see cref="Gather"/> before each tick, so a tick without them counts as standing still.</remarks>
    public bool PlayerHammers(Thing anvil) => !playerBusy && Near(anvil);
    /// <summary>Stations that can need someone working at them.</summary>
    public static bool Worked(string kind) => kind is "anvil" or "bench" or "cooktable";
    /// <summary>Whether the station's current recipe only advances while someone works at it; at the cooking table that is the mortar's recipes.</summary>
    public bool Worked(Thing t) => Worked(t.Kind) && (t.Kind != "cooktable" || Data.Recipes.TryGetValue(t.Recipe, out var r) && r.Tool != "");
    /// <summary>An anvil or bench only works while the player or a paid worker is hammering at it, not merely standing near.</summary>
    public bool Hammered(Thing anvil) {
        foreach (var p in S.Players) using (ActAs(p)) if (PlayerHammers(anvil)) return true;
        foreach (var w in S.Workers) if (w.Assignment == anvil.Id && w.WageDebt <= 0 && w.WorkState == WorkerWorkState.Hammering && HasDuty(w, anvil.Id, "craft")) return true;
        return false;
    }
    /// <summary>Why a building, or a crate from the backpack when <paramref name="item"/> names one, cannot go here.</summary>
    public string? PlacementBlock(string kind, Vector2 p, string? item = null) {
        bool crate = kind == "crate";
        if (!crate && !Data.Buildings.ContainsKey(kind)) return "Unknown building.";
        if (Downed) return "You are still recovering.";
        if (S.Sheltered != 0) return "Leave the cabin first.";
        if (kind == "mine") return MineBlock(p);
        if (IsZone(kind)) return ZoneBlock(kind, p);
        if (Player.DistanceTo(p) > 5.5f) return "Build within 5 tiles of yourself.";
        if (Player.DistanceTo(p) < 1.2f || Terrain.Spawn.DistanceTo(p) < 1.2f) return "Leave your feet and the arrival point clear.";
        if (!CanStand(p) || Terrain.InLake(p, .8f)) return "Choose dry, open ground.";
        if (S.Things.Any(t => t.Pos.DistanceTo(p) < (t.IsResource ? Footprint(kind) + .65f : Footprint(kind) + Footprint(t.Kind) + .45f) && (t.Remaining > 0 || !t.IsResource))) return "Leave room around resources and buildings.";
        if (crate ? item != null && S.Backpack[item] == 0 : !S.Backpack.Has(Data.Buildings[kind].Cost)) return crate ? "That crate is no longer in your backpack." : "Missing materials in your backpack.";
        return null;
    }
    private string? ZoneBlock(string kind, Vector2 p) {
        if (Player.DistanceTo(p) > 5.5f) return "Build within 5 tiles of yourself.";
        if (ZoneTiles(p).Any(q => !CanStand(q) || Terrain.InLake(q, .8f) || Terrain.Spawn.DistanceTo(q) < 1)) return "Choose dry, open ground.";
        float half = ZoneSize / 2;
        // Yards may touch edge to edge; anything else keeps its usual room from the square.
        bool Inside(Thing t, float margin) => Math.Abs(t.Pos.X - p.X) < half + margin && Math.Abs(t.Pos.Y - p.Y) < half + margin;
        if (S.Things.Any(t => IsZone(t.Kind) ? Inside(t, half - .05f) : (t.Remaining > 0 || !t.IsResource) && Inside(t, t.IsResource ? .3f : Footprint(t.Kind) + .2f))) return "Leave room around resources and buildings.";
        return S.Backpack.Has(Data.Buildings[kind].Cost) ? null : "Missing materials in your backpack.";
    }
    public Thing? Build(string kind, Vector2 p) {
        p = Snap(kind, p);
        var blocked = PlacementBlock(kind, p);
        if (blocked != null) { Say(blocked); return null; }
        if (Forwarded("build", item: kind, at: p)) return null;
        S.Backpack.Spend(Data.Buildings[kind].Cost);
        var t = Add(kind, p); if (kind == "campfire") { t.Burn = Data.CampfireSeconds; Play("start_fire", p); }
        S.Milestones.Add(kind); if (kind == "mine") Say($"Marked the {MineName(t)} site. Assign a worker to dig the entrance; they need a pickaxe."); return t;
    }
    public void Consume(string item) {
        if (Downed || !Data.Items.TryGetValue(item, out var def) || Forwarded("consume", item: item, count: S.Backpack.PreferredSlot)) return;
        if (Water.IsBottle(item)) {
            if (!Water.SipBottle(this, item)) Say("Your bottle is empty. Collect water at the pond to refill it.");
            return;
        }
        if (def.Hunger <= 0 && def.Thirst <= 0) { Say($"{def.Name} needs cooking first."); return; }
        if (S.Backpack.Remove(item, 1) == 0) { Say($"No {def.Name} in your backpack."); return; }
        S.Hunger = Math.Min(100, S.Hunger + def.Hunger); S.Thirst = Math.Min(100, S.Thirst + def.Thirst); S.Health = Math.Min(100, S.Health + def.Health);
        if (def.Food) Play("eat_berry", Player);
    }
    public void Pump(Thing t) {
        if (!Near(t) || t.Kind != "reinforced" || Forwarded("pump", t.Id)) return;
        playerBusy = true;
        t.Heat = Math.Min(100, t.Heat + 24); PlayerActed?.Invoke(ActorAction.Pump, t.Pos);
    }
    public void FuelFire(Thing t) {
        if (!Near(t) || Forwarded("fuel_fire", t.Id)) return;
        int n = S.Backpack.Remove("wood", 1);
        if (n == 0) { Say("You need wood to feed the fire."); return; }
        if (t.Burn <= 0) Play("start_fire", t.Pos);
        t.Burn += Data.CampfireSeconds; Say($"Campfire fed: +{Data.CampfireHours:0} hours of light.");
    }
    public void SetRecipe(Thing t, string recipe) {
        if (t.Processing) return;
        if (Forwarded("set_recipe", t.Id, item: recipe)) return;
        if (Data.Recipes.TryGetValue(recipe, out var r) && r.Station == t.Kind) { t.Recipe = recipe; t.Progress = 0; }
    }
    /// <summary>The fuel a station burns: coal in the reinforced furnace, otherwise its selection, falling back to wood for anything unknown.</summary>
    public static string BurnedFuel(Thing t) => t.Kind == "reinforced" ? "coal" : t.Burner?.First() is "wood" or "coal" ? t.Burner.First()! : t.Fuel is "wood" or "coal" ? t.Fuel : "wood";
    /// <summary>The fuel one batch of the station's recipe burns, or null when it burns none.</summary>
    public string? FuelOf(Thing t) => Data.Recipes.TryGetValue(t.Recipe, out var r) && r.Fuel ? BurnedFuel(t) : null;
    // A batch's cost depends only on its recipe, so each is built once.
    private readonly Dictionary<string, Dictionary<string, int>> batchCosts = new();
    /// <summary>One batch's inputs, without the fuel, which has a slot of its own. Shared between stations, so read it without changing it.</summary>
    public Dictionary<string, int> BatchCost(Thing t) {
        if (!batchCosts.TryGetValue(t.Recipe, out var cost)) batchCosts[t.Recipe] = cost = Data.Recipes.TryGetValue(t.Recipe, out var r) ? new(r.Inputs) : new();
        return cost;
    }
    /// <summary>Whether the station holds a batch's inputs and, if it burns fuel, one unit of it.</summary>
    private bool Stocked(Thing t) => t.Inventory.Has(BatchCost(t)) && (FuelOf(t) is not string fuel || t.Burner?[fuel] > 0);
    public void LoadBatch(Thing t, int batches = 1) {
        if (!Near(t)) { Say("Walk closer to load this building."); return; }
        if (Forwarded("load_batch", t.Id, count: batches)) return;
        var cost = BatchCost(t);
        if (cost.Count == 0) { Say("Choose a recipe first."); return; }
        string? fuel = FuelOf(t);
        if (NeedsFullBottle(cost, S.Backpack)) { Say(Water.FullBottleWarning); return; }
        for (int i = 0; i < batches; i++) {
            if (!S.Backpack.Has(cost) || fuel != null && S.Backpack[fuel] < cost.GetValueOrDefault(fuel) + 1) { if (i == 0) Say("Missing batch ingredients or fuel in backpack."); break; }
            if (cost.Any(pair => t.Inventory[pair.Key] + pair.Value > t.Inventory.StackSize) || fuel != null && (t.Burner!.Room(fuel) == 0 || FuelBlock(t, fuel) != null)) { Say("Input slots are full."); break; }
            foreach (var pair in cost) S.Backpack.TransferTo(t.Inventory, pair.Key, pair.Value);
            if (fuel != null) S.Backpack.TransferTo(t.Burner!, fuel, 1);
        }
    }
    /// <summary>Manual loading of one input slot, which holds at most one stack.</summary>
    public int LoadInput(Thing t, string item, int quantity) {
        if (!Near(t)) { Say("Walk closer to load this building."); return 0; }
        if (PartialRecipeBottle(t, item)) { Say(Water.FullBottleWarning); return 0; }
        // Fuel nothing smelts goes to the fuel slot; coal at the reinforced furnace fills the ingredient slot, and the fuel slot is filled by dragging.
        if (!BatchCost(t).ContainsKey(item) && t.Burner != null && FuelFits(t, item)) return LoadFuel(t, item, quantity);
        if (!BatchCost(t).ContainsKey(item)) { Say($"{Data.Name(item)} is not used by this recipe."); return 0; }
        if (Forwarded("load_input", t.Id, item: item, count: quantity)) return 0;
        int moved = S.Backpack.TransferTo(t.Inventory, item, Math.Min(quantity, t.Inventory.StackSize - t.Inventory[item]));
        if (moved == 0) Say(S.Backpack[item] == 0 ? $"No {Data.Name(item)} in your backpack." : $"The {Data.Name(item)} slot is full.");
        return moved;
    }
    /// <summary>Manual loading of the fuel slot, which holds one kind of fuel at a time.</summary>
    public int LoadFuel(Thing t, string item, int quantity) {
        if (!Near(t)) { Say("Walk closer to load this building."); return 0; }
        if (t.Burner == null) return 0;
        if (FuelBlock(t, item) is string refused) { Say(refused); return 0; }
        if (Forwarded("load_fuel", t.Id, item: item, count: quantity)) return 0;
        int moved = S.Backpack.TransferTo(t.Burner, item, Math.Min(quantity, t.Burner.StackOf(item) - t.Burner[item]));
        if (moved > 0) t.Fuel = item;
        else Say(S.Backpack[item] == 0 ? $"No {Data.Name(item)} in your backpack." : "The fuel slot is full.");
        return moved;
    }
    public void Collect(Thing t) {
        if (!Near(t)) { Say("Walk closer to collect items."); return; }
        if (Forwarded("collect", t.Id)) return;
        foreach (var item in Leftovers(t).ToArray()) t.Inventory.TransferTo(S.Backpack, item, 999);
    }
    /// <summary>Finished goods plus anything a previous recipe or fuel choice left behind.</summary>
    public IEnumerable<string> Leftovers(Thing t) {
        var cost = BatchCost(t);
        return t.Inventory.Items.Keys.Where(item => !cost.ContainsKey(item));
    }
    private string? FirstLeftover(Thing t) {
        var cost = BatchCost(t);
        foreach (var pair in t.Inventory.Items) if (!cost.ContainsKey(pair.Key)) return pair.Key;
        return null;
    }
    /// <summary>Steps inside a cabin, out of reach of wolves; any movement steps back out.</summary>
    public bool Enter(Thing hut) {
        if (hut.Kind != "hut" || !Near(hut, 2.6f)) return false;
        if (!Forwarded("enter", hut.Id)) S.Sheltered = hut.Id;
        return true;
    }
    public void Leave() { if (!Forwarded("leave")) S.Sheltered = 0; }
    /// <summary>Bedtime comes a little before night falls, so the player can turn in without waiting for the dark.</summary>
    public const int SleepHour = 22;
    public float SecondsToDawn => Data.DaySeconds - S.Time % Data.DaySeconds + 1;
    /// <summary>Why sleeping is not possible now, or null; hunger and thirst must last until morning.</summary>
    public string? SleepBlock() {
        if (Hour is >= 6 and < SleepHour) return "You can sleep after 10 pm. Keep food and water for the morning.";
        bool hungry = S.Hunger - SecondsToDawn * Data.HungerPerSecond <= 0, thirsty = S.Thirst - SecondsToDawn * Data.ThirstPerSecond <= 0;
        if (hungry || thirsty) return hungry && thirsty ? "You are too hungry and thirsty to sleep." : hungry ? "You are too hungry to sleep." : "You are too thirsty to sleep.";
        // The night passes for everyone at once, so the others have to be inside and fed too.
        foreach (var p in S.Players) if (p != S.Actor && p.Sheltered == 0) return "Everyone has to be inside a cabin to sleep.";
        foreach (var p in S.Players)
            if (p != S.Actor && (p.Hunger - SecondsToDawn * Data.HungerPerSecond <= 0 || p.Thirst - SecondsToDawn * Data.ThirstPerSecond <= 0)) return "Someone is too hungry or thirsty to sleep.";
        return null;
    }
    public void Sleep(Thing hut) {
        if (!Near(hut)) return;
        if (SleepBlock() is string blocked) { Say(blocked); return; }
        if (Forwarded("sleep", hut.Id)) return;
        PassNight();
    }
    /// <summary>Runs the world to dawn a second at a time, so time, wages, trades, hunger and production advance together, then clears the wolves.</summary>
    private void PassNight() {
        float seconds = SecondsToDawn;
        asleep = true;
        try {
            while (seconds > 0) { float dt = Math.Min(1, seconds); Tick(dt, true); seconds -= dt; }
        } finally {
            asleep = false;
        }
        S.Wolves.Clear(); SayToAll(S.Workers.Count > 0 ? "Dawn breaks. Your workers are walking back from Bramblewick." : "Dawn breaks.");
    }
    /// <summary>How far a burning campfire's safety reaches; its light is drawn to the same edge.</summary>
    public const float FireReach = 5.3f;
    /// <summary>Burning campfires keep wolves out of their light, unless a wolf is already chasing the player.</summary>
    public bool Protected(Vector2 p) {
        foreach (var t in S.Things) if (t.Kind == "campfire" && t.Burn > 0 && t.Pos.DistanceTo(p) < FireReach) return true;
        return false;
    }
    /// <summary>Wolves scared by a torch circle the player at this distance until enough gather to attack together.</summary>
    public const float StalkDistance = 4.5f;
    public const int PackSize = 5;
    public const float KindleSeconds = 2, ScareRange = 9, FleeSeconds = 2.5f;
    /// <summary>
    /// This many torch swings within <see cref="WildWindow"/> seconds put the torch out.
    /// The swing cooldown allows at most four in two seconds, so four is the fastest non-stop swinging.
    /// </summary>
    public const int WildSwings = 4;
    public const float WildWindow = 2;
    private List<float> torchSwings => S.Actor.TorchSwings;
    /// <summary>Seconds left of the brighter flare that follows a torch swing.</summary>
    public float TorchFlare { get => S.Actor.TorchFlare; private set => S.Actor.TorchFlare = value; }
    /// <summary>Wolves close enough to the player to count toward a pack attack.</summary>
    public int PackAround {
        get {
            int around = 0;
            foreach (var w in S.Wolves) if (w.Pos.DistanceTo(Player) < StalkDistance * 2) around++;
            return around;
        }
    }
    /// <summary>A burning torch in hand, with the player out in the open, sets how wolves behave.</summary>
    private bool Torchlit => S.TorchLit && !Downed && S.Sheltered == 0 && !Protected(Player);
    /// <summary>Lights the torch in hand, taking a new one from the backpack if the hand is empty, or swings it once it burns.</summary>
    public void UseTorch(Vector2 aim) {
        if (Downed || S.Sheltered != 0 || S.TorchKindling > 0 || Forwarded("torch", at: aim)) return;
        if (S.TorchLit) { SwingTorch(aim); return; }
        if (S.Torch <= 0) {
            if (S.Backpack.Remove("torch", 1) == 0) { Say("No torch in your backpack. Make one with a chisel from the crafting menu."); return; }
            S.Torch = Data.CampfireSeconds;
        }
        S.TorchKindling = KindleSeconds; PlayerActed?.Invoke(ActorAction.Kindle, Player);
    }
    private void SwingTorch(Vector2 aim) {
        if (attackClock > 0) return;
        attackClock = .6f;
        torchSwings.RemoveAll(t => S.Time - t > WildWindow); torchSwings.Add(S.Time);
        Vector2 direction = aim == Vector2.Zero ? Vector2.Down : aim.Normalized();
        PlayerActed?.Invoke(ActorAction.TorchSwing, Player + direction); Play("tool_air_swing", Player);
        if (torchSwings.Count >= WildSwings) { PutOutTorch(); Say("Torch went off due to wild swinging."); return; }
        TorchFlare = .5f;
        bool pack = PackAround >= PackSize;
        var wolf = S.Wolves.Where(w => w.Leap <= 0 && !w.Feeding).OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
        if (wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f) {
            wolf.Health -= 1; wolf.Flee = FleeSeconds; KnockBack(wolf);
            if (wolf.Health <= 0) KillWolf(wolf);
        }
        // A wave of fire toward a wolf on its own makes it keep its distance; a pack closing in no longer cares.
        if (!pack)
            foreach (var w in S.Wolves.Where(w => w.Pos.DistanceTo(Player) < ScareRange && (w.Pos - Player).Normalized().Dot(direction) > .7f)) w.Scared = true;
    }
    public const float StaggerSeconds = .45f;
    /// <summary>Throws a struck wolf back from the player; the view slides it there over the stagger.</summary>
    private void KnockBack(Wolf wolf) { wolf.Pos += (wolf.Pos - Player).Normalized() * 1.2f; wolf.Stagger = StaggerSeconds; }
    /// <summary>Leaves a carcass where a struck wolf fell, within the map.</summary>
    private void KillWolf(Wolf wolf) {
        S.Wolves.Remove(wolf); Add("wolf_corpse", wolf.Pos.Clamp(Vector2.One * 2, Vector2.One * (Terrain.Size - 3)), 4);
        Say("The wolf falls. Butcher the carcass for meat and cook it in a furnace.");
    }
    private void PutOutTorch() {
        S.TorchLit = false; S.TorchKindling = 0; torchSwings.Clear();
        foreach (var w in Hunters) w.Scared = false;
    }
    public void Attack() {
        if (attackClock > 0 || Downed || S.Sheltered != 0 || Forwarded("attack")) return;
        attackClock = .6f;
        var wolf = S.Wolves.OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
        string? axe = BestTool("axe");
        ActorAction action = axe == "steel_axe" ? ActorAction.SteelChop : axe != null ? ActorAction.Chop : ActorAction.Attack;
        Vector2 target = wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f ? wolf.Pos : Player;
        PlayerActed?.Invoke(action, target); Play("tool_air_swing", Player);
        if (wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f) {
            wolf.Health -= axe == null ? .5f : 1.5f * Speed(axe);
            KnockBack(wolf);
            if (wolf.Health <= 0) KillWolf(wolf);
        }
    }
    public void Die() {
        // After a mauling the box lands past the feet, away from the wolf at the throat, so the feeding stays visible.
        var killer = S.Mauled ? Hunters.FirstOrDefault(w => w.Pinning) : null;
        var box = Add("grave", killer == null ? Player : Player + (Player - killer.Pos).Normalized() * 1.5f); box.Inventory = S.Backpack;
        S.Backpack = new Inventory { Slots = 24 }; S.RespawnIn = Data.RespawnSeconds; S.Sheltered = 0; S.Torch = 0; PutOutTorch();
        if (S.Mauled) {
            // The pack eats every scrap of food and stays to feed until the player respawns.
            foreach (var item in box.Inventory.Items.Keys.Where(Data.IsFood).ToArray()) box.Inventory.Remove(item, box.Inventory[item]);
            Say($"{(Hunters.Count() > 1 ? "The wolves" : "A wolf")} brought you down and ate all your food. The rest is in a recovery box. Respawning in {Data.RespawnSeconds:0} seconds.");
            return;
        }
        S.Wolves.RemoveAll(w => w.Target == S.Actor.Id);
        Say($"You collapsed. Your belongings are in a recovery box where you fell. Respawning in {Data.RespawnSeconds:0} seconds.");
    }
    private void Respawn() {
        // The player wakes inside the cabin nearest to where they fell, or in the clearing without one.
        Thing? cabin = null;
        foreach (var t in S.Things) if (t.Kind == "hut" && (cabin == null || t.Pos.DistanceTo(Player) < cabin.Pos.DistanceTo(Player))) cabin = t;
        S.Health = 100; S.Hunger = 65; S.Thirst = 65; Player = cabin == null ? Terrain.Spawn : Doorstep(cabin); S.Actor.Warp++; S.Mauled = false; S.Sheltered = cabin?.Id ?? 0; S.Wolves.RemoveAll(w => w.Target == S.Actor.Id);
        Say(cabin == null ? "You wake in the clearing. Recover your belongings from the box." : "You wake in the cabin. Recover your belongings from the box.");
    }
    /// <summary>Open ground just outside a cabin's walls, where its occupant stands when they step out.</summary>
    private Vector2 Doorstep(Thing cabin) {
        for (int i = 0; i < 8; i++) {
            var p = cabin.Pos + Vector2.Down.Rotated(i * MathF.PI / 4) * (Footprint("hut") + .6f);
            if (CanStand(p)) return p;
        }
        return cabin.Pos + Vector2.Down * (Footprint("hut") + .6f);
    }
    // Only sales move demand, and purchases always cost more than the best sale price, so a buy-then-sell round trip never profits.
    public float UnitPrice(string item, bool buying) => Data.Items[item].Price * (buying ? 1.25f : Math.Min(1, Demand(item)));
    // Demand runs above 1 by the steady amount's worth, so those units sell at the full price; past it, each unit takes a share of a crate's drop.
    private float DemandStep(string item) => Data.Market.Sells[item].Drop / Data.CrateCapacity;
    private float FullDemand(string item) => Data.Market.Sells.TryGetValue(item, out var demand) ? 1 + demand.Steady * DemandStep(item) : 1;
    private float Demand(string item) => S.Demand.TryGetValue(item, out float demand) ? demand : FullDemand(item);
    /// <summary>Units in one purchase of a good the village sells; more than one arrive packed in a crate.</summary>
    public int Lot(string item) => Data.Market.Buys.GetValueOrDefault(item, 1);
    /// <summary>The whole coins one purchase costs, crate included for boxed goods.</summary>
    public int LotPrice(string item) => (int)MathF.Ceiling(1.25f * (Data.Items[item].Price * Lot(item) + (Lot(item) > 1 ? Data.Items["crate"].Price : 0)));
    public const int MarketSlots = 6;
    private static Inventory NewBurner() => new() { Slots = 1 };
    // Stations with a recipe that burns fuel, worked out once from the data.
    private HashSet<string>? burning;
    public const int MaxWorkers = 8;
    /// <summary>Whether a kind of building has a fuel slot.</summary>
    public bool Burns(string kind) => (burning ??= Data.Recipes.Values.Where(r => r.Fuel).Select(r => r.Station).ToHashSet()).Contains(kind);
    /// <summary>Items a station's fuel slot takes: coal at the reinforced furnace, wood or coal elsewhere.</summary>
    public static bool FuelFits(Thing t, string item) => t.Kind == "reinforced" ? item == "coal" : item is "wood" or "coal";
    private static Inventory NewWageSlot() => new() { Slots = 1, InputFilter = { Inventory.Coins }, OutputFilter = { Inventory.Coins } };
    /// <summary>Why an item cannot go into this box by hand: a wage slot only holds coins.</summary>
    public string? BoxBlock(Thing t, Inventory box, string item) =>
        box == t.Wages && item != Inventory.Coins ? "The wage slot only holds coins." :
        box == t.Burner ? FuelBlock(t, item) :
        box == t.Output ? "Things only come out of here." :
        box == t.Inventory ? (PartialRecipeBottle(t, item) ? Water.FullBottleWarning : HoldBlock(t, item)) : null;
    /// <summary>Why a fuel slot will not take an item: it burns only fuel, one kind at a time.</summary>
    public string? FuelBlock(Thing t, string item) =>
        !FuelFits(t, item) ? (t.Kind == "reinforced" ? "The reinforced furnace burns only mined coal." : "The fuel slot takes wood or coal.") :
        t.Burner?.First() is string held && held != item ? $"The fuel slot holds {Data.Name(held).ToLowerInvariant()}. Take it out first." : null;
    /// <summary>Coins kept for wages in every signpost's wage slot.</summary>
    public int WageCoins {
        get {
            int coins = 0;
            foreach (var t in S.Things) if (t.Wages != null) coins += t.Wages[Inventory.Coins];
            return coins;
        }
    }
    /// <summary>Pays wages from the signposts' wage slots first, then the acting player's backpack, or nothing if together they fall short.</summary>
    private bool PayWage(int amount) {
        if (amount > WageCoins + Coins) return false;
        foreach (var box in S.Things.Select(t => t.Wages).OfType<Inventory>()) amount -= box.Remove(Inventory.Coins, amount);
        return Pay(amount);
    }
    /// <summary>The village only takes packed crates holding nothing but goods it buys.</summary>
    public bool CanSell(string item) => Packed(item) is { Count: > 0 } contents && contents.Items.Keys.All(Data.Market.Sells.ContainsKey);
    /// <summary>The sale price as last reported from the village; it goes stale until someone arrives with news.</summary>
    public float KnownPrice(string item) => S.Prices.TryGetValue(item, out float price) ? price : Data.Items[item].Price;
    public int PriceAge => Day - (int)(S.PricesTime / Data.DaySeconds) - 1;
    private void UpdatePrices() {
        S.Prices = Data.Market.Sells.Keys.ToDictionary(item => item, item => UnitPrice(item, false)); S.PricesTime = S.Time;
    }
    private string? CartBlock(Thing sign) => !Near(sign) || sign.Kind != "signpost" ? "Walk closer to the trading post." : S.Orders.Count >= 3 ? "All three village carts are busy." : null;
    /// <summary>Sends every acceptable crate in the market input on one cart; the coins come back into the market output.</summary>
    public bool Sell(Thing sign) {
        if (CartBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Forwarded("sell", sign.Id)) return false;
        var crates = sign.Inventory.Items.Keys.Where(CanSell).ToList();
        if (crates.Count == 0) { Say(sign.Inventory.Count > 0 ? "The village only takes crates packed with meat, berries or steel tools." : "Put a packed crate in the market first."); return false; }
        var goods = new Dictionary<string, int>();
        foreach (var crate in crates) {
            foreach (var (item, quantity) in Packed(crate)!.Items) goods[item] = goods.GetValueOrDefault(item) + quantity;
            sign.Inventory.Remove(crate, 1); S.Crates.Remove(PackedId(crate));
        }
        // The crates go with the goods.
        float price = crates.Count * Data.Items["crate"].Price;
        foreach (var (item, quantity) in goods) {
            // A sale lowers demand unit by unit, so a bulk sale is not paid entirely at the pre-sale price.
            float demand = Demand(item);
            for (int i = 0; i < quantity; i++) { price += Data.Items[item].Price * Math.Min(1, demand); demand = Math.Max(.25f, demand - DemandStep(item)); }
            S.Demand[item] = demand;
        }
        // Coins are whole: sales round down and purchases round up, so a round trip never profits.
        S.Orders.Add(new TradeOrder { Goods = goods, Value = MathF.Floor(price), Signpost = sign.Id, Crates = crates.Count });
        Say($"Cart left for Bramblewick with {crates.Count} {(crates.Count == 1 ? "crate" : "crates")}: {Goods(goods)}. Back in {Data.TradeSeconds:0} seconds."); return true;
    }
    private string Goods(Dictionary<string, int> goods) => string.Join(", ", goods.Select(p => $"{p.Value} {Data.Name(p.Key)}"));
    /// <summary>Orders <paramref name="lots"/> purchases of a good the village sells: tools one by one, the rest in crates.</summary>
    public bool Buy(Thing sign, string item, int lots) {
        if (!Data.Market.Buys.ContainsKey(item) || lots <= 0) return false;
        if (CartBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Forwarded("buy", sign.Id, item: item, count: lots)) return false;
        int price = LotPrice(item) * lots, units = Lot(item) * lots;
        if (!Pay(price)) { Say("Not enough coins in your backpack."); return false; }
        S.Orders.Add(new TradeOrder { Goods = new() { [item] = units }, Buying = true, Value = price, Signpost = sign.Id, Crates = Lot(item) > 1 ? lots : 0 });
        Say($"Ordered {(Lot(item) > 1 ? $"{lots} {(lots == 1 ? "crate" : "crates")} of " : "")}{units} {Data.Name(item)} for {price} coins. Back in {Data.TradeSeconds:0} seconds."); return true;
    }
    // ---------- Crates ----------
    /// <summary>Packed crates are items named by this prefix and a number that keys their contents in <see cref="SaveState.Crates"/>.</summary>
    public const string PackedPrefix = "crate_";
    public static bool IsCrate(string item) => item == "crate" || item.StartsWith(PackedPrefix);
    private static int PackedId(string item) => item.StartsWith(PackedPrefix) && int.TryParse(item[PackedPrefix.Length..], out int id) ? id : 0;
    /// <summary>The contents of a packed crate item, or null for anything else.</summary>
    public Inventory? Packed(string item) => S.Crates.GetValueOrDefault(PackedId(item));
    public Inventory NewCrate() => new() { Slots = Data.CrateCapacity, StackSize = Data.CrateCapacity, Capacity = Data.CrateCapacity };
    /// <summary>Packs contents into a new crate item.</summary>
    private string Pack(Inventory contents) {
        int id = S.NextId++; S.Crates[id] = contents; return PackedPrefix + id;
    }
    /// <summary>An item's name, with what a packed crate holds.</summary>
    public string Describe(string item) => Packed(item) is Inventory contents ? $"{Data.Name("crate")} of {Goods(contents.Items)}" : Data.Name(item);
    /// <summary>Puts a crate from the backpack down, still holding whatever it was packed with.</summary>
    public Thing? PlaceCrate(string item, Vector2 p) {
        if (!IsCrate(item)) return null;
        if (PlacementBlock("crate", p, item) is string blocked) { Say(blocked); return null; }
        if (Forwarded("place_crate", item: item, at: p)) return null;
        S.Backpack.Remove(item, 1);
        var crate = Add("crate", p);
        if (Packed(item) is Inventory contents) { crate.Inventory = contents; S.Crates.Remove(PackedId(item)); }
        Play("close_chest", p); Say($"Placed {Data.Name("crate")}. Left click to open it; hold right click to pick it up."); return crate;
    }
    /// <summary>Lifts a placed crate into the backpack; anything inside stays packed in it.</summary>
    public bool PickUpCrate(Thing crate) {
        if (crate.Kind != "crate" || !Near(crate) || Forwarded("pick_up_crate", crate.Id)) return false;
        bool empty = crate.Inventory.Count == 0;
        if (S.Backpack.Room(empty ? "crate" : PackedPrefix) == 0) { Say("Backpack full. Make room for the crate."); return false; }
        S.Backpack.Add(empty ? "crate" : Pack(crate.Inventory), 1); S.Things.Remove(crate);
        Play("wood_rocks_inventory", crate.Pos); PlayerActed?.Invoke(ActorAction.PickUp, crate.Pos); return true;
    }
    // ---------- Crafting ----------
    /// <summary>Hand recipes for the personal crafting menu.</summary>
    public IEnumerable<string> HandRecipes => Data.Recipes.Where(p => p.Value.Hand).Select(p => p.Key);
    private bool PartialRecipeBottle(Thing t, string item) => item != "water" && Water.IsBottle(item) && BatchCost(t).ContainsKey("water");
    private static bool NeedsFullBottle(Dictionary<string, int> cost, Inventory bag) =>
        cost.TryGetValue("water", out int needed) && bag["water"] < needed &&
        (bag["water_2"] > 0 || bag["water_1"] > 0 || bag["empty_bottle"] > 0);
    private enum CraftRefusal { None, StationOnly, Recovering, NoTool, NoInputs, QueueFull, FullBottle }
    /// <summary>The one place the hand-crafting rules live; <see cref="CanCraft"/> and <see cref="CraftBlock"/> both read it.</summary>
    private CraftRefusal CraftCheck(string recipe) {
        if (!Data.Recipes.TryGetValue(recipe, out var r) || !r.Hand) return CraftRefusal.StationOnly;
        if (Downed) return CraftRefusal.Recovering;
        if (r.Tool != "" && BestTool(r.Tool, null, r.Tier) == null) return CraftRefusal.NoTool;
        if (NeedsFullBottle(r.Inputs, S.Backpack)) return CraftRefusal.FullBottle;
        if (!S.Backpack.Has(r.Inputs)) return CraftRefusal.NoInputs;
        return S.Crafting.Count >= 8 ? CraftRefusal.QueueFull : CraftRefusal.None;
    }
    /// <summary>Whether a hand recipe can be queued now, without building the reason; the crafting page asks on every refresh.</summary>
    public bool CanCraft(string recipe) => CraftCheck(recipe) == CraftRefusal.None;
    /// <summary>Why a recipe cannot be queued from the backpack now, or null.</summary>
    public string? CraftBlock(string recipe) => CraftCheck(recipe) switch {
        CraftRefusal.FullBottle => Water.FullBottleWarning,
        CraftRefusal.StationOnly => "This can only be made at a station.",
        CraftRefusal.Recovering => "You are still recovering.",
        CraftRefusal.NoTool => $"Needs a {Data.ToolName(Data.Recipes[recipe].Tool, Data.Recipes[recipe].Tier)} in your backpack.",
        CraftRefusal.NoInputs => $"Needs {Goods(Data.Recipes[recipe].Inputs)} in your backpack.",
        CraftRefusal.QueueFull => "Eight things are already being made.",
        _ => null
    };
    /// <summary>Queues a hand recipe; its inputs leave the backpack now so they cannot be spent twice.</summary>
    public bool Craft(string recipe) {
        if (CraftBlock(recipe) is string blocked) { Say(blocked); return false; }
        if (Forwarded("craft", item: recipe)) return false;
        S.Backpack.Spend(Data.Recipes[recipe].Inputs); S.Crafting.Add(recipe); return true;
    }
    /// <summary>Cancels a queued recipe and returns its inputs; what the backpack cannot hold goes into a box at the player's feet.</summary>
    public void CancelCraft(int index) {
        if (index < 0 || index >= S.Crafting.Count || Forwarded("cancel_craft", count: index)) return;
        foreach (var (item, quantity) in Data.Recipes[S.Crafting[index]].Inputs) Give(item, quantity);
        S.Crafting.RemoveAt(index); if (index == 0) S.CraftTime = 0;
    }
    private void Give(string item, int quantity) {
        int left = quantity - S.Backpack.Add(item, quantity);
        if (left <= 0) return;
        var box = Add("grave", Player); box.Inventory.Slots = left / box.Inventory.StackOf(item) + 1; box.Inventory.Add(item, left);
    }
    /// <summary>How fast the first queued recipe progresses: the carried tool's speed, or zero while its tool is missing.</summary>
    public float CraftRate => S.Crafting.Count == 0 || Downed ? 0 : Data.Recipes[S.Crafting[0]] is var r && r.Tool != "" ? BestTool(r.Tool, null, r.Tier) is string held ? Speed(held) : 0 : 1;
    private void CraftTick(float dt) {
        float rate = CraftRate;
        if (rate <= 0) return;
        var r = Data.Recipes[S.Crafting[0]];
        S.CraftTime = Math.Min(r.Seconds, S.CraftTime + dt * rate);
        // A full backpack holds the finished item back rather than losing it.
        if (S.CraftTime < r.Seconds || S.Backpack.Room(r.Output) < r.Amount) return;
        S.Backpack.Add(r.Output, r.Amount); S.Crafting.RemoveAt(0); S.CraftTime = 0;
        if (r.Inputs.TryGetValue("water", out int usedWater)) Give("empty_bottle", usedWater);
        Crafted(r.Output); Play(Sounds.ForItem(r.Output), Player);
    }
    private void Crafted(string output) {
        S.Milestones.Add(output);
        if (Data.Items[output].Tool == "pickaxe") S.Milestones.Add("pick");
        if (output.StartsWith("steel_") && Data.Items[output].Tool != "") S.Milestones.Add("steel_tool");
    }
    /// <summary>The tool kinds a station has slots for.</summary>
    public static string[] SlotKinds(string kind) => kind switch { "bench" => new[] { "chisel", "hammer" }, "cooktable" => new[] { "mortar" }, _ => Array.Empty<string>() };
    private static Inventory NewToolSlots(string kind) => new() { Slots = SlotKinds(kind).Length, StackSize = 1 };
    /// <summary>The tool of a kind slotted in a station, or null when its slot is empty or holds a tool below <paramref name="tier"/>.</summary>
    public string? BenchTool(Thing bench, string tool, int tier = 0) => bench.Tools == null ? null : BestTool(tool, bench.Tools, tier);
    /// <summary>Puts a tool from the backpack into the station's empty slot for its kind.</summary>
    public bool SlotTool(Thing bench, string item) {
        string name = Data.Name(bench.Kind).ToLowerInvariant();
        if (bench.Tools == null || !Near(bench)) { Say($"Walk closer to the {name}."); return false; }
        string tool = Data.Items.TryGetValue(item, out var def) ? def.Tool : "";
        var kinds = SlotKinds(bench.Kind);
        if (!kinds.Contains(tool)) { Say($"The {name} has slots for {(kinds.Length == 1 ? A(kinds[0]) : $"a {kinds[0]} and a {kinds[1]}")}."); return false; }
        if (BenchTool(bench, tool) != null) { Say($"The {tool} slot is taken. Take that {tool} out first."); return false; }
        if (Forwarded("slot_tool", bench.Id, item: item)) return false;
        return S.Backpack.TransferTo(bench.Tools, item, 1) == 1;
    }
    public void UnslotTool(Thing bench, string item) {
        if (bench.Tools == null || !Near(bench)) { Say($"Walk closer to the {Data.Name(bench.Kind).ToLowerInvariant()}."); return; }
        if (Forwarded("unslot_tool", bench.Id, item: item)) return;
        if (bench.Tools.TransferTo(S.Backpack, item, 1) == 0 && bench.Tools[item] > 0) Say("Backpack full.");
    }
    public string? FetchBlock(Thing sign) {
        bool assigned = false, working = false;
        foreach (var w in S.Workers) if (HasDuty(w, sign.Id, "deliveries")) { assigned = true; working |= w.WageDebt <= 0; }
        if (!assigned) return "No worker assigned.";
        if (Dusk) return "Your workers have signed out until morning.";
        if (Lunch) return "Your workers are at lunch.";
        foreach (var w in S.Workers) if (HasDuty(w, sign.Id, "deliveries") && w.Away > 0) return Phrase(w.Name, " is already on the way.");
        return working ? null : "The assigned worker is on strike.";
    }
    /// <summary>Sends the post's worker to the village; the price sheet refreshes when they return.</summary>
    public bool FetchPrices(Thing sign) {
        if (FetchBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Forwarded("fetch_prices", sign.Id)) return false;
        var w = S.Workers.First(w => HasDuty(w, sign.Id, "deliveries") && w.WageDebt <= 0);
        w.Assignment = sign.Id; w.Away = Data.TradeSeconds; Say($"{w.Name} set off to Bramblewick for fresh prices."); return true;
    }
    public Worker? Hire(Thing sign, HireOffer offer) {
        if (!Near(sign) || sign.Kind != "signpost" || Forwarded("hire", sign.Id, count: Data.HireOffers.IndexOf(offer))) return null;
        if (S.Workers.Count >= MaxWorkers) { Say("The POC supports eight workers."); return null; }
        if (!Pay(offer.Upfront)) { Say($"This offer needs {offer.Upfront:0} coins up front from your backpack."); return null; }
        string[] names = { "Rowan", "Hazel", "Milo", "Fern", "Jasper", "Willow", "Clover", "Ash" };
        string name = names.FirstOrDefault(n => S.Workers.All(w => w.Name != n)) ?? "Neighbor";
        var w = new Worker { Id = S.NextId++, Name = name, Pos = sign.Pos + new Vector2(1, 0), Wage = offer.Wage };
        S.Workers.Add(w); S.Milestones.Add("hired"); Say($"{name} joined. Give them jobs with the job buttons at a building, or from their panel send them to gather."); return w;
    }
    /// <summary>The jobs a place offers workers, in the order the panel shows them.</summary>
    public string[] Jobs(Thing t) => t.IsResource ? GatherJobs : t.Kind switch {
        "reinforced" => BellowsJobs,
        "bench" or "anvil" or "cooktable" => CraftJobs,
        "furnace" or "cannery" => HaulJobs,
        "signpost" => DeliveryJobs,
        "campfire" => FireJobs,
        "farm" => FarmJobs,
        "mine" => MineJobs,
        "toolstation" => StockJobs,
        "pile" when t.Compost => TendJobs,
        _ => NoJobs
    };
    private static readonly string[] GatherJobs = { "gather" }, BellowsJobs = { "bellows", "haul" }, CraftJobs = { "craft", "haul" }, HaulJobs = { "haul" },
        DeliveryJobs = { "deliveries" }, FireJobs = { "fuel" }, FarmJobs = { "farm", "haul" }, MineJobs = { "build", "haul", "mine" }, StockJobs = { "stock" },
        TendJobs = { "tend" }, NoJobs = { };
    /// <summary>Workers with any job at a place.</summary>
    public int Crew(Thing t) {
        int crew = 0;
        foreach (var w in S.Workers) if (HasDuty(w, t.Id)) crew++;
        return crew;
    }
    /// <summary>Puts a worker on every job at one place, instead of whatever they did before, or takes all their jobs away.</summary>
    public void Assign(Worker w, Thing? target) {
        if (target?.Kind == "mine" && !HasDuty(w, target.Id) && Crew(target) >= Data.Mine.Workers) { Say($"A mine has room for {Data.Mine.Workers} workers."); return; }
        if (Forwarded("assign", target?.Id ?? 0, w.Id)) return;
        w.Duties.Clear();
        if (target != null) foreach (string job in Jobs(target)) w.Duties.Add(new Duty { Thing = target.Id, Job = job });
        // A resource has no listed jobs only by mistake; gathering is what a worker does there.
        if (target != null && w.Duties.Count == 0) w.Duties.Add(new Duty { Thing = target.Id, Job = target.IsResource ? "gather" : "work" });
        Started(w, target);
    }
    /// <summary>Gives a worker one job at a place, or takes it away, keeping their other jobs; with jobs at several places they take turns.</summary>
    public void SetDuty(Worker w, Thing target, string job, bool on) {
        if (on == HasDuty(w, target.Id, job) || Array.IndexOf(Jobs(target), job) < 0) return;
        if (on && target.Kind == "mine" && !HasDuty(w, target.Id) && Crew(target) >= Data.Mine.Workers) { Say($"A mine has room for {Data.Mine.Workers} workers."); return; }
        if (Forwarded("duty", target.Id, w.Id, job, on ? 1 : 0)) return;
        if (on) w.Duties.Add(new Duty { Thing = target.Id, Job = job });
        else w.Duties.RemoveAll(d => d.Thing == target.Id && d.Job == job);
        if (on && w.Duties.Count == 1 || !on && w.Assignment == target.Id && !HasDuty(w, target.Id)) Started(w, w.Duties.Count > 0 ? Find(w.Duties[0].Thing) : null);
    }
    private void Started(Worker w, Thing? target) {
        // Reassigning calls off a price errand, so the worker never freezes on the road.
        w.Assignment = target?.Id ?? 0; w.Away = 0; w.WorkState = WorkerWorkState.Idle;
        w.Destination = 0; w.Timer = 0; w.Status = target == null ? "Available" : "Starting work";
        if (target != null && target.Kind != "signpost") S.Milestones.Add("assigned");
    }
    public void Fire(Worker w) {
        if (Forwarded("fire", other: w.Id)) return;
        if (w.Bag.Count > 0 || w.Tool != "") { var box = Add("grave", w.Pos); box.Inventory = w.Bag; box.Inventory.Capacity = 0; box.Inventory.Slots = box.Inventory.UsedSlots + 1; box.Inventory.Add(w.Tool, w.Tool == "" ? 0 : 1); }
        S.Workers.Remove(w); Say($"{w.Name} has left. Carried items were left in a recovery box.");
    }
    private enum HoldRefusal { None, ToolsOnly, CompostOnly, OtherResource, FarmOnly }
    /// <summary>The one place the storage rules live: a tool station holds only tools, a pile one resource and compost what rots; other storage holds anything.</summary>
    private HoldRefusal HoldCheck(Thing t, string item) => t.Kind switch {
        "toolstation" when !(Data.Items.TryGetValue(item, out var def) && def.Tool != "") => HoldRefusal.ToolsOnly,
        "pile" when t.Compost && !Compostable(item) && item != "fertilizer" => HoldRefusal.CompostOnly,
        "pile" when !t.Compost && HeldOtherThan(t.Inventory, item) != null => HoldRefusal.OtherResource,
        "farm" when !Data.Farm.Sow.ContainsKey(item) => HoldRefusal.FarmOnly,
        _ => HoldRefusal.None
    };
    /// <summary>Whether storage takes an item, without building the reason; workers ask on every tick.</summary>
    public bool Holds(Thing t, string item) => HoldCheck(t, item) == HoldRefusal.None;
    /// <summary>Why storage will not take an item, or null.</summary>
    public string? HoldBlock(Thing t, string item) => HoldCheck(t, item) switch {
        HoldRefusal.ToolsOnly => "The tool station only holds tools.",
        HoldRefusal.CompostOnly => "The compost takes leaves, grass and food.",
        HoldRefusal.OtherResource => $"This pile holds {Data.Name(HeldOtherThan(t.Inventory, item)!)}. Empty it to store something else.",
        HoldRefusal.FarmOnly => $"A farm plot takes {string.Join(" and ", Data.Farm.Sow.Keys.Select(Data.Name)).ToLowerInvariant()}.",
        _ => null
    };
    private static string? HeldOtherThan(Inventory inventory, string item) {
        foreach (var pair in inventory.Items) if (pair.Key != item) return pair.Key;
        return null;
    }
    public bool Compostable(string item) => item is "leaves" or "grass" || Data.IsFood(item);
    /// <summary>Turns a pile into compost or back; refused while it holds something the new use would not take.</summary>
    public bool SetCompost(Thing pile, bool on) {
        if (pile.Kind != "pile" || pile.Compost == on) return false;
        var items = pile.Inventory.Items.Keys.ToList();
        if (on ? items.Any(i => !Compostable(i) && i != "fertilizer") : items.Count > 1) { Say(on ? "Empty the pile before composting." : "Empty the compost down to one resource first."); return false; }
        if (Forwarded("set_compost", pile.Id, count: on ? 1 : 0)) return true;
        pile.Compost = on; pile.Progress = 0;
        // Compost starts out taking everything that rots and giving fertilizer; its filters are then the player's to change.
        pile.Inventory.InputFilter.Clear(); pile.Inventory.OutputFilter.Clear();
        if (on) { foreach (var item in compostables ??= Data.Items.Keys.Where(Compostable).ToArray()) pile.Inventory.InputFilter.Add(item); pile.Inventory.OutputFilter.Add("fertilizer"); }
        PileFilters(pile); return true;
    }
    /// <summary>Piles keep their automation filters in step with what they hold, one resource in and out; compost keeps the filters it was given.</summary>
    private string[]? compostables;
    private void PileFilters(Thing pile) {
        if (pile.Compost) return;
        var inv = pile.Inventory;
        inv.InputFilter.Clear(); inv.OutputFilter.Clear();
        if (inv.First() is string item) {
            inv.InputFilter.Add(item);
            inv.OutputFilter.Add(item);
        }
    }
    /// <summary>Compost turns 4 units of whatever it holds into 1 fertilizer each in-game hour.</summary>
    public const int CompostBatch = 4;
    private void CompostTick(Thing pile, float dt) {
        int rotting = 0;
        foreach (var pair in pile.Inventory.Items) if (Compostable(pair.Key)) rotting += pair.Value;
        if (rotting < CompostBatch || pile.Inventory.Room("fertilizer") == 0) { pile.Progress = 0; return; }
        pile.Progress += dt;
        if (pile.Progress < Data.DaySeconds / 24) return;
        pile.Progress = 0;
        for (int left = CompostBatch; left > 0;) left -= pile.Inventory.Remove(pile.Inventory.Items.Keys.First(Compostable), left);
        pile.Inventory.Add("fertilizer", 1);
    }
    // Only chests and piles take part in automation; recovery boxes hold the player's or a dismissed worker's belongings.
    public Thing? DepositFor(string item, Vector2 origin, int exclude = 0) {
        Thing? best = null; int bestRank = 0; float bestDistance = 0;
        foreach (var t in S.Things) {
            if (t.Kind is not ("chest" or "pile") || t.Id == exclude || t.Inventory.Room(item) <= 0 || !t.Inventory.Accepts(item) || !Holds(t, item)) continue;
            // Compost names every food in its filter, but takes food only when nothing else will, so harvests and picked berries are not left to rot.
            if (t.Compost && t.SkipFood && Data.IsFood(item)) continue;
            int rank = t.Compost && Data.IsFood(item) ? 2 : t.Inventory.InputFilter.Contains(item) ? 0 : 1;
            float distance = t.Pos.DistanceSquaredTo(origin);
            if (best == null || rank < bestRank || rank == bestRank && distance < bestDistance) { best = t; bestRank = rank; bestDistance = distance; }
        }
        return best;
    }
    public Thing? SourceFor(string item, Vector2 origin, int exclude = 0) {
        Thing? best = null; float bestDistance = 0;
        foreach (var t in S.Things) {
            if (t.Kind is not ("chest" or "pile") || t.Id == exclude || t.Inventory[item] <= 0 || !t.Inventory.AllowsWithdrawal(item)) continue;
            float distance = t.Pos.DistanceSquaredTo(origin);
            if (best == null || distance < bestDistance) { best = t; bestDistance = distance; }
        }
        return best;
    }
    /// <summary>The nearest thing of a kind, or null.</summary>
    private Thing? Nearest(string kind, Vector2 origin) {
        Thing? best = null; float bestDistance = 0;
        foreach (var t in S.Things) {
            if (t.Kind != kind) continue;
            float distance = t.Pos.DistanceSquaredTo(origin);
            if (best == null || distance < bestDistance) { best = t; bestDistance = distance; }
        }
        return best;
    }
    // A search on the large map can visit thousands of tiles, so its buffers are kept: tiles are marked with the search's number rather than cleared.
    private int[]? routeSeen, routeFrom;
    private readonly Queue<Vector2I> routeQueue = new();
    private readonly List<Thing> routeBlockers = new();
    private int routeSearch;
    private static readonly Vector2I[] RouteSteps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };
    private bool RouteOpen(Vector2I p) {
        if (!Open(p)) return false;
        foreach (var t in routeBlockers) if (Blocks(t, p)) return false;
        return true;
    }
    private static int RouteIndex(Vector2I p) => p.Y * Terrain.Size + p.X;
    private static bool OnMap(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < Terrain.Size && p.Y < Terrain.Size;
    /// <summary>Appends the tiles from <paramref name="from"/> to within reach of <paramref name="destination"/> to <paramref name="route"/>, or nothing when there is no way.</summary>
    private void FindRoute(Vector2 from, Vector2 destination, List<Vector2> route) {
        routeSeen ??= new int[Terrain.Size * Terrain.Size]; routeFrom ??= new int[Terrain.Size * Terrain.Size];
        int search = ++routeSearch;
        // Collected once, since a search on the large map can visit thousands of tiles.
        routeBlockers.Clear();
        foreach (var t in S.Things) if (Blocks(t, t.Pos)) routeBlockers.Add(t);
        var start = new Vector2I((int)MathF.Round(from.X), (int)MathF.Round(from.Y));
        if (!OnMap(start)) return;
        routeQueue.Clear(); routeQueue.Enqueue(start);
        routeSeen[RouteIndex(start)] = search;
        int end = -1;
        while (routeQueue.Count > 0) {
            var current = routeQueue.Dequeue();
            if (((Vector2)current).DistanceTo(destination) <= 1.15f) { end = RouteIndex(current); break; }
            foreach (var step in RouteSteps) {
                var next = current + step;
                if (!OnMap(next) || routeSeen[RouteIndex(next)] == search || !RouteOpen(next)) continue;
                if (step.X != 0 && step.Y != 0 && (!RouteOpen(current + new Vector2I(step.X, 0)) || !RouteOpen(current + new Vector2I(0, step.Y)))) continue;
                routeSeen[RouteIndex(next)] = search; routeFrom[RouteIndex(next)] = RouteIndex(current); routeQueue.Enqueue(next);
            }
        }
        if (end < 0) return;
        int first = route.Count;
        for (int node = end; node != RouteIndex(start); node = routeFrom[node]) route.Add(new Vector2(node % Terrain.Size, node / Terrain.Size));
        route.Reverse(first, route.Count - first);
    }
    /// <summary>Moves a worker toward a destination and reports arrival within <paramref name="reach"/>.
    /// A spot closer than the usual reach is a preference: if it cannot be reached, the usual reach counts as arrival.</summary>
    private bool Walk(Worker w, Vector2 destination, float dt, float reach = 1.2f) {
        float distance = w.Pos.DistanceTo(destination);
        if (distance < reach) return true;
        if (w.RouteTarget.DistanceTo(destination) > .2f) { w.Route.Clear(); w.RouteTarget = destination; }
        // Stop at arrival range rather than overshooting into the target's footprint, where the step would be refused.
        var next = w.Pos.MoveToward(destination, Math.Min(dt * 2.25f, distance - reach + .05f));
        if (w.Route.Count == 0 && CanStand(next)) { w.Pos = next; return false; }
        if (w.Route.Count > 0 && !CanStand(w.Route[0])) w.Route.Clear();
        if (w.Route.Count == 0) FindRoute(w.Pos, destination, w.Route);
        if (w.Route.Count == 0 && distance < 1.2f) return true;
        if (w.Route.Count == 0) { w.Status = "Path blocked · clear a route"; w.WorkState = WorkerWorkState.Blocked; return false; }
        w.Pos = w.Pos.MoveToward(w.Route[0], dt * 2.25f);
        if (w.Pos.DistanceTo(w.Route[0]) < .06f) w.Route.RemoveAt(0);
        return false;
    }
    private void WorkerTick(Worker w, float dt) {
        w.WorkState = WorkerWorkState.Idle;
        if (w.Away > 0) {
            w.Away = Math.Max(0, w.Away - dt); w.Status = Phrase("Fetching village prices · back in ", Number(w.Away), "s");
            // Like a cart, the runner walks a little way down the valley and fades from view rather than pathing around the woods.
            if (Find(w.Assignment) is { Kind: "signpost" } post) w.Pos = post.Pos + Vector2.One + Terrain.Seaward * TripDistance * TripLeg(1 - w.Away / Data.TradeSeconds);
            if (w.Away == 0) { UpdatePrices(); Say($"{w.Name} is back with fresh prices from Bramblewick."); }
            return;
        }
        // Like a cart, the walk home goes a little way down the valley over half a trade trip while the worker fades out, and back at dawn.
        float half = Data.TradeSeconds / 2, pace = TripDistance / half;
        if (w.Home && Dusk) { w.Status = "Signed out · back in the morning"; return; }
        if (!Dusk && w.Commute > 0) {
            w.Home = false; w.Status = "Walking in from Bramblewick";
            float step = Math.Min(dt, w.Commute);
            w.Commute -= step; w.Pos -= Terrain.Seaward * pace * step;
            if (w.Commute == 0) w.Route.Clear();
            return;
        }
        w.Home = false;
        if (Dusk) {
            // Everyone signs out from the nearest signpost, or the clearing without one, and comes back there at dawn.
            // Assignments and loads are kept, and one cut off from the way still sets off.
            w.Status = "Heading home to Bramblewick";
            if (w.Commute == 0) {
                if (w.Tool != "" && !ReturnTool(w, dt)) return;
                var post = Nearest("signpost", w.Pos);
                if (!Walk(w, post?.Pos + Vector2.One ?? Terrain.Spawn, dt) && w.WorkState != WorkerWorkState.Blocked) return;
                w.WorkState = WorkerWorkState.Idle;
            }
            float step = Math.Min(dt, half - w.Commute);
            w.Commute += step; w.Pos += Terrain.Seaward * pace * step;
            if (w.Commute >= half) { w.Home = true; w.Status = "Signed out · back in the morning"; }
            return;
        }
        if (w.WageDebt > 0) { w.Status = "On strike · wages owed"; return; }
        if (Lunch) { EatLunch(w, dt); return; }
        TidyDuties(w);
        var target = Find(w.Assignment);
        if (target?.Kind == "water" && w.Destination == target.Id && Water.FindBottle(w.Bag) is string bottle && bottle != "water") {
            WaterWork(w, target, dt); return;
        }
        if (w.Bag.Count > 0) {
            string item = w.Bag.First()!;
            var dest = Find(w.Destination);
            // Re-evaluate rules every tick, including filters changed while a worker is carrying goods.
            // A mine site takes only what it still needs, so a surplus goes back to storage.
            int wanted = dest?.Kind == "mine" ? MineNeed(dest, item) : int.MaxValue;
            // Fuel for a station goes into its fuel slot; once that is full or gone, it goes to storage like anything else.
            var into = w.Fueling && dest?.Burner != null && FuelBlock(dest, item) == null ? dest.Burner : dest?.Inventory;
            if (dest == null || into == null || wanted == 0 || into.Room(item) == 0 || !into.Accepts(item) || (dest.Id == target?.Id && target.IsResource)) { dest = DepositFor(item, w.Pos); into = dest?.Inventory; w.Fueling = false; }
            if (dest == null) { w.Status = Phrase("Holding ", Data.Name(item), " · no accepting storage"); return; }
            w.Destination = dest.Id; w.Status = Phrase("Carrying ", Data.Name(item), " → ", Data.Name(dest.Kind));
            if (Walk(w, dest.Pos, dt)) {
                w.Bag.TransferTo(into!, item, dest.Kind == "mine" ? MineNeed(dest, item) : w.Bag[item], true); w.Destination = 0;
                if (w.Bag.Count == 0) { w.Fueling = false; w.Hands(Worker.BagStack); NextPlace(w); }
            }
            return;
        }
        string? need = target is { IsResource: true } && HasDuty(w, target.Id, "gather") ? ToolKind(target) : target is { Kind: "mine" } ? MineTool(w, target) : null;
        if (need == null) w.Missing = "";
        if (w.Tool != "" && Data.Items[w.Tool].Tool != need) {
            if (!ReturnTool(w, dt)) return;
            // With no room in a tool station the tool stays in hand, and a job that needs another kind waits for room.
            if (w.Tool != "" && need != null) { w.Status = Phrase("No room to put back the ", Lower(Data.Name(w.Tool))); return; }
        }
        if (target == null) { w.Status = "Available"; return; }
        // With nothing to do here, a worker with jobs elsewhere moves on to the next place.
        if (!WorkAt(w, target, need, dt)) NextPlace(w);
    }
    /// <summary>Works the worker's jobs at one place and tells whether there was anything to do.</summary>
    private bool WorkAt(Worker w, Thing target, string? need, float dt) {
        if (target.Kind == "water") return WaterWork(w, target, dt);
        if (target.IsResource) {
            string? blocked = GatherBlock(target, true);
            if (blocked != null) { w.Status = blocked; return false; }
            string item = ResourceItem(target);
            if (DepositFor(item, target.Pos) == null) { w.Status = "Needs an accepting storage chest"; return false; }
            if (need != null && w.Tool == "") { FetchTool(w, need, target, dt); return w.Missing == ""; }
            w.Status = Phrase("Gathering ", Data.Name(item));
            if (!Walk(w, target.Pos, dt)) return true;
            w.WorkState = WorkerWorkState.Gathering;
            w.Timer += dt;
            if (target.IsLoose || w.Timer >= Data.GatherSeconds * 1.6f / Speed(w.Tool == "" ? null : w.Tool)) {
                w.Timer = 0; target.Remaining -= w.Bag.Add(item, target.Remaining);
                if (target.IsLoose && target.Remaining <= 0) S.Things.Remove(target);
            }
            return true;
        }
        coworkers.Clear();
        foreach (var other in S.Workers) if (other.WageDebt <= 0 && HasDuty(other, target.Id)) coworkers.Add(other);
        coworkers.Sort(ById);
        if (target.Kind == "mine") return MineWork(w, target, coworkers, dt);
        if (target.Kind == "farm") return FarmWork(w, target, dt);
        if (target.Kind == "toolstation") return StockTools(w, target, dt);
        if (target.Kind == "pile") return TendCompost(w, target, dt);
        bool haul = HasDuty(w, target.Id, "haul");
        // Only one worker ever pumps: the first on the bellows. With others hauling, or no hauling of their own, they only pump.
        var pumper = Pumper(target);
        if (pumper == w && (!haul || HaulerBesides(target, w))) {
            w.Status = "Operating bellows"; if (Walk(w, target.Pos + BellowsStand, dt, .1f)) { w.WorkState = WorkerWorkState.Pumping; target.Heat = Math.Min(100, target.Heat + dt * 18); }
            return true;
        }
        if (target.Kind == "signpost") return ManageDeliveries(w, target, dt);
        if (target.Kind == "campfire") {
            if (target.Burn > 120 || target.Inventory["wood"] > 0) { w.Status = "Tending fire"; Walk(w, target.Pos, dt); return false; }
            var source = SourceFor("wood", w.Pos);
            if (source == null) { w.Status = "Needs wood in output-enabled storage"; return false; }
            if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, "wood", 3, true); w.Destination = target.Id; }
            w.Status = "Fetching firewood"; return true;
        }
        if (!Data.Recipes.ContainsKey(target.Recipe)) { w.Status = "Choose a building recipe"; return false; }
        if (haul) {
            if (FirstLeftover(target) is string leftover) {
                var dest = DepositFor(leftover, target.Pos);
                if (dest == null) { w.Status = "Output blocked · needs accepting storage"; return false; }
                w.Status = "Collecting finished goods";
                if (Walk(w, target.Pos, dt)) { target.Inventory.TransferTo(w.Bag, leftover, w.Bag.StackSize, true); w.Destination = dest.Id; }
                return true;
            }
            var costs = BatchCost(target);
            // A worked station only works while worked, so its crafter stays put until the batch is done before fetching the next one.
            bool fetches = !Worked(target) || !target.Processing || !HasDuty(w, target.Id, "craft");
            if (fetches) foreach (var pair in costs) {
                if (target.Inventory[pair.Key] >= pair.Value * 2) continue;
                var source = SourceFor(pair.Key, w.Pos);
                if (source == null) continue;
                w.Status = Phrase("Fetching ", Data.Name(pair.Key));
                if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, pair.Key, pair.Value * 2 - target.Inventory[pair.Key], true); w.Destination = target.Id; }
                return true;
            }
            // Then two batches' worth of fuel for the fuel slot.
            if (fetches && FuelOf(target) is string fuel && target.Burner![fuel] < 2 && FuelBlock(target, fuel) == null && SourceFor(fuel, w.Pos) is Thing fuelSource) {
                w.Status = Phrase("Fetching ", Data.Name(fuel), " for the fire");
                if (Walk(w, fuelSource.Pos, dt)) { fuelSource.Inventory.TransferTo(w.Bag, fuel, 2 - target.Burner[fuel], true); w.Destination = target.Id; w.Fueling = true; }
                return true;
            }
        }
        // A lone bellows worker who also hauls pumps between loads.
        bool pumps = pumper == w;
        bool crafts = Worked(target) && HasDuty(w, target.Id, "craft");
        string? missing = target.Tools != null && Data.Recipes[target.Recipe] is var needed && needed.Tool != "" && BenchTool(target, needed.Tool, needed.Tier) == null ? Data.ToolName(needed.Tool, needed.Tier) : null;
        w.Status = pumps ? "Pumping bellows between loads" : target.Kind == "reinforced" ? "Waiting for the next load" : missing != null ? Phrase("Needs ", A(missing), " in the ", Lower(Data.Name(target.Kind))) : crafts ? "Working at station" : "Waiting for the next load";
        if (!pumps && !crafts) return false;
        if (Walk(w, target.Pos, dt)) {
            if (pumps) { w.WorkState = WorkerWorkState.Pumping; target.Heat = Math.Min(100, target.Heat + dt * 16); } else w.WorkState = WorkerWorkState.Hammering;
        }
        return pumps || target.Processing || Stocked(target);
    }
    /// <summary>The worker who pumps a reinforced furnace's bellows: the first on duty with the bellows job, or null.</summary>
    private Worker? Pumper(Thing t) {
        if (t.Kind != "reinforced") return null;
        // Only a worker at the furnace now can pump; one away on another job leaves it to the next.
        foreach (var o in coworkers) if (o.Assignment == t.Id && HasDuty(o, t.Id, "bellows")) return o;
        return null;
    }
    private bool HaulerBesides(Thing t, Worker w) {
        foreach (var o in coworkers) if (o != w && HasDuty(o, t.Id, "haul")) return true;
        return false;
    }
    /// <summary>
    /// A post's runner fetches prices on request and carries what the cart delivered, other than coins, to storage.
    /// </summary>
    private bool ManageDeliveries(Worker w, Thing sign, float dt) {
        if (sign.Output != null)
            foreach (var pair in sign.Output.Items) {
                if (pair.Key == Inventory.Coins || DepositFor(pair.Key, sign.Pos) is not Thing dest) continue;
                w.Status = Phrase("Carrying in ", Data.Name(pair.Key));
                if (Walk(w, sign.Pos + Vector2.One, dt)) { w.Hands(Math.Max(Worker.BagStack, sign.Output.StackOf(pair.Key))); sign.Output.TransferTo(w.Bag, pair.Key, w.Bag.StackSize); w.Destination = dest.Id; }
                return true;
            }
        w.Status = "Minding the trading post"; Walk(w, sign.Pos + Vector2.One, dt); return false;
    }
    /// <summary>A rack's stocker brings tools it takes from storage until none are left there or the rack is full.</summary>
    private bool StockTools(Worker w, Thing rack, float dt) {
        foreach (var t in S.Things) {
            if (t.Kind is not ("chest" or "pile")) continue;
            foreach (var pair in t.Inventory.Items) {
                if (!Data.Items.TryGetValue(pair.Key, out var def) || def.Tool == "" || !t.Inventory.AllowsWithdrawal(pair.Key) || !rack.Inventory.Accepts(pair.Key) || rack.Inventory.Room(pair.Key) == 0) continue;
                w.Status = Phrase("Fetching ", A(Lower(def.Name)), " for the rack");
                if (Walk(w, t.Pos, dt)) { t.Inventory.TransferTo(w.Bag, pair.Key, 1, true); w.Destination = rack.Id; }
                return true;
            }
        }
        w.Status = "Rack stocked"; return false;
    }
    /// <summary>A compost's tender carries its fertilizer to storage and brings what it takes from storage, food only when it allows food deliveries.</summary>
    private bool TendCompost(Worker w, Thing pile, float dt) {
        if (!pile.Compost) { w.Status = "Nothing to tend"; return false; }
        if (pile.Inventory["fertilizer"] > 0 && pile.Inventory.AllowsWithdrawal("fertilizer") && DepositFor("fertilizer", pile.Pos, pile.Id) is Thing store) {
            w.Status = "Carrying out fertilizer";
            if (Walk(w, pile.Pos, dt)) { pile.Inventory.TransferTo(w.Bag, "fertilizer", w.Bag.StackSize, true); w.Destination = store.Id; }
            return true;
        }
        foreach (var t in S.Things) {
            if (t == pile || t.Kind is not ("chest" or "pile") || t.Compost) continue;
            foreach (var pair in t.Inventory.Items) {
                if (!Compostable(pair.Key) || pile.SkipFood && Data.IsFood(pair.Key) || !t.Inventory.AllowsWithdrawal(pair.Key) || !pile.Inventory.Accepts(pair.Key) || pile.Inventory.Room(pair.Key) == 0) continue;
                w.Status = Phrase("Fetching ", Data.Name(pair.Key), " for the compost");
                if (Walk(w, t.Pos, dt)) { t.Inventory.TransferTo(w.Bag, pair.Key, w.Bag.StackSize, true); w.Destination = pile.Id; }
                return true;
            }
        }
        w.Status = "Compost tended"; return false;
    }
    /// <summary>Whether the worker has a job at a place, or the given job there.</summary>
    public static bool HasDuty(Worker w, int place, string job = "") {
        foreach (var d in w.Duties) if (d.Thing == place && (job == "" || d.Job == job)) return true;
        return false;
    }
    /// <summary>Moves a worker with jobs at several places on to the next one, in the order they were given.</summary>
    private static void NextPlace(Worker w) {
        if (w.Duties.Count < 2) return;
        int at = -1;
        for (int i = 0; i < w.Duties.Count; i++) if (w.Duties[i].Thing == w.Assignment) at = i;
        for (int step = 1; step <= w.Duties.Count; step++) {
            var next = w.Duties[(at + step + w.Duties.Count) % w.Duties.Count];
            if (next.Thing == w.Assignment) continue;
            w.Assignment = next.Thing; w.Timer = 0; return;
        }
    }
    /// <summary>Drops jobs at places that are gone, and points the worker at a place they still have a job at.</summary>
    private void TidyDuties(Worker w) {
        for (int i = w.Duties.Count - 1; i >= 0; i--) if (Find(w.Duties[i].Thing) == null) w.Duties.RemoveAt(i);
        if (w.Duties.Count == 0) { w.Assignment = 0; return; }
        if (!HasDuty(w, w.Assignment)) w.Assignment = w.Duties[0].Thing;
    }
    /// <summary>Walks a tool the job no longer needs to the nearest tool station with room, and reports when it is put back; with no such station the worker keeps it.</summary>
    private bool ReturnTool(Worker w, float dt) {
        Thing? station = null; float nearest = 0;
        foreach (var t in S.Things)
            if (t.Kind == "toolstation" && t.Inventory.Room(w.Tool) > 0 && t.Inventory.Accepts(w.Tool) && (station == null || t.Pos.DistanceSquaredTo(w.Pos) < nearest)) { station = t; nearest = t.Pos.DistanceSquaredTo(w.Pos); }
        if (station == null) return true;
        w.Status = Phrase("Putting back the ", Lower(Data.Name(w.Tool)));
        if (!Walk(w, station.Pos, dt)) return false;
        station.Inventory.Add(w.Tool, 1); w.Tool = ""; return true;
    }
    /// <summary>Takes the best tool of a kind from the nearest tool station holding one; without one, the worker says once what they need and waits.</summary>
    private void FetchTool(Worker w, string kind, Thing target, float dt) {
        Thing? station = null; float nearest = 0;
        foreach (var t in S.Things)
            if (t.Kind == "toolstation" && BestTool(kind, t.Inventory) is string held && t.Inventory.AllowsWithdrawal(held) && (station == null || t.Pos.DistanceSquaredTo(w.Pos) < nearest)) { station = t; nearest = t.Pos.DistanceSquaredTo(w.Pos); }
        if (station == null) {
            w.Status = Phrase("Needs ", A(kind), " to ", Task(target));
            if (w.Missing != kind) { w.Missing = kind; Say($"{w.Name} needs {A(kind)} to {Task(target)}. Put one in a tool station."); }
            return;
        }
        w.Missing = "";
        w.Status = Phrase("Fetching ", A(kind), " from the tool station");
        if (!Walk(w, station.Pos, dt)) return;
        w.Tool = BestTool(kind, station.Inventory)!; station.Inventory.Remove(w.Tool, 1);
    }
    /// <summary>Walks to an evenly spaced seat around the lunch fire and sits; without a campfire the worker sits where they are.</summary>
    private void EatLunch(Worker w, float dt) {
        w.Status = "Lunch break";
        if (LunchFire is not Thing fire) { w.WorkState = WorkerWorkState.Resting; return; }
        int seat = -1, seated = 0;
        foreach (var o in S.Workers) if (!o.Home && o.Away <= 0) { if (o == w) seat = seated; seated++; }
        float angle = seat * Mathf.Tau / seated;
        w.Status = "Lunch break by the campfire";
        if (Walk(w, fire.Pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.4f, dt, .1f)) w.WorkState = WorkerWorkState.Resting;
    }
    public void PayWages() {
        foreach (var w in S.Workers) {
            // A striking worker does not work, so no further wages are added until the strike is settled.
            if (w.WageDebt > 0) continue;
            if (!PayWage(w.Wage)) { float due = w.WageDebt = w.Wage; Say($"{w.Name} is on strike: {due:0} coins owed. Pay or dismiss them at the trading post."); }
        }
    }
    public void SettleDebt(Worker w) {
        if (Forwarded("settle", other: w.Id)) return;
        if (!PayWage((int)w.WageDebt)) { Say("Not enough coins in the wage slot or your backpack to settle wages."); return; }
        w.WageDebt = 0;
    }
    /// <summary>The wolves hunting the acting player.</summary>
    private IEnumerable<Wolf> Hunters => S.Wolves.Where(w => w.Target == S.Actor.Id);
    // Reused copies of lists the tick walks while it may change them, so ticking allocates nothing.
    private readonly List<Thing> ticking = new();
    private readonly List<PlayerState> acting = new();
    private readonly List<Worker> crew = new(), coworkers = new();
    private readonly List<Wolf> hunting = new();
    private readonly List<TradeOrder> arriving = new();
    private readonly List<string> demanded = new();
    /// <summary>
    /// Advances the world. It runs as the world's own player, whoever's action started it, such as a guest's sleep:
    /// wages come out of that player's backpack, and each player's own part runs as that player.
    /// </summary>
    public void Tick(float dt, bool sleeping = false) { using (ActAs(S.Players[0])) TickWorld(dt, sleeping); }
    private void TickWorld(float dt, bool sleeping) {
        bool wasDusk = Dusk, wasLunch = Lunch;
        everyone = true;
        S.Time += dt;
        if (!sleeping) S.PlaySeconds += dt;
        foreach (var wolf in S.Wolves) if (PlayerById(wolf.Target) == null) wolf.Target = NearestPlayer(wolf.Pos).Id;
        acting.Clear(); acting.AddRange(S.Players);
        foreach (var p in acting) using (ActAs(p)) NeedsTick(dt);
        if (Day > S.WageDay) {
            // Workers on strike are not charged, so their wage does not count against the day.
            int wages = S.Workers.Where(w => w.WageDebt <= 0).Sum(w => w.Wage);
            PayWages(); CloseDay(wages); S.WageDay = Day; Say($"Day {Day}. Wages settled where funds allow.");
            foreach (var leaves in S.Things.Where(t => t.Kind == "leaves")) leaves.Remaining = LeafPatch;
        }
        if (S.Workers.Count > 0 && Dusk && !wasDusk) Say("Dusk. Your workers head home to Bramblewick until morning.");
        if (S.Workers.Count > 0 && Lunch && !wasLunch) Say("Midday. Your workers gather by the campfire for an hour of lunch.");
        // A day restores what selling the daily amount takes, so selling it every day keeps the price steady.
        demanded.Clear(); demanded.AddRange(S.Demand.Keys);
        foreach (var item in demanded) {
            if (!Data.Market.Sells.TryGetValue(item, out var demand)) continue;
            S.Demand[item] = Mathf.MoveToward(S.Demand[item], FullDemand(item), dt * demand.Daily * DemandStep(item) / Data.DaySeconds);
        }
        arriving.Clear(); arriving.AddRange(S.Orders);
        foreach (var order in arriving) {
            order.Elapsed += dt; if (order.Elapsed < Data.TradeSeconds) continue;
            // The cart unloads into the market output; whatever does not fit waits in a box beside the signpost.
            var sign = Find(order.Signpost);
            var output = sign?.Output ?? S.Backpack;
            var goods = order.Buying ? order.Goods : new() { [Inventory.Coins] = (int)order.Value - (sign?.Wages?.Add(Inventory.Coins, (int)order.Value) ?? 0) };
            // Boxed goods come back as packed crates of one purchase each.
            if (order.Buying && order.Crates > 0)
                goods = order.Goods.SelectMany(p => Enumerable.Range(0, order.Crates).Select(_ => { var box = NewCrate(); box.Add(p.Key, p.Value / order.Crates); return Pack(box); })).ToDictionary(id => id, _ => 1);
            foreach (var (item, quantity) in goods) {
                int overflow = quantity - output.Add(item, quantity);
                if (overflow <= 0) continue;
                var delivery = Add("grave", sign?.Pos + Vector2.One ?? Terrain.Spawn);
                delivery.Inventory.Slots = overflow / delivery.Inventory.StackOf(item) + 1;
                delivery.Inventory.Add(item, overflow);
            }
            if (!order.Buying) S.Milestones.Add("sold");
            if (!order.Buying && order.Goods.Keys.Any(id => id.StartsWith("steel_") && Data.Items[id].Tool != "")) S.Milestones.Add("sold_steel");
            if (order.Buying && order.Goods.Keys.Any(id => Data.Items[id].Tool == "pickaxe")) S.Milestones.Add("pick");
            // Played at the player, not the signpost, so a payment is heard wherever the player is.
            if (!order.Buying) { Play("payment_from_village_arrives", Player); S.DayRevenue += (int)order.Value; }
            S.Orders.Remove(order); UpdatePrices();
            Say(order.Buying ? $"Cart returned with {Goods(order.Goods)}." : $"Cart returned with {(int)order.Value} coins.");
        }
        S.Things.RemoveAll(t => t.Kind == "grave" && t.Inventory.Count == 0 || t.Kind == "wolf_corpse" && t.Remaining <= 0);
        // A reused copy, since ticking can add and remove things.
        ticking.Clear(); ticking.AddRange(S.Things);
        foreach (var t in ticking) {
            // A carcass keeps its age so the death animation plays once and not again after loading.
            if (t.Kind == "wolf_corpse") t.Progress = Math.Min(60, t.Progress + dt);
            if (t.Kind == "mine") MineTick(t, dt);
            if (t.Kind == "pile") { PileFilters(t); if (t.Compost) CompostTick(t, dt); }
            if (t.Kind == "farm") FarmTick(t, dt);
            if (t.Kind == "berries" && t.Remaining == 0) { t.Regrow += dt; if (t.Regrow >= Data.BerryRegrowSeconds) { t.Remaining = 24; t.Regrow = 0; } }
            if (t.Kind == "campfire") {
                t.Burn = Math.Max(0, t.Burn - dt);
                if (t.Burn < 30 && t.Inventory.Remove("wood", 1) > 0) { if (t.Burn <= 0) Play("start_fire", t.Pos); t.Burn += Data.CampfireSeconds; }
            }
            if (t.Kind == "reinforced") {
                // Once a working crew has the forge hot, they keep it at or above the clear bar between strokes.
                // A crew that is striking or stuck on a blocked path is not working, so the forge cools.
                // So is a crew that has signed out or is at lunch.
                t.Heat = Math.Max(Crewed(t) && t.Heat >= SteelHeat ? SteelHeat : 0, t.Heat - dt * 3.2f);
            }
            if (!Data.Recipes.TryGetValue(t.Recipe, out var recipe)) continue;
            bool wasBurning = stillBurning.Remove(t.Id);
            bool hot = t.Kind != "reinforced" || t.Heat >= SteelHeat;
            bool atAnvil = !Worked(t) || Hammered(t);
            // A recipe at a station with tool slots needs its tool slotted, and the tool's speed sets the pace.
            string? tool = t.Tools != null && recipe.Tool != "" ? BenchTool(t, recipe.Tool, recipe.Tier) : null;
            if (t.Tools != null && recipe.Tool != "" && tool == null) atAnvil = false;
            float rate = (t.Kind == "bench" ? Data.BenchSpeed : 1) * Speed(tool);
            if (!t.Processing && hot && atAnvil && Stocked(t)) {
                t.Inventory.Spend(BatchCost(t)); t.Processing = true; t.Progress = 0;
                if (recipe.Inputs.TryGetValue("water", out int usedWater)) t.Inventory.Add("empty_bottle", usedWater);
                if (FuelOf(t) is string fuel) t.Burner!.Remove(fuel, 1);
                if (recipe.Fuel && !wasBurning) Play("start_fire", t.Pos);
            }
            if (t.Processing && !hot) {
                // A cold forge mid-batch keeps burning coal from its slot at the batch fuel rate, but the steel waits for heat.
                // Burn is the campfire's fuel clock, reused here to time the next coal.
                t.Burn = Math.Min(recipe.Seconds, t.Burn + dt);
                if (t.Burn >= recipe.Seconds && t.Burner?.Remove("coal", 1) > 0) t.Burn = 0;
            }
            if (t.Processing && hot && atAnvil) {
                t.Progress = Math.Min(recipe.Seconds, t.Progress + dt * rate);
                if (t.Progress >= recipe.Seconds) {
                    // Manual loading or another hauler can fill the station while a batch cooks.
                    // Retain the completed batch until the one-stack output slot has room rather than discarding output.
                    if (Math.Min(t.Inventory.Room(recipe.Output), t.Inventory.StackSize - t.Inventory[recipe.Output]) < recipe.Amount) continue;
                    t.Inventory.Add(recipe.Output, recipe.Amount); t.Processing = false; t.Progress = 0; stillBurning.Add(t.Id);
                    Crafted(recipe.Output);
                }
            }
        }
        acting.Clear(); acting.AddRange(S.Players);
        foreach (var p in acting) using (ActAs(p)) CraftTick(dt);
        UpdateLunchFire();
        crew.Clear(); crew.AddRange(S.Workers);
        foreach (var w in crew) WorkerTick(w, dt);
        for (int i = 0; i < S.Workers.Count; i++) for (int j = i + 1; j < S.Workers.Count; j++) {
            var a = S.Workers[i]; var b = S.Workers[j]; var offset = a.Pos - b.Pos;
            // Miners underground are inside the hill, so they never crowd anyone at the entrance.
            if (!a.Home && !b.Home && a.WorkState != WorkerWorkState.Underground && b.WorkState != WorkerWorkState.Underground && offset.LengthSquared() < .64f) {
                var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
                if (CanStand(a.Pos + push)) a.Pos += push;
                if (CanStand(b.Pos - push)) b.Pos -= push;
            }
        }
        if (!Night) S.Wolves.Clear();
        acting.Clear(); acting.AddRange(S.Players);
        foreach (var p in acting)
            using (ActAs(p)) {
                if (!Night) {
                    nightSpawn = 0;
                    if (!Dead) S.Mauled = false;
                } else if (!sleeping) {
                    hunting.Clear();
                    foreach (var w in S.Wolves) if (w.Target == p.Id) hunting.Add(w);
                    TickWolves(dt, hunting);
                }
                if (!Dead && S.Health <= 0) Die();
                playerBusy = false;
            }
        everyone = false;
        Look();
        UpdateGoal();
    }
    private PlayerState? PlayerById(int id) {
        foreach (var p in S.Players) if (p.Id == id) return p;
        return null;
    }
    private PlayerState NearestPlayer(Vector2 at) {
        PlayerState nearest = S.Players[0];
        foreach (var p in S.Players) if (p.Pos.DistanceSquaredTo(at) < nearest.Pos.DistanceSquaredTo(at)) nearest = p;
        return nearest;
    }
    private static readonly Comparison<Worker> ById = (a, b) => a.Id.CompareTo(b.Id);
    private bool Crewed(Thing t) {
        foreach (var w in S.Workers) if (w.Assignment == t.Id && HasDuty(w, t.Id, "bellows") && w.WageDebt <= 0 && w.WorkState != WorkerWorkState.Blocked && OnDuty(w)) return true;
        return false;
    }
    /// <summary>The acting player's clocks, hunger and thirst, and the torch in hand; a remote player's held gather also runs here.</summary>
    private void NeedsTick(float dt) {
        attackClock = Math.Max(0, attackClock - dt);
        if (S.Actor != Me && Find(S.Actor.GatherTarget) is { IsLoose: false } held) Gather(held, dt);
        if (Dead) { S.RespawnIn = Math.Max(0, S.RespawnIn - dt); if (!Dead) Respawn(); } else {
            float drain = S.Actor.Sprinting ? Data.SprintDrain : 1;
            S.Hunger = Math.Max(0, S.Hunger - dt * drain * Data.HungerPerSecond); S.Thirst = Math.Max(0, S.Thirst - dt * drain * Data.ThirstPerSecond);
            if (S.Hunger <= 0 || S.Thirst <= 0) S.Health -= dt * 1.4f;
            else if (S.Hunger > 45 && S.Thirst > 45) S.Health = Math.Min(100, S.Health + dt * .12f);
        }
        TorchFlare = Math.Max(0, TorchFlare - dt);
        if (S.TorchKindling > 0 && (S.TorchKindling -= dt) <= 0) {
            S.TorchKindling = 0;
            if (!Downed && S.Torch > 0) { S.TorchLit = true; Play("start_fire", Player); Say($"Torch lit: {S.Torch / Data.DaySeconds * 24:0.#} hours of light left."); }
        }
        if (S.TorchLit && (S.Torch -= dt) <= 0) { S.Torch = 0; PutOutTorch(); Say("Your torch burned out."); }
    }
    /// <summary>The campfire the crew sits around: the one closest to them all, during lunch.</summary>
    internal void UpdateLunchFire() {
        LunchFire = null;
        if (!Lunch) return;
        float best = float.MaxValue;
        foreach (var t in S.Things) {
            if (t.Kind != "campfire") continue;
            float sum = 0; bool anyone = false;
            foreach (var w in S.Workers) if (!w.Home && w.Away <= 0) { sum += w.Pos.DistanceTo(t.Pos); anyone = true; }
            if (!anyone) return;
            if (sum < best) { best = sum; LunchFire = t; }
        }
    }
    /// <summary>Explores the tiles in every player's sight and replaces the memory of everything there with its current state.</summary>
    public void Look() { foreach (var p in S.Players) using (ActAs(p)) LookAround(); }
    private readonly List<int> forgotten = new();
    private void LookAround() {
        float radius = Data.SightRadius;
        int fromX = Math.Max(0, (int)(Player.X - radius)), toX = Math.Min(Terrain.Size - 1, (int)(Player.X + radius) + 1);
        int fromY = Math.Max(0, (int)(Player.Y - radius)), toY = Math.Min(Terrain.Size - 1, (int)(Player.Y + radius) + 1);
        for (int y = fromY; y <= toY; y++)
            for (int x = fromX; x <= toX; x++)
                if (S.Explored[y * Terrain.Size + x] == 0 && Sees(new Vector2(x, y))) { S.Explored[y * Terrain.Size + x] = 255; ExploredRevision++; }
        forgotten.Clear();
        foreach (var pair in S.Remembered) if (Sees(pair.Value.Pos) && Find(pair.Key) == null) forgotten.Add(pair.Key);
        foreach (var id in forgotten) S.Remembered.Remove(id);
        // Only the look is remembered: no inventory, and no fire, heat or work, so nothing out of sight smokes or glows.
        foreach (var t in S.Things) {
            if (!Sees(t.Pos)) continue;
            if (!S.Remembered.TryGetValue(t.Id, out var memory)) S.Remembered[t.Id] = memory = new Thing { Id = t.Id };
            memory.Kind = t.Kind; memory.Pos = t.Pos; memory.Remaining = t.Remaining; memory.Recipe = t.Recipe; memory.Progress = t.Progress; memory.Stage = t.Stage;
        }
    }
    public bool Explored(Vector2 p) => p.X >= 0 && p.Y >= 0 && p.X < Terrain.Size && p.Y < Terrain.Size && S.Explored[(int)p.Y * Terrain.Size + (int)p.X] != 0;
    /// <summary>Moves the pack hunting the acting player.</summary>
    private void TickWolves(float dt, List<Wolf> hunters) {
        if (!Dead) {
            nightSpawn -= dt;
            // A torch draws the whole pack in, but slowly.
            if (nightSpawn <= 0 && hunters.Count < (S.TorchLit ? 6 : Math.Min(6, 2 + Day))) {
                nightSpawn = S.TorchLit ? 25 : 15; float angle = S.Time * 2.4f;
                // Wolves come out of the woods, never the sea or the mountains, and just beyond every player's sight, so none appears on screen.
                // Without such a spot this time, the wolf comes on a later try.
                var p = Player;
                for (int turn = 0; turn < 24 && p == Player; turn++) {
                    var at = Player + new Vector2(MathF.Cos(angle + turn * .52f), MathF.Sin(angle + turn * .52f)) * (Data.SightRadius + 2 + turn / 12 * 4);
                    if (Open(at) && Unseen(at)) p = at;
                }
                if (p != Player) {
                    var wolf = new Wolf { Pos = p, Health = 4 + Day * .4f, Target = S.Actor.Id }; S.Wolves.Add(wolf); hunters.Add(wolf);
                    Play("wolf_growl_distance", p);
                } else nightSpawn = 1;
            }
        }
        bool lit = Protected(Player), inside = S.Sheltered != 0;
        // Behind a torch a lone wolf creeps in until a swing scares it off; once the pack is complete they attack together.
        bool torch = Torchlit, pack = torch && PackAround >= PackSize, wary = torch && !pack;
        if (pack && !packAttack) Say("The pack has you surrounded. They attack!");
        packAttack = pack;
        // A wounded player away from fire and shelter invites a leap for the throat.
        if (!Downed && S.Health <= 50 && !lit && !inside && !wary && !AnyLeaping(hunters)) {
            Wolf? hunter = null;
            foreach (var w in hunters)
                if (!Protected(w.Pos) && w.Flee <= 0 && w.Stagger <= 0 && w.Pos.DistanceTo(Player) < LeapRange && (hunter == null || w.Pos.DistanceTo(Player) < hunter.Pos.DistanceTo(Player))) hunter = w;
            if (hunter != null) { hunter.Leap = LeapSeconds; Say("A wolf leaps for your throat!"); }
        }
        // Stalking and backing off speed up a little each day; a chase always outpaces a walk but not a sprint.
        float speed = Math.Min(3.8f, 3 + Day * .1f), chase = Data.WalkSpeed * Data.WolfChase;
        foreach (var wolf in hunters) {
            wolf.Attack -= dt;
            if (wolf.Leap > 0) {
                // Closing the remaining gap evenly makes every leap last LeapSeconds.
                var landing = Player + (wolf.Pos - Player).Normalized() * PinDistance;
                wolf.Pos = wolf.Pos.MoveToward(landing, wolf.Pos.DistanceTo(landing) * Math.Min(1, dt / wolf.Leap));
                wolf.Leap = Math.Max(0, wolf.Leap - dt);
                if (wolf.Leap == 0) { wolf.Feeding = wolf.Pinning = true; S.Mauled = true; Say(hunters.Count > 1 ? "You are pinned. The pack closes in." : "You are pinned."); Play("wolf_bite", Player); }
            } else if (S.Mauled) {
                if (wolf.Feeding) continue;
                var seat = Player + (wolf.Pos - Player).Normalized() * FeastSeat;
                wolf.Pos = wolf.Pos.MoveToward(seat, dt * FeastSpeed);
                wolf.Feeding = wolf.Pos.DistanceTo(seat) < .05f;
            } else if (wolf.Stagger > 0) {
                wolf.Stagger = Math.Max(0, wolf.Stagger - dt); wolf.Chase = 0;
            } else if (wolf.Flee > 0) {
                wolf.Flee -= dt; wolf.Chase = 0;
                wolf.Pos = wolf.Pos.MoveToward(wolf.Pos + AwayFromPlayer(wolf) * 3, dt * speed);
            } else if (wary && wolf.Scared) {
                // Each scared wolf keeps its bearing at the edge of the torchlight, so a growing pack surrounds the player.
                wolf.Chase = 0;
                var away = AwayFromPlayer(wolf);
                wolf.Pos = wolf.Pos.MoveToward(Player + away * StalkDistance, dt * speed * .6f);
                if (!wolf.Growled && wolf.Pos.DistanceTo(Player) < 6) { wolf.Growled = true; Play("wolf_growl_approaching", wolf.Pos); }
            } else if (wary) {
                wolf.Chase = 0;
                wolf.Pos = wolf.Pos.MoveToward(Player, dt * speed * .35f);
                if (!wolf.Growled && wolf.Pos.DistanceTo(Player) < 6) { wolf.Growled = true; Play("wolf_growl_approaching", wolf.Pos); }
                if (wolf.Pos.DistanceTo(Player) < 1.1f && wolf.Attack <= 0) { wolf.Attack = 1; S.Health -= 12 + Day; Play("wolf_bite", Player); }
            }
              // A wolf already on the player's heels follows briefly into firelight, but backs off from a cabin at once.
              else if (inside || (lit || Protected(wolf.Pos)) && wolf.Chase <= 0) { wolf.Chase = 0; wolf.Pos = wolf.Pos.MoveToward(wolf.Pos + AwayFromPlayer(wolf) * 3, dt * 3); } else {
                wolf.Chase = lit || Protected(wolf.Pos) ? wolf.Chase - dt : ChaseSeconds;
                wolf.Pos = wolf.Pos.MoveToward(Player, dt * chase);
                if (!wolf.Growled && wolf.Pos.DistanceTo(Player) < 6) { wolf.Growled = true; Play("wolf_growl_approaching", wolf.Pos); }
                if (wolf.Pos.DistanceTo(Player) < 1.1f && wolf.Attack <= 0) { wolf.Attack = 1; S.Health -= 12 + Day; Play("wolf_bite", Player); }
            }
        }
        // A sheltered player's pack gives up once it has backed out of sight, instead of waiting at the cabin.
        if (inside)
            for (int i = hunters.Count - 1; i >= 0; i--)
                if (hunters[i].Pos.DistanceTo(Player) > Data.SightRadius) { S.Wolves.Remove(hunters[i]); hunters.RemoveAt(i); }
        // Feeding wolves crowd around the player without stacking on one spot.
        for (int i = 0; i < hunters.Count; i++) for (int j = i + 1; j < hunters.Count; j++) {
            var a = hunters[i]; var b = hunters[j]; var offset = a.Pos - b.Pos;
            if (a.Leap > 0 || b.Leap > 0 || offset.LengthSquared() >= .5f) continue;
            var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
            a.Pos += push; b.Pos -= push;
        }
        if (S.Mauled && !Dead) {
            int feeding = 0;
            foreach (var w in hunters) if (w.Feeding) feeding++;
            S.Health -= dt * (16 + 8 * feeding);
        }
    }
    /// <summary>The way a wolf backs off; one standing right on the player still has somewhere to go.</summary>
    private Vector2 AwayFromPlayer(Wolf wolf) => wolf.Pos == Player ? Vector2.Right : (wolf.Pos - Player).Normalized();
    private bool Unseen(Vector2 p) {
        foreach (var player in S.Players) if (player.Pos.DistanceTo(p) <= Data.SightRadius) return false;
        return true;
    }
    private static bool AnyLeaping(List<Wolf> wolves) {
        foreach (var w in wolves) if (w.Leap > 0) return true;
        return false;
    }
    private static readonly string[] Milestones = {
        "water", "hut", "campfire", "gather", "bench", "crate", "sold", "pick",
        "furnace", "iron_bar", "reinforced", "steel_bar", "anvil", "steel_tool", "sold_steel", "hired", "assigned"
    };
    public bool GoalComplete => S.Goal >= Milestones.Length;
    private void UpdateGoal() {
        while (!GoalComplete && S.Milestones.Contains(Milestones[S.Goal])) S.Goal++;
    }
    public static int GoalCount => Milestones.Length;
    public (string Title, string Hint) Objective => ObjectiveFor(S.Goal);
    public static (string Title, string Hint) ObjectiveFor(int goal) => goal switch {
        0 => ("Carry fresh water", "Collect water around the pond shore. It holds three sips and stays with you when empty; without a bottle, drink at the source."),
        1 => ("A safe place", "Gather wood and stone, then build a cabin before night. Step inside to shelter from wolves."),
        2 => ("Keep the fire lit", "Build and light a campfire. Firelight keeps wolves away; feed it wood to keep it burning."),
        3 => ("Gather by hand", "You start with a steel axe. Hold right mouse on trees to fell them; gather stone and berries by hand."),
        4 => ("A workbench", "Press C to craft. Knap a stone chisel from 2 stone, carve planks, then build a crafting bench: 10 planks."),
        5 => ("Something to carry", "Carve a wooden mallet by hand and slot it in the bench beside the chisel. Craft a crate there: 6 planks."),
        6 => ("The road to Bramblewick", "Build a trading post: 10 wood + 4 stone. Put berries in a crate and load it at the market. Sell, then collect the coins."),
        7 => ("Beneath the surface", "Buy your first steel pickaxe from the trading post price sheet. The cart delivers it to the trading post."),
        8 => ("Stone and embers", "Build a stone furnace: 16 stone + 6 wood. Smelt iron ore with wood or coal."),
        9 => ("The first iron", "Explore for a rust-streaked cliff and mine iron ore at its foot. Load the furnace with ore and fuel; collect iron bars."),
        10 => ("Hotter than fire", "Explore for a dark cliff and mine coal. Build a reinforced furnace: 20 stone, 5 iron bars, 8 wood."),
        11 => ("Make steel", "Load iron and mined coal into the reinforced furnace and coal into its fuel slot. Pump the bellows: steel requires 1450+ °C."),
        12 => ("A proper workshop", "Build an anvil: 4 iron bars + 6 stone. It hammers steel bars into steel parts."),
        13 => ("Tools for the village", "Hammer steel parts at the anvil, then craft a steel axe, pickaxe or hammer at the bench."),
        14 => ("Steel for sale", "Pack steel tools into a crate and sell it at the trading post, then collect the coins."),
        15 => ("Many hands", "At the trading post pick a hiring offer: more coins up front means a lower daily wage. Keep coins in the trading post wage slot for each dawn."),
        16 => ("Your first automation", "Build storage and a tool station with a spare axe or pickaxe. Assign a worker to Gather from their panel or use a building's job buttons."),
        _ => ("A settlement of your own", "The starter goals are done. Work toward the end goals: a working mine on every ore and a week of steady profit.")
    };
    // ---------- End goals ----------
    /// <summary>Ores found in the valley's cliffs; the end goal wants a working mine on each.</summary>
    public static readonly string[] MineOres = Cliffs.Select(c => c.Ore).Distinct().ToArray();
    public bool HasMine(string ore) {
        foreach (var t in S.Things) if (t.Kind == "mine" && t.Stage == MineOpen && CliffAt(t.Pos).Ore == ore) return true;
        return false;
    }
    /// <summary>Finished days in a row, up to today, whose profit reached the goal.</summary>
    public int ProfitStreak => S.Profits.AsEnumerable().Reverse().TakeWhile(p => p >= Data.Goals.DailyProfit).Count();
    /// <summary>Once reached, the week of profit stays reached even if later days fall short.</summary>
    public bool ProfitGoalMet => S.Milestones.Contains("profit_week");
    // The milestone comes first: it is a set lookup, and the game checks this every frame.
    public bool EndGoalsComplete {
        get {
            if (!ProfitGoalMet) return false;
            foreach (string ore in MineOres) if (!HasMine(ore)) return false;
            return true;
        }
    }
    private void CloseDay(int wages) {
        S.Profits.Add(S.DayRevenue - wages); S.DayRevenue = 0;
        if (ProfitStreak >= Data.Goals.ProfitDays) S.Milestones.Add("profit_week");
    }
    public string Serialize() => JsonSerializer.Serialize(S, GameData.Json);
    // ---------- Validation of saves and guests ----------
    /// <summary>Whether an id names a known item, or a packed crate whose contents are in <paramref name="crates"/>.</summary>
    private bool KnownItem(string item, Dictionary<int, Inventory> crates) => item.StartsWith(PackedPrefix) ? crates.ContainsKey(PackedId(item)) : Data.Items.ContainsKey(item);
    /// <summary>Whether every entry is a positive count of a known item or of a packed crate in <paramref name="crates"/>.</summary>
    private bool ValidInventory(Inventory? box, Dictionary<int, Inventory> crates) {
        if (box?.Items == null) return false;
        foreach (var (item, count) in box.Items) if (count <= 0 || !KnownItem(item, crates)) return false;
        return true;
    }
    /// <summary>Whether each packed crate holds positive counts of known items, no other crates, and no more than a crate fits.</summary>
    private bool ValidCrates(Dictionary<int, Inventory> crates) {
        foreach (var box in crates.Values) {
            if (box?.Items == null || box.Count > Data.CrateCapacity) return false;
            foreach (var (item, count) in box.Items) if (count <= 0 || item.StartsWith(PackedPrefix) || !Data.Items.ContainsKey(item)) return false;
        }
        return true;
    }
    /// <summary>
    /// Whether a player from a save or a joining guest carries only known items and has needs, torch and respawn timer within their limits.
    /// The crafting queue must hold only recipes made by hand.
    /// </summary>
    private bool ValidPlayer(PlayerState p, Dictionary<int, Inventory> crates) {
        if (p.HotbarSlots == null || p.Crafting == null || !ValidInventory(p.Backpack, crates)) return false;
        foreach (string id in p.Crafting) if (!Data.Recipes.TryGetValue(id, out var r) || !r.Hand) return false;
        // A player who left while dead comes back still waiting to respawn.
        return (p.Health is > 0 and <= 100 || p.Health <= 0 && p.RespawnIn > 0) && p.Hunger is >= 0 and <= 100 && p.Thirst is >= 0 and <= 100
            && p.Torch >= 0 && p.Torch <= Data.CampfireSeconds && p.RespawnIn >= 0 && p.RespawnIn <= Data.RespawnSeconds;
    }
    /// <summary>Whether every building, worker and cart in a save holds only known items, and every worker's tool is a known item.</summary>
    private bool ValidSettlement(SaveState save) {
        foreach (var t in save.Things) {
            if (t?.Inventory == null) return false;
            foreach (var box in Boxes(t)) if (box != null && !ValidInventory(box, save.Crates)) return false;
        }
        foreach (var w in save.Workers)
            if (w?.Duties == null || w.Tool == null || w.Tool != "" && !Data.Items.ContainsKey(w.Tool) || !ValidInventory(w.Bag, save.Crates)) return false;
        foreach (var order in save.Orders) {
            if (order?.Goods == null) return false;
            foreach (var (item, count) in order.Goods) if (count <= 0 || !KnownItem(item, save.Crates)) return false;
        }
        return true;
    }
    public void Deserialize(string json) {
        // Steel tools became steel parts; carried ones that were the only axe or pickaxe become those tools so the save stays playable.
        bool legacyTools = json.Contains("\"steel_tools\"");
        json = json.Replace("\"steel_tools\"", "\"steel_parts\"").Replace("\"cooked_meat\"", "\"burnt_steak\"");
        // Iron tools and parts left the game: older saves get the steel ones instead.
        json = json.Replace("\"iron_axe\"", "\"steel_axe\"").Replace("\"iron_pick\"", "\"steel_pick\"").Replace("\"iron_hammer\"", "\"steel_hammer\"").Replace("\"iron_parts\"", "\"steel_parts\"");
        var loaded = JsonSerializer.Deserialize<SaveState>(json, GameData.Json) ?? throw new InvalidOperationException("Invalid save.");
        if (loaded.Version < 4) throw new InvalidOperationException("This save is from an older, smaller map and cannot be loaded. Start a new game.");
        if (loaded.Version is not (4 or 5) || loaded.Things == null || loaded.Backpack == null || loaded.Workers == null || loaded.Orders == null || loaded.Wolves == null || loaded.Demand == null || loaded.Milestones == null) throw new InvalidOperationException("Unsupported or incomplete save.");
        // Edited or older saves may hold missing, extra or unknown hotbar entries.
        var slots = loaded.HotbarSlots ?? new();
        loaded.HotbarSlots = Enumerable.Range(0, 9).Select(i => slots.ElementAtOrDefault(i) is string id && (Data.Items.ContainsKey(id) || Data.Buildings.ContainsKey(id)) ? id : "").ToList();
        loaded.ActiveHotbar = Math.Clamp(loaded.ActiveHotbar, 0, 8);
        if (!loaded.Things.Any(t => t.Id == loaded.Sheltered && t.Kind == "hut")) loaded.Sheltered = 0;
        loaded.Prices ??= new();
        if (loaded.Coins is float coins) { loaded.Backpack.Add(Inventory.Coins, (int)coins); loaded.Coins = null; }
        foreach (var order in loaded.Orders.Where(o => o.Item != null && o.Goods.Count == 0)) { order.Goods[order.Item!] = order.Quantity; order.Item = null; order.Quantity = 0; }
        // Signposts from older saves had no market, so they get its boxes on load.
        foreach (var sign in loaded.Things.Where(t => t.Kind == "signpost" && t.Output == null)) { sign.Inventory.Slots = MarketSlots; sign.Output = new() { Slots = MarketSlots }; }
        foreach (var sign in loaded.Things.Where(t => t.Kind == "signpost" && t.Wages == null)) sign.Wages = NewWageSlot();
        // Saves from before the fog start unexplored.
        if (loaded.Explored?.Length != Terrain.Size * Terrain.Size) loaded.Explored = new byte[Terrain.Size * Terrain.Size];
        loaded.Remembered ??= new();
        if (loaded.Version == 4) AlignMines(loaded);
        // Loose iron left the game too.
        loaded.Things.RemoveAll(t => t.Kind == "loose_iron");
        foreach (var id in loaded.Remembered.Where(p => p.Value.Kind == "loose_iron").Select(p => p.Key).ToList()) loaded.Remembered.Remove(id);
        loaded.Crates ??= new(); loaded.Crafting ??= new(); loaded.Profits ??= new();
        loaded.Crafting.RemoveAll(id => !Data.Recipes.TryGetValue(id, out var r) || !r.Hand);
        foreach (var t in loaded.Things) {
            // Anvils of older saves may still be set to tools that are now made at the bench.
            if (t.Recipe != "" && (!Data.Recipes.TryGetValue(t.Recipe, out var r) || r.Station != t.Kind)) { t.Recipe = ""; t.Processing = false; t.Progress = 0; }
            if (SlotKinds(t.Kind).Length > 0) t.Tools ??= NewToolSlots(t.Kind);
            // Stations used to keep their fuel with the ingredients; fuel nothing smelts moves to the fuel slot.
            // Plots used to keep their harvest with the seed; the harvest moves to its own slots.
            if (t.Kind == "farm" && t.Output == null) {
                t.Output = new() { Slots = Data.Farm.Yield.Count };
                foreach (var item in t.Inventory.Items.Keys.Where(i => !Data.Farm.Sow.ContainsKey(i)).ToList()) t.Inventory.TransferTo(t.Output, item, t.Inventory[item]);
                t.Inventory.Slots = Data.Farm.Sow.Count;
            }
            if (Burns(t.Kind) && t.Burner == null) {
                t.Burner = NewBurner();
                string fuel = BurnedFuel(t);
                if (!BatchCost(t).ContainsKey(fuel)) t.Inventory.TransferTo(t.Burner, fuel, t.Inventory[fuel]);
            }
        }
        if (legacyTools) {
            foreach (var (tool, made) in new[] { ("axe", "steel_axe"), ("pickaxe", "steel_pick") })
                if (BestTool(tool, loaded.Backpack) == null && loaded.Backpack.Remove("steel_parts", 1) == 1) loaded.Backpack.Add(made, 1);
            if (loaded.Milestones.Contains("steel_parts")) loaded.Milestones.Add("steel_tool");
        }
        if (!ValidCrates(loaded.Crates) || !ValidPlayer(loaded.Actor, loaded.Crates) || !ValidSettlement(loaded)) throw new InvalidOperationException("This save holds unknown items or values out of range and cannot be loaded.");
        // The goal chain gained steps, so the current goal is worked out again from the milestones.
        loaded.Goal = 0;
        S = loaded;
        // Older saves set only a worker's place; it carries every job there.
        foreach (var w in S.Workers)
            if (w.Duties.Count == 0 && w.Assignment > 0 && Find(w.Assignment) is Thing place) foreach (string job in Jobs(place)) w.Duties.Add(new Duty { Thing = place.Id, Job = job });
        NormalizeBottleStorage();
        // Saves from before raking have no leaf litter.
        if (!S.Things.Any(t => t.Kind == "leaves")) DropLeaves();
        UpdateGoal(); gatherClock = 0; ExploredRevision++; Look(); Say("Settlement loaded.");
    }
}
