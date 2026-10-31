using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shown once when the end goals are reached; playing on is allowed.</summary>
public partial class EndingScreen : PanelContainer {
    private readonly UiKit kit;
    private readonly Action closePanel;
    private readonly Label stats;
    /// <summary>Whether this player has seen the ending; a guest's copy of the save flag follows the host's.</summary>
    private bool seen;

    public EndingScreen(UiKit kit, Action closePanel, Action restart) {
        this.kit = kit; this.closePanel = closePanel;
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        CustomMinimumSize = new Vector2(440, 0);
        var column = new VBoxContainer(); AddChild(column);
        var title = Style.Wrapped("A settlement of your own", 26); title.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(title);
        stats = Style.Wrapped("", 16, Style.Beige); stats.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(stats);
        var keep = kit.Button("Keep playing", () => Visible = false); keep.Name = "keep_playing"; column.AddChild(keep);
        var again = kit.Button("Restart", restart); again.Name = "restart"; column.AddChild(again);
        kit.World.Replaced += () => Visible = false;
        kit.HoldWhile(() => Visible);
    }

    /// <summary>Called on every running frame; opens the screen the first time the end goals are complete.</summary>
    public void ShowOnce() {
        var world = kit.World;
        // A guest's copy takes the host's flag with every update, so a guest remembers its own.
        if ((world.Remote ? seen : world.EndingShown) || !world.EndGoalsComplete) return;
        world.S.EndingShown = seen = true; closePanel();
        int seconds = (int)world.S.PlaySeconds;
        stats.Text = $"Every ore in the valley has its mine, and the settlement cleared {world.Data.Goals.DailyProfit} coins a day for {world.Data.Goals.ProfitDays} days.\nPlay time {seconds / 60}:{seconds % 60:00} · day {world.Day} · {world.Workers.Count} villagers";
        Visible = true;
    }

    /// <summary>A guest that joined another game may see the ending again; one that came back to the same game keeps it dismissed.</summary>
    public void Joined(bool back) => seen = seen && back;
}
