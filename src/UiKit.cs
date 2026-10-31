using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>What ends or replaces the game in progress, for the menus and screens that offer it.</summary>
public interface IGameFlow {
    /// <summary>Starts a new game.</summary>
    void Restart();
    void Quit();
    /// <summary>Ends a guest's game after the line closes and shows the start menu.</summary>
    void Leave();
    void LoadSlot(int slot);
    void ShowStart();
}

/// <summary>
/// What the interface's parts work with: the world, how a player action runs, and the buttons that run one.
/// Every button runs its action through <see cref="Act"/> and then refreshes the screen.
/// </summary>
public sealed class UiKit(World world, Sounds sounds, Action<string> notify, Action refresh) {
    public World World { get; } = world;
    public Sounds Sounds { get; } = sounds;
    private readonly List<Func<bool>> holds = new();

    /// <summary>A menu or overlay holds the player; only a game nobody else is in stops the world too.</summary>
    public bool Paused {
        get {
            foreach (var open in holds) if (open()) return true;
            return false;
        }
    }

    /// <summary>Adds what pauses the game while it is open, so no part writes or restores another's pause.</summary>
    public void HoldWhile(Func<bool> open) => holds.Add(open);

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

    /// <summary>Runs a player action, then brings the screen up to date.</summary>
    public void Run(Action action) { Act(action); Refresh(); }

    /// <summary>Runs a player action that gives a value back, such as what a drag left over, then brings the screen up to date.</summary>
    public T Run<T>(Func<T> action) {
        T result = default!;
        Run(() => { result = action(); });
        return result;
    }

    public Button Button(string text, Action action, bool disabled = false) {
        var b = new Button { Text = text, Disabled = disabled, MouseDefaultCursorShape = Control.CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 34), FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () => Run(action); return b;
    }

    /// <summary>A wide rounded button for a page's main actions.</summary>
    public Button BigButton(string text, Color color, Action action) {
        var button = Button(text, action); button.CustomMinimumSize = new Vector2(0, 56); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Style.SkinPill(button, color);
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
/// What keeps an open page up to date: every update runs on each refresh, and a watched value is drawn only when it changes,
/// so an open panel allocates nothing while what it shows stays the same.
/// </summary>
public sealed class LiveUpdates {
    private readonly List<Action> updates = new();
    // Counts clears, so a run notices when an update replaced the page under it.
    private int clears;

    public void Add(Action update) => updates.Add(update);
    public void Clear() { updates.Clear(); clears++; }

    /// <summary>Runs the updates; once one opens another page, the old page's remaining updates are dropped.</summary>
    public void Run() {
        int run = clears;
        for (int i = 0; i < updates.Count && run == clears; i++) updates[i]();
    }

    /// <summary>Calls <paramref name="draw"/> on the first refresh and whenever <paramref name="read"/> gives something new.</summary>
    public void Watch<T>(Func<T> read, Action<T> draw) {
        bool first = true;
        T shown = default!;
        updates.Add(() => {
            T now = read();
            if (!first && EqualityComparer<T>.Default.Equals(now, shown)) return;
            first = false; shown = now; draw(now);
        });
    }

    /// <summary>Keeps a label's text to what <paramref name="write"/> writes on each refresh.</summary>
    public void Bind(Label label, Action<StringBuilder> write) => Bind(shown => label.Text = shown, write);

    /// <summary>Rebuilds text on each refresh and hands it to <paramref name="show"/> when it changes.</summary>
    public void Bind(Action<string> show, Action<StringBuilder> write) {
        var text = new LiveText(show);
        updates.Add(() => { write(text.Clear()); text.Show(); });
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
