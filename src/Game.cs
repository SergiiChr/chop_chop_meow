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
    private Thing? gathering;
    /// <summary>A crate held under the right mouse button; it is lifted once held for <see cref="LiftSeconds"/>.</summary>
    private Thing? lifting;
    private float liftClock;
    private const float LiftSeconds = .6f;
    private UiKit kit = null!;
    private SidePanel panel = null!;
    private BackpackDrawer bag = null!;
    private Hotbar hotbar = null!;
    private EndingScreen ending = null!;
    private BlackHole blackHole = null!;
    private ItemMoves moves = null!;
    private RecoveryPrompt recovery = null!;
    private UiState state = null!;
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
    private bool CanPlaceAgain() => view.BuildMode == ThingKind.Crate ? world.S.Backpack[state.PlacingCrate] > 0 : view.BuildMode is ThingKind kind && world.S.Backpack.Has(world.Data.Buildings[kind].Cost);
    private void Restart() {
        net.Leave();
        world.NewGame();
        state.Selected = 0; state.WorkerId = 0; gathering = null; state.AssigningWorker = 0; saves.Slot = 0; autosave = 0;
        view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0;
        ending.Visible = false; state.Paused = false; panel.Open("welcome");
        Notify("Welcome. WASD to move; hold right mouse to approach and gather. I shows the field guide.");
    }
    /// <summary>Clears what belonged to the world before a load, and shows what was loaded.</summary>
    private void Loaded() {
        state.Selected = 0; state.WorkerId = 0; gathering = null; state.Entering = null; state.AssigningWorker = 0; view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0; panel.Open("inventory");
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
        if (ev is InputEventMouseButton mouse) {
            // A panel that has nothing to scroll passes the wheel on; over any panel it must not zoom the world behind.
            bool overPanel = GetViewport().GuiGetHoveredControl() != null;
            if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed && !overPanel) view.Zoom = Math.Min(1.6f, view.Zoom + .1f);
            if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed && !overPanel) view.Zoom = Math.Max(.55f, view.Zoom - .1f);
            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed) {
                var t = view.Pick(mouse.Position); if (t?.IsResource == true || t?.Kind == ThingKind.Farm) { gathering = t; state.Selected = t.Id; view.Selected = t.Id; } else gathering = null;
                lifting = t?.Kind == ThingKind.Crate ? t : null; liftClock = 0;
            }
            if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) {
                if (view.BuildMode != null) {
                    var at = view.ToWorld(mouse.Position).Round();
                    var built = view.BuildMode == ThingKind.Crate ? world.PlaceCrate(state.PlacingCrate, at) : world.Build(view.BuildMode.Value, at);
                    // Placement stays on without opening what was placed, so several go down in a row until the materials or crates run out.
                    // A guest's building goes up on the host, so the guest stays in placement until Esc, except after a mine the host was sent.
                    // A mine opens its panel instead, since each cliff takes only one.
                    if (built?.Kind == ThingKind.Mine) { view.BuildMode = null; panel.Select(built); } else if (built != null && !CanPlaceAgain()) view.BuildMode = null;
                    else if (world.Remote && view.BuildMode == ThingKind.Mine && world.PlacementBlock(ThingKind.Mine, world.Snap(ThingKind.Mine, at)) == null) view.BuildMode = null;
                    return;
                }
                var t = view.Pick(mouse.Position);
                if (state.AssigningWorker > 0) {
                    var worker = world.S.Workers.FirstOrDefault(w => w.Id == state.AssigningWorker);
                    if (worker != null && t?.IsResource == true) {
                        string? blocked = world.GatherBlock(t, true);
                        if (blocked != null) Notify(blocked); else { world.SetDuty(worker, t, "gather", true); state.AssigningWorker = 0; panel.Select(worker); }
                    } else Notify("Choose a tree, leaves, stone, ore, coal, berry bush or water source.");
                    return;
                }
                var w = view.PickWorker(mouse.Position);
                if (w != null) panel.Select(w);
                else if (t != null) {
                    if (t.Kind == ThingKind.Hut) state.Entering = t;
                    // A crate in hand only opens a clicked crate: the nesting alert is for deliberate moves inside the inventory.
                    if (t.IsStorage && hotbar.ActiveItem() is string item && hotbar.IsTransferable(item) && world.Holds(t, item) && !(t.Kind == ThingKind.Crate && World.IsCrate(item))) {
                        if (world.Near(t)) { int moved = 0; kit.Act(() => moved = world.Transfer(world.S.Backpack, t.Inventory, item, ItemMoves.All ? world.S.Backpack[item] : 1)); if (!world.Remote && moved == 0) Notify($"No {world.Data.Name(item)} to store, or chest is full."); } else Notify("Walk closer to the chest to store the state.Selected stack.");
                    }
                    panel.Select(t);
                } else if (hotbar.ActiveItem() is string held && hotbar.HasUse(held)) hotbar.UseHeld(view.ToWorld(mouse.Position));
                else panel.Close();
            }
        }
    }
    private static readonly bool smokeTest = Launch.Has("--smoke-test");
    private static readonly string? screenshotPath = Launch.Value("--screenshot");
    private static readonly float shotDelay = float.TryParse(Launch.Value("--shot-delay"), System.Globalization.CultureInfo.InvariantCulture, out float wait) ? wait : 2;
    public override void _Process(double delta) {
        if (world == null || view == null) return;
        float dt = Math.Min((float)delta, .1f); runTime += dt;
        if (!state.Paused) {
            float x = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
            float y = (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
            Vector2 direction = new Vector2(x + y * 2, y * 2 - x).Normalized();
            bool right = Input.IsMouseButtonPressed(MouseButton.Right);
            // A clicked cabin draws the player to its door, even with a movement key still held from walking there.
            if (world.Downed || state.Entering != null && world.Find(state.Entering.Id) == null) state.Entering = null;
            if (state.Entering != null) { if (world.Enter(state.Entering)) state.Entering = null; else direction = (state.Entering.Pos - world.Player).Normalized(); }
            // A loose piece needs only one click: the player keeps walking to it after the button is released.
            if (gathering != null && (right || gathering.IsLoose) && direction == Vector2.Zero && state.Entering == null) {
                // Step in close enough for the tool to reach the model; the lake edge keeps its wider reach.
                if (!world.Near(gathering, gathering.Kind == ThingKind.Water ? 2.5f : 1.35f)) direction = (world.GatherAt(gathering, world.Player) - world.Player).Normalized();
                else if (gathering.IsLoose) {
                    world.Gather(gathering, dt);
                    if (world.Find(gathering.Id) == null && state.Selected == gathering.Id) { state.Selected = 0; view.Selected = 0; }
                    gathering = null;
                } else {
                    // Emptying it shows on the stump or bare bush; a target that was already empty or out of reach still says why.
                    bool had = gathering.Remaining > 0; world.Gather(gathering, dt);
                    if (world.GatherBlock(gathering) is string blocked) { if (!had || gathering.Remaining > 0) Notify(blocked); gathering = null; }
                }
            } else { world.ResetGather(); if (!right || gathering?.IsLoose == true) gathering = null; }
            // A held right click walks to a crate and lifts it once the button has been held long enough.
            if (lifting != null && right && direction == Vector2.Zero && state.Entering == null && world.Find(lifting.Id) != null) {
                if (!world.Near(lifting, 1.5f)) direction = (lifting.Pos - world.Player).Normalized();
                else if ((liftClock += dt) >= LiftSeconds) {
                    var crate = lifting; lifting = null;
                    if (world.PickUpCrate(crate) && state.Selected == crate.Id) { state.Selected = 0; view.Selected = 0; if (state.Page == "inspect") panel.Close(); }
                }
            } else { lifting = null; liftClock = 0; }
            view.Lifting = lifting?.Id ?? 0; view.LiftProgress = liftClock / LiftSeconds;
            // Under 15% food or drink the player is too weak to sprint.
            world.Me.Sprinting = direction != Vector2.Zero && !world.Downed && Input.IsPhysicalKeyPressed(Key.Shift) && world.S.Hunger >= GameData.LowNeed && world.S.Thirst >= GameData.LowNeed;
            world.MovePlayer(direction * dt * world.Data.WalkSpeed * (world.Me.Sprinting ? world.Data.SprintSpeed : 1)); view.Gathering = gathering is { IsLoose: false } && right && world.Near(gathering, 2.5f);
            view.GatheringTarget = view.Gathering ? gathering : null;
            world.Me.Held = hotbar.ActiveItem() ?? "";
        } else world.Me.Sprinting = false;
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
