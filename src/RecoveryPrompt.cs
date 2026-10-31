using Godot;

namespace ChopChopMeow;

/// <summary>Offers a downed player without a cabin to wake in the skip to morning, or to wait for the respawn.</summary>
public partial class RecoveryPrompt : PanelContainer {
    private readonly World world;
    private readonly Label reason;
    private readonly Button skip;
    private bool? shownAllowed;
    private bool dismissed;

    public RecoveryPrompt(UiKit kit) {
        world = kit.World;
        Name = "recovery_prompt"; Visible = false;
        AnchorLeft = .5f; AnchorRight = .5f; AnchorTop = .5f; AnchorBottom = .5f;
        OffsetLeft = -245; OffsetRight = 245; OffsetTop = -105; OffsetBottom = 105;
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        AddThemeStyleboxOverride("panel", Style.Board);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 14); AddChild(body);
        body.AddChild(Style.Heading("Skip to morning?", 24));
        reason = Style.Text("", 15, Style.Rim); reason.AutowrapMode = TextServer.AutowrapMode.WordSmart; body.AddChild(reason);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 10); body.AddChild(actions);
        skip = kit.Button("Skip to morning", () => world.SkipMorning()); skip.Name = "skip_morning";
        actions.AddChild(skip);
        actions.AddChild(kit.Button("Wait for respawn", () => { dismissed = true; Visible = false; }));
        world.Replaced += Refresh;
    }

    public bool CanSkip => !skip.Disabled;

    /// <summary>Shows or hides the prompt to match the player's state, unless the player chose to wait.</summary>
    public void Refresh() {
        bool eligible = world.Me.RespawnIn > 0 && !world.HasCabin();
        if (!eligible) dismissed = false;
        bool show = eligible && !dismissed;
        Visible = show;
        if (!show) { shownAllowed = null; return; }
        bool allowed = world.CanSkipMorning;
        if (shownAllowed == allowed) return;
        shownAllowed = allowed;
        skip.Disabled = !allowed;
        reason.Text = allowed
            ? "No cabin can shelter you. Skip to dawn, or wait for your normal respawn. Your belongings remain in the recovery box."
            : "No cabin can shelter you. Another player is still alive, so the night must continue for everyone.";
    }
}
