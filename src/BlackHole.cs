using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Putting a crate inside a crate: a warning, then a black hole that swallows the screen from the crate outward, then game over.
/// The whole flow is styled like a starship console rather than the woodland interface.
/// </summary>
public partial class Game {
    private static readonly Color Neon = new("4fe3ff");
    private static readonly Color Alarm = new("ff4f7a");
    private Control anomaly = null!;
    private PanelContainer anomalyWarning = null!;
    private PanelContainer singularity = null!;
    private ColorRect collapse = null!;
    private Label anomalyReadout = null!;
    private Button reloadAfterCollapse = null!;
    private Thing? anomalyCrate;
    private bool pausedBeforeAnomaly;

    private const string CollapseShader = @"
shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform vec2 center = vec2(0.5);
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float aspect = 1.6;
void fragment() {
    vec2 d = SCREEN_UV - center;
    d.x *= aspect;
    float r = length(d);
    float p = progress;
    float horizon = 0.01 + pow(p, 4.0) * 1.7;
    // Space spirals and falls inward: sampling further out the closer it is to the hole draws everything toward it.
    float twist = p * 3.5 * exp(-r * 3.0);
    vec2 s = d * (1.0 + p * p * 2.2 / (r * 5.0 + 0.3));
    s = vec2(cos(twist) * s.x - sin(twist) * s.y, sin(twist) * s.x + cos(twist) * s.y);
    s.x /= aspect;
    vec2 uv = center + s;
    float split = 0.005 * p / (r + 0.12);
    vec3 col = vec3(texture(screen, uv + vec2(split, 0.0)).r, texture(screen, uv).g, texture(screen, uv - vec2(split, 0.0)).b);
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) col = vec3(0.0);
    // A thin accretion ring glows just outside the horizon.
    float ring = exp(-pow((r - horizon) * 28.0, 2.0)) * smoothstep(0.0, 0.15, p) * (1.0 - smoothstep(0.8, 0.97, p));
    col += ring * vec3(0.3, 0.85, 1.0) * 1.8;
    col *= smoothstep(horizon, horizon + 0.05 + 0.12 * (1.0 - p), r);
    col *= 1.0 - smoothstep(0.9, 1.0, p);
    COLOR = vec4(col, 1.0);
}";

    private void BuildAnomaly() {
        var layer = new CanvasLayer { Layer = 20 }; AddChild(layer);
        anomaly = new Control { Visible = false, Name = "anomaly" }; anomaly.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(anomaly);
        anomaly.Theme = ui.Theme;
        collapse = new ColorRect { Visible = false, Material = new ShaderMaterial { Shader = new Shader { Code = CollapseShader } }, MouseFilter = Control.MouseFilterEnum.Stop };
        collapse.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); anomaly.AddChild(collapse);

        anomalyWarning = ConsolePanel(Neon);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12); anomalyWarning.AddChild(column);
        column.AddChild(ConsoleLabel("⚠  SPACETIME INTEGRITY ALERT", 20, Alarm));
        column.AddChild(ConsoleLabel("Placing a crate inside a crate will collapse space time and create a black hole. Are you sure you want to proceed?", 16, Style.Rim));
        anomalyReadout = ConsoleLabel("", 12, Neon with { A = .7f }); column.AddChild(anomalyReadout);
        var answers = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; answers.AddThemeConstantOverride("separation", 24); column.AddChild(answers);
        var no = ConsoleButton("No", Neon, CancelAnomaly); no.Name = "anomaly_no"; answers.AddChild(no);
        var yes = ConsoleButton("Yes", Alarm, () => { if (anomalyCrate != null) Collapse(anomalyCrate); }); yes.Name = "anomaly_yes"; answers.AddChild(yes);
        anomaly.AddChild(anomalyWarning);

        singularity = ConsolePanel(Neon);
        var over = new VBoxContainer(); over.AddThemeConstantOverride("separation", 12); singularity.AddChild(over);
        over.AddChild(ConsoleLabel("SINGULARITY EVENT", 26, Neon));
        over.AddChild(ConsoleLabel("A crate folded into a crate. Space time collapsed around it and took Bramblewick, the woods and you with it.", 16, Style.Rim));
        over.AddChild(ConsoleLabel("SIGNAL LOST · NO TIMELINE TO RESUME", 12, Alarm));
        reloadAfterCollapse = ConsoleButton("Reload save", Neon, () => { EndAnomaly(); LoadSlot(LatestSlot()); }); reloadAfterCollapse.Name = "reload_after_collapse"; over.AddChild(reloadAfterCollapse);
        var menuButton = ConsoleButton("Main menu", Neon, () => { EndAnomaly(); ShowStart(); }); menuButton.Name = "menu_after_collapse"; over.AddChild(menuButton);
        anomaly.AddChild(singularity);
    }

    private PanelContainer ConsolePanel(Color edge) {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = Control.GrowDirection.Both; panel.GrowVertical = Control.GrowDirection.Both;
        var style = new StyleBoxFlat {
            BgColor = new Color(.02f, .04f, .08f, .94f),
            BorderColor = edge,
            ShadowColor = edge with { A = .35f },
            ShadowSize = 22,
            ContentMarginLeft = 28,
            ContentMarginRight = 28,
            ContentMarginTop = 22,
            ContentMarginBottom = 22
        };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(3);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static readonly SystemFont ConsoleFont = new() { FontNames = new[] { "DejaVu Sans Mono", "Liberation Mono", "monospace" } };

    private Label ConsoleLabel(string text, int size, Color color) {
        var label = Style.Wrapped(text, size, color); label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontOverride("font", ConsoleFont);
        return label;
    }

    private Button ConsoleButton(string text, Color edge, System.Action action) {
        var button = kit.Button(text.ToUpperInvariant(), action);
        button.CustomMinimumSize = new Vector2(150, 40); button.AddThemeFontOverride("font", ConsoleFont);
        button.AddThemeColorOverride("font_color", edge); button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", edge with { A = .35f });
        foreach (var (state, fill) in new[] { ("normal", .0f), ("hover", .25f), ("pressed", .45f), ("disabled", .0f) }) {
            var style = new StyleBoxFlat { BgColor = edge with { A = fill }, BorderColor = edge, ContentMarginLeft = 14, ContentMarginRight = 14 };
            style.SetBorderWidthAll(2); style.SetCornerRadiusAll(2);
            button.AddThemeStyleboxOverride(state, style);
        }
        return button;
    }

    /// <summary>Stops a crate from going into a placed crate and asks first; true when the move was intercepted.</summary>
    private bool CrateIntoCrate(Thing? target, Inventory to, string item) {
        if (target?.Kind != ThingKind.Crate || to != target.Inventory || !World.IsCrate(item)) return false;
        anomalyCrate = target; pausedBeforeAnomaly = state.Paused; state.Paused = true;
        anomalyReadout.Text = $"CONTAINER {target.Id:0000} · NESTING DEPTH 2 · LOCAL METRIC STABILITY 0.03%";
        anomaly.Visible = true; anomalyWarning.Visible = true; singularity.Visible = false; collapse.Visible = false;
        // The alert border breathes while it waits for an answer.
        var pulse = anomalyWarning.CreateTween().SetLoops(); anomalyWarning.SetMeta("tween", pulse);
        pulse.TweenProperty(anomalyWarning, "self_modulate", new Color(1, .75f, .85f), .6f); pulse.TweenProperty(anomalyWarning, "self_modulate", Colors.White, .6f);
        return true;
    }

    private void CancelAnomaly() {
        StopPulse(); anomaly.Visible = false; anomalyCrate = null; state.Paused = pausedBeforeAnomaly;
    }

    private void StopPulse() {
        if (anomalyWarning.HasMeta("tween") && anomalyWarning.GetMeta("tween").As<Tween>() is Tween pulse && pulse.IsValid()) pulse.Kill();
        anomalyWarning.SelfModulate = Colors.White;
    }

    /// <summary>The black hole opens at the crate, pulls the picture into itself until only black is left, then the game is over.</summary>
    private Tween? collapseTween;

    private void Collapse(Thing crate) {
        StopPulse(); anomalyWarning.Visible = false; ClosePanel(); state.Paused = true;
        var size = GetViewport().GetVisibleRect().Size;
        var material = (ShaderMaterial)collapse.Material;
        material.SetShaderParameter("center", view.Screen(crate.Pos, .3f) / size);
        material.SetShaderParameter("aspect", size.X / size.Y);
        material.SetShaderParameter("progress", 0f);
        collapse.Visible = true;
        var tween = collapseTween = collapse.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)), 0f, 1f, 5f);
        tween.TweenCallback(Callable.From(() => {
            reloadAfterCollapse.Disabled = LatestSlot() < 0;
            singularity.Modulate = new Color(1, 1, 1, 0); singularity.Visible = true;
        }));
        tween.TweenProperty(singularity, "modulate:a", 1f, 1.2f);
    }

    private void EndAnomaly() {
        collapseTween?.Kill();
        anomaly.Visible = false; collapse.Visible = false; singularity.Visible = false; anomalyCrate = null;
    }

    /// <summary>The save the player was using, else the most recent one, or -1 when there is none.</summary>
    private int LatestSlot() => slot > 0 && SlotUsed(slot) ? slot : Enumerable.Range(0, SlotCount + 1).Where(SlotUsed).OrderByDescending(SavedAt).DefaultIfEmpty(-1).First();
}
