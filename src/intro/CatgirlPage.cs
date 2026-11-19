using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// The web page on the hero's screen in the intro film: three catgirls dancing on a stage, the one in the middle a solo, inside a browser window.
/// The page is drawn in a world of its own, and <see cref="Picture"/> shows it on whatever wears that material.
/// </summary>
internal sealed class CatgirlPage {
    /// <summary>The page in pixels, in the shape of a tube monitor's screen.</summary>
    private static readonly Vector2I Size = new(800, 612);
    /// <summary>How tall the browser's bars above and below the stage are, in pixels.</summary>
    private const int TitleBar = 30, AddressBar = 34, Banner = 40, StatusBar = 24;
    /// <summary>How far apart the dancers stand, in metres.</summary>
    private const float Spacing = .82f;
    /// <summary>The dancers from the viewer's left to right; the one in the middle dances the solo.</summary>
    private static readonly CatgirlOutfit[] Outfits = { CatgirlOutfit.Maid, CatgirlOutfit.Sailor, CatgirlOutfit.Hoodie };
    private const int Soloist = 1;

    private readonly SubViewport view = new() { Size = Size, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
    private readonly List<Catgirl> dancers = new();
    private readonly List<Node3D> stars = new();
    private readonly Control live, browser;
    private readonly Camera3D camera;

    /// <summary>The page's own viewport, which draws only inside the scene tree.</summary>
    public Node Root => view;
    /// <summary>The page as a picture that glows by itself, for a screen to wear.</summary>
    public StandardMaterial3D Picture { get; }
    /// <summary>Whether the page is drawn again every frame; off, the picture stays as it last was and costs nothing.</summary>
    public bool Live { set => view.RenderTargetUpdateMode = value ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled; }

    public CatgirlPage() {
        Picture = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = view.GetTexture() };
        var stage = new Node3D(); view.AddChild(stage);
        var air = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("b8377c"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = .5f,
        };
        stage.AddChild(new WorldEnvironment { Environment = air });
        stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-42, 24, 0), LightEnergy = .55f, ShadowEnabled = true });
        // The stage sits in the part of the page the browser's bars leave free, so the camera aims above the dancers' middle.
        camera = new Camera3D { Position = new Vector3(0, 1.02f, 4.5f), Fov = 30 };
        stage.AddChild(camera);
        camera.Basis = Basis.LookingAt(new Vector3(0, .08f, -4.5f), Vector3.Up);
        // A pink floor, a darker wall with a lighter disc on it, and stars around the disc.
        Cylinder(stage, 2.6f, 2.6f, .05f, new Vector3(0, -.025f, 0), Mat("f3b4d2", .9f), sides: 32);
        const GeometryInstance3D.ShadowCastingSetting unshadowed = GeometryInstance3D.ShadowCastingSetting.Off;
        Box(stage, new Vector3(9, 5, .1f), new Vector3(0, 2, -1.9f), Mat("b8377c", .9f)).CastShadow = unshadowed;
        Cylinder(stage, 1.35f, 1.35f, .04f, new Vector3(0, 1.15f, -1.82f), Mat("e770ac", .9f), new Vector3(90, 0, 0), 32).CastShadow = unshadowed;
        var gold = Mat("ffe07a", .5f, 0, 1.2f);
        var rng = new Random(3);
        for (int i = 0; i < 12; i++) {
            float angle = i / 12f * MathF.Tau, away = 1.6f + (float)rng.NextDouble() * .5f;
            var star = Pivot(stage, new Vector3(MathF.Cos(angle) * away, 1.15f + MathF.Sin(angle) * away * .8f, -1.8f), new Vector3(0, 0, 45));
            Box(star, new Vector3(.13f, .13f, .02f), Vector3.Zero, gold).CastShadow = unshadowed;
            stars.Add(star);
        }
        for (int i = 0; i < Outfits.Length; i++) {
            var dancer = new Catgirl(Outfits[i], new Vector3((i - (Outfits.Length - 1) / 2f) * Spacing, 0, 0));
            stage.AddChild(dancer.Rig.Root); dancers.Add(dancer);
        }
        (browser, live) = BuildBrowser();
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

    /// <summary>
    /// Shows the soloist alone, without the browser's window and with the camera level on her, for working on her dance.
    /// </summary>
    public void Rehearse() {
        browser.Visible = false;
        for (int i = 0; i < dancers.Count; i++) dancers[i].Rig.Root.Visible = i == Soloist;
        camera.Position = new Vector3(0, 1, 4.4f); camera.Basis = Basis.Identity;
    }

    /// <summary>Poses the page for a moment of the film, in seconds since the solo began: the dances, the stars and the "LIVE" tag.</summary>
    public void Play(float time) {
        // Each blinks by a clock of her own, so they do not blink together.
        for (int i = 0; i < dancers.Count; i++) dancers[i].Show(i == Soloist ? CatgirlDance.Solo(time) : CatgirlDance.Chorus(time), time + i * 1.1f);
        for (int i = 0; i < stars.Count; i++) stars[i].Scale = Vector3.One * (.75f + .35f * MathF.Sin(time * 5 + i * 1.9f));
        live.Visible = Mathf.PosMod(time, .8f) < .5f;
    }
}
