using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// What a player inside a cabin sees of its door when a wolf forces it: a cabin of its own in the dark, looked at from the
/// back wall, with copies of the wolves outside the door and of a player the wolf takes out, standing as the world draws them.
/// It is drawn into a picture that <see cref="InsideWindow"/> shows, only while something has asked for it this frame.
/// It is made once and kept, so showing it mid-game builds nothing.
/// </summary>
public partial class InsideSet : SubViewport {
    /// <summary>How many pixels tall the picture is; its width follows the window that shows it.</summary>
    public const int Height = 648;
    /// <summary>How many wolves at the door the picture shows.</summary>
    public const int Wolves = 4;
    /// <summary>Where the camera stands in the cabin, at the back wall beside the bed, and the spot it looks at, on the half of the door that opens.</summary>
    private static readonly Vector3 Eye = new(.12f, 1.08f, -.92f), Aim = new(-.14f, 1.02f, 1.05f);
    /// <summary>The camera's field of view, in degrees, and the one it closes in to as a wolf launches itself at it.</summary>
    private const float FieldOfView = 50, CloseUp = 30;
    /// <summary>Degrees a second the camera opens out again after a launch.</summary>
    private const float OpenOut = 90;
    private readonly Camera3D camera;
    private readonly HutProp cabin;
    /// <summary>Holds the copies where the world has their originals, moved so that the cabin they are at stands in the middle of the set.</summary>
    private readonly Node3D stage = new();
    private readonly WolfModel[] wolves = new WolfModel[Wolves];
    private ActorRig rig;
    private string shirt = "";
    private int shown;
    /// <summary>Whether the picture was asked for this frame.</summary>
    public bool Showing { get; private set; }

    public InsideSet() {
        Size = new Vector2I(Height * 4 / 9, Height); OwnWorld3D = true; RenderTargetUpdateMode = UpdateMode.Once;
        cabin = CabinModel.Build(); AddChild(cabin.Root);
        // Bare ground outside the door, so the night out there is not a void.
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(60, 60) }, MaterialOverride = Mat("20251d", 1) }; AddChild(ground);
        AddChild(stage);
        for (int i = 0; i < Wolves; i++) { wolves[i] = new WolfModel(); stage.AddChild(wolves[i].Root); }
        rig = new ActorRig(ActorSkin.Player); stage.AddChild(rig.Root);
        camera = new Camera3D { Current = true, Fov = FieldOfView, Near = .05f, Far = 80, Position = Eye, Basis = Basis.LookingAt(Aim - Eye, Vector3.Up) }; AddChild(camera);
        // Moonlight from outside and above falls in only through the open door; nothing lights the inside.
        AddChild(new DirectionalLight3D { LightColor = new Color("9fb4d8"), LightEnergy = .55f, ShadowEnabled = true, DirectionalShadowMaxDistance = 12, Basis = Basis.LookingAt(new Vector3(.35f, -.5f, -1).Normalized(), Vector3.Up) });
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
        AddChild(new WorldEnvironment { Environment = environment });
    }

    /// <summary>Starts a frame of the picture with no copies in it.</summary>
    public void Clear() {
        Showing = false; shown = 0;
        rig.Root.Visible = false;
    }

    /// <summary>
    /// Asks for the picture this frame, of the cabin at <paramref name="at"/> with its door <paramref name="door"/> as the world
    /// draws it; the camera closes in on a wolf <paramref name="launched"/> of the way through its launch, 0 to 1, and opens out after it.
    /// </summary>
    public void Show(Vector2 at, Node3D door, float launched, float dt) {
        Showing = true;
        camera.Fov = launched is > 0 and < 1 ? Mathf.Lerp(FieldOfView, CloseUp, WorldView.Ease(launched)) : Mathf.MoveToward(camera.Fov, FieldOfView, OpenOut * dt);
        stage.Position = -WorldView.To3(at);
        cabin.Door.Rotation = door.Rotation;
    }

    /// <summary>Adds a copy of a wolf as the world draws it, while there is one to spare.</summary>
    public void Add(WolfModel wolf) {
        if (shown < Wolves) CopyPose(wolf.Root, wolves[shown++].Root);
    }

    /// <summary>Adds a copy of a player as the world draws them, in a shirt of <paramref name="colour"/>.</summary>
    public void Add(ActorRig player, string colour) {
        // A player in another shirt needs a rig of their own, made as the wolf comes for them.
        if (colour != shirt) {
            shirt = colour; rig.Root.QueueFree();
            rig = new ActorRig(ActorSkin.Player, colour); stage.AddChild(rig.Root);
        }
        CopyPose(player.Root, rig.Root);
    }

    /// <summary>Draws the picture when it was asked for, and stops drawing it when not.</summary>
    public void Finish() {
        for (int i = 0; i < Wolves; i++) wolves[i].Root.Visible = i < shown;
        var mode = Showing ? UpdateMode.Always : UpdateMode.Disabled;
        if (RenderTargetUpdateMode != mode && (Showing || RenderTargetUpdateMode == UpdateMode.Always)) RenderTargetUpdateMode = mode;
    }

    /// <summary>Sets the picture's width for a window of this shape, width over height.</summary>
    public void Fit(float aspect) {
        var size = new Vector2I(Math.Max(16, Mathf.RoundToInt(Height * aspect)), Height);
        if (Size != size) Size = size;
    }
}
