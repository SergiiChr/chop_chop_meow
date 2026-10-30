using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

public partial class Game : Node {
    private World world = null!;
    private WorldView view = null!;
    private Sounds sounds = null!;
    private Control ui = null!;
    private PanelContainer panel = null!;
    private VBoxContainer content = null!;
    private Label stats = null!;
    private MessageStack messages = null!;
    private PanelContainer needsPlate = null!;
    private Label waterWarning = null!;
    private float waterWarningClock;
    private Label modeLabel = null!;
    private DayDial dayDial = null!;
    private PanelContainer bag = null!;
    private VBoxContainer bagContent = null!;
    private Label bagFill = null!;
    private ScrollContainer scroll = null!;
    private HBoxContainer heading = null!;
    /// <summary>The board left of the panel that holds the price sign or the filter window.</summary>
    private PanelContainer side = null!;
    private TextureRect headingIcon = null!;
    private Label headingTitle = null!;
    private Label deathLabel = null!;
    private readonly List<Action> live = new();
    private string page = "welcome";
    private int selected;
    private int workerId;
    private int assigningWorker;
    private Thing? gathering;
    /// <summary>A cabin the player is walking to after clicking it; they step inside on arrival.</summary>
    private Thing? entering;
    /// <summary>A crate held under the right mouse button; it is lifted once held for <see cref="LiftSeconds"/>.</summary>
    private Thing? lifting;
    private float liftClock;
    private const float LiftSeconds = .6f;
    /// <summary>The backpack crate being placed while the build mode is "crate".</summary>
    private string placingCrate = "";
    private UiKit kit = null!;
    private EndingScreen ending = null!;
    private BlackHole blackHole = null!;
    private UiState state = null!;
    private float uiClock;
    private float autosave;
    private const float AutosaveSeconds = 300;
    private Net net = null!;
    /// <summary>Health as last shown, so a guest flashes the damage its updates bring.</summary>
    private float shownHealth;
    private float runTime;
    private bool capturing;
    private bool quitting;
    /// <summary>Left column of the side panel; only the signpost's price sheet uses it.</summary>
    private VBoxContainer sheet = null!;
    /// <summary>Left edge of the side panel and the clock above it, measured from the right of the screen.</summary>
    private const int PanelLeft = -476;
    /// <summary>Top of the side panel, below the clock.</summary>
    private const int PanelTop = 156;
    /// <summary>Bottom of the backpack drawer, measured up from the bottom of the screen, just above the hotbar.</summary>
    private const int BagBottom = 112;
    public override void _Ready() {
        try {
            var data = GameData.Load(); world = launchArgs.Contains("--performance-run") ? PerformanceFixture.Create(data) : new World(data);
            if (launchArgs.Contains("--self-test")) { SimulationChecks.Run(data); GetTree().Quit(); return; }
            if (LaunchValue("--render-icons") is string icons) { AddChild(new IconRender { Data = data, Folder = icons }); return; }
            world.Notify = Notify;
            sounds = new Sounds { World = world, MusicVolume = SavedMusicVolume() }; AddChild(sounds);
            // Closing the window goes through Quit, which lets playing sounds stop first.
            GetTree().AutoAcceptQuit = false;
            view = new WorldView { World = world, Sounds = sounds, EnvironmentReview = launchArgs.Contains("--environment-review") }; AddChild(view);
            kit = new UiKit(world, sounds, Notify, RefreshLive); state = kit.State;
            net = new Net { Name = "Net", World = world, FilePrefix = FilePrefix }; AddChild(net);
            world.Sound = (name, at) => { PlayWorldSound(name, at); net.Echo(name, at); };
            world.PlayerActed += (action, at) => net.Echo(world.S.Actor.Id, action, at);
            ConnectNet();
            sounds.OffScreen = view.OffScreen;
            var layer = new CanvasLayer(); AddChild(layer);
            ui = new Control { Name = "Interface" }; ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(ui); ui.MouseFilter = Control.MouseFilterEnum.Ignore;
            SetTheme(); BuildHud();
            GetViewport().UseTaa = SavedTaa();
            if (launchArgs.Contains("--showcase")) Showcase();
            else if (!launchArgs.Contains("--animation-review")) ShowStart();
            if (!launchArgs.Any(a => a is "--showcase" or "--ui-test" or "--net-check" or "--animation-review" or "--performance-run")) { ApplyResolution(SavedResolution()); Engine.MaxFps = SavedFrameLimit(); }
            StartPerformanceRun();
            if (launchArgs.Contains("--ui-test")) RunUIChecks();
            if (launchArgs.Contains("--net-check")) RunNetCheck(LaunchValue("--net-check") == "host");
            if (launchArgs.Contains("--animation-review")) {
                ClosePanel(); ui.Visible = false; view.AnimationReview = true;
                if (LaunchValue("--animation-capture") is string captures) CaptureAnimations(captures);
            }
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private void SetTheme() {
        var theme = new Theme { DefaultFont = Style.Body };
        theme.DefaultFontSize = 16;
        theme.SetStylebox("panel", "PanelContainer", Style.Board);
        theme.SetStylebox("normal", "Button", Style.ButtonNormal);
        theme.SetStylebox("hover", "Button", Style.ButtonHover);
        theme.SetStylebox("pressed", "Button", Style.ButtonPressed);
        theme.SetStylebox("disabled", "Button", Style.ButtonDisabled);
        theme.SetStylebox("hover_pressed", "Button", Style.ButtonPressed);
        theme.SetStylebox("focus", "Button", Style.None);
        theme.SetFont("font", "Button", Style.Head);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) theme.SetColor(state, "Button", Style.Rim);
        theme.SetColor("font_disabled_color", "Button", new Color(Style.Rim, .45f));
        theme.SetColor("font_color", "Label", Style.Rim);
        theme.SetColor("font_color", "CheckBox", Style.Rim);
        theme.SetStylebox("normal", "CheckBox", Style.None); theme.SetStylebox("hover", "CheckBox", Style.None); theme.SetStylebox("pressed", "CheckBox", Style.None); theme.SetStylebox("hover_pressed", "CheckBox", Style.None);
        theme.SetConstant("separation", "VBoxContainer", 16);
        theme.SetConstant("separation", "HBoxContainer", 10);
        theme.SetStylebox("normal", "OptionButton", Style.ButtonNormal);
        theme.SetStylebox("background", "ProgressBar", Style.BarBack);
        theme.SetStylebox("fill", "ProgressBar", Style.BarFill);
        // Tooltips read as solid popups with a bright edge rather than a translucent wash over the scene.
        theme.SetStylebox("panel", "TooltipPanel", Style.Tooltip);
        theme.SetColor("font_color", "TooltipLabel", Style.Rim);
        theme.SetFont("font", "TooltipLabel", Style.Bold);
        ui.Theme = theme;
    }
    private void PlayWorldSound(string name, Vector2 at) => sounds.Play(name, at, name == "wolf_growl_distance" ? 14 : Sounds.Reach);
    private HBoxContainer Row() { var r = new HBoxContainer(); content.AddChild(r); return r; }
    private void AddLabel(string text, int size = 15, Color? color = null) => content.AddChild(Style.Wrapped(text, size, color));
    private void AddButton(string text, Action action, bool disabled = false) => content.AddChild(kit.Button(text, action, disabled));
    /// <summary>A label whose text <paramref name="write"/> rebuilds on every refresh.</summary>
    private void Dynamic(Action<StringBuilder> write, int size = 15, Color? color = null) {
        var l = Style.Wrapped("", size, color); content.AddChild(l);
        var text = new LiveText(shown => l.Text = shown);
        void Update() {
            if (!GodotObject.IsInstanceValid(l)) return;
            write(text.Clear()); text.Show();
        }
        Update(); live.Add(Update);
    }
    private void BuildHud() {
        // The day dial and clock share a plate in the top right corner; the side panel opens below it.
        var clock = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -200, OffsetRight = -20, OffsetTop = 18, OffsetBottom = 18, MouseFilter = Control.MouseFilterEnum.Ignore };
        clock.AddThemeStyleboxOverride("panel", Style.Plate); ui.AddChild(clock);
        var dial = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; dial.AddThemeConstantOverride("separation", 6); clock.AddChild(dial);
        dayDial = new DayDial { Arc = () => world.SkyArc, Night = () => world.MoonUp, CustomMinimumSize = new Vector2(0, 62), MouseFilter = Control.MouseFilterEnum.Ignore }; dial.AddChild(dayDial);
        stats = Style.Heading("", 16); stats.HorizontalAlignment = HorizontalAlignment.Center; dial.AddChild(stats);
        modeLabel = Style.Outlined(Style.Wrapped("", 17)); modeLabel.AnchorLeft = .5f; modeLabel.AnchorRight = .5f; modeLabel.OffsetTop = 18; modeLabel.GrowHorizontal = Control.GrowDirection.Both; modeLabel.HorizontalAlignment = HorizontalAlignment.Center; modeLabel.AutowrapMode = TextServer.AutowrapMode.Off; modeLabel.MouseFilter = Control.MouseFilterEnum.Ignore; ui.AddChild(modeLabel);
        // Menu badges and the hotbar sit on one plate centred along the bottom, like a tool belt.
        var bottom = new PanelContainer { AnchorLeft = .5f, AnchorRight = .5f, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -14, OffsetBottom = -14, GrowHorizontal = Control.GrowDirection.Both, GrowVertical = Control.GrowDirection.Begin }; ui.AddChild(bottom);
        bottom.AddThemeStyleboxOverride("panel", Style.Plate);
        var slots = new HBoxContainer(); slots.AddThemeConstantOverride("separation", 10); bottom.AddChild(slots);
        slots.AddChild(MenuIcon("build", "Build", () => OpenPage("build"), "B", Style.Brick)); slots.AddChild(MenuIcon("bag", "Backpack", () => OpenPage("inventory"), "Tab", Style.Sage));
        slots.AddChild(MenuIcon("craft", "Crafting", () => OpenPage("craft"), "C", new Color("b08a45"))); slots.AddChild(MenuIcon("crew", "Crew", () => OpenPage("workers"), "H", new Color("5f8aa6"))); slots.AddChild(MenuIcon("guide", "Field guide", () => OpenPage("help"), "I", new Color("7d6488")));
        var divider = new ColorRect { Color = new Color(Style.Beige, .6f), CustomMinimumSize = new Vector2(3, 46), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; slots.AddChild(divider);
        for (int i = 0; i < 9; i++) slots.AddChild(HotbarButton(i));
        // _Process keeps it between the needs and bank plate on the left and the day dial or side panel on the right.
        messages = new MessageStack(() => Style.Outlined(Style.Wrapped("", 17))) { Name = "messages", AnchorRight = 1, OffsetTop = 56 }; ui.AddChild(messages);
        // The side panel hugs what it shows, up to the room above the hotbar, and scrolls beyond that.
        panel = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = PanelLeft, OffsetRight = -20, OffsetTop = PanelTop, OffsetBottom = PanelTop };
        ui.AddChild(panel);
        // The backpack opens as a drawer above the hotbar whenever a page works with it, always in the same place.
        bag = new PanelContainer { Name = "bag", Visible = false, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 20, OffsetTop = -BagBottom, OffsetBottom = -BagBottom, GrowVertical = Control.GrowDirection.Begin }; ui.AddChild(bag);
        var drawer = new HBoxContainer(); drawer.AddThemeConstantOverride("separation", 14); bag.AddChild(drawer);
        var label = new VBoxContainer { CustomMinimumSize = new Vector2(40, 0) }; label.AddThemeConstantOverride("separation", 4); drawer.AddChild(label);
        label.AddChild(Style.Glyph("line_bag", 30, Style.Rim));
        bagFill = Style.Text("", 13, Style.Beige); bagFill.HorizontalAlignment = HorizontalAlignment.Center; label.AddChild(bagFill);
        var sort = kit.Button("Sort", () => { world.SortBackpack(); RefreshLive(); }); sort.Name = "sort_bag"; sort.TooltipText = "Stack loose items and sort by type"; label.AddChild(sort);
        bagContent = new VBoxContainer(); drawer.AddChild(bagContent);
        // Every closable window keeps its close button in its top right corner.
        var hide = kit.HeaderButton("close", "Hide the backpack", () => { if (page == "inventory") ClosePanel(); else { bagHidden = true; bag.Visible = false; } }, Style.CloseRound, "bag_close");
        hide.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin; drawer.AddChild(hide);
        waterWarning = Style.Outlined(Style.Wrapped("", 17)); waterWarning.Name = "water_warning";
        waterWarning.AnchorTop = 1; waterWarning.AnchorBottom = 1;
        waterWarning.OffsetLeft = 20; waterWarning.OffsetRight = 650; waterWarning.OffsetTop = -250; waterWarning.OffsetBottom = -220;
        waterWarning.AutowrapMode = TextServer.AutowrapMode.Off;
        waterWarning.MouseFilter = Control.MouseFilterEnum.Ignore; ui.AddChild(waterWarning);
        deathLabel = Style.Outlined(Style.Wrapped("", 26)); deathLabel.AutowrapMode = TextServer.AutowrapMode.Off; deathLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); deathLabel.GrowHorizontal = Control.GrowDirection.Both;
        // Raised above the centred player so a downed body stays visible under the message.
        deathLabel.OffsetTop -= 190; deathLabel.OffsetBottom -= 190;
        deathLabel.HorizontalAlignment = HorizontalAlignment.Center; deathLabel.MouseFilter = Control.MouseFilterEnum.Ignore; ui.AddChild(deathLabel);
        BuildNeeds(); ending = new EndingScreen(kit, ClosePanel, Restart); ui.AddChild(ending); BuildMenu(); BuildScreens();
        blackHole = new BlackHole(kit, view, ui.Theme, ClosePanel, LatestSlot, LoadSlot, ShowStart); AddChild(blackHole);
        RefreshLive();
    }
    private void Notify(string message) {
        if (message == Water.FullBottleWarning && waterWarning != null) { waterWarning.Text = message; waterWarningClock = 7; } else messages?.Post(message);
        GD.Print(message);
    }
    // What the labels show, so their text is built only when it changes: building it allocates.
    private (int Day, int Minute, bool Night) shownClock = (-1, -1, false);
    private (bool Dead, bool Mauled, bool Pack, int Seconds) shownDeath;
    private (bool Assigning, ThingKind? Mode) shownMode = (false, null);
    private void RefreshLive() {
        if (world == null || stats == null) return;
        var clock = (world.Day, (int)(world.DayPhase * 1440), world.Night);
        if (clock != shownClock) {
            shownClock = clock;
            int hour = (int)world.Hour;
            stats.Text = $"Day {world.Day} · {hour}:{(int)(world.DayPhase * 1440) % 60:00}";
            dayDial.QueueRedraw();
        }
        int feeding = 0;
        foreach (var w in world.S.Wolves) if (w.Feeding) feeding++;
        var death = (world.Dead, world.S.Mauled, feeding > 1, (int)MathF.Ceiling(world.S.RespawnIn));
        if (death != shownDeath) {
            shownDeath = death;
            deathLabel.Text = world.Dead ? $"{(world.S.Mauled ? feeding > 1 ? "The pack is feeding" : "A wolf is feeding" : "You collapsed")}\nRespawning in {MathF.Ceiling(world.S.RespawnIn):0}s" : "";
        }
        var mode = (assigningWorker > 0, view.BuildMode);
        if (mode != shownMode) {
            shownMode = mode;
            modeLabel.Text = assigningWorker > 0 ? "WORK ORDER · Click a resource to assign this villager. Esc cancels." : view.BuildMode != null ? $"PLACE {world.Data.Name(view.BuildMode.Value).ToUpperInvariant()} · Left click ground. Esc cancels." : "";
        }
        // An update can open another page, which replaces the list; the old page's remaining updates are then dropped.
        int opened = pagesOpened;
        for (int i = 0; i < live.Count && opened == pagesOpened; i++) live[i]();
        RefreshHotbar(); RefreshNeeds(); RefreshBag();
    }
    private (int Used, int Slots) shownFill = (-1, -1);
    private void RefreshBag() {
        if (!bag.Visible) return;
        var fill = (world.S.Backpack.UsedSlots, world.S.Backpack.Slots);
        if (fill == shownFill) return;
        shownFill = fill;
        bagFill.Text = $"{fill.UsedSlots}/{fill.Slots}";
    }
    /// <summary>Sizes the panel's scroll area to what it shows, up to the room between the clock and the hotbar.</summary>
    private void FitPanel() {
        if (!panel.Visible || !GodotObject.IsInstanceValid(scroll)) return;
        float room = Math.Max(160, ui.Size.Y - PanelTop - 120);
        float want = Math.Min(content.GetParent<Control>().GetCombinedMinimumSize().Y, room);
        if (sheet.Visible && GodotObject.IsInstanceValid(sheetScroll)) {
            // The board reaches further left than the panel, over the backpack drawer, so it stops above the drawer.
            float clear = bag.Visible ? ui.Size.Y - PanelTop - BagBottom - bag.Size.Y - 40 : room;
            float rows = Math.Min(sheetRows!.GetCombinedMinimumSize().Y, Math.Min(room, clear) - 110);
            if (Math.Abs(sheetScroll.CustomMinimumSize.Y - rows) > .5f) sheetScroll.CustomMinimumSize = new Vector2(0, rows);
        }
        if (Math.Abs(scroll.CustomMinimumSize.Y - want) < .5f) return;
        // A control grows to its minimum size but never shrinks back on its own, so the panel is reset to it.
        scroll.CustomMinimumSize = new Vector2(0, want);
        panel.OffsetBottom = PanelTop;
    }
    private void OpenPage(string name) {
        // A different page or building closes the job list and the filter window that belonged to the last one.
        if (name != page || name == "inspect" && (selected, workerId) != shownInspect) { openJob = ""; filterOpen = false; filterPicking = false; bagHidden = false; }
        shownInspect = (selected, workerId);
        page = name; live.Clear(); pagesOpened++; panel.Visible = true; view.PanelOpen = true;
        foreach (var child in panel.GetChildren()) { panel.RemoveChild(child); child.QueueFree(); }
        foreach (var child in bagContent.GetChildren()) { bagContent.RemoveChild(child); child.QueueFree(); }
        bag.Visible = false; shownFill = (-1, -1); sheetScroll = null;
        panel.OffsetLeft = PanelLeft;
        var frame = new HBoxContainer(); frame.AddThemeConstantOverride("separation", 20); panel.AddChild(frame);
        side = new PanelContainer { Visible = false, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; frame.AddChild(side);
        sheet = new VBoxContainer { Visible = false }; sheet.AddThemeConstantOverride("separation", 12); side.AddChild(sheet);
        var outer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; outer.AddThemeConstantOverride("separation", 16); frame.AddChild(outer);
        heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 12); outer.AddChild(heading);
        headingIcon = Style.Icon(null, 44); headingIcon.Visible = false; headingIcon.MouseFilter = Control.MouseFilterEnum.Pass; heading.AddChild(headingIcon);
        headingTitle = Style.Heading(name switch { "welcome" => "A new beginning", "build" => "Building plans", "inventory" => "Backpack", "help" => "Field guide", "workers" => "Your crew", "craft" => "Crafting", _ => "Inspect" });
        headingTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(headingTitle);
        heading.AddChild(kit.HeaderButton("close", "Close · Esc", ClosePanel, Style.CloseRound, "close"));
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        // Room for the pills that sit on the cups' corners.
        var inset = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; inset.AddThemeConstantOverride("margin_right", 8); inset.AddThemeConstantOverride("margin_bottom", 8); scroll.AddChild(inset);
        content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; inset.AddChild(content);
        switch (name) {
            case "welcome": Welcome(); break;
            case "help": Help(); break;
            case "build": BuildingPlans(); break;
            case "inventory": Backpack(); break;
            case "workers": Workers(); break;
            case "craft": CraftMenu(); break;
            default: Inspect(); break;
        }
        RefreshLive();
    }
    private (int, int) shownInspect;
    /// <summary>Names the page after the building it shows, with the building's own icon; its description becomes the icon's tooltip.</summary>
    private void Headline(ThingKind kind) {
        headingTitle.Text = world.Data.Name(kind);
        headingIcon.Texture = Icons.Of(kind.Id()); headingIcon.Visible = headingIcon.Texture != null;
        headingIcon.TooltipText = world.Data.Buildings.TryGetValue(kind, out var def) ? def.Description : "";
    }
    /// <summary>Shows the backpack drawer with a grid that works with the open page: <paramref name="wanted"/> lights up what it takes.</summary>
    private void BagGrid(Action<string> click, string hint, Func<ItemDrag, bool>? accepts = null, Func<string, bool>? wanted = null) {
        var host = content; content = bagContent;
        AddInventoryGrid(() => world.S.Backpack, null, click, hint, accepts, wanted, BagColumns);
        content = host; bag.Visible = !bagHidden;
    }
    /// <summary>Set by the drawer's close button; the drawer stays hidden until another page or building opens.</summary>
    private bool bagHidden;
    private void ClosePanel() { panel.Visible = false; bag.Visible = false; view.PanelOpen = false; page = ""; live.Clear(); pagesOpened++; }
    // Counts pages opened and closed, so a refresh notices when an update replaced the page under it.
    private int pagesOpened;
    private void Welcome() {
        AddLabel("The woods are quiet. For now.", 21);
        AddLabel("First, secure water, a cabin and a fire. Then use your steel axe and your hands to build a workshop, trade berries for a pickaxe, and work toward steel.", 16, Style.Beige);
        AddLabel("FIRST STEPS", 14);
        AddLabel("1  Collect water at the pond and keep a bottle filled. The sea is salt.\n2  Gather wood and stone; build a cabin and campfire before night.\n3  With your steel axe, fell trees. Gather stone and berries by hand. Knap a chisel, carve planks and a mallet, then build a bench.\n4  Make crates, build a trading post and sell berries. Buy your first pickaxe, then mine toward iron and steel.\n5  Press Esc any time to see your current goal.");
        AddLabel("A bottle holds three sips and can be refilled at the lake. Without one, drink at the source. Each gathering action takes time, so plan ahead.", 15, Style.Beige);
        AddButton("Into the clearing", ClosePanel);
    }
    private string Needs(Dictionary<string, int> cost) => LiveText.Of(text => AppendNeeds(text, cost));
    private string Cost(Dictionary<string, int> cost) => LiveText.Of(text => AppendCost(text, cost));
    /// <summary>Each input with how many the backpack holds of how many are needed.</summary>
    private void AppendNeeds(StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{world.Data.Name(item)} {world.S.Backpack[item]}/{amount}");
        }
    }
    private void AppendCost(StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{amount} {world.Data.Name(item)}");
        }
    }
    private void BuildingPlans() {
        var grid = AddGrid();
        var details = Style.Wrapped("Hover a plan to see what it needs. Click it, then click open ground nearby.", 14, Style.Beige);
        foreach (var (id, def) in world.Data.Buildings) {
            var (cell, _) = ItemSlot.Cup($"build_{id.Id()}"); cell.Icon = Icons.Of(id.Id()); cell.TooltipText = $"{def.Name}\nClick to place · Drag onto the hotbar"; DragReference(cell, id.Id());
            cell.Pressed += () => { view.BuildMode = id; assigningWorker = 0; ClosePanel(); RefreshLive(); };
            cell.MouseEntered += () => details.Text = $"{def.Name}\n{def.Description}\n\nNeeds: {Needs(def.Cost)}";
            live.Add(() => { if (GodotObject.IsInstanceValid(cell)) cell.Modulate = world.S.Backpack.Has(def.Cost) ? Colors.White : Style.Unready; });
            grid.AddChild(cell);
        }
        content.AddChild(details);
    }
    /// <summary>The backpack is only the drawer; the side panel stays hidden while it shows.</summary>
    private void Backpack() {
        BagGrid(UseFromBag, UseHint);
        // Only the drawer shows, so the camera keeps the player centred.
        panel.Visible = false; view.PanelOpen = false;
    }
    private const string UseHint = "Food and water: click to consume. Crates: click to place. Torch: click to light. Drag onto the hotbar to assign.";
    private void UseFromBag(string id) {
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) world.Consume(id); else if (World.IsCrate(id)) PlaceCrate(id); else if (id == "torch" && !world.S.TorchLit) world.UseTorch(Vector2.Zero);
    }
    private int DailyWages() {
        int wages = 0;
        foreach (var w in world.S.Workers) wages += w.Wage;
        return wages;
    }
    private void Select(Thing t) {
        if (t.Kind == ThingKind.Reinforced && !(page == "inspect" && selected == t.Id)) sounds.Play("open_steel_smelter", t.Pos);
        selected = t.Id; workerId = 0; view.Selected = t.Id; view.SelectedWorker = 0; view.BuildMode = null; OpenPage("inspect");
    }
    /// <summary>Enters placement for a crate from the backpack.</summary>
    private void PlaceCrate(string item) {
        if (world.S.Backpack[item] == 0) { Notify("No empty crate in your backpack. Craft one at a bench."); return; }
        placingCrate = item; view.BuildMode = ThingKind.Crate; assigningWorker = 0; ClosePanel();
    }
    private bool CanPlaceAgain() => view.BuildMode == ThingKind.Crate ? world.S.Backpack[placingCrate] > 0 : view.BuildMode is ThingKind kind && world.S.Backpack.Has(world.Data.Buildings[kind].Cost);
    private void Select(Worker w) { workerId = w.Id; selected = 0; view.Selected = 0; view.SelectedWorker = w.Id; OpenPage("inspect"); }
    private void Inspect() {
        if (workerId > 0) { WorkerPanel(world.S.Workers.FirstOrDefault(w => w.Id == workerId)); return; }
        var t = world.Find(selected);
        if (t == null) { AddLabel("Select a building or resource in the world."); return; }
        Headline(t.Kind);
        if (t.Kind == ThingKind.Water) headingTitle.Text = "Collect water";
        Reach(t);
        if (t.IsResource) { Resource(t); return; }
        if (t.Kind == ThingKind.Hut) { Cabin(t); return; }
        if (t.Tools != null) { Bench(t); return; }
        if (t.Kind == ThingKind.Signpost) { Signpost(t); return; }
        if (t.Kind == ThingKind.Mine) { MinePanel(t); return; }
        if (t.Kind == ThingKind.Farm) { FarmPanel(t); return; }
        if (t.Kind == ThingKind.Campfire) { Campfire(t); return; }
        if (t.Kind == ThingKind.Packer) { PackerPanel(t); return; }
        if (t.IsStorage) { Storage(t); return; }
        Station(t);
    }
    /// <summary>How far away a building is, shown only while it is out of reach.</summary>
    private void Reach(Thing t) {
        var label = Style.Text("", 14, Style.Warn); label.Visible = false; content.AddChild(label);
        var text = new LiveText(shown => label.Text = shown);
        // Rewritten only when the distance moves a tenth of a tile or the building comes into reach.
        int drawn = int.MinValue;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(label)) return;
            int tenths = world.Near(t) ? -1 : (int)(world.Player.DistanceTo(world.GatherAt(t, world.Player)) * 10);
            if (tenths == drawn) return;
            drawn = tenths;
            label.Visible = tenths >= 0;
            if (tenths >= 0) { text.Clear().Append(tenths / 10).Append('.').Append(tenths % 10).Append(" tiles away · walk closer"); text.Show(); }
        });
    }
    private void Resource(Thing t) {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Icon(Icons.Of(World.ResourceItem(t)), 44));
        var left = Style.Text("", 16, Style.Ink); left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; left.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(left);
        var text = new LiveText(shown => left.Text = shown);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(left)) return;
            var line = text.Clear();
            if (t.Kind != ThingKind.Water) line.Append(t.Remaining).Append(" left · ");
            line.Append(world.GatherBlock(t) ?? (t.IsLoose ? "right-click to pick it up" : "hold right mouse to gather"));
            text.Show();
        });
        if (t.Kind == ThingKind.Water) AddLabel("Collect water · hold right mouse on the pond shore to fill a bottle, or drink if you have none.", 14, Style.Beige);
        JobButtons(t);
    }
    private void Campfire(Thing fire) {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Glyph("line_flame", 30, Style.Ember));
        var light = Style.Text("", 16, Style.Ink); light.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(light);
        var text = new LiveText(shown => light.Text = shown);
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(light)) return;
            int minutes = (int)(fire.Burn / world.Data.DaySeconds * 1440);
            if (minutes == drawn) return;
            drawn = minutes;
            AppendGameHours(text.Clear(), fire.Burn).Append(" of light · wolves keep 5 tiles off"); text.Show();
        });
        var (wood, count) = ItemSlot.Cup("fire_wood"); wood.Icon = Icons.Of("wood"); row.AddChild(wood);
        int held = -1;
        live.Add(() => { if (GodotObject.IsInstanceValid(wood) && fire.Inventory["wood"] != held) { held = fire.Inventory["wood"]; count.Text = held > 0 ? held.ToString() : ""; } });
        AddButton($"Feed · 1 wood · +{world.Data.CampfireHours:0} h", () => world.FuelFire(fire));
        JobButtons(fire);
    }
    private void Cabin(Thing hut) {
        var tag = Style.Pill("", Style.TagPill, Style.Rim, 13); tag.MouseFilter = Control.MouseFilterEnum.Pass; tag.TooltipText = "Wolves back off while you are inside";
        heading.AddChild(tag); heading.MoveChild(tag, heading.GetChildCount() - 2);
        // What a night's sleep would leave: each need now, and at dawn.
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 12);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); well.AddChild(top);
        top.AddChild(Style.Glyph("line_moon", 20, Style.Ink));
        var until = Style.Text("", 14, Style.Ink); until.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(until);
        var block = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 13); top.AddChild(block);
        var vials = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vials.AddThemeConstantOverride("separation", 36); well.AddChild(vials);
        var now = new Vial[3]; var then = new Vial[3];
        var colors = new[] { Style.Brick, Style.Mustard, Style.Sky };
        for (int i = 0; i < 3; i++) {
            var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 8); vials.AddChild(pair);
            pair.AddChild(now[i] = new Vial { Fill = colors[i] });
            pair.AddChild(Style.Glyph("line_arrow", 16, Style.Ink));
            pair.AddChild(then[i] = new Vial { Fill = colors[i], Faded = true });
        }
        var buttons = Row(); buttons.AddThemeConstantOverride("separation", 12);
        var door = kit.BigButton("", Style.Sage, () => { if (world.S.Sheltered == hut.Id) world.Leave(); else entering = hut; });
        var sleep = kit.BigButton("Sleep", Style.Enamel, () => world.Sleep(hut)); sleep.Name = "sleep";
        buttons.AddChild(door); buttons.AddChild(sleep);
        var untilText = new LiveText(shown => until.Text = shown);
        (bool Inside, string? Block, int Minutes) shown = (false, "", -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(sleep)) return;
            bool inside = world.S.Sheltered == hut.Id;
            string? blocked = world.SleepBlock();
            int minutes = (int)(world.SecondsToDawn / world.Data.DaySeconds * 1440);
            if ((inside, blocked, minutes) == shown) return;
            shown = (inside, blocked, minutes);
            tag.Text = inside ? "inside" : "outside";
            tag.AddThemeStyleboxOverride("normal", inside ? Style.TagPill : Style.BlueTag);
            door.Text = inside ? "Step outside" : "Go inside";
            sleep.TooltipText = blocked ?? "Sleep until dawn. Production, hunger, trade and wages advance.";
            sleep.Modulate = blocked == null ? Colors.White : Style.Unready;
            block.Visible = blocked != null; block.Text = blocked == null ? "" : ShortBlock(blocked);
            AppendGameHours(untilText.Clear().Append("until dawn · +"), world.SecondsToDawn); untilText.Show();
            float seconds = world.SecondsToDawn;
            Show(0, world.S.Health, world.S.Health);
            Show(1, world.S.Hunger, world.S.Hunger - seconds * world.Data.HungerPerSecond);
            Show(2, world.S.Thirst, world.S.Thirst - seconds * world.Data.ThirstPerSecond);
        });
        void Show(int i, float level, float after) {
            now[i].Level = level; then[i].Level = Math.Max(0, after); then[i].Warn = after <= 0;
            now[i].QueueRedraw(); then[i].QueueRedraw();
        }
    }
    /// <summary>A sleep refusal cut to a tag: what stops it, in a word or two.</summary>
    private static string ShortBlock(string reason) => reason.Contains("thirsty") && reason.Contains("hungry") ? "too hungry and thirsty" : reason.Contains("thirsty") ? "too thirsty" : reason.Contains("hungry") ? "too hungry" : reason.Contains("night") ? "only at night" : "not yet";
    private void Storage(Thing t) {
        switch (t.Kind) {
            case ThingKind.Crate: Crate(t); return;
            case ThingKind.Grave: Recovery(t); return;
            case ThingKind.Toolstation: ToolRack(t); return;
            case ThingKind.Pile: Pile(t); return;
        }
        HeaderAction("stash", "Store all materials · keeps tools", () => StoreAll(t), Style.ActionRound, "deposit");
        if (t.Kind == ThingKind.Chest) FilterButton(t);
        Well(new VBoxContainer()).AddChild(StorageGrid(t));
        BagGrid(id => MoveStack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move");
    }
    /// <summary>A storage's slots in a grid of cups; click takes one item, Ctrl all of it.</summary>
    private GridContainer StorageGrid(Thing t, int columns = GridColumns, float size = 52) {
        var host = content; var column = new VBoxContainer(); content = column;
        var grid = AddInventoryGrid(() => t.Inventory, t, id => MoveStack(t.Inventory, world.S.Backpack, id, t), "Click: take one · Ctrl: take all · Drag to move", columns: columns, size: size);
        content = host; column.RemoveChild(grid); column.QueueFree();
        return grid;
    }
    /// <summary>Moves every material the storage takes from the backpack; tools stay, and so do crates, so one is never packed inside another.</summary>
    private void StoreAll(Thing t) {
        if (!world.Near(t)) { Notify("Walk closer first."); return; }
        foreach (var id in world.S.Backpack.Items.Keys.Where(i => world.Data.Items.TryGetValue(i, out var def) && def.Tool == "" && i != "crate").ToArray())
            if (world.Holds(t, id) && t.Inventory.Accepts(id)) world.Transfer(world.S.Backpack, t.Inventory, id, 999);
    }
    private void TakeAll(Thing t) {
        if (!world.Near(t)) { Notify("Walk closer first."); return; }
        foreach (var id in t.Inventory.Items.Keys.ToArray()) world.Transfer(t.Inventory, world.S.Backpack, id, 999);
    }
    /// <summary>A crate: its fill out of twelve, whether the village would buy it packed, and how to pick it up.</summary>
    private void Crate(Thing t) {
        var sells = Style.Pill("✓", Style.TagPill, Style.Rim, 13); sells.MouseFilter = Control.MouseFilterEnum.Pass; sells.TooltipText = "The village buys this crate packed";
        heading.AddChild(sells); heading.MoveChild(sells, heading.GetChildCount() - 2);
        var meter = Row(); meter.AddThemeConstantOverride("separation", 10);
        var cells = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; cells.AddThemeConstantOverride("separation", 4); meter.AddChild(cells);
        var segments = Enumerable.Range(0, t.Inventory.Capacity).Select(_ => new ColorRect { CustomMinimumSize = new Vector2(0, 12), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }).ToArray();
        foreach (var segment in segments) cells.AddChild(segment);
        var fill = Style.Text("", 14, Style.Rim); meter.AddChild(fill);
        var fillText = new LiveText(shown => fill.Text = shown);
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(fill) || t.Inventory.Count == drawn) return;
            drawn = t.Inventory.Count;
            for (int i = 0; i < segments.Length; i++) segments[i].Color = i < drawn ? Style.Mustard : new Color(Style.Rim, .25f);
            fillText.Clear().Append(drawn).Append(" / ").Append(t.Inventory.Capacity); fillText.Show();
            bool sellable = drawn > 0;
            foreach (var pair in t.Inventory.Items) sellable &= world.Data.Market.Sells.ContainsKey(pair.Key);
            sells.Visible = sellable;
        });
        Well(new VBoxContainer()).AddChild(StorageGrid(t));
        var hint = Row(); hint.AddThemeConstantOverride("separation", 8);
        hint.AddChild(Style.Text("Hold", 14, Style.Beige)); hint.AddChild(Style.Glyph("line_mouse", 24, Style.Rim)); hint.AddChild(Style.Text("to pick up", 14, Style.Beige));
        BagGrid(id => MoveStack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move");
    }
    private void Recovery(Thing t) {
        headingTitle.Text = "Recovery box";
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 5));
        var row = Row(); row.AddChild(kit.BigButton("Take all", Style.Brick, () => TakeAll(t)));
        BagGrid(id => MoveStack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Drag to move");
    }
    /// <summary>A tool rack: a row per kind of tool workers use, with what hangs there, how many are out, and who waits for one.</summary>
    private void ToolRack(Thing t) {
        HeaderAction("stash", "Hang spare tools · keeps your best of each", () => {
            if (!world.Near(t)) { Notify("Walk closer first."); return; }
            foreach (var id in world.S.Backpack.Items.Keys.Where(i => world.Holds(t, i) && t.Inventory.Accepts(i)).ToArray())
                world.Transfer(world.S.Backpack, t.Inventory, id, world.BestTool(world.Data.Items[id].Tool) == id ? world.S.Backpack[id] - 1 : world.S.Backpack[id]);
        }, Style.ActionRound, "deposit");
        FilterButton(t);
        var rows = Well(new VBoxContainer()); rows.AddThemeConstantOverride("separation", 0);
        // The four kinds workers fetch always have a row; any other tool hung here, such as a chisel, gets one too.
        var kinds = new List<string> { "axe", "pickaxe", "rake", "hammer" };
        foreach (var id in t.Inventory.Items.Keys) if (world.Data.Items.TryGetValue(id, out var def) && def.Tool != "" && !kinds.Contains(def.Tool)) kinds.Add(def.Tool);
        for (int k = 0; k < kinds.Count; k++) {
            string kind = kinds[k];
            if (k > 0) rows.AddChild(new HSeparator { CustomMinimumSize = new Vector2(0, 14) });
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 60) }; row.AddThemeConstantOverride("separation", 12); rows.AddChild(row);
            string plainest = world.Data.Items.Where(p => p.Value.Tool == kind).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
            var (cell, count) = ItemSlot.Cup($"rack_{kind}"); cell.Icon = Icons.Of(plainest); row.AddChild(cell);
            cell.Pressed += () => { if (world.BestTool(kind, t.Inventory) is string best) { kit.Act(() => MoveStack(t.Inventory, world.S.Backpack, best, t)); RefreshLive(); } };
            cell.Drag = () => world.BestTool(kind, t.Inventory) is string best ? new ItemDrag(best, t.Inventory[best], t.Inventory, t) : null;
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var outPill = Style.Pill("", Style.Tag(Style.Enamel), Style.Rim, 12); outPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(outPill);
            var waitPill = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 12); waitPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(waitPill);
            (string? Best, int Held, int Out, int Waiting) shown = (null, -1, -1, -1);
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(cell)) return;
                int held = 0, out_ = 0, waiting = 0;
                foreach (var pair in t.Inventory.Items) if (world.Data.Items.TryGetValue(pair.Key, out var def) && def.Tool == kind) held += pair.Value;
                foreach (var w in world.S.Workers) { if (w.Tool != "" && world.Data.Items[w.Tool].Tool == kind) out_++; if (w.Missing == kind) waiting++; }
                var now = (world.BestTool(kind, t.Inventory), held, out_, waiting);
                if (now == shown) return;
                shown = now;
                cell.Icon = Icons.Of(now.Item1 ?? plainest);
                cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
                count.Text = held > 1 ? held.ToString() : "";
                cell.TooltipText = held > 0 ? $"{world.Data.Name(now.Item1!)}\nClick to take" : $"No {kind} on the rack";
                outPill.Visible = out_ > 0; outPill.Text = $"{out_} out";
                outPill.TooltipText = string.Join("\n", world.S.Workers.Where(w => w.Tool != "" && world.Data.Items[w.Tool].Tool == kind).Select(w => w.Name));
                waitPill.Visible = waiting > 0; waitPill.Text = $"{waiting} waiting";
                waitPill.TooltipText = string.Join("\n", world.S.Workers.Where(w => w.Missing == kind).Select(w => $"{w.Name} needs {A(kind)}"));
            });
        }
        BagGrid(id => MoveStack(world.S.Backpack, t.Inventory, id, t), "Click: hang a tool · Ctrl: all of them · Drag to move", wanted: id => world.Holds(t, id) && t.Inventory.Accepts(id));
        JobButtons(t);
    }
    private static string A(string word) => "aeiou".Contains(word[0]) ? $"an {word}" : $"a {word}";
    /// <summary>
    /// A pile: fourteen stacks of the one resource it holds, filled left to right and top to bottom, and a switch that turns it into compost.
    /// Compost shows the same grid; under its bar is what it is composting and how long that lasts, and the fertilizer it makes.
    /// </summary>
    private void Pile(Thing t) {
        headingTitle.Text = t.Compost ? "Compost" : t.Inventory.First() is string held ? $"Pile · {world.Data.Name(held).ToLowerInvariant()}" : "Pile";
        if (t.Compost) { headingIcon.Texture = Icons.Of("leaves"); headingIcon.Visible = true; }
        var compost = new CheckButton { ButtonPressed = t.Compost, FocusMode = Control.FocusModeEnum.None, TooltipText = "Compost: turns leaves, grass and food into fertilizer", Name = "compost" };
        compost.Toggled += on => { if (!world.SetCompost(t, on)) compost.SetPressedNoSignal(t.Compost); else OpenPage("inspect"); };
        heading.AddChild(compost); heading.MoveChild(compost, heading.GetChildCount() - 2);
        if (t.Compost) FilterButton(t);
        else HeaderAction("stash", "Store everything this pile takes", () => StoreAll(t), Style.ActionRound, "deposit");
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 7, 44));
        if (!t.Compost) {
            Dynamic(text => text.Append(t.Inventory.UsedSlots).Append(" of ").Append(t.Inventory.Slots).Append(" stacks"), 14, Style.Beige);
        } else {
            var item = Style.Icon(Icons.Of("leaves"), 40); item.MouseFilter = Control.MouseFilterEnum.Pass;
            var lasts = Style.Text("", 13, Style.Tan);
            var making = Style.Icon(Icons.Of("fertilizer"), 52); making.TooltipText = "Makes fertilizer"; making.MouseFilter = Control.MouseFilterEnum.Pass;
            var main = new VBoxContainer(); main.AddThemeConstantOverride("separation", 10); main.AddChild(making); main.AddChild(PerBatch("×1"));
            float hour = world.Data.DaySeconds / 24;
            Process(Array.Empty<Control>(), "1 h", () => t.Progress / hour, FuelRow("line_leaf", new Color("8fae7e"), item, lasts), main, Array.Empty<Control>());
            var lastsText = new LiveText(shown => lasts.Text = shown);
            (string? Item, int Hours) shown = ("", -1);
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(lasts)) return;
                string? rotting = null; int units = 0;
                foreach (var pair in t.Inventory.Items) if (world.Compostable(pair.Key)) { rotting ??= pair.Key; units += pair.Value; }
                var now = (rotting, units / World.CompostBatch);
                if (now == shown) return;
                shown = now;
                item.Texture = Icons.Of(rotting ?? "leaves"); item.Modulate = rotting == null ? Style.Unready : Colors.White;
                item.TooltipText = rotting == null ? "Nothing to compost" : $"Composting {world.Data.Name(rotting).ToLowerInvariant()}";
                lastsText.Clear().Append('×').Append(World.CompostBatch).Append(" · ").Append(now.Item2).Append(" h"); lastsText.Show();
            });
            var skip = new CheckBox { Text = "Skip food delivery", ButtonPressed = t.SkipFood, FocusMode = Control.FocusModeEnum.None, Name = "skip_food", TooltipText = "Workers bring no food here; food you put in yourself still composts" };
            skip.Toggled += on => { world.SetSkipFood(t, on); skip.SetPressedNoSignal(t.SkipFood); };
            content.AddChild(skip);
            JobButtons(t);
        }
        BagGrid(id => MoveStack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move", wanted: id => world.Holds(t, id));
    }
    /// <summary>
    /// A farm plot: its growth from bare soil to ripe wheat, then the process row: seeds in, fertilizer as its fuel, wheat out with seeds and grass beside.
    /// </summary>
    private void FarmPanel(Thing farm) {
        var stages = new HBoxContainer(); stages.AddThemeConstantOverride("separation", 0); content.AddChild(stages);
        string?[] looks = { null, "wheat_seeds", "grass", "wheat" };
        string[] names = { "Fallow", "Sown", "Growing", "Ripe" };
        var nodes = new PanelContainer[4]; var links = new ColorRect[3];
        for (int i = 0; i < 4; i++) {
            if (i > 0) { links[i - 1] = new ColorRect { CustomMinimumSize = new Vector2(0, 6), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; stages.AddChild(links[i - 1]); }
            var node = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), TooltipText = names[i], MouseFilter = Control.MouseFilterEnum.Pass };
            node.AddChild(looks[i] is string look ? Style.Icon(Icons.Of(look), 30) : new ColorRect { Color = new Color("8a6a48"), CustomMinimumSize = new Vector2(24, 10), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            nodes[i] = node; stages.AddChild(node);
        }
        var ripe = Style.Text("", 14, Style.Rim); ripe.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; content.AddChild(ripe);
        var ripeText = new LiveText(shown => ripe.Text = shown);
        (int Stage, int Hours) drawn = (-1, -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(ripe)) return;
            int at = farm.FarmStage == FarmStage.Ripe ? 3 : farm.FarmStage == FarmStage.Growing ? (farm.Progress < world.GrowSeconds / 3 ? 1 : 2) : 0;
            int hours = farm.FarmStage == FarmStage.Growing ? (int)((world.GrowSeconds - farm.Progress) / world.Data.DaySeconds * 24) : -1;
            if ((at, hours) == drawn) return;
            drawn = (at, hours);
            for (int i = 0; i < 4; i++) nodes[i].AddThemeStyleboxOverride("panel", Style.Round(i < at ? Style.Sage : i == at ? Style.Mustard : Style.Line, 22).Item1);
            for (int i = 0; i < 3; i++) links[i].Color = i < at ? Style.Sage : Style.Line;
            var line = ripeText.Clear();
            if (hours >= 0) line.Append("ripe in ").Append(hours / 24).Append(" d ").Append(hours % 24).Append(" h");
            else line.Append(farm.FarmStage == FarmStage.Ripe ? "ripe · hold right mouse on the plot to harvest" : "sows once seeds and fertilizer are in");
            ripeText.Show();
        });
        var output = farm.Output ?? farm.Inventory;
        var main = FarmCup(farm, output, "wheat", false, $"×{world.Data.Farm.Yield.GetValueOrDefault("wheat")}");
        var side = world.Data.Farm.Yield.Keys.Where(k => k != "wheat").Select(k => FarmCup(farm, output, k, false, $"×{world.Data.Farm.Yield[k]}", 44)).ToArray();
        var fertilizer = FarmCup(farm, farm.Inventory, "fertilizer", true, "", 52, Style.CupFuel);
        var lasts = Style.Text("", 13, Style.Tan);
        var lastsText = new LiveText(shown => lasts.Text = shown);
        int stock = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(lasts) || farm.Inventory["fertilizer"] == stock) return;
            stock = farm.Inventory["fertilizer"];
            lastsText.Clear().Append("×").Append(world.Data.Farm.Sow.GetValueOrDefault("fertilizer")).Append(" · ").Append(stock / Math.Max(1, world.Data.Farm.Sow.GetValueOrDefault("fertilizer"))).Append(stock == 1 ? " crop" : " crops"); lastsText.Show();
        });
        var inputs = world.Data.Farm.Sow.Keys.Where(k => k != "fertilizer").Select(k => FarmCup(farm, farm.Inventory, k, true, $"×{world.Data.Farm.Sow[k]}"));
        Process(inputs, $"{world.Data.Farm.GrowDays:0} d", () => farm.FarmStage == FarmStage.Growing ? farm.Progress / world.GrowSeconds : farm.FarmStage == FarmStage.Ripe ? 1 : 0,
            FuelRow("line_leaf", new Color("8fae7e"), FuelCup(fertilizer), lasts), main, side);
        BagGrid(id => MoveStack(world.S.Backpack, farm.Inventory, id, farm), "Click: sow seeds or fertilizer · Drag to move", wanted: id => world.Holds(farm, id));
        JobButtons(farm);
    }
    /// <summary>Takes a cup out of the column it was built in, for the fuel row, and frees the column.</summary>
    private static Control FuelCup(Control column) {
        var cup = column.GetChild<Control>(0); column.RemoveChild(cup); column.QueueFree();
        return cup;
    }
    /// <summary>A cup bound to one item of a farm's sowing or harvest slots, with what one crop takes or gives under it.</summary>
    private VBoxContainer FarmCup(Thing farm, Inventory box, string item, bool input, string perBatch, float size = 52, StyleBoxFlat? look = null) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, count) = ItemSlot.Cup($"{(input ? "input" : "output")}_{item}", size);
        cell.Icon = Icons.Of(item);
        if (look != null) cell.AddThemeStyleboxOverride("normal", look); else if (!input) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Pressed += () => { kit.Act(() => MoveStack(box, world.S.Backpack, item, farm)); RefreshLive(); };
        cell.Drag = () => box[item] > 0 ? new ItemDrag(item, box[item], box, farm) : null;
        if (input) {
            // Seed comes back in from the plot's own harvest as well as from the backpack.
            cell.CanTake = drag => (drag.From == world.S.Backpack || drag.From == farm.Output) && drag.Item == item;
            cell.Take = drag => { int left = 0; kit.Act(() => left = MoveDrag(drag, box, farm)); RefreshLive(); return left; };
        }
        int drawn = -1;
        live.Add(() => {
            int held = box[item];
            if (held == drawn) return;
            drawn = held;
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}\n{(input ? "Click: take back · Drag seeds here from the harvest or backpack to sow" : "Click: take · Drag seeds to the sowing slot")}";
        });
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(PerBatch(perBatch));
        return column;
    }
    /// <summary>Burn time as in-game hours and minutes.</summary>
    private StringBuilder AppendGameHours(StringBuilder text, float seconds) {
        int minutes = (int)(seconds / world.Data.DaySeconds * 1440);
        return text.Append(minutes / 60).Append(" h ").Append(minutes % 60).Append(" min");
    }
    private void Restart() {
        net.Leave();
        world.NewGame();
        selected = 0; workerId = 0; gathering = null; assigningWorker = 0; slot = 0; autosave = 0;
        view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0;
        ending.Visible = false; state.Paused = false; OpenPage("welcome");
        Notify("Welcome. WASD to move; hold right mouse to approach and gather. I shows the field guide.");
    }
    private void Save(int n) {
        try {
            using var file = Godot.FileAccess.Open(SlotPath(n), Godot.FileAccess.ModeFlags.Write); if (file == null) throw new InvalidOperationException("Cannot open save file");
            file.StoreString(world.Serialize()); if (n > 0) slot = n; Notify($"Settlement saved · {SlotName(n)}.");
        } catch (Exception e) { Notify($"Save failed: {e.Message}"); }
    }
    private bool Load(int n) {
        try {
            if (!SlotUsed(n)) { Notify($"{SlotName(n)} is empty."); return false; }
            using var file = Godot.FileAccess.Open(SlotPath(n), Godot.FileAccess.ModeFlags.Read); world.Deserialize(file.GetAsText());
            // Another world ends the game the guests are in.
            net.Leave();
            slot = n; selected = 0; workerId = 0; gathering = null; entering = null; assigningWorker = 0; view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0; OpenPage("inventory");
            return true;
        } catch (Exception e) { Notify($"Load failed: {e.Message}"); return false; }
    }
    public override void _UnhandledInput(InputEvent ev) {
        if (view?.EnvironmentReview == true) return;
        if (world == null || view == null || ending.Visible) return;
        if (blackHole.Showing) { if (blackHole.Asking && ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) blackHole.Cancel(); return; }
        if (backdrop.Visible) { if (ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) screenBack?.Invoke(); return; }
        if (menu.Visible) { if (ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) CloseMenu(); return; }
        if (ev is InputEventKey key && key.Pressed && !key.Echo) {
            switch (key.PhysicalKeycode) {
                case Key.Escape:
                    if (view.BuildMode != null || assigningWorker > 0) { view.BuildMode = null; assigningWorker = 0; } else if (panel.Visible || bag.Visible) ClosePanel(); else OpenMenu();
                    RefreshLive(); break;
                case Key.B: OpenPage("build"); break;
                case Key.I: if (page == "help") ClosePanel(); else OpenPage("help"); break;
                case Key.H: if (page == "workers") ClosePanel(); else OpenPage("workers"); break;
                case Key.C: if (page == "craft") ClosePanel(); else OpenPage("craft"); break;
                case Key.Tab: if (page == "inventory") ClosePanel(); else OpenPage("inventory"); break;
                // Until a numbered slot is chosen, quick save and load ask which slot to use.
                case Key.F5: if (world.Remote) { net.SaveGuest(); Notify("Your player is saved on this computer. The host saves the world."); } else if (slot > 0) Save(slot); else ShowSlots(true, HideScreen); break;
                case Key.F9: if (world.Remote) Notify("Leave the host's game to load your own."); else if (slot > 0) Load(slot); else ShowSlots(false, HideScreen); break;
                case >= Key.Key1 and <= Key.Key9: SelectHotbar((int)(key.PhysicalKeycode - Key.Key1)); break;
                case Key.E:
                    var t = world.Find(selected);
                    if (t == null || !world.Near(t)) t = world.S.Things.Where(t => world.Near(t)).OrderBy(t => t.Pos.DistanceTo(world.Player)).FirstOrDefault();
                    if (t != null) Select(t); break;
                case Key.Space:
                    if (state.Paused) { Notify("Resume before using tools."); break; }
                    var station = world.Find(selected);
                    if (station != null && station.Kind == ThingKind.Reinforced && world.Near(station)) world.Pump(station); else world.Attack(); break;
            }
        }
        if (ev is InputEventMouseButton mouse) {
            // A panel that has nothing to scroll passes the wheel on; over any panel it must not zoom the world behind.
            bool overPanel = GetViewport().GuiGetHoveredControl() != null;
            if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed && !overPanel) view.Zoom = Math.Min(1.6f, view.Zoom + .1f);
            if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed && !overPanel) view.Zoom = Math.Max(.55f, view.Zoom - .1f);
            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed) {
                var t = view.Pick(mouse.Position); if (t?.IsResource == true || t?.Kind == ThingKind.Farm) { gathering = t; selected = t.Id; view.Selected = t.Id; } else gathering = null;
                lifting = t?.Kind == ThingKind.Crate ? t : null; liftClock = 0;
            }
            if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) {
                if (view.BuildMode != null) {
                    var at = view.ToWorld(mouse.Position).Round();
                    var built = view.BuildMode == ThingKind.Crate ? world.PlaceCrate(placingCrate, at) : world.Build(view.BuildMode.Value, at);
                    // Placement stays on without opening what was placed, so several go down in a row until the materials or crates run out.
                    // A guest's building goes up on the host, so the guest stays in placement until Esc, except after a mine the host was sent.
                    // A mine opens its panel instead, since each cliff takes only one.
                    if (built?.Kind == ThingKind.Mine) { view.BuildMode = null; Select(built); } else if (built != null && !CanPlaceAgain()) view.BuildMode = null;
                    else if (world.Remote && view.BuildMode == ThingKind.Mine && world.PlacementBlock(ThingKind.Mine, world.Snap(ThingKind.Mine, at)) == null) view.BuildMode = null;
                    return;
                }
                var t = view.Pick(mouse.Position);
                if (assigningWorker > 0) {
                    var worker = world.S.Workers.FirstOrDefault(w => w.Id == assigningWorker);
                    if (worker != null && t?.IsResource == true) {
                        string? blocked = world.GatherBlock(t, true);
                        if (blocked != null) Notify(blocked); else { world.SetDuty(worker, t, "gather", true); assigningWorker = 0; Select(worker); }
                    } else Notify("Choose a tree, leaves, stone, ore, coal, berry bush or water source.");
                    return;
                }
                var w = view.PickWorker(mouse.Position);
                if (w != null) Select(w);
                else if (t != null) {
                    if (t.Kind == ThingKind.Hut) entering = t;
                    // A crate in hand only opens a clicked crate: the nesting alert is for deliberate moves inside the inventory.
                    if (t.IsStorage && ActiveHotbarItem() is string item && IsTransferableHotbarItem(item) && world.Holds(t, item) && !(t.Kind == ThingKind.Crate && World.IsCrate(item))) {
                        if (world.Near(t)) { int moved = 0; kit.Act(() => moved = world.Transfer(world.S.Backpack, t.Inventory, item, MoveAll ? world.S.Backpack[item] : 1)); if (!world.Remote && moved == 0) Notify($"No {world.Data.Name(item)} to store, or chest is full."); } else Notify("Walk closer to the chest to store the selected stack.");
                    }
                    Select(t);
                } else if (ActiveHotbarItem() is string held && HasUse(held)) UseHeld(view.ToWorld(mouse.Position));
                else ClosePanel();
            }
        }
    }
    // Read once: fetching them from the engine allocates a fresh array each time.
    private static readonly string[] launchArgs = OS.GetCmdlineUserArgs();
    private static readonly bool smokeTest = launchArgs.Contains("--smoke-test");
    private static readonly string? screenshotPath = LaunchValue("--screenshot");
    private static readonly float shotDelay = float.TryParse(LaunchValue("--shot-delay"), System.Globalization.CultureInfo.InvariantCulture, out float wait) ? wait : 2;
    // Review fixtures and UI checks keep their own files so they never touch the player's saves or settings.
    private static readonly string FilePrefix = launchArgs.Contains("--performance-run") ? "performance-" : launchArgs.Contains("--showcase") ? "review-" : launchArgs.Contains("--ui-test") ? "ui-check-" : launchArgs.Contains("--net-check") ? "net-check-" : "";
    /// <summary>The launch argument after <paramref name="option"/>, or null when it is missing.</summary>
    private static string? LaunchValue(string option) {
        int at = Array.IndexOf(launchArgs, option);
        return at >= 0 && at + 1 < launchArgs.Length ? launchArgs[at + 1] : null;
    }
    public override void _Process(double delta) {
        if (world == null || view == null) return;
        float dt = Math.Min((float)delta, .1f); runTime += dt;
        if (!state.Paused) {
            float x = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
            float y = (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
            Vector2 direction = new Vector2(x + y * 2, y * 2 - x).Normalized();
            bool right = Input.IsMouseButtonPressed(MouseButton.Right);
            // A clicked cabin draws the player to its door, even with a movement key still held from walking there.
            if (world.Downed || entering != null && world.Find(entering.Id) == null) entering = null;
            if (entering != null) { if (world.Enter(entering)) entering = null; else direction = (entering.Pos - world.Player).Normalized(); }
            // A loose piece needs only one click: the player keeps walking to it after the button is released.
            if (gathering != null && (right || gathering.IsLoose) && direction == Vector2.Zero && entering == null) {
                // Step in close enough for the tool to reach the model; the lake edge keeps its wider reach.
                if (!world.Near(gathering, gathering.Kind == ThingKind.Water ? 2.5f : 1.35f)) direction = (world.GatherAt(gathering, world.Player) - world.Player).Normalized();
                else if (gathering.IsLoose) {
                    world.Gather(gathering, dt);
                    if (world.Find(gathering.Id) == null && selected == gathering.Id) { selected = 0; view.Selected = 0; }
                    gathering = null;
                } else {
                    // Emptying it shows on the stump or bare bush; a target that was already empty or out of reach still says why.
                    bool had = gathering.Remaining > 0; world.Gather(gathering, dt);
                    if (world.GatherBlock(gathering) is string blocked) { if (!had || gathering.Remaining > 0) Notify(blocked); gathering = null; }
                }
            } else { world.ResetGather(); if (!right || gathering?.IsLoose == true) gathering = null; }
            // A held right click walks to a crate and lifts it once the button has been held long enough.
            if (lifting != null && right && direction == Vector2.Zero && entering == null && world.Find(lifting.Id) != null) {
                if (!world.Near(lifting, 1.5f)) direction = (lifting.Pos - world.Player).Normalized();
                else if ((liftClock += dt) >= LiftSeconds) {
                    var crate = lifting; lifting = null;
                    if (world.PickUpCrate(crate) && selected == crate.Id) { selected = 0; view.Selected = 0; if (page == "inspect") ClosePanel(); }
                }
            } else { lifting = null; liftClock = 0; }
            view.Lifting = lifting?.Id ?? 0; view.LiftProgress = liftClock / LiftSeconds;
            // Under 15% food or drink the player is too weak to sprint.
            world.Me.Sprinting = direction != Vector2.Zero && !world.Downed && Input.IsPhysicalKeyPressed(Key.Shift) && world.S.Hunger >= GameData.LowNeed && world.S.Thirst >= GameData.LowNeed;
            world.MovePlayer(direction * dt * world.Data.WalkSpeed * (world.Me.Sprinting ? world.Data.SprintSpeed : 1)); view.Gathering = gathering is { IsLoose: false } && right && world.Near(gathering, 2.5f);
            view.GatheringTarget = view.Gathering ? gathering : null;
            world.Me.Held = ActiveHotbarItem() ?? "";
        } else world.Me.Sprinting = false;
        // Menus pause only a game nobody else is in.
        bool running = !state.Paused || net.Hosting || world.Remote;
        if (running) {
            float health = world.Remote ? shownHealth : world.S.Health;
            if (!world.Remote) world.Tick(dt);
            ending.ShowOnce(); autosave += dt;
            DamageFlash(health - world.S.Health); shownHealth = world.S.Health;
            // A guest saves its player whenever it changes instead; the world is the host's to save.
            if (autosave >= AutosaveSeconds) { if (!world.Remote && !performanceRunning) Save(0); autosave = 0; }
        }
        view.Paused = !running; view.SelectedTool = ActiveHotbarItem();
        view.Mouse = GetViewport().GetMousePosition();
        var hover = view.Pick(view.Mouse); view.Hover = hover?.Id ?? 0; view.HoverWorker = view.PickWorker(view.Mouse)?.Id ?? 0;
        uiClock += dt; if (uiClock > .2f) { uiClock = 0; RefreshLive(); }
        RefreshRecoveryPrompt();
        // Every frame, so a page that just opened takes its size once its wrapped text has a width.
        FitPanel();
        if (waterWarningClock > 0) waterWarning.Position = new Vector2(20, bag.Visible ? bag.Position.Y - 36 : ui.Size.Y - 148);
        waterWarningClock -= dt; if (waterWarningClock <= 0 && waterWarning != null) waterWarning.Text = "";
        // The needs plate grows with the bank count, and an open side panel would cover the messages' right end.
        float messagesLeft = needsPlate.GetRect().End.X + 24, messagesRight = panel.Visible ? PanelLeft - 16 : -messagesLeft;
        if (messages.OffsetLeft != messagesLeft || messages.OffsetRight != messagesRight) { messages.OffsetLeft = messagesLeft; messages.OffsetRight = messagesRight; }
        if (smokeTest && runTime > .5f && !quitting) { GD.Print("SMOKE-TEST PASS"); Quit(); }
        if (screenshotPath != null && runTime > shotDelay && !capturing) { capturing = true; Capture(screenshotPath); }
        if (performanceRunning) SamplePerformance(delta);
    }
    private async void Capture(string path) {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"Screenshot: {path} ({error})"); Quit(error == Error.Ok ? 0 : 1);
    }
    public override void _Notification(int what) {
        if (what == NotificationWMCloseRequest && !GetTree().AutoAcceptQuit) Quit();
    }
    private async void Quit(int code = 0) {
        if (quitting) return;
        quitting = true; net.Leave(); sounds.StopAll(); await Seconds(.2);
        // Godot objects left to the finalizer must be released before the engine shuts down, or a late finalizer aborts the exit.
        GC.Collect(); GC.WaitForPendingFinalizers(); GetTree().Quit(code);
    }
    /// <summary>Mine review at the near cliff of <c>--mine-ore</c>, iron by default: <c>--mine-stage N</c> sets the finished stages, and a finished mine gets a crew underground and some output.</summary>
    private void MineReview(Thing sign) {
        var stage = int.TryParse(LaunchValue("--mine-stage"), out int n) ? (MineStage)n : MineStage.Open;
        var cliff = World.CliffOf(Kinds.Parse(LaunchValue("--mine-ore")) ?? ThingKind.Iron);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        var mine = world.Build(ThingKind.Mine, World.MineSpot(cliff))!;
        mine.MineStage = stage; mine.Progress = stage is MineStage.Marked or MineStage.Supplied ? world.MineStageSeconds / 2 : 0;
        if (stage == MineStage.Dug) mine.Inventory.Add("wood", 20);
        world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3 + cliff.Along * 2);
        foreach (var w in world.S.Workers.ToArray()) world.Assign(w, null);
        foreach (var w in world.S.Workers.Take(stage == MineStage.Open ? 3 : 1)) { w.Pos = world.Player; world.Assign(w, mine); }
        if (stage == MineStage.Open && !launchArgs.Contains("--idle")) mine.Output!.Add(World.MineOre(mine), 20);
        if (launchArgs.Contains("--idle")) { foreach (var w in world.S.Workers) world.Assign(w, null); mine.Output!.Add(World.MineOre(mine), 12); mine.Output.Add("stone", 12); }
        world.S.Time = world.Data.DaySeconds * (launchArgs.Contains("--night") ? .55f : .2f);
        for (int i = 0; i < 80; i++) world.Tick(.25f);
        if (launchArgs.Contains("--panel")) Select(mine); else ClosePanel();
        // Review from between the cart and portal, so either cliff-facing orientation fits in view.
        world.Player = mine.Pos - cliff.Face * .8f;
        view.Zoom = 1.25f;
    }
    private void Showcase() {
        if (launchArgs.Contains("--environment-review")) {
            string location = LaunchValue("--environment-review") ?? "forest";
            float coastAcross = Terrain.Across(Terrain.Spawn);
            var coastCenter = Terrain.Point(Terrain.Coast(coastAcross) - 2, coastAcross);
            var reviewCliff = World.CliffOf(Kinds.Parse(LaunchValue("--mine-ore")) ?? ThingKind.Coal);
            var mineCenter = World.MineSpot(reviewCliff) - reviewCliff.Face * 1.4f;
            world.Player = location switch {
                "lake" => Terrain.WaterPoint(Terrain.Lake + Terrain.Seaward) + Terrain.Seaward,
                "coast" => coastCenter - Terrain.Seaward * 3,
                "mine" => World.MineSpot(reviewCliff) + reviewCliff.Face,
                "terrain" => Terrain.Point(125, -18),
                _ => Terrain.Spawn,
            };
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear();
            world.Tick(0);
            view.EnvironmentReviewFog = launchArgs.Contains("--review-fog");
            view.EnvironmentCenter = location switch { "lake" => Terrain.Lake + Terrain.Seaward * 3, "terrain" => Terrain.Point(125, -18), "coast" => coastCenter, "mine" => mineCenter, _ => null };
            if (location == "mine") {
                var mine = world.Build(ThingKind.Mine, World.MineSpot(reviewCliff))!;
                mine.MineStage = MineStage.Open;
                mine.Output!.Add(World.MineOre(mine), 12);
            }
            // The wide terrain camera must sit above the peaks rather than inside a near mountain face.
            if (location is "terrain" or "coast" or "mine") view.EnvironmentCameraDistance = 140;
            view.Zoom = location switch { "lake" => .44f, "terrain" => .24f, "coast" => .36f, "mine" => .85f, _ => .75f };
            ClosePanel(); ui.Visible = false; state.Paused = true;
            return;
        }
        if (launchArgs.Contains("--bushes")) {
            // A clean three-bush comparison row, with one picked-clean bush to show its persistent model.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Bush variants are selected from the id; start the row at variant zero and include all three.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            world.Add(ThingKind.Berries, at + new Vector2(-2.25f, 2.25f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(-.75f, .75f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(.75f, -.75f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(2.25f, -2.25f), 0);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(-1, -.5f);
            world.Tick(0); view.Zoom = 1.8f;
            ClosePanel(); return;
        }
        if (launchArgs.Contains("--trees")) {
            // A clean three-tree comparison row, placed beyond the settlement showcase.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Tree variants are selected from the id: align the first id to oak, then add spruce and birch.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            world.Add(ThingKind.Tree, at + new Vector2(-3, 3), 42);
            world.Add(ThingKind.Tree, at, 42);
            world.Add(ThingKind.Tree, at + new Vector2(3, -3), 42);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(1, -1);
            if (launchArgs.Contains("--tree-shadow")) world.Player = at - Vector2.One * 2;
            world.Tick(0); view.Zoom = 1;
            ClosePanel(); return;
        }
        foreach (var t in world.S.Things.Where(t => t.Pos.DistanceTo(Terrain.Spawn) < 8).ToArray()) world.S.Things.Remove(t);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(-2, -2)); var fire = world.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(1, -3)); fire.Burn = 200;
        var f = world.Add(ThingKind.Furnace, Terrain.Spawn + new Vector2(4, -2)); f.Inventory.Add("iron_ore", 12); f.Inventory.Add("wood", 6);
        var bellows = world.Add(ThingKind.Reinforced, Terrain.Spawn + new Vector2(6, 1)); bellows.Inventory.Add("iron_bar", 12); bellows.Inventory.Add("coal", 20); bellows.Burner!.Add("coal", 10); bellows.Heat = 100;
        var chest = world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 3)); chest.Inventory.Add("wood", 35); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_bar", 15);
        world.Add(ThingKind.Anvil, Terrain.Spawn + new Vector2(4, 4)); var sign = world.Add(ThingKind.Signpost, Terrain.Spawn + new Vector2(-1, 4));
        world.S.Backpack.Add(Inventory.Coins, 140); world.S.Hunger = 55; world.S.Thirst = 30; world.S.Backpack.Add("water", 4); world.S.Backpack.Add("steel_pick", 1);
        var bench = world.Add(ThingKind.Bench, Terrain.Spawn + new Vector2(-4, 1.5f)); bench.Recipe = "crate"; bench.Inventory.Add("planks", 12); bench.Tools!.Add("stone_chisel", 1); bench.Tools.Add("steel_hammer", 1);
        var rack = world.Add(ThingKind.Toolstation, Terrain.Spawn + new Vector2(-5, -1.5f)); rack.Inventory.Add("steel_axe", 1); rack.Inventory.Add("rake", 1);
        world.Add(ThingKind.Leaves, Terrain.Spawn + new Vector2(-3.5f, 5), World.LeafPatch);
        var crate = world.Add(ThingKind.Crate, Terrain.Spawn + new Vector2(1.5f, 1.5f)); crate.Inventory.Add("berries", 6); crate.Inventory.Add("steel_axe", 1); crate.Inventory.Add("steel_pick", 1);
        foreach (var (item, count) in new[] { ("steel_axe", 1), ("steel_pick", 1), ("stone_chisel", 1), ("wood", 9), ("planks", 8), ("wood_parts", 3), ("crate", 1), ("torch", 2) }) world.S.Backpack.Add(item, count);
        world.S.Milestones.UnionWith(new[] { "gather", "bench", "crate", "sold", "pick", "furnace", "iron_bar", "reinforced", "steel_bar", "anvil", "steel_tool" });
        // The review fixture is deliberately separate from a fresh playable game.
        world.Player = sign.Pos + Vector2.One; var offer = world.Data.HireOffers[1];
        var w1 = world.Hire(sign, offer); var w2 = world.Hire(sign, offer); var w3 = world.Hire(sign, offer);
        if (w1 != null) world.Assign(w1, bellows); if (w2 != null) world.Assign(w2, bellows); if (w3 != null) world.Assign(w3, sign);
        // Three packed crates leave on the cart, so it should show three crates.
        foreach (var goods in new[] { "steel_axe", "berries", "raw_meat" }) {
            var box = world.Add(ThingKind.Crate, world.Player); box.Inventory.Add(goods, goods == "steel_axe" ? 2 : 12); world.PickUpCrate(box);
            world.S.Backpack.TransferTo(sign.Inventory, world.S.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix)), 1);
        }
        world.Sell(sign); world.S.Orders[0].Elapsed = 4;
        world.S.Crafting.AddRange(new[] { "planks", "wood_parts" }); world.S.CraftTime = 6;
        var meat = world.Add(ThingKind.Crate, world.Player); meat.Inventory.Add("burnt_steak", 12); world.PickUpCrate(meat);
        world.S.Backpack.TransferTo(sign.Inventory, world.S.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix)), 1); sign.Output!.Add(Inventory.Coins, 36);
        if (launchArgs.Contains("--night")) world.S.Time = world.Data.DaySeconds * .78f;
        // --day-phase sets the time of day from 0 at dawn, for comparing the light across the day.
        if (LaunchValue("--day-phase") is string phase) world.S.Time = world.Data.DaySeconds * float.Parse(phase, System.Globalization.CultureInfo.InvariantCulture);
        // A past walk toward the iron cliff leaves explored ground beyond the live sight circle.
        for (float d = 0; d <= 14; d += 2) { world.Player = Terrain.Spawn + new Vector2(d * .5f, -d); world.Tick(0); }
        world.Player = Terrain.Spawn; world.Tick(0); Select(bellows);
        // --hide-ui leaves only the world in review screenshots.
        if (launchArgs.Contains("--hide-ui")) ui.Visible = false;
        if (launchArgs.Contains("--market")) { world.Player = sign.Pos + Vector2.One; sheetOpen = true; Select(sign); }
        if (launchArgs.Contains("--bench")) { world.Player = bench.Pos + Vector2.One; Select(bench); }
        if (launchArgs.Contains("--craft")) OpenPage("craft");
        if (launchArgs.Contains("--crew")) OpenPage("workers");
        if (launchArgs.Contains("--worker")) Select(world.S.Workers[0]);
        if (launchArgs.Contains("--guide")) OpenPage("help");
        if (launchArgs.Contains("--menu")) { ClosePanel(); OpenMenu(); }
        if (launchArgs.Contains("--furnace")) { world.Player = bellows.Pos + Vector2.One; Select(bellows); }
        if (launchArgs.Contains("--piles")) {
            var at = Terrain.Spawn + new Vector2(-3, 9);
            foreach (var (item, stacks, dx) in new[] { ("stone", 14, 0), ("wood", 6, 3), ("leaves", 3, 6) }) {
                var pile = world.Add(ThingKind.Pile, at + new Vector2(dx, 0)); pile.Inventory.Add(item, stacks * pile.Inventory.StackSize);
                if (item == "leaves") { pile.Compost = true; pile.Inventory.Add("fertilizer", 20); }
            }
            world.Player = at + new Vector2(3, -2.5f); world.Tick(0); view.Zoom = 1.4f;
            if (launchArgs.Contains("--panel")) Select(world.S.Things.Last(t => t.Kind == ThingKind.Pile)); else ClosePanel();
        }
        if (launchArgs.Contains("--farm")) {
            // A row of plots, one per look: bare, sown soil, straws, green plants and ripe wheat half harvested, beside a pile of wheat.
            var at = Terrain.Spawn + new Vector2(-6, 9);
            for (int i = 0; i < 5; i++) {
                var plot = world.Add(ThingKind.Farm, at + new Vector2(i * World.ZoneSize, 0));
                plot.FarmStage = i switch { 0 => FarmStage.Fallow, 4 => FarmStage.Ripe, _ => FarmStage.Growing };
                plot.Progress = i == 4 ? world.HarvestSeconds / 2 : (i - 1) * world.GrowSeconds / 3 + 1;
            }
            var wheat = world.Add(ThingKind.Pile, at + new Vector2(0, World.ZoneSize)); wheat.Inventory.Add("wheat", 300);
            world.Player = at + new Vector2(6, 2.5f); world.Tick(0); view.Zoom = 1.2f;
            if (launchArgs.Contains("--panel")) Select(world.S.Things.Last(t => t.Kind == ThingKind.Farm)); else ClosePanel();
        }
        if (launchArgs.Contains("--kitchen")) {
            // The cooking table mid-stew beside a canning station sealing cans, with the mortar slotted.
            var at = Terrain.Spawn + new Vector2(-3, 8);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 4 && !t.IsResource);
            var table = world.Add(ThingKind.Cooktable, at); table.Tools!.Add("mortar", 1); world.SetRecipe(table, "meat_stew");
            table.Inventory.Add("water", 2); table.Inventory.Add("raw_meat", 2); table.Burner!.Add("wood", 2);
            var cannery = world.Add(ThingKind.Cannery, at + new Vector2(2.2f, 0)); world.SetRecipe(cannery, "canned_meat_stew");
            cannery.Inventory.Add("meat_stew", 2); cannery.Inventory.Add("tin_can", 2); cannery.Burner!.Add("wood", 2);
            world.Player = at + new Vector2(1, 2.5f); world.Tick(.5f); view.Zoom = 1.7f;
            if (launchArgs.Contains("--panel")) Select(table); else ClosePanel();
        }
        if (launchArgs.Contains("--packing")) {
            // A packing station part way through a crate of bread, with bread and empty crates waiting, beside the trading post asking for crates of bread and berries.
            var at = Terrain.Spawn + new Vector2(3, 8);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 4 && !t.IsResource);
            var packer = world.Add(ThingKind.Packer, at); world.SetPacking(packer, "bread");
            packer.Inventory.Add("bread", 20); packer.Inventory.Add("crate", 3);
            world.Player = at + new Vector2(1, 1.5f); view.Zoom = 1.7f;
            for (int i = 0; i < 7; i++) world.Tick(world.Data.PackSeconds);
            // --crates N leaves that many empty crates at the station, and --unpacked takes its bread so packing stops.
            if (LaunchValue("--crates") is string crates) { packer.Inventory.Remove("crate", 99); packer.Inventory.Add("crate", int.Parse(crates, System.Globalization.CultureInfo.InvariantCulture)); }
            if (launchArgs.Contains("--unpacked")) { packer.Inventory.Remove("bread", 99); world.Tick(.1f); }
            if (launchArgs.Contains("--crates") || launchArgs.Contains("--unpacked")) { world.Player = at + new Vector2(-1.4f, -1.4f); view.Zoom = 2.6f; }
            // --packer puts a worker at the table packing, with the player stepped back.
            if (launchArgs.Contains("--packer")) { world.S.Backpack.Add(Inventory.Coins, 500); world.Player = sign.Pos + Vector2.One; }
            if (launchArgs.Contains("--packer") && world.Hire(sign, world.Data.HireOffers[1]) is Worker packing) {
                world.SetDuty(packing, packer, "craft", true); packing.Pos = at + World.PackStand;
                world.Player = at + new Vector2(.4f, -2.6f); view.Zoom = 3.4f; world.Tick(.1f);
            }
            world.SetFilter(sign, World.CrateOf("bread"), false, true); world.SetFilter(sign, World.CrateOf("berries"), false, true); world.SetAutoSell(sign, true);
            if (launchArgs.Contains("--post")) { world.Player = sign.Pos + Vector2.One; Select(sign); filterOpen = true; OpenPage("inspect"); } else if (launchArgs.Contains("--panel")) { packPicking = launchArgs.Contains("--pick"); Select(packer); } else ClosePanel();
        }
        if (launchArgs.Contains("--tools")) { world.Player = rack.Pos + new Vector2(2.5f, 2.5f); view.Zoom = 1.6f; if (launchArgs.Contains("--panel")) Select(rack); else ClosePanel(); }
        if (launchArgs.Contains("--tin")) { var cliff = World.CliffOf(ThingKind.Tin); world.Player = cliff.Center + cliff.Face * 5.5f; world.Tick(0); ClosePanel(); }
        if (launchArgs.Contains("--pantry")) {
            foreach (var id in new[] { "tin_ore", "tin_bar", "tin_can", "steel_chisel", "rake", "mortar", "leaves", "grass", "fertilizer", "wheat_seeds", "wheat", "flour", "dough", "bread", "crushed_berries", "berry_jam", "meat_stew", "burnt_steak", "canned_jam", "canned_meat_stew" }) world.S.Backpack.Add(id, 3);
            OpenPage("inventory");
        }
        // Hovering a recipe shows its description below the grids and as a tooltip at the pointer.
        if (launchArgs.Contains("--hover")) GetTree().CreateTimer(.5).Timeout += () => {
            if (Named("craft_torch") is not Button cell) return;
            cell.EmitSignal(Control.SignalName.MouseEntered);
            var at = GetViewport().GetFinalTransform() * cell.GetGlobalRect().GetCenter();
            Input.WarpMouse(at); Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        };
        if (launchArgs.Contains("--torch")) { world.Player = Terrain.Spawn + new Vector2(-5, 6); world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); if (!launchArgs.Contains("--kindle")) world.Tick(World.KindleSeconds); ClosePanel(); view.Zoom = 1.6f; }
        if (launchArgs.Contains("--crate")) { world.Player = crate.Pos + Vector2.One; Select(crate); }
        if (launchArgs.Contains("--anomaly")) { world.Player = crate.Pos + Vector2.One; blackHole.CrateIntoCrate(crate, crate.Inventory, "crate"); }
        if (launchArgs.Contains("--collapse")) { world.Player = crate.Pos + Vector2.One; ClosePanel(); blackHole.CrateIntoCrate(crate, crate.Inventory, "crate"); blackHole.Collapse(crate); }
        if (launchArgs.Contains("--lunch")) {
            // Lunch review: the crew has walked over and sat down around the campfire, with the pay overview open.
            world.S.Time = (world.Data.LunchHour - 6) / 24 * world.Data.DaySeconds;
            for (int i = 0; i < 60; i++) world.Tick(.25f);
            OpenPage("workers");
        }
        if (launchArgs.Contains("--strike")) {
            // Strike review: the whole crew owes wages and stands holding protest signs.
            foreach (var w in world.S.Workers) w.WageDebt = w.Wage;
            world.Player = sign.Pos + new Vector2(2.5f, 1.5f); world.Tick(.25f); ClosePanel(); view.Zoom = 2.2f;
        }
        if (launchArgs.Contains("--mine")) MineReview(sign);
        if (!launchArgs.Contains("--wolves")) return;
        // Night ambush review: a wounded player, one wolf in leaping range, the pack further out and a fresh carcass.
        var spot = Terrain.Spawn + new Vector2(11, 8);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(spot) < 6);
        world.S.Time = world.Data.DaySeconds * .78f; world.Player = spot; world.S.Health = 45;
        foreach (var offset in new[] { new Vector2(2.5f, -1), new Vector2(-6, 3), new Vector2(5, 5) }) world.S.Wolves.Add(new Wolf { Pos = spot + offset });
        world.Add(ThingKind.WolfCorpse, spot + new Vector2(-2, -2), 4);
        ClosePanel(); view.Zoom = 1.6f;
    }
}
