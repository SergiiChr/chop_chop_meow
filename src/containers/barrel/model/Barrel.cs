namespace ChopChopMeow;

/// <summary>A steel barrel carried as one item: empty, a plain item that stacks, or an object holding its liquid.</summary>
public static class Barrel {
    public const string Item = "steel_barrel";
    public const int Capacity = 50;

    /// <summary>Whether an item is a barrel, empty or holding something.</summary>
    public static bool Is(string item) => ChopChopMeow.Item.IsKind(item, Item);
    /// <summary>Whether an item is a barrel holding some liquid, an object found by its key.</summary>
    public static bool IsFilled(string item) => ChopChopMeow.Item.IsKey(item) && Is(item);
    /// <summary>The liquid a barrel holds, water for an empty one.</summary>
    public static string LiquidOf(ChopChopMeow.Item? barrel) => barrel?.Contents?.First() ?? Water.Liquid;
    /// <summary>The share of a walker's speed left to one carrying a barrel holding so many liters.</summary>
    public static float Speed(int liters) => 1 - .5f * liters / Capacity;
    /// <summary>A barrel's name holding so many liters of a liquid.</summary>
    public static string Name(int liters, string liquid, GameData data) {
        if (liters <= 0) return data.Items[Item].Name;
        if (!names.TryGetValue((liters, liquid), out var name))
            names[(liters, liquid)] = name = liquid == BerryJam.Liquid ? $"{data.Items[Item].Name} ({liters} L berry jam)" : $"{data.Items[Item].Name} ({liters} L)";
        return name;
    }
    // Each fill's name, made once, so text built every tick allocates nothing.
    private static readonly System.Collections.Generic.Dictionary<(int, string), string> names = new();

    public static string? In(Inventory inventory) {
        foreach (var (item, _) in inventory.Items) if (Is(item)) return item;
        return null;
    }

    public static string? FilledIn(Inventory inventory) {
        foreach (var (item, _) in inventory.Items) if (IsFilled(item)) return item;
        return null;
    }
}
