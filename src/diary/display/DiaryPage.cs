using Godot;

namespace ChopChopMeow;

/// <summary>
/// The diary's page in the side panel: a tile for each of its days, in order, that opens the day large to read again.
/// A day the player has not found leaves an empty space, and one they have not opened yet stands out.
/// </summary>
public partial class DiaryPage(PageTools tools) : PagePanel(tools) {
    private static readonly StyleBoxFlat Tile = Style.Flat(new Color("f3ead2"), 10, 6, Style.Line, 2), TileHover = Style.Flat(new Color("f3ead2"), 10, 6, Style.Mustard, 3);
    private static readonly StyleBoxFlat Unread = Style.Flat(new Color("f8efd6"), 10, 6, Style.Brick, 4), UnreadHover = Style.Flat(new Color("f8efd6"), 10, 6, Style.Mustard, 4);
    private static readonly StyleBoxFlat Missing = Style.Flat(Colors.Transparent, 10, 6, new Color(Style.Beige, .3f), 2);
    private enum DayState { Missing, Read, Unread }
    public static readonly Texture2D Glyph = GD.Load<Texture2D>("res://assets/ui/line_diary.svg");

    protected override void Build() {
        headingIcon.Texture = Glyph; headingIcon.SelfModulate = Style.Rim; headingIcon.Visible = true;
        var grid = NewGrid(3);
        AddChild(grid);
        var tiles = new Button[DiaryDay.All.Length];
        for (int i = 0; i < tiles.Length; i++) {
            int day = DiaryDay.All[i].Number;
            var tile = kit.Button($"Day {day}", () => panel.Sheet.Open(day));
            tile.Name = $"diary_day_{day}";
            tile.CustomMinimumSize = new Vector2(128, 84);
            tile.AddThemeFontOverride("font", Style.HandBold); tile.AddThemeFontSizeOverride("font_size", 30);
            Style.InkText(tile, Style.DiaryInk);
            grid.AddChild(tile);
            tiles[i] = tile;
        }
        // Each day's state in two bits, so the tiles draw again only when one is found or read.
        live.Watch(() => {
            var me = world.Me;
            long states = 0;
            for (int i = 0; i < tiles.Length; i++) {
                int day = DiaryDay.All[i].Number;
                var state = !Diary.Has(me, day) ? DayState.Missing : me.DiaryRead.Contains(day) ? DayState.Read : DayState.Unread;
                states |= (long)state << (2 * i);
            }
            return states;
        }, states => {
            for (int i = 0; i < tiles.Length; i++) {
                var state = (DayState)(states >> (2 * i) & 3);
                var tile = tiles[i];
                bool found = state != DayState.Missing, unread = state == DayState.Unread;
                tile.Disabled = !found;
                tile.Text = found ? $"Day {DiaryDay.All[i].Number}" : "";
                tile.TooltipText = found ? unread ? "Not read yet · click to read" : "Click to read again" : "";
                tile.MouseDefaultCursorShape = found ? CursorShape.PointingHand : CursorShape.Arrow;
                if (!found) Style.Skin(tile, Missing, Missing, Missing);
                else if (unread) Style.Skin(tile, Unread, UnreadHover, UnreadHover);
                else Style.Skin(tile, Tile, TileHover, TileHover);
            }
        });
    }
}
