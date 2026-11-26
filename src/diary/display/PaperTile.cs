using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A cell of the Diary: a scrap of paper, a little crooked and torn along its left edge, with a loose page lying straight on it and,
/// written faintly over the page's middle, the day's place in the diary, or a question mark while the day is not found.
/// One not read yet is edged in brick.
/// </summary>
public partial class PaperTile : Button {
    public enum Look { Missing, Read, Unread }
    private static readonly Texture2D PagePicture = Icons.Of(PageShape.Icon)!;
    private static readonly Color Shadow = new(0, 0, 0, .28f);
    /// <summary>How far in from the cell's edges the sheet lies, leaving room for its shadow and edge.</summary>
    private const float Inset = 5;
    /// <summary>Pixels between the points of the torn edge, and how far each strays in or out.</summary>
    private const float TearStep = 7, TearDepth = 3;
    /// <summary>How far each corner of the scrap may stray from a true rectangle.</summary>
    private const float Crooked = 5;
    private readonly float[] tear;
    private readonly Vector2[] corners = new Vector2[4];
    // The faint number, built once the cell is in the tree.
    private Label? counter;
    private readonly string place;
    // The sheet's outline, and the same closed back on its first point for drawing its edge.
    private Vector2[] sheet = [], edge = [];
    private Look look = Look.Missing;

    /// <param name="place">The cell's place in the diary, counted from one.</param>
    public PaperTile(int place, Action open) {
        this.place = place.ToString();
        CustomMinimumSize = new Vector2(112, 140);
        Style.Skin(this, Style.None, Style.None);
        Pressed += open;
        // Each scrap is crooked and torn its own way.
        var random = new Random(place);
        float Stray(float most) => (float)(random.NextDouble() * 2 - 1) * most;
        tear = new float[32];
        for (int i = 0; i < tear.Length; i++) tear[i] = Stray(TearDepth);
        for (int i = 0; i < corners.Length; i++) corners[i] = new Vector2(Stray(Crooked), Stray(Crooked));
    }

    public override void _Ready() {
        var picture = Style.Icon(PagePicture, 0);
        picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        picture.OffsetLeft = picture.OffsetTop = 9; picture.OffsetRight = picture.OffsetBottom = -9;
        AddChild(picture);
        counter = Style.Text(place, 34, new Color(Style.DiaryInk, .45f));
        counter.Name = "counter";
        counter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        counter.HorizontalAlignment = HorizontalAlignment.Center; counter.VerticalAlignment = VerticalAlignment.Center;
        // A rim of paper keeps the number apart from the writing on the page under it.
        counter.AddThemeColorOverride("font_outline_color", Style.DiaryPaper);
        counter.AddThemeConstantOverride("outline_size", 10);
        AddChild(counter);
        Resized += Shape;
        ShowAs(look);
    }

    /// <summary>Draws the cell as a day found and read, found and not read yet, or not found.</summary>
    public void ShowAs(Look shown) {
        look = shown;
        bool found = shown != Look.Missing;
        Disabled = !found;
        MouseDefaultCursorShape = found ? CursorShape.PointingHand : CursorShape.Arrow;
        TooltipText = shown switch { Look.Unread => "Not read yet · click to read", Look.Read => "Click to read again", _ => "" };
        if (counter != null) counter.Text = found ? place : "?";
        QueueRedraw();
    }

    public override void _Draw() {
        // The outline comes with the cell's first size.
        if (sheet.Length == 0) return;
        DrawSetTransform(new Vector2(2, 3));
        DrawColoredPolygon(sheet, Shadow);
        DrawSetTransform(Vector2.Zero);
        DrawColoredPolygon(sheet, Style.DiaryPaper);
        var (color, width) = !Disabled && IsHovered() ? (Style.Mustard, 3f) : look == Look.Unread ? (Style.Brick, 4f) : (Style.Line, 2f);
        DrawPolyline(edge, color, width, true);
    }

    /// <summary>The scrap's outline for the cell's size: its crooked corners joined by straight top, right and bottom edges and a torn left one.</summary>
    private void Shape() {
        float left = Inset + Crooked, top = Inset + Crooked, right = Size.X - Inset - Crooked, bottom = Size.Y - Inset - Crooked;
        int points = Math.Min(tear.Length, (int)((bottom - top) / TearStep));
        sheet = new Vector2[points + 4];
        sheet[0] = new Vector2(left, top) + corners[0]; sheet[1] = new Vector2(right, top) + corners[1];
        sheet[2] = new Vector2(right, bottom) + corners[2]; sheet[3] = new Vector2(left, bottom) + corners[3];
        // The torn edge runs from the bottom left corner up to the top left one.
        for (int i = 1; i <= points; i++) sheet[3 + i] = sheet[3].Lerp(sheet[0], i / (points + 1f)) + new Vector2(TearDepth + tear[i - 1], 0);
        edge = [.. sheet, sheet[0]];
        QueueRedraw();
    }
}
