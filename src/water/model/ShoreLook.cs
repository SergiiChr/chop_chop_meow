namespace ChopChopMeow;

/// <summary>A shore photoscan and how it is laid along the water.</summary>
/// <param name="Name">The file prefix of its colour, bumps and height maps in the lake folder.</param>
/// <param name="Tile">World units one tile of the photoscan spans.</param>
/// <param name="Wet">Brightness at the waterline, where it is wet, as a share of the dry shore's.</param>
/// <param name="GrassFade">Units over which the shore thins out into the grass.</param>
/// <param name="Wander">Units over which the shore's edges wander in small bites: the waterline half that either side
/// of its line, the grass edge that much on top of its broad patches.</param>
internal sealed record ShoreLook(string Name, float Tile, float Wet, float GrassFade, float Wander) {
    /// <summary>Rippled beach sand along the sea coast.</summary>
    internal static readonly ShoreLook Sand = new("sand", 15, .6f, 2.2f, 1.2f);
    /// <summary>Mossy ground with stones and bare earth around the pond.</summary>
    internal static readonly ShoreLook Rocks = new("rocks", 15, .6f, 1.5f, 0);

    /// <summary>Along the valley, the most inland the sea's drawn water reaches, where its waterline wanders furthest in.</summary>
    internal static float SeaWaterEdge(float across) => Terrain.Waterline(across) - Sand.Wander / 2;
}
