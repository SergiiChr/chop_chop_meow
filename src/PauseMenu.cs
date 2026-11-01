using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The Esc menu: pauses the world and lists the starter goal chain with the current goal's hint, then the end goals.</summary>
public partial class PauseMenu : PanelContainer {
    private readonly UiKit kit;
    private readonly World world;
    private readonly Net net;
    private readonly StartMenu startMenu;
    private readonly IGameFlow flow;
    private readonly VBoxContainer goals;
    private readonly Button loadButton, newGameButton;
    private readonly Label netLine;
    private int shownGuests;

    public Button HostButton { get; }
    public Button JoinButton { get; }
    public Button SaveButton { get; }

    /// <summary>
    /// The pause board: the goal checklist in a well on the left, with the current goal's hint under it and the end goals below,
    /// and the game's buttons stacked on the right.
    /// </summary>
    public PauseMenu(UiKit kit, Net net, StartMenu startMenu, IGameFlow flow) {
        this.kit = kit; world = kit.World; this.net = net; this.startMenu = startMenu; this.flow = flow;
        Visible = false; CustomMinimumSize = new Vector2(820, 0);
        AddThemeStyleboxOverride("panel", Style.Board);
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); AddChild(column);
        var title = Style.Heading("Paused", 28); column.AddChild(title);
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 20); column.AddChild(body);
        var well = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; well.AddThemeStyleboxOverride("panel", Style.Well); body.AddChild(well);
        goals = new VBoxContainer(); goals.AddThemeConstantOverride("separation", 6); well.AddChild(goals);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) }; side.AddThemeConstantOverride("separation", 12); body.AddChild(side);
        var resume = kit.BigButton("Resume · Esc", Style.Brick, Close); resume.Name = "resume"; side.AddChild(resume);
        SaveButton = MenuButton("Save", Style.Enamel, () => startMenu.ShowSlots(true, BackToPause)); SaveButton.Name = "save"; side.AddChild(SaveButton);
        loadButton = MenuButton("Load", Style.Enamel, () => startMenu.ShowSlots(false, BackToPause)); loadButton.Name = "load"; side.AddChild(loadButton);
        var options = MenuButton("Options", Style.Enamel, () => startMenu.ShowOptions(BackToPause)); options.Name = "options"; side.AddChild(options);
        newGameButton = MenuButton("New game", Style.Enamel, () => ConfirmLeave("NEW GAME", "Start a new game", flow.Restart)); newGameButton.Name = "new_game"; side.AddChild(newGameButton);
        var quitButton = MenuButton("Quit", Style.Enamel, () => ConfirmLeave("QUIT", "Quit to desktop", flow.Quit)); quitButton.Name = "quit"; side.AddChild(quitButton);
        side.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        HostButton = MenuButton("Host", Style.Sage, HostGame); HostButton.Name = "host"; side.AddChild(HostButton);
        JoinButton = MenuButton("Join", Style.Sage, () => { if (!flow.Owns) { net.Leave(); flow.Leave(); } else startMenu.ShowJoin(BackToPause, true); }); JoinButton.Name = "join_game"; side.AddChild(JoinButton);
        netLine = Style.Text("", 13, Style.Beige, wrap: true); netLine.Name = "net_status"; netLine.HorizontalAlignment = HorizontalAlignment.Center; side.AddChild(netLine);
        world.Replaced += () => Visible = false;
        kit.HoldWhile(() => Visible);
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
        mark.CustomMinimumSize = new Vector2(24, 24); mark.SizeFlagsVertical = SizeFlags.ShrinkCenter; mark.VerticalAlignment = VerticalAlignment.Center;
        return mark;
    }

    /// <summary>Opens the board up to date with the goals and the game's online state, and holds the player.</summary>
    public void Open() {
        foreach (var child in goals.GetChildren()) child.QueueFree();
        goals.AddChild(Style.WellLabel("Getting started"));
        // Goals already met shrink to a row of ticks, named on hover; the current goal sits in a framed box with its hint.
        var met = new HFlowContainer(); met.AddThemeConstantOverride("h_separation", 4); met.AddThemeConstantOverride("v_separation", 4); goals.AddChild(met);
        for (int i = 0; i < World.GoalCount; i++) {
            var (title, _) = World.ObjectiveFor(i);
            if (i < world.Goal) { var tick = GoalMark(true, false); tick.TooltipText = title; tick.MouseFilter = Control.MouseFilterEnum.Pass; met.AddChild(tick); continue; }
            if (i == world.Goal) {
                var current = new PanelContainer(); current.AddThemeStyleboxOverride("panel", Style.Dropdown); goals.AddChild(current);
                var inner = new VBoxContainer(); inner.AddThemeConstantOverride("separation", 4); current.AddChild(inner);
                inner.AddChild(GoalRow(title, false, true));
                var hint = Style.Text(world.Objective.Hint, 13, Style.Tan, wrap: true); inner.AddChild(hint);
                continue;
            }
            goals.AddChild(GoalRow(title, false, false));
        }
        met.Visible = met.GetChildCount() > 0;
        if (world.GoalComplete) { var hint = Style.Text(world.Objective.Hint, 13, Style.Tan, wrap: true); goals.AddChild(hint); }
        goals.AddChild(new HSeparator());
        goals.AddChild(Style.WellLabel("End goals"));
        foreach (var ore in World.MineOres) goals.AddChild(GoalRow($"A working {ore.Id()} mine", world.HasMine(ore), false, Icons.Of(World.ResourceItem(ore))));
        var target = world.Data.Goals;
        goals.AddChild(GoalRow($"{target.DailyProfit} coins profit a day for {target.ProfitDays} days in a row", world.ProfitGoalMet, false, Icons.Of(Inventory.Coins)));
        var streak = new HBoxContainer(); streak.AddThemeConstantOverride("separation", 4); goals.AddChild(streak);
        streak.AddChild(new Control { CustomMinimumSize = new Vector2(70, 0) });
        int days = Math.Min(world.ProfitStreak, target.ProfitDays);
        for (int i = 0; i < target.ProfitDays; i++) {
            var day = new Panel { CustomMinimumSize = new Vector2(26, 10), SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = i < days ? "A day at the target" : "A day still to go" };
            day.AddThemeStyleboxOverride("panel", i < days ? Style.BarGrow : Style.BarBack); streak.AddChild(day);
        }
        string yesterday = world.Profits.Count > 0 ? $"yesterday {world.Profits[^1]}" : "no day finished yet";
        var profit = Style.Text($"Profit is sale coins minus wages · {yesterday} · today's sales {world.DayRevenue}", 13, Style.Tan, wrap: true);
        goals.AddChild(profit);
        HostButton.Disabled = !net.Owning || net.Opened; HostButton.Text = net.Opened ? "Hosting" : "Host";
        // Joining elsewhere would drop the guests without a word, and a guest's saves and loads are the host's.
        JoinButton.Text = flow.Owns ? "Join" : "Leave game"; JoinButton.Disabled = net.Opened; SaveButton.Disabled = loadButton.Disabled = newGameButton.Disabled = !flow.Owns;
        shownGuests = Guests; netLine.Text = NetStatus(); netLine.Visible = netLine.Text != "";
        Visible = true;
    }

    public override void _Process(double delta) {
        // Players join and leave while the menu is open.
        if (Visible && Guests != shownGuests) { shownGuests = Guests; netLine.Text = NetStatus(); }
    }

    private void BackToPause() { startMenu.Close(); Open(); }

    public void Close() => Visible = false;

    private void HostGame() {
        net.Open();
        var addresses = IP.GetLocalAddresses().Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254.")).Take(2).ToArray();
        kit.Notify($"Hosting on port {net.Port}. Others join with {(addresses.Length > 0 ? string.Join(" or ", addresses) : "this computer's address")}.");
        Close();
    }

    /// <summary>A line for the pause menu while the game is online.</summary>
    private string NetStatus() => net.Opened ? $"Hosting on port {net.Port} · {Guests} {(Guests == 1 ? "guest" : "guests")}"
        : !flow.Owns ? "Playing in the host's world. The host's saves keep your player." : "";

    private int Guests => world.Players.Count - 1;

    /// <summary>Asks before leaving the settlement, since anything done after the last save would be lost.</summary>
    private void ConfirmLeave(string heading, string confirm, Action leave) {
        startMenu.Open(heading, BackToPause);
        var note = Style.Wrapped(net.Opened ? "Progress since the last save is lost, and your guests are disconnected." : "Progress since the last save is lost.", 14);
        note.HorizontalAlignment = HorizontalAlignment.Center; startMenu.Screen.AddChild(note);
        startMenu.AddButton(confirm, leave);
        startMenu.AddButton("Back · Esc", BackToPause);
    }
}
