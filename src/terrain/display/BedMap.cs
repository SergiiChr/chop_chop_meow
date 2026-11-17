using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The map of where the drawn water lies, one texel a world unit, which the terrain draws the beds by: red under the
/// pond's water and green under the sea's.
/// </summary>
internal static class BedMap {
    /// <summary>
    /// World units past the drawn water's edge that the map still marks as bed, so filtering keeps the bed whole up to the
    /// water's edge; the banks cover the ground there.
    /// </summary>
    private const float Margin = 1;

    /// <summary>The map over <paramref name="span"/> world units square from <paramref name="corner"/> in world x and z.</summary>
    public static ImageTexture Bake(Vector2 corner, int span) {
        var beds = new byte[span * span * 2];
        // Rows are independent and the water's lines are read-only, so the map fills on all cores.
        Parallel.For(0, span, z => {
            for (int x = 0; x < span; x++) {
                var p = corner + new Vector2(x + .5f, z + .5f);
                beds[(z * span + x) * 2] = (byte)(Terrain.InLake(p, Margin) ? 255 : 0);
                beds[(z * span + x) * 2 + 1] = (byte)(Terrain.Along(p) > ShorelineAppearance.SeaWaterEdge(Terrain.Across(p)) - Margin ? 255 : 0);
            }
        });
        return ImageTexture.CreateFromImage(Image.CreateFromData(span, span, false, Image.Format.Rg8, beds));
    }
}
