using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Where the terrain draws the beds, one texel a world unit: red under the lake, green under the sea at its original
/// level, and blue for the tide at which the sea reaches each part of the beach.
/// </summary>
internal static class BedMap {
    /// <summary>
    /// World units past the drawn water's edge that the map still marks as bed, so filtering keeps the bed whole up to the
    /// water's edge; the banks cover the ground there.
    /// </summary>
    private const float Margin = 1;

    /// <summary>The map over <paramref name="span"/> world units square from <paramref name="corner"/> in world x and z.</summary>
    public static ImageTexture Bake(Vector2 corner, int span) {
        var beds = new byte[span * span * 3];
        // Rows are independent and the water's lines are read-only, so the map fills on all cores.
        Parallel.For(0, span, z => {
            for (int x = 0; x < span; x++) {
                var p = corner + new Vector2(x + .5f, z + .5f);
                int at = (z * span + x) * 3;
                float across = Terrain.Across(p);
                beds[at] = (byte)(Terrain.InLake(p, Margin) ? 255 : 0);
                beds[at + 1] = (byte)(Terrain.Along(p) > ShoreLook.SeaWaterEdge(across) - Margin ? 255 : 0);
                if (beds[at + 1] == 255) continue;
                // The shader reads back tide shares in [-0.25, 1.25] to feather the wet sand at both ends.
                float tide = Beach.TideOverSand(p, across, Terrain.Along(p) - Terrain.Waterline(across));
                beds[at + 2] = (byte)(255 * Mathf.Clamp((tide + .25f) / 1.5f, 0, 1));
            }
        });
        return ImageTexture.CreateFromImage(Image.CreateFromData(span, span, false, Image.Format.Rgb8, beds));
    }
}
