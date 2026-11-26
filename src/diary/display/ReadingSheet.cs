using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One diary day opened large in the middle of the screen: a sheet of paper with the day written on it by hand,
/// falling apart where it was written in the dark and scribbles where it cannot be read. The close button, Esc or a click beside the sheet closes it.
/// Opening a day marks it read.
/// </summary>
public partial class ReadingSheet : Control {
    private static readonly StyleBoxFlat Paper = Style.Flat(new Color("f3ead2"), 6, 34, new Color("cdbf9c"), 2);
    // Loaded with the game and kept, so the text that draws them by path finds them in the cache rather than loading them as a day opens.
    private static readonly Texture2D ScribbleWord = GD.Load<Texture2D>("res://assets/ui/scribble_word.svg"), ScribbleLine = GD.Load<Texture2D>("res://assets/ui/scribble_line.svg");
    private readonly UiKit kit;
    private readonly Label title;
    private readonly RichTextLabel text;
    /// <summary>The scale the scribbles' drawings are imported at, so they stay sharp on a large screen.</summary>
    private const int ScribbleScale = 2;

    /// <summary>The number of the day open, or zero while closed.</summary>
    public int Day { get; private set; }
    /// <summary>The words of the open day as the player reads them, without the drawings.</summary>
    public string Reading => text.GetParsedText();

    public ReadingSheet(UiKit kit) {
        this.kit = kit;
        Name = "diary_sheet"; Visible = false;
        title = Style.Text("", 44, Style.DiaryInk, Style.HandBold);
        var (shade, column) = kit.Window(this, 620, Paper, title, Close, "diary_sheet_close", 14);
        // A turn of the mouse wheel is no click beside the sheet.
        shade.GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: not (MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight) }) Close(); };
        text = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SelectionEnabled = false, MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeFontOverride("normal_font", Style.Hand); text.AddThemeFontSizeOverride("normal_font_size", 30);
        text.AddThemeColorOverride("default_color", Style.DiaryInk);
        text.AddThemeConstantOverride("paragraph_separation", 14);
        text.CustomEffects = [new FallingApart()];
        column.AddChild(text);
        kit.World.Replaced += () => { Visible = false; Day = 0; };
    }

    /// <summary>Opens a day large, turning a page unless <paramref name="quiet"/>, and marks it read.</summary>
    public void Open(int day, bool quiet = false) {
        if (!quiet) kit.Sounds.Play("page_turn");
        Day = day;
        title.Text = $"Day {day}";
        text.Text = Written(DiaryDay.Of(day));
        Visible = true;
        kit.World.Diary.Read(day);
    }

    public void Close() {
        if (!Visible) return;
        kit.Sounds.Play("page_turn");
        Visible = false; Day = 0;
    }

    /// <summary>A day's paragraphs as the sheet's text, falling apart where they say so, with a scribble drawn in for each word or line nobody can read.</summary>
    private static string Written(DiaryDay day) {
        var written = new StringBuilder();
        foreach (var paragraph in day.Lines) {
            if (written.Length > 0) written.Append('\n');
            if (paragraph == DiaryDay.Scribbles) { written.Append(Drawn(ScribbleLine)); continue; }
            var line = paragraph.Replace(DiaryDay.Illegible, Drawn(ScribbleWord));
            written.Append(line.Contains(DiaryDay.FallsApart) ? line.Replace(DiaryDay.FallsApart, "[falling_apart]") + "[/falling_apart]" : line);
        }
        return written.ToString();
    }

    /// <summary>A scribble's drawing in the text, at the size it was drawn.</summary>
    private static string Drawn(Texture2D scribble) => $"[img height={scribble.GetHeight() / ScribbleScale}]{scribble.ResourcePath}[/img]";
}
