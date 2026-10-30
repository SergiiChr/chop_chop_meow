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
    private ClockPlate clock = null!;
    private MessageStack messages = null!;
    private NeedsPlate needsPlate = null!;
    private DamageEdges damageEdges = null!;
    private WaterWarning waterWarning = null!;
    private ModeLabel modeLabel = null!;
    private DeathLabel deathLabel = null!;
    private UiKit kit = null!;
    private SidePanel panel = null!;
    private BackpackDrawer bag = null!;
    private Hotbar hotbar = null!;
    private EndingScreen ending = null!;
    private BlackHole blackHole = null!;
    private ItemMoves moves = null!;
    private RecoveryPrompt recovery = null!;
    private UiState state = null!;
    private PlayerControl player = null!;
    private float uiClock;
    private float autosave;
    private const float AutosaveSeconds = 300;
    private Net net = null!;
    private Saves saves = null!;
    private StartMenu startMenu = null!;
    private PauseMenu menu = null!;
    /// <summary>Health as last shown, so a guest flashes the damage its updates bring.</summary>
    private float shownHealth;
    private float runTime;
    private bool capturing;
    private bool quitting;
    public override void _Ready() {
        try {
            var data = GameData.Load(); world = Launch.Has("--performance-run") ? PerformanceFixture.Create(data) : new World(data);
            if (Launch.Has("--self-test")) { SimulationChecks.Run(data); GetTree().Quit(); return; }
            if (Launch.Value("--render-icons") is string icons) { AddChild(new IconRender { Data = data, Folder = icons }); return; }
            world.Notify = Notify;
            sounds = new Sounds { World = world, MusicVolume = Settings.MusicVolume }; AddChild(sounds);
            // Closing the window goes through Quit, which lets playing sounds stop first.
            GetTree().AutoAcceptQuit = false;
            view = new WorldView { World = world, Sounds = sounds, EnvironmentReview = Launch.Has("--environment-review") }; AddChild(view);
            kit = new UiKit(world, sounds, Notify, RefreshLive); state = kit.State;
            net = new Net { Name = "Net", World = world, FilePrefix = Launch.FilePrefix }; AddChild(net);
            saves = new Saves(kit, net, Loaded);
            world.Sound = (name, at) => { PlayWorldSound(name, at); net.Echo(name, at); };
            world.PlayerActed += (action, at) => net.Echo(world.S.Actor.Id, action, at);
            ConnectNet();
            sounds.OffScreen = view.OffScreen;
            var layer = new CanvasLayer(); AddChild(layer);
            ui = new Control { Name = "Interface" }; ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(ui); ui.MouseFilter = Control.MouseFilterEnum.Ignore;
            ui.Theme = Style.Interface(); BuildHud();
            GetViewport().UseTaa = Settings.Taa;
            if (Launch.Has("--showcase")) new Showcase(world, view, panel, ui, blackHole, menu, kit.State).Run();
            else if (!Launch.Has("--animation-review")) startMenu.ShowStart();
            if (!Launch.Args.Any(a => a is "--showcase" or "--ui-test" or "--net-check" or "--animation-review" or "--performance-run")) { Settings.ApplyResolution(GetWindow(), Settings.Resolution); Engine.MaxFps = Settings.FrameLimit; }
            StartPerformanceRun();
            if (Launch.Has("--ui-test")) RunUIChecks();
            if (Launch.Has("--net-check")) RunNetCheck(Launch.Value("--net-check") == "host");
            if (Launch.Has("--animation-review")) {
                panel.Close(); ui.Visible = false; view.AnimationReview = true;
                if (Launch.Value("--animation-capture") is string captures) CaptureAnimations(captures);
            }
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private void PlayWorldSound(string name, Vector2 at) => sounds.Play(name, at, name == "wolf_growl_distance" ? 14 : Sounds.Reach);
    private void BuildHud() {
        blackHole = new BlackHole(kit, view, ui.Theme, () => panel.Close(), saves.Latest, n => startMenu.LoadSlot(n), () => startMenu.ShowStart()); AddChild(blackHole);
        moves = new ItemMoves(kit, blackHole);
        panel = new SidePanel(kit, view, moves, ui); bag = panel.Bag;
        hotbar = new Hotbar(kit, view, panel);
        player = new PlayerControl(kit, view, panel, hotbar);
        clock = new ClockPlate(world); ui.AddChild(clock);
        modeLabel = new ModeLabel(kit, view); ui.AddChild(modeLabel);
        ui.AddChild(hotbar);
        // _Process keeps it between the needs and bank plate on the left and the day dial or side panel on the right.
        messages = new MessageStack(() => Style.Outlined(Style.Wrapped("", 17))) { Name = "messages", AnchorRight = 1, OffsetTop = 56 }; ui.AddChild(messages);
        ui.AddChild(panel); ui.AddChild(bag);
        waterWarning = new WaterWarning(bag); ui.AddChild(waterWarning);
        deathLabel = new DeathLabel(world); ui.AddChild(deathLabel);
        damageEdges = new DamageEdges(); damageEdges.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        needsPlate = new NeedsPlate(kit, moves, damageEdges); ui.AddChild(needsPlate); ui.AddChild(damageEdges);
        ending = new EndingScreen(kit, panel.Close, Restart); ui.AddChild(ending);
        startMenu = new StartMenu(kit, saves, net, panel, ending, () => menu.Visible = false, Restart, () => Quit());
        menu = new PauseMenu(kit, net, startMenu, Restart, () => Quit(), () => { DropWorld(); startMenu.ShowStart(); }); ui.AddChild(menu); ui.AddChild(startMenu);
        recovery = new RecoveryPrompt(kit); ui.AddChild(recovery);
        RefreshLive();
    }
    private void Notify(string message) {
        if (message == Water.FullBottleWarning && waterWarning != null) waterWarning.Warn(message); else messages?.Post(message);
        GD.Print(message);
    }
    private void RefreshLive() {
        if (needsPlate == null) return;
        clock.Refresh(); deathLabel.Refresh(); modeLabel.Refresh();
        panel.Refresh();
        hotbar.Refresh(); needsPlate.Refresh(); bag.Refresh();
    }
    private void Restart() {
        net.Leave();
        world.NewGame();
        state.Selected = 0; state.WorkerId = 0; player.StopGathering(); state.AssigningWorker = 0; saves.Slot = 0; autosave = 0;
        view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0;
        ending.Visible = false; state.Paused = false; panel.Open("welcome");
        Notify("Welcome. WASD to move; hold right mouse to approach and gather. I shows the field guide.");
    }
    /// <summary>Clears what belonged to the world before a load, and shows what was loaded.</summary>
    private void Loaded() {
        state.Selected = 0; state.WorkerId = 0; player.StopGathering(); state.Entering = null; state.AssigningWorker = 0; view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0; panel.Open("inventory");
    }
    public override void _UnhandledInput(InputEvent ev) {
        if (view?.EnvironmentReview == true) return;
        if (world == null || view == null || ending.Visible) return;
        if (blackHole.Showing) { if (blackHole.Asking && ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) blackHole.Cancel(); return; }
        if (startMenu.Visible) { if (ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) startMenu.Back(); return; }
        if (menu.Visible) { if (ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) menu.Close(); return; }
        if (ev is InputEventKey key && key.Pressed && !key.Echo) {
            switch (key.PhysicalKeycode) {
                case Key.Escape:
                    if (view.BuildMode != null || state.AssigningWorker > 0) { view.BuildMode = null; state.AssigningWorker = 0; } else if (panel.Visible || bag.Visible) panel.Close(); else menu.Open();
                    RefreshLive(); break;
                case Key.B: panel.Open("build"); break;
                case Key.I: if (state.Page == "help") panel.Close(); else panel.Open("help"); break;
                case Key.H: if (state.Page == "workers") panel.Close(); else panel.Open("workers"); break;
                case Key.C: if (state.Page == "craft") panel.Close(); else panel.Open("craft"); break;
                case Key.Tab: if (state.Page == "inventory") panel.Close(); else panel.Open("inventory"); break;
                // Until a numbered slot is chosen, quick save and load ask which slot to use.
                case Key.F5: if (world.Remote) { net.SaveGuest(); Notify("Your player is saved on this computer. The host saves the world."); } else if (saves.Slot > 0) saves.Save(saves.Slot); else startMenu.ShowSlots(true, startMenu.Close); break;
                case Key.F9: if (world.Remote) Notify("Leave the host's game to load your own."); else if (saves.Slot > 0) saves.Load(saves.Slot); else startMenu.ShowSlots(false, startMenu.Close); break;
                case >= Key.Key1 and <= Key.Key9: hotbar.Select((int)(key.PhysicalKeycode - Key.Key1)); break;
                case Key.E:
                    var t = world.Find(state.Selected);
                    if (t == null || !world.Near(t)) t = world.S.Things.Where(t => world.Near(t)).OrderBy(t => t.Pos.DistanceTo(world.Player)).FirstOrDefault();
                    if (t != null) panel.Select(t); break;
                case Key.Space:
                    if (state.Paused) { Notify("Resume before using tools."); break; }
                    var station = world.Find(state.Selected);
                    if (station != null && station.Kind == ThingKind.Reinforced && world.Near(station)) world.Pump(station); else world.Attack(); break;
            }
        }
        if (ev is InputEventMouseButton mouse) player.Click(mouse, GetViewport().GuiGetHoveredControl() != null);
    }
    private static readonly bool smokeTest = Launch.Has("--smoke-test");
    private static readonly string? screenshotPath = Launch.Value("--screenshot");
    private static readonly float shotDelay = float.TryParse(Launch.Value("--shot-delay"), System.Globalization.CultureInfo.InvariantCulture, out float wait) ? wait : 2;
    public override void _Process(double delta) {
        if (world == null || view == null) return;
        float dt = Math.Min((float)delta, .1f); runTime += dt;
        player.Update(dt);
        // Menus pause only a game nobody else is in.
        bool running = !state.Paused || net.Hosting || world.Remote;
        if (running) {
            float health = world.Remote ? shownHealth : world.S.Health;
            if (!world.Remote) world.Tick(dt);
            ending.ShowOnce(); autosave += dt;
            needsPlate.DamageFlash(health - world.S.Health, runTime); shownHealth = world.S.Health;
            // A guest saves its player whenever it changes instead; the world is the host's to save.
            if (autosave >= AutosaveSeconds) { if (!world.Remote && !performanceRunning) saves.Save(0); autosave = 0; }
        }
        view.Paused = !running; view.SelectedTool = hotbar.ActiveItem();
        view.Mouse = GetViewport().GetMousePosition();
        var hover = view.Pick(view.Mouse); view.Hover = hover?.Id ?? 0; view.HoverWorker = view.PickWorker(view.Mouse)?.Id ?? 0;
        uiClock += dt; if (uiClock > .2f) { uiClock = 0; RefreshLive(); }
        // Every frame, so a page that just opened takes its size once its wrapped text has a width.
        panel.Fit();
        // The needs plate grows with the bank count, and an open side panel would cover the messages' right end.
        float messagesLeft = needsPlate.GetRect().End.X + 24, messagesRight = panel.Visible ? SidePanel.Left - 16 : -messagesLeft;
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
}
