using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The pause menu's list of discovered quests and the window that shows a quest's details.</summary>
public partial class QuestLog : VBoxContainer {
    private readonly UiKit kit;
    private readonly RecipeMap map;
    private readonly Action leaveMenu;
    private readonly World world;
    private readonly VBoxContainer active = new(), completed = new();
    private readonly Label explore;
    private readonly Button disclosure;
    private readonly QuestStatus[] shown = new QuestStatus[Quests.Entries.Length];
    private readonly Label detailTitle;
    private readonly HBoxContainer detailItems;
    private readonly VBoxContainer detailBody;
    private readonly LiveUpdates progress = new();

    public Control Details { get; }

    public QuestLog(UiKit kit, RecipeMap map, Action leaveMenu) {
        this.kit = kit; this.map = map; this.leaveMenu = leaveMenu; world = kit.World;
        AddThemeConstantOverride("separation", 10);
        AddChild(Style.WellLabel("Quest Log"));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 8); scroll.AddChild(list);
        active.AddThemeConstantOverride("separation", 5); list.AddChild(active);
        explore = Style.Text("Explore to unlock more tasks.", 14, Style.Tan, wrap: true); list.AddChild(explore);
        disclosure = Row("▸  Completed", ToggleCompleted); disclosure.Name = "quest_completed"; list.AddChild(disclosure);
        completed.Visible = false; completed.AddThemeConstantOverride("separation", 5); list.AddChild(completed);

        Details = new Control { Name = "quest_details", Visible = false };
        Details.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(Style.Line, .55f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Details.AddChild(shade);
        var center = new CenterContainer(); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Details.AddChild(center);
        var board = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        board.AddThemeStyleboxOverride("panel", Style.Board); center.AddChild(board);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12); board.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        detailTitle = Style.Heading("", 23); detailTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(detailTitle);
        header.AddChild(kit.HeaderButton("close", "Close · Esc", CloseDetails, Style.CloseRound, "quest_close"));
        var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); column.AddChild(well);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 12); well.AddChild(content);
        detailItems = new HBoxContainer(); detailItems.AddThemeConstantOverride("separation", 8); content.AddChild(detailItems);
        detailBody = new VBoxContainer(); detailBody.AddThemeConstantOverride("separation", 6); content.AddChild(detailBody);
    }

    /// <summary>Draws quest titles when their visibility or completion changes.</summary>
    public void Refresh() {
        if (Details.Visible) progress.Run();
        var quests = Quests.Entries;
        Span<QuestStatus> statuses = stackalloc QuestStatus[quests.Length];
        for (int i = 0; i < quests.Length; i++) statuses[i] = world.Quests.Status(quests[i].Id);
        if (statuses.SequenceEqual(shown)) return;
        statuses.CopyTo(shown);

        Clear(active); Clear(completed);
        bool hasHidden = false;
        for (int i = 0; i < quests.Length; i++) {
            var quest = quests[i];
            if (shown[i] == QuestStatus.Hidden) { hasHidden = true; continue; }
            var row = Row(quest.Title, () => ShowDetails(quest));
            (shown[i] == QuestStatus.Completed ? completed : active).AddChild(row);
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

    private void ShowDetails(Quest quest) {
        detailTitle.Text = quest.Title;
        Clear(detailItems);
        foreach (string item in quest.Items) {
            var icon = kit.Button("", () => map.OpenAt(item, leaveMenu));
            icon.Name = $"quest_item_{item}";
            icon.TooltipText = world.Data.Name(item);
            icon.CustomMinimumSize = new Vector2(46, 46);
            icon.Icon = Icons.Of(item);
            icon.ExpandIcon = true;
            icon.IconAlignment = HorizontalAlignment.Center;
            icon.AddThemeConstantOverride("icon_max_width", 34);
            Style.Skin(icon, Style.SlotNormal, Style.SlotHover, Style.SlotPressed);
            detailItems.AddChild(icon);
        }
        detailItems.Visible = quest.Items.Length > 0;
        SetDescription(quest);
        progress.Run();
        Details.Show();
    }

    private void SetDescription(Quest quest) {
        Clear(detailBody);
        progress.Clear();
        if (quest.Id == QuestId.Mines) {
            foreach (var (ore, task) in Quests.MineTasks) {
                var row = new HBoxContainer();
                detailBody.AddChild(row);
                var mark = Style.Text("", 16, Style.Tan);
                mark.Name = $"quest_mine_{ore.Id()}";
                mark.CustomMinimumSize = new Vector2(20, 0);
                row.AddChild(mark);
                row.AddChild(Style.Text(task, 16, Style.Ink));
                progress.Watch(() => world.Mines.Working(ore), open => {
                    mark.Text = open ? "✓" : "○";
                    mark.AddThemeColorOverride("font_color", open ? Style.Rise : Style.Tan);
                });
            }
            return;
        }
        detailBody.AddChild(Style.Text(world.Quests.Description(quest), 16, Style.Ink, wrap: true));
        if (quest.Id != QuestId.Profit) return;
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
