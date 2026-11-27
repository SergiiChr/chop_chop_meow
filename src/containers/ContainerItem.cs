namespace ChopChopMeow;

/// <summary>Resolves a container holding fuel or liquid to the item used for its pile and appearance.</summary>
internal static class ContainerItem {
    public static string Base(string item) => Barrel.Is(item) ? Barrel.Item : Bucket.Is(item) ? Bucket.Item : Lanterns.IsItem(item) ? Lanterns.Item : item;
    public static bool IsBottle(string item) => Water.IsBottle(item) || BerryJam.InBottle(item);
}
