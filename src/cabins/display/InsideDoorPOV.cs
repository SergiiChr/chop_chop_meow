using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// What a player inside a cabin sees of its door when a wolf forces it: a cabin of its own in the dark, looked at from the
/// back wall, with copies of the wolves outside the door and of a player the wolf takes out, standing as the world draws them.
/// It is drawn into a picture of its own and shown as an old tape would, in a window in the left third of the screen,
/// three tenths of its width and 72% of its height, only while something has asked for it this frame.
/// It is made once and kept, so showing it mid-game builds nothing.
/// </summary>
public partial class InsideDoorPOV : TextureRect {
    private const string ShaderPath = "res://assets/environment/vhs/vhs.gdshader";
    /// <summary>The window's share of the screen's width and height, and where across the screen its middle is.</summary>
    private const float Width = .3f, Tall = .72f, Middle = 1f / 6;
    /// <summary>How coarse the tape is: its smears and jitters are this many to the picture's width and height.</summary>
    private static readonly Vector2 TapeLines = new(270, 365);
    /// <summary>How many pixels tall the picture is; its width follows the window's shape.</summary>
    public const int Height = 648;
    /// <summary>How many wolves at the door the picture shows.</summary>
    public const int Wolves = 4;
    /// <summary>Where the camera stands in the cabin, at the back wall beside the bed, and the spot it looks at, on the half of the door that opens.</summary>
    internal static readonly Vector3 Eye = new(.12f, 1.08f, -.92f), Aim = new(-.14f, 1.02f, 1.05f);
    /// <summary>The camera's field of view, in degrees.</summary>
    private const float FieldOfView = 50;
    /// <summary>The picture, drawn in a world of its own.</summary>
    private readonly SubViewport picture = new() { Size = new Vector2I(Height * 4 / 9, Height), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
    private readonly HutProp cabin;
    /// <summary>Holds the copies where the world has their originals, moved so that the cabin they are at stands in the middle of the picture's world.</summary>
    private readonly Node3D stage = new();
    private readonly WolfModel[] wolves = new WolfModel[Wolves];
    /// <summary>A player in each shirt one may wear, of which only the one a wolf takes out is shown.</summary>
    private readonly ActorRig[] rigs = new ActorRig[WorldView.Shirts.Length];
    private int shown;
    /// <summary>Frames left of the window's first ones, in which it is drawn faintly to build its shader's pipeline.</summary>
    private int warmFrames = 4;
    /// <summary>How faintly the window is drawn in those frames: the least the renderer still draws.</summary>
    private static readonly Color Faint = new(1, 1, 1, .01f);
    /// <summary>Whether the picture was asked for this frame.</summary>
    public bool Showing { get; private set; }

    public InsideDoorPOV() {
        Name = "inside"; Visible = false; Modulate = Faint; MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = Middle - Width / 2; AnchorRight = Middle + Width / 2; AnchorTop = (1 - Tall) / 2; AnchorBottom = (1 + Tall) / 2;
        AddChild(picture);
        cabin = CabinModel.Build(); picture.AddChild(cabin.Root);
        // Bare ground outside the door, so the night out there is not a void.
        picture.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(60, 60) }, MaterialOverride = Mat("20251d", 1) });
        picture.AddChild(stage);
        for (int i = 0; i < Wolves; i++) { wolves[i] = new WolfModel(); stage.AddChild(wolves[i].Root); }
        for (int i = 0; i < rigs.Length; i++) { rigs[i] = new ActorRig(ActorSkin.Player, WorldView.Shirts[i]); stage.AddChild(rigs[i].Root); }
        picture.AddChild(new Camera3D { Current = true, Fov = FieldOfView, Near = .05f, Far = 80, Position = Eye, Basis = Basis.LookingAt(Aim - Eye, Vector3.Up) });
        // Moonlight from outside and above falls in only through the open door; nothing lights the inside.
        picture.AddChild(new DirectionalLight3D { LightColor = new Color("9fb4d8"), LightEnergy = .55f, ShadowEnabled = true, DirectionalShadowMaxDistance = 12, Basis = Basis.LookingAt(new Vector3(.35f, -.5f, -1).Normalized(), Vector3.Up) });
        var environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("070a12"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("5d6f8f"),
            AmbientLightEnergy = .06f,
            GlowEnabled = true,
            GlowIntensity = 1.2f,
            GlowBloom = .15f,
            GlowHdrThreshold = .8f,
        };
        picture.AddChild(new WorldEnvironment { Environment = environment });
        Texture = picture.GetTexture(); ExpandMode = ExpandModeEnum.IgnoreSize; StretchMode = StretchModeEnum.Scale; TextureFilter = TextureFilterEnum.Linear;
        var tape = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        tape.SetShaderParameter("vhs_resolution", TapeLines);
        tape.SetShaderParameter("noise_texture", Noise());
        // Builds the shader while loading, so the window's first frame mid-game doesn't stall on it.
        _ = tape.GetRid();
        Material = tape;
    }

    /// <summary>
    /// Shows the window while the picture is drawn, and keeps the picture to the window's shape.
    /// For its first frames it is drawn all but see-through, which builds the tape shader's pipeline while loading
    /// rather than as the first wolf forces a door.
    /// </summary>
    public override void _Process(double delta) {
        if (warmFrames > 0 && --warmFrames == 0) Modulate = Colors.White;
        bool show = Showing || warmFrames > 0;
        if (Visible != show) Visible = show;
        if (!Visible || Size.Y <= 0) return;
        var size = new Vector2I(Math.Max(16, Mathf.RoundToInt(Height * Size.X / Size.Y)), Height);
        if (picture.Size != size) picture.Size = size;
    }

    /// <summary>Starts a frame of the picture with no copies in it.</summary>
    public void Clear() {
        Showing = false; shown = 0;
        foreach (var rig in rigs) rig.Root.Visible = false;
    }

    /// <summary>Asks for the picture this frame, of the cabin at <paramref name="at"/> with its door <paramref name="door"/> as the world draws it.</summary>
    public void Show(Vector2 at, Node3D door) {
        Showing = true;
        stage.Position = -WorldView.To3(at);
        cabin.Door.Rotation = door.Rotation;
    }

    /// <summary>Adds a copy of a wolf as the world draws it, while there is one to spare.</summary>
    public void Add(WolfModel wolf) {
        if (shown < Wolves) CopyPose(wolf.Root, wolves[shown++].Root);
    }

    /// <summary>Adds a copy of a player as the world draws them, in a shirt of <paramref name="colour"/>.</summary>
    public void Add(ActorRig player, string colour) {
        CopyPose(player.Root, rigs[Math.Max(0, Array.IndexOf(WorldView.Shirts, colour))].Root);
    }

    /// <summary>Draws the picture when it was asked for, and stops drawing it when not.</summary>
    public void Finish() {
        for (int i = 0; i < Wolves; i++) wolves[i].Root.Visible = i < shown;
        // A picture still on the one drawing it starts with is left to finish it.
        var mode = Showing ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (picture.RenderTargetUpdateMode != mode && (Showing || picture.RenderTargetUpdateMode == SubViewport.UpdateMode.Always)) picture.RenderTargetUpdateMode = mode;
    }

    /// <summary>Coloured noise for the tape's grain and jitter, the same on every run.</summary>
    private static ImageTexture Noise() {
        const int side = 256;
        var grain = new byte[side * side * 4];
        new Random(1987).NextBytes(grain);
        var image = Image.CreateFromData(side, side, false, Image.Format.Rgba8, grain);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
