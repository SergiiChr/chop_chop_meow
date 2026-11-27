using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The Tools window beside a building's page, such as a station's tool slots or a cabin's roof barrel.
/// It opens rolled up, as a roll of the panel's blue board with "Tools" written up its side and an arrow above the word.
/// A click on the roll unrolls it: the roll moves left and leaves the board flat behind it, with the tools still in their places.
/// Fully unrolled, the roll has flattened into the board's left edge, which keeps the word and the arrow; another click rolls it back.
/// Each building opens it rolled up.
/// </summary>
public partial class ToolsWindow : Control {
    /// <summary>How long the roll takes to unroll or roll back, in seconds.</summary>
    private const float RollTime = .22f;
    /// <summary>The last part of unrolling, over which the roll flattens into the board.</summary>
    private const float FlattenPart = .3f;
    /// <summary>The room between the roll and what the board shows, and around the rest of it.</summary>
    private const int Inset = 12;
    private readonly Control clip;
    private readonly PanelContainer board;
    private readonly Roll roll;
    /// <summary>How far the window has got unrolling: 0 rolled up, 1 unrolled.</summary>
    private float progress;

    /// <summary>The column a page fills.</summary>
    public VBoxContainer Content { get; }
    /// <summary>Whether the window is unrolled, or unrolling.</summary>
    public bool Unrolled { get; private set; }
    /// <summary>How wide the window is once fully unrolled, with what it showed when last fitted.</summary>
    public float UnrolledWidth { get; private set; }

    public ToolsWindow(UiKit kit) {
        Name = "tools_window"; Visible = false; MouseFilter = MouseFilterEnum.Ignore;
        // The board lies in a strip that widens as the roll moves left, cut off at the strip's left edge, so it appears from under the roll.
        clip = new Control { Name = "tools_strip", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore }; AddChild(clip);
        board = new PanelContainer(); board.AddThemeStyleboxOverride("panel", Style.ToolsBoard); clip.AddChild(board);
        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left", Roll.Radius * 2 + Inset - 6); inner.AddThemeConstantOverride("margin_right", Inset);
        inner.AddThemeConstantOverride("margin_top", Inset); inner.AddThemeConstantOverride("margin_bottom", Inset);
        board.AddChild(inner);
        Content = new VBoxContainer(); Content.AddThemeConstantOverride("separation", 12); inner.AddChild(Content);
        // The roll comes after the strip, so it lies over the board's edge and takes clicks there.
        roll = new Roll("Tools") { Name = "tools_roll" };
        roll.Pressed += () => kit.Run(() => { Unrolled = !Unrolled; });
        AddChild(roll);
    }

    /// <summary>Empties the window for the next page.</summary>
    public void Clear() {
        foreach (var child in Content.GetChildren()) { Content.RemoveChild(child); child.QueueFree(); }
    }

    /// <summary>Rolls the window up at once, as each building opens it.</summary>
    public void RollUp() { Unrolled = false; progress = 0; }

    /// <summary>
    /// Moves the roll a frame's step toward where it is heading, then places it and sizes the board's strip to that step and to what the window shows now.
    /// The panel calls it every frame while the window shows.
    /// </summary>
    public void Fit() {
        progress = Mathf.MoveToward(progress, Unrolled ? 1 : 0, (float)GetProcessDeltaTime() / RollTime);
        float eased = Mathf.Ease(progress, .5f);
        // Round while it rolls along, then flat over the last part of unrolling.
        float round = Math.Clamp((1 - eased) / FlattenPart, 0, 1);
        var full = board.GetCombinedMinimumSize();
        UnrolledWidth = MathF.Round(full.X);
        float height = Math.Max(full.Y, Roll.Length);
        // The board's left edge stays under the roll's middle while it is round, and comes out to the roll's left side as it flattens.
        float strip = MathF.Round(full.X * eased), left = MathF.Round(Roll.Radius * round);
        Place(clip, new Vector2(left, 0), new Vector2(strip, height));
        // The board keeps its right edge on the strip's, which stays put beside the page, so the tools stay still while the roll moves.
        Place(board, new Vector2(strip - full.X, 0), new Vector2(full.X, height));
        Place(roll, Vector2.Zero, new Vector2(Roll.Radius * 2, height));
        // The roll is drawn as round as the strip is laid out for, so the two never show different moments.
        roll.Look(round, Unrolled);
        var size = new Vector2(Math.Max(Roll.Radius * 2, left + strip), height);
        if (Size != size) Size = size;
    }

    private static void Place(Control control, Vector2 position, Vector2 size) {
        if (control.Position != position) control.Position = position;
        if (control.Size != size) control.Size = size;
    }

    /// <summary>
    /// The rolled-up board drawn as a round tube standing on end: shaded blue with the board's cream edge round its top and bottom, the rolled end on top,
    /// and a word written from the bottom up with an arrow above it that points the way a click moves the roll.
    /// At the end of unrolling the tube flattens and fades into the board, so only the word and the arrow are left on the flat board.
    /// </summary>
    private sealed partial class Roll : Button {
        /// <summary>Half the roll's width.</summary>
        public const int Radius = 18;
        /// <summary>The shortest the roll is, which fits its word and arrow.</summary>
        public const float Length = 112;
        private const int FontSize = 17, Strips = 14, EllipsePoints = 28;
        /// <summary>How tall the roll's top and bottom ellipses are, half their height.</summary>
        private const float Cap = 5;
        /// <summary>How tall the board's cream edge is round the roll.</summary>
        private const float Edge = 4;
        /// <summary>A circle's points, scaled into each ellipse the rolled end draws.</summary>
        private static readonly Vector2[] Circle = MakeCircle();
        private readonly string text;
        private readonly float textLength, textLift;
        /// <summary>How round the tube is drawn, from 1 round to 0 flat.</summary>
        private float shape = 1;
        private bool pointsRight;
        // Kept between draws, so drawing each frame of the roll's move allocates nothing.
        private readonly Vector2[] quad = new Vector2[4];
        private readonly Color[] shades = new Color[4];
        private readonly float[] fronts = new float[Strips + 1];
        private readonly Vector2[] outline = new Vector2[Strips + 3];
        private readonly Vector2[] arrow = new Vector2[3];
        private readonly Vector2[] ellipseArea = new Vector2[EllipsePoints];
        private readonly Vector2[] ellipseRing = new Vector2[EllipsePoints + 1];

        public Roll(string text) {
            this.text = text;
            textLength = Style.Head.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize).X;
            textLift = (Style.Head.GetAscent(FontSize) - Style.Head.GetDescent(FontSize)) / 2;
            MouseDefaultCursorShape = CursorShape.PointingHand; FocusMode = FocusModeEnum.None;
            Style.Skin(this, Style.None, Style.None);
        }

        /// <summary>Draws the roll as round as <paramref name="round"/>, with its arrow pointing right once <paramref name="unrolled"/>.</summary>
        public void Look(float round, bool unrolled) {
            if (shape == round && pointsRight == unrolled) return;
            shape = round; pointsRight = unrolled;
            QueueRedraw();
        }

        public override void _Draw() {
            float top = Cap, bottom = Size.Y - Cap;
            // The tube stays solid until it is nearly flat, then fades into the board's edge.
            float shown = Math.Min(1, shape * 3);
            if (shown > 0) {
                for (int i = 0; i <= Strips; i++) { float t = 2f * i / Strips - 1; fronts[i] = Cap * shape * MathF.Sqrt(Math.Max(0, 1 - t * t)); }
                Band(top, top + Edge, Style.Rim, shown);
                Band(top + Edge, bottom - Edge, Style.Enamel, shown);
                Band(bottom - Edge, bottom, Style.Rim, shown);
                // The outline down both sides and round the front of the bottom.
                outline[0] = new Vector2(0, top);
                for (int i = 0; i <= Strips; i++) outline[i + 1] = new Vector2(X(i), bottom + fronts[i]);
                outline[^1] = new Vector2(Radius * 2, top);
                DrawPolyline(outline, Style.Line with { A = shown }, 1.5f, true);
                DrawRolledEnd(new Vector2(Radius, top), shown);
            }
            var ink = IsHovered() ? Style.Mustard : Style.Rim;
            // The arrow under the rolled end points left while the roll is up, the way a click unrolls it, and right once it is down.
            float arrowY = top + Edge + 13, side = pointsRight ? 1 : -1;
            arrow[0] = new Vector2(Radius + 5 * side, arrowY); arrow[1] = new Vector2(Radius - 4 * side, arrowY - 6); arrow[2] = new Vector2(Radius - 4 * side, arrowY + 6);
            DrawColoredPolygon(arrow, ink);
            float middle = (arrowY + 10 + bottom - Edge) / 2;
            // Turned a quarter left, the text runs up the roll and the tops of its letters face left.
            DrawSetTransform(new Vector2(Radius + textLift, middle + textLength / 2), -Mathf.Pi / 2);
            DrawString(Style.Head, Vector2.Zero, text, HorizontalAlignment.Left, -1, FontSize, ink);
            DrawSetTransform(Vector2.Zero);
        }

        /// <summary>Where the edge before strip <paramref name="i"/> lies across the roll.</summary>
        private static float X(int i) => Radius * 2f * i / Strips;

        /// <summary>A ring of the roll between two heights, lit from the upper left so it reads as round, as opaque as <paramref name="shown"/>.</summary>
        private void Band(float from, float to, Color color, float shown) {
            var left = Lit(color, 0) with { A = shown };
            for (int i = 0; i < Strips; i++) {
                var right = Lit(color, i + 1) with { A = shown };
                quad[0] = new Vector2(X(i), from + fronts[i]); quad[1] = new Vector2(X(i + 1), from + fronts[i + 1]);
                quad[2] = new Vector2(X(i + 1), to + fronts[i + 1]); quad[3] = new Vector2(X(i), to + fronts[i]);
                shades[0] = shades[3] = left; shades[1] = shades[2] = right;
                DrawPolygon(quad, shades);
                left = right;
            }
        }

        /// <summary>A colour as the roll's surface shows it at strip edge <paramref name="i"/>: brightest a little left of the middle, darkest at the sides, and even once flat.</summary>
        private Color Lit(Color color, int i) {
            float t = (2f * i / Strips - 1 + .3f) / 1.3f, light = 1 - t * t;
            return color.Lerp(light > .75f ? color.Lightened((light - .75f) * .6f) : color.Darkened((.75f - light) * .55f), shape);
        }

        /// <summary>The roll's top seen from a little above: the board's cream edge wound round, with the turns of the board showing as rings.</summary>
        private void DrawRolledEnd(Vector2 centre, float shown) {
            float cap = Cap * shape;
            DrawEllipse(centre, Radius, cap, Style.Beige with { A = shown }, filled: true);
            DrawEllipse(centre, Radius * .66f, cap * .66f, Style.Line with { A = .45f * shown }, filled: false);
            DrawEllipse(centre, Radius * .33f, cap * .33f, Style.Line with { A = .45f * shown }, filled: false);
            DrawEllipse(centre, Radius, cap, Style.Line with { A = shown }, filled: false);
        }

        private void DrawEllipse(Vector2 centre, float across, float high, Color color, bool filled) {
            var points = filled ? ellipseArea : ellipseRing;
            var scale = new Vector2(across, high);
            for (int i = 0; i < points.Length; i++) points[i] = centre + Circle[i % EllipsePoints] * scale;
            if (filled) DrawColoredPolygon(points, color); else DrawPolyline(points, color, 1.2f, true);
        }

        private static Vector2[] MakeCircle() {
            var points = new Vector2[EllipsePoints];
            for (int i = 0; i < EllipsePoints; i++) points[i] = Vector2.FromAngle(Mathf.Tau * i / EllipsePoints);
            return points;
        }
    }
}
