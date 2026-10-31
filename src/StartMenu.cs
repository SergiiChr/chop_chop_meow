using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The start menu and the save, load and options screens it shares with the pause menu.</summary>
public partial class StartMenu : ColorRect {
    public static readonly Vector2I[] Resolutions = { new(1280, 720), new(1440, 900), new(1600, 900), new(1920, 1080), new(2560, 1440) };
    /// <summary>Frame rate caps offered in the options; 0 leaves the rate to vsync alone.</summary>
    private static readonly int[] FrameLimits = { 0, 60, 90 };
    private readonly UiKit kit;
    private readonly World world;
    private readonly Saves saves;
    private readonly Net net;
    private readonly SidePanel panel;
    private readonly Action hideMenu, restart, quit;
    private Action? screenBack;
    private Label joinStatus = null!;
    private Label reconnectStatus = null!;

    /// <summary>The column the open screen's title and controls sit in.</summary>
    public VBoxContainer Screen { get; }
    /// <summary>Why the last joined game ended, shown once on the start menu.</summary>
    public string LeftReason { get; set; } = "";
    /// <summary>What the join popup last said.</summary>
    public string JoinStatus => joinStatus.Text;

    public StartMenu(UiKit kit, Saves saves, Net net, SidePanel panel, Action hideMenu, Action restart, Action quit) {
        this.kit = kit; world = kit.World; this.saves = saves; this.net = net; this.panel = panel; this.hideMenu = hideMenu; this.restart = restart; this.quit = quit;
        Color = new Color(.07f, .12f, .09f, .86f); Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer(); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(center);
        var box = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) }; center.AddChild(box);
        Screen = new VBoxContainer(); box.AddChild(Screen);
        world.Replaced += Close;
    }

    /// <summary>Clears the column for a new screen under a title; <paramref name="back"/> is what Esc does there.</summary>
    public void Open(string heading, Action? back) {
        foreach (var child in Screen.GetChildren()) { Screen.RemoveChild(child); child.QueueFree(); }
        var title = Style.Wrapped(heading, 22); title.HorizontalAlignment = HorizontalAlignment.Center; Screen.AddChild(title);
        screenBack = back; Visible = true; hideMenu(); kit.State.Paused = true;
    }

    public void AddButton(string text, Action action, bool disabled = false) => Screen.AddChild(kit.Button(text, action, disabled));

    public void Close() { Visible = false; screenBack = null; kit.State.Paused = false; }

    /// <summary>What Esc does on the open screen.</summary>
    public void Back() => screenBack?.Invoke();

    public void ShowStart() {
        panel.Close(); Open("CHOP CHOP MEOW", null);
        if (LeftReason != "") { var note = Style.Wrapped(LeftReason, 14, new Color("f2b38b")); note.HorizontalAlignment = HorizontalAlignment.Center; Screen.AddChild(note); LeftReason = ""; }
        int latest = Saves.Newest();
        AddButton("Continue", () => saves.Load(latest), latest < 0);
        AddButton("New game", restart);
        AddButton("Load game", () => ShowSlots(false, ShowStart));
        AddButton("Join game", () => ShowJoin(ShowStart, false));
        AddButton("Options", () => ShowOptions(ShowStart));
        AddButton("Quit", quit);
    }

    public void ShowSlots(bool saving, Action back) {
        Open(saving ? "SAVE GAME" : "LOAD GAME", back);
        // The autosave is written by the game alone, so it is only offered for loading.
        for (int n = saving ? 1 : 0; n <= Saves.SlotCount; n++) {
            int target = n;
            AddButton(Saves.SlotLabel(n), () => { if (saving) { saves.Save(target); back(); } else saves.Load(target); }, !saving && !Saves.SlotUsed(n));
        }
        AddButton("Back · Esc", back);
    }

    public void ShowOptions(Action back) {
        Open("OPTIONS", back);
        var row = new HBoxContainer(); Screen.AddChild(row);
        var label = Style.Wrapped("Resolution"); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(label);
        var picker = new OptionButton { Name = "resolution", CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        foreach (var size in Resolutions) picker.AddItem($"{size.X} × {size.Y}");
        picker.Selected = Array.IndexOf(Resolutions, Settings.Resolution);
        picker.ItemSelected += index => {
            Settings.Store("display", "resolution", Resolutions[index]);
            Settings.ApplyResolution(GetWindow(), Resolutions[index]);
        };
        row.AddChild(picker);
        var limitRow = new HBoxContainer(); Screen.AddChild(limitRow);
        var limitLabel = Style.Wrapped("Frame rate limit"); limitLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; limitRow.AddChild(limitLabel);
        var limits = new OptionButton { Name = "frame_limit", CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        foreach (int fps in FrameLimits) limits.AddItem(fps == 0 ? "Off" : $"{fps} fps");
        limits.Selected = Math.Max(0, Array.IndexOf(FrameLimits, Settings.FrameLimit));
        limits.ItemSelected += index => {
            Settings.Store("display", "frame_limit", FrameLimits[index]);
            Engine.MaxFps = FrameLimits[index];
        };
        limitRow.AddChild(limits);
        var musicRow = new HBoxContainer(); Screen.AddChild(musicRow);
        var musicLabel = Style.Wrapped("Music volume"); musicLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; musicRow.AddChild(musicLabel);
        var slider = new HSlider { Name = "music", MaxValue = 100, Step = 5, Value = Settings.MusicVolume * 100, CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        slider.ValueChanged += value => {
            Settings.Store("audio", "music", value / 100);
            kit.Sounds.MusicVolume = (float)value / 100;
        };
        musicRow.AddChild(slider);
        var taaRow = new HBoxContainer(); Screen.AddChild(taaRow);
        var taaLabel = Style.Wrapped("Temporal anti-aliasing"); taaLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; taaRow.AddChild(taaLabel);
        var taa = new CheckButton { Name = "taa", ButtonPressed = Settings.Taa, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
        taa.Toggled += on => { Settings.Store("display", "taa", on); GetViewport().UseTaa = on; };
        taaRow.AddChild(taa);
        AddButton("Back · Esc", back);
    }

    /// <summary>The join popup: the host's address and port, which starts out as the port a host opens.</summary>
    /// <param name="inGame">Opened from the pause menu: the game in progress is autosaved before the host's world replaces it.</param>
    public void ShowJoin(Action back, bool inGame) {
        Open("JOIN A GAME", () => { if (net.Connecting) net.Leave(); back(); });
        var address = new LineEdit { Name = "address", Text = Settings.Get("net", "address", ""), PlaceholderText = "Host address, such as 192.168.1.20", CustomMinimumSize = new Vector2(0, 38) };
        var port = new LineEdit { Name = "port", Text = Net.DefaultPort.ToString(), CustomMinimumSize = new Vector2(0, 38) };
        foreach (var (text, field) in new[] { ("Address", address), ("Port", port) }) {
            var row = new HBoxContainer(); Screen.AddChild(row);
            var label = Style.Wrapped(text); label.CustomMinimumSize = new Vector2(80, 0); row.AddChild(label);
            field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(field);
        }
        joinStatus = Style.Wrapped("", 14, Style.Beige); Screen.AddChild(joinStatus);
        var join = kit.Button("Join", () => {
            if (!int.TryParse(port.Text.Trim(), out int number) || number is < 1 or > 65535) { joinStatus.Text = "The port is a number from 1 to 65535."; return; }
            if (address.Text.Trim() == "") { joinStatus.Text = "Enter the host's address."; return; }
            if (inGame && !world.Remote) saves.Save(0);
            var error = net.Join(address.Text.Trim(), number);
            joinStatus.Text = error == Error.Ok ? "Connecting…" : $"Could not connect: {error}.";
        });
        join.Name = "join"; Screen.AddChild(join);
        address.TextSubmitted += _ => join.EmitSignal(BaseButton.SignalName.Pressed);
        AddButton("Back · Esc", Back);
        address.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Shown while a dropped guest tries to get back in.</summary>
    public void ShowReconnecting() {
        Open("RECONNECTING", () => { net.Leave(); ShowStart(); });
        reconnectStatus = Style.Wrapped("", 14, Style.Beige); Screen.AddChild(reconnectStatus);
        AddButton("Stop · Esc", Back);
    }

    public void ShowRetry(int left) { if (IsInstanceValid(reconnectStatus)) reconnectStatus.Text = $"{Net.LostLine} Trying again for {left} more {(left == 1 ? "second" : "seconds")}."; }

    public void ShowJoinFailure(string reason) { if (IsInstanceValid(joinStatus)) joinStatus.Text = reason; }
}
