using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The waves on the water, which the water and the lily pads share, and the vertex spacing they set.</summary>
internal static class Waves {
    /// <summary>The three waves: direction across the world in x and y, steepness in z and length in w.</summary>
    internal static readonly Vector4[] All = { new(1, 1, .35f, 3), new(1, .6f, .3f, 1.55f), new(1, 1.3f, .25f, .9f) };
    /// <summary>How fast the waves run, as a share of the speed their length gives them.</summary>
    internal const float Speed = .5f;
    /// <summary>World units between a water surface's vertices: three to the shortest wave.</summary>
    internal static readonly float Spacing = All.Min(wave => wave.W) / 3;
}
