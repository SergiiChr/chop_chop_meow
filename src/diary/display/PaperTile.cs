using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A cell of the Diary: a scrap of paper, a little crooked and torn along its left edge, with a loose page lying straight on it.
/// A found day's page is bright, with its place in the diary written in black over its middle, and one not read yet is edged in brick.
/// A day not found yet is greyed out, scrap and page alike, with a faint question mark.
/// </summary>
public partial class PaperTile : Button {
    public enum Look { Missing, Read, Unread }
    private static readonly Texture2D PagePicture = Icons.Of(PageShape.Icon)!;
    private static readonly Color Shadow = new(0, 0, 0, .28f);
    /// <summary>The page picture of a found day, brightened to clean paper from the dim light the icon is rendered in, and of a day not found, dulled.</summary>
    private static readonly Color BrightPage = new(1.6f, 1.6f, 1.6f), DullPage = new(.8f, .8f, .8f, .7f);
    /// <summary>The scrap under a day not found, and its question mark.</summary>
    private static readonly Color GreyPaper = Style.DiaryPaper.Lerp(new Color(.62f, .62f, .62f), .7f), Unknown = new(.25f, .25f, .25f, .45f);
    /// <summary>How far in from the cell's edges the sheet lies, leaving room for its shadow and edge.</summary>
    private const float Inset = 5;
    /// <summary>Pixels between the points of the torn edge, and how far each strays in or out.</summary>
    private const float TearStep = 7, TearDepth = 3;
    /// <summary>How far each corner of the scrap may stray from a true rectangle.</summary>
    private const float Crooked = 5;
    private readonly float[] tear;
    private readonly Vector2[] corners = new Vector2[4];
    // The page and its number, built once the cell is in the tree.
    private TextureRect? picture;
    private Label? counter;
    private readonly string place;
    // The sheet's outline, and the same closed back on its first point for drawing its edge, for the cell's size when they were made.
    private Vector2[] sheet = [], edge = [];
    private Vector2 shaped;
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
        picture = Style.Icon(PagePicture, 0);
        picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        picture.OffsetLeft = picture.OffsetTop = 9; picture.OffsetRight = picture.OffsetBottom = -9;
        AddChild(picture);
        counter = Style.Text(place, 34, Colors.Black);
        counter.Name = "counter";
        counter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        counter.HorizontalAlignment = HorizontalAlignment.Center; counter.VerticalAlignment = VerticalAlignment.Center;
        // A rim of paper keeps the number apart from the writing on the page under it.
        counter.AddThemeConstantOverride("outline_size", 10);
        AddChild(counter);
        ShowAs(look);
    }

    /// <summary>Draws the cell as a day found and read, found and not read yet, or not found.</summary>
    public void ShowAs(Look shown) {
        look = shown;
        bool found = shown != Look.Missing;
        Disabled = !found;
        MouseDefaultCursorShape = found ? CursorShape.PointingHand : CursorShape.Arrow;
        TooltipText = shown switch { Look.Unread => "Not read yet · click to read", Look.Read => "Click to read again", _ => "" };
        if (picture != null) picture.Modulate = found ? BrightPage : DullPage;
        if (counter != null) {
            counter.Text = found ? place : "?";
            counter.AddThemeColorOverride("font_color", found ? Colors.Black : Unknown);
            counter.AddThemeColorOverride("font_outline_color", found ? Style.DiaryPaper : GreyPaper);
        }
        QueueRedraw();
    }

    public override void _Draw() {
        // A cell may get its size before it is first drawn, or never be resized after, so its outline follows the size it is drawn at.
        if (Size != shaped) Shape();
        DrawSetTransform(new Vector2(2, 3));
        DrawColoredPolygon(sheet, Shadow);
        DrawSetTransform(Vector2.Zero);
        DrawColoredPolygon(sheet, look == Look.Missing ? GreyPaper : Style.DiaryPaper);
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
        shaped = Size;
    }
}
