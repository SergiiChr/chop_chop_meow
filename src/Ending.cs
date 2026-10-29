using Godot;

namespace ChopChopMeow;

/// <summary>Shown once when the end goals are reached; playing on is allowed.</summary>
public partial class Game {
    private PanelContainer ending = null!;
    private Label endingStats = null!;
    private bool endingSeen;

    private void BuildEnding() {
        ending = new PanelContainer { Visible = false };
        ending.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        ending.GrowHorizontal = Control.GrowDirection.Both; ending.GrowVertical = Control.GrowDirection.Both;
        ending.CustomMinimumSize = new Vector2(440, 0);
        ui.AddChild(ending);
        var column = new VBoxContainer(); ending.AddChild(column);
        var title = Label("A settlement of your own", 26); title.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(title);
        endingStats = Label("", 16, Muted); endingStats.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(endingStats);
        var keep = Button("Keep playing", () => { ending.Visible = false; paused = false; }); keep.Name = "keep_playing"; column.AddChild(keep);
        var restart = Button("Restart", Restart); restart.Name = "restart"; column.AddChild(restart);
    }

    private void ShowEndingOnce() {
        // A guest's copy takes the host's flag with every update, so a guest remembers its own.
        if ((world.Remote ? endingSeen : world.S.EndingShown) || !world.EndGoalsComplete) return;
        world.S.EndingShown = endingSeen = true; paused = true; ClosePanel();
        int seconds = (int)world.S.PlaySeconds;
        endingStats.Text = $"Every ore in the valley has its mine, and the settlement cleared {world.Data.Goals.DailyProfit} coins a day for {world.Data.Goals.ProfitDays} days.\nPlay time {seconds / 60}:{seconds % 60:00} · day {world.Day} · {world.S.Workers.Count} villagers";
        ending.Visible = true;
    }

    private void Restart() {
        net.Leave();
        world.NewGame();
        selected = 0; workerId = 0; gathering = null; assigningWorker = 0; slot = 0; autosave = 0;
        view.BuildMode = ""; view.Selected = 0; view.SelectedWorker = 0;
        ending.Visible = false; paused = false; OpenPage("welcome");
        Notify("Welcome. WASD to move; hold right mouse to approach and gather. I shows the field guide.");
    }
}
