using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The outline gravel is drawn with and walked by, from the tiles laid: everything within <see cref="HalfWidth"/> of
/// the paths' middle lines.
/// A tile's middle is a point of those lines, so a tile alone is a round pad; tiles side by side are joined by a line
/// between their middles, and so are two tiles corner to corner with neither tile between them laid.
/// With one of those laid the path turns through it instead, and four tiles in a square are gravel all over.
/// </summary>
public static class GravelOutline {
    /// <summary>Metres from a path's middle line to its edge: a path is three quarters of a tile wide.</summary>
    public const float HalfWidth = .375f;
    /// <summary>The furthest from gravel <see cref="Distance"/> measures; further off it says this much.</summary>
    public const float Reach = .5f;

    /// <summary>
    /// Metres from a point to the gravel's edge: negative on gravel, positive off it up to <see cref="Reach"/>.
    /// It allocates nothing.
    /// </summary>
    public static float Distance(GravelPaths paths, Vector2 p) {
        var tile = GravelPaths.TileAt(p);
        // Only the nine tiles round the point have middle lines within reach of it.
        float nearest = HalfWidth + Reach;
        for (int y = tile.Y - 1; y <= tile.Y + 1; y++)
            for (int x = tile.X - 1; x <= tile.X + 1; x++) {
                if (!paths.Laid(x, y)) continue;
                var middle = new Vector2(x, y);
                nearest = MathF.Min(nearest, p.DistanceTo(middle));
                if (x <= tile.X && paths.Laid(x + 1, y)) nearest = MathF.Min(nearest, LineDistance(p, middle, new Vector2(x + 1, y)));
                if (y <= tile.Y && paths.Laid(x, y + 1)) nearest = MathF.Min(nearest, LineDistance(p, middle, new Vector2(x, y + 1)));
            }
        // Each square of four tiles among the nine: gravel all over, or one slanted line between its two laid tiles.
        for (int y = tile.Y - 1; y <= tile.Y; y++)
            for (int x = tile.X - 1; x <= tile.X; x++) {
                bool near = paths.Laid(x, y), right = paths.Laid(x + 1, y), below = paths.Laid(x, y + 1), far = paths.Laid(x + 1, y + 1);
                if (near && right && below && far) nearest = MathF.Min(nearest, (p - new Vector2(x + .5f, y + .5f)).Abs().Max(.5f).DistanceTo(new Vector2(.5f, .5f)));
                else if (near && far && !right && !below) nearest = MathF.Min(nearest, LineDistance(p, new Vector2(x, y), new Vector2(x + 1, y + 1)));
                else if (right && below && !near && !far) nearest = MathF.Min(nearest, LineDistance(p, new Vector2(x + 1, y), new Vector2(x, y + 1)));
            }
        return nearest - HalfWidth;
    }

    /// <summary>Distance from a point to the line from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static float LineDistance(Vector2 p, Vector2 from, Vector2 to) {
        var along = to - from;
        return p.DistanceTo(from + along * Math.Clamp((p - from).Dot(along) / along.LengthSquared(), 0, 1));
    }
}
