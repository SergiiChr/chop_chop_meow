using Godot;

namespace ChopChopMeow;

/// <summary>
/// The intro film, shown over the start menu when the game opens.
/// While it plays it quiets the game's music and takes every key and click; Esc skips it.
/// The film is a small movie the window blows up, made from frames the game itself draws with <c>--render-intro</c>.
/// </summary>
public partial class IntroMovie(Sounds sounds) : Control {
    private const string Movie = "res://assets/intro.ogv";
    /// <summary>Seconds the skip hint stays before it fades.</summary>
    private const float HintSeconds = 4;
    private readonly VideoStreamPlayer player = new() { Expand = true, Stream = GD.Load<VideoStream>(Movie) };
    private readonly Label hint = Style.Outlined(Style.Text("Esc · skip", 15, Style.Beige));
    private float shown;

    public override void _Ready() {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var black = new ColorRect { Color = Colors.Black };
        black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(black);
        // The film keeps its shape in a window of any other, with black around it.
        var frame = new AspectRatioContainer { Ratio = 16f / 9, MouseFilter = MouseFilterEnum.Ignore };
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(frame);
        frame.AddChild(player);
        hint.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 24); AddChild(hint);
        Visible = false; SetProcess(false);
    }

    public void Play() {
        Visible = true; SetProcess(true);
        shown = 0; hint.Modulate = Colors.White;
        sounds.MusicVolume = 0;
        player.Play();
    }

    private void End() {
        player.Stop();
        Visible = false; SetProcess(false);
        sounds.MusicVolume = Settings.MusicVolume;
    }

    public override void _Process(double delta) {
        shown += (float)delta;
        hint.Modulate = new Color(1, 1, 1, Mathf.Clamp(HintSeconds + 1 - shown, 0, 1));
        if (!player.IsPlaying()) End();
    }

    public override void _Input(InputEvent ev) {
        if (!Visible || ev is not (InputEventKey or InputEventMouseButton)) return;
        if (ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) End();
        // Nothing under the film hears a key or a click, the Esc that skipped it included.
        GetViewport().SetInputAsHandled();
    }
}
