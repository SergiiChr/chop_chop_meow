using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The workshop look shared by every panel: dark stained plywood with a birch-ply top edge, recessed sockets for items,
/// safety orange for what is active or wanted, a stencil face for headings and chalk on the trading post's board.
/// Style boxes and fonts are made once, since creating a resource allocates.
/// </summary>
public static class Style {
    public static readonly Color Ply = new("2b2119");
    public static readonly Color Raised = new("3c2f24");
    public static readonly Color Hover = new("4f3d2d");
    public static readonly Color Socket = new("16100b");
    public static readonly Color Edge = new("c08d52");
    public static readonly Color Cream = new("f3e6c9");
    public static readonly Color Muted = new("b9a68a");
    public static readonly Color Orange = new("f28c28");
    public static readonly Color Gold = new("f2c14e");
    public static readonly Color Short = new("f29a8a");
    public static readonly Color Good = new("9cc46a");
    public static readonly Color Slate = new("2e3a34");
    public static readonly Color Chalk = new("eef0e6");

    public static readonly Font Body = GD.Load<Font>("res://assets/fonts/BarlowSemiCondensed-SemiBold.ttf");
    public static readonly Font Bold = GD.Load<Font>("res://assets/fonts/BarlowSemiCondensed-Bold.ttf");
    public static readonly Font Stencil = GD.Load<Font>("res://assets/fonts/SairaStencilOne-Regular.ttf");
    public static readonly Font ChalkHand = GD.Load<Font>("res://assets/fonts/Kalam-Bold.ttf");

    /// <summary>A plywood board: the ply's laminated edge shows as a light band along the top.</summary>
    public static readonly StyleBoxFlat Board = Make(Ply, 6, 16, top: 5, edge: Edge, shadow: 12);
    /// <summary>The HUD's smaller boards.</summary>
    public static readonly StyleBoxFlat Plate = Make(Ply, 6, 8, top: 4, edge: Edge, shadow: 8);
    /// <summary>A darker well inside a board, holding a station's process line or a group of slots.</summary>
    public static readonly StyleBoxFlat Well = Make(new Color(0, 0, 0, .22f), 8, 10);
    public static readonly StyleBoxFlat ButtonNormal = Make(Raised, 8, 9, bottom: 3, edge: new Color("1d150f"));
    public static readonly StyleBoxFlat ButtonHover = Make(Hover, 8, 9, bottom: 3, edge: new Color("1d150f"));
    public static readonly StyleBoxFlat ButtonPressed = Make(Orange, 8, 9, bottom: 3, edge: new Color("9a5212"));
    public static readonly StyleBoxFlat ButtonDisabled = Make(new Color("2f251c"), 8, 9);
    /// <summary>The one call to action on a page, such as sending the cart.</summary>
    public static readonly StyleBoxFlat Primary = Make(Orange, 8, 9, bottom: 4, edge: new Color("9a5212"));
    public static readonly StyleBoxFlat PrimaryHover = Make(new Color("ff9d3d"), 8, 9, bottom: 4, edge: new Color("9a5212"));
    /// <summary>A recessed item socket; its dark rim reads as depth against the plywood.</summary>
    public static readonly StyleBoxFlat SocketNormal = Make(Socket, 7, 3, all: 1, edge: new Color("44352a"));
    public static readonly StyleBoxFlat SocketHover = Make(new Color("221810"), 7, 3, all: 2, edge: Gold);
    public static readonly StyleBoxFlat SocketPressed = Make(new Color("2c2015"), 7, 3, all: 2, edge: Orange);
    /// <summary>A socket whose item the open station or market takes, or the item in hand.</summary>
    public static readonly StyleBoxFlat SocketWanted = Make(Socket, 7, 3, all: 2, edge: Orange, glow: Orange);
    /// <summary>The trading post's wage jar.</summary>
    public static readonly StyleBoxFlat SocketWage = Make(Socket, 7, 3, all: 2, edge: Gold);
    /// <summary>A station's output socket, rimmed so it reads as where the result lands.</summary>
    public static readonly StyleBoxFlat SocketOutput = Make(Socket, 7, 3, all: 2, edge: new Color("6a5440"));
    /// <summary>A recipe tile on the pegboard.</summary>
    public static readonly StyleBoxFlat Tile = Make(new Color("241a12"), 8, 6, bottom: 3, edge: new Color("120c08"));
    public static readonly StyleBoxFlat TileHover = Make(new Color("33261a"), 8, 6, all: 2, edge: Gold);
    public static readonly StyleBoxFlat TileChosen = Make(new Color("3c2a1a"), 8, 6, all: 3, edge: Orange);
    public static readonly StyleBoxFlat Pegboard = Make(new Color("3a2d22"), 8, 8);
    /// <summary>The tool tab beside a pegboard row, joined to it.</summary>
    public static readonly StyleBoxFlat Rail = Make(Raised, 8, 8);
    public static readonly StyleBoxFlat ChalkBoard = Make(Slate, 6, 16, all: 7, edge: new Color("6b4a2c"));
    public static readonly StyleBoxFlat ChalkButton = Make(new Color(1, 1, 1, .06f), 14, 4, all: 2, edge: new Color(.93f, .94f, .9f, .5f));
    public static readonly StyleBoxFlat ChalkButtonHover = Make(new Color(1, 1, 1, .16f), 14, 4, all: 2, edge: Chalk);
    public static readonly StyleBoxFlat Tooltip = Make(Socket, 8, 10, all: 2, edge: Gold, shadow: 8);
    public static readonly StyleBoxFlat BarBack = Make(Socket, 6, 0);
    public static readonly StyleBoxFlat BarFill = Make(Orange, 6, 0);
    public static readonly StyleBoxFlat RoundNormal = Make(Raised, 28, 6, bottom: 3, edge: new Color("1d150f"));
    public static readonly StyleBoxFlat RoundHover = Make(Hover, 28, 6, bottom: 3, edge: new Color("1d150f"));
    public static readonly StyleBoxFlat RoundPressed = Make(Orange, 28, 6, bottom: 3, edge: new Color("9a5212"));
    /// <summary>The worker chip's round initials badge.</summary>
    public static readonly StyleBoxFlat Badge = Make(Gold, 15, 0);
    public static readonly StyleBoxFlat Key = Make(Cream, 4, 3, bottom: 2, edge: new Color("9d8a6a"));

    private static StyleBoxFlat Make(Color bg, int radius, int margin, int top = 0, int bottom = 0, int all = 0, Color? edge = null, int shadow = 0, Color? glow = null) {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = edge ?? Colors.Transparent, ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin + top, ContentMarginBottom = margin };
        box.SetCornerRadiusAll(radius);
        if (all > 0) box.SetBorderWidthAll(all);
        if (top > 0) box.BorderWidthTop = top;
        if (bottom > 0) box.BorderWidthBottom = bottom;
        if (shadow > 0) { box.ShadowColor = new Color(0, 0, 0, .45f); box.ShadowSize = shadow; box.ShadowOffset = new Vector2(0, 4); }
        if (glow is Color g) { box.ShadowColor = g with { A = .5f }; box.ShadowSize = 6; }
        return box;
    }

    /// <summary>Sets a button's four states to the given boxes.</summary>
    public static void Skin(Button button, StyleBox normal, StyleBox hover, StyleBox pressed) {
        button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed); button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    public static Label Heading(string text, int size = 22) {
        var label = new Label { Text = text };
        label.AddThemeFontOverride("font", Stencil); label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Cream);
        return label;
    }

    /// <summary>A small cream keycap naming the key that does the same thing.</summary>
    public static PanelContainer Keycap(string key) {
        var cap = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(20, 0) };
        cap.AddThemeStyleboxOverride("panel", Key);
        var label = new Label { Text = key, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", Bold); label.AddThemeFontSizeOverride("font_size", 12); label.AddThemeColorOverride("font_color", Ply);
        cap.AddChild(label);
        return cap;
    }

    public static TextureRect Icon(Texture2D? texture, float size) => new() {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };
}

/// <summary>
/// The bellows furnace's heat as a dial like the gauge on the forge itself: a dark arc, a warm band from the steel line up, and a needle.
/// </summary>
public partial class HeatDial : Control {
    public Func<float> Level { get; set; } = () => 0;

    public override void _Draw() {
        var centre = new Vector2(Size.X / 2, Size.Y - 12);
        float radius = Math.Min(Size.X / 2 - 12, Size.Y - 24), heat = Mathf.Clamp(Level(), 0, 100);
        float Angle(float level) => Mathf.Pi + level / 100 * Mathf.Pi;
        DrawArc(centre, radius, Mathf.Pi, Mathf.Tau, 32, Style.Socket, 20, true);
        DrawArc(centre, radius, Mathf.Pi, Angle(World.SteelHeat), 24, new Color("6b3d26"), 12, true);
        DrawArc(centre, radius, Angle(World.SteelHeat), Mathf.Tau, 12, heat >= World.SteelHeat ? new Color("ff8a2a") : new Color("8a4a26"), 12, true);
        var tip = centre + Vector2.FromAngle(Angle(heat)) * (radius + 4);
        DrawLine(centre, tip, Style.Cream, 4, true);
        DrawCircle(centre, 7, Style.Cream);
        var font = Style.Stencil;
        string text = HeatText((int)heat);
        var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 24);
        DrawString(font, centre + new Vector2(-size.X / 2, -radius * .35f), text, HorizontalAlignment.Left, -1, 24, Style.Cream);
    }

    // Degrees as text, made once per whole degree so redraws reuse them.
    private static readonly string[] Degrees = new string[101];
    private static string HeatText(int heat) => Degrees[heat] ??= $"{heat}°";
}

/// <summary>Time of day as the sun's (or moon's) way along an arc.</summary>
public partial class DayDial : Control {
    public Func<float> Phase { get; set; } = () => 0;
    public Func<bool> Night { get; set; } = () => false;

    public override void _Draw() {
        var centre = new Vector2(Size.X / 2, Size.Y - 2);
        float radius = Math.Min(Size.X / 2 - 8, Size.Y - 8), phase = Mathf.Clamp(Phase(), 0, 1);
        DrawArc(centre, radius, Mathf.Pi, Mathf.Tau, 32, Style.Socket, 8, true);
        DrawArc(centre, radius, Mathf.Pi, Mathf.Pi + phase * Mathf.Pi, 32, Night() ? new Color("8fa6d9") : Style.Gold, 3, true);
        var at = centre + Vector2.FromAngle(Mathf.Pi + phase * Mathf.Pi) * radius;
        DrawCircle(at, 7, Night() ? new Color("dfe6f2") : new Color("f9d66b"));
        DrawArc(at, 7, 0, Mathf.Tau, 16, Style.Ply, 2, true);
    }
}

/// <summary>The road from the trading post to the village, with each cart drawn where it is on its trip.</summary>
public partial class CartRoad : Control {
    public Texture2D? Cart { get; set; }
    /// <summary>How far each cart on this road has gone, out and back, from 0 to 1.</summary>
    public readonly System.Collections.Generic.List<float> Trips = new();

    public override void _Draw() {
        float y = Size.Y / 2 + 4;
        for (float x = 0; x < Size.X; x += 10) DrawLine(new Vector2(x, y), new Vector2(Math.Min(x + 4, Size.X), y), Style.Muted, 3);
        if (Cart == null) return;
        foreach (float trip in Trips) {
            // A cart drives out over the first half of its trip and back over the second.
            float along = trip < .5f ? trip * 2 : 2 - trip * 2;
            var rect = new Rect2((Size.X - 30) * along, y - 24, 30, 30);
            DrawTextureRect(Cart, rect, false);
        }
    }
}
