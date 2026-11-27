using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The pause menu's list of discovered goals and the window that shows a goal's details.</summary>
public partial class GoalLog : VBoxContainer {
    private readonly UiKit kit;
    private readonly RecipeMap map;
    private readonly Action leaveMenu, openDiary;
    private readonly World world;
    private readonly VBoxContainer active = new(), completed = new();
    private readonly Label explore;
    private readonly Button disclosure;
    private readonly GoalStatus[] shown = new GoalStatus[Goals.Entries.Length];
    private readonly Label detailTitle;
    private readonly HBoxContainer detailItems;
    private readonly VBoxContainer detailBody;
    private readonly LiveUpdates progress = new();

    public Control Details { get; }

    public GoalLog(UiKit kit, RecipeMap map, Action leaveMenu, Action openDiary) {
        this.kit = kit; this.map = map; this.leaveMenu = leaveMenu; this.openDiary = openDiary; world = kit.World;
        AddThemeConstantOverride("separation", 10);
        AddChild(Style.WellLabel("Goal Log"));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 8); scroll.AddChild(list);
        active.AddThemeConstantOverride("separation", 5); list.AddChild(active);
        explore = Style.Text("Explore to unlock more goals.", 14, Style.Tan, wrap: true); list.AddChild(explore);
        disclosure = Row("▸  Completed", ToggleCompleted); disclosure.Name = "goal_completed"; list.AddChild(disclosure);
        completed.Visible = false; completed.AddThemeConstantOverride("separation", 5); list.AddChild(completed);

        Details = new Control { Name = "goal_details", Visible = false };
        detailTitle = Style.Heading("", 23);
        var (_, column) = kit.Window(Details, 480, Style.Board, detailTitle, CloseDetails, "goal_close", 12);
        var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); column.AddChild(well);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 12); well.AddChild(content);
        detailItems = new HBoxContainer(); detailItems.AddThemeConstantOverride("separation", 8); content.AddChild(detailItems);
        detailBody = new VBoxContainer(); detailBody.AddThemeConstantOverride("separation", 6); content.AddChild(detailBody);
    }

    /// <summary>Draws goal titles when their visibility or completion changes.</summary>
    public void Refresh() {
        if (Details.Visible) progress.Run();
        var goals = Goals.Entries;
        Span<GoalStatus> statuses = stackalloc GoalStatus[goals.Length];
        for (int i = 0; i < goals.Length; i++) statuses[i] = world.Goals.Status(goals[i].Id);
        if (statuses.SequenceEqual(shown)) return;
        statuses.CopyTo(shown);

        Clear(active); Clear(completed);
        bool hasHidden = false;
        for (int i = 0; i < goals.Length; i++) {
            var goal = goals[i];
            if (shown[i] == GoalStatus.Hidden) { hasHidden = true; continue; }
            var row = Row(goal.Title, () => ShowDetails(goal));
            (shown[i] == GoalStatus.Completed ? completed : active).AddChild(row);
        }
        explore.Visible = hasHidden;
        disclosure.Visible = completed.GetChildCount() > 0;
    }

    private Button Row(string title, Action action) {
        var row = kit.Button(title, action);
        row.Alignment = HorizontalAlignment.Left;
        Style.Skin(row, Style.OutlinePill, Style.OutlinePillHover);
        Style.InkText(row);
        return row;
    }

    private void ToggleCompleted() {
        completed.Visible = !completed.Visible;
        disclosure.Text = completed.Visible ? "▾  Completed" : "▸  Completed";
    }

    private void ShowDetails(Goal goal) {
        detailTitle.Text = goal.Title;
        Clear(detailItems);
        foreach (string item in goal.Items) {
            var icon = Badge(Icons.Of(item), world.Data.Name(item), () => map.OpenAt(item, leaveMenu));
            icon.Name = $"goal_item_{item}";
            detailItems.AddChild(icon);
        }
        // The diary's goal shows the diary, which opens as its badge in the bottom bar does.
        if (goal.Id == GoalId.Diary) {
            var diary = Badge(DiaryPanel.Glyph, "Diary", () => { CloseDetails(); leaveMenu(); openDiary(); });
            diary.Name = "goal_diary";
            diary.AddThemeColorOverride("icon_normal_color", Style.Ink); diary.AddThemeColorOverride("icon_hover_color", Style.Ink); diary.AddThemeColorOverride("icon_pressed_color", Style.Ink);
            diary.AddThemeConstantOverride("icon_max_width", 26);
            detailItems.AddChild(diary);
        }
        detailItems.Visible = detailItems.GetChildCount() > 0;
        SetDescription(goal);
        progress.Run();
        Details.Show();
    }

    /// <summary>A slot-like button with a picture, which names it when hovered.</summary>
    private Button Badge(Texture2D? picture, string name, Action action) {
        var icon = kit.Button("", action);
        icon.TooltipText = name;
        icon.CustomMinimumSize = new Vector2(46, 46);
        icon.Icon = picture;
        icon.ExpandIcon = true;
        icon.IconAlignment = HorizontalAlignment.Center;
        icon.AddThemeConstantOverride("icon_max_width", 34);
        Style.Skin(icon, Style.SlotNormal, Style.SlotHover, Style.SlotPressed);
        return icon;
    }

    private void SetDescription(Goal goal) {
        Clear(detailBody);
        progress.Clear();
        if (goal.Id == GoalId.Mines) {
            foreach (var ore in Cliff.Ores) {
                string oreName = ore.Id();
                var row = new HBoxContainer();
                detailBody.AddChild(row);
                var mark = Style.Text("", 16, Style.Tan);
                mark.Name = $"goal_mine_{oreName}";
                mark.CustomMinimumSize = new Vector2(20, 0);
                row.AddChild(mark);
                string mine = world.A($"{oreName} mine");
                row.AddChild(Style.Text($"Build {mine}.", 16, Style.Ink));
                progress.Watch(() => world.Mines.Working(ore), open => {
                    mark.Text = open ? "✓" : "○";
                    mark.AddThemeColorOverride("font_color", open ? Style.Rise : Style.Tan);
                });
            }
            return;
        }
        detailBody.AddChild(Style.Text(world.Goals.Description(goal), 16, Style.Ink, wrap: true));
        if (goal.Id == GoalId.Diary) {
            var found = Style.Text("", 14, Style.Tan, wrap: true);
            found.Name = "goal_diary_found";
            detailBody.AddChild(found);
            progress.Watch(() => Diary.FoundDays(world.Me), days => found.Text = $"{days} of {DiaryDay.All.Length} pages found");
            return;
        }
        if (goal.Id != GoalId.Profit) return;
        var figures = Style.Text("", 14, Style.Tan, wrap: true);
        detailBody.AddChild(figures);
        progress.Watch(() => (world.Profits.Count, world.DayRevenue, world.ProfitGoalMet), current => {
            int days = world.Data.Goals.ProfitDays;
            int streak = current.ProfitGoalMet ? days : world.ProfitStreak;
            string yesterday = current.Count > 0 ? $"Yesterday's profit {world.Profits[^1]}" : "No day finished yet";
            figures.Text = $"{streak} of {days} days in a row\n{yesterday} · Today's sales {current.DayRevenue}";
        });
    }

    public void CloseDetails() => Details.Hide();

    private static void Clear(Container container) {
        while (container.GetChildCount() > 0) {
            var child = container.GetChild(0);
            container.RemoveChild(child); child.QueueFree();
        }
    }
}
