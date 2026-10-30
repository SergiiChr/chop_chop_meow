using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The enamel camp-gear look shared by every panel: speckled slate-blue enamel with a cream rim, beige wells,
/// cream cups for items with a count pill on their corner, and rounded buttons in muted brick, sage and mustard.
/// Style boxes, fonts and the enamel texture are made once, since creating a resource allocates.
/// </summary>
public static class Style {
    public static readonly Color Enamel = new("3b5470");
    public static readonly Color Line = new("263849");
    public static readonly Color Rim = new("efe6d2");
    public static readonly Color Beige = new("e3d5b8");
    public static readonly Color CupFill = new("f6f0e2");
    public static readonly Color Ink = new("4a3d2e");
    public static readonly Color Tan = new("6d5b45");
    public static readonly Color Brick = new("b65a43");
    public static readonly Color Mustard = new("c9a14f");
    public static readonly Color Sage = new("6b8a6e");
    public static readonly Color Sky = new("6fa3c4");
    public static readonly Color Ember = new("d98a4e");
    /// <summary>Text on enamel that warns: a cost the bank cannot cover, a count the backpack is short of.</summary>
    public static readonly Color Warn = new("f2937c");
    public static readonly Color Rise = new("4f7a55");
    /// <summary>Modulates: a faint picture of what an empty cup wants, something not ready yet, and what the open page does not take.</summary>
    public static readonly Color Ghost = new(1, 1, 1, .28f), Unready = new(1, 1, 1, .5f), Faded = new(1, 1, 1, .4f);

    // Boxes by colour are made once and shared, since creating a resource allocates; declared first so the boxes below can use them.
    private static readonly System.Collections.Generic.Dictionary<Color, StyleBoxFlat> tags = new();
    private static readonly System.Collections.Generic.Dictionary<Color, (StyleBoxFlat, StyleBoxFlat)> pills = new();
    private static readonly System.Collections.Generic.Dictionary<(Color, int), (StyleBoxFlat, StyleBoxFlat)> rounds = new();

    public static readonly Font Head = Weighted("res://assets/fonts/Fredoka.ttf", 600);
    public static readonly Font Body = Weighted("res://assets/fonts/Nunito.ttf", 700);
    public static readonly Font Bold = Weighted("res://assets/fonts/Nunito.ttf", 800);

    private static Font Weighted(string path, int weight) {
        var font = new FontVariation { BaseFont = GD.Load<FontFile>(path) };
        font.VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("wght"), weight } };
        return font;
    }

    // Built before the boards that use it, since static fields start in the order they are written.
    private static readonly Texture2D EnamelTexture = MakeEnamel();
    /// <summary>The enamel board every panel sits on: a cream rim with a dark outline around speckled enamel.</summary>
    public static readonly StyleBoxTexture Board = EnamelBox(20, 18);
    /// <summary>The HUD's smaller enamel plates.</summary>
    public static readonly StyleBoxTexture Plate = EnamelBox(14, 10);
    /// <summary>A beige well inside a board, holding a station's process row or a group of slots.</summary>
    public static readonly StyleBoxFlat Well = Flat(Beige, 14, 16);
    /// <summary>A goal still to do on the pause board: an empty ring.</summary>
    public static readonly StyleBoxFlat GoalTodo = Flat(Beige, 12, 0, Tan, 3);
    /// <summary>A job chip on a worker card: a small cream pill with a thin outline.</summary>
    public static readonly StyleBoxFlat Chip = Flat(CupFill, 13, 2, Line, 2, horizontal: 8);
    /// <summary>The ring a clickable well, such as a worker card, gets on hover.</summary>
    public static readonly StyleBoxFlat CardHover = Flat(Colors.Transparent, 14, 0, Mustard, 3);
    /// <summary>The trading post's price sign: beige enamel in a slate frame.</summary>
    public static readonly StyleBoxFlat Sign = Flat(Beige, 16, 16, Enamel, 5);
    /// <summary>The worker list that drops down from a job button.</summary>
    public static readonly StyleBoxFlat Dropdown = Flat(Beige, 12, 8, Line, 2);
    public static readonly StyleBoxFlat Tooltip = Flat(Line, 10, 10, Rim, 2);
    public static readonly StyleBoxFlat ButtonNormal = Flat(new Color("4b6582"), 20, 10, Rim, 3);
    public static readonly StyleBoxFlat ButtonHover = Flat(new Color("5a7593"), 20, 10, Rim, 3);
    public static readonly StyleBoxFlat ButtonPressed = Flat(Brick, 20, 10, Rim, 3);
    public static readonly StyleBoxFlat ButtonDisabled = Flat(new Color("40536a"), 20, 10, new Color(Rim, .5f), 3);
    public static readonly StyleBoxFlat CupNormal = Flat(CupFill, 14, 3, Line, 2);
    public static readonly StyleBoxFlat CupHover = Flat(CupFill, 14, 3, Mustard, 3);
    public static readonly StyleBoxFlat CupPressed = Flat(new Color("ece2cc"), 14, 3, Brick, 3);
    /// <summary>A cup whose item the open page takes, a station's output, or the item in hand.</summary>
    public static readonly StyleBoxFlat CupWanted = Flat(CupFill, 14, 3, Brick, 3);
    public static readonly StyleBoxFlat CupFuel = Flat(CupFill, 14, 3, Ember, 3);
    public static readonly StyleBoxFlat CupBank = Flat(CupFill, 28, 3, Line, 2);
    /// <summary>A recipe plate: a larger cup; the recipe a station makes is ringed in mustard.</summary>
    public static readonly StyleBoxFlat PlateNormal = Flat(CupFill, 16, 6, Line, 2), PlateHover = Flat(CupFill, 16, 6, Mustard, 3), PlateChosen = Flat(CupFill, 16, 6, Mustard, 4);
    /// <summary>The pill on a cup's corner that counts what it holds.</summary>
    public static readonly StyleBoxFlat CountPill = Flat(Line, 9, 0, Rim, 2, horizontal: 5);
    /// <summary>The count beside a job's name, and a menu badge's key.</summary>
    public static readonly StyleBoxFlat BeigePill = Flat(Beige, 12, 0, horizontal: 6);
    public static readonly StyleBoxFlat KeyPill = Flat(Beige, 9, 0, Line, 2, horizontal: 5);
    public static readonly StyleBoxFlat TagPill = Tag(Sage), BlueTag = Tag(Enamel);
    /// <summary>A flat pill for a short label, with room either side of its text.</summary>
    public static StyleBoxFlat Tag(Color bg) {
        if (!tags.TryGetValue(bg, out var box)) tags[bg] = box = Flat(bg, 13, 0, horizontal: 10);
        return box;
    }
    public static readonly StyleBoxFlat BarBack = Flat(Line, 7, 0);
    public static readonly StyleBoxFlat BarFill = Flat(Mustard, 7, 0);
    public static readonly StyleBoxFlat BarGrow = Flat(Sage, 7, 0);
    public static readonly StyleBoxEmpty None = new();

    /// <summary>The colours a job button comes in; the first is for the main job of a place, the next for hauling.</summary>
    public static readonly Color[] JobColors = { Brick, Sage, new("b08a45"), new("5f8aa6") };
    /// <summary>Round buttons in a panel's header: close in brick, an action in sage, filters in beige.</summary>
    public static readonly (StyleBoxFlat Normal, StyleBoxFlat Hover) CloseRound = Round(Brick, 18), ActionRound = Round(Sage, 18), FilterRound = Round(Beige, 18);
    public static readonly (StyleBoxFlat Normal, StyleBoxFlat Hover) TabRound = Round(Line, 24), TabActive = Round(Brick, 24);

    private static StyleBoxFlat Flat(Color bg, int radius, int margin, Color? edge = null, int border = 0, int horizontal = -1) {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = edge ?? Colors.Transparent, AntiAliasing = true };
        box.SetCornerRadiusAll(radius);
        if (border > 0) box.SetBorderWidthAll(border);
        box.ContentMarginTop = box.ContentMarginBottom = margin;
        box.ContentMarginLeft = box.ContentMarginRight = horizontal >= 0 ? horizontal : margin;
        return box;
    }

    public static (StyleBoxFlat, StyleBoxFlat) Round(Color bg, int radius) {
        if (!rounds.TryGetValue((bg, radius), out var look)) rounds[(bg, radius)] = look = (Flat(bg, radius, 0, Rim, 3), Flat(bg.Lightened(.12f), radius, 0, Mustard, 3));
        return look;
    }

    /// <summary>A rounded job or action button's colours: its fill with the cream rim, and a lighter fill on hover.</summary>
    public static (StyleBoxFlat, StyleBoxFlat) Pill(Color bg) {
        if (!pills.TryGetValue(bg, out var look)) pills[bg] = look = (Flat(bg, 24, 0, Rim, 3, horizontal: 14), Flat(bg.Lightened(.1f), 24, 0, Mustard, 3, horizontal: 14));
        return look;
    }

    /// <summary>Sets a button's states to the given boxes.</summary>
    public static void Skin(Button button, StyleBox normal, StyleBox hover, StyleBox? pressed = null) {
        button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed ?? hover); button.AddThemeStyleboxOverride("hover_pressed", pressed ?? hover);
        button.AddThemeStyleboxOverride("disabled", normal); button.AddThemeStyleboxOverride("focus", None);
    }

    /// <summary>The theme of the whole interface.</summary>
    public static Theme Interface() {
        var theme = new Theme { DefaultFont = Body };
        theme.DefaultFontSize = 16;
        theme.SetStylebox("panel", "PanelContainer", Board);
        theme.SetStylebox("normal", "Button", ButtonNormal);
        theme.SetStylebox("hover", "Button", ButtonHover);
        theme.SetStylebox("pressed", "Button", ButtonPressed);
        theme.SetStylebox("disabled", "Button", ButtonDisabled);
        theme.SetStylebox("hover_pressed", "Button", ButtonPressed);
        theme.SetStylebox("focus", "Button", None);
        theme.SetFont("font", "Button", Head);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) theme.SetColor(state, "Button", Rim);
        theme.SetColor("font_disabled_color", "Button", new Color(Rim, .45f));
        theme.SetColor("font_color", "Label", Rim);
        theme.SetColor("font_color", "CheckBox", Rim);
        theme.SetStylebox("normal", "CheckBox", None); theme.SetStylebox("hover", "CheckBox", None); theme.SetStylebox("pressed", "CheckBox", None); theme.SetStylebox("hover_pressed", "CheckBox", None);
        theme.SetConstant("separation", "VBoxContainer", 16);
        theme.SetConstant("separation", "HBoxContainer", 10);
        theme.SetStylebox("normal", "OptionButton", ButtonNormal);
        theme.SetStylebox("background", "ProgressBar", BarBack);
        theme.SetStylebox("fill", "ProgressBar", BarFill);
        // Tooltips read as solid popups with a bright edge rather than a translucent wash over the scene.
        theme.SetStylebox("panel", "TooltipPanel", Tooltip);
        theme.SetColor("font_color", "TooltipLabel", Rim);
        theme.SetFont("font", "TooltipLabel", Bold);
        return theme;
    }

    /// <summary>A label in the theme's body font that wraps at its width.</summary>
    public static Label Wrapped(string text, int size = 15, Color? color = null) {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", size); if (color != null) l.AddThemeColorOverride("font_color", color.Value); return l;
    }

    /// <summary>Text over the world gets a dark outline so it reads on grass by day and night.</summary>
    public static Label Outlined(Label label) {
        label.AddThemeColorOverride("font_outline_color", Line); label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    public static Label Heading(string text, int size = 23) {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontOverride("font", Head); label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Rim);
        return label;
    }

    /// <summary>Plain text in the given colour, the body face by default.</summary>
    public static Label Text(string text, int size, Color color, Font? font = null) {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Off, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font ?? Bold); label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>A small pill with a key or count on it.</summary>
    /// <summary>A small heading at the top of a well.</summary>
    public static Label WellLabel(string text) => Text(text, 13, Tan, Head);

    public static Label Pill(string text, StyleBox box, Color color, int size = 12) {
        var label = Text(text, size, color);
        label.AddThemeStyleboxOverride("normal", box); label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    public static TextureRect Icon(Texture2D? texture, float size) => new() {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    /// <summary>A line icon from <c>assets/ui</c>, tinted for the background it sits on.</summary>
    public static TextureRect Glyph(string name, float size, Color tint) {
        var icon = Icon(GD.Load<Texture2D>($"res://assets/ui/{name}.svg"), size);
        icon.SelfModulate = tint;
        return icon;
    }

    /// <summary>Lays a control on a parent's corner: <paramref name="x"/> and <paramref name="y"/> are 0 or 1 for the side, the offset pushes it past the edge.</summary>
    public static T Corner<T>(T control, float x, float y, float offset = 6) where T : Control {
        control.AnchorLeft = control.AnchorRight = x; control.AnchorTop = control.AnchorBottom = y;
        control.GrowHorizontal = x > .5f ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        control.GrowVertical = y > .5f ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        control.OffsetLeft = control.OffsetRight = x > .5f ? offset : -offset;
        control.OffsetTop = control.OffsetBottom = y > .5f ? offset : -offset;
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        return control;
    }

    /// <summary>A board's texture: a rounded square with an outline, a cream rim and speckled enamel, sliced so the rim stays put and the enamel tiles.</summary>
    private static StyleBoxTexture EnamelBox(int horizontal, int vertical) => new() {
        Texture = EnamelTexture,
        TextureMarginLeft = 24,
        TextureMarginRight = 24,
        TextureMarginTop = 24,
        TextureMarginBottom = 24,
        AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit,
        AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit,
        ContentMarginLeft = horizontal,
        ContentMarginRight = horizontal,
        ContentMarginTop = vertical,
        ContentMarginBottom = vertical
    };

    private static Texture2D MakeEnamel() {
        const int Size = 96; const float Radius = 18;
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        var random = new Random(7);
        var speckles = new bool[Size * Size];
        for (int i = 0; i < 70; i++) speckles[random.Next(Size) + random.Next(Size) * Size] = true;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++) {
                // Distance inside the rounded square's edge, so each band blends a pixel wide.
                float dx = Math.Max(Radius - x - .5f, x + .5f - (Size - Radius)), dy = Math.Max(Radius - y - .5f, y + .5f - (Size - Radius));
                float outside = dx > 0 && dy > 0 ? MathF.Sqrt(dx * dx + dy * dy) - Radius : Math.Max(dx, dy) - Radius;
                float depth = -outside;
                Color c = depth < 2 ? Line : depth < 6 ? Rim : speckles[x + y * Size] ? Enamel.Lerp(Colors.White, .22f) : Enamel;
                if (depth > 1.5f && depth < 2.5f) c = Line.Lerp(Rim, depth - 1.5f);
                if (depth > 5.5f && depth < 6.5f) c = Rim.Lerp(Enamel, depth - 5.5f);
                image.SetPixel(x, y, c with { A = Math.Clamp(depth + .5f, 0, 1) });
            }
        return ImageTexture.CreateFromImage(image);
    }
}

/// <summary>
/// The reinforced furnace's heat as a round stovetop dial in degrees Celsius: a dark track, a brick band where steel forms, and a needle.
/// </summary>
public partial class HeatDial : Control {
    public Func<float> Level { get; set; } = () => 0;
    private const float Sweep = Mathf.Pi * 1.5f, Start = Mathf.Pi * .75f;

    public override void _Draw() {
        var centre = Size / 2;
        float radius = Math.Min(Size.X, Size.Y) / 2 - 3;
        DrawCircle(centre, radius, Style.CupFill);
        DrawArc(centre, radius, 0, Mathf.Tau, 48, Style.Line, 3, true);
        float track = radius - 8;
        DrawArc(centre, track, Start, Start + Sweep, 40, new Color(Style.Line, .3f), 2, true);
        DrawArc(centre, track, Start + Sweep * World.SteelHeat / 100, Start + Sweep, 12, Style.Brick, 8, true);
        float heat = Mathf.Clamp(Level(), 0, 100);
        DrawLine(centre, centre + Vector2.FromAngle(Start + Sweep * heat / 100) * (track - 4), Style.Enamel, 4, true);
        DrawCircle(centre, 6, Style.Enamel);
    }
}

/// <summary>Time of day as the sun's way along the arc from sunrise to sunset, then the moon's along the same arc until dawn.</summary>
public partial class DayDial : Control {
    /// <summary>How far the sun or moon has crossed the sky, from 0 to 1.</summary>
    public Func<float> Arc { get; set; } = () => 0;
    public Func<bool> Night { get; set; } = () => false;
    private static readonly Color NightArc = new("8fa6c4"), Moon = new("dfe6f2");

    public override void _Draw() {
        var centre = new Vector2(Size.X / 2, Size.Y - 6);
        float radius = Math.Min(Size.X / 2 - 12, Size.Y - 14), phase = Mathf.Clamp(Arc(), 0, 1);
        DrawArc(centre, radius, Mathf.Pi, Mathf.Tau, 32, Style.Line, 8, true);
        var tint = Night() ? NightArc : Style.Mustard;
        DrawArc(centre, radius, Mathf.Pi, Mathf.Pi + Math.Max(.02f, phase) * Mathf.Pi, 32, tint, 8, true);
        var at = centre + Vector2.FromAngle(Mathf.Pi + phase * Mathf.Pi) * radius;
        DrawCircle(at, 9, Style.Rim);
        DrawCircle(at, 7, Night() ? Moon : Style.Mustard);
    }
}

/// <summary>The road from the trading post to the village, with each cart drawn where it is on its trip.</summary>
public partial class CartRoad : Control {
    public Texture2D? Cart { get; set; }
    /// <summary>How far each cart on this road has gone, out and back, from 0 to 1.</summary>
    public readonly System.Collections.Generic.List<float> Trips = new();

    public override void _Draw() {
        float y = Size.Y / 2;
        for (float x = 0; x < Size.X; x += 10) DrawLine(new Vector2(x, y), new Vector2(Math.Min(x + 4, Size.X), y), new Color(Style.Rim, .5f), 3);
        if (Cart == null) return;
        foreach (float trip in Trips) {
            // A cart drives out over the first half of its trip and back over the second.
            float along = trip < .5f ? trip * 2 : 2 - trip * 2;
            DrawTextureRect(Cart, new Rect2((Size.X - 30) * along, y - 15, 30, 30), false, Style.Mustard);
        }
    }
}
