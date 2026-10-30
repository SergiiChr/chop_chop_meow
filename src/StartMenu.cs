using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The start menu and the save, load and options screens it shares with the pause menu.</summary>
public partial class Game {
    private static readonly Vector2I[] Resolutions = { new(1280, 720), new(1440, 900), new(1600, 900), new(1920, 1080), new(2560, 1440) };
    /// <summary>Frame rate caps offered in the options; 0 leaves the rate to vsync alone.</summary>
    private static readonly int[] FrameLimits = { 0, 60, 90 };
    private ColorRect backdrop = null!;
    private VBoxContainer screen = null!;
    private Action? screenBack;

    private void BuildScreens() {
        backdrop = new ColorRect { Color = new Color(.07f, .12f, .09f, .86f), Visible = false };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); ui.AddChild(backdrop);
        var center = new CenterContainer(); center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); backdrop.AddChild(center);
        var box = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) }; center.AddChild(box);
        screen = new VBoxContainer(); box.AddChild(screen);
    }

    private void OpenScreen(string heading, Action? back) {
        foreach (var child in screen.GetChildren()) { screen.RemoveChild(child); child.QueueFree(); }
        var title = Style.Wrapped(heading, 22); title.HorizontalAlignment = HorizontalAlignment.Center; screen.AddChild(title);
        screenBack = back; backdrop.Visible = true; menu.Visible = false; state.Paused = true;
    }

    private void AddScreenButton(string text, Action action, bool disabled = false) => screen.AddChild(kit.Button(text, action, disabled));

    private void HideScreen() { backdrop.Visible = false; screenBack = null; state.Paused = false; }

    private void BackToPause() { HideScreen(); OpenMenu(); }

    private void ShowStart() {
        panel.Close(); OpenScreen("CHOP CHOP MEOW", null);
        if (leftReason != "") { var note = Style.Wrapped(leftReason, 14, new Color("f2b38b")); note.HorizontalAlignment = HorizontalAlignment.Center; screen.AddChild(note); leftReason = ""; }
        int latest = Saves.Newest();
        AddScreenButton("Continue", () => LoadSlot(latest), latest < 0);
        AddScreenButton("New game", () => { HideScreen(); Restart(); });
        AddScreenButton("Load game", () => ShowSlots(false, ShowStart));
        AddScreenButton("Join game", () => ShowJoin(ShowStart, false));
        AddScreenButton("Options", () => ShowOptions(ShowStart));
        AddScreenButton("Quit", () => Quit());
    }

    private void ShowSlots(bool saving, Action back) {
        OpenScreen(saving ? "SAVE GAME" : "LOAD GAME", back);
        // The autosave is written by the game alone, so it is only offered for loading.
        for (int n = saving ? 1 : 0; n <= Saves.SlotCount; n++) {
            int target = n;
            AddScreenButton(Saves.SlotLabel(n), () => { if (saving) { saves.Save(target); back(); } else LoadSlot(target); }, !saving && !Saves.SlotUsed(n));
        }
        AddScreenButton("Back · Esc", back);
    }

    private void ShowOptions(Action back) {
        OpenScreen("OPTIONS", back);
        var row = new HBoxContainer(); screen.AddChild(row);
        var label = Style.Wrapped("Resolution"); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(label);
        var picker = new OptionButton { Name = "resolution", CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        foreach (var size in Resolutions) picker.AddItem($"{size.X} × {size.Y}");
        picker.Selected = Array.IndexOf(Resolutions, Settings.Resolution);
        picker.ItemSelected += index => {
            Settings.Store("display", "resolution", Resolutions[index]);
            Settings.ApplyResolution(GetWindow(), Resolutions[index]);
        };
        row.AddChild(picker);
        var limitRow = new HBoxContainer(); screen.AddChild(limitRow);
        var limitLabel = Style.Wrapped("Frame rate limit"); limitLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; limitRow.AddChild(limitLabel);
        var limits = new OptionButton { Name = "frame_limit", CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        foreach (int fps in FrameLimits) limits.AddItem(fps == 0 ? "Off" : $"{fps} fps");
        limits.Selected = Math.Max(0, Array.IndexOf(FrameLimits, Settings.FrameLimit));
        limits.ItemSelected += index => {
            Settings.Store("display", "frame_limit", FrameLimits[index]);
            Engine.MaxFps = FrameLimits[index];
        };
        limitRow.AddChild(limits);
        var musicRow = new HBoxContainer(); screen.AddChild(musicRow);
        var musicLabel = Style.Wrapped("Music volume"); musicLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; musicRow.AddChild(musicLabel);
        var slider = new HSlider { Name = "music", MaxValue = 100, Step = 5, Value = Settings.MusicVolume * 100, CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        slider.ValueChanged += value => {
            Settings.Store("audio", "music", value / 100);
            sounds.MusicVolume = (float)value / 100;
        };
        musicRow.AddChild(slider);
        var taaRow = new HBoxContainer(); screen.AddChild(taaRow);
        var taaLabel = Style.Wrapped("Temporal anti-aliasing"); taaLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; taaRow.AddChild(taaLabel);
        var taa = new CheckButton { Name = "taa", ButtonPressed = Settings.Taa, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
        taa.Toggled += on => { Settings.Store("display", "taa", on); GetViewport().UseTaa = on; };
        taaRow.AddChild(taa);
        AddScreenButton("Back · Esc", back);
    }

    private void LoadSlot(int n) {
        if (!saves.Load(n)) return;
        HideScreen(); ending.Visible = false;
    }
}
