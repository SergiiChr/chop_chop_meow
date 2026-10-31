using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The day dial and clock on a plate in the top right corner; the side panel opens below it.</summary>
public partial class ClockPlate : PanelContainer {
    private readonly World world;
    private readonly DayDial dial;
    private readonly Label time;
    // What the clock shows, so its text is built only when it changes: building it allocates.
    private (int Day, int Minute, bool Night) shown = (-1, -1, false);

    /// <summary>What the clock reads.</summary>
    public string Text => time.Text;

    public ClockPlate(World world) {
        this.world = world;
        AnchorLeft = 1; AnchorRight = 1; OffsetLeft = -200; OffsetRight = -20; OffsetTop = 18; OffsetBottom = 18; MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", Style.Plate);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; column.AddThemeConstantOverride("separation", 6); AddChild(column);
        dial = new DayDial { Arc = () => world.SkyArc, Night = () => world.MoonUp, CustomMinimumSize = new Vector2(0, 62), MouseFilter = MouseFilterEnum.Ignore }; column.AddChild(dial);
        time = Style.Heading("", 16); time.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(time);
    }

    public void Refresh() {
        var now = (world.Day, (int)(world.DayPhase * 1440), world.Night);
        if (now == shown) return;
        shown = now;
        int hour = (int)world.Hour;
        time.Text = $"Day {world.Day} · {hour}:{(int)(world.DayPhase * 1440) % 60:00}";
        dial.QueueRedraw();
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
        Text = shown is ClickMode.Assign ? "WORK ORDER · Click a resource to assign this villager. Esc cancels." : shown.Placing is ThingKind kind ? $"PLACE {world.Data.Name(kind).ToUpperInvariant()} · Left click ground. Esc cancels." : "";
    }
}

/// <summary>The full-bottle warning, shown for a few seconds at the left of the screen, above the backpack drawer when it is open.</summary>
public partial class WaterWarning : Label {
    private const float ShowSeconds = 7;
    private readonly BackpackDrawer bag;
    private readonly Control screen;
    private float clock;

    public WaterWarning(BackpackDrawer bag, Control screen) {
        this.bag = bag; this.screen = screen;
        Name = "water_warning";
        AddThemeFontSizeOverride("font_size", 17); Style.Outlined(this);
        AnchorTop = 1; AnchorBottom = 1; OffsetLeft = 20; OffsetRight = 650; OffsetTop = -250; OffsetBottom = -220;
        AutowrapMode = TextServer.AutowrapMode.Off; MouseFilter = MouseFilterEnum.Ignore;
    }

    // Here, not in the constructor: Godot turns processing on just before _Ready.
    public override void _Ready() => SetProcess(false);

    public void Warn(string message) { Text = message; clock = ShowSeconds; SetProcess(true); }

    public override void _Process(double delta) {
        Position = new Vector2(20, bag.Visible ? bag.Position.Y - 36 : screen.Size.Y - 148);
        clock -= Math.Min((float)delta, .1f);
        if (clock <= 0) { Text = ""; SetProcess(false); }
    }
}

/// <summary>The respawn countdown, or what is keeping the player down.</summary>
public partial class DeathLabel : Label {
    private readonly World world;
    private (bool Dead, bool Mauled, bool Pack, int Seconds) shown;

    public DeathLabel(World world) {
        this.world = world;
        AddThemeFontSizeOverride("font_size", 26); Style.Outlined(this);
        AutowrapMode = TextServer.AutowrapMode.Off; SetAnchorsAndOffsetsPreset(LayoutPreset.Center); GrowHorizontal = GrowDirection.Both;
        // Raised above the centred player so a downed body stays visible under the message.
        OffsetTop -= 190; OffsetBottom -= 190;
        HorizontalAlignment = HorizontalAlignment.Center; MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Refresh() {
        int feeding = 0;
        foreach (var w in world.Wolves) if (w.Feeding) feeding++;
        var now = (world.Dead, world.Mauled, feeding > 1, (int)MathF.Ceiling(world.RespawnIn));
        if (now == shown) return;
        shown = now;
        Text = world.Dead ? $"{(world.Mauled ? feeding > 1 ? "The pack is feeding" : "A wolf is feeding" : "You collapsed")}\nRespawning in {MathF.Ceiling(world.RespawnIn):0}s" : "";
    }
}
