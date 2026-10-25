using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

public sealed class ItemDef
{
    public string Name { get; set; } = "";
    public float Price { get; set; }
    public string Color { get; set; } = "ffffff";
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
}
public sealed class GameData
{
    public float DaySeconds { get; set; } = 420;
    public float NightStarts { get; set; } = .7f;
    public float TradeSeconds { get; set; } = 30;
    public float HireCost { get; set; } = 24;
    public float DailyWage { get; set; } = 8;
    public float GatherSeconds { get; set; } = 1.5f;
    public float HungerPerSecond { get; set; } = .12f;
    public float RespawnSeconds { get; set; } = 30;
    public float ThirstPerSecond { get; set; } = .16f;
    /// <summary>Radius in tiles of the player's live view; sized to fill a 16:10 screen at full zoom-out.</summary>
    public float SightRadius { get; set; } = 20;
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
        if (DaySeconds <= 0 || TradeSeconds <= 0 || GatherSeconds <= 0 || NightStarts <= 0 || NightStarts >= 1 || HireCost < 0 || DailyWage < 0 || RespawnSeconds < 0 || SightRadius <= 0)
            throw new InvalidOperationException("Invalid time or economy balance in data/game.json.");
        foreach (var building in Buildings)
            foreach (var cost in building.Value.Cost)
                if (!Items.ContainsKey(cost.Key) || cost.Value <= 0) throw new InvalidOperationException($"Invalid cost for {building.Key}: {cost.Key}.");
        foreach (var recipe in Recipes)
        {
            if (!Buildings.ContainsKey(recipe.Value.Station) || !Items.ContainsKey(recipe.Value.Output) || recipe.Value.Amount <= 0 || recipe.Value.Seconds <= 0)
                throw new InvalidOperationException($"Invalid recipe: {recipe.Key}.");
            foreach (var input in recipe.Value.Inputs)
                if (!Items.ContainsKey(input.Key) || input.Value <= 0) throw new InvalidOperationException($"Invalid input for {recipe.Key}: {input.Key}.");
        }
    }
    public string Name(string id) => Items.TryGetValue(id, out var i) ? i.Name : Buildings.TryGetValue(id, out var b) ? b.Name : id switch
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
