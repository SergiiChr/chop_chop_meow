using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

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
}
public sealed class RecipeDef {
    public string Name { get; set; } = "";
    public string Station { get; set; } = "";
    public Dictionary<string, int> Inputs { get; set; } = new();
    public string Output { get; set; } = "";
    public int Amount { get; set; } = 1;
    public float Seconds { get; set; }
    /// <summary>The tool kind the recipe needs: carried for crafting by hand, slotted at a bench.</summary>
    public string Tool { get; set; } = "";
    /// <summary>The lowest <see cref="ItemDef.Tier"/> of that tool that will do; a better one always does.</summary>
    public int Tier { get; set; }
    /// <summary>Also craftable from the backpack through the personal crafting menu.</summary>
    public bool Hand { get; set; }
    /// <summary>Each batch burns one wood or coal, from the station's fuel slot; the reinforced furnace burns coal.</summary>
    public bool Fuel { get; set; }
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
public sealed class GameData {
    public float DaySeconds { get; set; } = 420;
    public float NightStarts { get; set; } = .7f;
    /// <summary>Day phase where the light starts to fade, a tenth of a day before night; workers sign out then.</summary>
    public float DuskStarts => NightStarts - .1f;
    /// <summary>Clock hour when workers break for an hour of lunch; the day starts at 06:00.</summary>
    public float LunchHour { get; set; } = 12;
    public float TradeSeconds { get; set; } = 30;
    public List<HireOffer> HireOffers { get; set; } = new();
    public float GatherSeconds { get; set; } = 1.5f;
    /// <summary>Tiles a second the player walks.</summary>
    public float WalkSpeed { get; set; } = 4.2f;
    /// <summary>How many times faster than walking a wolf chases: above 1 so walking never outpaces it, below <see cref="SprintSpeed"/> so sprinting does.</summary>
    public float WolfChase { get; set; } = 1.5f;
    /// <summary>How many times faster the player moves with Shift held.</summary>
    public float SprintSpeed { get; set; } = 1.6f;
    /// <summary>How many times faster food and drink drain while sprinting.</summary>
    public float SprintDrain { get; set; } = 1.7f;
    /// <summary>Food or drink percentage below which the player is too weak to sprint and the screen edges warn.</summary>
    public const float LowNeed = 15;
    public float RespawnSeconds { get; set; } = 30;
    /// <summary>In-game hours a picked-clean berry bush takes to fruit again.</summary>
    public float BerryRegrowHours { get; set; } = 24;
    public float BerryRegrowSeconds => BerryRegrowHours / 24 * DaySeconds;
    /// <summary>In-game hours a full hunger or thirst bar lasts, so the drain follows the day length.</summary>
    public float HungerHours { get; set; } = 24;
    public float ThirstHours { get; set; } = 12;
    /// <summary>In-game hours one log keeps a campfire burning.</summary>
    public float CampfireHours { get; set; } = 4;
    public float CampfireSeconds => CampfireHours / 24 * DaySeconds;
    public float HungerPerSecond => 100 / (HungerHours / 24 * DaySeconds);
    public float ThirstPerSecond => 100 / (ThirstHours / 24 * DaySeconds);
    /// <summary>Radius in tiles of the player's live view; sized to fill a 16:10 screen at full zoom-out.</summary>
    public float SightRadius { get; set; } = 20;
    /// <summary>Items one crate holds in total, in stacks of one item each.</summary>
    public int CrateCapacity { get; set; } = 12;
    /// <summary>How much faster a crafting bench works than crafting by hand.</summary>
    public float BenchSpeed { get; set; } = 2;
    public MarketDef Market { get; set; } = new();
    public MineDef Mine { get; set; } = new();
    public FarmDef Farm { get; set; } = new();
    public GoalsDef Goals { get; set; } = new();
    /// <summary>A worker's time on duty in a day: dawn to dusk, less lunch and the walk in.</summary>
    public float WorkdaySeconds => DuskStarts * DaySeconds - DaySeconds / 24 - TradeSeconds / 2;
    public Dictionary<string, ItemDef> Items { get; set; } = new();
    public Dictionary<string, BuildingDef> Buildings { get; set; } = new();
    public Dictionary<string, RecipeDef> Recipes { get; set; } = new();
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static GameData Load() {
        using var file = Godot.FileAccess.Open("res://data/game.json", Godot.FileAccess.ModeFlags.Read);
        var data = JsonSerializer.Deserialize<GameData>(file.GetAsText(), Json) ?? throw new InvalidOperationException("Missing game data");
        data.Validate();
        Inventory.Unstacked.Clear();
        foreach (var (id, item) in data.Items) if (item.Tool != "") Inventory.Unstacked.Add(id);
        return data;
    }
    public void Validate() {
        if (DaySeconds <= 0 || TradeSeconds <= 0 || GatherSeconds <= 0 || DuskStarts <= 0 || NightStarts >= 1 || LunchHour < 6 || LunchHour + 1 > 6 + DuskStarts * 24 || RespawnSeconds < 0 || HungerHours <= 0 || ThirstHours <= 0 || CampfireHours <= 0 || BerryRegrowHours <= 0 || HireOffers.Count == 0 || HireOffers.Exists(o => o.Upfront < 0 || o.Wage < 0) || SightRadius <= 0 || CrateCapacity <= 0 || BenchSpeed <= 0 || SprintSpeed < 1 || SprintDrain < 1 || WalkSpeed <= 0 || WolfChase <= 1 || WolfChase >= SprintSpeed || !Items.ContainsKey("crate"))
            throw new InvalidOperationException("Invalid time or economy balance in data/game.json.");
        foreach (var building in Buildings)
            foreach (var cost in building.Value.Cost)
                if (!Items.ContainsKey(cost.Key) || cost.Value <= 0) throw new InvalidOperationException($"Invalid cost for {building.Key}: {cost.Key}.");
        foreach (var recipe in Recipes) {
            bool tool = recipe.Value.Tool == "" || Items.Values.Any(i => i.Tool == recipe.Value.Tool && i.Tier >= recipe.Value.Tier);
            if (!(recipe.Value.Station == "" ? recipe.Value.Hand : Buildings.ContainsKey(recipe.Value.Station)) || !tool || !Items.ContainsKey(recipe.Value.Output) || recipe.Value.Amount <= 0 || recipe.Value.Seconds <= 0)
                throw new InvalidOperationException($"Invalid recipe: {recipe.Key}.");
            foreach (var input in recipe.Value.Inputs)
                if (!Items.ContainsKey(input.Key) || input.Value <= 0) throw new InvalidOperationException($"Invalid input for {recipe.Key}: {input.Key}.");
        }
        if (Items.Values.Any(i => i.Speed <= 0 || i.Hunger < 0 || i.Thirst < 0 || i.Health < 0 || i.Hunger > 0 && !i.Food) || Market.Sells.Keys.Concat(Market.Buys.Keys).Any(id => !Items.ContainsKey(id) || id == "coins") || Market.Sells.Values.Any(d => d.Daily <= 0 || d.Steady < 0 || d.Steady > d.Daily || d.Drop <= 0 || d.Drop >= .75f) || Market.Buys.Values.Any(n => n <= 0 || n > CrateCapacity))
            throw new InvalidOperationException("Invalid tool speed or market goods in data/game.json.");
        if (Mine.Materials.Any(p => !Items.ContainsKey(p.Key) || p.Value <= 0) || Mine.StageWorkdays <= 0 || Mine.Seconds <= 0 || Mine.Workers <= 0 || Goals.DailyProfit <= 0 || Goals.ProfitDays <= 0)
            throw new InvalidOperationException("Invalid mine or goals in data/game.json.");
        if (Farm.Sow.Count == 0 || Farm.Yield.Count == 0 || Farm.Sow.Concat(Farm.Yield).Any(p => !Items.ContainsKey(p.Key) || p.Value <= 0) || Farm.GrowDays <= 0 || Farm.HarvestWorkdays <= 0)
            throw new InvalidOperationException("Invalid farm in data/game.json.");
    }
    /// <summary>Eaten from the backpack or hotbar: food and drink, plus raw food, which only says it needs cooking.</summary>
    public bool IsConsumable(string item) => Items.TryGetValue(item, out var def) && (def.Food || def.Thirst > 0);
    public bool IsFood(string item) => Items.TryGetValue(item, out var def) && def.Food;
    /// <summary>The plainest tool of a kind that meets a tier, for naming what a recipe needs.</summary>
    public string ToolName(string tool, int tier) {
        if (!toolNames.TryGetValue((tool, tier), out var name))
            toolNames[(tool, tier)] = name = Items.Values.Where(i => i.Tool == tool && i.Tier >= tier).MinBy(i => i.Tier) is ItemDef def && tier > 0 ? def.Name.ToLowerInvariant() : tool;
        return name;
    }
    // Named once per tool and tier, since workers ask every tick while a station waits for its tool.
    private readonly Dictionary<(string, int), string> toolNames = new();
    public string Name(string id) => World.IsCrate(id) ? Items["crate"].Name : Items.TryGetValue(id, out var i) ? i.Name : Buildings.TryGetValue(id, out var b) ? b.Name : id switch {
        "tree" => "Tree",
        "iron" => "Iron deposit",
        "tin" => "Tin deposit",
        "loose_coal" => "Coal",
        "grave" => "Recovery box",
        "wolf_corpse" => "Wolf carcass",
        _ => id.Replace('_', ' ')
    };
}
