namespace ChopChopMeow;

/// <summary>Resolves a container holding fuel or liquid to the item used for its pile and appearance.</summary>
internal static class ContainerItem {
    public static string Base(string item) => Item.KindOf(item);
    /// <summary>Whether a look id, as <see cref="Vessels.LookOf"/> gives, shows a bottle at some fill.</summary>
    public static bool IsBottle(string look) => System.Array.IndexOf(Water.Looks, look) >= 0 || System.Array.IndexOf(BerryJam.Looks, look) >= 0;
}
