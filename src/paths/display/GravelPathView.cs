using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Laid gravel paths as the ground draws them: a map of how far each spot is from the gravel's edge
/// (<see cref="GravelOutline"/>), which the ground's and its plants' shaders read.
/// The map is redrawn only round tiles laid or taken up.
/// </summary>
internal sealed class GravelPathView {
    /// <summary>
    /// Texels a tile each way: the distance between them reads smoothly, so round ends and slanted edges stay clean.
    /// </summary>
    private const int Scale = 8, Size = Terrain.Size * Scale;
    /// <summary>
    /// Tiles round a changed one whose texels may change with it: its strips reach the tiles next to it, and the map
    /// reaches half a metre past the gravel.
    /// </summary>
    private const int Spread = 2;
    private readonly byte[] field = new byte[Size * Size];
    /// <summary>The tiles the map was last drawn from, and those to draw again.</summary>
    private readonly bool[] shown = new bool[Terrain.Size * Terrain.Size], stale = new bool[Terrain.Size * Terrain.Size];
    private readonly Image image;
    private int revision = -1;

    /// <summary>
    /// The map: half at the gravel's edge, rising to one half a metre inside it and falling to zero half a metre
    /// outside.
    /// The texel at the map's corner is centred a sixteenth of a tile inside the first tile's corner, at -0.5 + 1/16.
    /// </summary>
    public ImageTexture Texture { get; }

    public GravelPathView() {
        image = Image.CreateFromData(Size, Size, false, Image.Format.R8, field);
        Texture = ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Redraws the map round every tile laid or taken up since the last call; with nothing changed it does nothing and
    /// allocates nothing.
    /// </summary>
    public void Update(GravelPaths paths) {
        if (paths.Revision == revision) return;
        revision = paths.Revision;
        bool changed = false;
        for (int y = 0; y < Terrain.Size; y++)
            for (int x = 0; x < Terrain.Size; x++) {
                int tile = Terrain.TileIndex(new Vector2I(x, y));
                bool laid = paths.Laid(x, y);
                if (laid == shown[tile]) continue;
                shown[tile] = laid; changed = true;
                for (int sy = Math.Max(0, y - Spread); sy <= Math.Min(Terrain.Size - 1, y + Spread); sy++)
                    for (int sx = Math.Max(0, x - Spread); sx <= Math.Min(Terrain.Size - 1, x + Spread); sx++) stale[Terrain.TileIndex(new Vector2I(sx, sy))] = true;
            }
        if (!changed) return;
        for (int y = 0; y < Terrain.Size; y++)
            for (int x = 0; x < Terrain.Size; x++) {
                int tile = Terrain.TileIndex(new Vector2I(x, y));
                if (!stale[tile]) continue;
                stale[tile] = false;
                Draw(paths, x, y);
            }
        image.SetData(Size, Size, false, Image.Format.R8, field);
        Texture.Update(image);
    }

    /// <summary>Draws one tile's texels from the outline.</summary>
    private void Draw(GravelPaths paths, int x, int y) {
        for (int ty = 0; ty < Scale; ty++)
            for (int tx = 0; tx < Scale; tx++) {
                var at = new Vector2(x - .5f + (tx + .5f) / Scale, y - .5f + (ty + .5f) / Scale);
                float inside = .5f - GravelOutline.Distance(paths, at);
                field[(y * Scale + ty) * Size + x * Scale + tx] = (byte)MathF.Round(Math.Clamp(inside, 0, 1) * 255);
            }
    }
}
