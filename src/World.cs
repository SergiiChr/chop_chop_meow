using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

public sealed class Thing {
    public int Id { get; init; }
    public ThingKind Kind { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>A building's turn in radians from its original direction, positive from X toward Y on the ground.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float Rotation { get; init; }
    public int Remaining { get; set; }
    public Inventory Inventory { get; set; } = new();
    /// <summary>A station's recipe now: the batch in progress, else <see cref="Asked"/> while batches of it wait, else <see cref="Keep"/>; empty when it has nothing to make.</summary>
    [JsonIgnore] public string Recipe => Making != "" ? Making : NextRecipe;
    /// <summary>The next batch whose ingredients can still be supplied.</summary>
    internal string NextRecipe => Batches > 0 ? Asked : Keep;
    /// <summary>The recipe of a station's batch in progress, or empty between batches.</summary>
    [JsonIgnore] public string Making { get => making ?? ""; set => making = value == "" ? null : value; }
    /// <summary>The recipe workers keep a station making once the batches asked of it are done, or empty. A marked station is on its marked recipe: asking for batches marks their recipe.</summary>
    [JsonIgnore] public string Keep { get => keep ?? ""; set => keep = value == "" ? null : value; }
    /// <summary>The one recipe a player asked a station for batches of last, or the one it lost its mark on since; a station with nothing to make stays on it.</summary>
    [JsonIgnore] public string Asked { get => asked ?? ""; set => asked = value == "" ? null : value; }
    /// <summary>How many batches of <see cref="Asked"/> still wait; a batch leaves the count as it starts.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Batches { get; set; }
    private string? making, keep, asked;
    // Saves and updates carry these only when set, so a tree or a rock carries none of them.
    [JsonInclude, JsonPropertyName("Recipe"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] internal string? SavedMaking { get => making; set => Making = value ?? ""; }
    [JsonInclude, JsonPropertyName("Keep"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] internal string? SavedKeep { get => keep; set => Keep = value ?? ""; }
    [JsonInclude, JsonPropertyName("Asked"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] internal string? SavedAsked { get => asked; set => Asked = value ?? ""; }
    public float Progress { get; set; }
    public bool Processing { get; set; }
    /// <summary>Units of work finished here since the world loaded: batches made, pieces mined, harvests and building stages; a worker reads their job list again after each.</summary>
    [JsonIgnore] public int Rounds { get; set; }
    public float Heat { get; set; }
    /// <summary>Pressure built up at a water pump, from empty at zero to full at 100.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float Pressure { get; set; }
    /// <summary>Water the pump can supply at its current pressure, in liters per second.</summary>
    [JsonIgnore] public float FlowRate => Pressure * .05f;
    /// <summary>Seconds remaining in the stroke before the handle stops and pressure begins to drop.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float PumpStrokeLeft { get; set; }
    /// <summary>Whether the plumbing network is supplying water to this thing.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool ReceivingWater { get; set; }
    /// <summary>Liters supplied through connected plumbing below one whole liter, carried until the next delivery.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float PipedWaterRemainder { get; set; }
    public float Burn { get; set; }
    /// <summary>When a picked berry bush fruits again, on the world's clock, or zero while it is full.</summary>
    public float Regrow { get; set; }
    /// <summary>The id of the tree that left a stump, whose species and size shape the stump; zero for a stump with no tree on record, which is shaped by its own id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OfTree { get; set; }
    /// <summary>The way a stump's tree fell across the ground, away from whoever landed the last blow on it, in radians from the x axis toward y.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float FellToward { get; set; }
    /// <summary>A signpost's market output: bought goods arrive here with the returning cart.</summary>
    public Inventory? Output { get; set; }
    /// <summary>A signpost's coins for wages, with no limit: sale coins land here, and dawn wages come out of it before any backpack.</summary>
    public Inventory? Wages { get; set; }
    /// <summary>A fuel-burning station's one stack of fuel, apart from what it smelts or cooks; what it holds is what it burns.</summary>
    public Inventory? Burner { get; set; }
    /// <summary>Tools fitted to a workstation, such as the anvil’s hammer or the cooking table’s mortar.</summary>
    public Inventory? Tools { get; set; }
    /// <summary>The pipes fitted to a building that takes water from underground.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Inventory? PlumbingSlot {
        get => Plumbing.HasPlumbing(Kind) ? field ??= new Inventory { Slots = 1, StackSize = Plumbing.InstallCost, Capacity = Plumbing.InstallCost } : field;
        set;
    }
    [JsonIgnore] public bool PlumbingInstalled => PlumbingSlot?["steel_pipe"] == Plumbing.InstallCost;
    /// <summary>Installed plumbing last seen on an explored building, without remembering its inventory or live flow.</summary>
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] internal bool RememberedPlumbing { get; set; }
    internal bool ShowsPlumbing => RememberedPlumbing || PlumbingInstalled;
    /// <summary>A cabin's door locked from inside with its key, which keeps out players and wolves; only a cabin with someone inside is locked.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Locked { get; set; }
    /// <summary>How far a mine is built.</summary>
    public MineStage MineStage { get; set; }
    /// <summary>Where a farm plot is in its growing cycle.</summary>
    public FarmStage FarmStage { get; set; }
    /// <summary>Growth seconds left in the liter already in use by the plot.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float FarmWaterLeft { get; set; }
    /// <summary>A pile turned into compost: it takes leaves, grass and food and slowly turns them into fertilizer.</summary>
    public bool Compost { get; set; }
    /// <summary>A compost that workers bring no food to; food put in by hand still rots into fertilizer.</summary>
    public bool SkipFood { get; set; }
    /// <summary>The good a packing station packs into crates, or empty until one is chosen.</summary>
    public string Packs { get; set; } = "";
    /// <summary>A trading post that sends the cart by itself once every market slot holds a crate.</summary>
    public bool AutoSell { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
    /// <summary>Where a point measured from the middle of this thing lies on the ground.</summary>
    public Vector2 ToWorld(Vector2 offset) => Pos + offset.Rotated(Rotation);
    [JsonIgnore] public bool IsResource => GatherRules.Of(Kind) != null;
    /// <summary>Ore at the foot of a cliff, mined with a pickaxe.</summary>
    [JsonIgnore] public bool IsDeposit => GatherRules.Of(Kind)?.Shape == GatherShape.Deposit;
    /// <summary>Loose pieces are picked up whole in one go and vanish, instead of being worked down like a deposit.</summary>
    [JsonIgnore] public bool IsLoose => GatherRules.Of(Kind)?.Shape == GatherShape.Loose;
    /// <summary>A stump or a bush: digging adds up in <see cref="Progress"/> until it comes out of the ground whole.</summary>
    [JsonIgnore] public bool IsDug => GatherRules.Of(Kind)?.Shape == GatherShape.Dug;
    [JsonIgnore] public bool IsStorage => Kind is ThingKind.Chest or ThingKind.Grave or ThingKind.Crate or ThingKind.Toolstation or ThingKind.Pile or ThingKind.Barrel;
}
/// <summary>Batches of one recipe waiting in the hand's queue.</summary>
public sealed class Queued {
    public string Recipe { get; set; } = "";
    public int Count { get; set; }
}
/// <summary>
/// Reads and writes the hand's queue as an array of its entries with a plain loop.
/// Every player's entry in a world update carries a queue, and the generic list writer allocates on each call.
/// </summary>
public sealed class QueueJson : JsonConverter<List<Queued>> {
    private static readonly JsonEncodedText RecipeName = JsonEncodedText.Encode(nameof(Queued.Recipe)), CountName = JsonEncodedText.Encode(nameof(Queued.Count));

    public override List<Queued> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("A queue is an array.");
        var queue = new List<Queued>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A queue entry is an object.");
            var q = new Queued();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("A queue entry holds names and values.");
                bool recipe = reader.ValueTextEquals(RecipeName.EncodedUtf8Bytes), count = reader.ValueTextEquals(CountName.EncodedUtf8Bytes);
                reader.Read();
                if (recipe) q.Recipe = reader.GetString() ?? "";
                else if (count) q.Count = reader.GetInt32();
                else reader.Skip();
            }
            queue.Add(q);
        }
        return queue;
    }

    public override void Write(Utf8JsonWriter writer, List<Queued> value, JsonSerializerOptions options) {
        writer.WriteStartArray();
        for (int i = 0; i < value.Count; i++) {
            writer.WriteStartObject();
            writer.WriteString(RecipeName, value[i].Recipe); writer.WriteNumber(CountName, value[i].Count);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}
public enum WorkerWorkState { Idle, Gathering, Pumping, Hammering, Blocked, Resting, Digging, Underground, Perching }

public sealed class Worker : IWalker {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>The place of the job in hand, or of their first job while none can be done.</summary>
    public int Assignment { get; set; }
    /// <summary>The job in hand at <see cref="Assignment"/>, or null while none can be done.</summary>
    [JsonIgnore] public JobKind? Doing { get; set; }
    /// <summary>The worker's jobs and haul groups in the order they come first: the first that can be done is the one in hand.</summary>
    public List<JobEntry> Jobs { get; set; } = new();
    /// <summary>Every job in <see cref="Jobs"/>, the hauls of each group included.</summary>
    [JsonIgnore] public DutyList Duties => new(Jobs);
    public string Status { get; set; } = "Available";
    /// <summary>Items a worker carries in all, as many as a wood crate holds.</summary>
    public const int BagStack = 12;
    /// <summary>A worker's hands hold <see cref="BagStack"/> items like a crate: one stack of them or that many different items.</summary>
    public Inventory Bag { get; set; } = new() { Slots = BagStack, StackSize = BagStack, Capacity = BagStack, CarriedBy = Inventory.Carrier.Worker };
    /// <summary>Still filling their hands at a gathering spot or a water source, so what they hold is not carried off yet.</summary>
    [JsonIgnore] public bool Loading { get; set; }
    /// <summary>The entry the worker keeps to until its round ends, a delivery, a crafted batch or another unit of its work; null while they keep to none.</summary>
    [JsonIgnore] public JobEntry? InHand { get; set; }
    /// <summary>The rounds the place of the job in hand had counted when the worker took the job up.</summary>
    [JsonIgnore] public int Mark { get; set; }
    /// <summary>The carry a hauler is on the way to pick up.</summary>
    internal Carry Plan;
    /// <summary>The stand chosen for a water haul and, when filling a vessel, its source.</summary>
    internal (Thing Target, Thing? Source, Vector2 Stand)? WaterWalk;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public WorkerWaterAction WaterAction { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int WaterTarget { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float WaterProgress { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int FarmWaterStop { get; set; }

    /// <summary>What the first job lacked when the worker last found nothing to do, and when on the world's clock they read their list again; a change to the list reads it at once.</summary>
    [JsonIgnore] public string? Lacking { get; set; }
    [JsonIgnore] public float ReadAgain { get; set; }
    /// <summary>The bag holds fuel for the destination's burner rather than ingredients.</summary>
    public bool Fueling { get; set; }
    /// <summary>The slot for the one tool a worker holds: taken from a tool station for the job in hand, or put there by a player.</summary>
    public Inventory ToolSlot { get; set; } = new() { Slots = 1, StackSize = 1 };
    /// <summary>The tool in <see cref="ToolSlot"/>, or empty; it goes back to a tool station when the job in hand has no use for it.</summary>
    [JsonIgnore] public string Tool { get => ToolSlot.First() ?? ""; set { ToolSlot.Clear(); if (value != "") ToolSlot.Add(value, 1); } }
    /// <summary>The tool kind the worker last reported missing, so the message is said once rather than every tick.</summary>
    public string Missing { get; set; } = "";
    /// <summary>The kind of resource the worker last said they know no more of.</summary>
    [JsonIgnore] public ThingKind? Exhausted { get; set; }
    public int Destination { get; set; }
    public float Timer { get; set; }
    public float WageDebt { get; set; }
    /// <summary>Coins due at each dawn, fixed by the hiring offer.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int Wage { get; set; } = 8;
    /// <summary>Seconds until the worker returns from fetching village prices; while away they are off the map.</summary>
    public float Away { get; set; }
    /// <summary>Signed out for the night: off the map in the village until dawn, still holding their assignment and anything carried.</summary>
    public bool Home { get; set; }
    /// <summary>Seconds into the walk down the valley at dusk, counted back down on the way in at dawn; the view fades the worker with it.</summary>
    public float Commute { get; set; }
    [JsonIgnore] public List<Vector2> Route { get; } = new();
    [JsonIgnore] public Vector2 RouteTarget { get; set; } = new(-100, -100);
    [JsonIgnore] public float RouteRetry { get; set; }
    [JsonIgnore] public WorkerWorkState WorkState { get; set; }
    /// <summary>The direction the worker faces while sitting at lunch, in radians.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public float LunchHeading { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
public sealed class TradeOrder {
    public Dictionary<string, int> Goods { get; set; } = new();
    public bool Buying { get; set; }
    public float Value { get; set; }
    public float Elapsed { get; set; }
    public int Signpost { get; set; }
    /// <summary>Crates on the cart: sold crates on the way out, bought boxes on the way back.</summary>
    public int Crates { get; set; }
    /// <summary>Tells carts apart for other machines; zero until <see cref="World.NumberWolvesAndOrders"/>.</summary>
    public int Id { get; set; }
}
/// <summary>
/// Everything a save holds. The player properties read and write <see cref="Actor"/>, the player whose action or turn the simulation is running.
/// A save keeps every player in <see cref="Party"/> and leaves those properties out.
/// </summary>
public sealed class SaveState {
    /// <summary>Everyone playing now; a new world starts with one player not yet claimed by any machine.</summary>
    [JsonIgnore] public List<PlayerState> Players { get; set; }
    /// <summary>Players of this world who are not playing now, by the key of the machine they play from.</summary>
    [JsonIgnore] public Dictionary<string, PlayerState> Absent { get; set; } = new();
    [JsonIgnore] public PlayerState Actor { get; set; }
    public SaveState() : this(new byte[Terrain.Size * Terrain.Size]) { }
    /// <summary>A save holding the given fog map; a guest reads an update's state into one with an empty map, which it never looks at.</summary>
    internal SaveState(byte[] explored) { Players = new() { new PlayerState() }; Actor = Players[0]; Explored = explored; }
    /// <summary>Every player of this world, playing or not, by machine key; the empty key is a player no machine has claimed yet.</summary>
    public Dictionary<string, PlayerState> Party {
        get => Players.Concat(Absent.Values).ToDictionary(p => p.Key);
        set { Absent = value; foreach (var (key, p) in value) p.Key = key; }
    }
    /// <summary>The key of the machine that last started this world, whose player follows the save to another computer.</summary>
    public string Owner { get; set; } = "";
    /// <summary>
    /// Raised whenever a change would make saves written before it load wrong.
    /// Saves from any other version are refused, never converted.
    /// </summary>
    public int Version { get; set; } = 17;
    public ThingList Things { get; set; } = new();
    public List<Worker> Workers { get; set; } = new();
    public List<TradeOrder> Orders { get; set; } = new();
    public List<Wolf> Wolves { get; set; } = new();
    /// <summary>One bit for each of <see cref="WolfPatrols.Dens"/> whose wolves are out tonight.</summary>
    public int DensOut { get; set; }
    /// <summary>Whole hours since dusk that have had their one more wolf tonight.</summary>
    public int DenHours { get; set; }
    /// <summary>A wolf has howled tonight.</summary>
    public bool Howled { get; set; }
    /// <summary>How many times players have come to a cabin's door, which numbers each arrival.</summary>
    public int DoorArrivals { get; set; }
    [JsonIgnore] public Inventory Backpack { get => Actor.Backpack; set => Actor.Backpack = value; }
    public Dictionary<string, float> Demand { get; set; } = new();
    /// <summary>Sale prices as last reported by someone arriving from the village, and when that was.</summary>
    public Dictionary<string, float> Prices { get; set; } = new();
    public float PricesTime { get; set; }
    public HashSet<string> Milestones { get; set; } = new();
    /// <summary>Every item the settlement has gathered, grown, bought or started with; with what it made, in <see cref="Milestones"/>, the recipe map shows these as known.</summary>
    public HashSet<string> Obtained { get; set; } = new();
    [JsonIgnore] public float PlayerX { get => Actor.PlayerX; set => Actor.PlayerX = value; }
    [JsonIgnore] public float PlayerY { get => Actor.PlayerY; set => Actor.PlayerY = value; }
    public float Time { get; set; } = 55;
    [JsonIgnore] public float Health { get => Actor.Health; set => Actor.Health = value; }
    [JsonIgnore] public float Hunger { get => Actor.Hunger; set => Actor.Hunger = value; }
    [JsonIgnore] public float Thirst { get => Actor.Thirst; set => Actor.Thirst = value; }
    public int NextId { get; set; } = 1;
    public int Goal { get; set; }
    public int WageDay { get; set; } = 1;
    [JsonIgnore] public List<string> HotbarSlots { get => Actor.HotbarSlots; set => Actor.HotbarSlots = value; }
    /// <summary>Contents of crates packed into items, keyed by the number in their item id.</summary>
    public Dictionary<int, Inventory> Crates { get; set; } = new();
    [JsonIgnore] public List<Queued> Crafting { get => Actor.Crafting; set => Actor.Crafting = value; }
    [JsonIgnore] public string Making { get => Actor.Making; set => Actor.Making = value; }
    [JsonIgnore] public float CraftTime { get => Actor.CraftTime; set => Actor.CraftTime = value; }
    [JsonIgnore] public float Torch { get => Actor.Torch; set => Actor.Torch = value; }
    [JsonIgnore] public bool TorchLit { get => Actor.TorchLit; set => Actor.TorchLit = value; }
    [JsonIgnore] public float TorchKindling { get => Actor.TorchKindling; set => Actor.TorchKindling = value; }
    [JsonIgnore] public int ActiveHotbar { get => Actor.ActiveHotbar; set => Actor.ActiveHotbar = value; }
    [JsonIgnore] public string InHand { get => Actor.InHand; set => Actor.InHand = value; }
    [JsonIgnore] public float RespawnIn { get => Actor.RespawnIn; set => Actor.RespawnIn = value; }
    [JsonIgnore] public bool Mauled { get => Actor.Mauled; set => Actor.Mauled = value; }
    [JsonIgnore] public int Sheltered { get => Actor.Sheltered; set => Actor.Sheltered = value; }
    public float PlaySeconds { get; set; }
    public bool EndingShown { get; set; }
    /// <summary>Coins from sales that came back today; wages are taken off at dawn to give the day's profit.</summary>
    public int DayRevenue { get; set; }
    /// <summary>Profit of each finished day, oldest first.</summary>
    public List<int> Profits { get; set; } = new();
    /// <summary>One byte per map tile, 255 once the player has seen it; serialized as base64.</summary>
    public byte[] Explored { get; set; }
    /// <summary>Things as last seen, kept so the fog shows them unchanged until the player looks again.</summary>
    public Dictionary<int, Thing> Remembered { get; set; } = new();
    /// <summary>Every tile with a gravel path laid on it, as its place in the map's rows, <c>y * Terrain.Size + x</c>.</summary>
    public List<int> GravelPaths { get; set; } = new();
    /// <summary>The shape and rotation of each steel pipe, keyed by the tile's place in the map's rows.</summary>
    public Dictionary<int, PipeTile> SteelPipes { get; set; } = new();
    /// <summary>Liters in each wet steel pipe, keyed by its index in the map's rows.</summary>
    public Dictionary<int, double> PipeWater { get; set; } = new();
}

/// <summary>Authoritative simulation, independent of rendering and UI; the host runs it for every player.</summary>
public sealed partial class World {
    public readonly GameData Data;
    public SaveState S { get; private set; } = new();
    public Action<string>? Notify;
    public Action<ActorAction, Vector2>? PlayerActed;
    /// <summary>The server takes its newest movement report for the acting player as a wolf's bite lands.</summary>
    internal Action<PlayerState>? PlaceBiteTarget;
    /// <summary>Raised once the whole state has been replaced by a new game, a load or a guest's copy of the host's world; what belonged to the old world resets itself on it.</summary>
    public Action? Replaced;
    /// <summary>A named sound effect and where it happens; the simulation only names it.</summary>
    public Action<string, Vector2>? Sound;
    /// <summary>How far down the valley an errand runner walks before fading from view on the way to Bramblewick.</summary>
    public const float TripDistance = 8;
    /// <summary>How fast a worker walks, in tiles a second.</summary>
    public const float WorkerSpeed = 2.25f;
    /// <summary>
    /// How far down the valley a worker walking off is, from 0 at the signpost to 1 out of view, after <paramref name="out"/> seconds away and <paramref name="back"/> seconds before coming back.
    /// The walker goes at a worker's walking pace over <see cref="TripDistance"/>, and waits out of view for the rest of the trip.
    /// </summary>
    public static float WalkLeg(float @out, float back) => Math.Clamp(Math.Min(@out, back) * WorkerSpeed / TripDistance, 0, 1);
    /// <summary>How far down the valley a worker is, from 0 at the signpost to 1 out of view: running off for prices, or walking home or back over half a trade trip.</summary>
    public float TripLeg(Worker w) => w.Away > 0 ? WalkLeg(Data.TradeSeconds - w.Away, w.Away) : WalkLeg(w.Commute, Data.TradeSeconds / 2);
    public const float LeapSeconds = .4f;
    internal const float LeapRange = 3.5f;
    /// <summary>Minimum reinforced furnace heat: steel only progresses at or above it.</summary>
    public const float SteelHeat = 80;
    /// <summary>Heat as degrees Celsius for display: the steel line sits where steel forms, 1450 °C.</summary>
    public static int Celsius(float heat) => (int)MathF.Round(heat * 1450 / SteelHeat / 10) * 10;
    private float gatherClock { get => S.Actor.GatherClock; set => S.Actor.GatherClock = value; }
    private float attackClock { get => S.Actor.AttackClock; set => S.Actor.AttackClock = value; }
    private bool asleep;
    // Furnaces whose batch finished on the previous tick: starting the next batch keeps the fire going rather than lighting it.
    private readonly HashSet<int> stillBurning = new();
    // Set by walking, gathering or pumping and cleared at the end of each tick: a busy player is not hammering.
    private bool playerBusy { get => S.Actor.Busy; set => S.Actor.Busy = value; }
    public World(GameData data, bool generate = true) {
        Data = data;
        Doorways = new Doorways(this); DoorTug = new DoorTug(this); DoorCheck = new DoorCheck(this); WolfBodies = new WolfBodies(this);
        WolfPatrols = new WolfPatrols(this); WolfHunt = new WolfHunt(this); WolfSnap = new WolfSnap(this);
        WolfCounter = new WolfCounter(this); WolfFollow = new WolfFollow(this); Swings = new Swings(this);
        Construction = new Construction(this); GravelPaths = new GravelPaths(this); Mines = new Mines(this);
        Farms = new Farms(this); SteelPipes = new SteelPipes(this);
        lunchBreak = new LunchBreak(this); Replaced += lunchBreak.Reset;
        walkSearch = new WalkSearch(this);
        barrels = new Barrels(this); plumbing = new Plumbing(this, barrels);
        barrelHaul = new BarrelHaul(this, barrels); bedtimeWater = new BedtimeWater(this);
        hauling = new Hauling(this, barrelHaul); gathering = new Gathering(this); waterGathering = new WaterGathering(this); crafting = new Crafting(this); packCraft = new PackCraft(this);
        stationKinds = Data.Recipes.Values.Select(r => r.Station).OfType<ThingKind>().Distinct().ToArray();
        if (generate) NewGame();
    }
    /// <summary>The kinds of station some recipe is made at.</summary>
    private readonly ThingKind[] stationKinds;
    public ReadOnlySpan<ThingKind> StationKinds => stationKinds;
    public Vector2 Player { get => new(S.PlayerX, S.PlayerY); set { S.PlayerX = value.X; S.PlayerY = value.Y; } }
    /// <summary>Putting buildings, yards and crates up and taking buildings down.</summary>
    public Construction Construction { get; }
    public Farms Farms { get; }
    private readonly Barrels barrels;

    private readonly BarrelHaul barrelHaul;
    // The jobs several kinds of place offer, and the places' own versions of them: the lake's gathering and the packing station's crafting.
    private readonly Hauling hauling;
    private readonly Gathering gathering, waterGathering;
    private readonly Crafting crafting, packCraft;
    private readonly BedtimeWater bedtimeWater;
    private readonly LunchBreak lunchBreak;
    /// <summary>Gravel path tiles: laying them, taking them up and how much faster walkers are on them.</summary>
    public GravelPaths GravelPaths { get; }
    public SteelPipes SteelPipes { get; }
    private readonly Plumbing plumbing;
    /// <summary>Standing liters in a pipe and pressure from the pumps attached to its connected pipes.</summary>
    public (float Liters, float Pressure) PipeReading(Vector2I tile) => plumbing.Reading(tile);
    /// <summary>Mines: marking one at a cliff, building it in stages and working it underground.</summary>
    public Mines Mines { get; }
    /// <summary>Cabin doors: stepping in and out, the queue at the door and knocking.</summary>
    public Doorways Doorways { get; }
    /// <summary>Wolves tugging players back out of cabin doorways.</summary>
    public DoorTug DoorTug { get; }
    /// <summary>Wolves at the doors of cabins with someone inside: scratching, forcing an unlocked door and taking a player out.</summary>
    public DoorCheck DoorCheck { get; }
    /// <summary>How wolves at a player keep their distance from them and from each other.</summary>
    public WolfBodies WolfBodies { get; }
    /// <summary>Wolves coming out of their dens at dusk, patrolling, smelling players and going home before dawn.</summary>
    public WolfPatrols WolfPatrols { get; }
    /// <summary>Wolves hunting a player: howling, closing in, leaping, biting and pinning.</summary>
    public WolfHunt WolfHunt { get; }
    /// <summary>A wolf's bite: gathering itself, leaping and snapping at where the player is heading.</summary>
    public WolfSnap WolfSnap { get; }
    public WolfCounter WolfCounter { get; }
    /// <summary>Follow mode: wolves scared by a torch circling a player, bracing, running or standing their ground.</summary>
    public WolfFollow WolfFollow { get; }
    /// <summary>Players' swings at wolves: when each lands and which wolves it touches.</summary>
    public Swings Swings { get; }
    /// <summary>At this much health or less, a wolf's attack takes a player down rather than biting.</summary>
    public const float TakedownHealth = 50;
    public int Day => (int)(S.Time / Data.DaySeconds) + 1;
    public float DayPhase => DayPhaseAt(S.Time);
    /// <summary>How far through its day the world's clock is at <paramref name="time"/>, from 0 at dawn to 1 at the next.</summary>
    public float DayPhaseAt(float time) => time % Data.DaySeconds / Data.DaySeconds;
    public bool Night => DayPhase >= Data.NightStarts;
    /// <summary>Whether the moon rather than the sun is in the sky, from sunset until dawn.</summary>
    public bool MoonUp => MoonUpAt(S.Time);
    public bool MoonUpAt(float time) => DayPhaseAt(time) >= Data.Sunset;
    /// <summary>How far the sun, or the moon after sunset, has crossed the sky, from 0 where it rises to 1 where it sets.</summary>
    public float SkyArc => SkyArcAt(S.Time);
    public float SkyArcAt(float time) => MoonUpAt(time) ? (DayPhaseAt(time) - Data.Sunset) / (1 - Data.Sunset) : DayPhaseAt(time) / Data.Sunset;
    /// <summary>How much of the moon is lit, from 0 at new moon to 1 at full; a new game starts near full moon so the first night is not the darkest.</summary>
    public float Moonlight => MoonlightAt(S.Time);
    public float MoonlightAt(float time) => (1 + MathF.Cos(MathF.Tau * time / Data.DaySeconds / Data.MoonCycleDays)) / 2;
    /// <summary>Clock hour, with the day starting at 06:00.</summary>
    public float Hour => (DayPhase * 24 + 6) % 24;
    /// <summary>From the first fading light until dawn; workers are off duty and wolves hunt.</summary>
    public bool Dusk => DayPhase >= Data.DuskStarts;
    public bool Lunch => Hour >= Data.LunchHour && Hour < Data.LunchHour + 1;
    /// <summary>On the map and neither signed out nor at lunch.</summary>
    public bool OnDuty(Worker w) => !w.Home && !Dusk && !Lunch;
    /// <summary>A worker in sight on the map; one mining underground is out of sight inside the hill.</summary>
    public bool Sees(Worker w) => !w.Home && w.WorkState != WorkerWorkState.Underground && Sees(w.Pos);
    public bool Dead => S.RespawnIn > 0;
    /// <summary>Dead or pinned by a wolf: the player cannot move or act.</summary>
    public bool Downed => Dead || S.Mauled;
    /// <summary>Whether the acting player is taken up by something else, downed or at a cabin door, so they can do nothing else.</summary>
    /// <summary>What the acting player is doing at a cabin's door.</summary>
    public Doorway Door => S.Actor.Door;
    public bool Occupied => Downed || S.Actor.Door != Doorway.None;
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
    public Vector2 GatherAt(Thing t, Vector2 from) => t.Kind switch {
        ThingKind.Water => Terrain.WaterPoint(from),
        ThingKind.Mine => Mines.WorkSpot(t, from),
        _ => t.Pos
    };
    /// <summary>How far, in metres, the player reaches to move items in and out of storage and stations.</summary>
    public const float ItemReach = 15;
    /// <summary>Whether the player can move items to or from a thing.</summary>
    public bool Reaches(Thing t) => Near(t, ItemReach);
    /// <summary>Whether the player can take from a worker's hands or put something in them: the worker is in sight and within the reach of a building's slots.</summary>
    public bool Reaches(Worker w) => Sees(w) && w.Away <= 0 && w.Commute == 0 && Reaches(w.Pos);
    /// <summary>Whether a spot on the ground is within the player's reach, which nothing is while they are down or in a cabin.</summary>
    public bool Reaches(Vector2 spot) => !Downed && S.Sheltered == 0 && Player.DistanceTo(spot) <= ItemReach;
    public bool Near(Thing t, float radius = 3.2f, Vector2? at = null) =>
        !Downed && (S.Sheltered == 0 || S.Sheltered == t.Id) && Player.DistanceTo(at ?? GatherAt(t, Player)) <= radius;
    public int Coins => S.Backpack[Inventory.Coins];
    /// <summary>What every worker's wage comes to each dawn.</summary>
    public int DailyWages {
        get {
            int wages = 0;
            foreach (var w in S.Workers) wages += w.Wage;
            return wages;
        }
    }
    public InventoryView Backpack => new(S.Backpack);
    public float Time => S.Time;
    public float Health => S.Health;
    public float Hunger => S.Hunger;
    public float Thirst => S.Thirst;
    public float Torch => S.Torch;
    public bool TorchLit => S.TorchLit;
    public float TorchKindling => S.TorchKindling;
    public float RespawnIn => S.RespawnIn;
    public bool Mauled => S.Mauled;
    public int Sheltered => S.Sheltered;
    public int ActiveHotbar => S.ActiveHotbar;
    /// <summary>The tool selected in hand without a hotbar slot, or empty.</summary>
    public string InHand => S.InHand;
    public ListView<string> HotbarSlots => new(S.HotbarSlots);
    /// <summary>Hand recipes waiting to be made, one entry per recipe.</summary>
    public ListView<Queued> Crafting => new(S.Crafting);
    /// <summary>The hand recipe being made now, or empty.</summary>
    public string Making => S.Making;
    public float CraftTime => S.CraftTime;
    public ListView<Thing> Things => S.Things.View;
    public ListView<Thing> ThingsOf(ThingKind kind) => S.Things.Of(kind);
    public ListView<Worker> Workers => new(S.Workers);
    public ListView<Wolf> Wolves => new(S.Wolves);
    public ListView<TradeOrder> Orders => new(S.Orders);
    public ListView<PlayerState> Players => new(S.Players);
    public int Goal => S.Goal;
    public bool Reached(string milestone) => S.Milestones.Contains(milestone);
    /// <summary>
    /// Whether the settlement has ever made, gathered, bought or started with an item, or with anything the item ends up in:
    /// whoever has had an iron bar counts as having had the ore.
    /// </summary>
    public bool Had(string item) {
        if (HadItself(item)) return true;
        foreach (string product in Data.EndsUpIn(item)) if (HadItself(product)) return true;
        return false;
    }
    /// <summary>What the settlement gathered, bought or started with is in its record of items, and what it made is among its milestones.</summary>
    private bool HadItself(string item) => S.Obtained.Contains(item) || S.Milestones.Contains(item);
    internal void Got(string item) => S.Obtained.Add(item);
    public int DayRevenue => S.DayRevenue;
    public ListView<int> Profits => new(S.Profits);
    public bool EndingShown => S.EndingShown;
    public float PlaySeconds => S.PlaySeconds;
    /// <summary>The player whose action or turn is running.</summary>
    public int ActorId => S.Actor.Id;
    public bool Priced(string item) => S.Prices.ContainsKey(item);
    public Thing? Remembered(int id) => S.Remembered.GetValueOrDefault(id);
    /// <summary>Spends carried coins; coins stored elsewhere do not count.</summary>
    public bool Pay(int amount) => amount <= Coins && S.Backpack.Remove(Inventory.Coins, amount) == amount;
    public Thing? Find(int id) => S.Things.Find(id);
    /// <summary>Tells the acting player, or everyone while the tick runs the shared world.</summary>
    public void Say(string message) {
        if (everyone || S.Actor.Id == LocalId) Notify?.Invoke(message);
        if (everyone || S.Actor.Id != LocalId) Relay?.Invoke(everyone ? null : S.Actor, message);
    }
    // Sleep fast-forwards hours in one frame, so its events stay silent instead of playing all at once.
    internal void Play(string sound, Vector2 at) { if (!asleep) Sound?.Invoke(sound, at); }
    /// <summary>The recipe game data names for a station placed or loaded without one, or empty.</summary>
    private string StartingRecipe(ThingKind kind) => Data.Buildings.TryGetValue(kind, out var building) ? building.Recipe : "";
    public Thing Add(ThingKind kind, Vector2 pos, int remaining = 0, float rotation = 0) {
        var t = new Thing { Id = S.NextId++, Kind = kind, Pos = pos, Rotation = rotation, Remaining = remaining, Keep = StartingRecipe(kind) };
        if (kind == ThingKind.Signpost) { t.Inventory.Slots = MarketSlots; t.Output = new() { Slots = MarketSlots }; t.Wages = NewWageSlot(); }
        if (SlotRules(kind).Length > 0) t.Tools = new() { Slots = SlotRules(kind).Length, StackSize = 1 };
        if (Burns(kind)) t.Burner = NewBurner();
        if (kind == ThingKind.Crate) t.Inventory = NewCrate();
        if (kind == ThingKind.Barrel) t.Inventory = Barrels.NewTank();
        if (kind == ThingKind.Hut) { t.Inventory.Slots = 1; t.Inventory.StackSize = 1; }
        if (kind == ThingKind.Packer) { t.Inventory.Slots = Data.CrateCapacity + 1; t.Output = new() { Slots = 1 }; }
        if (kind == ThingKind.Mine) { t.Inventory.Slots = Data.Mine.Materials.Values.Sum(n => (n + t.Inventory.StackSize - 1) / t.Inventory.StackSize); t.Output = new() { Slots = 2 }; }
        if (kind == ThingKind.Toolstation) { t.Inventory.Slots = ToolSlots; WatchTools(t); }
        if (kind == ThingKind.Pile) t.Inventory.Slots = PileStacks;
        if (kind == ThingKind.Chest) t.Inventory.Slots = ChestStacks;
        if (kind == ThingKind.Farm) { t.Inventory.Slots = Data.Farm.Sow.Count + 1; t.Output = new() { Slots = Data.Farm.Yield.Count }; }
        S.Things.Add(t); return t;
    }
    public void NewGame() {
        S = new SaveState(); toolsChanged = true;
        S.Backpack.Add("steel_axe", 1); S.Backpack.Add("berries", 10); S.Backpack.Add("wood", 6);
        S.Backpack.Add(Water.Empty, 1);
        foreach (var pair in S.Backpack.Items) Got(pair.Key);
        var rng = new Random(4271);
        foreach (var cliff in Cliff.All) {
            // Two deposits flank the mine's mouth in the middle of the cliff, holding what three did before.
            foreach (float along in new[] { -3.5f, 3.5f }) Add(cliff.Ore, cliff.Center + cliff.Face * 2.6f + cliff.Along * along, DepositOre);
            if (cliff != Cliff.Of(cliff.Ore) || cliff.Ore != ThingKind.Coal) continue;
            // Loose coal rolls well out past the deposits of the nearer coal cliff, so collecting it takes a search.
            // Iron has no loose pieces: the first pickaxe is bought with berry money.
            for (int placed = 0; placed < 6;) {
                var p = cliff.Center + cliff.Face * (4.2f + (float)rng.NextDouble() * 6.5f) + cliff.Along * ((float)rng.NextDouble() * 2 - 1) * (cliff.HalfLength + 4);
                if (!CanStand(p) || S.Things.Any(t => t.Pos.DistanceTo(p) < 2)) continue;
                Add(ThingKind.LooseCoal, p, 2); placed++;
            }
        }
        // Rocks and berry bushes stand alone on the plains, far from the clearing and each other, so more of them means exploring.
        Scatter(rng, ThingKind.Stone, 30, StonePile, 15, 9, _ => 1, 2000);
        Scatter(rng, ThingKind.Berries, 24, BushBerries, 14, 10, _ => 1, 2000);
        var shore = Terrain.Spawn - Terrain.Lake;
        Add(ThingKind.Water, Terrain.Lake + shore.Normalized() * (Terrain.Shore(MathF.Atan2(shore.Y, shore.X)) + .2f), 9999);
        Add(ThingKind.Tree, Terrain.Spawn + new Vector2(5, 0), 42);
        // Trees are occasional on the plains and thicken into woods toward the mountains.
        // Full-size trees need room for their crowns, so even the woods stay walkable and readable.
        Scatter(rng, ThingKind.Tree, int.MaxValue, 42, 3.4f, 9, p => Math.Clamp(1.15f - Terrain.FromMountains(p) / 16, .015f, 1), 60000);
        DropLeaves();
        ScatterLoose();
        GrowBushes();
        Look();
        Replaced?.Invoke();
    }
    /// <summary>Leaves a rake gathers in one go from a patch, which fills again every day.</summary>
    public const int LeafPatch = 12;
    /// <summary>Ore in a full coal, iron or tin deposit at a cliff.</summary>
    public const int DepositOre = 600;
    /// <summary>Berries on a full bush, at the start and each time it fruits again.</summary>
    public const int BushBerries = 24;
    /// <summary>Stone in a full boulder.</summary>
    public const int StonePile = 64;
    /// <summary>Meat on a fresh wolf carcass.</summary>
    public const int WolfMeat = 4;
    public static readonly Vector2 LeafOffset = new(.9f, .9f);
    /// <summary>Leaf litter around every oak, the broadleaf one of the three tree variants picked by id; the patch sits off the trunk so both can be clicked.</summary>
    private void DropLeaves() {
        foreach (var tree in S.Things.Of(ThingKind.Tree)) if (tree.Id % 3 == 0 && Terrain.Open(tree.Pos + LeafOffset)) Add(ThingKind.Leaves, tree.Pos + LeafOffset, LeafPatch);
    }
    /// <summary>Places resources on open ground away from the clearing, at least <paramref name="spacing"/> apart from their own kind.</summary>
    /// <param name="chance">How likely a spot is kept, so density can follow the landscape.</param>
    /// <param name="tries">Random spots to consider; with a low chance this, not the count, sets how many are placed.</param>
    private void Scatter(Random rng, ThingKind kind, int count, int remaining, float spacing, float clearing, Func<Vector2, float> chance, int tries) {
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
            if (p.DistanceTo(Terrain.Spawn) < clearing || !Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 3.5f) || Cliff.All.Any(c => c.DistanceTo(p) < Cliff.Depth + 2.5f) || InOutcrop(p, 2.5f) || WolfPatrols.NearDen(p, 5)) continue;
            if (rng.NextDouble() > chance(p) || Crowded(p)) continue;
            var t = Add(kind, p, remaining); placed++;
            if (!near.TryGetValue(Cell(p), out var cellList)) near[Cell(p)] = cellList = new();
            cellList.Add(t);
        }
    }
    /// <summary>Where a step of <paramref name="delta"/> from <paramref name="from"/> ends on ground a player can stand on, sliding along whatever blocks it.</summary>
    public Vector2 Slide(Vector2 from, Vector2 delta) {
        if (CanStand(from + delta)) return from + delta;
        var p = from;
        if (CanStand(p + new Vector2(delta.X, 0))) p += new Vector2(delta.X, 0);
        if (CanStand(p + new Vector2(0, delta.Y))) p += new Vector2(0, delta.Y);
        return p;
    }
    /// <summary>Whether a player, worker or wolf can stand on a spot: walkable ground with nothing built in the way.</summary>
    public bool CanStand(Vector2 p) => CanStand(p, null);

    internal bool CanStand(Vector2 p, Thing? except) {
        if (!Open(p)) return false;
        foreach (var kind in Blockers)
            foreach (var t in S.Things.Of(kind))
                if (t != except && Blocks(t, p)) return false;
        return true;
    }
    /// <summary>Walkable ground ignoring buildings: inside the valley, off the water and clear of cliff walls and their outcrops.</summary>
    internal static bool Open(Vector2 p) {
        if (!Terrain.Open(p)) return false;
        foreach (var c in Cliff.All) if (c.DistanceTo(p) < Cliff.Depth) return false;
        return !InOutcrop(p);
    }
    /// <summary>Whether a point lies within a cliff's or a den's outcrop of solid rock.</summary>
    private static bool InOutcrop(Vector2 p, float margin = 0) => Mines.InOutcrop(p, margin) || WolfPatrols.InOutcrop(p, margin);
    /// <summary>The kinds that are not resources: buildings, yards and what is left on the ground, such as graves.</summary>
    public static readonly ThingKind[] Structures = Array.FindAll(Kinds.All, kind => GatherRules.Of(kind) == null);
    /// <summary>The kinds that stand in the way; resources, yards, campfires, graves, signposts and mines are walked past.</summary>
    private static readonly ThingKind[] Blockers = Array.FindAll(Structures, kind => !Construction.IsZone(kind) && kind is not (ThingKind.Campfire or ThingKind.Grave or ThingKind.Signpost or ThingKind.Mine));
    internal static ReadOnlySpan<ThingKind> WalkBlockers => Blockers;
    /// <summary>Whether a thing of one of the <see cref="Blockers"/> kinds stands on a spot.</summary>
    private static bool Blocks(Thing t, Vector2 p) => t.Pos.DistanceTo(p) < Footprint(t.Kind);
    public const int PileStacks = 14;
    /// <summary>A third of a pile, rounded to the nearest stack.</summary>
    public const int ChestStacks = (PileStacks + 1) / 3;
    /// <summary>Where a bellows operator stands: past the handle end of the bellows, behind the forge.</summary>
    public static readonly Vector2 BellowsStand = new(0, -2.1f);
    /// <summary>Where a packer stands: behind the packing table, facing the camera, between the open crate and the heap of goods.</summary>
    public static readonly Vector2 PackStand = new(-.3f, -.8f);
    public static float Footprint(ThingKind kind) => kind switch { ThingKind.Pile => 1.5f, ThingKind.Farm => Farms.HalfSize, ThingKind.Cooktable => .6f, ThingKind.Packer => .7f, ThingKind.Cannery => .5f, ThingKind.Hut => 1.55f, ThingKind.Reinforced => .8f, ThingKind.Furnace => .65f, ThingKind.Bench => .6f, ThingKind.Chest or ThingKind.Anvil or ThingKind.Toolstation => .45f, _ => .35f };
    /// <summary>A word with "a" or "an" before it, built once.</summary>
    public string A(string word) => Phrase("aeiou".Contains(word[0]) ? "an " : "a ", word);
    private string Task(Thing t) => t.Kind switch {
        ThingKind.Tree => "chop wood",
        ThingKind.Leaves => "rake leaves",
        ThingKind.Stump => "dig up the stump",
        ThingKind.Bush => "dig up the bush",
        _ when t.IsLoose => Phrase("pick up ", Lower(Data.Name(t.Kind))),
        ThingKind.Mine => Mines.Task(t),
        _ => Phrase("mine ", t.Kind.Id())
    };
    // Status lines are made of a few phrases, names and small numbers, so each line is built once and reused rather than on every tick.
    private readonly Dictionary<(string, string, string, string), string> phrases = new();
    private readonly Dictionary<string, string> lowered = new();
    private static readonly string[] Numbers = Enumerable.Range(0, 1000).Select(n => n.ToString()).ToArray();
    internal string Phrase(string a, string b, string c = "", string d = "") {
        if (!phrases.TryGetValue((a, b, c, d), out var text)) phrases[(a, b, c, d)] = text = a + b + c + d;
        return text;
    }
    /// <summary>Text in lower case, built once.</summary>
    public string Lower(string text) {
        if (!lowered.TryGetValue(text, out var lower)) lowered[text] = lower = text.ToLowerInvariant();
        return lower;
    }
    /// <summary>A whole number as text, rounded like a <c>0</c> format; small ones come from a table.</summary>
    private static string Number(float value) {
        int n = (int)MathF.Round(value, MidpointRounding.AwayFromZero);
        return n >= 0 && n < Numbers.Length ? Numbers[n] : n.ToString();
    }
    /// <summary>The kind of tool an item counts as in a worker's slot and on the tool rack.</summary>
    public string ToolKind(string item) => Water.IsBottle(item) ? Water.BottleTool : Data.Items.GetValueOrDefault(item)?.Tool ?? "";

    /// <summary>The fastest carried tool of a kind at or above <paramref name="tier"/>, or null when none is carried.</summary>
    public string? BestTool(string tool, Inventory? from = null, int tier = 0) => BestTool(tool, from ?? S.Backpack, tier, requireFree: false);

    private string? BestTool(string tool, Inventory from, int tier, bool requireFree) {
        string? best = null; float speed = 0;
        foreach (var (item, _) in from.Items) {
            if (!Data.Items.TryGetValue(item, out var def) || def.Tool != tool || def.Tier < tier || best != null && def.Speed <= speed) continue;
            if (requireFree && from.Free(item) == 0) continue;
            best = item; speed = def.Speed;
        }
        return best;
    }
    public float Speed(string? tool) => tool == null ? 1 : Data.Items[tool].Speed;
    /// <summary>How many times faster than a recipe's own seconds work goes: a bench's speed, or 1 elsewhere and by hand, times the tool's.</summary>
    public float WorkRate(ThingKind? station, string? tool) => (station == ThingKind.Bench ? Data.BenchSpeed : 1) * Speed(tool);
    /// <summary>Tiles a second the acting player moves where they stand: walking or sprinting, and faster on gravel.</summary>
    public bool CanSprint => Barrel.In(S.Backpack) == null;
    public float PlayerSpeed => (Barrel.FilledIn(S.Backpack) is string barrel ? Data.WalkSpeed * Barrel.Speed(barrel)
        : Data.MoveSpeed(S.Actor.Sprinting && CanSprint)) * GravelPaths.SpeedAt(Player);
    public void MovePlayer(Vector2 delta) {
        // Inside a cabin the player stays put until they step outside on purpose.
        if (Occupied || S.Sheltered != 0) return;
        if (delta == Vector2.Zero) return;
        playerBusy = true;
        Player = Slide(Player, delta);
    }
    public string? GatherBlock(Thing t, bool worker = false) {
        if (t.Kind == ThingKind.Water && !worker && Water.FindBottle(S.Backpack) == Water.Full && !Water.HasBucketRoom(S.Backpack)) return "Your bottles are filled.";
        if (t.Kind == ThingKind.Mine) return Mines.BuildBlock(t);
        if (t.Kind == ThingKind.Farm) return t.FarmStage switch { FarmStage.Ripe => null, FarmStage.Growing => "The wheat is not ripe yet.", _ => "Nothing grows here yet. Sow the plot with wheat seeds and fertilizer." };
        if (!t.IsResource) return "Select a natural resource.";
        if (t.Remaining <= 0) return t.Kind switch { ThingKind.Berries => Phrase("Picked clean. The bush fruits again within ", Number(Data.BerryRegrowHours), " hours."), ThingKind.Leaves => "Raked clean. More leaves fall by tomorrow.", _ => "Depleted. Look for another deposit." };
        // Workers fetch their tools from a tool station instead of needing them carried, and carry off whatever they dig up.
        if (worker) return null;
        if (GatherRules.Of(t.Kind)?.Needs is string kind && BestTool(kind) == null) return kind switch {
            "axe" => "An axe is required to chop a tree.",
            "shovel" => Phrase("A shovel is required to dig up the ", Lower(Data.Name(t.Kind)), "."),
            _ => "A pickaxe is required to mine."
        };
        // A stump or a bush comes out whole, so all of it has to fit.
        if (t.IsDug && S.Backpack.Room(ResourceItem(t)) < t.Remaining) return Phrase(Phrase("No room in your backpack for ", Number(t.Remaining), " "), Lower(Data.Name(ResourceItem(t))), ".");
        return null;
    }
    public static string ResourceItem(Thing t) => ResourceItem(t.Kind);
    public static string ResourceItem(ThingKind kind) => GatherRules.Of(kind)?.Item ?? kind.Id();
    /// <summary>Seconds to gather one piece of a kind with a tool, or by hand with none.</summary>
    internal float GatherTime(ThingKind kind, string? tool) => Data.GatherSeconds / Speed(tool) * (tool == null ? GatherRules.Of(kind)?.ByHand ?? 1 : 1);
    public void Gather(Thing t, float dt) {
        // A loose piece is a single pick-up; anything else the host works on for as long as the player holds on to it.
        if (Busy()) { ResetGather(); return; }
        if (t.IsLoose ? Forwarded(Verb.PickUp, t.Id) : Remote) { S.Actor.GatherTarget = t.IsLoose ? 0 : t.Id; return; }
        // A felled tree or a dug up bush is no longer there to work on.
        if (Find(t.Id) != t) { ResetGather(); return; }
        S.Actor.GatherTarget = t.Id;
        playerBusy = true;
        if (!Near(t)) { gatherClock = 0; return; }
        var blocked = GatherBlock(t);
        if (blocked != null) { gatherClock = 0; return; }
        if (t.IsLoose) { PickUp(t); return; }
        if (t.Kind == ThingKind.Mine) { Mines.Work(t, dt); return; }
        if (t.Kind == ThingKind.Farm) { Farms.Harvest(t, dt); if (t.FarmStage == FarmStage.Fallow) Say("Harvested. Take the wheat, seeds and grass from the plot, and put seeds back in to sow again."); return; }
        gatherClock += dt;
        var rule = GatherRules.Of(t.Kind);
        string? tool = (rule?.Needs ?? rule?.Helps) is string kind ? BestTool(kind) : null;
        if (t.IsDug) {
            t.Progress += dt * Speed(tool);
            if (DugOut(t)) Unearth(t, S.Backpack);
            return;
        }
        if (gatherClock < GatherTime(t.Kind, tool)) return;
        gatherClock = 0;
        if (t.Kind == ThingKind.Water) {
            var result = Water.CollectWater(this, t);
            if (result != WaterCollectResult.NoChange) { Got(Water.Full); Play("gather_water", Player); }
            return;
        }
        int amount = Math.Min(t.Remaining, GatherYield(t.Kind));
        string item = ResourceItem(t);
        amount = S.Backpack.Add(item, amount); t.Remaining -= amount;
        if (t.Kind == ThingKind.Tree && t.Remaining <= 0) Fell(t, Player);
        if (amount > 0) Chip(t, tool);
        if (amount == 0) Say("Backpack full. Store or sell some supplies.");
        if (amount > 0) Got(item);
    }
    private void PickUp(Thing t) {
        string item = ResourceItem(t);
        int amount = S.Backpack.Add(item, t.Remaining); t.Remaining -= amount;
        if (amount == 0) { Say("Backpack full. Store or sell some supplies."); return; }
        if (t.Remaining <= 0) S.Things.Remove(t);
        Got(item); Play("wood_rocks_inventory", t.Pos); PlayerActed?.Invoke(ActorAction.PickUp, t.Pos);
    }
    public void ResetGather() { gatherClock = 0; S.Actor.GatherTarget = 0; }
    /// <summary>The player hammers an anvil while standing by it with nothing else to do.</summary>
    /// <remarks>Input calls <see cref="MovePlayer"/> and <see cref="Gather"/> before each tick, so a tick without them counts as standing still.</remarks>
    public bool PlayerHammers(Thing anvil) => !playerBusy && AtStation(anvil);
    /// <summary>How far, in metres from a station's edge, the player stands to work it: hammering, crafting or packing at it, or pumping its bellows.</summary>
    public const float WorkReach = 1;
    /// <summary>Whether the player stands within <see cref="WorkReach"/> of a station's edge.</summary>
    public bool AtStation(Thing t) => Near(t, Footprint(t.Kind) + WorkReach)
        || t.Kind == ThingKind.Pump && Near(t, WorkReach, t.ToWorld(BellowsStand));
    /// <summary>Stations that can need someone working at them.</summary>
    public static bool Worked(ThingKind kind) => kind is ThingKind.Anvil or ThingKind.Bench or ThingKind.Cooktable or ThingKind.Packer;
    public static readonly ThingKind[] WorkedKinds = Array.FindAll(Kinds.All, Worked);
    /// <summary>Whether the station's current recipe only advances while someone works at it; at the cooking table, a recipe marked <see cref="RecipeDef.Worked"/>.</summary>
    public bool Worked(Thing t) => Worked(t.Kind) && (t.Kind != ThingKind.Cooktable || Data.Recipes.TryGetValue(t.Recipe, out var r) && r.Worked);
    /// <summary>An anvil or bench only works while the player or a paid worker is hammering at it, not merely standing near.</summary>
    public bool Hammered(Thing anvil) {
        foreach (var p in S.Players) using (ActAs(p)) if (PlayerHammers(anvil)) return true;
        return CrewHammers(anvil);
    }
    /// <summary>Whether a paid worker is hammering at a station right now.</summary>
    public bool CrewHammers(Thing anvil) {
        foreach (var w in S.Workers) if (w.Assignment == anvil.Id && w.WageDebt <= 0 && w.WorkState == WorkerWorkState.Hammering && HasDuty(w, anvil.Id, JobKind.Craft)) return true;
        return false;
    }
    public void Consume(string item) {
        if (!Data.Items.TryGetValue(item, out var def) || Busy() || Forwarded(Verb.Consume, item: item, count: S.Backpack.PreferredSlot)) return;
        if (Water.IsBottle(item)) {
            if (!Water.DrinkLiter(this, item)) Say("Your bottle is empty. Collect water at the lake to refill it.");
            return;
        }
        if (def.Hunger <= 0 && def.Thirst <= 0) { Say($"{def.Name} needs cooking first."); return; }
        if (S.Backpack.Remove(item, 1) == 0) { Say($"No {def.Name} in your backpack."); return; }
        S.Hunger = Math.Min(100, S.Hunger + def.Hunger); S.Thirst = Math.Min(100, S.Thirst + def.Thirst);
        if (def.Food) Play("eat_berry", Player);
    }
    public void Pump(Thing t) {
        if (t.Kind is not (ThingKind.Reinforced or ThingKind.Pump) || !AtStation(t) || Busy() || Forwarded(Verb.Pump, t.Id)) return;
        playerBusy = true;
        if (t.Kind == ThingKind.Pump) WaterPumps.Stroke(t);
        else t.Heat = Math.Min(100, t.Heat + 24);
        PlayerActed?.Invoke(ActorAction.Pump, t.Pos);
    }
    public void FuelFire(Thing t) {
        if (!Reaches(t) || Busy() || Forwarded(Verb.FuelFire, t.Id)) return;
        int n = S.Backpack.Remove("wood", 1);
        if (n == 0) { Say("You need wood to feed the fire."); return; }
        if (t.Burn <= 0) Play("start_fire", t.Pos);
        t.Burn += Data.CampfireSeconds; Say($"Campfire fed: +{Data.CampfireHours:0} hours of light.");
    }
    /// <summary>
    /// Marks the recipe workers keep a station making once the batches asked of it are done; an empty one clears the mark.
    /// A mark on another recipe than the one the station is on turns the station to it and drops the batches that waited; <see cref="MarkBlock"/> says when it cannot.
    /// </summary>
    public void SetRecipe(Thing t, string recipe) {
        if (recipe != "" && !Makes(t, recipe)) return;
        if (Busy() || Forwarded(Verb.SetRecipe, t.Id, item: recipe)) return;
        // Only the server knows the mark as it is now: a window's copy can still show the one from before its last click.
        if (recipe == t.Keep) return;
        if (MarkBlock(t, recipe) is string blocked) { Say(blocked); return; }
        // A station that loses its mark with no batches waiting stays on the recipe it was marked with.
        if (recipe == "" && t.Batches == 0) t.Asked = t.Keep;
        else if (recipe != "" && recipe != Selected(t)) Ask(t, recipe, 0);
        t.Keep = recipe;
    }
    /// <summary>
    /// Why a station's mark cannot move to a recipe now, or null: a mark turns a station to another recipe only while no batch is worked and its slots are empty.
    /// Taking the mark off, and marking the recipe the station is on, are never refused.
    /// </summary>
    public string? MarkBlock(Thing t, string recipe) =>
        recipe == "" || recipe == Selected(t) ? null
        : t.Processing ? Busy(t)
        : t.Inventory.Count > 0 ? $"Empty the {Lower(Data.Name(t.Kind))}'s slots first." : null;
    private string Busy(Thing t) => $"The {Lower(Data.Name(t.Kind))} has to finish its batch first.";
    // The first recipe on each kind's board, found once.
    private readonly Dictionary<ThingKind, string> firstRecipes = new();
    /// <summary>
    /// The recipe a station is on, whose slots its page shows: the one it makes now, else the one it was on last, else the first on its board.
    /// A station is always on one; a thing that makes nothing is on none.
    /// </summary>
    public string Selected(Thing t) => t.Recipe != "" ? t.Recipe : t.Asked != "" ? t.Asked : firstRecipes.TryGetValue(t.Kind, out string? first) ? first : FirstRecipe(t.Kind);
    // Apart from Selected, which the tick calls: a lambda that reads its kind would allocate there on every call.
    private string FirstRecipe(ThingKind kind) =>
        firstRecipes[kind] = Data.Recipes.Where(p => p.Value.Station == kind).OrderBy(p => p.Value.Group).Select(p => p.Key).FirstOrDefault("");
    /// <summary>Most batches of one recipe that wait at a station or in the hand's queue.</summary>
    public const int MaxQueued = 99;
    private bool Makes(Thing t, string recipe) => Data.Recipes.TryGetValue(recipe, out var r) && r.Station == t.Kind;
    /// <summary>Takes waiting batches of a recipe off a station's count; a batch in progress stays.</summary>
    public void Unqueue(Thing t, string recipe, int count) {
        if (count <= 0 || t.Asked != recipe || Busy() || Forwarded(Verb.Unqueue, t.Id, item: recipe, count: count)) return;
        Ask(t, recipe, t.Batches - count);
    }
    /// <summary>Sets the recipe asked of a station and how many batches of it wait, between none and <see cref="MaxQueued"/>.</summary>
    private static void Ask(Thing t, string recipe, int batches) { t.Asked = recipe; t.Batches = Math.Clamp(batches, 0, MaxQueued); }
    /// <summary>Adds to or takes from the entry for a recipe in a hand's queue, between none and <see cref="MaxQueued"/>, and returns how many it moved.</summary>
    private static int Requeue(List<Queued> queue, string recipe, int count) {
        int at = queue.Count - 1;
        while (at >= 0 && queue[at].Recipe != recipe) at--;
        if (at < 0) { if (count <= 0) return 0; queue.Add(new Queued { Recipe = recipe }); at = queue.Count - 1; }
        int before = queue[at].Count, after = Math.Clamp(before + count, 0, MaxQueued);
        if (after == 0) queue.RemoveAt(at); else queue[at].Count = after;
        return Math.Abs(after - before);
    }
    /// <summary>A number that changes with the recipes in a hand's queue and their order, built without allocating.</summary>
    public static int QueueCode(ListView<Queued> queue) {
        int code = queue.Count;
        for (int i = 0; i < queue.Count; i++) code = code * 31 + queue[i].Recipe.GetHashCode();
        return code;
    }
    /// <summary>Batches of a recipe waiting at a station, or with none in the acting player's hand crafting.</summary>
    public int Waiting(Thing? station, string recipe) => station == null ? Waiting(Crafting, recipe) : station.Asked == recipe ? station.Batches : 0;
    /// <summary>The recipes a station makes, or with none those made by hand.</summary>
    public IEnumerable<string> RecipesAt(Thing? station) => station == null ? HandRecipes : Data.Recipes.Where(p => p.Value.Station == station.Kind).Select(p => p.Key);
    /// <summary>The tool the acting player works a recipe with: the faster of the one slotted at a station and the best one carried; null when there is none or none is needed.</summary>
    public string? ToolInUse(Thing? station, string tool, int tier) => tool == "" ? null : Faster(station == null ? null : BenchTool(station, tool, tier), BestTool(tool, null, tier));
    /// <summary>
    /// Whether the acting player has the tool a recipe needs, slotted at the station or carried, or it needs none.
    /// </summary>
    public bool HasToolFor(Thing? station, RecipeDef r) => r.Tool == "" || ToolInUse(station, r.Tool, r.Tier) != null;
    /// <summary>
    /// Whether the acting player works a station with the tool of a kind slotted there rather than one they carry: one
    /// is slotted, and they carry none faster.
    /// </summary>
    public bool WorksWithSlotted(Thing station, string tool) => BenchTool(station, tool) is string slotted && Faster(slotted, BestTool(tool, null, 0)) == slotted;
    /// <summary>
    /// The tool a station's recipe is worked with: the fastest of the one slotted and those carried by players working at the station.
    /// A worker brings none, so a station worked only by its crew works with the slotted tool and waits when there is none.
    /// </summary>
    private string? StationTool(Thing t, RecipeDef r) {
        if (r.Tool == "") return null;
        string? best = BenchTool(t, r.Tool, r.Tier);
        foreach (var p in S.Players)
            using (ActAs(p))
                if (PlayerHammers(t)) best = Faster(best, BestTool(r.Tool, null, r.Tier));
        return best;
    }
    /// <summary>The faster of two tools, the first when they are as fast; null when there is neither.</summary>
    private string? Faster(string? first, string? second) => first == null || second != null && Speed(second) > Speed(first) ? second : first;
    /// <summary>Batches of a recipe waiting in a hand's queue.</summary>
    public static int Waiting(ListView<Queued> queue, string recipe) {
        for (int i = 0; i < queue.Count; i++) if (queue[i].Recipe == recipe) return queue[i].Count;
        return 0;
    }
    /// <summary>
    /// Asks a station for batches of a recipe and loads their inputs and one fuel each from the backpack, on top of what the station holds.
    /// A station counts batches of one recipe at a time.
    /// The recipe asked for gets the station's mark, so workers go on making it once the batches are done.
    /// Turning from the recipe it is on to another drops the batches still waiting and takes what the station's boxes and fuel slot hold into the backpack;
    /// <see cref="SwitchBlock"/> says when it cannot.
    /// </summary>
    public void QueueBatch(Thing t, string recipe, int batches = 1) {
        if (batches <= 0 || !Makes(t, recipe)) return;
        if (!Reaches(t)) { Say("Walk closer to load this building."); return; }
        if (Busy() || Forwarded(Verb.QueueBatch, t.Id, item: recipe, count: batches)) return;
        if (SwitchBlock(t, recipe) is string blocked) { Say(blocked); return; }
        int waiting = Waiting(t, recipe);
        if (waiting >= MaxQueued) { Say($"{MaxQueued} batches are the most a station queues."); return; }
        string? missing = BatchShort(t, recipe);
        if (recipe != Selected(t)) { Unload(t.Inventory); if (t.Burner != null) Unload(t.Burner); }
        t.Keep = recipe;
        Ask(t, recipe, waiting + batches);
        batches = t.Batches - waiting;
        var r = Data.Recipes[recipe];
        // What loads, loads; one message names all that is still missing for the first batch, or else why the fuel slot takes no fuel.
        foreach (var (item, amount) in r.Inputs) Load(t, item, amount * batches);
        if (r.Burns && t.Burner != null && FuelFromBackpack(t, r, NoCost, say: missing == null) is string fuel) LoadFuel(t, fuel, batches);
        if (missing != null) Say(missing);
    }
    /// <summary>
    /// What a station would still lack for one batch of a recipe once the backpack's share is loaded, as a message, or null for nothing.
    /// Turning from another recipe counts what the station holds as carried, since it goes to the backpack first.
    /// </summary>
    public string? BatchShort(Thing t, string recipe) {
        if (!Data.Recipes.TryGetValue(recipe, out var r)) return null;
        bool switching = recipe != Selected(t);
        int Have(string item) => Carried(item) + t.Inventory[item] + (switching && t.Burner != null ? t.Burner[item] : 0);
        var missing = new List<string>();
        foreach (var (item, needed) in r.Inputs)
            if (needed - Have(item) is > 0 and int lack) missing.Add($"{lack}{(Data.VesselFor(item) != null ? "L of" : "")} {Lower(Data.Name(item))}");
        // Fuel burns only from the fuel slot: what it holds now, or carried fuel left once a batch's inputs are loaded in the input slots.
        int Spare(string fuel) => Carried(fuel) + (switching ? t.Inventory[fuel] + (t.Burner?[fuel] ?? 0) : 0) - r.Inputs.GetValueOrDefault(fuel);
        bool fuelled = !r.Burns || !switching && t.Burner?.First() is string held && r.Fuels.Contains(held) || r.Fuels.Exists(f => Spare(f) > 0);
        if (!fuelled) missing.Add($"fuel ({FuelWords(r.Fuels)})");
        return missing.Count == 0 ? null : $"Missing {Listed(missing, "and")}.";
    }
    /// <summary>
    /// Why a station cannot turn from the recipe it is on to another now, or null: a batch is being worked, or the backpack has no room for what the station's boxes and fuel slot hold.
    /// More batches of the recipe it is on are never blocked.
    /// </summary>
    public string? SwitchBlock(Thing t, string recipe) =>
        recipe == Selected(t) ? null
        : t.Processing ? Busy(t)
        : NoVesselRoom(t.Inventory) is string dry ? dry
        : !S.Backpack.Fits(t.Inventory, t.Burner, leaveOut: item => Data.VesselFor(item) != null) ? $"No room in your backpack for what the {Lower(Data.Name(t.Kind))} holds." : null;
    /// <summary>Moves everything a box holds into the backpack, as far as it fits; a liquid goes into the backpack's vessels.</summary>
    private void Unload(Inventory box) { foreach (string item in box.Items.Keys.ToArray()) Carry(box, S.Backpack, null, item, box[item]); }
    private static readonly Dictionary<string, int> NoCost = new();
    /// <summary>
    /// The fuel loading a recipe burns: what the fuel slot holds, or for an empty slot the first fuel the recipe burns
    /// that the backpack has beyond what <paramref name="inputs"/> takes. Says why and gives null when there is none.
    /// </summary>
    private string? FuelFromBackpack(Thing t, RecipeDef r, Dictionary<string, int> inputs, bool say = true) {
        if (t.Burner?.First() is string held) {
            if (r.Fuels.Contains(held)) return held;
            if (say) Say($"The fuel slot holds {Lower(Data.Name(held))}, which {Lower(r.Name)} cannot burn. Take it out first.");
            return null;
        }
        foreach (string fuel in r.Fuels) if (S.Backpack[fuel] > inputs.GetValueOrDefault(fuel)) return fuel;
        if (say) Say(Phrase("Needs ", FuelWords(r.Fuels), " in your backpack for the fire."));
        return null;
    }
    private static readonly List<string> NoFuels = new();
    /// <summary>The fuels the station's recipe burns, in the order workers look for them; empty when it burns none.</summary>
    public List<string> Fuels(Thing t) => Data.Recipes.TryGetValue(t.Recipe, out var r) ? r.Fuels : NoFuels;
    /// <summary>The fuel the next batch burns: what the fuel slot holds while the recipe takes it, or null.</summary>
    public string? FuelOf(Thing t) => t.Burner?.First() is string held && Fuels(t).Contains(held) ? held : null;
    /// <summary>The fuel a station shows: what its fuel slot holds, else the first its recipe burns, else the first any of its recipes burns.</summary>
    public static string ShownFuel(Thing t, GameData data) =>
        t.Burner?.First() ?? (data.Recipes.TryGetValue(t.Recipe, out var r) && r.Burns ? r.Fuels[0] : FirstFuel(t.Kind, data));
    // A plain loop, since the view asks every frame and a lambda would allocate its closure.
    private static string FirstFuel(ThingKind station, GameData data) {
        foreach (var r in data.Recipes.Values) if (r.Station == station && r.Burns) return r.Fuels[0];
        throw new InvalidOperationException($"No recipe at {station.Id()} burns fuel.");
    }
    /// <summary>The fuels a recipe burns as words, such as "coal or charcoal".</summary>
    public string FuelWords(List<string> fuels) => Listed(fuels.Select(f => Lower(Data.Name(f))).ToList(), "or");
    /// <summary>Words as a list read aloud: "a", "a or b", "a, b or c", with <paramref name="last"/> before the last one.</summary>
    private static string Listed(List<string> words, string last) =>
        words.Count <= 1 ? string.Concat(words) : $"{string.Join(", ", words.Take(words.Count - 1))} {last} {words[^1]}";
    // A batch's cost depends only on its recipe, so each is built once.
    private readonly Dictionary<string, Dictionary<string, int>> batchCosts = new();
    /// <summary>One batch's inputs, without the fuel, which has a slot of its own. Shared between stations, so read it without changing it.</summary>
    public Dictionary<string, int> BatchCost(Thing t) => BatchCost(t.Recipe);
    private Dictionary<string, int> BatchCost(string recipe) {
        if (!batchCosts.TryGetValue(recipe, out var cost)) batchCosts[recipe] = cost = Data.Recipes.TryGetValue(recipe, out var r) ? new(r.Inputs) : new();
        return cost;
    }
    /// <summary>Whether the recipe a station is on or the one it keeps to takes an item as an input.</summary>
    internal bool Pending(Thing t, string item) => BatchCost(Selected(t)).ContainsKey(item) || BatchCost(t.Keep).ContainsKey(item);
    /// <summary>
    /// Whether a station's slots hold something left by another recipe: neither an input of the recipe it is on or keeps to, nor what its recipe makes.
    /// It starts no batch until a hauler or a player takes that out.
    /// </summary>
    private bool Blocked(Thing t, RecipeDef r) {
        foreach (var pair in t.Inventory.Items)
            if (!Pending(t, pair.Key) && pair.Key != r.Output) return true;
        return false;
    }
    /// <summary>Whether a station takes an item from the player: an input of the recipe it is on or keeps to, a vessel holding a liquid input, or a fuel it burns.</summary>
    public bool Takes(Thing t, string item) => Pending(t, item) || PoursInto(t, t.Inventory, item) != null || t.Burner != null && FuelFits(t, item);
    /// <summary>Whether the station holds a batch's inputs and, if it burns fuel, one unit of it, with nothing left by another recipe in its slots.</summary>
    internal bool Stocked(Thing t) => !HeldAt(t) && t.Inventory.Has(BatchCost(t)) && (Fuels(t).Count == 0 || FuelOf(t) != null) && !(Data.Recipes.TryGetValue(t.Recipe, out var r) && Blocked(t, r));
    /// <summary>Loads batches of the station's current recipe's inputs and one fuel each from the backpack.</summary>
    public void LoadBatch(Thing t, int batches = 1) {
        if (!Reaches(t)) { Say("Walk closer to load this building."); return; }
        if (Busy() || Forwarded(Verb.LoadBatch, t.Id, count: batches)) return;
        string recipe = t.Recipe;
        var cost = BatchCost(recipe);
        if (cost.Count == 0) { Say("Choose a recipe first."); return; }
        var r = Data.Recipes[recipe];
        string? fuel = null;
        if (r.Burns && (fuel = FuelFromBackpack(t, r, cost)) == null) return;
        for (int i = 0; i < batches; i++) {
            if (!CarriesBatch(cost) || fuel != null && S.Backpack[fuel] < cost.GetValueOrDefault(fuel) + 1) { if (i == 0) Say("Missing batch ingredients or fuel in backpack."); break; }
            if (cost.Any(pair => t.Inventory.SlotRoom(pair.Key) < pair.Value) || fuel != null && (t.Burner!.Room(fuel) == 0 || !FuelTakes(t, fuel))) { Say("Input slots are full."); break; }
            foreach (var pair in cost) Load(t, pair.Key, pair.Value);
            if (fuel != null) S.Backpack.TransferTo(t.Burner!, fuel, 1);
        }
    }
    /// <summary>Manual loading of one input slot, which holds at most one stack.</summary>
    public int LoadInput(Thing t, string item, int quantity) {
        if (!Reaches(t)) { Say("Walk closer to load this building."); return 0; }
        // Fuel nothing smelts goes to the fuel slot; coal at the reinforced furnace fills the input slot, and its fuel slot is filled by putting coal on it.
        if (!Pending(t, item) && t.Burner != null && FuelFits(t, item)) return LoadFuel(t, item, quantity);
        if ((t.Kind == ThingKind.Packer || Barrel.Is(item)) && HoldBlock(t, item) is string refused) { Say(refused); return 0; }
        // A vessel pours what it holds into the station's input slot for its liquid.
        var vessel = PoursInto(t, t.Inventory, item);
        string lands = vessel?.Liquid ?? item;
        if (t.Kind != ThingKind.Packer && !Pending(t, lands)) { Say($"{Data.Name(item)} is not used by what this station makes."); return 0; }
        if (Busy() || Forwarded(Verb.LoadInput, t.Id, item: item, count: quantity)) return 0;
        int moved = Load(t, item, quantity);
        if (moved == 0) Say(Carried(item) == 0 ? $"No {Data.Name(item)} in your backpack." : FullInput(lands));
        return moved;
    }
    private string FullInput(string item) => $"The {Data.Name(item)} slot is full.";

    /// <summary>
    /// Loads units of a station input from the backpack while its input slot has room, and says nothing.
    /// A vessel pours what it holds, and a liquid asked for by name, as when a batch is queued, pours from the backpack's vessels.
    /// </summary>
    private int Load(Thing t, string item, int quantity) =>
        Carry(S.Backpack, t.Inventory, t, item, quantity, t.Inventory.SlotRoom(PoursInto(t, t.Inventory, item)?.Liquid ?? item));
    /// <summary>Manual loading of the fuel slot, which holds one kind of fuel at a time.</summary>
    public int LoadFuel(Thing t, string item, int quantity) {
        if (!Reaches(t)) { Say("Walk closer to load this building."); return 0; }
        if (t.Burner == null) return 0;
        if (FuelBlock(t, item) is string refused) { Say(refused); return 0; }
        if (Busy() || Forwarded(Verb.LoadFuel, t.Id, item: item, count: quantity)) return 0;
        int moved = S.Backpack.TransferTo(t.Burner, item, Math.Min(quantity, t.Burner.StackOf(item) - t.Burner[item]));
        if (moved == 0) Say(S.Backpack[item] == 0 ? $"No {Data.Name(item)} in your backpack." : "The fuel slot is full.");
        return moved;
    }
    public void Collect(Thing t) {
        if (!Reaches(t)) { Say("Walk closer to collect items."); return; }
        if (Busy() || Forwarded(Verb.Collect, t.Id)) return;
        foreach (var item in Leftovers(t).ToArray()) { Carry(t.Inventory, S.Backpack, null, item, 999); SayHeldBehind(t.Inventory, item); }
    }
    /// <summary>Finished goods plus anything neither the recipe the station is on nor the kept one takes.</summary>
    public IEnumerable<string> Leftovers(Thing t) => t.Inventory.Items.Keys.Where(item => !Pending(t, item));
    /// <summary>Bedtime comes a little before night falls, so the player can turn in without waiting for the dark.</summary>
    public const int SleepHour = 22;
    public float SecondsToDawn => Data.DaySeconds - S.Time % Data.DaySeconds + 1;
    public float ThirstAtDawn() => bedtimeWater.Preview(S.Actor, SecondsToDawn * Data.ThirstPerSecond).Dawn;
    /// <summary>Hunger and thirst both above this let sleep heal, at <see cref="SleepHeal"/> health a second.</summary>
    public const float FedNeed = 45, SleepHeal = .12f;
    /// <summary>The acting player's health after sleeping until dawn: sleep heals while hunger and thirst both stay above <see cref="FedNeed"/>.</summary>
    public float HealthAtDawn() {
        float seconds = SecondsToDawn;
        float watered = bedtimeWater.Preview(S.Actor, seconds * Data.ThirstPerSecond).Bedtime;
        float fed = Math.Min(seconds, Math.Min((S.Hunger - FedNeed) / Data.HungerPerSecond, (watered - FedNeed) / Data.ThirstPerSecond));
        return Math.Min(100, S.Health + Math.Max(0, fed) * SleepHeal);
    }
    /// <summary>Why sleeping is not possible now, or null; hunger and thirst must last until morning.</summary>
    public string? SleepBlock() {
        if (Hour is >= 6 and < SleepHour) return "You can only sleep after 10 pm.";
        float hungerLoss = SecondsToDawn * Data.HungerPerSecond, thirstLoss = SecondsToDawn * Data.ThirstPerSecond;
        bool hungry = S.Hunger <= hungerLoss, thirsty = bedtimeWater.Preview(S.Actor, thirstLoss).Bedtime <= thirstLoss;
        if (hungry || thirsty) return hungry && thirsty ? "You are too hungry and thirsty to sleep." : hungry ? "You are too hungry to sleep." : "You are too thirsty to sleep.";
        // The night passes for everyone at once, so the others have to be inside and fed too.
        foreach (var p in S.Players) if (p != S.Actor && p.Sheltered == 0) return "Everyone has to be inside a cabin to sleep.";
        foreach (var p in S.Players)
            if (p != S.Actor && (p.Hunger <= hungerLoss || bedtimeWater.Preview(p, thirstLoss).Bedtime <= thirstLoss)) return "Someone is too hungry or thirsty to sleep.";
        return null;
    }
    public void Sleep(Thing hut) {
        if (hut.Kind != ThingKind.Hut || !Near(hut)) return;
        if (SleepBlock() is string blocked) { Say(blocked); return; }
        if (Forwarded(Verb.Sleep, hut.Id)) return;
        if (DoorCheck.Waking) { PassNight(); return; }
        var occupants = bedtimeWater.Occupants();
        BedtimeWater.ServeThirst(occupants);
        BedtimeWater.FillBottles(occupants);
        PassNight();
        BedtimeWater.ServeThirst(occupants);
    }
    /// <summary>
    /// Runs the world to dawn a second at a time, so time, wages, trades, hunger and production advance together, then clears the wolves.
    /// A wolf that starts scratching at an unlocked cabin door stops the night there and wakes everyone, and no night
    /// starts while a wolf is at such a door.
    /// </summary>
    private void PassNight() {
        float seconds = SecondsToDawn;
        asleep = true;
        try {
            while (seconds > 0 && !DoorCheck.Waking) { float dt = Math.Min(1, seconds); Tick(dt, true); seconds -= dt; }
        } finally {
            asleep = false;
        }
        if (DoorCheck.Waking) { SayToAll("You are awakened by the noise outside."); return; }
        S.Wolves.Clear(); SayToAll(S.Workers.Count > 0 ? "Dawn breaks. Your workers are walking back from Bramblewick." : "Dawn breaks.");
    }
    /// <summary>How far a burning campfire's safety reaches; its light is drawn to the same edge.</summary>
    public const float FireReach = 5.3f;
    /// <summary>Burning campfires keep wolves out of their light, unless a wolf is already chasing the player.</summary>
    public bool Protected(Vector2 p) {
        foreach (var t in S.Things.Of(ThingKind.Campfire)) if (t.Burn > 0 && t.Pos.DistanceTo(p) < FireReach) return true;
        return false;
    }
    /// <summary>Wolves scared by a torch circle the player about this far off, in follow mode, until enough gather to attack together.</summary>
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
    internal bool Torchlit => S.TorchLit && !Downed && S.Sheltered == 0 && !Protected(Player);
    /// <summary>
    /// Lights a torch from the backpack, which stays there while it burns, or swings it once it burns.
    /// A torch put out before, by wild swinging, lights again with the burn time it had left.
    /// </summary>
    public void UseTorch(Vector2 aim) {
        if (S.Sheltered != 0 || S.TorchKindling > 0 || Busy() || Forwarded(Verb.Torch, at: aim)) return;
        if (S.TorchLit) { SwingTorch(aim); return; }
        if (S.Backpack["torch"] == 0) { Say("No torch in your backpack."); return; }
        if (S.Torch <= 0) S.Torch = Data.CampfireSeconds;
        S.TorchKindling = KindleSeconds; PlayerActed?.Invoke(ActorAction.Kindle, Player);
    }
    private void SwingTorch(Vector2 aim) {
        if (attackClock > 0) return;
        torchSwings.RemoveAll(t => S.Time - t > WildWindow); torchSwings.Add(S.Time);
        Vector2 direction = StartSwing(ActorAction.TorchSwing, aim);
        if (torchSwings.Count >= WildSwings) { PutOutTorch(); Say("Torch went off due to wild swinging."); return; }
        TorchFlare = .5f;
        Swings.Start(ActorAction.TorchSwing, direction);
        WolfFollow.Scare(direction);
    }
    /// <summary>
    /// The acting player begins a swing along <paramref name="aim"/>, south when none is given: it shows and whooshes,
    /// and the next has to wait. Returns the way it goes, a unit vector.
    /// </summary>
    private Vector2 StartSwing(ActorAction action, Vector2 aim) {
        attackClock = Swings.Cooldown;
        Vector2 direction = aim == Vector2.Zero ? Vector2.Down : aim.Normalized();
        PlayerActed?.Invoke(action, direction); Play("tool_air_swing", Player);
        return direction;
    }
    public const float StaggerSeconds = .45f;
    /// <summary>How far a blow throws a wolf back: a short hop, which the view plays as the wolf jumping clear.</summary>
    public const float KnockBackDistance = .5f;
    /// <summary>
    /// Applies a blow, interrupts the wolf's attack and starts its reaction, or drops it if the blow kills it.
    /// </summary>
    internal void Strike(Wolf wolf, float damage, bool burnt = false) {
        wolf.Health -= damage;
        wolf.Snap = wolf.Leap = wolf.Recover = wolf.Threaten = wolf.Circle = 0;
        wolf.Route.Clear(); WolfCounter.Clear(wolf);
        if (wolf.Health > 0 && WolfHunt.ReactToHit(wolf, burnt)) {
            Play("wolf_hit", wolf.Pos);
            return;
        }
        KnockBack(wolf);
        if (wolf.Health <= 0) KillWolf(wolf);
    }
    /// <summary>Throws a wolf back from the acting player and leaves it reeling, which stops a snap it had begun.</summary>
    private void KnockBack(Wolf wolf) {
        var away = WolfHunt.AwayFromPlayer(wolf);
        wolf.Pos = Slide(wolf.Pos, away * KnockBackDistance);
        wolf.Heading = (-away).Angle();
        wolf.Stagger = StaggerSeconds;
    }
    /// <summary>Leaves a carcass where a struck wolf fell.</summary>
    private void KillWolf(Wolf wolf) {
        S.Wolves.Remove(wolf); Add(ThingKind.WolfCorpse, wolf.Pos, WolfMeat);
        Say("The wolf falls. Butcher the carcass for meat and cook it in a furnace.");
    }
    private void PutOutTorch() {
        if (S.TorchLit) Play("torch_out", Player);
        S.TorchLit = false; S.TorchKindling = 0; torchSwings.Clear();
        WolfFollow.Stop();
    }
    /// <summary>
    /// Swings the best axe carried, or a fist without one, along <paramref name="aim"/>, south when none is given.
    /// Axe swings take turns: the overhead chop, then the sideways sweep.
    /// </summary>
    public void Attack(Vector2 aim = default) {
        if (attackClock > 0 || S.Sheltered != 0 || Busy() || Forwarded(Verb.Attack, at: aim)) return;
        string? axe = BestTool("axe");
        bool sweep = axe != null && S.Actor.SweepNext;
        S.Actor.SweepNext = axe != null && !sweep;
        ActorAction action = (axe, sweep) switch {
            (null, _) => ActorAction.Attack,
            ("steel_axe", true) => ActorAction.SteelAxeSweep,
            (_, true) => ActorAction.AxeSweep,
            ("steel_axe", false) => ActorAction.SteelAxeSwing,
            _ => ActorAction.AxeSwing
        };
        Swings.Start(action, StartSwing(action, aim), axe == null ? 1 : Speed(axe));
    }
    public void Die() {
        Release();
        // After a mauling the box lands past the feet, behind the wolf standing over the player, so the feeding stays visible.
        // The player falls backwards facing the wolf that leapt, so the feet point back the way it came.
        var killer = S.Mauled ? Hunters.FirstOrDefault(w => w.Pinning) : null;
        var box = Add(ThingKind.Grave, killer == null ? Player : Player + (killer.Pos - Player).Normalized() * 1.5f); box.Inventory = S.Backpack;
        box.Inventory.CarriedBy = Inventory.Carrier.None;
        int cabin = S.Sheltered;
        S.Backpack = new Inventory { Slots = 24, CarriedBy = Inventory.Carrier.Player }; S.RespawnIn = Data.RespawnSeconds; S.Sheltered = 0; S.Torch = 0; PutOutTorch();
        Doorways.UnlockEmpty(cabin);
        // A wolf that pinned the player, or bit them down without a leap, eats every scrap of food.
        bool wolfKill = S.Mauled || Hunters.Any(w => w.Pos.DistanceTo(Player) < LeapRange);
        if (wolfKill) foreach (var item in box.Inventory.Items.Keys.Where(Data.IsFood).ToArray()) box.Inventory.Remove(item, box.Inventory[item]);
        const string ate = "Wolves ate food in your backpack.";
        // A pack that pinned the player stays to feed until they respawn.
        if (S.Mauled) { Say(ate); return; }
        S.Wolves.RemoveAll(w => w.Target == S.Actor.Id);
        Say(wolfKill ? ate : $"You collapsed. Your belongings are in a recovery box where you fell. Respawning in {Data.RespawnSeconds:0} seconds.");
    }
    private void Respawn() {
        // The player wakes inside the cabin with a place left nearest to where they fell, the last one in, or in the clearing without one.
        var cabin = Doorways.NearestFreeCabin(Player);
        if (cabin != null) S.Actor.CabinEntry = S.Actor.DoorQueued = ++S.DoorArrivals;
        S.Health = 100; S.Hunger = 65; S.Thirst = 65; Player = cabin == null ? Terrain.Spawn : Doorways.Doorstep(cabin); S.Actor.Warp++; S.Mauled = false; S.Sheltered = cabin?.Id ?? 0; S.Wolves.RemoveAll(w => w.Target == S.Actor.Id);
        Say(cabin == null ? "You wake in the clearing. Recover your belongings from the box." : "You wake in the cabin. Recover your belongings from the box.");
    }
    // Only sales move demand, and purchases always cost more than the best sale price, so a buy-then-sell round trip never profits.
    public float UnitPrice(string item, bool buying) => Data.Market.Goods[item].Price * (buying ? 1.25f : Math.Min(1, Demand(item)));
    // Demand runs above 1 by the steady amount's worth, so those units sell at the full price; past it, each unit takes a share of a crate's drop.
    private float DemandStep(string item) => Data.Market.Sells[item].Drop / Data.CrateCapacity;
    private float FullDemand(string item) => Data.Market.Sells.TryGetValue(item, out var demand) ? 1 + demand.Steady * DemandStep(item) : 1;
    public float Demand(string item) => S.Demand.TryGetValue(item, out float demand) ? demand : FullDemand(item);
    /// <summary>Units in one purchase of a good the village sells; more than one arrive packed in a crate.</summary>
    public int Lot(string item) => Data.Market.Buys.GetValueOrDefault(item, 1);
    /// <summary>The whole coins one purchase costs, crate included for boxed goods.</summary>
    public int LotPrice(string item) => (int)MathF.Ceiling(1.25f * (Data.Market.Goods[item].Price * Lot(item) + (Lot(item) > 1 ? Data.Market.CratePrice : 0)));
    public const int MarketSlots = 6;
    /// <summary>Tools a tool station holds, one in each slot and any kind in any of them.</summary>
    public const int ToolSlots = 4;
    private static Inventory NewBurner() => new() { Slots = 1 };
    // Stations with a recipe that burns fuel, worked out once from the data.
    private HashSet<ThingKind?>? burning;
    public const int MaxWorkers = 40;
    public const int Carts = 3;
    /// <summary>Whether a kind of building has a fuel slot.</summary>
    public bool Burns(ThingKind kind) => (burning ??= Data.Recipes.Values.Where(r => r.Burns).Select(r => r.Station).ToHashSet()).Contains(kind);
    /// <summary>Whether a station's fuel slot takes an item: what its recipe burns, or without one what any of its recipes burns.</summary>
    public bool FuelFits(Thing t, string item) => Data.Recipes.TryGetValue(t.Recipe, out var r) ? r.Fuels.Contains(item) : AnyRecipeBurns(t.Kind, item);
    // A plain loop, so a check of a station without a recipe allocates no closure.
    private bool AnyRecipeBurns(ThingKind station, string item) {
        foreach (var r in Data.Recipes.Values) if (r.Station == station && r.Fuels.Contains(item)) return true;
        return false;
    }
    private static Inventory NewWageSlot() => new() { Slots = 1, CoinStack = Inventory.UnlimitedCoins, InputFilter = { Inventory.Coins }, OutputFilter = { Inventory.Coins } };
    /// <summary>Why an item cannot go into this box by hand: a wage slot only holds coins, and a tool slot only the tools its rules take.</summary>
    public string? BoxBlock(Thing t, Inventory box, string item) =>
        box == t.PlumbingSlot ? (item == "steel_pipe" ? null : "The Plumbing slot only takes steel pipes.") :
        Barrel.Is(item) && box != t.Inventory && BoxIndex(t, box) >= 0 ? $"Barrel does not fit in {Lower(Data.Name(t.Kind))}." :
        box == t.Wages && item != Inventory.Coins ? "The wage slot only holds coins." :
        box == t.Burner ? FuelBlock(t, item) :
        box == t.Output ? "Things only come out of here." :
        box == t.Inventory ? HoldBlock(t, item) :
        box == t.Tools ? SlotBlock(t, item) : null;
    /// <summary>Whether a fuel slot takes an item now: a fuel that fits, and the same kind as what it holds.</summary>
    internal bool FuelTakes(Thing t, string item) => FuelFits(t, item) && (t.Burner?.First() is not string held || held == item);
    /// <summary>Why a fuel slot will not take an item: it burns only fuel, one kind at a time.</summary>
    public string? FuelBlock(Thing t, string item) =>
        !FuelFits(t, item) ? (Data.Recipes.TryGetValue(t.Recipe, out var r) ? $"{r.Name} burns {FuelWords(r.Fuels)}." : "The fuel slot only takes fuel.") :
        t.Burner?.First() is string held && held != item ? $"The fuel slot holds {Data.Name(held).ToLowerInvariant()}. Take it out first." : null;
    /// <summary>Coins kept for wages in every signpost's wage slot.</summary>
    public int WageCoins {
        get {
            int coins = 0;
            foreach (var t in S.Things.Of(ThingKind.Signpost)) if (t.Wages != null) coins += t.Wages[Inventory.Coins];
            return coins;
        }
    }
    /// <summary>Pays wages from the signposts' wage slots first, then the acting player's backpack, or nothing if together they fall short.</summary>
    private bool PayWage(int amount) {
        if (amount > WageCoins + Coins) return false;
        foreach (var t in S.Things.Of(ThingKind.Signpost)) if (t.Wages != null) amount -= t.Wages.Remove(Inventory.Coins, amount);
        return Pay(amount);
    }
    /// <summary>The village takes any packed crate that holds something; goods it does not buy go as donations.</summary>
    public bool CanSell(string item) => Packed(item) is { Count: > 0 };
    private bool VillageBuys(string item) => Data.Market.Sells.ContainsKey(item);
    /// <summary>The sale price as last reported from the village; it goes stale until someone arrives with news.</summary>
    public float KnownPrice(string item) => S.Prices.TryGetValue(item, out float price) ? price : Data.Market.Goods[item].Price;
    public int PriceAge => Day - (int)(S.PricesTime / Data.DaySeconds) - 1;
    private void UpdatePrices() {
        S.Prices = Data.Market.Sells.Keys.ToDictionary(item => item, item => UnitPrice(item, false)); S.PricesTime = S.Time;
    }
    private string? CartBlock(Thing sign) => !Near(sign) || sign.Kind != ThingKind.Signpost ? "Walk closer to the trading post." : S.Orders.Count >= Carts ? "All three village carts are busy." : null;
    /// <summary>Sends every acceptable crate in the market input on one cart; the coins come back into the market output.</summary>
    public bool Sell(Thing sign) {
        if (CartBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Busy() || Forwarded(Verb.Sell, sign.Id)) return true;
        if (Ship(sign)) return true;
        Say(sign.Inventory.Count > 0 ? "The village only takes packed crates." : "Put a packed crate in the market first."); return false;
    }
    /// <summary>Sends the cart once every market slot holds a crate, for a post set to sell on its own.</summary>
    private void AutoSell(Thing sign) {
        if (sign.AutoSell && sign.Inventory.Room(PackedPrefix) == 0 && S.Orders.Count < Carts) Ship(sign);
    }
    /// <summary>Sets whether the post sends the cart by itself once its market slots are full.</summary>
    public void SetAutoSell(Thing sign, bool on) {
        if (sign.Kind != ThingKind.Signpost || Busy() || Forwarded(Verb.AutoSell, sign.Id, count: on ? 1 : 0)) return;
        sign.AutoSell = on;
    }
    /// <summary>The goods packed in these crates, added up by kind.</summary>
    public Dictionary<string, int> Contents(IEnumerable<string> crates) {
        var goods = new Dictionary<string, int>();
        foreach (var crate in crates)
            if (Packed(crate) is Inventory packed) foreach (var (item, quantity) in packed.Items) goods[item] = goods.GetValueOrDefault(item) + quantity;
        return goods;
    }
    /// <summary>What a sale pays at the given unit prices: every unit of a good the village takes, plus the crates, which go with the goods.</summary>
    public float SaleValue(Dictionary<string, int> goods, int crates, Func<string, float> price) =>
        crates * Data.Market.CratePrice + goods.Where(p => VillageBuys(p.Key)).Sum(p => p.Value * price(p.Key));
    private bool Ship(Thing sign) {
        var crates = sign.Inventory.Items.Keys.Where(CanSell).ToList();
        if (crates.Count == 0) return false;
        var goods = Contents(crates);
        // The whole sale is paid at the pre-sale price, so selling in bulk loses nothing; demand drops after it.
        float price = SaleValue(goods, crates.Count, item => UnitPrice(item, false));
        foreach (var (item, quantity) in goods) if (VillageBuys(item)) S.Demand[item] = Math.Max(.25f, Demand(item) - quantity * DemandStep(item));
        foreach (var crate in crates) { sign.Inventory.Remove(crate, 1); S.Crates.Remove(PackedId(crate)); }
        // Coins are whole: sales round down and purchases round up, so a round trip never profits.
        S.Orders.Add(new TradeOrder { Goods = goods, Value = MathF.Floor(price), Signpost = sign.Id, Crates = crates.Count });
        Say($"Goods sent: {string.Join(", ", goods.Select(p => $"{Data.Name(p.Key)} x {p.Value}"))}."); return true;
    }
    internal string Goods(IEnumerable<KeyValuePair<string, int>> goods) => string.Join(", ", goods.Select(p => $"{p.Value} {Data.Name(p.Key)}"));
    /// <summary>Orders <paramref name="lots"/> purchases of a good the village sells: tools one by one, the rest in crates.</summary>
    public bool Buy(Thing sign, string item, int lots) {
        if (!Data.Market.Buys.ContainsKey(item) || lots <= 0) return false;
        if (CartBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Busy() || Forwarded(Verb.Buy, sign.Id, item: item, count: lots)) return false;
        int price = LotPrice(item) * lots, units = Lot(item) * lots;
        if (!Pay(price)) { Say("Not enough coins in your backpack."); return false; }
        Play("buy_coins", sign.Pos);
        S.Orders.Add(new TradeOrder { Goods = new() { [item] = units }, Buying = true, Value = price, Signpost = sign.Id, Crates = Lot(item) > 1 ? lots : 0 });
        Say($"Ordered {(Lot(item) > 1 ? $"{lots} {(lots == 1 ? "crate" : "crates")} of " : "")}{units} {Data.Name(item)} for {price} coins. Back in {Data.TradeSeconds:0} seconds."); return true;
    }
    // ---------- Crates ----------
    /// <summary>
    /// Packed crates are items named by this prefix and a number that keys their contents in <see cref="SaveState.Crates"/>.
    /// A crate of one good also carries its name in between, such as <c>crate_bread_17</c>, so filters can ask for crates of it.
    /// </summary>
    public const string PackedPrefix = "crate_";
    public static bool IsCrate(string item) => item == "crate" || IsPacked(item);
    /// <summary>Whether an item is a packed crate, one with goods in it.</summary>
    public static bool IsPacked(string item) => item.StartsWith(PackedPrefix, StringComparison.Ordinal);
    /// <summary>Whether an item is a crate, empty or packed, going into a placed crate: no worker carries one there and no filter names one.</summary>
    public static bool Nests(Thing t, string item) => t.Kind == ThingKind.Crate && IsCrate(item);
    /// <summary>The number that keys a packed crate's contents, or 0 for any other item.</summary>
    public static int PackedId(string item) => item.StartsWith(PackedPrefix, StringComparison.Ordinal) && int.TryParse(item.AsSpan(item.LastIndexOf('_') + 1), out int id) ? id : 0;
    /// <summary>The filter entry that stands for any packed crate of one good, such as <c>crate_bread</c>.</summary>
    public static string CrateOf(string item) => PackedPrefix + item;
    /// <summary>The filter entry that stands for any packed crate of mixed goods; only a pile's filter names it.</summary>
    public const string MixedCrate = PackedPrefix;
    /// <summary>The <see cref="CrateOf"/> entry a packed crate of one good matches, <see cref="MixedCrate"/> for a crate of mixed goods, or empty for anything else.</summary>
    public static ReadOnlySpan<char> PackedKind(string item) {
        if (!item.StartsWith(PackedPrefix, StringComparison.Ordinal)) return default;
        int last = item.LastIndexOf('_');
        return item.AsSpan(0, Math.Max(last, PackedPrefix.Length));
    }
    /// <summary>The good a filter entry from <see cref="CrateOf"/> asks crates of, or null for any other entry.</summary>
    public string? CrateGood(string entry) => entry.StartsWith(PackedPrefix) && Data.Items.ContainsKey(entry[PackedPrefix.Length..]) ? entry[PackedPrefix.Length..] : null;
    /// <summary>What a pile's filter names for an item: the kind of crate for a packed crate of one good, a vessel's liquid for a vessel holding some, or the item itself, a <see cref="CrateOf"/> entry included.</summary>
    public string PileEntry(string item) {
        string kindOfContainer = ContainerItem.Base(item);
        if (kindOfContainer is Barrel.Item or Bucket.Item) return kindOfContainer;
        if (CrateGood(item) != null) return item;
        var kind = PackedKind(item);
        if (!kind.IsEmpty) return new string(kind);
        return Data.VesselOf(item) is VesselDef v && item != v.Empty ? v.Liquid : item;
    }
    /// <summary>The items a pile's filter lists for a <see cref="PileEntry"/>: every fill of a vessel that holds its liquid, or the entry itself.</summary>
    private IEnumerable<string> PileItems(string entry) => entry == Barrel.Item ? Barrel.Fills : entry == Bucket.Item ? Bucket.Fills
        : Data.VesselFor(entry) is VesselDef v ? v.States.Skip(1) : new[] { entry };
    /// <summary>The <see cref="PileEntry"/> a pile's filter names, or null while it names none.</summary>
    public string? PileFilter(Thing pile) {
        foreach (string item in pile.Inventory.InputFilter) return PileEntry(item);
        return null;
    }
    /// <summary>Whether a pile is a plain one, whose filter names one resource, rather than compost.</summary>
    public static bool IsPlainPile(Thing t) => t.Kind == ThingKind.Pile && !t.Compost;
    public static bool IsSingleResourceStore(Thing t) => IsPlainPile(t) || t.Kind == ThingKind.Barrel;
    /// <summary>What a filter entry names: an item, a crate of a good, or a crate of mixed goods.</summary>
    public string FilterName(string entry) => entry == MixedCrate ? "Mixed crate" : CrateGood(entry) is string good ? Phrase("Crate of ", Lower(Data.Name(good))) : Data.Name(entry);
    /// <summary>The contents of a packed crate item, or null for anything else.</summary>
    public Inventory? Packed(string item) => S.Crates.GetValueOrDefault(PackedId(item));
    public Inventory NewCrate() => new() { Slots = Data.CrateCapacity, StackSize = Data.CrateCapacity, Capacity = Data.CrateCapacity };
    /// <summary>Packs contents into a new crate item.</summary>
    private string Pack(Inventory contents) {
        int id = S.NextId++; S.Crates[id] = contents;
        return contents.Items.Count == 1 && contents.First() is string good ? $"{PackedPrefix}{good}_{id}" : PackedPrefix + id;
    }
    /// <summary>An item's name, with what a packed crate holds.</summary>
    public string Describe(string item) => Packed(item) is Inventory contents ? $"{Data.Name("crate")} of {Goods(contents.Items)}" : Data.Name(item);
    /// <summary>The packed crate with this number in the acting player's backpack, or null once it has gone or been emptied.</summary>
    public string? CarriedCrate(int id) {
        foreach (var pair in S.Backpack.Items) if (PackedId(pair.Key) == id) return pair.Key;
        return null;
    }
    /// <summary>
    /// Moves items between a packed crate in the backpack and the backpack itself, then names the crate after what it holds, keeping its number.
    /// An emptied crate becomes a plain empty crate.
    /// </summary>
    public int MoveInCrate(int id, string item, int quantity, bool packing) {
        if (Downed) return 0;
        if (packing && Barrel.Is(item)) { Say("Barrel does not fit in wood crate."); return 0; }
        if (packing && IsCrate(item)) { Say("Never put a crate in a crate."); return 0; }
        if (id == 0) return packing ? PackEmptyCrate(item, quantity) : 0;
        if (CarriedCrate(id) is not string crate || Packed(crate) is not Inventory contents) return 0;
        if (Busy() || Forwarded(Verb.MoveInCrate, id, packing ? 1 : 0, item, quantity)) return 0;
        int moved = packing ? S.Backpack.TransferTo(contents, item, quantity) : contents.TransferTo(S.Backpack, item, quantity);
        if (moved == 0) { Say(packing ? "The crate is full." : "Your backpack is full."); return 0; }
        S.Backpack.Replace(crate, contents.Count == 0 ? "crate" : contents.Items.Count == 1 && contents.First() is string good ? $"{PackedPrefix}{good}_{id}" : PackedPrefix + id);
        if (contents.Count == 0) S.Crates.Remove(id);
        return moved;
    }
    /// <summary>Packs goods from the backpack into one of its empty crates, which becomes a packed crate; what <see cref="MoveInCrate"/> does with crate number 0.</summary>
    private int PackEmptyCrate(string item, int quantity) {
        if (S.Backpack["crate"] == 0) { Say("No empty crate in your backpack."); return 0; }
        if (Busy() || Forwarded(Verb.MoveInCrate, 0, 1, item, quantity)) return 0;
        var contents = NewCrate();
        int moved = S.Backpack.TransferTo(contents, item, quantity);
        if (moved == 0) return 0;
        S.Backpack.Remove("crate", 1);
        string packed = Pack(contents);
        if (S.Backpack.Add(packed, 1) == 1) return moved;
        S.Crates.Remove(PackedId(packed)); S.Backpack.Add("crate", 1); contents.TransferTo(S.Backpack, item, moved);
        Say("Your backpack is full.");
        return 0;
    }
    /// <summary>Puts a crate from the backpack, or the held crate, down, still holding whatever it was packed with.</summary>
    public Thing? PlaceCrate(string item, Vector2 p, float rotation = 0) {
        if (!IsCrate(item)) return null;
        if (Construction.PlacementBlock(ThingKind.Crate, p, item, rotation) is string blocked) { Say(blocked); return null; }
        if (Busy() || Forwarded(Verb.PlaceCrate, item: item, count: S.Backpack.PreferredSlot, at: p, rotation: rotation)) return null;
        // A crate put down from the held stack comes from wherever it is held, such as a chest; any other from the backpack.
        (MarkedSource ?? S.Backpack).Remove(item, 1);
        var crate = Add(ThingKind.Crate, p, rotation: rotation); ClearGround(crate);
        if (Packed(item) is Inventory contents) { ReleaseIn(contents); crate.Inventory = contents; S.Crates.Remove(PackedId(item)); }
        Play("close_chest", p); return crate;
    }
    /// <summary>Lifts a placed crate into the backpack; anything inside stays packed in it.</summary>
    public bool PickUpCrate(Thing crate) {
        if (crate.Kind != ThingKind.Crate || !Near(crate)) return false;
        bool empty = crate.Inventory.Count == 0;
        if (S.Backpack.Room(empty ? "crate" : PackedPrefix) == 0) { Say("Backpack full. Make room for the crate."); return false; }
        if (Busy() || Forwarded(Verb.PickUpCrate, crate.Id)) return true;
        if (HeldByOthers(crate)) return false;
        S.Backpack.Add(empty ? "crate" : Pack(crate.Inventory), 1); S.Things.Remove(crate);
        Play("wood_rocks_inventory", crate.Pos); PlayerActed?.Invoke(ActorAction.PickUp, crate.Pos); return true;
    }
    // ---------- Crafting ----------
    /// <summary>Hand recipes for the personal crafting menu.</summary>
    public IEnumerable<string> HandRecipes => Data.Recipes.Where(p => p.Value.Hand).Select(p => p.Key);
    public Thing? PlaceBarrel(string item, Vector2 at, float rotation = 0) {
        if (!Barrel.Is(item)) return null;
        if (Construction.PlacementBlock(ThingKind.Barrel, at, item, rotation) is string blocked) { Say(blocked); return null; }
        if (Busy() || Forwarded(Verb.PlaceBarrel, item: item, count: S.Backpack.PreferredSlot, at: at, rotation: rotation)) return null;
        return barrels.Place(item, at, MarkedSource ?? S.Backpack, rotation);
    }

    private static string? CarryBarrelBlock(Inventory backpack, string item) =>
        Barrel.IsFilled(item) && Barrel.FilledIn(backpack) != null ? "You can only carry one barrel with water." : null;

    public bool PickUpBarrel(Thing barrel) {
        if (barrel.Kind != ThingKind.Barrel || !Near(barrel) || Find(barrel.Id) != barrel) return false;
        string item = Barrel.AtFill(Barrels.WaterIn(barrel));
        if (CarryBarrelBlock(S.Backpack, item) is string blocked) { Say(blocked); return false; }
        if (S.Backpack.Room(item) == 0) { Say("Backpack full. Make room for the barrel."); return false; }
        if (Busy() || Forwarded(Verb.PickUpBarrel, barrel.Id)) return true;
        if (HeldByOthers(barrel)) return false;
        S.Backpack.Add(item, 1);
        if (barrel.PlumbingSlot is Inventory plumbingSlot && plumbingSlot.Count > 0)
            Construction.GiveBack(new Dictionary<string, int> { ["steel_pipe"] = plumbingSlot["steel_pipe"] }, barrel.Pos);
        S.Things.Remove(barrel);
        Play("wood_rocks_inventory", barrel.Pos);
        PlayerActed?.Invoke(ActorAction.PickUp, barrel.Pos);
        return true;
    }

    /// <summary>Pours into one barrel in the named backpack cell, or draws water into carried vessels.</summary>
    public int MoveInBarrel(string barrelItem, string item, int quantity, bool puttingIn, int barrelSlot = -1) {
        int before = Barrel.Liters(barrelItem);
        if (before < 0 || quantity <= 0) return 0;
        var backpack = S.Backpack;
        if (barrelSlot == -1) barrelSlot = backpack.Layout.FindIndex(cell => cell.Item == barrelItem && cell.Held == 0);
        if ((uint)barrelSlot >= (uint)Math.Min(backpack.Layout.Count, backpack.Slots)) return 0;
        var barrel = backpack.Layout[barrelSlot];
        if (barrel.Item != barrelItem || barrel.Held > 0) return 0;
        if (puttingIn && !Barrels.CanPour(Data, item)) { Say("The barrel only holds water."); return 0; }
        if (!puttingIn && item != Water.Liquid) return 0;
        if (puttingIn && before == 0) {
            if (CarryBarrelBlock(backpack, Barrel.AtFill(1)) is string blocked) { Say(blocked); return 0; }
            if (!backpack.CanSplitBarrelStack(barrelSlot)) { Say("Make room in your backpack to fill a barrel."); return 0; }
        }
        if (Busy() || Forwarded(Verb.MoveInBarrel, before, puttingIn ? 1 : 0, item, quantity, new Vector2(backpack.PreferredSlot, barrelSlot))) return 0;
        var tank = Barrels.NewTank();
        tank.Add(Water.Liquid, before);
        if (puttingIn) {
            if (Data.VesselOf(item) is not VesselDef vessel) return 0;
            PourVessels(S.Backpack, item, tank, vessel, MarkedSource == S.Backpack ? Math.Min(quantity, heldCount) : quantity, tank.Room(Water.Liquid));
        } else {
            if (FillBlock(tank, S.Backpack, item) is string dry) { Say(dry); return 0; }
            Carry(tank, S.Backpack, null, item, quantity);
        }
        int after = tank[Water.Liquid];
        if (before != after) backpack.SetBarrelFill(barrelSlot, Barrel.AtFill(after));
        if (before == after && puttingIn && tank.Room(Water.Liquid) == 0) Say("The barrel is full.");
        return Math.Abs(after - before);
    }

    private enum CraftRefusal { None, StationOnly, Busy, NoTool, NoInputs, QueueFull }
    /// <summary>The one place the hand-crafting rules live; <see cref="CanCraft"/> and <see cref="CraftBlock"/> both read it.</summary>
    private CraftRefusal CraftCheck(string recipe) {
        if (!Data.Recipes.TryGetValue(recipe, out var r) || !r.Hand) return CraftRefusal.StationOnly;
        if (Occupied) return CraftRefusal.Busy;
        if (r.Tool != "" && BestTool(r.Tool, null, r.Tier) == null) return CraftRefusal.NoTool;
        if (!S.Backpack.Has(r.Inputs)) return CraftRefusal.NoInputs;
        return Waiting(Crafting, recipe) >= MaxQueued ? CraftRefusal.QueueFull : CraftRefusal.None;
    }
    /// <summary>Whether a hand recipe can be queued now, without building the reason; the crafting page asks on every refresh.</summary>
    public bool CanCraft(string recipe) => CraftCheck(recipe) == CraftRefusal.None;
    /// <summary>Why a recipe cannot be queued from the backpack now, or null.</summary>
    public string? CraftBlock(string recipe) => CraftCheck(recipe) switch {
        CraftRefusal.StationOnly => "This can only be made at a station.",
        CraftRefusal.Busy => "You are busy.",
        CraftRefusal.NoTool => $"Needs a {Data.ToolName(Data.Recipes[recipe].Tool, Data.Recipes[recipe].Tier)} in your backpack.",
        CraftRefusal.NoInputs => $"Needs {Goods(Data.Recipes[recipe].Inputs)} in your backpack.",
        CraftRefusal.QueueFull => $"{MaxQueued} of these are already queued.",
        _ => null
    };
    /// <summary>Queues batches of a hand recipe while the backpack holds their inputs, which leave it now so they cannot be spent twice; false when none queued.</summary>
    public bool Craft(string recipe, int count = 1) {
        if (CraftBlock(recipe) is string blocked) { Say(blocked); return false; }
        if (Busy() || Forwarded(Verb.Craft, item: recipe, count: count)) return false;
        for (int i = 0; i < count && CanCraft(recipe); i++) { S.Backpack.Spend(Data.Recipes[recipe].Inputs); Requeue(S.Crafting, recipe, 1); }
        return true;
    }
    /// <summary>Takes up to <paramref name="count"/> waiting batches of a hand recipe off the queue and returns their inputs; the one being made stays.</summary>
    public void CancelCraft(string recipe, int count = 1) {
        if (count <= 0 || Waiting(Crafting, recipe) == 0 || Busy() || Forwarded(Verb.CancelCraft, item: recipe, count: count)) return;
        int removed = Requeue(S.Crafting, recipe, -count);
        foreach (var (item, quantity) in Data.Recipes[recipe].Inputs) Give(item, quantity * removed);
    }
    private void Give(string item, int quantity) {
        int left = quantity - S.Backpack.Add(item, quantity);
        if (left <= 0) return;
        var box = Add(ThingKind.Grave, Player); box.Inventory.Slots = left / box.Inventory.StackOf(item) + 1; box.Inventory.Add(item, left);
    }
    /// <summary>The hand recipe being made, else the first waiting, or null.</summary>
    private RecipeDef? NextCraft => S.Making != "" ? Data.Recipes[S.Making] : S.Crafting.Count > 0 ? Data.Recipes[S.Crafting[0].Recipe] : null;
    /// <summary>How fast hand crafting progresses: the carried tool's speed, or zero while its tool is missing or nothing is queued.</summary>
    public float CraftRate => NextCraft is not RecipeDef r || Downed ? 0 : r.Tool != "" ? BestTool(r.Tool, null, r.Tier) is string held ? Speed(held) : 0 : 1;
    /// <summary>Seconds a player stands still before hand crafting goes on, so walking never crafts between steps.</summary>
    public const float CraftSettle = .25f;
    private void CraftTick(float dt) {
        float rate = CraftRate;
        if (rate <= 0 || playerBusy || S.Actor.Still < CraftSettle) return;
        // Work starts on the first waiting recipe, which then leaves the queue and can no longer be taken off.
        if (S.Making == "") { S.Making = S.Crafting[0].Recipe; Requeue(S.Crafting, S.Making, -1); S.CraftTime = 0; }
        var r = Data.Recipes[S.Making];
        S.CraftTime = Math.Min(r.Seconds, S.CraftTime + dt * rate);
        // A full backpack holds the finished item back rather than losing it.
        if (S.CraftTime < r.Seconds || S.Backpack.Room(r.Output) < r.Amount) return;
        S.Backpack.Add(r.Output, r.Amount); S.Making = ""; S.CraftTime = 0;
        S.Milestones.Add(r.Output); Play(Sounds.ForItem(r.Output), Player);
    }
    /// <summary>The tool slots a station has, each with the tool it takes; none for anything that is not a building.</summary>
    public ToolSlotRule[] SlotRules(ThingKind kind) => Data.Buildings.TryGetValue(kind, out var building) ? building.ToolSlots : [];
    /// <summary>The tool of a kind slotted in a station, or null when its slot is empty or holds a tool below <paramref name="tier"/>.</summary>
    public string? BenchTool(Thing bench, string tool, int tier = 0) => bench.Tools == null ? null : BestTool(tool, bench.Tools, tier);
    /// <summary>
    /// Why a station's tool slots will not take an item: no slot takes it, or the slot that does already holds a tool;
    /// null when it goes in.
    /// </summary>
    public string? SlotBlock(Thing station, string item) {
        var def = Data.Items.GetValueOrDefault(item);
        var slots = SlotRules(station.Kind);
        int at = def == null ? -1 : Array.FindIndex(slots, slot => slot.Takes(def));
        if (at < 0) return $"The {Lower(Data.Name(station.Kind))} has slots for {Listed(slots.Select(slot => A(Data.ToolName(slot.Tool, slot.Tier))).ToList(), "and")}.";
        string tool = slots[at].Tool;
        return BenchTool(station, tool) != null ? $"The {tool} slot is taken. Take that {tool} out first." : null;
    }
    /// <summary>
    /// Puts a tool from the backpack into the station's empty slot for its kind, when <see cref="SlotBlock"/> allows it.
    /// A tool too plain for its kind's slot just stays in the backpack, with nothing said.
    /// </summary>
    public bool SlotTool(Thing bench, string item) {
        if (bench.Tools == null || !Reaches(bench)) { Say($"Walk closer to the {Lower(Data.Name(bench.Kind))}."); return false; }
        if (Data.Items.GetValueOrDefault(item) is ItemDef def && Array.Exists(SlotRules(bench.Kind), slot => slot.Tool == def.Tool && !slot.Takes(def))) return false;
        if (SlotBlock(bench, item) is string block) { Say(block); return false; }
        if (Busy() || Forwarded(Verb.SlotTool, bench.Id, item: item)) return false;
        return S.Backpack.TransferTo(bench.Tools, item, 1) == 1;
    }
    public void UnslotTool(Thing bench, string item) {
        if (bench.Tools == null || !Reaches(bench)) { Say($"Walk closer to the {Data.Name(bench.Kind).ToLowerInvariant()}."); return; }
        if (Busy() || Forwarded(Verb.UnslotTool, bench.Id, item: item)) return;
        if (bench.Tools.TransferTo(S.Backpack, item, 1) == 0 && bench.Tools[item] > 0) Say("Backpack full.");
    }
    public string? FetchBlock(Thing sign) {
        bool assigned = false, working = false;
        foreach (var w in S.Workers) if (HasDuty(w, sign.Id, JobKind.Deliveries)) { assigned = true; working |= w.WageDebt <= 0; }
        if (!assigned) return "No worker assigned.";
        if (Dusk) return "Your workers have signed out until morning.";
        if (Lunch) return "Your workers are at lunch.";
        foreach (var w in S.Workers) if (HasDuty(w, sign.Id, JobKind.Deliveries) && w.Away > 0) return Phrase(w.Name, " is already on the way.");
        return working ? null : "The assigned worker is on strike.";
    }
    /// <summary>Sends the post's worker to the village; the price sheet refreshes when they return.</summary>
    public bool FetchPrices(Thing sign) {
        if (FetchBlock(sign) is string blocked) { Say(blocked); return false; }
        if (Busy() || Forwarded(Verb.FetchPrices, sign.Id)) return false;
        var w = S.Workers.First(w => HasDuty(w, sign.Id, JobKind.Deliveries) && w.WageDebt <= 0);
        w.Assignment = sign.Id; w.Away = Data.TradeSeconds; Say($"{w.Name} set off to Bramblewick for fresh prices."); return true;
    }
    private static readonly string[] WorkerNames = {
        "Rowan", "Hazel", "Milo", "Fern", "Jasper", "Willow", "Clover", "Ash", "Bramble", "Linden", "Sorrel", "Tansy", "Alder", "Juniper",
        "Basil", "Briar", "Cedar", "Dahlia", "Elm", "Flint", "Heath", "Iris", "Laurel", "Moss", "Nettle", "Oakley", "Pip", "Quill",
        "Reed", "Sage", "Thistle", "Wren", "Yarrow", "Birch", "Cress", "Marigold", "Rue", "Teasel", "Holly", "Bryony" };
    public Worker? Hire(Thing sign, HireOffer offer) {
        if (!Near(sign) || sign.Kind != ThingKind.Signpost || Busy() || Forwarded(Verb.Hire, sign.Id, count: Data.HireOffers.IndexOf(offer))) return null;
        if (S.Workers.Count >= MaxWorkers) { Say("The POC supports forty workers."); return null; }
        // Hiring is paid from the bank first, like wages; purchases stay paid from the backpack.
        if (!PayWage(offer.Upfront)) { Say($"This offer needs {offer.Upfront:0} coins up front from the bank or your backpack."); return null; }
        string name = WorkerNames.FirstOrDefault(n => S.Workers.All(w => w.Name != n)) ?? "Neighbor";
        var w = new Worker { Id = S.NextId++, Name = name, Pos = sign.Pos + new Vector2(1, 0), Wage = offer.Wage };
        S.Workers.Add(w); Play("hire_sign", sign.Pos); Say($"New worker {name} is ready for a job."); Made?.Invoke(S.Actor.Id, w.Id); return w;
    }
    /// <summary>Workers with any job at a place.</summary>
    public int Crew(Thing t) {
        int crew = 0;
        foreach (var w in S.Workers) if (HasDuty(w, t.Id)) crew++;
        return crew;
    }
    /// <summary>The place a worker's job is at: the place itself, or for a gather job whose spot is gone, a stand-in of its kind.</summary>
    public Thing? DutyPlace(Worker w, int place) {
        if (Find(place) is Thing t) return t;
        foreach (var d in w.Duties) if (d.Thing == place && d.Kind is ThingKind kind) return new Thing { Id = place, Kind = kind };
        return null;
    }
    private void Started(Worker w, Thing? target) {
        // Reassigning calls off a price errand, so the worker never freezes on the road.
        w.Assignment = target?.Id ?? 0; w.Away = 0; w.WorkState = WorkerWorkState.Idle;
        w.Destination = 0; w.Timer = 0; w.Status = target == null ? "Available" : "Starting work";
    }
    public void Fire(Worker w) {
        if (Busy() || Forwarded(Verb.Fire, other: w.Id)) return;
        ReleaseIn(w.Bag); ReleaseIn(w.ToolSlot);
        if (w.Bag.Count > 0 || w.Tool != "") {
            var box = Add(ThingKind.Grave, w.Pos);
            box.Inventory = w.Bag;
            box.Inventory.CarriedBy = Inventory.Carrier.None;
            box.Inventory.Capacity = 0;
            box.Inventory.Slots = box.Inventory.UsedSlots + 1;
            box.Inventory.Add(w.Tool, w.Tool == "" ? 0 : 1);
        }
        S.Workers.Remove(w); Say($"{w.Name} has left. Carried items were left in a recovery box.");
    }
    private enum HoldRefusal { None, ToolsOnly, CompostOnly, OtherResource, FarmOnly, PackerOnly, SellOnly, WoodOnly, Barrel, LiquidOnly, BarrelOnly }
    /// <summary>A tool station holds tools and water vessels, a pile one resource, compost what rots and a campfire wood.</summary>
    private HoldRefusal HoldCheck(Thing t, string item) => t.Kind switch {
        ThingKind.Hut when Barrel.Is(item) => HoldRefusal.None,
        _ when Barrel.Is(item) && (t.Kind != ThingKind.Pile || t.Compost) => HoldRefusal.Barrel,
        ThingKind.Barrel when Barrels.CanPour(Data, item) => HoldRefusal.None,
        ThingKind.Barrel => HoldRefusal.LiquidOnly,
        ThingKind.Hut => HoldRefusal.BarrelOnly,
        ThingKind.Toolstation when ToolKind(item) == "" => HoldRefusal.ToolsOnly,
        ThingKind.Pile when t.Compost && !Compostable(item) && item != "fertilizer" => HoldRefusal.CompostOnly,
        ThingKind.Pile when !t.Compost && !PileTakes(t.Inventory, item) => HoldRefusal.OtherResource,
        ThingKind.Farm when !Data.Farm.Sow.ContainsKey(item) && !Barrels.CanPour(Data, item) => HoldRefusal.FarmOnly,
        ThingKind.Packer when item != "crate" && item != t.Packs => HoldRefusal.PackerOnly,
        ThingKind.Signpost when !CanSell(item) => HoldRefusal.SellOnly,
        ThingKind.Campfire when item != "wood" => HoldRefusal.WoodOnly,
        _ => HoldRefusal.None
    };
    /// <summary>Whether storage takes an item, without building the reason; workers ask on every tick.</summary>
    public bool Holds(Thing t, string item) => HoldCheck(t, item) == HoldRefusal.None;
    /// <summary>Why storage will not take an item, or null.</summary>
    public string? HoldBlock(Thing t, string item) => HoldCheck(t, item) switch {
        HoldRefusal.Barrel => $"Barrel does not fit in {Lower(Data.Name(t.Kind))}.",
        HoldRefusal.LiquidOnly => "The barrel only holds water.",
        HoldRefusal.BarrelOnly => "The cabin slot only holds a steel barrel.",
        HoldRefusal.ToolsOnly => "The tool station only holds tools, buckets and bottles.",
        HoldRefusal.CompostOnly => "The compost takes leaves, grass and food.",
        HoldRefusal.OtherResource => Phrase("This is ", A(PileName(t)!), " pile."),
        HoldRefusal.PackerOnly => t.Packs == "" ? "Choose what to pack first." : $"The packing station takes {Data.Name(t.Packs).ToLowerInvariant()} and empty crates.",
        HoldRefusal.SellOnly => "The village only takes packed crates.",
        HoldRefusal.WoodOnly => "The campfire burns only wood.",
        HoldRefusal.FarmOnly => $"A farm plot takes {string.Join(" and ", Data.Farm.Sow.Keys.Select(Data.Name)).ToLowerInvariant()}, and water.",
        _ => null
    };
    /// <summary>Whether a pile takes an item: what its filter names, or with no filter yet, what it holds or anything while empty.</summary>
    private bool PileTakes(Inventory pile, string item) {
        if (pile.InputFilter.Count > 0) return pile.Wants(item);
        return pile.First() is not string held || PileEntry(held) == PileEntry(item);
    }
    /// <summary>The resource a plain pile is for, in lower case, as its filter names it or, until the next tick sets the filter, as it holds it.</summary>
    public string? PileName(Thing pile) => (PileFilter(pile) ?? (pile.Inventory.First() is string held ? PileEntry(held) : null)) is string entry ? Lower(FilterName(entry)) : null;
    public bool Compostable(string item) => item is "leaves" or "grass" || Data.IsFood(item);
    /// <summary>Turns a pile into compost or back; refused while it holds something the new use would not take.</summary>
    public bool SetCompost(Thing pile, bool on) {
        if (pile.Kind != ThingKind.Pile || pile.Compost == on) return false;
        if (pile.Inventory.Items.Count > (on ? 0 : 1)) { Say(on ? "Empty the pile before composting." : "Empty the compost down to one resource first."); return false; }
        if (on && pile.Inventory.InputFilter.Count > 0) { Say("Take the pile's filter off before composting."); return false; }
        if (Busy() || Forwarded(Verb.SetCompost, pile.Id, count: on ? 1 : 0)) return true;
        pile.Compost = on; pile.Progress = 0;
        // Compost starts out taking everything that rots and giving fertilizer; its filters are then the player's to change.
        pile.Inventory.InputFilter.Clear(); pile.Inventory.OutputFilter.Clear();
        if (on) { foreach (var item in compostables ??= Data.Items.Keys.Where(Compostable).ToArray()) pile.Inventory.InputFilter.Add(item); pile.Inventory.OutputFilter.Add("fertilizer"); }
        PileFilters(pile); return true;
    }
    private string[]? compostables;
    /// <summary>The first resource put in an unfiltered pile sets its filters, in and out; they then stay, emptied or not, until the player takes them off. Compost keeps the filters it was given.</summary>
    internal void PileFilters(Thing pile) {
        if (pile.Compost || pile.Inventory.InputFilter.Count > 0 || pile.Inventory.First() is not string item) return;
        SetPileFilter(pile, PileEntry(item));
    }
    /// <summary>Makes a pile's filters, in and out, name the items of one <see cref="PileEntry"/>.</summary>
    private void SetPileFilter(Thing thing, string entry) {
        var pile = thing.Inventory;
        pile.InputFilter.Clear(); pile.OutputFilter.Clear();
        foreach (string item in thing.Kind == ThingKind.Barrel ? new[] { Water.Liquid } : PileItems(entry)) { pile.InputFilter.Add(item); pile.OutputFilter.Add(item); }
        pile.Changed?.Invoke();
    }
    /// <summary>Compost turns 4 units of whatever it holds into 1 fertilizer each in-game hour.</summary>
    public const int CompostBatch = 4;
    private void CompostTick(Thing pile, float dt) {
        int rotting = 0;
        foreach (var pair in pile.Inventory.Items) if (Compostable(pair.Key)) rotting += pair.Value;
        if (rotting < CompostBatch || pile.Inventory.Room("fertilizer") == 0) { pile.Progress = 0; return; }
        // Like a station, compost holding a stack a player picked up makes nothing until it is let go of.
        if (HeldAt(pile)) return;
        pile.Progress += dt;
        if (pile.Progress < Data.HourSeconds) return;
        pile.Progress = 0;
        for (int left = CompostBatch; left > 0;) left -= pile.Inventory.Remove(pile.Inventory.Items.Keys.First(Compostable), left);
        pile.Inventory.Add("fertilizer", 1); Got("fertilizer");
    }
    // Only chests, piles and placed crates take part in automation; recovery boxes hold the player's or a dismissed worker's belongings.
    internal static readonly ThingKind[] Stores = { ThingKind.Chest, ThingKind.Pile, ThingKind.Crate };
    // Trading posts also take deliveries, of the crates they ask for, and packing stations of the good they pack.
    private static readonly ThingKind[] Depots = { ThingKind.Chest, ThingKind.Pile, ThingKind.Crate, ThingKind.Signpost, ThingKind.Packer };
    /// <summary>Where a load of an item goes: the nearest place asking for it, else the nearest storage with no filter, and compost last for food.</summary>
    public Thing? DepositFor(string item, Vector2 origin, int exclude = 0) => Deposit(item, origin, exclude, askersOnly: false, storesTake: true);
    /// <summary>
    /// The nearest place asking for an item that a chest's or crate's hauler carries out to, or null.
    /// Storage with no filter does not count, and other storage only while this one does not ask for the item too, so two that ask never pass it back and forth.
    /// </summary>
    internal Thing? AskerFor(string item, Thing store) => Deposit(item, store.Pos, store.Id, askersOnly: true, storesTake: !store.Inventory.Wants(item));
    /// <param name="askersOnly">Only places asking for the item count, not storage with no filter nor compost.</param>
    /// <param name="storesTake">Chests, piles and placed crates count; without them only trading posts, packing stations and places that use the item do.</param>
    private Thing? Deposit(string item, Vector2 origin, int exclude, bool askersOnly, bool storesTake) {
        Thing? best = null; int bestRank = 0; float bestDistance = 0;
        foreach (var kind in Depots) {
            if (!storesTake && Array.IndexOf(Stores, kind) >= 0) continue;
            foreach (var t in S.Things.Of(kind)) {
                if (t.Id == exclude) continue;
                // Compost names every food in its filter, but takes food only when nothing else will, so harvests and picked berries are not left to rot.
                int rank = t.Compost && Data.IsFood(item) ? 2 : kind == ThingKind.Packer || t.Inventory.Wants(item) ? 0 : 1;
                if (askersOnly && rank > 0 || Intake(t, item, false) == null) continue;
                float distance = t.Pos.DistanceSquaredTo(origin);
                if (best == null || rank < bestRank || rank == bestRank && distance < bestDistance) { best = t; bestRank = rank; bestDistance = distance; }
            }
        }
        // A station, farm plot or mine site that uses the item takes it as storage asking for it does: its input slots, its fuel slot, the materials it lacks.
        foreach (var kind in stationKinds) Users(kind);
        Users(ThingKind.Farm); Users(ThingKind.Mine);
        return best;

        void Users(ThingKind kind) {
            foreach (var t in S.Things.Of(kind)) {
                // Distance first: asking a station what it takes costs more than a nearer place already found saves.
                float distance = t.Pos.DistanceSquaredTo(origin);
                if (t.Id == exclude || best != null && bestRank == 0 && distance >= bestDistance || Intake(t, item, false) == null) continue;
                best = t; bestRank = 0; bestDistance = distance;
            }
        }
    }
    /// <summary>The nearest chest, pile or placed crate that gives out an item, or station holding it as a finished good, or null.</summary>
    public Thing? SourceFor(string item, Vector2 origin, int exclude = 0) {
        Thing? best = null; float bestDistance = 0;
        foreach (var kind in Stores) Nearer(kind, false);
        foreach (var kind in stationKinds) Nearer(kind, true);
        return best;

        void Nearer(ThingKind kind, bool station) {
            foreach (var t in S.Things.Of(kind)) {
                if (t.Id == exclude || t.Inventory[item] <= 0 || !t.Inventory.AllowsWithdrawal(item) || station && Pending(t, item)) continue;
                float distance = t.Pos.DistanceSquaredTo(origin);
                if (best == null || distance < bestDistance) { best = t; bestDistance = distance; }
            }
        }
    }
    /// <summary>The nearest thing of a kind, or null.</summary>
    public Thing? Nearest(ThingKind kind, Vector2 origin) => Nearest(new ReadOnlySpan<ThingKind>(in kind), origin);
    /// <summary>The nearest thing of any of these kinds, or null.</summary>
    internal Thing? Nearest(ReadOnlySpan<ThingKind> kinds, Vector2 origin) {
        Thing? best = null; float bestDistance = 0;
        foreach (var kind in kinds)
            foreach (var t in S.Things.Of(kind)) {
                float distance = t.Pos.DistanceSquaredTo(origin);
                if (best == null || distance < bestDistance) { best = t; bestDistance = distance; }
            }
        return best;
    }
    private readonly WalkSearch walkSearch;
    private const float RouteRetrySeconds = 1;
    private static readonly float[] Swerves = { 0, MathF.PI / 4, -MathF.PI / 4, MathF.PI / 2, -MathF.PI / 2 };
    /// <summary>
    /// A step toward <paramref name="to"/> on ground a player can stand on.
    /// A route runs between open tiles, so its straight legs can clip the shore or something built: the step then swerves round it, or holds still.
    /// A walker already on blocked ground, such as one built over, may step anywhere to get out.
    /// </summary>
    private Vector2 StepAround(Vector2 from, Vector2 to, float step) {
        var ahead = from.MoveToward(to, step);
        if (ahead == from || !CanStand(from)) return ahead;
        var heading = ahead - from;
        foreach (float swerve in Swerves) if (CanStand(from + heading.Rotated(swerve))) return from + heading.Rotated(swerve);
        return from;
    }
    /// <summary>How near a worker counts as at a place: the reach of a walk, and how near a closer spot they cannot get to counts as there.</summary>
    internal const float WalkReach = 1.2f;
    internal const string BlockedPath = "Path blocked · clear a route";
    /// <summary>
    /// Moves a worker toward a destination and reports arrival within <paramref name="reach"/>.
    /// The usual reach counts as arrival when the closer spot is inaccessible, unless the job needs that exact spot.
    /// </summary>
    internal bool Walk(Worker w, Vector2 destination, float dt, float reach = WalkReach, bool exact = false) {
        float distance = w.Pos.DistanceTo(destination);
        if (distance < reach) return true;
        // Stop at arrival range rather than overshooting into the target's footprint, where the step would be refused.
        float step = dt * WorkerSpeed * GravelPaths.SpeedAt(w.Pos) * (Barrel.In(w.Bag) is string barrel ? Barrel.Speed(barrel) : 1);
        if (Travel(w, destination, Math.Min(step, distance - reach + .05f), step, exact: exact)) return false;
        if (!exact && distance < WalkReach) return true;
        w.Status = BlockedPath; w.WorkState = WorkerWorkState.Blocked;
        return false;
    }
    /// <summary>
    /// Moves a walker toward <paramref name="destination"/> over ground a player can stand on: up to <paramref name="direct"/> straight at it while that way is clear,
    /// otherwise <paramref name="step"/> along a route around what blocks it, found again once the destination has moved more than <paramref name="repath"/>.
    /// False when no way leads there.
    /// </summary>
    internal bool Travel(IWalker walker, Vector2 destination, float direct, float step, float repath = .2f, bool exact = false) {
        if (walker.RouteTarget.DistanceTo(destination) > repath) { walker.Route.Clear(); walker.RouteTarget = destination; walker.RouteRetry = 0; }
        var next = walker.Pos.MoveToward(destination, direct);
        if (walker.Route.Count == 0 && CanStand(next)) { walker.Pos = next; return true; }
        if (walker.Route.Count > 0 && !CanStand(walker.Route[0])) walker.Route.Clear();
        // A search covers the whole map when no way leads there, so a failed one waits a while before the next.
        if (walker.Route.Count == 0 && S.Time >= walker.RouteRetry) {
            if (walkSearch.Begin(walker.Pos, destination, walker.Route, exact) == WalkSearch.Result.Searching)
                walkSearch.Continue(walker.Route, int.MaxValue);
            if (walker.Route.Count == 0) walker.RouteRetry = S.Time + RouteRetrySeconds;
        }
        if (walker.Route.Count == 0) return false;
        var along = StepAround(walker.Pos, walker.Route[0], step);
        if (exact && along == walker.Pos && step > 0 && walker.Pos.DistanceSquaredTo(walker.Route[0]) >= WalkSearch.WaypointReach * WalkSearch.WaypointReach) {
            walker.Route.Clear(); walker.RouteRetry = S.Time + RouteRetrySeconds;
            return false;
        }
        walker.Pos = along;
        if (walker.Pos.DistanceTo(walker.Route[0]) < WalkSearch.WaypointReach) walker.Route.RemoveAt(0);
        return true;
    }
    private void WorkerTick(Worker w, float dt) {
        w.WorkState = WorkerWorkState.Idle;
        w.LunchHeading = 0;
        if (barrelHaul.FinishAction(w, dt)) return;
        if (w.Away > 0) {
            w.Away = Math.Max(0, w.Away - dt); w.Status = Phrase("Fetching village prices · back in ", Number(w.Away), "s");
            // Like a cart, the runner walks a little way down the valley and fades from view rather than pathing around the woods.
            if (Find(w.Assignment) is { Kind: ThingKind.Signpost } post) w.Pos = post.Pos + Vector2.One + Terrain.Seaward * TripDistance * TripLeg(w);
            if (w.Away == 0) { UpdatePrices(); Say($"{w.Name} is back with fresh prices from Bramblewick."); }
            return;
        }
        // Like a cart, the walk home goes a little way down the valley while the worker fades out, and back at dawn, over half a trade trip in all.
        float half = Data.TradeSeconds / 2;
        if (w.Home && Dusk) { w.Status = "Signed out · back in the morning"; return; }
        if (!Dusk && w.Commute > 0) {
            w.Home = false; w.Status = "Walking in from Bramblewick";
            float step = Math.Min(dt, w.Commute), before = TripLeg(w);
            w.Commute -= step; w.Pos -= Terrain.Seaward * TripDistance * (before - TripLeg(w));
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
                var post = Nearest(ThingKind.Signpost, w.Pos);
                if (!Walk(w, post?.Pos + Vector2.One ?? Terrain.Spawn, dt) && w.WorkState != WorkerWorkState.Blocked) return;
                w.WorkState = WorkerWorkState.Idle;
            }
            float step = Math.Min(dt, half - w.Commute), before = TripLeg(w);
            w.Commute += step; w.Pos += Terrain.Seaward * TripDistance * (TripLeg(w) - before);
            if (w.Commute >= half) { w.Home = true; w.Status = "Signed out · back in the morning"; }
            return;
        }
        if (w.WageDebt > 0) { w.Status = "On strike · wages owed"; return; }
        if (Lunch) { lunchBreak.Sit(w, dt); return; }
        TidyDuties(w);
        WorkJobs(w, dt);
    }
    /// <summary>
    /// A post's runner fetches prices on request and carries what the cart delivered, other than coins, to storage.
    /// </summary>
    private bool ManageDeliveries(Worker w, Thing sign, float dt) {
        if (sign.Output != null)
            foreach (var pair in sign.Output.Items) {
                if (pair.Key == Inventory.Coins || DepositFor(pair.Key, sign.Pos) is not Thing dest) continue;
                if (!HandsFree(w, dt)) return true;
                w.Status = Phrase("Carrying in ", Data.Name(pair.Key));
                if (Walk(w, sign.Pos + Vector2.One, dt)) { sign.Output.TransferTo(w.Bag, pair.Key, Worker.BagStack); w.Destination = dest.Id; }
                return true;
            }
        w.Status = "Minding the trading post"; return false;
    }
    /// <summary>A rack's stocker brings tools it takes from storage until none are left there or the rack is full.</summary>
    private bool StockTools(Worker w, Thing rack, float dt) {
        foreach (var kind in Stores)
            foreach (var t in S.Things.Of(kind))
                foreach (var pair in t.Inventory.Items) {
                    if (!Data.Items.TryGetValue(pair.Key, out var def) || def.Tool == "" || !t.Inventory.AllowsWithdrawal(pair.Key) || !rack.Inventory.Accepts(pair.Key) || rack.Inventory.Room(pair.Key) == 0) continue;
                    if (!HandsFree(w, dt)) return true;
                    w.Status = Phrase("Fetching ", A(Lower(def.Name)), " for the rack");
                    if (Walk(w, t.Pos, dt)) { t.Inventory.TransferTo(w.Bag, pair.Key, 1, true); w.Destination = rack.Id; }
                    return true;
                }
        w.Status = "Rack stocked"; return false;
    }
    /// <summary>A compost's tender carries its fertilizer to storage and brings what it takes from storage, food only when it allows food deliveries.</summary>
    private bool TendCompost(Worker w, Thing pile, float dt) {
        if (!pile.Compost) { w.Status = "Nothing to tend"; return false; }
        if (pile.Inventory["fertilizer"] > 0 && pile.Inventory.AllowsWithdrawal("fertilizer") && DepositFor("fertilizer", pile.Pos, pile.Id) is Thing store) {
            if (!HandsFree(w, dt)) return true;
            w.Status = "Carrying out fertilizer";
            if (Walk(w, pile.Pos, dt)) { pile.Inventory.TransferTo(w.Bag, "fertilizer", w.Bag.StackSize, true); w.Destination = store.Id; }
            return true;
        }
        foreach (var kind in Stores)
            foreach (var t in S.Things.Of(kind)) {
                if (t == pile || t.Compost) continue;
                foreach (var pair in t.Inventory.Items) {
                    if (!Compostable(pair.Key) || pile.SkipFood && Data.IsFood(pair.Key) || !t.Inventory.AllowsWithdrawal(pair.Key) || !pile.Inventory.Accepts(pair.Key) || pile.Inventory.Room(pair.Key) == 0) continue;
                    if (!HandsFree(w, dt)) return true;
                    w.Status = Phrase("Fetching ", Data.Name(pair.Key), " for the compost");
                    if (Walk(w, t.Pos, dt)) { t.Inventory.TransferTo(w.Bag, pair.Key, w.Bag.StackSize, true); w.Destination = pile.Id; }
                    return true;
                }
            }
        w.Status = "Compost tended"; return false;
    }
    /// <summary>
    /// Moves a gatherer whose spot is spent or gone to the nearest spot of the same job that any player has seen, even one now under fog, and tells whether there was one.
    /// With none known they say so once, and their status says it.
    /// </summary>
    internal bool NextSpot(Worker w, int spent, ThingKind kind, Vector2 from) {
        if (NearestSpot(w, kind, from) is Thing next) { MoveTo(w, spent, next); return true; }
        if (w.Exhausted != kind) { w.Exhausted = kind; Say($"{w.Name} knows no other {Lower(Data.Name(kind))} to gather."); }
        w.Status = Phrase("No other known ", Lower(Data.Name(kind)));
        return false;
    }
    /// <summary>
    /// The nearest known spot with stock left, of the job that gathering <paramref name="of"/> is, which the worker has no duty on.
    /// Spots that need a tool count only while the worker has one or a tool station does, and loose pieces come after every other spot.
    /// </summary>
    private Thing? NearestSpot(Worker w, ThingKind of, Vector2 from) {
        if (GatherRules.Of(of)?.JobName is not string job) return null;
        Thing? next = null, loose = null; float nearest = 0, nearestLoose = 0;
        foreach (var kind in Kinds.All) {
            if (GatherRules.Of(kind)?.JobName != job || !CanWork(w, kind)) continue;
            foreach (var t in S.Things.Of(kind)) {
                if (t.Remaining <= 0 || !S.Remembered.ContainsKey(t.Id) || HasDuty(w, t.Id)) continue;
                float distance = t.Pos.DistanceSquaredTo(from);
                if (t.IsLoose) { if (loose == null || distance < nearestLoose) { loose = t; nearestLoose = distance; } } else if (next == null || distance < nearest) { next = t; nearest = distance; }
            }
        }
        return next ?? loose;
    }
    /// <summary>Whether a worker can gather a kind of resource: it needs no tool, or one they have in hand or can take from a tool station.</summary>
    internal bool CanWork(Worker w, ThingKind kind) => GatherRules.Of(kind)?.Needs is not string tool || HasTool(w, tool);
    /// <summary>Moves a worker's duties on a spent spot to the next one.</summary>
    internal static void MoveTo(Worker w, int spent, Thing next) {
        foreach (var duty in w.Duties) if (duty.Thing == spent) duty.Thing = next.Id;
        w.Assignment = next.Id; w.Timer = 0; w.Exhausted = null;
    }
    private static bool Crowds(Worker w) => !w.Home && w.Commute == 0 && w.Away == 0 && w.WorkState is not (WorkerWorkState.Underground or WorkerWorkState.Resting or WorkerWorkState.Perching);
    private void WaitAtPost(Worker w, float dt) => Walk(w, Nearest(ThingKind.Signpost, w.Pos)?.Pos + Vector2.One ?? Terrain.Spawn, dt);
    /// <summary>Walks a tool the job no longer needs to the nearest tool station with room, and reports when it is put back; with no such station the worker keeps it.</summary>
    private bool ReturnTool(Worker w, float dt) {
        Thing? station = null; float nearest = 0;
        foreach (var t in S.Things.Of(ThingKind.Toolstation))
            if (Holds(t, w.Tool) && t.Inventory.Room(w.Tool) > 0 && t.Inventory.Accepts(w.Tool) && (station == null || t.Pos.DistanceSquaredTo(w.Pos) < nearest)) { station = t; nearest = t.Pos.DistanceSquaredTo(w.Pos); }
        if (station == null) return true;
        w.Status = Phrase("Putting back the ", Lower(Data.Name(w.Tool)));
        if (!Walk(w, station.Pos, dt)) return false;
        // A tool a player holds on their cursor stays in hand until they let go of it.
        return w.ToolSlot.TransferTo(station.Inventory, w.Tool, 1) > 0;
    }
    // The tool kinds tool stations give out; a tool station's contents or filters changing marks it out of date, and it is worked out again when next asked.
    private readonly HashSet<string> stationTools = new();
    private bool toolsChanged = true;
    internal void ToolsChanged() => toolsChanged = true;
    /// <summary>Has a tool station's contents and filters report to the tool kinds stations give out.</summary>
    private void WatchTools(Thing station) { station.Inventory.Changed = ToolsChanged; toolsChanged = true; }
    /// <summary>The rack's best free tool of a kind when its filter lets it out, or null.</summary>
    private string? ToolGivenOut(Thing station, string kind) {
        var items = station.Inventory;
        if (kind is not (BarrelHaul.WaterTool or Bucket.Item)) return BestTool(kind, items, tier: 0, requireFree: true) is string best && items.AllowsWithdrawal(best) ? best : null;
        string? bottle = null;
        foreach (var (item, _) in items.Items) {
            if (!BarrelHaul.Vessel(item) || !items.AllowsWithdrawal(item) || items.Free(item) == 0) continue;
            if (Bucket.Is(item)) return item;
            bottle ??= item;
        }
        return kind == BarrelHaul.WaterTool ? bottle : null;
    }
    /// <summary>Whether any tool station gives out a tool of a kind.</summary>
    internal bool StationsHold(string kind) {
        if (toolsChanged) {
            stationTools.Clear();
            foreach (var t in S.Things.Of(ThingKind.Toolstation))
                foreach (var (item, _) in t.Inventory.Items) {
                    string role = BarrelHaul.Vessel(item) ? BarrelHaul.WaterTool : ToolKind(item);
                    if (role != "" && !stationTools.Contains(role) && ToolGivenOut(t, role) != null) stationTools.Add(role);
                    if (Bucket.Is(item) && !stationTools.Contains(Bucket.Item) && ToolGivenOut(t, Bucket.Item) != null) stationTools.Add(Bucket.Item);
                }
            toolsChanged = false;
        }
        return stationTools.Contains(kind);
    }
    /// <summary>The nearest tool station a worker may take a tool of a kind from, or null.</summary>
    private (Thing Station, string Item)? ToolStationWith(string kind, Vector2 from) {
        if (!StationsHold(kind)) return null;
        (Thing Station, string Item)? closest = null; float nearest = 0;
        foreach (var t in S.Things.Of(ThingKind.Toolstation))
            if (ToolGivenOut(t, kind) is string item && (closest == null || t.Pos.DistanceSquaredTo(from) < nearest)) { closest = (t, item); nearest = t.Pos.DistanceSquaredTo(from); }
        return closest;
    }
    /// <summary>Takes the best tool of a kind from the nearest tool station holding one; without one, the worker says once what they need and waits.</summary>
    private void FetchTool(Worker w, string kind, Thing target, float dt) {
        if (ToolStationWith(kind, w.Pos) is not (Thing station, string item)) {
            w.Status = Phrase("Needs ", A(kind), " to ", Task(target));
            if (w.Missing != kind) { w.Missing = kind; Say($"{w.Name} needs {A(kind)} to {Task(target)}. Put one in a tool station."); }
            return;
        }
        w.Missing = "";
        string tool = kind == BarrelHaul.WaterTool ? Lower(Data.Name(item)) : kind == Bucket.Item ? Lower(Data.Name(Bucket.Item)) : kind;
        w.Status = Phrase("Fetching ", A(tool), " from the tool station");
        if (!Walk(w, station.Pos, dt)) return;
        station.Inventory.TransferTo(w.ToolSlot, item, 1, true);
    }
    public void PayWages() {
        foreach (var w in S.Workers) {
            // A striking worker does not work, so no further wages are added until the strike is settled.
            if (w.WageDebt > 0) continue;
            if (!PayWage(w.Wage)) { float due = w.WageDebt = w.Wage; Say($"{w.Name} is on strike: {due:0} coins owed. Pay or dismiss them at the trading post."); }
        }
    }
    public void SettleDebt(Worker w) {
        if (Busy() || Forwarded(Verb.Settle, other: w.Id)) return;
        if (!PayWage((int)w.WageDebt)) { Say("Not enough coins in the wage slot or your backpack to settle wages."); return; }
        w.WageDebt = 0;
    }
    /// <summary>
    /// Whether a wolf is fighting or hunting within six metres, including one hunting another player.
    /// </summary>
    internal bool WolfNearby(Vector2 at) {
        const float distance = 6;
        foreach (var wolf in S.Wolves)
            if (wolf.Target != 0 && wolf.Pos.DistanceSquaredTo(at) < distance * distance) return true;
        return false;
    }

    /// <summary>The wolves hunting the acting player.</summary>
    internal IEnumerable<Wolf> Hunters => S.Wolves.Where(w => w.Target == S.Actor.Id);
    // Reused copies of lists the tick walks while it may change them, so ticking allocates nothing.
    private readonly List<Thing> ticking = new();
    private readonly List<PlayerState> acting = new();
    private readonly List<Worker> crew = new();
    private readonly List<Wolf> hunting = new();
    private readonly List<TradeOrder> arriving = new();
    private readonly List<string> demanded = new();
    /// <summary>Unloads a cart back from the village; kept apart from the tick loop, whose carts on the road must not allocate.</summary>
    private void Arrive(TradeOrder order) {
        // The cart unloads into the market output; whatever does not fit waits in a box beside the signpost.
        var sign = Find(order.Signpost);
        var output = sign?.Output ?? S.Backpack;
        var goods = order.Buying ? order.Goods : new() { [Inventory.Coins] = (int)order.Value - (sign?.Wages?.Add(Inventory.Coins, (int)order.Value) ?? 0) };
        // Boxed goods come back as packed crates of one purchase each.
        if (order.Buying && order.Crates > 0)
            goods = order.Goods.SelectMany(p => Enumerable.Range(0, order.Crates).Select(_ => { var box = NewCrate(); box.Add(p.Key, p.Value / order.Crates); return Pack(box); })).ToDictionary(id => id, _ => 1);
        if (order.Buying) foreach (string item in order.Goods.Keys) Got(item);
        foreach (var (item, quantity) in goods) {
            int overflow = quantity - output.Add(item, quantity);
            if (overflow <= 0) continue;
            var delivery = Add(ThingKind.Grave, sign?.Pos + Vector2.One ?? Terrain.Spawn);
            delivery.Inventory.Slots = overflow / delivery.Inventory.StackOf(item) + 1;
            delivery.Inventory.Add(item, overflow);
        }
        if (!order.Buying) {
            if (order.Goods.Keys.Any(VillageBuys)) S.Milestones.Add("sold");
        }
        // Played at the player, not the signpost, so a payment is heard wherever the player is.
        if (!order.Buying) { Play("payment_from_village_arrives", Player); S.DayRevenue += (int)order.Value; }
        S.Orders.Remove(order); UpdatePrices();
        if (order.Buying) { Say($"Cart returned with {Goods(order.Goods)}."); return; }
        var donated = order.Goods.Where(p => !VillageBuys(p.Key)).ToList();
        Say($"Cart returned with {(int)order.Value} coins.{(donated.Count > 0 ? $" Donated {Goods(donated)}." : "")}");
    }
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
        // A wolf whose player left the game goes back to patrolling.
        foreach (var wolf in S.Wolves) if (wolf.Target != 0 && PlayerById(wolf.Target) == null) WolfPatrols.Patrol(wolf, wolf.Heading);
        acting.Clear(); acting.AddRange(S.Players);
        foreach (var p in acting) using (ActAs(p)) NeedsTick(dt, sleeping);
        KeepHolds();
        BindWaitingHotbars();
        if (Day > S.WageDay) {
            // Workers on strike are not charged, so their wage does not count against the day.
            int wages = S.Workers.Where(w => w.WageDebt <= 0).Sum(w => w.Wage);
            PayWages(); CloseDay(wages); S.WageDay = Day; Say(Phrase("Day ", Number(Day), "."));
            foreach (var leaves in S.Things.Of(ThingKind.Leaves)) leaves.Remaining = LeafPatch;
        }
        if (S.Workers.Count > 0 && Dusk && !wasDusk) Say("Dusk. Workers head home until morning.");
        if (S.Workers.Count > 0 && Lunch && !wasLunch) Say("Midday. Your workers take an hour for lunch.");
        // A day restores what selling the daily amount takes, so selling it every day keeps the price steady.
        demanded.Clear(); demanded.AddRange(S.Demand.Keys);
        foreach (var item in demanded) {
            if (!Data.Market.Sells.TryGetValue(item, out var demand)) continue;
            S.Demand[item] = Mathf.MoveToward(S.Demand[item], FullDemand(item), dt * demand.Daily * DemandStep(item) / Data.DaySeconds);
        }
        arriving.Clear(); arriving.AddRange(S.Orders);
        foreach (var order in arriving) {
            order.Elapsed += dt;
            if (order.Elapsed >= Data.TradeSeconds) Arrive(order);
        }
        S.Things.RemoveAll(ThingKind.Grave, t => t.Inventory.Count == 0);
        S.Things.RemoveAll(ThingKind.WolfCorpse, t => t.Remaining <= 0);
        // Only kinds that change by themselves tick, so trees and rocks cost nothing; a tick that can add or remove things walks a copy.
        // A carcass keeps its age so the death animation plays once and not again after loading.
        foreach (var t in S.Things.Of(ThingKind.WolfCorpse)) t.Progress = Math.Min(60, t.Progress + dt);
        foreach (var t in Ticking(ThingKind.Mine)) Mines.Tick(t, dt);
        foreach (var t in Ticking(ThingKind.Barrel)) PileFilters(t);
        foreach (var t in Ticking(ThingKind.Pile)) { PileFilters(t); if (t.Compost) CompostTick(t, dt); }
        foreach (var t in Ticking(ThingKind.Farm)) Farms.Tick(t, dt);
        foreach (var t in Ticking(ThingKind.Packer)) PackTick(t, dt);
        plumbing.Tick(dt);
        foreach (var t in Ticking(ThingKind.Signpost)) AutoSell(t);
        // A bush fruits again its hours after the first berry was picked, whether or not it was picked clean.
        // The moment is set once, so a bush waiting to fruit changes nothing from tick to tick and sends no updates.
        foreach (var t in S.Things.Of(ThingKind.Berries)) {
            if (t.Remaining >= BushBerries) continue;
            if (t.Regrow == 0) t.Regrow = S.Time + Data.BerryRegrowSeconds;
            else if (S.Time >= t.Regrow) { t.Remaining = BushBerries; t.Regrow = 0; }
        }
        foreach (var t in S.Things.Of(ThingKind.Campfire)) {
            t.Burn = Math.Max(0, t.Burn - dt);
            if (t.Burn < 30 && t.Inventory.Remove("wood", 1) > 0) { if (t.Burn <= 0) Play("start_fire", t.Pos); t.Burn += Data.CampfireSeconds; }
        }
        // Once a working crew has the forge hot, they keep it at or above the clear bar between strokes.
        // A crew that is striking or stuck on a blocked path is not working, so the forge cools.
        // So is a crew that has signed out or is at lunch.
        foreach (var t in S.Things.Of(ThingKind.Reinforced)) t.Heat = Math.Max(Crewed(t) && t.Heat >= SteelHeat ? SteelHeat : 0, t.Heat - dt * 3.2f);
        foreach (var kind in stationKinds) foreach (var t in Ticking(kind)) StationTick(t, dt);
        acting.Clear(); acting.AddRange(S.Players);
        foreach (var p in acting) using (ActAs(p)) CraftTick(dt);
        lunchBreak.Prepare();
        crew.Clear(); crew.AddRange(S.Workers);
        foreach (var w in crew) WorkerTick(w, dt);
        for (int i = 0; i < S.Workers.Count; i++) for (int j = i + 1; j < S.Workers.Count; j++) {
            var a = S.Workers[i]; var b = S.Workers[j]; var offset = a.Pos - b.Pos;
            // Miners underground are inside the hill, so they never crowd anyone at the entrance; workers on the walk to or from the village keep to their way.
            if (Crowds(a) && Crowds(b) && offset.LengthSquared() < .64f) {
                var push = (offset.LengthSquared() > .001f ? offset.Normalized() : Vector2.Right) * Math.Min(dt, .08f);
                if (CanStand(a.Pos + push)) a.Pos += push;
                if (CanStand(b.Pos - push)) b.Pos -= push;
            }
        }
        // Wolves patrol on through a slept night, so one can come to a sleeper's door; a tick that only skips ahead
        // leaves them where they are.
        if (!Dusk) { S.Wolves.Clear(); S.DensOut = 0; S.DenHours = 0; S.Howled = false; } else if (!sleeping || asleep) WolfPatrols.TickDens(dt);
        acting.Clear(); acting.AddRange(S.Players);
        foreach (var p in acting)
            using (ActAs(p)) {
                if (!sleeping) Doorways.Tick(dt);
                if (!Dusk) {
                    if (!Dead) S.Mauled = false;
                } else if (!sleeping || asleep && Doorways.Indoors(p)) {
                    // Through a slept night, wolves that hunted a sleeper go to their cabin's door; one sleeping out in the open is left alone.
                    hunting.Clear();
                    foreach (var w in S.Wolves) if (w.Target == p.Id) hunting.Add(w);
                    WolfHunt.TickWolves(dt, hunting);
                }
                if (!Dead && S.Health <= 0) Die();
                p.Still = playerBusy ? 0 : p.Still + dt;
                playerBusy = false;
            }
        everyone = false;
        Look();
        UpdateGoal();
    }
    /// <summary>Starts, fuels and finishes the batches of a station's recipe.</summary>
    private void StationTick(Thing t, float dt) {
        if (!Data.Recipes.TryGetValue(t.Recipe, out var recipe)) return;
        bool wasBurning = stillBurning.Remove(t.Id);
        bool hot = t.Kind != ThingKind.Reinforced || t.Heat >= SteelHeat;
        bool atAnvil = !Worked(t) || Hammered(t);
        // A recipe at a station with tool slots needs its tool slotted or carried by a player working there, and the tool's speed sets the pace.
        string? tool = atAnvil ? StationTool(t, recipe) : null;
        if (t.Tools != null && recipe.Tool != "" && tool == null) atAnvil = false;
        float rate = WorkRate(t.Kind, tool);
        if (!t.Processing && hot && atAnvil && Stocked(t)) {
            t.Inventory.Spend(BatchCost(t)); t.Making = t.Recipe; t.Processing = true; t.Progress = 0;
            if (t.Batches > 0) t.Batches--;
            if (FuelOf(t) is string fuel) t.Burner!.Remove(fuel, 1);
            if (recipe.Burns && !wasBurning) Play("start_fire", t.Pos);
        }
        if (t.Processing && !hot) {
            // A cold forge mid-batch keeps burning coal from its slot at the batch fuel rate, but the steel waits for heat.
            // Burn is the campfire's fuel clock, reused here to time the next coal.
            t.Burn = Math.Min(recipe.Seconds, t.Burn + dt);
            if (t.Burn >= recipe.Seconds && FuelOf(t) is string burning && t.Burner!.Remove(burning, 1) > 0) t.Burn = 0;
        }
        if (t.Processing && hot && atAnvil) {
            t.Progress = Math.Min(recipe.Seconds, t.Progress + dt * rate);
            if (t.Progress >= recipe.Seconds) {
                // Manual loading or another hauler can fill the station while a batch cooks.
                // Retain the completed batch until the one-stack output slot has room rather than discarding output.
                if (t.Inventory.SlotRoom(recipe.Output) < recipe.Amount) return;
                t.Inventory.Add(recipe.Output, recipe.Amount); t.Processing = false; t.Making = ""; t.Progress = 0; t.Rounds++; stillBurning.Add(t.Id);
                S.Milestones.Add(recipe.Output);
            }
        }
    }
    private List<Thing> Ticking(ThingKind kind) {
        ticking.Clear();
        foreach (var t in S.Things.Of(kind)) ticking.Add(t);
        return ticking;
    }
    public PlayerState? PlayerById(int id) {
        foreach (var p in S.Players) if (p.Id == id) return p;
        return null;
    }
    private bool Crewed(Thing t) {
        foreach (var w in S.Workers) if (AtBellows(w, t)) return true;
        return false;
    }
    /// <summary>How many times a stomach has growled by the time its food has fallen this low.</summary>
    private static float GrowlsBy(float hunger) => MathF.Ceiling((GameData.GrowlBelow - hunger) / GameData.GrowlEvery);
    /// <summary>
    /// The acting player's clocks, hunger and thirst, and the torch in hand; a remote player's held gather also runs here.
    /// Health comes back only while asleep with hunger and thirst both above <see cref="FedNeed"/>.
    /// </summary>
    private void NeedsTick(float dt, bool sleeping) {
        attackClock = Math.Max(0, attackClock - dt);
        Swings.Tick(dt);
        if (S.Actor.Id != LocalId && Find(S.Actor.GatherTarget) is { IsLoose: false } held) Gather(held, dt);
        if (Dead) { S.RespawnIn = Math.Max(0, S.RespawnIn - dt); if (!Dead) Respawn(); } else {
            float drain = !asleep && S.Actor.Sprinting && CanSprint ? Data.SprintDrain : 1, fed = S.Hunger;
            S.Hunger = Math.Max(0, S.Hunger - dt * drain * Data.HungerPerSecond); S.Thirst = Math.Max(0, S.Thirst - dt * drain * Data.ThirstPerSecond);
            if (S.Hunger < GameData.GrowlBelow && GrowlsBy(S.Hunger) > GrowlsBy(fed)) Play("stomach_growl", Player);
            if (S.Hunger <= 0 || S.Thirst <= 0) S.Health -= dt * 1.4f;
            else if (sleeping && S.Hunger > FedNeed && S.Thirst > FedNeed) S.Health = Math.Min(100, S.Health + dt * SleepHeal);
        }
        TorchFlare = Math.Max(0, TorchFlare - dt);
        if (S.TorchKindling > 0 && (S.TorchKindling -= dt) <= 0) {
            S.TorchKindling = 0;
            if (!Downed && S.Torch > 0) { S.TorchLit = true; Play("start_fire", Player); Say($"Torch lit: {S.Torch / Data.DaySeconds * 24:0.#} hours of light left."); }
        }
        // Burning out uses the torch up; moving it out of the backpack puts it out, and it goes as a fresh one.
        if (S.TorchLit && (S.Torch -= dt) <= 0) { S.Torch = 0; PutOutTorch(); S.Backpack.Remove("torch", 1); Say("Your torch burned out."); } else if (S.Torch > 0 && S.Backpack["torch"] == 0) { bool lit = S.TorchLit || S.TorchKindling > 0; S.Torch = 0; PutOutTorch(); if (lit) Say("Your torch went out as it left the backpack."); }
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
            if (!S.Remembered.TryGetValue(t.Id, out var memory)) S.Remembered[t.Id] = memory = new Thing { Id = t.Id, Kind = t.Kind, Rotation = t.Rotation };
            memory.RememberedPlumbing = t.PlumbingInstalled;
            memory.Pos = t.Pos; memory.Remaining = t.Remaining; memory.Keep = t.Keep; memory.Progress = t.Progress; memory.MineStage = t.MineStage; memory.FarmStage = t.FarmStage; memory.OfTree = t.OfTree;
        }
    }
    public bool Explored(Vector2 p) => p.X >= 0 && p.Y >= 0 && p.X < Terrain.Size && p.Y < Terrain.Size && S.Explored[(int)p.Y * Terrain.Size + (int)p.X] != 0;
    /// <summary>The starter goals in order: each one's title, the hint under it in the pause menu, and when it is met.</summary>
    private static readonly (string Title, string Hint, Func<World, bool> Met)[] StarterGoals = {
        ("Light in the dark", "Build and light a campfire. Fire keeps wolves away.", w => w.Reached("campfire")),
        ("Safe place", "Build a cabin for a peace of mind.", w => w.Reached("hut")),
        ("A start", "Build a crafting bench.", w => w.Reached("bench")),
        ("Road to riches", "Build a trading post and sell a crate of goods.", w => w.Reached("sold")),
        ("Stone and embers", "Build a stone furnace.", w => w.Reached("furnace")),
        ("Hotter than fire", "Build a reinforced furnace and produce your first steel bar.", w => w.Reached("steel_bar")),
        // Met by a steel hammer and a steel pickaxe the settlement has had, bought or made.
        ("Steel tools", "Purchase a hammer and a pickaxe from the trading post.", w => w.HadItself("steel_hammer") && w.HadItself("steel_pick")),
        ("Too soft", "Build an anvil and produce your first steel parts.", w => w.Reached("steel_parts")),
        ("More hands on deck", "Hire a worker at the trading post and assign a job.", w => w.Reached("assigned")),
    };
    public bool GoalComplete => S.Goal >= StarterGoals.Length;
    private void UpdateGoal() {
        while (!GoalComplete && StarterGoals[S.Goal].Met(this)) S.Goal++;
    }
    public static int GoalCount => StarterGoals.Length;
    public static (string Title, string Hint) ObjectiveFor(int goal) => (StarterGoals[goal].Title, StarterGoals[goal].Hint);
    // ---------- End goals ----------
    /// <summary>Finished days in a row, up to today, whose profit reached the goal.</summary>
    public int ProfitStreak => S.Profits.AsEnumerable().Reverse().TakeWhile(p => p >= Data.Goals.DailyProfit).Count();
    /// <summary>Once reached, the week of profit stays reached even if later days fall short.</summary>
    public bool ProfitGoalMet => S.Milestones.Contains("profit_week");
    // The milestone comes first: it is a set lookup, and the game checks this every frame.
    public bool EndGoalsComplete {
        get {
            if (!ProfitGoalMet) return false;
            foreach (var ore in Cliff.Ores) if (!Mines.Working(ore)) return false;
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
    /// <summary>No packed crates, for checking what a crate holds: a crate inside a crate is not a known item.</summary>
    private static readonly Dictionary<int, Inventory> NoCrates = new();
    /// <summary>Whether every entry is a positive count of a known item or of a packed crate in <paramref name="crates"/>.</summary>
    private bool ValidCounts(IReadOnlyDictionary<string, int> items, Dictionary<int, Inventory> crates) {
        foreach (var (item, count) in items) if (count <= 0 || !KnownItem(item, crates)) return false;
        return true;
    }
    /// <summary>Whether a box exists and holds only positive counts of known items or of packed crates in <paramref name="crates"/>.</summary>
    private bool ValidInventory([NotNullWhen(true)] Inventory? box, Dictionary<int, Inventory> crates) => box?.Items != null && ValidCounts(box.Items, crates);
    /// <summary>Whether each packed crate holds positive counts of known items, no other crates, and no more than a crate fits.</summary>
    private bool ValidCrates(Dictionary<int, Inventory> crates) {
        foreach (var box in crates.Values) if (!ValidInventory(box, NoCrates) || box.Count > Data.CrateCapacity || Barrel.In(box) != null) return false;
        return true;
    }
    /// <summary>
    /// Whether a player from a save or a joining guest carries only known items and has needs, torch and respawn timer within their limits.
    /// The crafting queue must hold only recipes made by hand.
    /// </summary>
    private bool ValidPlayer(PlayerState p, Dictionary<int, Inventory> crates) {
        if (p.HotbarSlots == null || p.Crafting == null || p.Making == null || !ValidInventory(p.Backpack, crates) || !p.Backpack.ValidFor(Inventory.Carrier.Player)) return false;
        foreach (var q in p.Crafting) if (q == null || !Hand(q.Recipe) || q.Count is <= 0 or > MaxQueued) return false;
        if (p.Making != "" && !Hand(p.Making)) return false;
        if (!float.IsFinite(p.DoorTime) || p.DoorTime < 0 || !p.DoorFrom.IsFinite()
            || !p.DoorBody.IsFinite()) return false;
        // A player who left while dead comes back still waiting to respawn.
        return (p.Health is > 0 and <= 100 || p.Health <= 0 && p.RespawnIn > 0) && p.Hunger is >= 0 and <= 100 && p.Thirst is >= 0 and <= 100
            && p.Torch >= 0 && p.Torch <= Data.CampfireSeconds && p.RespawnIn >= 0 && p.RespawnIn <= Data.RespawnSeconds;
    }
    /// <summary>
    /// Whether every building, worker and cart in a save holds only known items, every worker's tool is a known item,
    /// every building has its boxes, and every station is marked with, makes and counts batches of only recipes it makes.
    /// </summary>
    private bool ValidSettlement(SaveState save) {
        foreach (var t in save.Things) {
            if (t?.Inventory == null) return false;
            foreach (var box in Boxes(t)) if (box != null && !ValidInventory(box, save.Crates)) return false;
            if (t.Kind == ThingKind.Barrel && (t.Inventory.Slots != 1 || t.Inventory.StackSize != Barrel.Capacity
                || t.Inventory.Capacity != Barrel.Capacity || t.Inventory.Count != t.Inventory[Water.Liquid]
                || t.Inventory.Count > Barrel.Capacity)) return false;
            if (t.PlumbingSlot is Inventory plumbingSlot && (!Plumbing.HasPlumbing(t.Kind) || plumbingSlot.Slots != 1
                || plumbingSlot.Capacity != Plumbing.InstallCost || plumbingSlot.StackSize != Plumbing.InstallCost
                || plumbingSlot.Count > Plumbing.InstallCost || plumbingSlot.Count != plumbingSlot["steel_pipe"])) return false;
            if (t.Kind == ThingKind.Farm && !Farms.ValidWater(t)) return false;
            if (!(t.Pressure >= 0 && t.Pressure <= 100 && t.PumpStrokeLeft >= 0 && t.PumpStrokeLeft <= WaterPumps.StrokeSeconds)
                || t.Kind != ThingKind.Pump && (t.Pressure != 0 || t.PumpStrokeLeft != 0)) return false;
            if (!Plumbing.ValidReceiver(t)) return false;
            if (t.Kind == ThingKind.Hut && (t.Inventory.Slots != 1 || t.Inventory.StackSize != 1 || t.Inventory.Count > 1
                || t.Inventory.Count == 1 && Barrel.In(t.Inventory) == null)) return false;
            // A building has the boxes its kind is made with.
            if (SlotRules(t.Kind).Length > 0 && t.Tools == null || Burns(t.Kind) && t.Burner == null || t.Kind == ThingKind.Signpost && t.Wages == null
                || t.Kind is ThingKind.Signpost or ThingKind.Farm or ThingKind.Packer or ThingKind.Mine && t.Output == null) return false;
            if (t.Keep != "" && !Makes(t, t.Keep) || t.Making != "" && !Makes(t, t.Making)) return false;
            // Batches wait only for the recipe asked for, which is the one in progress when there is one, and the marked one on a station with a mark.
            if (t.Batches is < 0 or > MaxQueued || t.Asked != "" && !Makes(t, t.Asked) || t.Batches > 0 && (t.Asked == "" || t.Making != "" && t.Making != t.Asked)) return false;
            if (t.Batches > 0 && t.Keep != "" && t.Keep != t.Asked) return false;
        }
        foreach (var w in save.Workers)
            if (w == null || w.FarmWaterStop < 0 || w.FarmWaterStop >= BarrelHaul.FarmStops || !ValidJobs(w.Jobs) || !ValidToolSlot(w.ToolSlot, save.Crates) || !ValidInventory(w.Bag, save.Crates) || !w.Bag.ValidFor(Inventory.Carrier.Worker)) return false;
        foreach (var order in save.Orders) if (order?.Goods == null || !ValidCounts(order.Goods, save.Crates)) return false;
        return true;
    }
    /// <summary>Whether a worker's tool slot from a save is one slot holding nothing or one tool.</summary>
    private bool ValidToolSlot(Inventory? slot, Dictionary<int, Inventory> crates) =>
        ValidInventory(slot, crates) && slot.Slots == 1 && slot.Count <= 1 && (slot.First() is not string item || FitsWorkerToolSlot(item));

    /// <summary>Empties the hotbar slots that name nothing the game knows or the player carries, such as a packed crate since sold.</summary>
    private void TidyHotbar(PlayerState p) {
        p.HotbarSlots = Enumerable.Range(0, 9).Select(i => p.HotbarSlots.ElementAtOrDefault(i) is string id && (Data.Items.ContainsKey(id) || Data.IsBuilding(id) || p.Backpack[id] > 0) ? id : "").ToList();
        p.ActiveHotbar = Math.Clamp(p.ActiveHotbar, 0, 8);
        if (!(Data.Items.TryGetValue(p.InHand ?? "", out var def) && def.Tool != "")) p.InHand = "";
        TidyBindings(p);
    }
    private bool Hand(string recipe) => Data.Recipes.TryGetValue(recipe, out var r) && r.Hand;
    /// <summary>Brings the stacks of every inventory of a freshly parsed save in line with its totals.</summary>
    private static void RebuildAllStacks(SaveState save) {
        foreach (var player in save.Players.Concat(save.Absent.Values)) player?.Backpack?.RebuildStacks();
        RebuildAll(save.Crates?.Values);
        foreach (var worker in save.Workers) { worker?.Bag?.RebuildStacks(); worker?.ToolSlot?.RebuildStacks(); }
        foreach (var thing in save.Things) RebuildThing(thing);
        foreach (var thing in save.Remembered?.Values ?? Enumerable.Empty<Thing>()) RebuildThing(thing);
        static void RebuildAll(IEnumerable<Inventory>? boxes) { if (boxes != null) foreach (var box in boxes) box.RebuildStacks(); }
        static void RebuildThing(Thing? thing) { if (thing != null) foreach (var box in Boxes(thing)) box?.RebuildStacks(); }
    }

    /// <summary>
    /// Takes over a save written by this version of the game.
    /// One from any other version is refused rather than converted, and one that is incomplete, names unknown items or recipes or holds values out of range is refused rather than repaired.
    /// </summary>
    public void Deserialize(string json) {
        var loaded = JsonSerializer.Deserialize<SaveState>(json, GameData.Json) ?? throw new InvalidOperationException("Invalid save.");
        if (loaded.Version != new SaveState().Version) throw new InvalidOperationException("This save is from another version of the game and cannot be loaded. Start a new game.");
        if (loaded.Things == null || loaded.Workers == null || loaded.Orders == null || loaded.Wolves == null || loaded.Demand == null || loaded.Milestones == null || loaded.Obtained == null || loaded.Prices == null
            || loaded.Remembered == null || loaded.GravelPaths == null || loaded.GravelPaths.Exists(tile => (uint)tile >= Terrain.Size * Terrain.Size) || loaded.GravelPaths.Distinct().Count() != loaded.GravelPaths.Count || !SteelPipes.ValidLayout(loaded.SteelPipes) || !SteelPipes.ValidWater(loaded.PipeWater, loaded.SteelPipes) || loaded.Crates == null || loaded.Profits == null || loaded.Explored?.Length != Terrain.Size * Terrain.Size || loaded.Absent.Count == 0)
            throw new InvalidOperationException("Unsupported or incomplete save.");
        loaded.Players = new();
        // Until its machines join, the save's world runs as the player no machine has claimed, else the first.
        loaded.Actor = loaded.Absent.GetValueOrDefault("") ?? loaded.Absent.Values.First();
        RebuildAllStacks(loaded);
        if (loaded.Things.Concat(loaded.Remembered.Values).Any(t => !Enum.IsDefined(t.Kind) || !Enum.IsDefined(t.MineStage) || !Enum.IsDefined(t.FarmStage) || t.Rotation != 0 && (t.Kind is not (ThingKind.Crate or ThingKind.Barrel) && !Data.Buildings.ContainsKey(t.Kind) || !Construction.ValidRotation(t.Kind, t.Rotation)))) throw new InvalidOperationException("Unsupported or incomplete save.");
        if (loaded.Wolves.Any(w => w == null || !WolfCounter.ValidSavedPath(w) || !WolfHunt.ValidSavedReaction(w)))
            throw new InvalidOperationException("This save holds an invalid wolf movement.");
        if (loaded.Absent.Values.Any(p => p == null || !ValidPlayer(p, loaded.Crates)) || !ValidCrates(loaded.Crates) || !ValidSettlement(loaded) || loaded.Goal < 0) throw new InvalidOperationException("This save holds unknown items or values out of range and cannot be loaded.");
        foreach (var p in loaded.Absent.Values) TidyHotbar(p);
        // A server sends this player away again until each player joins.
        var local = loaded.Actor;
        loaded.Absent.Remove(local.Key);
        local.Id = 1; Forget(local); loaded.Players.Add(local);
        S = loaded;
        toolsChanged = true;
        foreach (var t in S.Things.Of(ThingKind.Toolstation)) WatchTools(t);
        gatherClock = 0; ExploredRevision++; Look();
        Replaced?.Invoke();
    }
}
