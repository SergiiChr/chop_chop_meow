using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The Esc menu: holds the player, shows the goal log and offers game controls.</summary>
public partial class PauseMenu : PanelContainer {
    private readonly UiKit kit;
    private readonly World world;
    private readonly Net net;
    private readonly StartMenu startMenu;
    private readonly IGameFlow flow;
    private readonly GoalLog goalLog;
    private readonly Button loadButton, newGameButton;
    private readonly Label netLine;
    private int shownGuests;

    public Control GoalDetails => goalLog.Details;
    public Button HostButton { get; }
    public Button JoinButton { get; }
    public Button SaveButton { get; }

    public PauseMenu(UiKit kit, Net net, StartMenu startMenu, IGameFlow flow, RecipeMap map) {
        this.kit = kit; world = kit.World; this.net = net; this.startMenu = startMenu; this.flow = flow;
        Visible = false; CustomMinimumSize = new Vector2(820, 0);
        AddThemeStyleboxOverride("panel", Style.Board);
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); AddChild(column);
        var title = Style.Heading("Paused", 28); column.AddChild(title);
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 20); column.AddChild(body);
        var well = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; well.AddThemeStyleboxOverride("panel", Style.Well); body.AddChild(well);
        goalLog = new GoalLog(kit, map, Close); well.AddChild(goalLog);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) }; side.AddThemeConstantOverride("separation", 12); body.AddChild(side);
        var resume = kit.BigButton("Resume · Esc", Style.Brick, Close); resume.Name = "resume"; side.AddChild(resume);
        SaveButton = MenuButton("Save", Style.Enamel, () => startMenu.ShowSlots(true, BackToPause)); SaveButton.Name = "save"; side.AddChild(SaveButton);
        loadButton = MenuButton("Load", Style.Enamel, () => startMenu.ShowSlots(false, BackToPause)); loadButton.Name = "load"; side.AddChild(loadButton);
        var options = MenuButton("Options", Style.Enamel, () => startMenu.ShowOptions(BackToPause)); options.Name = "options"; side.AddChild(options);
        side.AddChild(MenuButton("Controls", Style.Enamel, () => startMenu.ShowControls(BackToPause)));
        newGameButton = MenuButton("New game", Style.Enamel, () => ConfirmLeave("NEW GAME", "Start a new game", flow.Restart)); newGameButton.Name = "new_game"; side.AddChild(newGameButton);
        var quitButton = MenuButton("Quit", Style.Enamel, () => ConfirmLeave("QUIT", "Quit to desktop", flow.Quit)); quitButton.Name = "quit"; side.AddChild(quitButton);
        side.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        HostButton = MenuButton("Host", Style.Sage, HostGame); HostButton.Name = "host"; side.AddChild(HostButton);
        JoinButton = MenuButton("Join", Style.Sage, () => { if (!flow.Owns) flow.ShowStart(); else startMenu.ShowJoin(BackToPause, true); }); JoinButton.Name = "join_game"; side.AddChild(JoinButton);
        netLine = Style.Text("", 13, Style.Beige, wrap: true); netLine.Name = "net_status"; netLine.HorizontalAlignment = HorizontalAlignment.Center; side.AddChild(netLine);
        world.Replaced += Close;
        kit.HoldWhile(() => Visible);
    }

    private Button MenuButton(string text, Color color, Action action) {
        var button = kit.BigButton(text, color, action); button.CustomMinimumSize = new Vector2(0, 46); button.AddThemeFontSizeOverride("font_size", 16);
        return button;
    }

    /// <summary>Opens the board with the settlement's current goal progress and the game's online state.</summary>
    public void Open() {
        goalLog.Refresh();
        HostButton.Disabled = !net.Owning || net.Opened; HostButton.Text = net.Opened ? "Hosting" : "Host";
        // Joining elsewhere would drop the guests without a word, and a guest's saves and loads are the host's.
        bool guest = world.Remote && !flow.Owns;
        JoinButton.Text = guest ? "Leave game" : "Join"; JoinButton.Disabled = net.Opened; SaveButton.Disabled = loadButton.Disabled = newGameButton.Disabled = guest;
        shownGuests = Guests; netLine.Text = NetStatus(); netLine.Visible = netLine.Text != "";
        Visible = true;
    }

    /// <summary>Opens the board on one goal's details.</summary>
    public void OpenGoal(Goal goal) { if (!Visible) Open(); goalLog.ShowDetails(goal); }

    public override void _Process(double delta) {
        // Players join and leave while the menu is open.
        if (Visible && Guests != shownGuests) { shownGuests = Guests; netLine.Text = NetStatus(); }
    }

    private void BackToPause() { startMenu.Close(); Open(); }

    public void RefreshGoalLog() { if (Visible) goalLog.Refresh(); }

    public void Back() {
        if (GoalDetails.Visible) goalLog.CloseDetails(); else Close();
    }

    public void Close() { goalLog.CloseDetails(); Hide(); }

    private void HostGame() {
        net.Open();
        var addresses = IP.GetLocalAddresses().Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254.")).Take(2).ToArray();
        kit.Notify($"Hosting on port {net.Port}. Others join with {(addresses.Length > 0 ? string.Join(" or ", addresses) : "this computer's address")}.");
        Close();
    }

    /// <summary>A line for the pause menu while the game is online.</summary>
    private string NetStatus() => net.Opened ? $"Hosting on port {net.Port} · {Guests} {(Guests == 1 ? "guest" : "guests")}"
        : world.Remote && !flow.Owns ? "Playing in the host's world. The host's saves keep your player." : "";

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
