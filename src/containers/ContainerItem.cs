namespace ChopChopMeow;

/// <summary>Resolves a steel container's fill item to the item used for its pile and appearance.</summary>
internal static class ContainerItem {
    public static string Base(string item) => Barrel.Is(item) ? Barrel.Item : Bucket.Is(item) ? Bucket.Item : item;
}
