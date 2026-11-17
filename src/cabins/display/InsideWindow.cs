using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The window that shows a player inside a cabin the picture of a wolf forcing its door, <see cref="InsideSet"/>, as an old
/// tape would: in the left third of the screen, a fifth of its width and four fifths of its height.
/// It shows only while the picture is drawn.
/// </summary>
public partial class InsideWindow : TextureRect {
    private const string ShaderPath = "res://assets/environment/vhs/vhs.gdshader";
    /// <summary>The window's share of the screen's width and height, and where across the screen its middle is.</summary>
    private const float Width = .2f, Tall = .8f, Middle = 1f / 6;
    /// <summary>How coarse the tape is: its smears and jitters are this many to the picture's width and height.</summary>
    private static readonly Vector2 TapeLines = new(180, 405);
    private readonly InsideSet set;

    public InsideWindow(InsideSet set) {
        this.set = set;
        Name = "inside"; Visible = false; MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = Middle - Width / 2; AnchorRight = Middle + Width / 2; AnchorTop = (1 - Tall) / 2; AnchorBottom = (1 + Tall) / 2;
        Texture = set.GetTexture(); ExpandMode = ExpandModeEnum.IgnoreSize; StretchMode = StretchModeEnum.Scale; TextureFilter = TextureFilterEnum.Linear;
        var tape = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        tape.SetShaderParameter("vhs_resolution", TapeLines);
        tape.SetShaderParameter("noise_texture", Noise());
        // Builds the shader while loading, so the window's first frame mid-game doesn't stall on it.
        _ = tape.GetRid();
        Material = tape;
    }

    public override void _Process(double delta) {
        if (Visible != set.Showing) Visible = set.Showing;
        if (Visible && Size.Y > 0) set.Fit(Size.X / Size.Y);
    }

    /// <summary>Coloured noise for the tape's grain and jitter, the same on every run.</summary>
    private static ImageTexture Noise() {
        const int side = 256;
        var grain = new byte[side * side * 4];
        new Random(1987).NextBytes(grain);
        var image = Image.CreateFromData(side, side, false, Image.Format.Rgba8, grain);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
