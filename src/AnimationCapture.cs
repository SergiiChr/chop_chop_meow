using System;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    // Developer review fixture: capture the exact runtime clips, never alter a playable save.
    private async void CaptureAnimations(string directory) {
        try {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            for (int frame = 0; frame < 96; frame++) {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var result = GetViewport().GetTexture().GetImage().SavePng($"{directory}/{frame:D3}.png");
                if (result != Error.Ok) throw new InvalidOperationException($"Animation capture failed: {result}");
            }
            GD.Print("ANIMATION-CAPTURE PASS"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
