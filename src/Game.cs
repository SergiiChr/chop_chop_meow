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
    /// <summary>Health as last shown, so a guest flashes the damage its updates bring.</summary>
    private float shownHealth;
    private float runTime;
    private bool capturing;
    private bool quitting;
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
                panel.Close(); ui.Visible = false; view.AnimationReview = true;
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
    private void BuildHud() {
        blackHole = new BlackHole(kit, view, ui.Theme, () => panel.Close(), LatestSlot, LoadSlot, ShowStart); AddChild(blackHole);
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
        ending = new EndingScreen(kit, panel.Close, Restart); ui.AddChild(ending); BuildMenu(); BuildScreens();
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
        state.Selected = 0; state.WorkerId = 0; gathering = null; state.AssigningWorker = 0; slot = 0; autosave = 0;
        view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0;
        ending.Visible = false; state.Paused = false; panel.Open("welcome");
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
            slot = n; state.Selected = 0; state.WorkerId = 0; gathering = null; state.Entering = null; state.AssigningWorker = 0; view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0; panel.Open("inventory");
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
                    if (view.BuildMode != null || state.AssigningWorker > 0) { view.BuildMode = null; state.AssigningWorker = 0; } else if (panel.Visible || bag.Visible) panel.Close(); else OpenMenu();
                    RefreshLive(); break;
                case Key.B: panel.Open("build"); break;
                case Key.I: if (state.Page == "help") panel.Close(); else panel.Open("help"); break;
                case Key.H: if (state.Page == "workers") panel.Close(); else panel.Open("workers"); break;
                case Key.C: if (state.Page == "craft") panel.Close(); else panel.Open("craft"); break;
                case Key.Tab: if (state.Page == "inventory") panel.Close(); else panel.Open("inventory"); break;
                // Until a numbered slot is chosen, quick save and load ask which slot to use.
                case Key.F5: if (world.Remote) { net.SaveGuest(); Notify("Your player is saved on this computer. The host saves the world."); } else if (slot > 0) Save(slot); else ShowSlots(true, HideScreen); break;
                case Key.F9: if (world.Remote) Notify("Leave the host's game to load your own."); else if (slot > 0) Load(slot); else ShowSlots(false, HideScreen); break;
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
            if (autosave >= AutosaveSeconds) { if (!world.Remote && !performanceRunning) Save(0); autosave = 0; }
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
        if (launchArgs.Contains("--panel")) panel.Select(mine); else panel.Close();
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
            panel.Close(); ui.Visible = false; state.Paused = true;
            return;
        }
        if (launchArgs.Contains("--bushes")) {
            // A clean three-bush comparison row, with one picked-clean bush to show its persistent model.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Bush variants are state.Selected from the id; start the row at variant zero and include all three.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            world.Add(ThingKind.Berries, at + new Vector2(-2.25f, 2.25f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(-.75f, .75f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(.75f, -.75f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(2.25f, -2.25f), 0);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(-1, -.5f);
            world.Tick(0); view.Zoom = 1.8f;
            panel.Close(); return;
        }
        if (launchArgs.Contains("--trees")) {
            // A clean three-tree comparison row, placed beyond the settlement showcase.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Tree variants are state.Selected from the id: align the first id to oak, then add spruce and birch.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            world.Add(ThingKind.Tree, at + new Vector2(-3, 3), 42);
            world.Add(ThingKind.Tree, at, 42);
            world.Add(ThingKind.Tree, at + new Vector2(3, -3), 42);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(1, -1);
            if (launchArgs.Contains("--tree-shadow")) world.Player = at - Vector2.One * 2;
            world.Tick(0); view.Zoom = 1;
            panel.Close(); return;
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
        world.Player = Terrain.Spawn; world.Tick(0); panel.Select(bellows);
        // --hide-ui leaves only the world in review screenshots.
        if (launchArgs.Contains("--hide-ui")) ui.Visible = false;
        if (launchArgs.Contains("--market")) { world.Player = sign.Pos + Vector2.One; panel.Memory.SheetOpen = true; panel.Select(sign); }
        if (launchArgs.Contains("--bench")) { world.Player = bench.Pos + Vector2.One; panel.Select(bench); }
        if (launchArgs.Contains("--craft")) panel.Open("craft");
        if (launchArgs.Contains("--crew")) panel.Open("workers");
        if (launchArgs.Contains("--worker")) panel.Select(world.S.Workers[0]);
        if (launchArgs.Contains("--guide")) panel.Open("help");
        if (launchArgs.Contains("--menu")) { panel.Close(); OpenMenu(); }
        if (launchArgs.Contains("--furnace")) { world.Player = bellows.Pos + Vector2.One; panel.Select(bellows); }
        if (launchArgs.Contains("--piles")) {
            var at = Terrain.Spawn + new Vector2(-3, 9);
            foreach (var (item, stacks, dx) in new[] { ("stone", 14, 0), ("wood", 6, 3), ("leaves", 3, 6) }) {
                var pile = world.Add(ThingKind.Pile, at + new Vector2(dx, 0)); pile.Inventory.Add(item, stacks * pile.Inventory.StackSize);
                if (item == "leaves") { pile.Compost = true; pile.Inventory.Add("fertilizer", 20); }
            }
            world.Player = at + new Vector2(3, -2.5f); world.Tick(0); view.Zoom = 1.4f;
            if (launchArgs.Contains("--panel")) panel.Select(world.S.Things.Last(t => t.Kind == ThingKind.Pile)); else panel.Close();
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
            if (launchArgs.Contains("--panel")) panel.Select(world.S.Things.Last(t => t.Kind == ThingKind.Farm)); else panel.Close();
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
            if (launchArgs.Contains("--panel")) panel.Select(table); else panel.Close();
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
            if (launchArgs.Contains("--post")) { world.Player = sign.Pos + Vector2.One; panel.Select(sign); panel.Memory.FilterOpen = true; panel.Open("inspect"); } else if (launchArgs.Contains("--panel")) { panel.Memory.PackPicking = launchArgs.Contains("--pick"); panel.Select(packer); } else panel.Close();
        }
        if (launchArgs.Contains("--tools")) { world.Player = rack.Pos + new Vector2(2.5f, 2.5f); view.Zoom = 1.6f; if (launchArgs.Contains("--panel")) panel.Select(rack); else panel.Close(); }
        if (launchArgs.Contains("--tin")) { var cliff = World.CliffOf(ThingKind.Tin); world.Player = cliff.Center + cliff.Face * 5.5f; world.Tick(0); panel.Close(); }
        if (launchArgs.Contains("--pantry")) {
            foreach (var id in new[] { "tin_ore", "tin_bar", "tin_can", "steel_chisel", "rake", "mortar", "leaves", "grass", "fertilizer", "wheat_seeds", "wheat", "flour", "dough", "bread", "crushed_berries", "berry_jam", "meat_stew", "burnt_steak", "canned_jam", "canned_meat_stew" }) world.S.Backpack.Add(id, 3);
            panel.Open("inventory");
        }
        // Hovering a recipe shows its description below the grids and as a tooltip at the pointer.
        if (launchArgs.Contains("--hover")) GetTree().CreateTimer(.5).Timeout += () => {
            if (Named("craft_torch") is not Button cell) return;
            cell.EmitSignal(Control.SignalName.MouseEntered);
            var at = GetViewport().GetFinalTransform() * cell.GetGlobalRect().GetCenter();
            Input.WarpMouse(at); Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        };
        if (launchArgs.Contains("--torch")) { world.Player = Terrain.Spawn + new Vector2(-5, 6); world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); if (!launchArgs.Contains("--kindle")) world.Tick(World.KindleSeconds); panel.Close(); view.Zoom = 1.6f; }
        if (launchArgs.Contains("--crate")) { world.Player = crate.Pos + Vector2.One; panel.Select(crate); }
        if (launchArgs.Contains("--anomaly")) { world.Player = crate.Pos + Vector2.One; blackHole.CrateIntoCrate(crate, crate.Inventory, "crate"); }
        if (launchArgs.Contains("--collapse")) { world.Player = crate.Pos + Vector2.One; panel.Close(); blackHole.CrateIntoCrate(crate, crate.Inventory, "crate"); blackHole.Collapse(crate); }
        if (launchArgs.Contains("--lunch")) {
            // Lunch review: the crew has walked over and sat down around the campfire, with the pay overview open.
            world.S.Time = (world.Data.LunchHour - 6) / 24 * world.Data.DaySeconds;
            for (int i = 0; i < 60; i++) world.Tick(.25f);
            panel.Open("workers");
        }
        if (launchArgs.Contains("--strike")) {
            // Strike review: the whole crew owes wages and stands holding protest signs.
            foreach (var w in world.S.Workers) w.WageDebt = w.Wage;
            world.Player = sign.Pos + new Vector2(2.5f, 1.5f); world.Tick(.25f); panel.Close(); view.Zoom = 2.2f;
        }
        if (launchArgs.Contains("--mine")) MineReview(sign);
        if (!launchArgs.Contains("--wolves")) return;
        // Night ambush review: a wounded player, one wolf in leaping range, the pack further out and a fresh carcass.
        var spot = Terrain.Spawn + new Vector2(11, 8);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(spot) < 6);
        world.S.Time = world.Data.DaySeconds * .78f; world.Player = spot; world.S.Health = 45;
        foreach (var offset in new[] { new Vector2(2.5f, -1), new Vector2(-6, 3), new Vector2(5, 5) }) world.S.Wolves.Add(new Wolf { Pos = spot + offset });
        world.Add(ThingKind.WolfCorpse, spot + new Vector2(-2, -2), 4);
        panel.Close(); view.Zoom = 1.6f;
    }
}
