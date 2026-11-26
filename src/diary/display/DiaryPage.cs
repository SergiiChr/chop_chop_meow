using Godot;

namespace ChopChopMeow;

/// <summary>
/// The diary's page in the side panel: a sheet of paper for each of its days, in order, that opens the day large to read again.
/// A day the player has not found leaves a space with a question mark, and one they have not opened yet stands out.
/// </summary>
public partial class DiaryPage(PageTools tools) : PagePanel(tools) {
    public static readonly Texture2D Glyph = GD.Load<Texture2D>("res://assets/ui/line_diary.svg");

    protected override void Build() {
        headingIcon.Texture = Glyph; headingIcon.SelfModulate = Style.Rim; headingIcon.Visible = true;
        // Four pages a row, centred, with the room the panel keeps right of every page for count pills kept on the left too,
        // so the panel's edge is as far from the pages on the left as on the right.
        var room = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        room.AddThemeConstantOverride("margin_left", SidePanel.PillRoom);
        AddChild(room);
        var grid = NewGrid(4);
        room.AddChild(grid);
        var tiles = new PaperTile[DiaryDay.All.Length];
        for (int i = 0; i < tiles.Length; i++) {
            int day = DiaryDay.All[i].Number;
            var tile = new PaperTile(i + 1, () => panel.Sheet.Open(day)) { Name = $"diary_day_{day}" };
            grid.AddChild(tile);
            tiles[i] = tile;
        }
        // Each day's look in two bits, so the tiles draw again only when one is found or read.
        live.Watch(() => {
            var me = world.Me;
            long looks = 0;
            for (int i = 0; i < tiles.Length; i++) {
                int day = DiaryDay.All[i].Number;
                var look = !Diary.Has(me, day) ? PaperTile.Look.Missing : Diary.Unread(me, day) ? PaperTile.Look.Unread : PaperTile.Look.Read;
                looks |= (long)look << (2 * i);
            }
            return looks;
        }, looks => {
            for (int i = 0; i < tiles.Length; i++) tiles[i].ShowAs((PaperTile.Look)(looks >> (2 * i) & 3));
        });
    }
}
