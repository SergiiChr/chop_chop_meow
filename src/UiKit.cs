using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>What several parts of the interface read or change; each field has this one home.</summary>
public sealed class UiState {
    /// <summary>A menu or overlay holds the player; only a game nobody else is in stops the world too.</summary>
    public bool Paused;
    /// <summary>A cabin the player is walking to after clicking it; they step inside on arrival.</summary>
    public Thing? Entering;
}

/// <summary>
/// What the interface's parts work with: the world, how a player action runs, and the buttons that run one.
/// Every button runs its action through <see cref="Act"/> and then refreshes the screen.
/// </summary>
public sealed class UiKit(World world, Sounds sounds, Action<string> notify, Action refresh) {
    public World World { get; } = world;
    public Sounds Sounds { get; } = sounds;
    public UiState State { get; } = new();

    /// <summary>Tells the player something through the game's message stack.</summary>
    public void Notify(string message) => notify(message);

    /// <summary>Brings everything on screen up to date with the world.</summary>
    public void Refresh() => refresh();

    /// <summary>Runs a player action and gives it a generic sound when it made none of its own: that of the item whose backpack count changed most.</summary>
    public void Act(Action action) {
        var state = World.S; int played = Sounds.Played;
        var before = new Dictionary<string, int>(state.Backpack.Items);
        action();
        // Starting or loading a game replaces the whole state; that is not an item moving.
        if (World.S != state || Sounds.Played != played) return;
        var moved = state.Backpack.Items.Keys.Union(before.Keys).OrderByDescending(id => Math.Abs(state.Backpack[id] - before.GetValueOrDefault(id))).FirstOrDefault();
        if (moved != null && state.Backpack[moved] != before.GetValueOrDefault(moved)) Sounds.Play(Sounds.ForItem(moved));
    }

    public Button Button(string text, Action action, bool disabled = false) {
        var b = new Button { Text = text, Disabled = disabled, MouseDefaultCursorShape = Control.CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 34), FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () => { Act(action); Refresh(); }; return b;
    }

    /// <summary>A wide rounded button for a page's main actions.</summary>
    public Button BigButton(string text, Color color, Action action) {
        var button = Button(text, action); button.CustomMinimumSize = new Vector2(0, 56); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var look = Style.Pill(color); Style.Skin(button, look.Item1, look.Item2);
        button.AddThemeFontSizeOverride("font_size", 17);
        return button;
    }

    /// <summary>A round button in a panel's header, with a line icon.</summary>
    public Button HeaderButton(string glyph, string tooltip, Action action, (StyleBoxFlat Normal, StyleBoxFlat Hover) look, string name = "") {
        var button = Button("", action); button.TooltipText = tooltip; button.CustomMinimumSize = new Vector2(38, 38); button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        if (name != "") button.Name = name;
        Style.Skin(button, look.Normal, look.Hover);
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/line_{glyph}.svg"); button.ExpandIcon = true; button.IconAlignment = HorizontalAlignment.Center;
        button.AddThemeConstantOverride("icon_max_width", 18);
        button.AddThemeColorOverride("icon_normal_color", look.Normal == Style.FilterRound.Normal ? Style.Ink : Style.Rim);
        button.AddThemeColorOverride("icon_hover_color", look.Normal == Style.FilterRound.Normal ? Style.Ink : Style.Rim);
        return button;
    }
}

/// <summary>
/// Text rebuilt on every refresh into a reused buffer and handed to Godot only when it differs from what is shown,
/// so an open panel allocates nothing while what it shows stays the same.
/// </summary>
public sealed class LiveText(Action<string> show) {
    private readonly StringBuilder text = new();
    private string? shown;
    public StringBuilder Clear() => text.Clear();
    public void Show() {
        if (shown != null && text.Equals(shown.AsSpan())) return;
        shown = text.ToString(); show(shown);
    }

    /// <summary>Text from a writer that panels also use live, for one-off labels and messages.</summary>
    public static string Of(Action<StringBuilder> write) {
        var text = new StringBuilder(); write(text); return text.ToString();
    }
}
