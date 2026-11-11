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
    private static readonly float shotDelay = Launch.Number("--shot-delay", 2);
    /// <summary>Where <c>--render-intro</c> saves the intro film's frames; the film is drawn in place of a game.</summary>
    private static readonly string? introFolder = Launch.Value("--render-intro");
    private World world = null!;
    private WorldView view = null!;
    private Sounds sounds = null!;
    private Control ui = null!;
    private CanvasLayer layer = null!;
    private Corner corner = null!;
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
    /// <summary>The intro film while it plays; it frees itself when it ends.</summary>
    private IntroMovie? intro;
    private PlayerControl player = null!;
    private float uiClock;
    private Net net = null!;
    private GameData data = null!;
    /// <summary>The server running inside this process for the checks, which they set the world up through; real games run theirs in a process of its own.</summary>
    private Server? hosted;
    /// <summary>The server process this window last started; 0 for none.</summary>
    private int serverProcess;
    private Session session = new Session.None();
    /// <summary>How long a window waits for the server it started: the engine has to start and read the save first.</summary>
    private const ulong ServerStartMsec = 60000;
    /// <summary>A server process's own log, or each server start would rotate this window's log out of the folder.</summary>
    private const string ServerLog = "user://logs/server.log";
    /// <summary>The update the screen last refreshed for, so it refreshes once for each while an answer is due.</summary>
    private int refreshedUpdate;
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
    /// <summary>The window fills the screen; review runs and checks start in a window whatever the player chose.</summary>
    private bool fullscreen;
    public override void _Ready() {
        try {
            GD.Print(OS.IsDebugBuild() ? "Build: debug, from the project folder" : "Build: optimized release export");
            data = GameData.Create();
            if (Launch.Has("--server")) { Server.Run(this, data); return; }
            world = new World(data);
            if (Launch.Has("--self-test")) { SimulationChecks.Run(data); GetTree().Quit(); return; }
            if (Launch.Value("--render-icons") is string icons) { AddChild(new IconRender { Data = data, Folder = icons }); return; }
            world.Notify = Notify;
            // A failed check run leaves its saves and settings behind: the next one starts without them, as a first launch does.
            if (Launch.Args.Any(a => a is "--ui-test" or "--net-check" or "--server-check")) RemoveCheckFiles();
            sounds = new Sounds { World = world, MusicVolume = Settings.MusicVolume }; AddChild(sounds);
            // Closing the window goes through Quit, which lets playing sounds stop first.
            GetTree().AutoAcceptQuit = false;
            view = new WorldView { World = world, Sounds = sounds, EnvironmentReview = Launch.Has("--environment-review") }; AddChild(view);
            kit = new UiKit(world, sounds, Notify, RefreshLive);
            net = new Net { Name = "Net", World = world, Key = Settings.MachineKey }; AddChild(net);
            saves = new Saves(kit, net, this);
            world.Sound = PlayWorldSound;
            ConnectNet();
            sounds.OffScreen = view.OffScreen;
            layer = new CanvasLayer(); AddChild(layer);
            ui = new Control { Name = "Interface", MouseFilter = Control.MouseFilterEnum.Ignore }; layer.AddChild(ui);
            ui.Theme = Style.Interface();
            blackHole = new BlackHole(kit, view, ui.Theme, this, saves, () => panel.Close()); AddChild(blackHole);
            // Play renders at the player's pick; a review run or check at the design size, unless --resolution asks for one such as "2560x1440".
            bool review = introFolder != null || Launch.Args.Any(a => a is "--showcase" or "--ui-test" or "--net-check" or "--server-check" or "--animation-review" or "--performance-run");
            fullscreen = !review && Settings.Fullscreen;
            ApplyResolution(Launch.Size("--resolution") ?? (introFolder != null ? WorldView.IntroSize : review ? Settings.DesignSize : Settings.Resolution));
            if (!review) Engine.MaxFps = Settings.FrameLimit;
            BuildHud();
            Settings.ApplyAntiAliasing(GetViewport(), Settings.AntiAliasing);
            if (Launch.Has("--showcase")) new Showcase(world, view, panel, ui, blackHole, menu, kit).Run();
            else if (!Launch.Has("--animation-review") && introFolder == null) startMenu.ShowStart();
            if (Launch.Value("--drive") is string drive) AddChild(new ReviewDrive { Folder = drive });
            // The game opens on the intro film, over the start menu; review runs, checks and a screenshot of the menu open without it.
            if (!review && screenshotPath == null) PlayIntro();
            if (Launch.Has("--performance-run")) StartGame(-1);
            if (Launch.Has("--ui-test")) RunUIChecks();
            if (Launch.Has("--net-check")) RunNetCheck(Launch.Value("--net-check") == "host");
            if (Launch.Has("--server-check")) RunServerCheck();
            if (introFolder != null) {
                // --intro-stills saves only the frames at those seconds, such as 1.5,9.2, for a look at single moments.
                var stills = Launch.Value("--intro-stills")?.Split(',').Select(at => float.Parse(at, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                panel.Close(); ui.Visible = false; AudioServer.SetBusMute(0, true);
                view.RenderIntro(introFolder, stills);
            }
            if (Launch.Has("--animation-review")) {
                panel.Close(); ui.Visible = false; view.AnimationReview = true;
                if (Launch.Value("--animation-capture") is string captures) {
                    // Thirty frames a second of animation time, slowed by --animation-slow for a closer look.
                    float slow = Launch.Number("--animation-slow", 1);
                    if (!(slow > 0 && float.IsFinite(slow))) throw new ArgumentException($"--animation-slow must be a positive number: {slow}");
                    view.CaptureStep = 1f / 30 / slow;
                    CaptureAnimations(captures);
                }
            }
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    // A wolf breaks into its sprint some way off, so its growl carries further off screen than other sounds.
    private void PlayWorldSound(string name, Vector2 at) => sounds.Play(name, at, name == "wolf_growl_distance" ? 14 : Sounds.Reach);
    private void BuildHud() {
        moves = new ItemMoves(kit, blackHole);
        panel = new SidePanel(kit, view, moves, ui); bag = panel.Bag;
        hotbar = new Hotbar(kit, panel);
        player = new PlayerControl(kit, view, panel, hotbar); panel.Player = player;
        corner = new Corner(world); ui.AddChild(corner);
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
    private void PlayIntro() { intro = new IntroMovie(sounds) { Name = "intro" }; ui.AddChild(intro); }
    private void Notify(string message) {
        if (message == Water.FullBottleWarning && waterWarning != null) waterWarning.Warn(message); else messages?.Post(message);
        GD.Print(message);
    }
    private void RefreshLive() {
        if (recovery == null) return;
        corner.Refresh(); deathLabel.Refresh(); modeLabel.Refresh(); recovery.Refresh();
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
        net.Left += reason => { if (ReturnAfterFailedJoin(reason)) return; startMenu.LeftReason = reason; ShowStart(); };
        net.Failed += JoinFailed;
        net.Made += id => panel.SelectWhenHere(id);
    }
    /// <summary>Shown while a dropped guest tries to get back in, in place of the host's world, which it can no longer act on.</summary>
    private void ShowReconnecting() { world.NewGame(); startMenu.ShowReconnecting(); }
    public bool Owns => net.Owning;

    public void StepAway() { session = new Session.Away(net.Port); net.Leave(comingBack: true); }

    public bool ReturnAfterFailedJoin(string reason) {
        if (session is not Session.Away away) return false;
        startMenu.ShowStarting();
        JoinOwnServer(new Session.Returning(reason), away.Port);
        return true;
    }

    private void JoinFailed(string reason) {
        if (session is Session.Starting or Session.Returning) GameFailedToStart(reason);
        else if (!ReturnAfterFailedJoin(reason)) startMenu.ShowJoinFailure(reason);
    }

    private void GameFailedToStart(string reason) {
        Net.Log($"Could not start the game: {reason}");
        startMenu.LeftReason = $"Could not start the game. {reason}"; ShowStart();
    }

    private void OnJoined(bool back) {
        ending.Joined(back);
        shownHealth = world.Health;
        if (session is Session.Starting starting) Begin(starting.Slot);
        else if (session is Session.Returning returning) Notify($"{returning.Note} You are back in your game.");
        else if (net.Owning) Notify("You are back in your game.");
        else {
            Settings.Store("net", "address", net.Address);
            saves.Slot = 0;
            Notify(back ? "Reconnected to the host's settlement." : "Joined the host's settlement. The host's saves keep your belongings.");
        }
        session = net.Owning ? new Session.Playing() : new Session.None();
        if (Launch.Has("--performance-run")) StartPerformanceRun();
    }

    public void ApplyResolution(Vector2I size) { Settings.ApplyResolution(GetWindow(), size, fullscreen); FitInterface(); }
    public void ApplyFullscreen(bool on) { fullscreen = on; ApplyResolution(GetWindow().ContentScaleSize); }
    public void ShowFrameRate(bool on) => corner.FrameRate.Visible = on;

    /// <summary>How large the interface is drawn against the size it is laid out for.</summary>
    public const float InterfaceScale = .8f;

    /// <summary>
    /// The interface is laid out for the design size and drawn at <see cref="InterfaceScale"/> of it, larger at a taller render size, keeping its share of the screen.
    /// </summary>
    private void FitInterface() {
        var size = (Vector2)GetWindow().ContentScaleSize;
        float scale = size.Y / Settings.DesignSize.Y * InterfaceScale;
        layer.Scale = Vector2.One * scale; ui.Size = size / scale;
        // Letters are rasterised at the size they show rather than drawn at the laid-out size and stretched.
        GetViewport().OversamplingOverride = scale;
        blackHole.Fit(layer.Scale, ui.Size);
    }

    /// <summary>Opens a game this window started: a new one on the welcome page, a loaded one on the backpack.</summary>
    private void Begin(int slot) {
        saves.Slot = Math.Max(slot, 0);
        if (slot < 0) {
            panel.Open(Page.Welcome);
            Notify("Welcome. Press I to see the guide.");
        } else { panel.Open(Page.Backpack); Notify("Settlement loaded."); }
    }

    public void ShowStart() { LeaveServer(); session = new Session.None(); world.NewGame(); startMenu.ShowStart(); }
    void IGameFlow.Quit() => Quit();
    public void Restart() => StartGame(-1);

    /// <summary>Starts the game in a save slot; a slot that cannot load says why and leaves the game as it was.</summary>
    public void LoadSlot(int slot) {
        if (Saves.Read(data, slot).Error is string error) { Notify(error); return; }
        StartGame(slot);
    }

    /// <summary>Starts a server for a new game, or the one in <paramref name="slot"/>, and joins it as its owner once it is up.</summary>
    private void StartGame(int slot) {
        LeaveServer();
        startMenu.ShowStarting();
        // The checks that set the world up directly run the server inside this window, under a branch with its own network.
        if (Launch.Has("--net-check") || Launch.Has("--ui-test")) {
            var branch = new Node { Name = "Hosted" }; GetTree().Root.AddChild(branch);
            GetTree().SetMultiplayer(new SceneMultiplayer(), branch.GetPath());
            var main = new Node { Name = Name }; branch.AddChild(main);
            hosted = Server.Start(main, Server.Load(data, slot), net.Key);
            hosted.Ended += StopHosted;
            JoinOwnServer(new Session.Starting(slot), hosted.Port);
            return;
        }
        session = new Session.Launching(slot, Time.GetTicksMsec() + ServerStartMsec);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PortFile));
        serverProcess = RunServer(slot, net.Key, PortFile);
        if (serverProcess < 0) GameFailedToStart("The server process would not run.");
    }

    /// <summary>Starts a server process and returns its id, or a negative number when it would not run.</summary>
    private static int RunServer(int slot, string owner, string portFile) {
        var arguments = new System.Collections.Generic.List<string> { "--headless", "--log-file", ServerLog };
        // Outside an exported game the engine needs to be told where the project is.
        if (!OS.HasFeature("template")) arguments.AddRange(["--path", ProjectSettings.GlobalizePath("res://")]);
        arguments.Add("--"); arguments.AddRange(Server.Arguments(slot, owner, portFile));
        int id = OS.CreateProcess(OS.GetExecutablePath(), arguments.ToArray());
        if (id >= 0) Net.Log($"Started the server process {id}, which logs to {ProjectSettings.GlobalizePath(ServerLog)}.");
        return id;
    }

    /// <summary>Where the server process this window started says which port it took; the process id keeps two windows' apart.</summary>
    private static string PortFile => $"user://{Launch.FilePrefix}port-{OS.GetProcessId()}";

    private void JoinOwnServer(Session next, int port) {
        session = next;
        var error = net.Join("127.0.0.1", port);
        if (error != Error.Ok) GameFailedToStart(error.ToString());
    }

    /// <summary>Joins the server process this window started once it says which port it took.</summary>
    private void AwaitServer(Session.Launching launching) {
        if (Time.GetTicksMsec() >= launching.Deadline) { GameFailedToStart($"The server didn't start within {ServerStartMsec / 1000} seconds."); return; }
        if (!Godot.FileAccess.FileExists(PortFile) || !int.TryParse(Godot.FileAccess.GetFileAsString(PortFile), out int port)) return;
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PortFile));
        Net.Log($"The server process took port {port}.");
        JoinOwnServer(new Session.Starting(launching.Slot), port);
    }

    /// <summary>A server that dies says no goodbye, and the line would take most of a minute to notice.</summary>
    private void ServerDied() {
        Net.Log("The server process this window started has stopped.");
        net.Abandon();
        if (session is Session.Playing) { startMenu.LeftReason = Net.ServerStopped; ShowStart(); } else GameFailedToStart(Net.ServerStopped);
    }

    /// <summary>Leaves the game; a server inside this window goes first, since it could not answer a window waiting for it on the same thread.</summary>
    private void LeaveServer() { StopHosted(); net.Leave(); }

    /// <summary>Ends the server running inside this window, if any.</summary>
    private void StopHosted() {
        if (hosted is not Server server) return;
        hosted = null;
        // Its line closes now, so its port is free for the next game at once.
        server.End();
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
        // Drawing the intro film takes no input and runs no game under it.
        if (world == null || view == null || introFolder != null) return;
        float dt = Math.Min((float)delta, .1f); runTime += dt;
        player.Update(dt); panel.ShowAwaited();
        if (session is Session.Launching launching) AwaitServer(launching);
        // The server holds the world still while every player has a menu open, which in a game alone is whenever this one does.
        if (kit.Paused != holding) { holding = kit.Paused; net.Hold(holding); }
        bool running = !holding || world.Remote && world.Players.Count > 1;
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
        uiClock += dt;
        if (uiClock > .2f || net.AwaitingAnswer && net.ShownUpdate != refreshedUpdate) {
            uiClock = 0; refreshedUpdate = net.ShownUpdate; RefreshLive();
            if (serverProcess > 0 && session is not (Session.None or Session.Away) && !OS.IsProcessRunning(serverProcess)) ServerDied();
        }
        // Every frame, so a page that just opened takes its size once its wrapped text has a width.
        panel.Fit();
        // The needs plate grows with the bank count, and an open side panel would cover the messages' right end.
        float messagesLeft = needsPlate.GetRect().End.X + 24, messagesRight = panel.Visible ? SidePanel.Left - 16 : -messagesLeft;
        if (messages.OffsetLeft != messagesLeft || messages.OffsetRight != messagesRight) { messages.OffsetLeft = messagesLeft; messages.OffsetRight = messagesRight; }
        if (smokeTest && runTime > .5f && !quitting) { GD.Print("SMOKE-TEST PASS"); Quit(); }
        if (screenshotPath != null && runTime > shotDelay && !capturing) { capturing = true; Capture(screenshotPath); }
        if (performanceRunning) SamplePerformance();
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
        quitting = true; LeaveServer(); sounds.StopAll(); await Seconds(.2);
        // Godot objects left to the finalizer must be released before the engine shuts down, or a late finalizer aborts the exit.
        GC.Collect(); GC.WaitForPendingFinalizers(); GetTree().Quit(code);
    }
}
