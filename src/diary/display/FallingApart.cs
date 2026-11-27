using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Handwriting falling apart, as if written in the dark: each letter strays further off the line than the one before,
/// sinking, tipping and changing size, and some land back over the letter before them.
/// It writes the text between <c>[falling_apart]</c> and <c>[/falling_apart]</c> in a <see cref="RichTextLabel"/>, and every letter keeps its place from frame to frame.
/// </summary>
public partial class FallingApart : RichTextEffect {
    /// <summary>The tag that marks the writing in the label's text.</summary>
    public const string Tag = "falling_apart";
    // The label finds the effect's tag in a member of this name.
#pragma warning disable IDE1006
    public string bbcode = Tag;
#pragma warning restore IDE1006
    /// <summary>Letters it takes the writing to fall apart fully.</summary>
    private const float Letters = 9;

    public override bool _ProcessCustomFX(CharFXTransform fx) {
        int i = fx.RelativeIndex;
        float worse = Math.Min(1, (i + 1) / Letters);
        // The line sags further with each letter, and each letter strays up or down off it.
        float sink = i * 1.1f + worse * 7 * Noise(i, 1);
        // Now and then a letter lands back over the one before it.
        float back = Noise(i, 2) > .2f ? -worse * 6 : worse * 3 * Noise(i, 3);
        fx.Offset = new Vector2(back, sink);
        fx.Transform = fx.Transform.RotatedLocal(Mathf.DegToRad(worse * 24 * Noise(i, 4))).ScaledLocal(Vector2.One * (1 + worse * .3f * Noise(i, 5)));
        return true;
    }

    /// <summary>A number from -1 to 1 that a letter's place and a salt always give alike.</summary>
    private static float Noise(int i, int salt) {
        uint h = (uint)(i * 374761393 + salt * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) % 2001 / 1000f - 1;
    }
}
