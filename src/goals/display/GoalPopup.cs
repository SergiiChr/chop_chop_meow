using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Pops up at the top of the screen when a goal appears in the Goal Log during play, and again when one is completed,
/// centred in the strip of screen the game lays it over.
/// A trophy badge pops in, a bar opens sideways out of it with the goal's title, stays a while, then closes back into
/// the badge, which shrinks away.
/// A goal revealed already done pops up only as completed.
/// Goals a new, loaded or joined game already has stay quiet, and goals that change together pop up one after another.
/// Clicking it opens the pause menu on that goal's details.
/// </summary>
public partial class GoalPopup : Control {
    public const string NewHeading = "New goal", CompletedHeading = "Goal completed";
    public const string NewSound = "goal_new", CompletedSound = "goal_completed";
    // Seconds of each step: the badge pops in, the bar opens, and at the end the bar closes and the badge shrinks away.
    private const float PopSeconds = .3f, OpenSeconds = .4f, CloseSeconds = .35f, ShrinkSeconds = .25f;
    /// <summary>Seconds one popup stays on screen, from the badge popping in to its shrinking away.</summary>
    public const float ShowSeconds = 5;
    /// <summary>The popup's height, its badge's size.</summary>
    public const float Height = BadgeSize;
    private const float BadgeSize = 64, BarHeight = 54, TextGap = 12, EndRoom = 28, MinWidth = 320;
    private const int HeadingSize = 14, TitleSize = 21, LineOverlap = 8;
    private static readonly Texture2D Trophy = GD.Load<Texture2D>("res://assets/ui/line_trophy.svg");
    private static readonly StyleBoxFlat Bar = Style.Flat(Style.Line, (int)(BarHeight / 2), 0, Style.Rim, 3);
    private static readonly StyleBoxFlat NewBadge = Style.Flat(Style.Mustard, (int)(BadgeSize / 2), 0, Style.Rim, 3);
    private static readonly StyleBoxFlat CompletedBadge = Style.Flat(Style.Sage, (int)(BadgeSize / 2), 0, Style.Rim, 3);

    private readonly World world;
    private readonly Sounds sounds;
    private readonly Action<Goal> open;
    // Which goals have had their new and completed popups, or had that state already when the game was taken up.
    private readonly bool[] announcedNew = new bool[Goals.Entries.Length], announcedCompleted = new bool[Goals.Entries.Length];
    private readonly Queue<(Goal Goal, GoalStatus Status)> waiting = new(2 * Goals.Entries.Length);
    private readonly Control clip;
    private readonly Label heading, title;
    private float clock, openWidth, barWidth, badgeScale;

    // The goal on screen, and whether it popped up as new or completed.
    private (Goal Goal, GoalStatus Status)? showing;
    public string Heading => heading.Text;
    public string Title => title.Text;

    /// <summary><paramref name="open"/> opens the pause menu on a goal's details.</summary>
    public GoalPopup(World world, Sounds sounds, Action<Goal> open) {
        this.world = world; this.sounds = sounds; this.open = open;
        Name = "goal_popup"; Visible = false; AnchorRight = 1;
        clip = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore }; AddChild(clip);
        heading = Style.Text("", HeadingSize, Style.Beige, Style.Head); clip.AddChild(heading);
        title = Style.Text("", TitleSize, Style.Rim, Style.Head); clip.AddChild(title);
        // The two lines sit as one block in the bar's middle, with as much room above as below; the title rides up into
        // the empty room under the heading's letters, which leaves more room between the letters and the bar's edges.
        float lineStep = heading.GetCombinedMinimumSize().Y - LineOverlap, top = (BadgeSize - lineStep - title.GetCombinedMinimumSize().Y) / 2;
        heading.Position = new Vector2(TextGap, top); title.Position = new Vector2(TextGap, top + lineStep);
        TakeUp();
    }

    /// <summary>
    /// Notes the goals the game in hand already shows and drops any popup, so only what happens from now on pops up:
    /// the game calls it once the window has joined a game, new, loaded or another player's.
    /// </summary>
    public void TakeUp() {
        var goals = Goals.Entries;
        for (int i = 0; i < goals.Length; i++) {
            var status = world.Goals.Status(goals[i].Id);
            announcedNew[i] = status != GoalStatus.Hidden;
            announcedCompleted[i] = status == GoalStatus.Completed;
        }
        waiting.Clear(); showing = null; Visible = false;
    }

    /// <summary>Lines up a popup for each goal that appeared or was completed since the last refresh; one revealed already done pops up only as completed.</summary>
    public void Refresh() {
        var goals = Goals.Entries;
        for (int i = 0; i < goals.Length; i++) {
            if (announcedCompleted[i]) continue;
            var status = world.Goals.Status(goals[i].Id);
            if (status == GoalStatus.Hidden || status == GoalStatus.Active && announcedNew[i]) continue;
            announcedNew[i] = true; announcedCompleted[i] = status == GoalStatus.Completed;
            waiting.Enqueue((goals[i], status));
        }
    }

    public override void _Process(double delta) {
        if (showing == null) {
            if (!waiting.TryDequeue(out var next)) return;
            Start(next);
        }
        clock += (float)delta;
        if (clock >= ShowSeconds) { showing = null; Visible = false; return; }
        Animate();
    }

    private void Start((Goal Goal, GoalStatus Status) next) {
        showing = next; clock = 0;
        bool completed = next.Status == GoalStatus.Completed;
        heading.Text = completed ? CompletedHeading : NewHeading;
        title.Text = next.Goal.Title;
        float text = Math.Max(Style.Head.GetStringSize(heading.Text, fontSize: HeadingSize).X, Style.Head.GetStringSize(title.Text, fontSize: TitleSize).X);
        openWidth = Math.Max(MinWidth, BadgeSize + TextGap + text + EndRoom);
        sounds.Play(completed ? CompletedSound : NewSound);
        Animate();
        Visible = true;
    }

    /// <summary>Sets the badge's size, the bar's width and the text's fade for the time since this popup started.</summary>
    private void Animate() {
        float closing = ShowSeconds - ShrinkSeconds - CloseSeconds;
        badgeScale = clock < PopSeconds ? BackOut(clock / PopSeconds)
            : clock > ShowSeconds - ShrinkSeconds ? 1 - Smooth((clock - (ShowSeconds - ShrinkSeconds)) / ShrinkSeconds) : 1;
        float opened = clock < PopSeconds ? 0
            : clock < PopSeconds + OpenSeconds ? Smooth((clock - PopSeconds) / OpenSeconds)
            : clock < closing ? 1 : 1 - Smooth((clock - closing) / CloseSeconds);
        barWidth = BadgeSize + (openWidth - BadgeSize) * opened;
        clip.Position = new Vector2(BarLeft + BadgeSize, 0);
        clip.Size = new Vector2(Math.Max(0, barWidth - BadgeSize - BarHeight / 2), BadgeSize);
        var fade = new Color(1, 1, 1, Math.Clamp((opened - .6f) / .4f, 0, 1));
        heading.Modulate = title.Modulate = fade;
        QueueRedraw();
    }

    public override void _Draw() {
        if (showing is not { } shown) return;
        if (barWidth > BadgeSize) DrawStyleBox(Bar, new Rect2(BarLeft, (BadgeSize - BarHeight) / 2, barWidth, BarHeight));
        var centre = new Vector2(BarLeft + BadgeSize / 2, BadgeSize / 2);
        DrawSetTransform(centre, 0, new Vector2(badgeScale, badgeScale));
        var badge = shown.Status == GoalStatus.Completed ? CompletedBadge : NewBadge;
        DrawStyleBox(badge, new Rect2(-BadgeSize / 2, -BadgeSize / 2, BadgeSize, BadgeSize));
        const float glyph = BadgeSize * .56f;
        DrawTextureRect(Trophy, new Rect2(-glyph / 2, -glyph / 2, glyph, glyph), false, Style.Line);
        DrawSetTransform(Vector2.Zero);
    }

    // The bar's left end, where the badge sits: the bar opens out both ways from the middle of the popup's strip.
    private float BarLeft => (Size.X - barWidth) / 2;

    /// <summary>Only the badge and the open bar take clicks; the rest of the popup's strip passes them to the game.</summary>
    public override bool _HasPoint(Vector2 point) => showing != null && new Rect2(BarLeft, 0, barWidth, BadgeSize).HasPoint(point);

    public override void _GuiInput(InputEvent @event) {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } || showing is not { } shown) return;
        AcceptEvent();
        open(shown.Goal);
    }

    private static float Smooth(float t) => t * t * (3 - 2 * t);
    // Grows past full size and settles back, the badge's pop.
    private static float BackOut(float t) { const float c = 1.7f; float u = t - 1; return 1 + (c + 1) * u * u * u + c * u * u; }
}
