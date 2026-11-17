using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A body of water's look: its three waves, which its lily pads share, and its deep and shallow colours.</summary>
/// <param name="Waves">Each wave's direction across the world in x and y, steepness in z and length in w.</param>
/// <param name="Deep">The colour where the view passes through a lot of water.</param>
/// <param name="Shallow">The colour where it passes through little.</param>
/// <param name="Foam">How much of the shader's foam lies over its shallow water, from none at zero to all of it at one.</param>
internal sealed record WaterBody(Vector4[] Waves, Color Deep, Color Shallow, float Foam) {
    /// <summary>The sea: the shader's own waves and foam, and a cyan deep and light cyan shallows.</summary>
    internal static readonly WaterBody Sea = new(
        [new(1, 1, .35f, 3), new(1, .6f, .3f, 1.55f), new(1, 1.3f, .25f, .9f)],
        new(.35f, .72f, .78f), new(.70f, .93f, .92f), 1);
    /// <summary>The lake: the sea's waves at a twelfth of their steepness, lighter cyan than the sea, and no foam.</summary>
    internal static readonly WaterBody Lake = new(
        Sea.Waves.Select(wave => new Vector4(wave.X, wave.Y, wave.Z / 12, wave.W)).ToArray(),
        new(.45f, .82f, .86f), new(.80f, .97f, .96f), 0);
    /// <summary>How fast the waves run, as a share of the speed their length gives them.</summary>
    internal const float Speed = .5f;
    /// <summary>World units between a water surface's vertices: three to the shortest wave.</summary>
    internal static readonly float Spacing = Sea.Waves.Concat(Lake.Waves).Min(wave => wave.W) / 3;
}
