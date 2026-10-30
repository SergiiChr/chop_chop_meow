using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The Esc menu: pauses the world and lists the starter goal chain with the current goal's hint, then the end goals.</summary>
public partial class Game {
    private PanelContainer menu = null!;
    private VBoxContainer goals = null!;
    private Button hostButton = null!, joinButton = null!, saveButton = null!, loadButton = null!, newGameButton = null!;
    private Label netLine = null!;

    /// <summary>
    /// The pause board: the goal checklist in a well on the left, with the current goal's hint under it and the end goals below,
    /// and the game's buttons stacked on the right.
    /// </summary>
    private void BuildMenu() {
        menu = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(820, 0) };
        menu.AddThemeStyleboxOverride("panel", Style.Board);
        menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        menu.GrowHorizontal = Control.GrowDirection.Both; menu.GrowVertical = Control.GrowDirection.Both;
        ui.AddChild(menu);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); menu.AddChild(column);
        var title = Style.Heading("Paused", 28); column.AddChild(title);
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 20); column.AddChild(body);
        var well = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; well.AddThemeStyleboxOverride("panel", Style.Well); body.AddChild(well);
        goals = new VBoxContainer(); goals.AddThemeConstantOverride("separation", 6); well.AddChild(goals);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) }; side.AddThemeConstantOverride("separation", 12); body.AddChild(side);
        var resume = kit.BigButton("Resume · Esc", Style.Brick, CloseMenu); resume.Name = "resume"; side.AddChild(resume);
        foreach (var (text, action) in new (string, Action)[] {
            ("Save", () => ShowSlots(true, BackToPause)), ("Load", () => ShowSlots(false, BackToPause)), ("Options", () => ShowOptions(BackToPause)),
            ("New game", () => ConfirmLeave("NEW GAME", "Start a new game", () => { HideScreen(); Restart(); })), ("Quit", () => ConfirmLeave("QUIT", "Quit to desktop", () => Quit())),
        }) {
            var button = MenuButton(text, Style.Enamel, action); button.Name = text.ToLowerInvariant().Replace(' ', '_'); side.AddChild(button);
            if (text == "Save") saveButton = button;
            if (text == "Load") loadButton = button;
            if (text == "New game") newGameButton = button;
        }
        side.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        hostButton = MenuButton("Host", Style.Sage, HostGame); hostButton.Name = "host"; side.AddChild(hostButton);
        joinButton = MenuButton("Join", Style.Sage, () => { if (world.Remote) { net.Leave(); DropWorld(); ShowStart(); } else ShowJoin(BackToPause, true); }); joinButton.Name = "join_game"; side.AddChild(joinButton);
        netLine = Style.Text("", 13, Style.Beige); netLine.HorizontalAlignment = HorizontalAlignment.Center; netLine.AutowrapMode = TextServer.AutowrapMode.WordSmart; side.AddChild(netLine);
    }

    private Button MenuButton(string text, Color color, Action action) {
        var button = kit.BigButton(text, color, action); button.CustomMinimumSize = new Vector2(0, 46); button.AddThemeFontSizeOverride("font_size", 16);
        return button;
    }

    /// <summary>A checklist row: a round mark, done in sage, current in mustard, still to do as an empty ring, then its icon and title.</summary>
    private HBoxContainer GoalRow(string title, bool done, bool current, Texture2D? icon = null) {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
        row.AddChild(GoalMark(done, current));
        if (icon != null) row.AddChild(Style.Icon(icon, 26));
        row.AddChild(Style.Text(title, 15, done ? Style.Tan : Style.Ink, current ? Style.Head : null));
        return row;
    }

    private static Label GoalMark(bool done, bool current) {
        var mark = Style.Pill(done ? "✓" : "", done ? Style.Round(Style.Sage, 12).Item1 : current ? Style.Round(Style.Mustard, 12).Item1 : Style.GoalTodo, Style.Rim, 13);
        mark.CustomMinimumSize = new Vector2(24, 24); mark.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; mark.VerticalAlignment = VerticalAlignment.Center;
        return mark;
    }

    private void OpenMenu() {
        foreach (var child in goals.GetChildren()) child.QueueFree();
        goals.AddChild(Style.WellLabel("Getting started"));
        // Goals already met shrink to a row of ticks, named on hover; the current goal sits in a framed box with its hint.
        var met = new HFlowContainer(); met.AddThemeConstantOverride("h_separation", 4); met.AddThemeConstantOverride("v_separation", 4); goals.AddChild(met);
        for (int i = 0; i < World.GoalCount; i++) {
            var (title, _) = World.ObjectiveFor(i);
            if (i < world.S.Goal) { var tick = GoalMark(true, false); tick.TooltipText = title; tick.MouseFilter = Control.MouseFilterEnum.Pass; met.AddChild(tick); continue; }
            if (i == world.S.Goal) {
                var current = new PanelContainer(); current.AddThemeStyleboxOverride("panel", Style.Dropdown); goals.AddChild(current);
                var inner = new VBoxContainer(); inner.AddThemeConstantOverride("separation", 4); current.AddChild(inner);
                inner.AddChild(GoalRow(title, false, true));
                var hint = Style.Text(world.Objective.Hint, 13, Style.Tan); hint.AutowrapMode = TextServer.AutowrapMode.WordSmart; inner.AddChild(hint);
                continue;
            }
            goals.AddChild(GoalRow(title, false, false));
        }
        met.Visible = met.GetChildCount() > 0;
        if (world.GoalComplete) { var hint = Style.Text(world.Objective.Hint, 13, Style.Tan); hint.AutowrapMode = TextServer.AutowrapMode.WordSmart; goals.AddChild(hint); }
        goals.AddChild(new HSeparator());
        goals.AddChild(Style.WellLabel("End goals"));
        foreach (var ore in World.MineOres) goals.AddChild(GoalRow($"A working {ore.Id()} mine", world.HasMine(ore), false, Icons.Of(World.ResourceItem(ore))));
        var target = world.Data.Goals;
        goals.AddChild(GoalRow($"{target.DailyProfit} coins profit a day for {target.ProfitDays} days in a row", world.ProfitGoalMet, false, Icons.Of(Inventory.Coins)));
        var streak = new HBoxContainer(); streak.AddThemeConstantOverride("separation", 4); goals.AddChild(streak);
        streak.AddChild(new Control { CustomMinimumSize = new Vector2(70, 0) });
        int days = Math.Min(world.ProfitStreak, target.ProfitDays);
        for (int i = 0; i < target.ProfitDays; i++) {
            var day = new Panel { CustomMinimumSize = new Vector2(26, 10), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, TooltipText = i < days ? "A day at the target" : "A day still to go" };
            day.AddThemeStyleboxOverride("panel", i < days ? Style.BarGrow : Style.BarBack); streak.AddChild(day);
        }
        string yesterday = world.S.Profits.Count > 0 ? $"yesterday {world.S.Profits[^1]}" : "no day finished yet";
        var profit = Style.Text($"Profit is sale coins minus wages · {yesterday} · today's sales {world.S.DayRevenue}", 13, Style.Tan); profit.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        goals.AddChild(profit);
        hostButton.Disabled = net.Online; hostButton.Text = net.Hosting ? "Hosting" : "Host";
        // Joining elsewhere would drop the guests without a word, and a guest's saves and loads are the host's.
        joinButton.Text = world.Remote ? "Leave game" : "Join"; joinButton.Disabled = net.Hosting; saveButton.Disabled = loadButton.Disabled = newGameButton.Disabled = world.Remote;
        netLine.Text = NetStatus(); netLine.Visible = netLine.Text != "";
        menu.Visible = true; state.Paused = true;
    }

    private void CloseMenu() { menu.Visible = false; state.Paused = false; }

    /// <summary>Asks before leaving the settlement, since anything done after the last save would be lost.</summary>
    private void ConfirmLeave(string heading, string confirm, Action leave) {
        OpenScreen(heading, BackToPause);
        var note = Style.Wrapped(net.Hosting ? "Progress since the last save is lost, and your guests are disconnected." : "Progress since the last save is lost.", 14);
        note.HorizontalAlignment = HorizontalAlignment.Center; screen.AddChild(note);
        AddScreenButton(confirm, leave);
        AddScreenButton("Back · Esc", BackToPause);
    }
}
