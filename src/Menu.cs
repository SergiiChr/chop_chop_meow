using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The Esc menu: pauses the world and lists the goal chain with the current goal's hint.</summary>
public partial class Game
{
    private PanelContainer menu = null!;
    private VBoxContainer goals = null!;

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
        }
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
        menu.Visible = true; paused = true;
    }

    private void CloseMenu() { menu.Visible = false; paused = false; }
}
