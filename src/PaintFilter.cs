using Godot;

namespace ChopChopMeow;

/// <summary>
/// The paint filter: one full-screen pass of a Kuwahara filter over the drawn world, under the name tags and the
/// interface, so the world looks painted.
/// Hiding it skips its cost.
/// Each pixel takes its colour from an area 9 render pixels across, half the shader's <c>kernel_size</c> to each side.
/// </summary>
public partial class PaintFilter : CanvasLayer {
    private const string ShaderPath = "res://assets/environment/paint_filter/paint_filter.gdshader";

    public PaintFilter() {
        // Below the world's overlay on layer 0, but still drawn after the 3D view, so it filters only the world.
        Layer = -1;
        var cover = new ColorRect { Name = "paint_filter", Material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) }, MouseFilter = Control.MouseFilterEnum.Ignore };
        cover.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(cover);
    }
}
