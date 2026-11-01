using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The runner: creates the world and the interface's parts, holds them, routes input between them and runs the frame.
/// Each part owns its feature; what several share is in <see cref="UiKit"/>.
/// </summary>
public partial class Game : Node, IGameFlow {
    private static readonly bool smokeTest = Launch.Has("--smoke-test");
    private static readonly string? screenshotPath = Launch.Value("--screenshot");
    private static readonly float shotDelay = float.TryParse(Launch.Value("--shot-delay"), System.Globalization.CultureInfo.InvariantCulture, out float wait) ? wait : 2;
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
    private PlayerControl player = null!;
    private float uiClock;
    private Net net = null!;
    private GameData data = null!;
    /// <summary>The server running inside this process for the checks, which they set the world up through; real games run theirs in a process of its own.</summary>
    private Server? hosted;
    /// <summary>The save slot the game this window started came from, or -1 for a new game, until it joins.</summary>
    private int starting = -1;
    /// <summary>Whether the player had a menu open last frame, as the server was last told.</summary>
    private bool holding;
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
            data = GameData.Load();
            if (Launch.Has("--server")) { Server.Run(this, data); return; }
            world = Launch.Has("--performance-run") ? PerformanceFixture.Create(data) : new World(data);
            if (Launch.Has("--self-test")) { SimulationChecks.Run(data); GetTree().Quit(); return; }
            if (Launch.Value("--render-icons") is string icons) { AddChild(new IconRender { Data = data, Folder = icons }); return; }
            world.Notify = Notify;
            sounds = new Sounds { World = world, MusicVolume = Settings.MusicVolume }; AddChild(sounds);
            // Closing the window goes through Quit, which lets playing sounds stop first.
            GetTree().AutoAcceptQuit = false;
            view = new WorldView { World = world, Sounds = sounds, EnvironmentReview = Launch.Has("--environment-review") }; AddChild(view);
            kit = new UiKit(world, sounds, Notify, RefreshLive);
            net = new Net { Name = "Net", World = world, Key = Settings.MachineKey }; AddChild(net);
            saves = new Saves(kit, net, this);
            world.Sound = (name, at) => { PlayWorldSound(name, at); net.Echo(name, at); };
            world.PlayerActed += (action, at) => net.Echo(world.ActorId, action, at);
            ConnectNet();
            sounds.OffScreen = view.OffScreen;
            var layer = new CanvasLayer(); AddChild(layer);
            ui = new Control { Name = "Interface" }; ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(ui); ui.MouseFilter = Control.MouseFilterEnum.Ignore;
            ui.Theme = Style.Interface(); BuildHud();
            GetViewport().UseTaa = Settings.Taa;
            if (Launch.Has("--showcase")) new Showcase(world, view, panel, ui, blackHole, menu, kit).Run();
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
        blackHole = new BlackHole(kit, view, ui.Theme, this, saves, () => panel.Close()); AddChild(blackHole);
        moves = new ItemMoves(kit, blackHole);
        panel = new SidePanel(kit, view, moves, ui); bag = panel.Bag;
        hotbar = new Hotbar(kit, panel);
        player = new PlayerControl(kit, view, panel, hotbar); panel.Player = player;
        clock = new ClockPlate(world); ui.AddChild(clock);
        modeLabel = new ModeLabel(kit, player); ui.AddChild(modeLabel);
        ui.AddChild(hotbar);
        // _Process keeps it between the needs and bank plate on the left and the day dial or side panel on the right.
        messages = new MessageStack(() => Style.Outlined(Style.Wrapped("", 17))) { Name = "messages", AnchorRight = 1, OffsetTop = 56 }; ui.AddChild(messages);
        ui.AddChild(panel); ui.AddChild(bag);
        waterWarning = new WaterWarning(bag, ui); ui.AddChild(waterWarning);
        deathLabel = new DeathLabel(world); ui.AddChild(deathLabel);
        damageEdges = new DamageEdges(); damageEdges.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        needsPlate = new NeedsPlate(kit, moves, damageEdges); ui.AddChild(needsPlate); ui.AddChild(damageEdges);
        ending = new EndingScreen(kit, panel.Close, this); ui.AddChild(ending);
        startMenu = new StartMenu(kit, saves, net, panel, () => menu.Visible = false, this);
        menu = new PauseMenu(kit, net, startMenu, this); ui.AddChild(menu); ui.AddChild(startMenu);
        recovery = new RecoveryPrompt(kit); ui.AddChild(recovery);
        RefreshLive();
    }
    private void Notify(string message) {
        if (message == Water.FullBottleWarning && waterWarning != null) waterWarning.Warn(message); else messages?.Post(message);
        GD.Print(message);
    }
    private void RefreshLive() {
        if (recovery == null) return;
        clock.Refresh(); deathLabel.Refresh(); modeLabel.Refresh(); recovery.Refresh();
        panel.Refresh();
        hotbar.Refresh(); needsPlate.Refresh(); bag.Refresh();
    }
    private void ConnectNet() {
        net.Said += Notify;
        net.Heard += PlayWorldSound;
        net.Acted += view.Acted;
        net.Joined += OnJoined;
        net.Disconnected += ShowReconnecting;
        net.Reconnecting += left => startMenu.ShowRetry(left);
        net.Left += reason => { startMenu.LeftReason = reason; Leave(); };
        net.Failed += reason => startMenu.ShowJoinFailure(reason);
    }
    /// <summary>Shown while a dropped guest tries to get back in, in place of the host's world, which it can no longer act on.</summary>
    private void ShowReconnecting() { world.NewGame(); startMenu.ShowReconnecting(); }
    public bool Owns => net.Owning || playingHere;
    /// <summary>The UI checks play their games in this window's own world, without a server, so they can set it up directly.</summary>
    private static readonly bool hereOnly = Launch.Has("--ui-test");
    private bool playingHere;
    private void OnJoined(bool back) {
        ending.Joined(back);
        shownHealth = world.Health;
        if (!net.Owning) {
            Settings.Store("net", "address", net.Address);
            saves.Slot = 0;
            Notify(back ? "Reconnected to the host's settlement." : "Joined the host's settlement. The host's saves keep your belongings.");
        } else Begin(starting);
    }
    /// <summary>Opens a game this window started: a new one on the welcome page, a loaded one on the backpack.</summary>
    private void Begin(int slot) {
        if (slot < 0) {
            saves.Slot = 0; panel.Open(Page.Welcome);
            Notify("Welcome. WASD to move; hold right mouse to approach and gather. I shows the field guide.");
        } else {
            if (slot > 0) saves.Slot = slot;
            panel.Open(Page.Backpack); Notify("Settlement loaded.");
        }
    }
    public void Leave() { StopHosted(); playingHere = false; world.NewGame(); startMenu.ShowStart(); }
    public void ShowStart() { net.Leave(); Leave(); }
    void IGameFlow.Quit() => Quit();
    public void Restart() {
        if (hereOnly) { net.Leave(); world.NewGame(); PlayHere(-1); } else StartGame(-1);
    }

    /// <summary>Plays a game this window's world runs itself, as the UI checks do.</summary>
    private void PlayHere(int slot) {
        playingHere = true; startMenu.Close();
        Begin(slot);
    }
    /// <summary>Starts the game in a save slot; a slot that cannot load says why and leaves the game as it was.</summary>
    public void LoadSlot(int slot) {
        var (loaded, error) = Saves.Read(data, slot);
        if (error != null) { Notify(error); return; }
        if (hereOnly) { net.Leave(); world.Deserialize(loaded!.Serialize()); PlayHere(slot); return; }
        StartGame(slot);
    }

    /// <summary>Starts a server for a new game, or the one in <paramref name="slot"/>, and joins it as its owner.</summary>
    private void StartGame(int slot) {
        net.Leave(); StopHosted();
        string key = net.Key;
        // The network check runs the server inside this window, under a branch with its own network, so it can set its world up directly.
        if (Launch.Has("--net-check")) {
            var branch = new Node { Name = "Hosted" }; GetTree().Root.AddChild(branch);
            GetTree().SetMultiplayer(new SceneMultiplayer(), branch.GetPath());
            var main = new Node { Name = Name }; branch.AddChild(main);
            hosted = Server.Start(main, data, slot, key, Net.DefaultPort);
            hosted.Ended += StopHosted;
        } else {
            var arguments = new System.Collections.Generic.List<string> { "--headless" };
            // Outside an exported game the engine needs to be told where the project is.
            if (!OS.HasFeature("template")) arguments.AddRange(["--path", ProjectSettings.GlobalizePath("res://")]);
            arguments.Add("--"); arguments.AddRange(Server.Arguments(Net.DefaultPort, slot, key));
            if (OS.CreateProcess(OS.GetExecutablePath(), arguments.ToArray()) < 0) { Notify("Could not start the game."); return; }
        }
        starting = slot;
        var error = net.Join("127.0.0.1", Net.DefaultPort);
        if (error != Error.Ok) { Notify($"Could not start the game: {error}."); return; }
        net.Owning = true;
    }

    /// <summary>Ends the server running inside this window, if any.</summary>
    private void StopHosted() {
        if (hosted is not Server server) return;
        hosted = null;
        // Its line closes now, so its port is free for the next game at once.
        server.Stop();
        var branch = server.GetParent().GetParent();
        GetTree().SetMultiplayer(null, branch.GetPath());
        branch.QueueFree();
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
                    if (player.Mode is not ClickMode.Normal) player.Mode = new ClickMode.Normal(); else if (panel.Visible || bag.Visible) panel.Close(); else menu.Open();
                    RefreshLive(); break;
                case Key.B: panel.Open(Page.Build); break;
                case Key.I: panel.Toggle(Page.Guide); break;
                case Key.H: panel.Toggle(Page.Crew); break;
                case Key.C: panel.Toggle(Page.Craft); break;
                case Key.Tab: panel.Toggle(Page.Backpack); break;
                case Key.F5: saves.QuickSave(startMenu); break;
                case Key.F9: saves.QuickLoad(startMenu); break;
                case >= Key.Key1 and <= Key.Key9: hotbar.Select((int)(key.PhysicalKeycode - Key.Key1)); break;
                case Key.E: player.InspectNearest(); break;
                case Key.Space: player.Swing(); break;
            }
        }
        if (ev is InputEventMouseButton mouse) player.Click(mouse, GetViewport().GuiGetHoveredControl() != null);
    }
    public override void _Process(double delta) {
        if (world == null || view == null) return;
        float dt = Math.Min((float)delta, .1f); runTime += dt;
        player.Update(dt);
        // The server holds the world still while every player has a menu open, which in a game alone is whenever this one does.
        if (kit.Paused != holding) { holding = kit.Paused; net.Hold(holding); }
        bool running = world.Remote ? !holding || world.Players.Count > 1 : !kit.Paused;
        if (running) {
            float health = world.Remote ? shownHealth : world.Health;
            // A world not on a server is one of the review fixtures', which runs here.
            if (!world.Remote) world.Tick(dt);
            ending.ShowOnce();
            needsPlate.DamageFlash(health - world.Health, runTime); shownHealth = world.Health;
        }
        view.Paused = !running; view.SelectedTool = hotbar.ActiveItem(); view.Selection = panel.Selection; view.Placing = player.Mode.Placing;
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
        quitting = true; net.Leave(); StopHosted(); sounds.StopAll(); await Seconds(.2);
        // Godot objects left to the finalizer must be released before the engine shuts down, or a late finalizer aborts the exit.
        GC.Collect(); GC.WaitForPendingFinalizers(); GetTree().Quit(code);
    }
}
