using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The Esc menu: pauses the world and lists the starter goal chain with the current goal's hint, then the end goals.</summary>
public partial class Game
{
    private PanelContainer menu = null!;
    private VBoxContainer goals = null!;
    private Button hostButton = null!, joinButton = null!, saveButton = null!;
    private Label netLine = null!;

    private Button MenuIcon(string icon, string tooltip, Action action)
    {
        var button = Button("", action);
        button.Name = icon; button.TooltipText = tooltip;
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/{icon}.svg");
        button.ExpandIcon = true; button.IconAlignment = HorizontalAlignment.Center; button.VerticalIconAlignment = VerticalAlignment.Center;
        button.CustomMinimumSize = new Vector2(62, 62); button.AddThemeConstantOverride("icon_max_width", 40);
        return button;
    }

    private void BuildMenu()
    {
        menu = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(460, 0) };
        menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        menu.GrowHorizontal = Control.GrowDirection.Both; menu.GrowVertical = Control.GrowDirection.Both;
        ui.AddChild(menu);
        var column = new VBoxContainer(); menu.AddChild(column);
        var title = Label("PAUSED", 22); title.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(title);
        goals = new VBoxContainer(); goals.AddThemeConstantOverride("separation", 3); column.AddChild(goals);
        var resume = Button("Resume · Esc", CloseMenu); resume.Name = "resume"; column.AddChild(resume);
        var files = new HBoxContainer(); column.AddChild(files);
        foreach (var (text, action) in new (string, Action)[] { ("Save", () => ShowSlots(true, BackToPause)), ("Load", () => ShowSlots(false, BackToPause)), ("Options", () => ShowOptions(BackToPause)) })
        {
            var button = Button(text, action); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; files.AddChild(button);
            if (text == "Save") saveButton = button;
        }
        var online = new HBoxContainer(); column.AddChild(online);
        hostButton = Button("Host", HostGame); hostButton.Name = "host";
        joinButton = Button("Join", () => { if (world.Remote) { net.Leave(); world.NewGame(); ShowStart(); } else ShowJoin(BackToPause, true); }); joinButton.Name = "join_game";
        foreach (var button in new[] { hostButton, joinButton }) { button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; online.AddChild(button); }
        netLine = Label("", 13, Muted); netLine.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(netLine);
    }

    private void OpenMenu()
    {
        foreach (var child in goals.GetChildren()) child.QueueFree();
        for (int i = 0; i < World.GoalCount; i++)
        {
            var (title, _) = World.ObjectiveFor(i);
            bool done = i < world.S.Goal, current = i == world.S.Goal;
            goals.AddChild(Label($"{(done ? "✓" : current ? "▸" : "·")}  {title}", 15, done ? Muted : current ? new Color("f2d98b") : Cream));
            if (current) goals.AddChild(Label(world.Objective.Hint, 14, Muted));
        }
        if (world.GoalComplete) goals.AddChild(Label(world.Objective.Hint, 14, Muted));
        goals.AddChild(Label("END GOALS", 16));
        foreach (string ore in World.MineOres)
        {
            bool built = world.HasMine(ore);
            goals.AddChild(Label($"{(built ? "✓" : "·")}  A working {ore} mine", 15, built ? Muted : Cream));
        }
        var target = world.Data.Goals;
        goals.AddChild(Label($"{(world.ProfitGoalMet ? "✓" : "·")}  {target.DailyProfit} coins profit a day for {target.ProfitDays} days in a row", 15, world.ProfitGoalMet ? Muted : Cream));
        string yesterday = world.S.Profits.Count > 0 ? $"yesterday {world.S.Profits[^1]}" : "no day finished yet";
        goals.AddChild(Label($"Profit is sale coins minus wages. {Math.Min(world.ProfitStreak, target.ProfitDays)}/{target.ProfitDays} days · {yesterday} · today's sales {world.S.DayRevenue}", 14, Muted));
        hostButton.Disabled = net.Online; hostButton.Text = net.Hosting ? "Hosting" : "Host";
        joinButton.Text = world.Remote ? "Leave game" : "Join"; saveButton.Disabled = world.Remote;
        netLine.Text = NetStatus(); netLine.Visible = netLine.Text != "";
        menu.Visible = true; paused = true;
    }

    private void CloseMenu() { menu.Visible = false; paused = false; }
}
