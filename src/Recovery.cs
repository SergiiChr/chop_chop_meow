using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Recovery controls for a fallen player without a cabin to wake in.</summary>
public sealed partial class World {
    /// <summary>Whether the local downed player may advance this shared world to dawn.</summary>
    public bool CanSkipMorning {
        get {
            if (S.Actor.RespawnIn > 0) return !HasCabin() && NoLivingNeighbors();
            return false;
        }
    }

    internal bool HasCabin() {
        foreach (var thing in S.Things) if (thing.Kind == "hut") return true;
        return false;
    }

    private bool NoLivingNeighbors() {
        foreach (var player in S.Players) if (player != S.Actor && player.RespawnIn <= 0) return false;
        return true;
    }

    /// <summary>Advance normal world simulation to dawn while the local player is waiting to respawn.</summary>
    public void SkipMorning() {
        if (Forwarded("skip_morning") || !CanSkipMorning) return;
        float seconds = SecondsToDawn;
        asleep = true;
        try {
            while (seconds > 0) {
                float dt = Math.Min(1, seconds);
                Tick(dt, true);
                seconds -= dt;
            }
        } finally {
            asleep = false;
        }
        S.Wolves.Clear();
        SayToAll(S.Workers.Count > 0 ? "Dawn breaks. Your workers are walking back from Bramblewick." : "Dawn breaks.");
    }
}

public partial class Game {
    private PanelContainer? recoveryPrompt;
    private Label? recoveryReason;
    private Button? skipMorningButton;
    private bool? shownRecoveryAllowed;
    private bool recoveryDismissed;

    /// <summary>Refresh the reusable recovery overlay; its controls are created once, on the rare first check.</summary>
    private void RefreshRecoveryPrompt() {
        if (ui == null || world == null) return;
        if (recoveryPrompt == null) BuildRecoveryPrompt();
        bool eligible = world.Me.RespawnIn > 0 && !world.HasCabin();
        if (!eligible) recoveryDismissed = false;
        bool show = eligible && !recoveryDismissed;
        recoveryPrompt!.Visible = show;
        if (!show) { shownRecoveryAllowed = null; return; }
        bool allowed = world.CanSkipMorning;
        if (shownRecoveryAllowed == allowed) return;
        shownRecoveryAllowed = allowed;
        skipMorningButton!.Disabled = !allowed;
        recoveryReason!.Text = allowed
            ? "No cabin can shelter you. Skip to dawn, or wait for your normal respawn. Your belongings remain in the recovery box."
            : "No cabin can shelter you. Another player is still alive, so the night must continue for everyone.";
    }

    private void BuildRecoveryPrompt() {
        recoveryPrompt = new PanelContainer {
            Name = "recovery_prompt",
            Visible = false,
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = .5f,
            AnchorBottom = .5f,
            OffsetLeft = -245,
            OffsetRight = 245,
            OffsetTop = -105,
            OffsetBottom = 105,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        recoveryPrompt.AddThemeStyleboxOverride("panel", Style.Board);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 14); recoveryPrompt.AddChild(body);
        body.AddChild(Style.Heading("Skip to morning?", 24));
        recoveryReason = Style.Text("", 15, Style.Rim); recoveryReason.AutowrapMode = TextServer.AutowrapMode.WordSmart; body.AddChild(recoveryReason);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 10); body.AddChild(actions);
        skipMorningButton = Button("Skip to morning", () => world.SkipMorning());
        skipMorningButton.Name = "skip_morning";
        actions.AddChild(skipMorningButton);
        actions.AddChild(Button("Wait for respawn", () => { recoveryDismissed = true; recoveryPrompt!.Visible = false; }));
        ui.AddChild(recoveryPrompt);
    }
}
