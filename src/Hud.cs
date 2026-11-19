using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The day dial and clock on a plate at the top of the right-hand corner column; the side panel opens below it.</summary>
public partial class ClockPlate : PanelContainer {
    private readonly World world;
    private readonly DayDial dial;
    private readonly Label time;
    // What the clock shows, so its text is built only when it changes: building it allocates.
    private (int Day, int Minute, bool Night) shown = (-1, -1, false);

    public ClockPlate(World world) {
        this.world = world;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", Style.Plate);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; column.AddThemeConstantOverride("separation", 6); AddChild(column);
        dial = new DayDial { Arc = () => world.SkyArc, Night = () => world.MoonUp, CustomMinimumSize = new Vector2(0, 62), MouseFilter = MouseFilterEnum.Ignore }; column.AddChild(dial);
        time = Style.Heading("", 16); time.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(time);
    }

    public void Refresh() {
        var now = (world.Day, (int)(world.DayPhase * GameData.MinutesPerDay), world.Night);
        if (now == shown) return;
        shown = now;
        int hour = (int)world.Hour;
        time.Text = $"Day {world.Day} · {hour}:{(int)(world.DayPhase * GameData.MinutesPerDay) % 60:00}";
        dial.QueueRedraw();
    }
}

/// <summary>The right-hand corner column: the clock plate with the frame rate under it.</summary>
public partial class Corner : VBoxContainer {
    public readonly ClockPlate Clock;
    public readonly FrameRate FrameRate = new();

    public Corner(World world) {
        AnchorLeft = 1; AnchorRight = 1; OffsetLeft = -200; OffsetRight = -20; OffsetTop = 18; OffsetBottom = 18; MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 4);
        Clock = new ClockPlate(world); AddChild(Clock); AddChild(FrameRate);
    }

    public void Refresh() { Clock.Refresh(); FrameRate.Refresh(); }
}

/// <summary>The frame rate under the clock, built only when the whole number changes: building it allocates.</summary>
public partial class FrameRate : Label {
    private int shown = -1;

    public FrameRate() {
        Name = "frame_rate";
        AddThemeFontSizeOverride("font_size", 15); Style.Outlined(this);
        HorizontalAlignment = HorizontalAlignment.Right; MouseFilter = MouseFilterEnum.Ignore; Visible = Settings.FrameRate;
    }

    public void Refresh() {
        if (!Visible) return;
        int now = (int)Engine.GetFramesPerSecond();
        if (now == shown) return;
        shown = now;
        Text = $"{now} fps";
    }
}

/// <summary>Says what the next click in the world does while a placement or a work order is under way.</summary>
public partial class ModeLabel : Label {
    private readonly World world;
    private readonly PlayerControl player;
    private ClickMode shown = new ClickMode.Normal();

    public ModeLabel(UiKit kit, PlayerControl player) {
        world = kit.World; this.player = player;
        AddThemeFontSizeOverride("font_size", 17); Style.Outlined(this);
        AnchorLeft = .5f; AnchorRight = .5f; OffsetTop = 18; GrowHorizontal = GrowDirection.Both; HorizontalAlignment = HorizontalAlignment.Center;
        AutowrapMode = TextServer.AutowrapMode.Off; MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Refresh() {
        if (player.Mode == shown) return;
        shown = player.Mode;
        Text = shown is ClickMode.Assign ? "WORK ORDER · Click a resource to assign this villager. Esc cancels." : shown is ClickMode.Remove ? "REMOVE · Left click a building or gravel path to take it down. Esc or right click ends." : shown.Placing is ThingKind kind ? $"PLACE {world.Data.Name(kind).ToUpperInvariant()} · Left click ground. Esc cancels." : "";
    }
}

/// <summary>The respawn countdown, under "You collapsed" for a player no wolf pinned.</summary>
public partial class DeathLabel : Label {
    private readonly World world;
    private (bool Dead, bool Mauled, int Seconds) shown;

    public DeathLabel(World world) {
        this.world = world;
        AddThemeFontSizeOverride("font_size", 26); Style.Outlined(this);
        AutowrapMode = TextServer.AutowrapMode.Off; SetAnchorsAndOffsetsPreset(LayoutPreset.Center); GrowHorizontal = GrowDirection.Both;
        // Raised above the centred player so a downed body stays visible under the message.
        OffsetTop -= 190; OffsetBottom -= 190;
        HorizontalAlignment = HorizontalAlignment.Center; MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Refresh() {
        var now = (world.Dead, world.Mauled, Seconds: (int)MathF.Ceiling(world.RespawnIn));
        if (now == shown) return;
        shown = now;
        Text = world.Dead ? $"{(world.Mauled ? "" : "You collapsed\n")}Respawning in {now.Seconds}s" : "";
    }
}
