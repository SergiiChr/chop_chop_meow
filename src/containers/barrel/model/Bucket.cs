namespace ChopChopMeow;

/// <summary>A ten-liter vessel that a worker can use on a water haul.</summary>
public static class Bucket {
    public const string Item = "steel_bucket";
    public const int Capacity = 10;

    /// <summary>Whether an item is a bucket, empty or holding something.</summary>
    public static bool Is(string item) => ChopChopMeow.Item.IsKind(item, Item);
}
