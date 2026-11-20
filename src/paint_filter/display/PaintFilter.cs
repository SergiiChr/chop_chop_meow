using Godot;

namespace ChopChopMeow;

/// <summary>
/// The paint filter: one full-screen pass of a Kuwahara filter over the drawn world, under the name tags and the
/// interface, so the world looks painted.
/// Hiding it skips its cost.
/// Each pixel takes its colour from half the shader's <c>kernel_size</c> to each side.
/// In the game that is an area 9 render pixels across.
/// </summary>
public partial class PaintFilter : CanvasLayer {
    private const string ShaderPath = "res://assets/environment/paint_filter/paint_filter.gdshader";
    private static readonly StringName KernelSize = "kernel_size";
    private readonly ShaderMaterial material = new() { Shader = GD.Load<Shader>(ShaderPath) };

    /// <summary>The shader's <c>kernel_size</c>, for a picture that is shown smaller than it is drawn.</summary>
    public int Width { set => material.SetShaderParameter(KernelSize, value); }

    public PaintFilter() {
        // Below the world's overlay on layer 0, but still drawn after the 3D view, so it filters only the world.
        Layer = -1;
        // Builds the shader while loading, so switching the filter on mid-game doesn't stall that frame on it.
        _ = material.GetRid();
        var cover = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
        cover.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(cover);
    }
}
