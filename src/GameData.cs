using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChopChopMeow;

public sealed class ItemDef {
    public string Name { get; set; } = "";
    public float Price { get; set; }
    public string Color { get; set; } = "ffffff";
    /// <summary>The kind of tool this item is (axe, pickaxe, hammer, chisel, rake or mortar), or empty for materials.</summary>
    public string Tool { get; set; } = "";
    /// <summary>How fast the tool works compared with the plainest tool of its kind.</summary>
    public float Speed { get; set; } = 1;
    /// <summary>The tool's material grade: 0 for wood and stone, 1 for iron, 2 for steel.</summary>
    public int Tier { get; set; }
    /// <summary>Wolves eat it and compost takes it, cooked or not; only items that restore hunger or thirst can be eaten.</summary>
    public bool Food { get; set; }
    /// <summary>What eating or drinking one restores.</summary>
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Health { get; set; }
}
public sealed class BuildingDef {
    public string Name { get; set; } = "";
    public Dictionary<string, int> Cost { get; set; } = new();
    public string Description { get; set; } = "";
    /// <summary>The recipe a station starts on, or empty for one that waits for the player's choice.</summary>
    public string Recipe { get; set; } = "";
}
/// <summary>What a recipe's product is for; the recipe boards list recipes under these, in this order.</summary>
public enum RecipeGroup { Tools, Materials, Containers, Food }
public sealed class RecipeDef {
    public string Name { get; set; } = "";
    public required RecipeGroup Group { get; set; }
    /// <summary>The station that makes it, or null for a recipe made only by hand.</summary>
    public ThingKind? Station { get; set; }
    /// <summary>What a batch takes. The first is the one the product is made out of, which the recipe map hangs it under.</summary>
    public Dictionary<string, int> Inputs { get; set; } = new();
    public string Output { get; set; } = "";
    public int Amount { get; set; } = 1;
    public float Seconds { get; set; }
    /// <summary>The tool kind the recipe needs: carried for crafting by hand; at a station slotted, or carried by a player working there.</summary>
    public string Tool { get; set; } = "";
    /// <summary>The lowest <see cref="ItemDef.Tier"/> of that tool that will do; a better one always does.</summary>
    public int Tier { get; set; }
    /// <summary>Also craftable from the backpack through the personal crafting menu.</summary>
    public bool Hand { get; set; }
    /// <summary>The fuels a batch can burn, one unit per batch from the station's fuel slot, in the order workers look for them; empty for a recipe that burns none.</summary>
    public List<string> Fuels { get; set; } = new();
    public bool Burns => Fuels.Count > 0;
}
/// <summary>How an item that no recipe makes is come by, for the recipe map.</summary>
public sealed class SourceDef {
    /// <summary>Where it comes from, in a few words.</summary>
    public string How { get; set; } = "";
    /// <summary>The items it comes out of, if any; the map hangs it under the first.</summary>
    public List<string> From { get; set; } = new();
    /// <summary>Other items that getting it takes.</summary>
    public List<string> Also { get; set; } = new();
}
/// <summary>One row of the recipe map: a category and the items its chains start from, top to bottom.</summary>
public sealed class MapRowDef {
    public string Title { get; set; } = "";
    public List<string> Roots { get; set; } = new();
}
/// <summary>How the village's appetite for a good it takes moves with sales.</summary>
public sealed class DemandDef {
    /// <summary>Units the village absorbs in a day: a day restores what selling this many takes.</summary>
    public int Daily { get; set; }
    /// <summary>Units of the daily amount that sell at the full price before it starts to fall.</summary>
    public int Steady { get; set; }
    /// <summary>Fraction of the full price each crate's worth sold past the steady amount takes off; small for staples, large for tools.</summary>
    public float Drop { get; set; }
}
/// <summary>What the village takes in crates with its demand, and what it sells with how many units make one purchase.</summary>
public sealed class MarketDef {
    public Dictionary<string, DemandDef> Sells { get; set; } = new();
    public Dictionary<string, int> Buys { get; set; } = new();
}
/// <summary>A hiring deal: a larger payment up front buys a lower daily wage.</summary>
public sealed class HireOffer {
    public int Upfront { get; set; }
    public int Wage { get; set; }
}
/// <summary>How a mine is built and how fast it yields.</summary>
public sealed class MineDef {
    /// <summary>Materials the second stage needs delivered to the site.</summary>
    public Dictionary<string, int> Materials { get; set; } = new();
    /// <summary>Working days of one worker that digging the entrance, and later fitting the frame, each take.</summary>
    public float StageWorkdays { get; set; } = 1;
    /// <summary>Seconds one miner takes to bring out one ore and one rock.</summary>
    public float Seconds { get; set; } = 2;
    public int Workers { get; set; } = 3;
}
/// <summary>A farm plot's season: what sows it, how long the wheat grows and how much work its harvest takes.</summary>
public sealed class FarmDef {
    public Dictionary<string, int> Sow { get; set; } = new();
    /// <summary>Whole days from sowing to ripe wheat, nights included.</summary>
    public float GrowDays { get; set; } = 3;
    /// <summary>Working days of one worker that a harvest takes; several workers share it.</summary>
    public float HarvestWorkdays { get; set; } = 2;
    public Dictionary<string, int> Yield { get; set; } = new();
}
/// <summary>The end goals: a mine on every ore and a week of steady profit.</summary>
public sealed class GoalsDef {
    /// <summary>Sale coins minus wages that a day has to clear.</summary>
    public int DailyProfit { get; set; }
    /// <summary>Consecutive days that have to clear it.</summary>
    public int ProfitDays { get; set; }
}
public sealed partial class GameData {
    public const int MinutesPerDay = 1440;
    public float DaySeconds { get; } = 840;
    public float NightStarts { get; } = .7f;
    /// <summary>Day phase where the light starts to fade, a tenth of a day before night; workers sign out then.</summary>
    public float DuskStarts => NightStarts - .1f;
    /// <summary>Day phase where the sun sets and the moon rises, halfway through the fading light.</summary>
    public float Sunset => (DuskStarts + NightStarts) / 2;
    /// <summary>Clock hour when workers break for an hour of lunch; the day starts at 06:00.</summary>
    public float LunchHour { get; } = 12;
    /// <summary>Days from one full moon to the next; new-moon nights are the darkest.</summary>
    public float MoonCycleDays { get; } = 8;
    public float TradeSeconds { get; } = 30;
    public float GatherSeconds { get; } = 2;
    /// <summary>Tiles a second the player walks.</summary>
    public float WalkSpeed { get; } = 4.2f;
    /// <summary>How many times faster than walking a wolf chases: above 1 so walking never outpaces it, below <see cref="SprintSpeed"/> so sprinting does.</summary>
    public float WolfChase { get; } = 1.5f;
    /// <summary>Wolves that come out of each den at dusk; after them one den lets one more out every hour until the wolves head home.</summary>
    public int WolvesPerDen { get; } = 1;
    /// <summary>A wolf's health: an axe blow takes more than half of it.</summary>
    public float WolfHealth { get; } = 4;
    /// <summary>Health one bite takes: the fourth bite, not the third, brings a healthy player to half.</summary>
    public float WolfBite { get; } = 15;
    /// <summary>Metres a second a wolf stalks and backs off at; it patrols at half of it.</summary>
    public float WolfTrot { get; } = 3;
    /// <summary>Metres within which a wolf smells a player out in the open; it howls and goes after them.</summary>
    public float SmellRadius { get; } = 30;
    /// <summary>Metres within which a wolf closing in on a player outside firelight sees them and breaks into a sprint.</summary>
    public float WolfSightRange { get; } = 15;
    /// <summary>Metres a howl carries; patrolling wolves within it run to the howl.</summary>
    public float HowlRadius { get; } = 80;
    /// <summary>Seconds of the night's first howl, a lone wolf; the wolf stands howling as long before it goes after the player it smelled.</summary>
    public float LoneHowlSeconds { get; } = 5.9f;
    /// <summary>Seconds of every later howl, the pack answering.</summary>
    public float PackHowlSeconds { get; } = 7.6f;
    /// <summary>Hours before dawn when every wolf gives up and heads back to its den.</summary>
    public float WolfRetreatHours { get; } = 1;
    /// <summary>Day phase when the wolves head home.</summary>
    public float WolfRetreatStarts => 1 - WolfRetreatHours / 24;
    /// <summary>How many times faster the player moves with Shift held.</summary>
    public float SprintSpeed { get; } = 1.6f;
    /// <summary>Tiles a second the player moves, walking or sprinting.</summary>
    public float MoveSpeed(bool sprinting) => WalkSpeed * (sprinting ? SprintSpeed : 1);
    /// <summary>How many times faster food and drink drain while sprinting.</summary>
    public float SprintDrain { get; } = 1.7f;
    /// <summary>Food or drink percentage below which the player is too weak to sprint and the screen edges warn.</summary>
    public const float LowNeed = 15;
    public float RespawnSeconds { get; } = 30;
    /// <summary>In-game hours a picked-clean berry bush takes to fruit again.</summary>
    public float BerryRegrowHours { get; } = 24;
    public float HourSeconds => DaySeconds / 24;
    /// <summary>Seconds of play as whole in-game minutes.</summary>
    public int GameMinutes(float seconds) => (int)(seconds / DaySeconds * MinutesPerDay);
    public float BerryRegrowSeconds => BerryRegrowHours / 24 * DaySeconds;
    /// <summary>In-game hours a full hunger or thirst bar lasts, so the drain follows the day length.</summary>
    public float HungerHours { get; } = 24;
    public float ThirstHours { get; } = 24;
    /// <summary>In-game hours one log keeps a campfire burning.</summary>
    public float CampfireHours { get; } = 4;
    public float CampfireSeconds => CampfireHours / 24 * DaySeconds;
    public float HungerPerSecond => 100 / (HungerHours / 24 * DaySeconds);
    public float ThirstPerSecond => 100 / (ThirstHours / 24 * DaySeconds);
    /// <summary>Radius in tiles of the player's live view; sized to fill a 16:10 screen at full zoom-out.</summary>
    public float SightRadius { get; } = 20;
    /// <summary>Items one crate holds in total, in stacks of one item each.</summary>
    public int CrateCapacity { get; } = 12;
    /// <summary>Seconds a packing station takes to pack one item into a crate.</summary>
    public float PackSeconds { get; } = 1.5f;
    /// <summary>How much faster a crafting bench works than crafting by hand.</summary>
    public float BenchSpeed { get; } = 2;
    /// <summary>A worker's time on duty in a day: dawn to dusk, less lunch and the walk in.</summary>
    public float WorkdaySeconds => DuskStarts * DaySeconds - HourSeconds - TradeSeconds / 2;
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    /// <summary>The game's values, checked to fit together.</summary>
    public static GameData Create() {
        var data = new GameData();
        data.Validate();
        Inventory.Unstacked.Clear();
        foreach (var (id, item) in data.Items) if (item.Tool != "") Inventory.Unstacked.Add(id);
        foreach (var id in Water.BottleItems) Inventory.Unstacked.Add(id);
        return data;
    }
    private void Validate() {
        if (DaySeconds <= 0 || TradeSeconds <= 0 || GatherSeconds <= 0 || DuskStarts <= 0 || NightStarts >= 1 || LunchHour < 6 || LunchHour + 1 > 6 + DuskStarts * 24 || RespawnSeconds < 0 || HungerHours <= 0 || ThirstHours <= 0 || CampfireHours <= 0 || MoonCycleDays <= 0 || BerryRegrowHours <= 0 || HireOffers.Count == 0 || HireOffers.Exists(o => o.Upfront < 0 || o.Wage < 0) || SightRadius <= 0 || CrateCapacity <= 0 || PackSeconds <= 0 || BenchSpeed <= 0 || SprintSpeed < 1 || SprintDrain < 1 || WalkSpeed <= 0 || WolfChase <= 1 || WolfChase >= SprintSpeed || WolvesPerDen < 1 || WolfHealth <= 0 || WolfBite <= 0 || WolfTrot <= 0 || WolfSightRange <= 0 || WolfSightRange >= SmellRadius || HowlRadius <= 0 || LoneHowlSeconds <= 0 || PackHowlSeconds <= 0 || WolfRetreatStarts <= NightStarts || !Items.ContainsKey("crate") || Water.BottleItems.Any(id => !Items.ContainsKey(id)))
            throw new InvalidOperationException("Invalid time or economy balance in GameData.");
        foreach (var building in Buildings) {
            foreach (var cost in building.Value.Cost)
                if (!Items.ContainsKey(cost.Key) || cost.Value <= 0) throw new InvalidOperationException($"Invalid cost for {building.Key}: {cost.Key}.");
            if (building.Value.Recipe != "" && (!Recipes.TryGetValue(building.Value.Recipe, out var starting) || starting.Station != building.Key))
                throw new InvalidOperationException($"Invalid starting recipe for {building.Key}: {building.Value.Recipe}.");
            if (building.Value.Recipe == "" && Recipes.Values.Count(r => r.Station == building.Key) == 1)
                throw new InvalidOperationException($"{building.Key} has one recipe, so it must start on it.");
        }
        foreach (var recipe in Recipes) {
            bool tool = recipe.Value.Tool == "" || Items.Values.Any(i => i.Tool == recipe.Value.Tool && i.Tier >= recipe.Value.Tier);
            if (!(recipe.Value.Station is ThingKind station ? Buildings.ContainsKey(station) : recipe.Value.Hand) || !tool || !Items.ContainsKey(recipe.Value.Output) || recipe.Value.Amount <= 0 || recipe.Value.Seconds <= 0)
                throw new InvalidOperationException($"Invalid recipe: {recipe.Key}.");
            foreach (var input in recipe.Value.Inputs)
                if (!Items.ContainsKey(input.Key) || input.Value <= 0) throw new InvalidOperationException($"Invalid input for {recipe.Key}: {input.Key}.");
            if (recipe.Value.Fuels.Any(fuel => !Items.ContainsKey(fuel)) || recipe.Value.Burns && recipe.Value.Station == null)
                throw new InvalidOperationException($"Invalid fuel for {recipe.Key}.");
        }
        if (Items.Values.Any(i => i.Speed <= 0 || i.Hunger < 0 || i.Thirst < 0 || i.Health < 0 || i.Hunger > 0 && !i.Food) || Market.Sells.Keys.Concat(Market.Buys.Keys).Any(id => !Items.ContainsKey(id) || id == "coins") || Market.Sells.Values.Any(d => d.Daily <= 0 || d.Steady < 0 || d.Steady > d.Daily || d.Drop <= 0 || d.Drop >= .75f) || Market.Buys.Values.Any(n => n <= 0 || n > CrateCapacity))
            throw new InvalidOperationException("Invalid tool speed or market goods in GameData.");
        if (Mine.Materials.Any(p => !Items.ContainsKey(p.Key) || p.Value <= 0) || Mine.StageWorkdays <= 0 || Mine.Seconds <= 0 || Mine.Workers <= 0 || Goals.DailyProfit <= 0 || Goals.ProfitDays <= 0)
            throw new InvalidOperationException("Invalid mine or goals in GameData.");
        if (Farm.Sow.Count == 0 || Farm.Yield.Count == 0 || Farm.Sow.Concat(Farm.Yield).Any(p => !Items.ContainsKey(p.Key) || p.Value <= 0) || Farm.GrowDays <= 0 || Farm.HarvestWorkdays <= 0)
            throw new InvalidOperationException("Invalid farm in GameData.");
        ValidateMap();
    }
    // Filled by Validate: the recipe that makes each item, and what the recipe map hangs under each item.
    private readonly Dictionary<string, RecipeDef> recipeOf = new();
    private readonly Dictionary<string, List<string>> madeOutOf = new();
    private static readonly List<string> Nothing = new();
    /// <summary>Items the recipe map leaves out: coins, and the part-drunk bottles that stand with the full one.</summary>
    public static bool OffTheMap(string item) => item == Inventory.Coins || item is "water_1" or "water_2";
    /// <summary>The recipe that makes an item, or null for one that is gathered, grown or bought.</summary>
    public RecipeDef? RecipeOf(string item) => recipeOf.GetValueOrDefault(item);
    /// <summary>The item another is made out of, which the recipe map hangs it under; null for one a chain starts from.</summary>
    public string? MadeFrom(string item) => RecipeOf(item) is RecipeDef r ? r.Inputs.Keys.First() : Sources.TryGetValue(item, out var source) && source.From.Count > 0 ? source.From[0] : null;
    /// <summary>The items made out of another, longest chain first.</summary>
    public List<string> MadeOutOf(string item) => madeOutOf.GetValueOrDefault(item, Nothing);
    /// <summary>How many steps the longest chain out of an item runs.</summary>
    public int ChainLength(string item) => MadeOutOf(item).Count == 0 ? 0 : 1 + MadeOutOf(item).Max(ChainLength);
    /// <summary>What an item goes into: the products that take it as an input, then for a tool those made with it.</summary>
    public IEnumerable<string> UsedFor(string item) {
        var uses = Recipes.Values.Where(r => r.Inputs.ContainsKey(item)).Select(r => r.Output).Concat(Sources.Where(p => p.Value.From.Contains(item) || p.Value.Also.Contains(item)).Select(p => p.Key));
        if (Items[item].Tool is { Length: > 0 } kind) uses = uses.Concat(Recipes.Values.Where(r => r.Tool == kind && r.Tier <= Items[item].Tier).Select(r => r.Output));
        return uses.Distinct();
    }
    /// <summary>Works out the recipe map's chains and checks that every item is on the map once.</summary>
    private void ValidateMap() {
        foreach (var recipe in Recipes.Values)
            if (!recipeOf.TryAdd(recipe.Output, recipe) || recipe.Inputs.Count == 0) throw new InvalidOperationException($"{recipe.Output} needs one recipe with an input for the recipe map.");
        foreach (var (item, source) in Sources)
            if (!Items.ContainsKey(item) || recipeOf.ContainsKey(item) || source.How == "" || source.From.Concat(source.Also).Any(other => !Items.ContainsKey(other)))
                throw new InvalidOperationException($"Invalid source for {item}.");
        foreach (string item in Items.Keys) {
            if (OffTheMap(item)) continue;
            if (!recipeOf.ContainsKey(item) && !Sources.ContainsKey(item)) throw new InvalidOperationException($"{item} has neither a recipe nor a source for the recipe map.");
            if (MadeFrom(item) is string parent) { if (!madeOutOf.TryGetValue(parent, out var children)) madeOutOf[parent] = children = new(); children.Add(item); }
        }
        // A stable sort, so chains of one length keep the tables' order.
        foreach (var (parent, children) in madeOutOf.ToList()) madeOutOf[parent] = children.OrderByDescending(ChainLength).ToList();
        var placed = new HashSet<string>();
        void Place(string item) {
            if (!placed.Add(item)) throw new InvalidOperationException($"{item} is on the recipe map twice.");
            foreach (string child in MadeOutOf(item)) Place(child);
        }
        foreach (var row in MapRows) {
            if (row.Title == "" || row.Roots.Count == 0) throw new InvalidOperationException("A recipe map row needs a title and an item.");
            foreach (string root in row.Roots) {
                if (!Items.ContainsKey(root) || MadeFrom(root) != null) throw new InvalidOperationException($"{root} cannot start a recipe map row.");
                Place(root);
            }
        }
        if (Items.Keys.FirstOrDefault(item => !OffTheMap(item) && !placed.Contains(item)) is string missing) throw new InvalidOperationException($"{missing} is not on the recipe map.");
    }
    /// <summary>Eaten from the backpack or hotbar: food and drink, plus raw food, which only says it needs cooking.</summary>
    public bool IsConsumable(string item) => Water.BottleSips(item) > 0 || Items.TryGetValue(item, out var def) && (def.Food || def.Thirst > 0);
    public bool IsFood(string item) => Items.TryGetValue(item, out var def) && def.Food;
    /// <summary>The humblest tool of a kind at or above a tier, whose picture stands for a tool not there yet.</summary>
    public string PlainestTool(string kind, int tier = 0) => Items.Where(p => p.Value.Tool == kind && p.Value.Tier >= tier).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;

    /// <summary>The plainest tool of a kind that meets a tier, for naming what a recipe needs.</summary>
    public string ToolName(string tool, int tier) {
        if (!toolNames.TryGetValue((tool, tier), out var name))
            toolNames[(tool, tier)] = name = Items.Values.Where(i => i.Tool == tool && i.Tier >= tier).MinBy(i => i.Tier) is ItemDef def && tier > 0 ? def.Name.ToLowerInvariant() : tool;
        return name;
    }
    // Named once per tool and tier, since workers ask every tick while a station waits for its tool.
    private readonly Dictionary<(string, int), string> toolNames = new();
    /// <summary>Whether an id, such as a hotbar slot's, names a building plan, and which.</summary>
    public bool TryBuilding(string id, out ThingKind kind) => Kinds.TryParse(id, out kind) && Buildings.ContainsKey(kind);
    public bool IsBuilding(string id) => TryBuilding(id, out _);
    public string Name(string id) => World.IsCrate(id) ? Items["crate"].Name : Items.TryGetValue(id, out var i) ? i.Name : Kinds.TryParse(id, out var kind) ? Name(kind) : id.Replace('_', ' ');
    public string Name(ThingKind kind) => Items.TryGetValue(kind.Id(), out var i) ? i.Name : Buildings.TryGetValue(kind, out var b) ? b.Name : kind switch {
        ThingKind.Tree => "Tree",
        ThingKind.Iron => "Iron deposit",
        ThingKind.Tin => "Tin deposit",
        ThingKind.LooseCoal => "Coal",
        ThingKind.LooseStone => "Loose stone",
        ThingKind.Stick => "Stick",
        ThingKind.IronChip => "Iron ore",
        ThingKind.TinChip => "Tin ore",
        ThingKind.Grave => "Recovery box",
        ThingKind.WolfCorpse => "Wolf carcass",
        _ => kind.Id().Replace('_', ' ')
    };
}
