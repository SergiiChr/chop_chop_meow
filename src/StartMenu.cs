using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The start menu and the save, load and options screens it shares with the pause menu.</summary>
public partial class Game {
    private const int SlotCount = 3;
    private static readonly Vector2I[] Resolutions = { new(1280, 720), new(1440, 900), new(1600, 900), new(1920, 1080), new(2560, 1440) };
    private ColorRect backdrop = null!;
    private VBoxContainer screen = null!;
    private Action? screenBack;
    /// <summary>Numbered slot that F5 and F9 use; 0 until the player saves to or loads one.</summary>
    private int slot;

    // Review fixtures and UI checks keep their own files so they never touch the player's saves or settings.
    private static string FilePrefix => OS.GetCmdlineUserArgs().Contains("--showcase") ? "review-" : OS.GetCmdlineUserArgs().Contains("--ui-test") ? "ui-check-" : OS.GetCmdlineUserArgs().Contains("--net-check") ? "net-check-" : "";
    private static string SettingsPath => $"user://{FilePrefix}settings.cfg";
    // Slot 0 is the autosave; slot 1 keeps the original single-save file name, so older saves load as slot 1.
    private static string SlotPath(int n) => $"user://{FilePrefix}{(n == 0 ? "autosave" : n == 1 ? "settlement" : $"settlement-{n}")}.json";
    private static string SlotName(int n) => n == 0 ? "Autosave" : $"Slot {n}";
    private static bool SlotUsed(int n) => Godot.FileAccess.FileExists(SlotPath(n));
    private static long SavedAt(int n) => (long)Godot.FileAccess.GetModifiedTime(SlotPath(n));

    private static string SlotLabel(int n) {
        if (!SlotUsed(n)) return $"{SlotName(n)} · empty";
        long local = SavedAt(n) + Time.GetTimeZoneFromSystem()["bias"].AsInt64() * 60;
        return $"{SlotName(n)} · {Time.GetDatetimeStringFromUnixTime(local, true)[..16]}";
    }

    private void BuildScreens() {
        backdrop = new ColorRect { Color = new Color(.07f, .12f, .09f, .86f), Visible = false };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); ui.AddChild(backdrop);
        var center = new CenterContainer(); center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); backdrop.AddChild(center);
        var box = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) }; center.AddChild(box);
        screen = new VBoxContainer(); box.AddChild(screen);
    }

    private void OpenScreen(string heading, Action? back) {
        foreach (var child in screen.GetChildren()) { screen.RemoveChild(child); child.QueueFree(); }
        var title = Label(heading, 22); title.HorizontalAlignment = HorizontalAlignment.Center; screen.AddChild(title);
        screenBack = back; backdrop.Visible = true; menu.Visible = false; paused = true;
    }

    private void AddScreenButton(string text, Action action, bool disabled = false) => screen.AddChild(Button(text, action, disabled));

    private void HideScreen() { backdrop.Visible = false; screenBack = null; paused = false; }

    private void BackToPause() { HideScreen(); OpenMenu(); }

    private void ShowStart() {
        ClosePanel(); OpenScreen("CHOP CHOP MEOW", null);
        if (leftReason != "") { var note = Label(leftReason, 14, new Color("f2b38b")); note.HorizontalAlignment = HorizontalAlignment.Center; screen.AddChild(note); leftReason = ""; }
        int latest = Enumerable.Range(0, SlotCount + 1).Where(SlotUsed).OrderByDescending(SavedAt).DefaultIfEmpty(-1).First();
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
        for (int n = saving ? 1 : 0; n <= SlotCount; n++) {
            int target = n;
            AddScreenButton(SlotLabel(n), () => { if (saving) { Save(target); back(); } else LoadSlot(target); }, !saving && !SlotUsed(n));
        }
        AddScreenButton("Back · Esc", back);
    }

    private void ShowOptions(Action back) {
        OpenScreen("OPTIONS", back);
        var row = new HBoxContainer(); screen.AddChild(row);
        var label = Label("Resolution"); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(label);
        var picker = new OptionButton { Name = "resolution", CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        foreach (var size in Resolutions) picker.AddItem($"{size.X} × {size.Y}");
        picker.Selected = Array.IndexOf(Resolutions, SavedResolution());
        picker.ItemSelected += index => {
            StoreSetting("display", "resolution", Resolutions[index]);
            ApplyResolution(Resolutions[index]);
        };
        row.AddChild(picker);
        var musicRow = new HBoxContainer(); screen.AddChild(musicRow);
        var musicLabel = Label("Music volume"); musicLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; musicRow.AddChild(musicLabel);
        var slider = new HSlider { Name = "music", MaxValue = 100, Step = 5, Value = SavedMusicVolume() * 100, CustomMinimumSize = new Vector2(180, 38), FocusMode = Control.FocusModeEnum.None };
        slider.ValueChanged += value => {
            var config = new ConfigFile(); config.Load(SettingsPath);
            config.SetValue("audio", "music", value / 100); config.Save(SettingsPath);
            sounds.MusicVolume = (float)value / 100;
        };
        musicRow.AddChild(slider);
        AddScreenButton("Back · Esc", back);
    }

    /// <summary>The stored window size, or the project's default size before the player picks one.</summary>
    private static Vector2I SavedResolution() => Setting("display", "resolution",
        new Vector2I(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32(), ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32()));

    private static T Setting<[MustBeVariant] T>(string section, string key, T fallback) {
        var config = new ConfigFile(); config.Load(SettingsPath);
        return config.GetValue(section, key, Variant.From(fallback)).As<T>();
    }

    private static void StoreSetting<[MustBeVariant] T>(string section, string key, T value) {
        var config = new ConfigFile(); config.Load(SettingsPath);
        config.SetValue(section, key, Variant.From(value)); config.Save(SettingsPath);
    }

    private static float SavedMusicVolume() {
        var config = new ConfigFile(); config.Load(SettingsPath);
        return config.GetValue("audio", "music", 1f).AsSingle();
    }

    private void ApplyResolution(Vector2I size) {
        var window = GetWindow(); window.Mode = Window.ModeEnum.Windowed; window.Size = size; window.MoveToCenter();
    }

    private void LoadSlot(int n) {
        if (!Load(n)) return;
        HideScreen(); ending.Visible = false;
    }
}
