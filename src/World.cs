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
    /// <summary>A signpost's market output: bought goods arrive here with the returning cart, and sale coins once <see cref="Wages"/> is full.</summary>
    public Inventory? Output { get; set; }
    /// <summary>A signpost's one stack of coins for wages: sale coins land here first, and dawn wages come out of it before any backpack.</summary>
    public Inventory? Wages { get; set; }
    /// <summary>A crafting bench's chisel and hammer slots; automation never touches them.</summary>
    public Inventory? Tools { get; set; }
    /// <summary>Building stages finished, for buildings put up in stages: a mine counts up to <see cref="World.MineOpen"/>.</summary>
    public int Stage { get; set; }
    /// <summary>A pile turned into compost: it takes leaves, grass and food and slowly turns them into fertilizer.</summary>
    public bool Compost { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
    [JsonIgnore] public bool IsResource => IsLoose || IsDeposit || Kind is "tree" or "stone" or "berries" or "water" or "wolf_corpse" or "leaves";
    /// <summary>Ore at the foot of a cliff, mined with a pickaxe.</summary>
    [JsonIgnore] public bool IsDeposit => Kind is "iron" or "coal" or "tin";
    /// <summary>Loose pieces are picked up whole in one go and vanish, instead of being worked down like a deposit.</summary>
    [JsonIgnore] public bool IsLoose => Kind is "loose_iron" or "loose_coal";
    [JsonIgnore] public bool IsStorage => Kind is "chest" or "grave" or "crate" or "toolstation" or "pile";
}
/// <summary>An impassable rock face whose ore deposits sit at its foot, with loose pieces scattered toward the clearing.</summary>
public readonly record struct Cliff(string Ore, Vector2 Center, Vector2 Face, float HalfLength)
{
    public const float Depth = 1.4f;
    public Vector2 Along => new(-Face.Y, Face.X);
    /// <summary>Distance to the cliff's spine, so the whole wall blocks movement rather than only its middle.</summary>
    public float DistanceTo(Vector2 p) => p.DistanceTo(Center + Along * Math.Clamp((p - Center).Dot(Along), -HalfLength, HalfLength));
}
public enum WorkerWorkState { Idle, Gathering, Pumping, Hammering, Blocked, Resting, Digging, Underground }

public sealed class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public int Assignment { get; set; }
    public string Job { get; set; } = "idle";
    public string Status { get; set; } = "Available";
    /// <summary>Units of one stack a worker carries; a miner bringing out a full mine output carries the whole stack.</summary>
    public const int BagStack = 5;
    public Inventory Bag { get; set; } = new() { Slots = 1, StackSize = BagStack };
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
public sealed class TradeOrder
{
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
    /// <summary>The wolf has growled at the player while closing in; it growls only once.</summary>
    [JsonIgnore] public bool Growled { get; set; }
    /// <summary>Seconds a chasing wolf keeps following the player into firelight before it gives up.</summary>
    public float Chase { get; set; }
    /// <summary>A torch swing toward this wolf made it keep its distance; it lasts while the torch burns.</summary>
    public bool Scared { get; set; }
    /// <summary>Seconds left running away after a direct torch hit.</summary>
    public float Flee { get; set; }
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
public sealed class SaveState
{
    /// <summary>Everyone in the world; the first is the save's own player. Guests are saved on their own machines.</summary>
    [JsonIgnore] public List<PlayerState> Players { get; set; }
    [JsonIgnore] public PlayerState Actor { get; set; }
    public SaveState() { Players = new() { new PlayerState() }; Actor = Players[0]; }
    /// <summary>5 aligns mine entrances with their cliff faces; version 4 valley saves migrate on load.</summary>
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
    public byte[] Explored { get; set; } = new byte[World.MapSize * World.MapSize];
    /// <summary>Things as last seen, kept so the fog shows them unchanged until the player looks again.</summary>
    public Dictionary<int, Thing> Remembered { get; set; } = new();
}

/// <summary>Authoritative simulation, independent of rendering and UI; the host runs it for every player.</summary>
public sealed partial class World
{
    public readonly GameData Data;
    public SaveState S { get; private set; } = new();
    public Action<string>? Notify;
    public Action<ActorAction, Vector2>? PlayerActed;
    /// <summary>A named sound effect and where it happens; the simulation only names it.</summary>
    public Action<string, Vector2>? Sound;
    public const int MapSize = Terrain.Size;
    public static readonly Vector2 Spawn = Terrain.Spawn;
    /// <summary>How far down the valley a cart or errand runner walks before fading from view on the way to Bramblewick.</summary>
    public const float TripDistance = 8;
    /// <summary>How far out a cart or runner is, from 0 at the start through 1 at the halfway turn and back to 0.</summary>
    public static float TripLeg(float phase) => phase < .5f ? phase * 2 : (1 - phase) * 2;
    public const float LeapSeconds = .4f;
    // A wolf leaps from within LeapRange and lands PinDistance away, where its jaws reach the throat of the downed player.
    private bool packAttack { get => S.Actor.PackAttack; set => S.Actor.PackAttack = value; }
    private const float LeapRange = 3.5f, PinDistance = 1.45f, FeastSeat = 1.1f, FeastSpeed = 5.5f, ChaseSeconds = 3;
    /// <summary>Minimum bellows furnace temperature: steel only progresses at or above it.</summary>
    public const float SteelHeat = 80;
    // Rock faces at the foot of both ranges, facing into the valley; the first iron and coal cliffs are the nearer ones, with loose pieces below them.
    // Tin has no loose pieces: it waits for a pickaxe, further inland.
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
    /// <summary>Things as the player knows them: live within sight, as last seen elsewhere, and absent if never seen.</summary>
    public IEnumerable<Thing> Known => S.Things.Where(t => Sees(t.Pos)).Concat(S.Remembered.Values.Where(t => !Sees(t.Pos)));
    /// <summary>From inside a cabin only that cabin is within reach.</summary>
    public bool Near(Thing t, float radius = 3.2f) => !Downed && (S.Sheltered == 0 || S.Sheltered == t.Id) && Player.DistanceTo(t.Pos) <= radius;
    public int Coins => S.Backpack[Inventory.Coins];
    /// <summary>Spends carried coins; coins stored elsewhere do not count.</summary>
    public bool Pay(int amount) => amount <= Coins && S.Backpack.Remove(Inventory.Coins, amount) == amount;
    public Thing? Find(int id) => S.Things.FirstOrDefault(t => t.Id == id);
    /// <summary>Tells the acting player, or everyone while the tick runs the shared world.</summary>
    public void Say(string message)
    {
        if (everyone || S.Actor == Me) Notify?.Invoke(message);
        if (everyone || S.Actor != Me) Relay?.Invoke(everyone ? null : S.Actor, message);
    }
    // Sleep fast-forwards hours in one frame, so its events stay silent instead of playing all at once.
    private void Play(string sound, Vector2 at) { if (!asleep) Sound?.Invoke(sound, at); }
    public Thing Add(string kind, Vector2 pos, int remaining = 0)
    {
        var t = new Thing { Id = S.NextId++, Kind = kind, Pos = pos, Remaining = remaining };
        if (kind == "furnace") t.Recipe = "iron_bar";
        if (kind == "reinforced") { t.Recipe = "steel_bar"; t.Fuel = "coal"; }
        if (kind == "signpost") { t.Inventory.Slots = MarketSlots; t.Output = new() { Slots = MarketSlots }; t.Wages = NewWageSlot(); }
        if (SlotKinds(kind).Length > 0) t.Tools = NewToolSlots(kind);
        if (kind == "crate") t.Inventory = NewCrate();
        if (kind == "mine") { t.Inventory.Slots = Data.Mine.Materials.Values.Sum(n => (n + t.Inventory.StackSize - 1) / t.Inventory.StackSize); t.Output = new() { Slots = 2 }; }
        if (kind == "toolstation") t.Inventory.Slots = 12;
        if (kind == "pile") t.Inventory.Slots = PileStacks;
        if (kind == "chest") t.Inventory.Slots = ChestStacks;
        if (kind == "farm") t.Inventory.Slots = Data.Farm.Sow.Keys.Union(Data.Farm.Yield.Keys).Count();
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
            if (cliff != CliffOf(cliff.Ore) || cliff.Ore == "tin") continue;
            // Loose pieces roll well out past the deposits of the nearer cliff, so collecting them takes a search.
            // Iron is the tool-free bootstrap: 20 ore covers the anvil and pickaxe but not a bellows furnace too.
            (string kind, int count) = cliff.Ore == "iron" ? ("loose_iron", 10) : ("loose_coal", 6);
            for (int placed = 0; placed < count;)
            {
                var p = cliff.Center + cliff.Face * (4.2f + (float)rng.NextDouble() * 6.5f) + cliff.Along * ((float)rng.NextDouble() * 2 - 1) * (cliff.HalfLength + 4);
                if (!CanStand(p) || S.Things.Any(t => t.Pos.DistanceTo(p) < 2)) continue;
                Add(kind, p, 2); placed++;
            }
        }
        // Rocks and berry bushes stand alone on the plains, far from the clearing and each other, so more of them means exploring.
        Scatter(rng, "stone", 30, 64, 15, 9, _ => 1, 2000);
        Scatter(rng, "berries", 24, 34, 14, 10, _ => 1, 2000);
        var shore = Spawn - Terrain.Lake;
        Add("water", Terrain.Lake + shore.Normalized() * (Terrain.Shore(MathF.Atan2(shore.Y, shore.X)) + .2f), 9999);
        Add("tree", Spawn + new Vector2(5, 0), 42);
        // Trees are occasional on the plains and thicken into woods toward the mountains.
        // Full-size trees need room for their crowns, so even the woods stay walkable and readable.
        Scatter(rng, "tree", int.MaxValue, 42, 3.4f, 9, p => Math.Clamp(1.15f - Terrain.FromMountains(p) / 16, .015f, 1), 60000);
        DropLeaves();
        Look();
        Say("Welcome to Bramblewick. Gather wood and stone; build a sheltered workshop.");
    }
    /// <summary>Leaves a rake gathers in one go from a patch, which fills again every day.</summary>
    public const int LeafPatch = 12;
    public static readonly Vector2 LeafOffset = new(.9f, .9f);
    /// <summary>Leaf litter around every oak, the broadleaf one of the three tree variants picked by id; the patch sits off the trunk so both can be clicked.</summary>
    private void DropLeaves()
    {
        foreach (var tree in S.Things.Where(t => t.Kind == "tree" && t.Id % 3 == 0 && Terrain.Open(t.Pos + LeafOffset)).ToList()) Add("leaves", tree.Pos + LeafOffset, LeafPatch);
    }
    /// <summary>Places resources on open ground away from the clearing, at least <paramref name="spacing"/> apart from their own kind.</summary>
    /// <param name="chance">How likely a spot is kept, so density can follow the landscape.</param>
    /// <param name="tries">Random spots to consider; with a low chance this, not the count, sets how many are placed.</param>
    private void Scatter(Random rng, string kind, int count, int remaining, float spacing, float clearing, Func<Vector2, float> chance, int tries)
    {
        // A coarse grid of what is already placed keeps the spacing checks cheap on the large map.
        var near = new Dictionary<Vector2I, List<Thing>>();
        static Vector2I Cell(Vector2 p) => new((int)(p.X / 16), (int)(p.Y / 16));
        foreach (var t in S.Things) { if (!near.TryGetValue(Cell(t.Pos), out var list)) near[Cell(t.Pos)] = list = new(); list.Add(t); }
        bool Crowded(Vector2 p)
        {
            var cell = Cell(p);
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    if (near.TryGetValue(cell + new Vector2I(x, y), out var list) && list.Any(t => t.Pos.DistanceTo(p) < (t.Kind == kind ? spacing : 2))) return true;
            return false;
        }
        for (int placed = 0, tried = 0; placed < count && tried < tries; tried++)
        {
            var p = new Vector2(rng.Next(3, MapSize - 3), rng.Next(3, MapSize - 3));
            if (p.DistanceTo(Spawn) < clearing || !Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 3.5f) || Cliffs.Any(c => c.DistanceTo(p) < Cliff.Depth + 2.5f) || InSpur(p, 2.5f)) continue;
            if (rng.NextDouble() > chance(p) || Crowded(p)) continue;
            var t = Add(kind, p, remaining); placed++;
            if (!near.TryGetValue(Cell(p), out var cellList)) near[Cell(p)] = cellList = new();
            cellList.Add(t);
        }
    }
    public bool CanStand(Vector2 p) => Open(p) && !S.Things.Any(t => Blocks(t, p));
    /// <summary>Walkable ground ignoring buildings: inside the valley, off the water and clear of cliff walls and their spurs.</summary>
    private static bool Open(Vector2 p) => Terrain.Open(p) && !Cliffs.Any(c => c.DistanceTo(p) < Cliff.Depth) && !InSpur(p);
    private static bool Blocks(Thing t, Vector2 p) => !t.IsResource && !IsZone(t.Kind) && t.Kind is not "campfire" and not "grave" and not "signpost" and not "mine" && t.Pos.DistanceTo(p) < Footprint(t.Kind);
    /// <summary>Square 3×3 yards laid out on the ground, walkable and snapping edge to edge into grids.</summary>
    public static bool IsZone(string kind) => kind is "pile" or "farm";
    public const float ZoneSize = 3;
    public const int PileStacks = 14;
    /// <summary>A third of a pile, rounded to the nearest stack.</summary>
    public const int ChestStacks = (PileStacks + 1) / 3;
    /// <summary>Snaps a mine to its cliff and a yard to the grid of the nearest yard close enough to touch it.</summary>
    public Vector2 Snap(string kind, Vector2 p)
    {
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
    private static string A(string word) => ("aeiou".Contains(word[0]) ? "an " : "a ") + word;
    private static string Task(Thing t) => t.Kind switch
    {
        "tree" => "chop wood",
        "leaves" => "rake leaves",
        "mine" => t.Stage == 0 ? "dig the mine" : t.Stage == MineSupplied ? "fit the mine frame" : $"mine {CliffAt(t.Pos).Ore}",
        _ => $"mine {t.Kind}"
    };
    /// <summary>The fastest carried tool of a kind at or above <paramref name="tier"/>, or null when none is carried.</summary>
    public string? BestTool(string tool, Inventory? from = null, int tier = 0) => (from ?? S.Backpack).Items.Keys
        .Where(id => Data.Items.TryGetValue(id, out var def) && def.Tool == tool && def.Tier >= tier).OrderByDescending(id => Data.Items[id].Speed).FirstOrDefault();
    private float Speed(string? tool) => tool == null ? 1 : Data.Items[tool].Speed;
    public void MovePlayer(Vector2 delta)
    {
        if (Downed) return;
        if (delta == Vector2.Zero) return;
        playerBusy = true;
        if (S.Sheltered != 0) S.Sheltered = 0;
        var p = Player + delta;
        if (CanStand(p)) Player = p;
        else { if (CanStand(Player + new Vector2(delta.X, 0))) Player += new Vector2(delta.X, 0); if (CanStand(Player + new Vector2(0, delta.Y))) Player += new Vector2(0, delta.Y); }
    }
    public string? GatherBlock(Thing t, bool worker = false)
    {
        if (t.Kind == "farm") return t.Stage switch { FarmRipe => null, FarmGrowing => "The wheat is not ripe yet.", _ => "Nothing grows here yet. Sow the plot with wheat seeds and fertilizer." };
        if (!t.IsResource) return "Select a natural resource.";
        if (t.Remaining <= 0) return t.Kind switch { "berries" => $"Picked clean. The bush fruits again in {Data.BerryRegrowHours:0} hours.", "tree" => "Only a stump is left. Find another tree.", "leaves" => "Raked clean. More leaves fall by tomorrow.", _ => "Depleted. Look for another deposit." };
        // Workers fetch their tools from a tool station instead of needing them carried.
        if (worker || ToolKind(t) is not string kind || BestTool(kind) != null) return null;
        return kind switch
        {
            "axe" => "An axe is needed. Recover your belongings or craft one at a bench.",
            "rake" => "Raking leaves needs a rake. Craft one at a bench.",
            _ => t.Kind == "tin" ? "Mining tin needs a pickaxe." : "Craft a pickaxe at a bench. Loose rusty ore nearby can be picked up by hand."
        };
    }
    public static string ResourceItem(Thing t) => t.Kind switch { "tree" => "wood", "iron" or "loose_iron" => "iron_ore", "tin" => "tin_ore", "loose_coal" => "coal", "wolf_corpse" => "raw_meat", _ => t.Kind };
    public void Gather(Thing t, float dt)
    {
        // A loose piece is a single pick-up; anything else the host works on for as long as the player holds on to it.
        if (t.IsLoose ? Forwarded("pick_up", t.Id) : Remote) { S.Actor.GatherTarget = t.IsLoose ? 0 : t.Id; return; }
        S.Actor.GatherTarget = t.Id;
        playerBusy = true;
        if (!Near(t)) { gatherClock = 0; return; }
        var blocked = GatherBlock(t);
        if (blocked != null) { gatherClock = 0; return; }
        if (t.IsLoose) { PickUp(t); return; }
        if (t.Kind == "farm") { Harvest(t, dt); if (t.Stage == FarmFallow) Say("Harvested. Take the wheat, seeds and grass from the plot."); return; }
        gatherClock += dt;
        string? tool = t.Kind == "stone" ? BestTool("pickaxe") : ToolKind(t) is string kind ? BestTool(kind) : null;
        float seconds = Data.GatherSeconds / Speed(tool);
        if (gatherClock < seconds) return;
        gatherClock = 0;
        bool small = t.Kind == "wolf_corpse" || t.Kind == "stone" && tool == null;
        int amount = Math.Min(t.Remaining, small ? 2 : 4);
        amount = S.Backpack.Add(ResourceItem(t), amount); t.Remaining -= amount;
        if (amount == 0) Say("Backpack full. Store or sell some supplies.");
        else Say($"+{amount} {Data.Name(ResourceItem(t))} · {t.Remaining} left here.");
        if (t.Kind == "water") { S.Thirst = Math.Min(100, S.Thirst + 15); t.Remaining = 9999; Play("gather_water", t.Pos); }
        if (amount > 0) S.Milestones.Add("gather");
    }
    private void PickUp(Thing t)
    {
        string item = ResourceItem(t);
        int amount = S.Backpack.Add(item, t.Remaining); t.Remaining -= amount;
        if (amount == 0) { Say("Backpack full. Store or sell some supplies."); return; }
        if (t.Remaining <= 0) S.Things.Remove(t);
        Say($"+{amount} {Data.Name(item)}."); S.Milestones.Add("gather"); Play("wood_rocks_inventory", t.Pos);
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
    public bool Hammered(Thing anvil) => S.Players.Any(p => As(p, () => PlayerHammers(anvil))) ||
        S.Workers.Any(w => w.Assignment == anvil.Id && w.WageDebt <= 0 && w.WorkState == WorkerWorkState.Hammering);
    /// <summary>Why a building, or a crate from the backpack when <paramref name="item"/> names one, cannot go here.</summary>
    public string? PlacementBlock(string kind, Vector2 p, string? item = null)
    {
        bool crate = kind == "crate";
        if (!crate && !Data.Buildings.ContainsKey(kind)) return "Unknown building.";
        if (Downed) return "You are still recovering.";
        if (S.Sheltered != 0) return "Leave the cabin first.";
        if (kind == "mine") return MineBlock(p);
        if (IsZone(kind)) return ZoneBlock(kind, p);
        if (Player.DistanceTo(p) > 5.5f) return "Build within 5 tiles of yourself.";
        if (Player.DistanceTo(p) < 1.2f || Spawn.DistanceTo(p) < 1.2f) return "Leave your feet and the arrival point clear.";
        if (!CanStand(p) || Terrain.InLake(p, .8f)) return "Choose dry, open ground.";
        if (S.Things.Any(t => t.Pos.DistanceTo(p) < (t.IsResource ? Footprint(kind) + .65f : Footprint(kind) + Footprint(t.Kind) + .45f) && (t.Remaining > 0 || !t.IsResource))) return "Leave room around resources and buildings.";
        if (crate ? item != null && S.Backpack[item] == 0 : !S.Backpack.Has(Data.Buildings[kind].Cost)) return crate ? "That crate is no longer in your backpack." : "Missing materials in your backpack.";
        return null;
    }
    private string? ZoneBlock(string kind, Vector2 p)
    {
        if (Player.DistanceTo(p) > 5.5f) return "Build within 5 tiles of yourself.";
        if (ZoneTiles(p).Any(q => !CanStand(q) || Terrain.InLake(q, .8f) || Spawn.DistanceTo(q) < 1)) return "Choose dry, open ground.";
        float half = ZoneSize / 2;
        // Yards may touch edge to edge; anything else keeps its usual room from the square.
        bool Inside(Thing t, float margin) => Math.Abs(t.Pos.X - p.X) < half + margin && Math.Abs(t.Pos.Y - p.Y) < half + margin;
        if (S.Things.Any(t => IsZone(t.Kind) ? Inside(t, half - .05f) : (t.Remaining > 0 || !t.IsResource) && Inside(t, t.IsResource ? .3f : Footprint(t.Kind) + .2f))) return "Leave room around resources and buildings.";
        return S.Backpack.Has(Data.Buildings[kind].Cost) ? null : "Missing materials in your backpack.";
    }
    public Thing? Build(string kind, Vector2 p)
    {
        p = Snap(kind, p);
        var blocked = PlacementBlock(kind, p);
        if (blocked != null) { Say(blocked); return null; }
        if (Forwarded("build", item: kind, at: p)) return null;
        S.Backpack.Spend(Data.Buildings[kind].Cost);
        var t = Add(kind, p); if (kind == "campfire") { t.Burn = Data.CampfireSeconds; Play("start_fire", p); }
        S.Milestones.Add(kind); Say(kind == "mine" ? $"Marked the {MineName(t)} site. Assign a worker to dig the entrance; they need a pickaxe." : $"Built {Data.Name(kind)}."); return t;
    }
    public void Consume(string item)
    {
        if (Downed || !Data.Items.TryGetValue(item, out var def) || Forwarded("consume", item: item)) return;
        if (def.Hunger <= 0 && def.Thirst <= 0) { Say($"{def.Name} needs cooking first."); return; }
        if (S.Backpack.Remove(item, 1) == 0) { Say($"No {def.Name} in your backpack."); return; }
        S.Hunger = Math.Min(100, S.Hunger + def.Hunger); S.Thirst = Math.Min(100, S.Thirst + def.Thirst); S.Health = Math.Min(100, S.Health + def.Health);
        if (def.Food) Play("eat_berry", Player);
    }
    public void Pump(Thing t)
    {
        if (!Near(t) || t.Kind != "reinforced" || Forwarded("pump", t.Id)) return;
        playerBusy = true;
        t.Heat = Math.Min(100, t.Heat + 24); PlayerActed?.Invoke(ActorAction.Pump, t.Pos); Say($"Bellows: {t.Heat:0}° / {SteelHeat:0}° needed for steel.");
    }
    public void FuelFire(Thing t)
    {
        if (!Near(t) || Forwarded("fuel_fire", t.Id)) return;
        int n = S.Backpack.Remove("wood", 1);
        if (n == 0) { Say("You need wood to feed the fire."); return; }
        if (t.Burn <= 0) Play("start_fire", t.Pos);
        t.Burn += Data.CampfireSeconds; Say($"Campfire fed: +{Data.CampfireHours:0} hours of light.");
    }
    public void SetRecipe(Thing t, string recipe)
    {
        if (t.Processing) { Say("Wait until the current batch finishes."); return; }
        if (Forwarded("set_recipe", t.Id, item: recipe)) return;
        if (Data.Recipes.TryGetValue(recipe, out var r) && r.Station == t.Kind) { t.Recipe = recipe; t.Progress = 0; }
    }
    /// <summary>The fuel a station burns: coal in the reinforced furnace, otherwise its selection, falling back to wood for anything unknown.</summary>
    public static string BurnedFuel(Thing t) => t.Kind == "reinforced" ? "coal" : t.Fuel is "wood" or "coal" ? t.Fuel : "wood";
    public Dictionary<string, int> BatchCost(Thing t)
    {
        var cost = new Dictionary<string, int>();
        if (!Data.Recipes.TryGetValue(t.Recipe, out var r)) return cost;
        foreach (var p in r.Inputs) cost[p.Key] = p.Value;
        if (r.Fuel)
        {
            string fuel = BurnedFuel(t);
            cost[fuel] = cost.GetValueOrDefault(fuel) + 1;
        }
        return cost;
    }
    public void LoadBatch(Thing t, int batches = 1)
    {
        if (!Near(t)) { Say("Walk closer to load this building."); return; }
        if (Forwarded("load_batch", t.Id, count: batches)) return;
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
        if (Forwarded("load_input", t.Id, item: item, count: quantity)) return 0;
        int moved = S.Backpack.TransferTo(t.Inventory, item, Math.Min(quantity, t.Inventory.StackSize - t.Inventory[item]));
        if (moved == 0) Say(S.Backpack[item] == 0 ? $"No {Data.Name(item)} in your backpack." : $"The {Data.Name(item)} slot is full.");
        return moved;
    }
    public void Collect(Thing t)
    {
        if (!Near(t)) { Say("Walk closer to collect items."); return; }
        if (Forwarded("collect", t.Id)) return;
        foreach (var item in Leftovers(t).ToArray()) t.Inventory.TransferTo(S.Backpack, item, 999);
    }
    /// <summary>Finished goods plus anything a previous recipe or fuel choice left behind.</summary>
    public IEnumerable<string> Leftovers(Thing t)
    {
        var cost = BatchCost(t);
        return t.Inventory.Items.Keys.Where(item => !cost.ContainsKey(item));
    }
    /// <summary>Steps inside a cabin, out of reach of wolves; any movement steps back out.</summary>
    public bool Enter(Thing hut)
    {
        if (hut.Kind != "hut" || !Near(hut, 2.6f)) return false;
        if (!Forwarded("enter", hut.Id)) S.Sheltered = hut.Id;
        return true;
    }
    public void Leave() { if (!Forwarded("leave")) S.Sheltered = 0; }
    private float SecondsToDawn => Data.DaySeconds - S.Time % Data.DaySeconds + 1;
    /// <summary>Why sleeping is not possible now, or null; hunger and thirst must last until morning.</summary>
    public string? SleepBlock()
    {
        if (!Night) return "You can sleep at night. Keep food and water for the morning.";
        bool hungry = S.Hunger - SecondsToDawn * Data.HungerPerSecond <= 0, thirsty = S.Thirst - SecondsToDawn * Data.ThirstPerSecond <= 0;
        if (hungry || thirsty) return hungry && thirsty ? "You are too hungry and thirsty to sleep." : hungry ? "You are too hungry to sleep." : "You are too thirsty to sleep.";
        // The night passes for everyone at once, so the others have to be inside and fed too.
        var others = S.Players.Where(p => p != S.Actor).ToList();
        if (others.Any(p => p.Sheltered == 0)) return "Everyone has to be inside a cabin to sleep.";
        if (others.Any(p => p.Hunger - SecondsToDawn * Data.HungerPerSecond <= 0 || p.Thirst - SecondsToDawn * Data.ThirstPerSecond <= 0)) return "Someone is too hungry or thirsty to sleep.";
        return null;
    }
    public void Sleep(Thing hut)
    {
        if (!Near(hut)) return;
        if (SleepBlock() is string blocked) { Say(blocked); return; }
        if (Forwarded("sleep", hut.Id)) return;
        // Time, wages, trades, hunger and production advance together; no frozen night exploit.
        float seconds = SecondsToDawn;
        asleep = true;
        while (seconds > 0) { float dt = Math.Min(1, seconds); Tick(dt, true); seconds -= dt; }
        asleep = false;
        S.Wolves.Clear(); SayToAll(S.Workers.Count > 0 ? "Dawn breaks. Your workers are walking back from Bramblewick." : "Dawn breaks.");
    }
    /// <summary>Burning campfires keep wolves out of their light, unless a wolf is already chasing the player.</summary>
    public bool Protected(Vector2 p) => S.Things.Any(t => t.Kind == "campfire" && t.Burn > 0 && t.Pos.DistanceTo(p) < 5.3f);
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
    public int PackAround => S.Wolves.Count(w => w.Pos.DistanceTo(Player) < StalkDistance * 2);
    /// <summary>A burning torch in hand, with the player out in the open, sets how wolves behave.</summary>
    private bool Torchlit => S.TorchLit && !Downed && S.Sheltered == 0 && !Protected(Player);
    /// <summary>Lights the torch in hand, taking a new one from the backpack if the hand is empty, or swings it once it burns.</summary>
    public void UseTorch(Vector2 aim)
    {
        if (Downed || S.Sheltered != 0 || S.TorchKindling > 0 || Forwarded("torch", at: aim)) return;
        if (S.TorchLit) { SwingTorch(aim); return; }
        if (S.Torch <= 0)
        {
            if (S.Backpack.Remove("torch", 1) == 0) { Say("No torch in your backpack. Make one with a chisel from the crafting menu."); return; }
            S.Torch = Data.CampfireSeconds;
        }
        S.TorchKindling = KindleSeconds; PlayerActed?.Invoke(ActorAction.Kindle, Player);
    }
    private void SwingTorch(Vector2 aim)
    {
        if (attackClock > 0) return;
        attackClock = .6f;
        torchSwings.RemoveAll(t => S.Time - t > WildWindow); torchSwings.Add(S.Time);
        Vector2 direction = aim == Vector2.Zero ? Vector2.Down : aim.Normalized();
        PlayerActed?.Invoke(ActorAction.TorchSwing, Player + direction); Play("tool_air_swing", Player);
        if (torchSwings.Count >= WildSwings) { PutOutTorch(); Say("Torch went off due to wild swinging."); return; }
        TorchFlare = .5f;
        bool pack = PackAround >= PackSize;
        var wolf = S.Wolves.Where(w => w.Leap <= 0 && !w.Feeding).OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
        if (wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f)
        {
            wolf.Health -= 1; wolf.Flee = FleeSeconds; wolf.Pos += (wolf.Pos - Player).Normalized() * 1.2f;
            if (wolf.Health <= 0)
            {
                S.Wolves.Remove(wolf); Add("wolf_corpse", wolf.Pos.Clamp(Vector2.One * 2, Vector2.One * 45), 4);
                Say("The wolf falls. Butcher the carcass for meat and cook it in a furnace.");
            }
        }
        // A wave of fire toward a wolf on its own makes it keep its distance; a pack closing in no longer cares.
        if (!pack)
            foreach (var w in S.Wolves.Where(w => w.Pos.DistanceTo(Player) < ScareRange && (w.Pos - Player).Normalized().Dot(direction) > .7f)) w.Scared = true;
    }
    private void PutOutTorch()
    {
        S.TorchLit = false; S.TorchKindling = 0; torchSwings.Clear();
        foreach (var w in Hunters) w.Scared = false;
    }
    public void Attack()
    {
        if (attackClock > 0 || Downed || S.Sheltered != 0 || Forwarded("attack")) return;
        attackClock = .6f;
        var wolf = S.Wolves.OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
        string? axe = BestTool("axe");
        ActorAction action = axe == "steel_axe" ? ActorAction.SteelChop : axe != null ? ActorAction.Chop : ActorAction.Attack;
        Vector2 target = wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f ? wolf.Pos : Player;
        PlayerActed?.Invoke(action, target); Play("tool_air_swing", Player);
        if (wolf != null && wolf.Pos.DistanceTo(Player) < 2.6f)
        {
            wolf.Health -= axe == null ? .5f : 1.5f * Speed(axe);
            wolf.Pos += (wolf.Pos - Player).Normalized() * 1.2f;
            if (wolf.Health <= 0)
            {
                S.Wolves.Remove(wolf); Add("wolf_corpse", wolf.Pos.Clamp(Vector2.One * 2, Vector2.One * (MapSize - 3)), 4);
                Say("The wolf falls. Butcher the carcass for meat and cook it in a furnace.");
            }
        }
    }
    public void Die()
    {
        // After a mauling the box lands past the feet, away from the wolf at the throat, so the feeding stays visible.
        var killer = S.Mauled ? Hunters.FirstOrDefault(w => w.Pinning) : null;
        var box = Add("grave", killer == null ? Player : Player + (Player - killer.Pos).Normalized() * 1.5f); box.Inventory = S.Backpack;
        S.Backpack = new Inventory { Slots = 24 }; S.RespawnIn = Data.RespawnSeconds; S.Sheltered = 0; S.Torch = 0; PutOutTorch();
        if (S.Mauled)
        {
            // The pack eats every scrap of food and stays to feed until the player respawns.
            foreach (var item in box.Inventory.Items.Keys.Where(Data.IsFood).ToArray()) box.Inventory.Remove(item, box.Inventory[item]);
            Say($"{(Hunters.Count() > 1 ? "The wolves" : "A wolf")} brought you down and ate all your food. The rest is in a recovery box. Respawning in {Data.RespawnSeconds:0} seconds.");
            return;
        }
        S.Wolves.RemoveAll(w => w.Target == S.Actor.Id);
        Say($"You collapsed. Your belongings are in a recovery box where you fell. Respawning in {Data.RespawnSeconds:0} seconds.");
    }
    private void Respawn()
    {
        S.Health = 100; S.Hunger = 65; S.Thirst = 65; Player = Spawn; S.Actor.Warp++; S.Mauled = false; S.Sheltered = 0; S.Wolves.RemoveAll(w => w.Target == S.Actor.Id);
        Say("You wake in the clearing. Recover your belongings from the box.");
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
    private static Inventory NewWageSlot() => new() { Slots = 1, InputFilter = { Inventory.Coins }, OutputFilter = { Inventory.Coins } };
    /// <summary>Why an item cannot go into this box by hand: a wage slot only holds coins.</summary>
    public string? BoxBlock(Thing t, Inventory box, string item) => box == t.Wages && item != Inventory.Coins ? "The wage slot only holds coins." : box == t.Inventory ? HoldBlock(t, item) : null;
    /// <summary>Coins kept for wages in every signpost's wage slot.</summary>
    public int WageCoins => S.Things.Sum(t => t.Wages?[Inventory.Coins] ?? 0);
    /// <summary>Pays wages from the signposts' wage slots first, then the acting player's backpack, or nothing if together they fall short.</summary>
    private bool PayWage(int amount)
    {
        if (amount > WageCoins + Coins) return false;
        foreach (var box in S.Things.Select(t => t.Wages).OfType<Inventory>()) amount -= box.Remove(Inventory.Coins, amount);
        return Pay(amount);
    }
    /// <summary>The village only takes packed crates holding nothing but goods it buys.</summary>
    public bool CanSell(string item) => Packed(item) is { Count: > 0 } contents && contents.Items.Keys.All(Data.Market.Sells.ContainsKey);
    /// <summary>The sale price as last reported from the village; it goes stale until someone arrives with news.</summary>
    public float KnownPrice(string item) => S.Prices.TryGetValue(item, out float price) ? price : Data.Items[item].Price;
    public int PriceAge => Day - (int)(S.PricesTime / Data.DaySeconds) - 1;
    private void UpdatePrices()
    {
        S.Prices = Data.Market.Sells.Keys.ToDictionary(item => item, item => UnitPrice(item, false)); S.PricesTime = S.Time;
    }
    private string? CartBlock(Thing sign) => !Near(sign) || sign.Kind != "signpost" ? "Walk closer to the signpost." : S.Orders.Count >= 3 ? "All three village carts are busy." : null;
    /// <summary>Sends every acceptable crate in the market input on one cart; the coins come back into the market output.</summary>
    public bool Sell(Thing sign)
    {
        if (CartBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Forwarded("sell", sign.Id)) return false;
        var crates = sign.Inventory.Items.Keys.Where(CanSell).ToList();
        if (crates.Count == 0) { Say(sign.Inventory.Count > 0 ? "The village only takes crates packed with meat, berries or steel tools." : "Put a packed crate in the market first."); return false; }
        var goods = new Dictionary<string, int>();
        foreach (var crate in crates)
        {
            foreach (var (item, quantity) in Packed(crate)!.Items) goods[item] = goods.GetValueOrDefault(item) + quantity;
            sign.Inventory.Remove(crate, 1); S.Crates.Remove(PackedId(crate));
        }
        // The crates go with the goods.
        float price = crates.Count * Data.Items["crate"].Price;
        foreach (var (item, quantity) in goods)
        {
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
    public bool Buy(Thing sign, string item, int lots)
    {
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
    private string Pack(Inventory contents)
    {
        int id = S.NextId++; S.Crates[id] = contents; return PackedPrefix + id;
    }
    /// <summary>An item's name, with what a packed crate holds.</summary>
    public string Describe(string item) => Packed(item) is Inventory contents ? $"{Data.Name("crate")} of {Goods(contents.Items)}" : Data.Name(item);
    /// <summary>Puts a crate from the backpack down, still holding whatever it was packed with.</summary>
    public Thing? PlaceCrate(string item, Vector2 p)
    {
        if (!IsCrate(item)) return null;
        if (PlacementBlock("crate", p, item) is string blocked) { Say(blocked); return null; }
        if (Forwarded("place_crate", item: item, at: p)) return null;
        S.Backpack.Remove(item, 1);
        var crate = Add("crate", p);
        if (Packed(item) is Inventory contents) { crate.Inventory = contents; S.Crates.Remove(PackedId(item)); }
        Play("close_chest", p); Say($"Placed {Data.Name("crate")}. Left click to open it; hold right click to pick it up."); return crate;
    }
    /// <summary>Lifts a placed crate into the backpack; anything inside stays packed in it.</summary>
    public bool PickUpCrate(Thing crate)
    {
        if (crate.Kind != "crate" || !Near(crate) || Forwarded("pick_up_crate", crate.Id)) return false;
        bool empty = crate.Inventory.Count == 0;
        if (S.Backpack.Room(empty ? "crate" : PackedPrefix) == 0) { Say("Backpack full. Make room for the crate."); return false; }
        S.Backpack.Add(empty ? "crate" : Pack(crate.Inventory), 1); S.Things.Remove(crate);
        Play("wood_rocks_inventory", crate.Pos); Say(empty ? "Picked up an empty crate." : "Picked up a packed crate."); return true;
    }
    // ---------- Crafting ----------
    /// <summary>Hand recipes for the personal crafting menu.</summary>
    public IEnumerable<string> HandRecipes => Data.Recipes.Where(p => p.Value.Hand).Select(p => p.Key);
    /// <summary>Why a recipe cannot be queued from the backpack now, or null.</summary>
    public string? CraftBlock(string recipe)
    {
        if (!Data.Recipes.TryGetValue(recipe, out var r) || !r.Hand) return "This can only be made at a station.";
        if (Downed) return "You are still recovering.";
        if (r.Tool != "" && BestTool(r.Tool, null, r.Tier) == null) return $"Needs a {Data.ToolName(r.Tool, r.Tier)} in your backpack.";
        if (!S.Backpack.Has(r.Inputs)) return $"Needs {Goods(r.Inputs)} in your backpack.";
        return S.Crafting.Count >= 8 ? "Eight things are already being made." : null;
    }
    /// <summary>Queues a hand recipe; its inputs leave the backpack now so they cannot be spent twice.</summary>
    public bool Craft(string recipe)
    {
        if (CraftBlock(recipe) is string blocked) { Say(blocked); return false; }
        if (Forwarded("craft", item: recipe)) return false;
        S.Backpack.Spend(Data.Recipes[recipe].Inputs); S.Crafting.Add(recipe); return true;
    }
    /// <summary>Cancels a queued recipe and returns its inputs; what the backpack cannot hold goes into a box at the player's feet.</summary>
    public void CancelCraft(int index)
    {
        if (index < 0 || index >= S.Crafting.Count || Forwarded("cancel_craft", count: index)) return;
        foreach (var (item, quantity) in Data.Recipes[S.Crafting[index]].Inputs) Give(item, quantity);
        S.Crafting.RemoveAt(index); if (index == 0) S.CraftTime = 0;
    }
    private void Give(string item, int quantity)
    {
        int left = quantity - S.Backpack.Add(item, quantity);
        if (left <= 0) return;
        var box = Add("grave", Player); box.Inventory.Slots = left / box.Inventory.StackOf(item) + 1; box.Inventory.Add(item, left);
    }
    /// <summary>How fast the first queued recipe progresses: the carried tool's speed, or zero while its tool is missing.</summary>
    public float CraftRate => S.Crafting.Count == 0 || Downed ? 0 : Data.Recipes[S.Crafting[0]] is var r && r.Tool != "" ? BestTool(r.Tool, null, r.Tier) is string held ? Speed(held) : 0 : 1;
    private void CraftTick(float dt)
    {
        float rate = CraftRate;
        if (rate <= 0) return;
        var r = Data.Recipes[S.Crafting[0]];
        S.CraftTime = Math.Min(r.Seconds, S.CraftTime + dt * rate);
        // A full backpack holds the finished item back rather than losing it.
        if (S.CraftTime < r.Seconds || S.Backpack.Room(r.Output) < r.Amount) return;
        S.Backpack.Add(r.Output, r.Amount); S.Crafting.RemoveAt(0); S.CraftTime = 0;
        Crafted(r.Output); Play(Sounds.ForItem(r.Output), Player); Say($"Made {r.Amount} {Data.Name(r.Output)}.");
    }
    private void Crafted(string output)
    {
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
    public bool SlotTool(Thing bench, string item)
    {
        string name = Data.Name(bench.Kind).ToLowerInvariant();
        if (bench.Tools == null || !Near(bench)) { Say($"Walk closer to the {name}."); return false; }
        string tool = Data.Items.TryGetValue(item, out var def) ? def.Tool : "";
        var kinds = SlotKinds(bench.Kind);
        if (!kinds.Contains(tool)) { Say($"The {name} has slots for {(kinds.Length == 1 ? A(kinds[0]) : $"a {kinds[0]} and a {kinds[1]}")}."); return false; }
        if (BenchTool(bench, tool) != null) { Say($"The {tool} slot is taken. Take that {tool} out first."); return false; }
        if (Forwarded("slot_tool", bench.Id, item: item)) return false;
        return S.Backpack.TransferTo(bench.Tools, item, 1) == 1;
    }
    public void UnslotTool(Thing bench, string item)
    {
        if (bench.Tools == null || !Near(bench)) { Say($"Walk closer to the {Data.Name(bench.Kind).ToLowerInvariant()}."); return; }
        if (Forwarded("unslot_tool", bench.Id, item: item)) return;
        if (bench.Tools.TransferTo(S.Backpack, item, 1) == 0 && bench.Tools[item] > 0) Say("Backpack full.");
    }
    public string? FetchBlock(Thing sign)
    {
        var crew = S.Workers.Where(w => w.Assignment == sign.Id).ToList();
        if (crew.Count == 0) return "No worker assigned.";
        if (Dusk) return "Your workers have signed out until morning.";
        if (Lunch) return "Your workers are at lunch.";
        if (crew.FirstOrDefault(w => w.Away > 0) is Worker away) return $"{away.Name} is already on the way.";
        return crew.Any(w => w.WageDebt <= 0) ? null : "The assigned worker is on strike.";
    }
    /// <summary>Sends the post's worker to the village; the price sheet refreshes when they return.</summary>
    public bool FetchPrices(Thing sign)
    {
        if (FetchBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Forwarded("fetch_prices", sign.Id)) return false;
        var w = S.Workers.First(w => w.Assignment == sign.Id && w.WageDebt <= 0);
        w.Away = Data.TradeSeconds; Say($"{w.Name} set off to Bramblewick for fresh prices."); return true;
    }
    public Worker? Hire(Thing sign, HireOffer offer)
    {
        if (!Near(sign) || sign.Kind != "signpost" || Forwarded("hire", sign.Id, count: Data.HireOffers.IndexOf(offer))) return null;
        if (S.Workers.Count >= 8) { Say("The POC supports eight workers."); return null; }
        if (!Pay(offer.Upfront)) { Say($"This offer needs {offer.Upfront:0} coins up front from your backpack."); return null; }
        string[] names = { "Rowan", "Hazel", "Milo", "Fern", "Jasper", "Willow", "Clover", "Ash" };
        string name = names.FirstOrDefault(n => S.Workers.All(w => w.Name != n)) ?? "Neighbor";
        var w = new Worker { Id = S.NextId++, Name = name, Pos = sign.Pos + new Vector2(1, 0), Wage = offer.Wage };
        S.Workers.Add(w); S.Milestones.Add("hired"); Say($"{name} joined. Select them → Work → click a resource, or Assign at a building."); return w;
    }
    public void Assign(Worker w, Thing? target)
    {
        if (target?.Kind == "mine" && w.Assignment != target.Id && S.Workers.Count(o => o.Assignment == target.Id) >= Data.Mine.Workers) { Say($"A mine has room for {Data.Mine.Workers} workers."); return; }
        if (Forwarded("assign", target?.Id ?? 0, w.Id)) return;
        // Reassigning calls off a price errand, so the worker never freezes on the road.
        w.Assignment = target?.Id ?? 0; w.Away = 0; w.Job = target == null ? "idle" : target.IsResource ? "gather" : "building"; w.WorkState = WorkerWorkState.Idle;
        w.Destination = 0; w.Timer = 0; w.Status = target == null ? "Available" : "Starting work";
        if (target != null && target.Kind != "signpost") S.Milestones.Add("assigned");
    }
    public void Fire(Worker w)
    {
        if (Forwarded("fire", other: w.Id)) return;
        if (w.Bag.Count > 0 || w.Tool != "") { var box = Add("grave", w.Pos); box.Inventory = w.Bag; box.Inventory.Slots++; box.Inventory.Add(w.Tool, w.Tool == "" ? 0 : 1); }
        S.Workers.Remove(w); Say($"{w.Name} has left. Carried items were left in a recovery box.");
    }
    public bool Holds(Thing t, string item) => HoldBlock(t, item) == null;
    /// <summary>Why storage will not take an item: a tool station holds only tools, a pile one resource and compost what rots; other storage holds anything.</summary>
    public string? HoldBlock(Thing t, string item) => t.Kind switch
    {
        "toolstation" when !(Data.Items.TryGetValue(item, out var def) && def.Tool != "") => "The tool station only holds tools.",
        "pile" when t.Compost && !Compostable(item) && item != "fertilizer" => "The compost takes leaves, grass and food.",
        "pile" when !t.Compost && t.Inventory.Items.Keys.FirstOrDefault(k => k != item) is string held => $"This pile holds {Data.Name(held)}. Empty it to store something else.",
        "farm" when !FarmItem(item) => $"A farm plot takes {string.Join(" and ", Data.Farm.Sow.Keys.Select(Data.Name)).ToLowerInvariant()}.",
        _ => null
    };
    public bool Compostable(string item) => item is "leaves" or "grass" || Data.IsFood(item);
    /// <summary>Turns a pile into compost or back; refused while it holds something the new use would not take.</summary>
    public bool SetCompost(Thing pile, bool on)
    {
        if (pile.Kind != "pile" || pile.Compost == on) return false;
        var items = pile.Inventory.Items.Keys.ToList();
        if (on ? items.Any(i => !Compostable(i) && i != "fertilizer") : items.Count > 1) { Say(on ? "Empty the pile before composting." : "Empty the compost down to one resource first."); return false; }
        if (Forwarded("set_compost", pile.Id, count: on ? 1 : 0)) return true;
        pile.Compost = on; pile.Progress = 0; PileFilters(pile); return true;
    }
    /// <summary>Piles keep their automation filters in step with what they hold: one resource in and out, or compost in and fertilizer out.</summary>
    private void PileFilters(Thing pile)
    {
        var inv = pile.Inventory;
        inv.InputFilter.Clear(); inv.OutputFilter.Clear();
        if (pile.Compost) { inv.InputFilter.UnionWith(Data.Items.Keys.Where(Compostable)); inv.OutputFilter.Add("fertilizer"); }
        else if (inv.Items.Keys.FirstOrDefault() is string item) { inv.InputFilter.Add(item); inv.OutputFilter.Add(item); }
    }
    /// <summary>Compost turns 4 units of whatever it holds into 1 fertilizer each in-game hour.</summary>
    public const int CompostBatch = 4;
    private void CompostTick(Thing pile, float dt)
    {
        int rotting = pile.Inventory.Items.Where(p => Compostable(p.Key)).Sum(p => p.Value);
        if (rotting < CompostBatch || pile.Inventory.Room("fertilizer") == 0) { pile.Progress = 0; return; }
        pile.Progress += dt;
        if (pile.Progress < Data.DaySeconds / 24) return;
        pile.Progress = 0;
        for (int left = CompostBatch; left > 0;) left -= pile.Inventory.Remove(pile.Inventory.Items.Keys.First(Compostable), left);
        pile.Inventory.Add("fertilizer", 1);
    }
    // Only chests and piles take part in automation; recovery boxes hold the player's or a dismissed worker's belongings.
    public Thing? DepositFor(string item, Vector2 origin, int exclude = 0) => S.Things
        .Where(t => t.Kind is "chest" or "pile" && t.Id != exclude && t.Inventory.Room(item) > 0 && t.Inventory.Accepts(item) && Holds(t, item))
        // Compost names every food in its filter, but takes food only when nothing else will, so harvests and picked berries are not left to rot.
        .OrderBy(t => t.Compost && Data.IsFood(item) ? 2 : t.Inventory.InputFilter.Contains(item) ? 0 : 1).ThenBy(t => t.Pos.DistanceSquaredTo(origin)).FirstOrDefault();
    public Thing? SourceFor(string item, Vector2 origin, int exclude = 0) => S.Things
        .Where(t => t.Kind is "chest" or "pile" && t.Id != exclude && t.Inventory[item] > 0 && t.Inventory.AllowsWithdrawal(item))
        .OrderBy(t => t.Pos.DistanceSquaredTo(origin)).FirstOrDefault();
    private List<Vector2> FindRoute(Vector2 from, Vector2 destination)
    {
        // Collected once, since a search on the large map can visit thousands of tiles.
        var blockers = S.Things.Where(t => Blocks(t, t.Pos)).ToList();
        bool CanStand(Vector2 p) => Open(p) && !blockers.Any(t => Blocks(t, p));
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
    /// <summary>Moves a worker toward a destination and reports arrival within <paramref name="reach"/>.
    /// A spot closer than the usual reach is a preference: if it cannot be reached, the usual reach counts as arrival.</summary>
    private bool Walk(Worker w, Vector2 destination, float dt, float reach = 1.2f)
    {
        float distance = w.Pos.DistanceTo(destination);
        if (distance < reach) return true;
        if (w.RouteTarget.DistanceTo(destination) > .2f) { w.Route.Clear(); w.RouteTarget = destination; }
        // Stop at arrival range rather than overshooting into the target's footprint, where the step would be refused.
        var next = w.Pos.MoveToward(destination, Math.Min(dt * 2.25f, distance - reach + .05f));
        if (w.Route.Count == 0 && CanStand(next)) { w.Pos = next; return false; }
        if (w.Route.Count > 0 && !CanStand(w.Route[0])) w.Route.Clear();
        if (w.Route.Count == 0) w.Route.AddRange(FindRoute(w.Pos, destination));
        if (w.Route.Count == 0 && distance < 1.2f) return true;
        if (w.Route.Count == 0) { w.Status = "Path blocked · clear a route"; w.WorkState = WorkerWorkState.Blocked; return false; }
        w.Pos = w.Pos.MoveToward(w.Route[0], dt * 2.25f);
        if (w.Pos.DistanceTo(w.Route[0]) < .06f) w.Route.RemoveAt(0);
        return false;
    }
    private void WorkerTick(Worker w, float dt)
    {
        w.WorkState = WorkerWorkState.Idle;
        if (w.Away > 0)
        {
            w.Away = Math.Max(0, w.Away - dt); w.Status = $"Fetching village prices · back in {w.Away:0}s";
            // Like a cart, the runner walks a little way down the valley and fades from view rather than pathing around the woods.
            if (Find(w.Assignment) is { Kind: "signpost" } post) w.Pos = post.Pos + Vector2.One + Terrain.Seaward * TripDistance * TripLeg(1 - w.Away / Data.TradeSeconds);
            if (w.Away == 0) { UpdatePrices(); Say($"{w.Name} is back with fresh prices from Bramblewick."); }
            return;
        }
        // Like a cart, the walk home goes a little way down the valley over half a trade trip while the worker fades out, and back at dawn.
        float half = Data.TradeSeconds / 2, pace = TripDistance / half;
        if (w.Home && Dusk) { w.Status = "Signed out · back in the morning"; return; }
        if (!Dusk && w.Commute > 0)
        {
            w.Home = false; w.Status = "Walking in from Bramblewick";
            float step = Math.Min(dt, w.Commute);
            w.Commute -= step; w.Pos -= Terrain.Seaward * pace * step;
            if (w.Commute == 0) w.Route.Clear();
            return;
        }
        w.Home = false;
        if (Dusk)
        {
            // Everyone signs out from the nearest signpost, or the clearing without one, and comes back there at dawn.
            // Assignments and loads are kept, and one cut off from the way still sets off.
            w.Status = "Heading home to Bramblewick";
            if (w.Commute == 0)
            {
                if (w.Tool != "" && !ReturnTool(w, dt)) return;
                var post = S.Things.Where(t => t.Kind == "signpost").MinBy(t => t.Pos.DistanceSquaredTo(w.Pos));
                if (!Walk(w, post?.Pos + Vector2.One ?? Spawn, dt) && w.WorkState != WorkerWorkState.Blocked) return;
                w.WorkState = WorkerWorkState.Idle;
            }
            float step = Math.Min(dt, half - w.Commute);
            w.Commute += step; w.Pos += Terrain.Seaward * pace * step;
            if (w.Commute >= half) { w.Home = true; w.Status = "Signed out · back in the morning"; }
            return;
        }
        if (w.WageDebt > 0) { w.Status = "On strike · wages owed"; return; }
        if (Lunch) { EatLunch(w, dt); return; }
        var target = Find(w.Assignment);
        if (w.Bag.Count > 0)
        {
            string item = w.Bag.Items.Keys.First();
            var dest = Find(w.Destination);
            // Re-evaluate rules every tick, including filters changed while a worker is carrying goods.
            // A mine site takes only what it still needs, so a surplus goes back to storage.
            int wanted = dest?.Kind == "mine" ? MineNeeds(dest).GetValueOrDefault(item) : int.MaxValue;
            if (dest == null || wanted == 0 || dest.Inventory.Room(item) == 0 || !dest.Inventory.Accepts(item) || (dest.Id == target?.Id && w.Job != "building")) dest = DepositFor(item, w.Pos);
            if (dest == null) { w.Status = $"Holding {Data.Name(item)} · no accepting storage"; return; }
            w.Destination = dest.Id; w.Status = $"Carrying {Data.Name(item)} → {Data.Name(dest.Kind)}";
            if (Walk(w, dest.Pos, dt))
            {
                w.Bag.TransferTo(dest.Inventory, item, dest.Kind == "mine" ? MineNeeds(dest).GetValueOrDefault(item) : w.Bag[item], true); w.Destination = 0;
                if (w.Bag.Count == 0) w.Bag.StackSize = Worker.BagStack;
            }
            return;
        }
        string? need = target is { IsResource: true } ? ToolKind(target) : target is { Kind: "mine" } ? MineTool(target.Stage) : null;
        if (need == null) w.Missing = "";
        if (w.Tool != "" && Data.Items[w.Tool].Tool != need && !ReturnTool(w, dt)) return;
        if (target == null) { w.Status = "Available"; return; }
        if (target.IsResource)
        {
            string? blocked = GatherBlock(target, true);
            if (blocked != null) { w.Status = blocked; return; }
            string item = ResourceItem(target);
            if (DepositFor(item, target.Pos) == null) { w.Status = "Needs an accepting storage chest"; return; }
            if (need != null && w.Tool == "") { FetchTool(w, need, target, dt); return; }
            w.Status = $"Gathering {Data.Name(item)}";
            if (!Walk(w, target.Pos, dt)) return;
            w.WorkState = WorkerWorkState.Gathering;
            w.Timer += dt;
            if (target.IsLoose || w.Timer >= Data.GatherSeconds * 1.6f / Speed(w.Tool == "" ? null : w.Tool))
            {
                w.Timer = 0; target.Remaining -= w.Bag.Add(item, target.Remaining);
                if (target.Kind == "water") target.Remaining = 9999;
                if (target.IsLoose && target.Remaining <= 0) S.Things.Remove(target);
            }
            return;
        }
        var coworkers = S.Workers.Where(other => other.Assignment == target.Id && other.WageDebt <= 0).OrderBy(other => other.Id).ToList();
        if (target.Kind == "mine") { MineWork(w, target, coworkers, dt); return; }
        if (target.Kind == "farm") { FarmWork(w, target, dt); return; }
        if (target.Kind == "reinforced" && coworkers.Count >= 2 && coworkers[0] == w)
        {
            w.Status = "Operating bellows"; if (Walk(w, target.Pos + BellowsStand, dt, .1f)) { w.WorkState = WorkerWorkState.Pumping; target.Heat = Math.Min(100, target.Heat + dt * 18); }
            return;
        }
        if (target.Kind == "signpost") { w.Status = "Minding the trading post"; Walk(w, target.Pos + Vector2.One, dt); return; }
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
        // An anvil only works while hammered, so its smith stays put until the batch is done before fetching the next one.
        if (!Worked(target) || !target.Processing) foreach (var pair in costs)
        {
            if (target.Inventory[pair.Key] >= pair.Value * 2) continue;
            var source = SourceFor(pair.Key, w.Pos);
            if (source == null) continue;
            w.Status = $"Fetching {Data.Name(pair.Key)}";
            if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, pair.Key, pair.Value * 2 - target.Inventory[pair.Key], true); w.Destination = target.Id; }
            return;
        }
        // Only one worker ever pumps: a lone worker pumps between loads, while extra crew only haul.
        bool pumps = target.Kind == "reinforced" && coworkers.Count < 2;
        string? missing = target.Tools != null && Data.Recipes[target.Recipe] is var needed && needed.Tool != "" && BenchTool(target, needed.Tool, needed.Tier) == null ? Data.ToolName(needed.Tool, needed.Tier) : null;
        w.Status = pumps ? "Pumping bellows between loads" : target.Kind == "reinforced" ? "Waiting for the next load" : missing != null ? $"Needs {A(missing)} in the {Data.Name(target.Kind).ToLowerInvariant()}" : "Working at station";
        if (Walk(w, target.Pos, dt))
        {
            if (pumps) { w.WorkState = WorkerWorkState.Pumping; target.Heat = Math.Min(100, target.Heat + dt * 16); }
            else if (Worked(target)) w.WorkState = WorkerWorkState.Hammering;
        }
    }
    /// <summary>Walks a tool the job no longer needs to the nearest tool station with room, and reports when it is put back; with no such station the worker keeps it.</summary>
    private bool ReturnTool(Worker w, float dt)
    {
        var station = S.Things.Where(t => t.Kind == "toolstation" && t.Inventory.Room(w.Tool) > 0).MinBy(t => t.Pos.DistanceSquaredTo(w.Pos));
        if (station == null) return true;
        w.Status = $"Putting back the {Data.Name(w.Tool).ToLowerInvariant()}";
        if (!Walk(w, station.Pos, dt)) return false;
        station.Inventory.Add(w.Tool, 1); w.Tool = ""; return true;
    }
    /// <summary>Takes the best tool of a kind from the nearest tool station holding one; without one, the worker says once what they need and waits.</summary>
    private void FetchTool(Worker w, string kind, Thing target, float dt)
    {
        var station = S.Things.Where(t => t.Kind == "toolstation" && BestTool(kind, t.Inventory) != null).MinBy(t => t.Pos.DistanceSquaredTo(w.Pos));
        if (station == null)
        {
            w.Status = $"Needs {A(kind)} to {Task(target)}";
            if (w.Missing != kind) { w.Missing = kind; Say($"{w.Name} needs {A(kind)} to {Task(target)}. Put one in a tool station."); }
            return;
        }
        w.Missing = "";
        w.Status = $"Fetching {A(kind)} from the tool station";
        if (!Walk(w, station.Pos, dt)) return;
        w.Tool = BestTool(kind, station.Inventory)!; station.Inventory.Remove(w.Tool, 1);
    }
    /// <summary>Walks to an evenly spaced seat around the lunch fire and sits; without a campfire the worker sits where they are.</summary>
    private void EatLunch(Worker w, float dt)
    {
        w.Status = "Lunch break";
        if (LunchFire is not Thing fire) { w.WorkState = WorkerWorkState.Resting; return; }
        var circle = S.Workers.Where(o => !o.Home && o.Away <= 0).ToList();
        float angle = circle.IndexOf(w) * Mathf.Tau / circle.Count;
        w.Status = "Lunch break by the campfire";
        if (Walk(w, fire.Pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.4f, dt, .1f)) w.WorkState = WorkerWorkState.Resting;
    }
    public void PayWages()
    {
        foreach (var w in S.Workers)
        {
            // A striking worker does not work, so no further wages are added until the strike is settled.
            if (w.WageDebt > 0) continue;
            if (!PayWage(w.Wage)) { float due = w.WageDebt = w.Wage; Say($"{w.Name} is on strike: {due:0} coins owed. Pay or dismiss them at the signpost."); }
        }
    }
    public void SettleDebt(Worker w)
    {
        if (Forwarded("settle", other: w.Id)) return;
        if (!PayWage((int)w.WageDebt)) { Say("Not enough coins in the wage slot or your backpack to settle wages."); return; }
        w.WageDebt = 0;
    }
    /// <summary>The wolves hunting the acting player.</summary>
    private IEnumerable<Wolf> Hunters => S.Wolves.Where(w => w.Target == S.Actor.Id);
    /// <summary>
    /// Advances the world. It runs as the world's own player, whoever's action started it, such as a guest's sleep:
    /// wages come out of that player's backpack, and each player's own part runs as that player.
    /// </summary>
    public void Tick(float dt, bool sleeping = false) => As(S.Players[0], () => TickWorld(dt, sleeping));
    private void TickWorld(float dt, bool sleeping)
    {
        bool wasDusk = Dusk, wasLunch = Lunch;
        everyone = true;
        S.Time += dt;
        if (!sleeping) S.PlaySeconds += dt;
        foreach (var wolf in S.Wolves.Where(w => S.Players.All(p => p.Id != w.Target))) wolf.Target = S.Players.MinBy(p => p.Pos.DistanceSquaredTo(wolf.Pos))!.Id;
        foreach (var p in S.Players.ToArray()) As(p, () => NeedsTick(dt));
        if (Day > S.WageDay)
        {
            // Workers on strike are not charged, so their wage does not count against the day.
            int wages = S.Workers.Where(w => w.WageDebt <= 0).Sum(w => w.Wage);
            PayWages(); CloseDay(wages); S.WageDay = Day; Say($"Day {Day}. Wages settled where funds allow.");
            foreach (var leaves in S.Things.Where(t => t.Kind == "leaves")) leaves.Remaining = LeafPatch;
        }
        if (S.Workers.Count > 0 && Dusk && !wasDusk) Say("Dusk. Your workers head home to Bramblewick until morning.");
        if (S.Workers.Count > 0 && Lunch && !wasLunch) Say("Midday. Your workers gather by the campfire for an hour of lunch.");
        // A day restores what selling the daily amount takes, so selling it every day keeps the price steady.
        foreach (var item in S.Demand.Keys.ToArray())
        {
            if (!Data.Market.Sells.TryGetValue(item, out var demand)) continue;
            S.Demand[item] = Mathf.MoveToward(S.Demand[item], FullDemand(item), dt * demand.Daily * DemandStep(item) / Data.DaySeconds);
        }
        foreach (var order in S.Orders.ToArray())
        {
            order.Elapsed += dt; if (order.Elapsed < Data.TradeSeconds) continue;
            // The cart unloads into the market output; whatever does not fit waits in a box beside the signpost.
            var sign = Find(order.Signpost);
            var output = sign?.Output ?? S.Backpack;
            var goods = order.Buying ? order.Goods : new() { [Inventory.Coins] = (int)order.Value - (sign?.Wages?.Add(Inventory.Coins, (int)order.Value) ?? 0) };
            // Boxed goods come back as packed crates of one purchase each.
            if (order.Buying && order.Crates > 0)
                goods = order.Goods.SelectMany(p => Enumerable.Range(0, order.Crates).Select(_ => { var box = NewCrate(); box.Add(p.Key, p.Value / order.Crates); return Pack(box); })).ToDictionary(id => id, _ => 1);
            foreach (var (item, quantity) in goods)
            {
                int overflow = quantity - output.Add(item, quantity);
                if (overflow <= 0) continue;
                var delivery = Add("grave", sign?.Pos + Vector2.One ?? Spawn);
                delivery.Inventory.Slots = overflow / delivery.Inventory.StackOf(item) + 1;
                delivery.Inventory.Add(item, overflow);
            }
            if (!order.Buying && order.Goods.Keys.Any(id => id.StartsWith("steel_") && Data.Items[id].Tool != "")) S.Milestones.Add("sold_steel");
            // Played at the player, not the signpost, so a payment is heard wherever the player is.
            if (!order.Buying) { Play("payment_from_village_arrives", Player); S.DayRevenue += (int)order.Value; }
            S.Orders.Remove(order); UpdatePrices();
            Say(order.Buying ? $"Cart returned with {Goods(order.Goods)}." : $"Cart returned with {(int)order.Value} coins.");
        }
        S.Things.RemoveAll(t => t.Kind == "grave" && t.Inventory.Count == 0 || t.Kind == "wolf_corpse" && t.Remaining <= 0);
        foreach (var t in S.Things.ToArray())
        {
            // A carcass keeps its age so the death animation plays once and not again after loading.
            if (t.Kind == "wolf_corpse") t.Progress = Math.Min(60, t.Progress + dt);
            if (t.Kind == "mine") MineTick(t, dt);
            if (t.Kind == "pile") { PileFilters(t); if (t.Compost) CompostTick(t, dt); }
            if (t.Kind == "farm") FarmTick(t, dt);
            if (t.Kind == "berries" && t.Remaining == 0) { t.Regrow += dt; if (t.Regrow >= Data.BerryRegrowSeconds) { t.Remaining = 24; t.Regrow = 0; } }
            if (t.Kind == "campfire")
            {
                t.Burn = Math.Max(0, t.Burn - dt);
                if (t.Burn < 30 && t.Inventory.Remove("wood", 1) > 0) { if (t.Burn <= 0) Play("start_fire", t.Pos); t.Burn += Data.CampfireSeconds; }
            }
            if (t.Kind == "reinforced")
            {
                // Once a working crew has the forge hot, they keep it at or above the clear bar between strokes.
                // A crew that is striking or stuck on a blocked path is not working, so the forge cools.
                // So is a crew that has signed out or is at lunch.
                bool crewed = S.Workers.Any(w => w.Assignment == t.Id && w.WageDebt <= 0 && w.WorkState != WorkerWorkState.Blocked && OnDuty(w));
                t.Heat = Math.Max(crewed && t.Heat >= SteelHeat ? SteelHeat : 0, t.Heat - dt * 3.2f);
            }
            if (!Data.Recipes.TryGetValue(t.Recipe, out var recipe)) continue;
            bool burning = stillBurning.Remove(t.Id);
            bool hot = t.Kind != "reinforced" || t.Heat >= SteelHeat;
            bool atAnvil = !Worked(t) || Hammered(t);
            // A recipe at a station with tool slots needs its tool slotted, and the tool's speed sets the pace.
            string? tool = t.Tools != null && recipe.Tool != "" ? BenchTool(t, recipe.Tool, recipe.Tier) : null;
            if (t.Tools != null && recipe.Tool != "" && tool == null) atAnvil = false;
            float rate = (t.Kind == "bench" ? Data.BenchSpeed : 1) * Speed(tool);
            if (!t.Processing && hot && atAnvil && t.Inventory.Has(BatchCost(t)))
            {
                t.Inventory.Spend(BatchCost(t)); t.Processing = true; t.Progress = 0;
                if (recipe.Fuel && !burning) Play("start_fire", t.Pos);
            }
            if (t.Processing && !hot)
            {
                // A cold forge mid-batch keeps burning coal from its slot at the batch fuel rate, but the steel waits for heat.
                // Burn is the campfire's fuel clock, reused here to time the next coal.
                t.Burn = Math.Min(recipe.Seconds, t.Burn + dt);
                if (t.Burn >= recipe.Seconds && t.Inventory.Remove("coal", 1) > 0) t.Burn = 0;
            }
            if (t.Processing && hot && atAnvil)
            {
                t.Progress = Math.Min(recipe.Seconds, t.Progress + dt * rate);
                if (t.Progress >= recipe.Seconds)
                {
                    // Manual loading or another hauler can fill the station while a batch cooks.
                    // Retain the completed batch until the one-stack output slot has room rather than discarding output.
                    if (Math.Min(t.Inventory.Room(recipe.Output), t.Inventory.StackSize - t.Inventory[recipe.Output]) < recipe.Amount) continue;
                    t.Inventory.Add(recipe.Output, recipe.Amount); t.Processing = false; t.Progress = 0; stillBurning.Add(t.Id);
                    Crafted(recipe.Output);
                }
            }
        }
        foreach (var p in S.Players.ToArray()) As(p, () => CraftTick(dt));
        UpdateLunchFire();
        foreach (var w in S.Workers.ToArray()) WorkerTick(w, dt);
        for (int i = 0; i < S.Workers.Count; i++) for (int j = i + 1; j < S.Workers.Count; j++)
        {
            var a = S.Workers[i]; var b = S.Workers[j]; var offset = a.Pos - b.Pos;
            // Miners underground are inside the hill, so they never crowd anyone at the entrance.
            if (!a.Home && !b.Home && a.WorkState != WorkerWorkState.Underground && b.WorkState != WorkerWorkState.Underground && offset.LengthSquared() < .64f)
            {
                var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
                if (CanStand(a.Pos + push)) a.Pos += push;
                if (CanStand(b.Pos - push)) b.Pos -= push;
            }
        }
        if (!Night) S.Wolves.Clear();
        foreach (var p in S.Players.ToArray())
            As(p, () =>
            {
                if (!Night) { nightSpawn = 0; if (!Dead) S.Mauled = false; }
                else if (!sleeping) TickWolves(dt, S.Wolves.Where(w => w.Target == p.Id).ToList());
                if (!Dead && S.Health <= 0) Die();
                playerBusy = false;
            });
        everyone = false;
        Look();
        UpdateGoal();
    }
    /// <summary>The acting player's clocks, hunger and thirst, and the torch in hand; a remote player's held gather also runs here.</summary>
    private void NeedsTick(float dt)
    {
        attackClock = Math.Max(0, attackClock - dt);
        if (S.Actor != Me && Find(S.Actor.GatherTarget) is { IsLoose: false } held) Gather(held, dt);
        if (Dead) { S.RespawnIn = Math.Max(0, S.RespawnIn - dt); if (!Dead) Respawn(); }
        else
        {
            S.Hunger = Math.Max(0, S.Hunger - dt * Data.HungerPerSecond); S.Thirst = Math.Max(0, S.Thirst - dt * Data.ThirstPerSecond);
            if (S.Hunger <= 0 || S.Thirst <= 0) S.Health -= dt * 1.4f;
            else if (S.Hunger > 45 && S.Thirst > 45) S.Health = Math.Min(100, S.Health + dt * .12f);
        }
        TorchFlare = Math.Max(0, TorchFlare - dt);
        if (S.TorchKindling > 0 && (S.TorchKindling -= dt) <= 0)
        {
            S.TorchKindling = 0;
            if (!Downed && S.Torch > 0) { S.TorchLit = true; Play("start_fire", Player); Say($"Torch lit: {S.Torch / Data.DaySeconds * 24:0.#} hours of light left."); }
        }
        if (S.TorchLit && (S.Torch -= dt) <= 0) { S.Torch = 0; PutOutTorch(); Say("Your torch burned out."); }
    }
    /// <summary>The campfire the crew sits around: the one closest to them all, during lunch.</summary>
    internal void UpdateLunchFire()
    {
        var present = S.Workers.Where(w => !w.Home && w.Away <= 0).ToList();
        LunchFire = Lunch && present.Count > 0 ? S.Things.Where(t => t.Kind == "campfire").MinBy(t => present.Sum(w => w.Pos.DistanceTo(t.Pos))) : null;
    }
    /// <summary>Explores the tiles in every player's sight and replaces the memory of everything there with its current state.</summary>
    public void Look() { foreach (var p in S.Players) As(p, LookAround); }
    private void LookAround()
    {
        float radius = Data.SightRadius;
        int fromX = Math.Max(0, (int)(Player.X - radius)), toX = Math.Min(MapSize - 1, (int)(Player.X + radius) + 1);
        int fromY = Math.Max(0, (int)(Player.Y - radius)), toY = Math.Min(MapSize - 1, (int)(Player.Y + radius) + 1);
        for (int y = fromY; y <= toY; y++)
            for (int x = fromX; x <= toX; x++)
                if (S.Explored[y * MapSize + x] == 0 && Sees(new Vector2(x, y))) { S.Explored[y * MapSize + x] = 255; ExploredRevision++; }
        foreach (var id in S.Remembered.Where(pair => Sees(pair.Value.Pos) && Find(pair.Key) == null).Select(pair => pair.Key).ToArray()) S.Remembered.Remove(id);
        // Only the look is remembered: no inventory, and no fire, heat or work, so nothing out of sight smokes or glows.
        foreach (var t in S.Things.Where(t => Sees(t.Pos)))
        {
            if (!S.Remembered.TryGetValue(t.Id, out var memory)) S.Remembered[t.Id] = memory = new Thing { Id = t.Id };
            memory.Kind = t.Kind; memory.Pos = t.Pos; memory.Remaining = t.Remaining; memory.Recipe = t.Recipe; memory.Progress = t.Progress; memory.Stage = t.Stage;
        }
    }
    public bool Explored(Vector2 p) => p.X >= 0 && p.Y >= 0 && p.X < MapSize && p.Y < MapSize && S.Explored[(int)p.Y * MapSize + (int)p.X] != 0;
    /// <summary>Moves the pack hunting the acting player.</summary>
    private void TickWolves(float dt, List<Wolf> hunters)
    {
        if (!Dead)
        {
            nightSpawn -= dt;
            // A torch draws the whole pack in, but slowly.
            if (nightSpawn <= 0 && hunters.Count < (S.TorchLit ? 6 : Math.Min(6, 2 + Day)))
            {
                nightSpawn = S.TorchLit ? 25 : 15; float angle = S.Time * 2.4f;
                // Wolves come out of the woods, never the sea or the mountains: turn until open ground is found.
                var p = Player;
                for (int turn = 0; turn < 12 && !Open(p); turn++) p = Player + new Vector2(MathF.Cos(angle + turn * .52f), MathF.Sin(angle + turn * .52f)) * 11;
                if (p == Player) p += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 11;
                var wolf = new Wolf { Pos = p, Health = 4 + Day * .4f, Target = S.Actor.Id }; S.Wolves.Add(wolf); hunters.Add(wolf);
                Say("A wolf is prowling nearby. Stay by fire or shelter; Space or a click with an axe in hand swings it."); Play("wolf_growl_distance", p);
            }
        }
        bool lit = Protected(Player), inside = S.Sheltered != 0;
        // Behind a torch a lone wolf creeps in until a swing scares it off; once the pack is complete they attack together.
        bool torch = Torchlit, pack = torch && PackAround >= PackSize, wary = torch && !pack;
        if (pack && !packAttack) Say("The pack has you surrounded. They attack!");
        packAttack = pack;
        // A wounded player away from fire and shelter invites a leap for the throat.
        if (!Downed && S.Health <= 50 && !lit && !inside && !wary && hunters.All(w => w.Leap <= 0))
        {
            var hunter = hunters.Where(w => !Protected(w.Pos) && w.Flee <= 0 && w.Pos.DistanceTo(Player) < LeapRange).OrderBy(w => w.Pos.DistanceTo(Player)).FirstOrDefault();
            if (hunter != null) { hunter.Leap = LeapSeconds; Say("A wolf leaps for your throat!"); }
        }
        float speed = Math.Min(3.8f, 3 + Day * .1f);
        foreach (var wolf in hunters)
        {
            wolf.Attack -= dt;
            if (wolf.Leap > 0)
            {
                // Closing the remaining gap evenly makes every leap last LeapSeconds.
                var landing = Player + (wolf.Pos - Player).Normalized() * PinDistance;
                wolf.Pos = wolf.Pos.MoveToward(landing, wolf.Pos.DistanceTo(landing) * Math.Min(1, dt / wolf.Leap));
                wolf.Leap = Math.Max(0, wolf.Leap - dt);
                if (wolf.Leap == 0) { wolf.Feeding = wolf.Pinning = true; S.Mauled = true; Say(hunters.Count > 1 ? "You are pinned. The pack closes in." : "You are pinned."); Play("wolf_bite", Player); }
            }
            else if (S.Mauled)
            {
                if (wolf.Feeding) continue;
                var seat = Player + (wolf.Pos - Player).Normalized() * FeastSeat;
                wolf.Pos = wolf.Pos.MoveToward(seat, dt * FeastSpeed);
                wolf.Feeding = wolf.Pos.DistanceTo(seat) < .05f;
            }
            else if (wolf.Flee > 0)
            {
                wolf.Flee -= dt; wolf.Chase = 0;
                wolf.Pos = wolf.Pos.MoveToward(wolf.Pos + (wolf.Pos - Player).Normalized() * 3, dt * speed);
            }
            else if (wary && wolf.Scared)
            {
                // Each scared wolf keeps its bearing at the edge of the torchlight, so a growing pack surrounds the player.
                wolf.Chase = 0;
                var away = wolf.Pos == Player ? Vector2.Right : (wolf.Pos - Player).Normalized();
                wolf.Pos = wolf.Pos.MoveToward(Player + away * StalkDistance, dt * speed * .6f);
                if (!wolf.Growled && wolf.Pos.DistanceTo(Player) < 6) { wolf.Growled = true; Play("wolf_growl_approaching", wolf.Pos); }
            }
            else if (wary)
            {
                wolf.Chase = 0;
                wolf.Pos = wolf.Pos.MoveToward(Player, dt * speed * .35f);
                if (!wolf.Growled && wolf.Pos.DistanceTo(Player) < 6) { wolf.Growled = true; Play("wolf_growl_approaching", wolf.Pos); }
                if (wolf.Pos.DistanceTo(Player) < 1.1f && wolf.Attack <= 0) { wolf.Attack = 1; S.Health -= 12 + Day; Play("wolf_bite", Player); }
            }
            // A wolf already on the player's heels follows briefly into firelight, but backs off from a cabin at once.
            else if (inside || (lit || Protected(wolf.Pos)) && wolf.Chase <= 0) { wolf.Chase = 0; wolf.Pos = wolf.Pos.MoveToward(wolf.Pos + (wolf.Pos - Player).Normalized() * 3, dt * 3); }
            else
            {
                wolf.Chase = lit || Protected(wolf.Pos) ? wolf.Chase - dt : ChaseSeconds;
                wolf.Pos = wolf.Pos.MoveToward(Player, dt * speed);
                if (!wolf.Growled && wolf.Pos.DistanceTo(Player) < 6) { wolf.Growled = true; Play("wolf_growl_approaching", wolf.Pos); }
                if (wolf.Pos.DistanceTo(Player) < 1.1f && wolf.Attack <= 0) { wolf.Attack = 1; S.Health -= 12 + Day; Play("wolf_bite", Player); }
            }
        }
        // Feeding wolves crowd around the player without stacking on one spot.
        for (int i = 0; i < hunters.Count; i++) for (int j = i + 1; j < hunters.Count; j++)
        {
            var a = hunters[i]; var b = hunters[j]; var offset = a.Pos - b.Pos;
            if (a.Leap > 0 || b.Leap > 0 || offset.LengthSquared() >= .5f) continue;
            var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
            a.Pos += push; b.Pos -= push;
        }
        if (S.Mauled && !Dead) S.Health -= dt * (16 + 8 * hunters.Count(w => w.Feeding));
    }
    private static readonly string[] Milestones = { "gather", "furnace", "iron_bar", "anvil", "bench", "pick", "reinforced", "steel_bar", "steel_tool", "sold_steel", "hired", "assigned" };
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
        2 => ("The first iron", "Explore for a rust-streaked cliff and pick up the loose ore below it. Load the furnace with ore + wood. Collect iron bars."),
        3 => ("A proper workshop", "Build an anvil: 4 iron bars + 6 stone. Keep smelting for a pickaxe and bellows furnace."),
        4 => ("A workbench", "Press C to craft. Knap a stone chisel from 2 stone, split wood into planks, then build a crafting bench: 10 planks."),
        5 => ("Beneath the surface", "Hammer 2 iron parts at the anvil. Make a wooden mallet by hand, slot it in the bench and craft an iron pickaxe: 2 iron parts + 1 wood parts."),
        6 => ("Hotter than fire", "Explore for a dark cliff and mine coal at its foot. Build a bellows furnace: 20 stone, 5 iron bars, 8 wood."),
        7 => ("Make steel", "Load iron + mined coal into the bellows furnace. Keep the heat above the clear bar; steel only progresses there. Each bar costs 2 iron + 3 coal incl. fuel."),
        8 => ("Tools for the village", "Hammer steel parts at the anvil, then craft a steel axe, pickaxe or hammer at the bench."),
        9 => ("The road to Bramblewick", "Build a village signpost. Craft a crate at the bench (6 planks, hammer slotted), place it, pack your steel tools, pick it up and put it in the market. Sell, then collect the coins."),
        10 => ("Many hands", "At the signpost pick a hiring offer: more coins up front means a lower daily wage. Keep wages in your backpack for each dawn."),
        11 => ("Your first automation", "Build storage, and a tool station holding a spare axe or pickaxe for woodcutters and miners. Select a worker → Work → click a resource. Or select a furnace → Assign."),
        _ => ("A settlement of your own", "The starter goals are done. Work toward the end goals: a mine on every ore and a week of steady profit.")
    };
    // ---------- End goals ----------
    /// <summary>Ores found in the valley's cliffs; the end goal wants a working mine on each.</summary>
    public static IEnumerable<string> MineOres => Cliffs.Select(c => c.Ore).Distinct();
    public bool HasMine(string ore) => S.Things.Any(t => t.Kind == "mine" && t.Stage == MineOpen && CliffAt(t.Pos).Ore == ore);
    /// <summary>Finished days in a row, up to today, whose profit reached the goal.</summary>
    public int ProfitStreak => S.Profits.AsEnumerable().Reverse().TakeWhile(p => p >= Data.Goals.DailyProfit).Count();
    /// <summary>Once reached, the week of profit stays reached even if later days fall short.</summary>
    public bool ProfitGoalMet => S.Milestones.Contains("profit_week");
    public bool EndGoalsComplete => MineOres.All(HasMine) && ProfitGoalMet;
    private void CloseDay(int wages)
    {
        S.Profits.Add(S.DayRevenue - wages); S.DayRevenue = 0;
        if (ProfitStreak >= Data.Goals.ProfitDays) S.Milestones.Add("profit_week");
    }
    public string Serialize() => JsonSerializer.Serialize(S, GameData.Json);
    public void Deserialize(string json)
    {
        // Steel tools became steel parts; carried ones that were the only axe or pickaxe become those tools so the save stays playable.
        bool legacyTools = json.Contains("\"steel_tools\"");
        json = json.Replace("\"steel_tools\"", "\"steel_parts\"").Replace("\"cooked_meat\"", "\"burnt_steak\"");
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
        if (loaded.Explored?.Length != MapSize * MapSize) loaded.Explored = new byte[MapSize * MapSize];
        loaded.Remembered ??= new();
        if (loaded.Version == 4) AlignLegacyMines(loaded);
        loaded.Crates ??= new(); loaded.Crafting ??= new(); loaded.Profits ??= new();
        loaded.Crafting.RemoveAll(id => !Data.Recipes.TryGetValue(id, out var r) || !r.Hand);
        foreach (var t in loaded.Things)
        {
            // Anvils of older saves may still be set to tools that are now made at the bench.
            if (t.Recipe != "" && (!Data.Recipes.TryGetValue(t.Recipe, out var r) || r.Station != t.Kind)) { t.Recipe = ""; t.Processing = false; t.Progress = 0; }
            if (SlotKinds(t.Kind).Length > 0) t.Tools ??= NewToolSlots(t.Kind);
        }
        if (legacyTools)
        {
            foreach (var (tool, made) in new[] { ("axe", "steel_axe"), ("pickaxe", "steel_pick") })
                if (BestTool(tool, loaded.Backpack) == null && loaded.Backpack.Remove("steel_parts", 1) == 1) loaded.Backpack.Add(made, 1);
            if (loaded.Milestones.Contains("steel_parts")) loaded.Milestones.Add("steel_tool");
        }
        // The goal chain gained steps, so the current goal is worked out again from the milestones.
        loaded.Goal = 0;
        S = loaded;
        // Saves from before raking have no leaf litter.
        if (!S.Things.Any(t => t.Kind == "leaves")) DropLeaves();
        UpdateGoal(); gatherClock = 0; ExploredRevision++; Look(); Say("Settlement loaded.");
    }
}
