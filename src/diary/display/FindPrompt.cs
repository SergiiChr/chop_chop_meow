using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The pop-up a diary find waits behind: its line and a Take button, the only way to close it.
/// Taking it turns a page and hands the find to <paramref name="taken"/>, which opens it in the diary.
/// </summary>
public partial class FindPrompt : PanelContainer {
    private readonly World world;
    private readonly Label line;
    // The find taken here, until the world says it is in the diary; a guest's world says so only with a later update.
    private DiaryFind answered;
    private DiaryFind shown;

    public FindPrompt(UiKit kit, Action<DiaryFind> taken) {
        world = kit.World;
        Name = "diary_find"; Visible = false;
        Style.Centered(this, 460, 160);
        var body = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; body.AddThemeConstantOverride("separation", 16); AddChild(body);
        line = Style.Text("", 19, Style.Rim, wrap: true); line.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(line);
        var take = kit.BigButton("Take", Style.Sage, () => {
            var find = shown;
            kit.Sounds.Play("page_turn");
            world.Diary.Take();
            answered = find; Visible = false;
            taken(find);
        });
        take.Name = "diary_take";
        body.AddChild(take);
        world.Replaced += () => answered = DiaryFind.None;
    }

    /// <summary>Shows the pop-up while the player has a find waiting to be taken.</summary>
    public void Refresh() {
        var finding = world.Me.DiaryFinding;
        if (finding != answered) answered = DiaryFind.None;
        bool show = finding != DiaryFind.None && finding != answered;
        if (show && finding != shown) { shown = finding; line.Text = DiaryFindDef.Of(finding).Line; }
        Visible = show;
    }
}
