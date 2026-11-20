using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// The web page on the hero's screen in the intro film: three catgirls dancing on a stage inside a browser window.
/// The one in the middle dances a solo.
/// The page is drawn in a world of its own, and <see cref="Picture"/> shows it on whatever wears that material.
/// </summary>
internal sealed class CatgirlPage {
    /// <summary>The page in pixels, 800 across, in the shape of the tube monitor's screen it is shown on.</summary>
    private static readonly Vector2I Size = new(800, Mathf.RoundToInt(800 * IntroSets.ScreenSize.Y / IntroSets.ScreenSize.X));
    /// <summary>How tall the browser's bars above and below the stage are, in pixels.</summary>
    private const int TitleBar = 30, AddressBar = 34, Banner = 40, StatusBar = 24;
    /// <summary>How far apart the dancers stand, in metres.</summary>
    private const float Spacing = 1.04f;
    /// <summary>The dancers from the viewer's left to right; the one in the middle dances the solo.</summary>
    private static readonly CatgirlOutfit[] Outfits = { CatgirlOutfit.Maid, CatgirlOutfit.Blouse, CatgirlOutfit.Hoodie };
    private const int Soloist = 1;

    private readonly SubViewport view = new() { Size = Size, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
    private readonly List<Catgirl> dancers = new();
    private readonly List<Node3D> stars = new();
    private readonly Control live, browser;
    private readonly Camera3D camera;
    private readonly SpotLight3D pinkSpot, blueSpot;
    private readonly StandardMaterial3D pinkBar, blueBar;

    /// <summary>The page's own viewport, which draws only inside the scene tree.</summary>
    public Node Root => view;
    /// <summary>The page as a picture that glows by itself, for a screen to wear.</summary>
    public StandardMaterial3D Picture { get; }
    /// <summary>
    /// Whether the page is drawn again every frame; off, the picture stays as it last was and costs nothing.
    /// </summary>
    public bool Live { set => view.RenderTargetUpdateMode = value ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled; }

    public CatgirlPage() {
        Picture = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = view.GetTexture() };
        var stage = new Node3D(); view.AddChild(stage);
        var air = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("21162e"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("f6e8ff"),
            AmbientLightEnergy = .27f,
        };
        stage.AddChild(new WorldEnvironment { Environment = air });
        stage.AddChild(new DirectionalLight3D {
            RotationDegrees = new Vector3(-42, 24, 0),
            LightColor = new Color("ffe9f2"),
            LightEnergy = .36f,
            ShadowEnabled = true,
        });
        pinkSpot = AddSpot(stage, new Vector3(-1.95f, 2.55f, 1.7f), new Vector3(-.62f, .72f, 0), new Color("ff7bc7"));
        blueSpot = AddSpot(stage, new Vector3(1.9f, 2.4f, 1.52f), new Vector3(.68f, .75f, 0), new Color("91aaff"));
        camera = new Camera3D { Fov = 30 };
        stage.AddChild(camera);
        FrameStage(0);
        // A dim floor and a violet backdrop with a disc and stars, which cast no shadows.
        Cylinder(stage, 2.6f, 2.6f, .05f, new Vector3(0, -.025f, 0), Mat("60415b", .9f), sides: 32);
        var backdrop = Pivot(stage, Vector3.Zero);
        Box(backdrop, new Vector3(9, 5, .1f), new Vector3(0, 2, -1.9f), Mat("362341", .9f));
        Cylinder(backdrop, 1.35f, 1.35f, .04f, new Vector3(0, 1.15f, -1.82f), Mat("694068", .9f), new Vector3(90, 0, 0), 32);
        pinkBar = AddLightBar(backdrop, -1.66f, new Color("f480cb"));
        blueBar = AddLightBar(backdrop, 1.66f, new Color("809cff"));
        var gold = Mat("ed9fd8", .65f, 0, .26f);
        var rng = new Random(3);
        for (int i = 0; i < 12; i++) {
            float angle = i / 12f * MathF.Tau, away = 1.6f + (float)rng.NextDouble() * .5f;
            var star = Pivot(backdrop, new Vector3(MathF.Cos(angle) * away, 1.15f + MathF.Sin(angle) * away * .8f, -1.8f), new Vector3(0, 0, 45));
            Box(star, new Vector3(.13f, .13f, .02f), Vector3.Zero, gold);
            stars.Add(star);
        }
        NoShadows(backdrop);
        for (int i = 0; i < Outfits.Length; i++) {
            var dancer = new Catgirl(Outfits[i], new Vector3((i - (Outfits.Length - 1) / 2f) * Spacing, 0, 0));
            stage.AddChild(dancer.Rig.Root); dancers.Add(dancer);
        }
        (browser, live) = BuildBrowser();
    }

    private static SpotLight3D AddSpot(Node3D stage, Vector3 position, Vector3 aim, Color tint) {
        var spot = new SpotLight3D {
            Position = position,
            Basis = Basis.LookingAt(aim - position, Vector3.Up),
            LightColor = tint,
            SpotRange = 6,
            SpotAngle = 32,
            SpotAngleAttenuation = 1.8f,
        };
        stage.AddChild(spot);
        return spot;
    }

    /// <summary>A narrow lamp on the back wall, returned so its brightness can follow the spotlight.</summary>
    private static StandardMaterial3D AddLightBar(Node3D backdrop, float x, Color tint) {
        var glow = new StandardMaterial3D {
            AlbedoColor = new Color("31293d"),
            EmissionEnabled = true,
            Emission = tint,
        };
        Box(backdrop, new Vector3(.105f, .56f, .046f), new Vector3(x, 1.10f, -1.83f), Mat("201c29"));
        Box(backdrop, new Vector3(.041f, .45f, .012f), new Vector3(x, 1.10f, -1.801f), glow);
        return glow;
    }

    /// <summary>Draws the browser's window around the stage and returns it with its blinking "LIVE" tag.</summary>
    private (Control Window, Control Live) BuildBrowser() {
        var window = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        window.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); view.AddChild(window);
        Control Bar(int top, int height, string color, int left = 0, int width = 0) {
            var bar = new ColorRect { Color = new Color(color), Position = new Vector2(left, top), Size = new Vector2(width == 0 ? Size.X : width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
            window.AddChild(bar);
            return bar;
        }
        void Write(Control bar, string text, int size, string color, Font? font = null, HorizontalAlignment align = HorizontalAlignment.Left) {
            var label = Style.Text(text, size, new Color(color), font);
            label.HorizontalAlignment = align;
            label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); label.OffsetLeft = 10; label.OffsetRight = -10;
            bar.AddChild(label);
        }
        Write(Bar(0, TitleBar, "1c2f7a"), "Catgirl Dance Party - NetSurfer", 17, "ffffff");
        Write(Bar(TitleBar, AddressBar, "c8c4bc"), "Address", 14, "2a2a2a");
        Write(Bar(TitleBar + 5, AddressBar - 10, "ffffff", 80, Size.X - 96), "http://www.catgirls.meow/dance", 14, "1a1a1a");
        Write(Bar(TitleBar + AddressBar, Banner, "c2327a"), "CATGIRL DANCE PARTY", 26, "ffffff", Style.Head, HorizontalAlignment.Center);
        var tag = Bar(TitleBar + AddressBar + 8, Banner - 16, "e0241f", Size.X - 96, 80);
        Write(tag, "LIVE", 15, "ffffff", Style.Head, HorizontalAlignment.Center);
        Write(Bar(Size.Y - StatusBar, StatusBar, "c8c4bc"), "You are visitor no. 0000001", 13, "2a2a2a");
        return (window, tag);
    }

    /// <summary>Moves the stage camera from square-on to the view from the dancers' left.</summary>
    public void FrameStage(float progress) {
        float travel = Mathf.SmoothStep(0, .92f, progress);
        float angle = Mathf.Atan2(2.9f, 4.4f) * travel;
        float radius = Mathf.Lerp(4.5f, new Vector2(2.9f, 4.4f).Length(), travel);
        camera.Position = new Vector3(Mathf.Sin(angle) * radius, Mathf.Lerp(1.02f, 1.22f, travel), Mathf.Cos(angle) * radius);
        var aim = new Vector3(0, Mathf.Lerp(1.10f, .92f, travel), 0);
        camera.Basis = Basis.LookingAt(aim - camera.Position, Vector3.Up);
    }

    /// <summary>
    /// Shows the soloist alone, with no browser window and the camera level on her, for working on her dance.
    /// </summary>
    public void Rehearse() {
        browser.Visible = false;
        for (int i = 0; i < dancers.Count; i++) dancers[i].Rig.Root.Visible = i == Soloist;
        camera.Position = new Vector3(0, 1, 4.4f); camera.Basis = Basis.Identity;
    }

    /// <summary>
    /// Poses the page for a moment, in seconds since the solo began: the dances, the stars and the "LIVE" tag.
    /// </summary>
    public void Play(float time) {
        var chorus = CatgirlDance.Chorus(time);
        // Blinks are held with the pose, but happen at different times for each dancer.
        float poseTime = CatgirlDance.PoseTime(time);
        var solo = CatgirlDance.Solo(time);
        for (int i = 0; i < dancers.Count; i++) dancers[i].Show(i == Soloist ? solo : chorus, poseTime + i * 1.1f);
        float beat = (time + .5f / WorldView.IntroFps) / CatgirlDance.Beat;
        int count = Mathf.FloorToInt(beat);
        float flash = 1 - Mathf.SmoothStep(.035f, .55f, beat - count);
        bool pink = (count & 1) == 0;
        float pinkHit = flash * (pink ? 1 : .18f), blueHit = flash * (pink ? .18f : 1);
        pinkSpot.LightEnergy = .14f + 1.66f * pinkHit;
        blueSpot.LightEnergy = .16f + 1.77f * blueHit;
        pinkBar.EmissionEnergyMultiplier = .15f + .82f * pinkHit;
        blueBar.EmissionEnergyMultiplier = .15f + .82f * blueHit;
        for (int i = 0; i < stars.Count; i++)
            stars[i].Scale = Vector3.One * (.69f + .2f * MathF.Sin(time * 5 + i * 1.9f) + .13f * flash);
        live.Visible = Mathf.PosMod(time, .8f) < .5f;
    }
}
