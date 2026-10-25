using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

public sealed class ItemDef
{
    public string Name { get; set; } = "";
    public float Price { get; set; }
    public string Color { get; set; } = "ffffff";
    /// <summary>The kind of tool this item is (axe, pickaxe, hammer or chisel), or empty for materials.</summary>
    public string Tool { get; set; } = "";
    /// <summary>How fast the tool works compared with the plainest tool of its kind.</summary>
    public float Speed { get; set; } = 1;
}
public sealed class BuildingDef
{
    public string Name { get; set; } = "";
    public Dictionary<string, int> Cost { get; set; } = new();
    public string Description { get; set; } = "";
}
public sealed class RecipeDef
{
    public string Name { get; set; } = "";
    public string Station { get; set; } = "";
    public Dictionary<string, int> Inputs { get; set; } = new();
    public string Output { get; set; } = "";
    public int Amount { get; set; } = 1;
    public float Seconds { get; set; }
    /// <summary>The tool kind the recipe needs: carried for crafting by hand, slotted at a bench.</summary>
    public string Tool { get; set; } = "";
    /// <summary>Also craftable from the backpack through the personal crafting menu.</summary>
    public bool Hand { get; set; }
}
/// <summary>How the village's appetite for a good it takes moves with sales.</summary>
public sealed class DemandDef
{
    /// <summary>Units the village absorbs in a day: selling this many lowers the price by <see cref="Drop"/>, and a day restores it.</summary>
    public int Daily { get; set; }
    /// <summary>Fraction of the price lost by selling one day's amount; small for staple goods, large for tools.</summary>
    public float Drop { get; set; }
}
/// <summary>What the village takes in crates with its demand, and what it sells with how many units make one purchase.</summary>
public sealed class MarketDef
{
    public Dictionary<string, DemandDef> Sells { get; set; } = new();
    public Dictionary<string, int> Buys { get; set; } = new();
}
/// <summary>A hiring deal: a larger payment up front buys a lower daily wage.</summary>
public sealed class HireOffer
{
    public int Upfront { get; set; }
    public int Wage { get; set; }
}
public sealed class GameData
{
    public float DaySeconds { get; set; } = 420;
    public float NightStarts { get; set; } = .7f;
    /// <summary>Day phase where the light starts to fade, a tenth of a day before night; workers sign out then.</summary>
    public float DuskStarts => NightStarts - .1f;
    /// <summary>Clock hour when workers break for an hour of lunch; the day starts at 06:00.</summary>
    public float LunchHour { get; set; } = 12;
    public float TradeSeconds { get; set; } = 30;
    public List<HireOffer> HireOffers { get; set; } = new();
    public float GatherSeconds { get; set; } = 1.5f;
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
    public Dictionary<string, ItemDef> Items { get; set; } = new();
    public Dictionary<string, BuildingDef> Buildings { get; set; } = new();
    public Dictionary<string, RecipeDef> Recipes { get; set; } = new();
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static GameData Load()
    {
        using var file = Godot.FileAccess.Open("res://data/game.json", Godot.FileAccess.ModeFlags.Read);
        var data = JsonSerializer.Deserialize<GameData>(file.GetAsText(), Json) ?? throw new InvalidOperationException("Missing game data");
        data.Validate();
        return data;
    }
    public void Validate()
    {
        if (DaySeconds <= 0 || TradeSeconds <= 0 || GatherSeconds <= 0 || DuskStarts <= 0 || NightStarts >= 1 || LunchHour < 6 || LunchHour + 1 > 6 + DuskStarts * 24 || RespawnSeconds < 0 || HungerHours <= 0 || ThirstHours <= 0 || CampfireHours <= 0 || BerryRegrowHours <= 0 || HireOffers.Count == 0 || HireOffers.Exists(o => o.Upfront < 0 || o.Wage < 0) || SightRadius <= 0 || CrateCapacity <= 0 || BenchSpeed <= 0 || !Items.ContainsKey("crate"))
            throw new InvalidOperationException("Invalid time or economy balance in data/game.json.");
        foreach (var building in Buildings)
            foreach (var cost in building.Value.Cost)
                if (!Items.ContainsKey(cost.Key) || cost.Value <= 0) throw new InvalidOperationException($"Invalid cost for {building.Key}: {cost.Key}.");
        foreach (var recipe in Recipes)
        {
            bool tool = recipe.Value.Tool == "" || Items.Values.Any(i => i.Tool == recipe.Value.Tool);
            if (!(recipe.Value.Station == "" ? recipe.Value.Hand : Buildings.ContainsKey(recipe.Value.Station)) || !tool || !Items.ContainsKey(recipe.Value.Output) || recipe.Value.Amount <= 0 || recipe.Value.Seconds <= 0)
                throw new InvalidOperationException($"Invalid recipe: {recipe.Key}.");
            foreach (var input in recipe.Value.Inputs)
                if (!Items.ContainsKey(input.Key) || input.Value <= 0) throw new InvalidOperationException($"Invalid input for {recipe.Key}: {input.Key}.");
        }
        if (Items.Values.Any(i => i.Speed <= 0) || Market.Sells.Keys.Concat(Market.Buys.Keys).Any(id => !Items.ContainsKey(id) || id == "coins") || Market.Sells.Values.Any(d => d.Daily <= 0 || d.Drop <= 0 || d.Drop >= .75f) || Market.Buys.Values.Any(n => n <= 0 || n > CrateCapacity))
            throw new InvalidOperationException("Invalid tool speed or market goods in data/game.json.");
    }
    public string Name(string id) => World.IsCrate(id) ? Items["crate"].Name : Items.TryGetValue(id, out var i) ? i.Name : Buildings.TryGetValue(id, out var b) ? b.Name : id switch
    {
        "tree" => "Tree",
        "iron" => "Iron deposit",
        "loose_iron" => "Loose iron ore",
        "loose_coal" => "Loose coal",
        "grave" => "Recovery box",
        "wolf_corpse" => "Wolf carcass",
        _ => id.Replace('_', ' ')
    };
}
