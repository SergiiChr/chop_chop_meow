using Godot;

namespace ChopChopMeow;

/// <summary>
/// The Diary in the side panel: a cell for each of the diary's days, in order, that opens the day large to read again.
/// A day the player has not found shows a question mark on its page, and one they have not opened yet stands out.
/// </summary>
public partial class DiaryPanel(PageTools tools) : PagePanel(tools) {
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
        for (int i = 0; i < DiaryDay.All.Length; i++) {
            int day = DiaryDay.All[i].Number;
            var cell = new DiaryCell(i + 1, () => panel.ReadingSheet.Open(day)) { Name = $"diary_day_{day}" };
            grid.AddChild(cell);
            // A cell draws again only when its day is found or read.
            live.Watch(() => !Diary.Has(world.Me, day) ? DiaryCell.Look.Missing : Diary.Unread(world.Me, day) ? DiaryCell.Look.Unread : DiaryCell.Look.Read, cell.ShowAs);
        }
    }
}
