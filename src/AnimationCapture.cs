using System;
using System.Globalization;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    // Developer review fixture: capture the exact runtime frames, never alter a playable save.
    // --capture-frames sets how many frames are saved and --capture-delay how long the scene runs first.
    // Launch Godot with --fixed-fps 30 before the game arguments, so each frame is a fixed step however long it renders.
    private async void CaptureAnimations(string directory) {
        try {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            int frames = int.TryParse(Launch.Value("--capture-frames"), CultureInfo.InvariantCulture, out int count) ? count : 96;
            if (float.TryParse(Launch.Value("--capture-delay"), CultureInfo.InvariantCulture, out float delay) && delay > 0)
                await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
            for (int frame = 0; frame < frames; frame++) {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var result = GetViewport().GetTexture().GetImage().SavePng($"{directory}/{frame:D3}.png");
                if (result != Error.Ok) throw new InvalidOperationException($"Animation capture failed: {result}");
            }
            GD.Print("ANIMATION-CAPTURE PASS"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
