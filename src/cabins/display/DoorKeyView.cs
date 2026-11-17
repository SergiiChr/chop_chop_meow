using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// The keyhole of a cabin's door with its key in it, drawn from the game's angle into a picture the cabin's page shows.
/// The key's grip stands up while the door is unlocked and turns half a turn, down, as it locks, in time with the lock's sound.
/// It is made once and kept, so showing it mid-game builds nothing.
/// </summary>
public partial class DoorKeyView : SubViewport {
    /// <summary>Pixels along each side of the picture.</summary>
    public const int Side = 192;
    /// <summary>The seconds into the lock's sound over which the key turns: from the bolt's rattle to its landing.</summary>
    public const float TurnFrom = 1.3f, TurnTo = 1.68f;
    /// <summary>The seconds into the lock's sound over which a hand takes the key and jogs it, and by how many degrees.</summary>
    private const float JogFrom = .02f, JogTo = .3f, JogDegrees = 9;
    private readonly Node3D key;
    private bool locked;
    /// <summary>Seconds since the key was last turned; from <see cref="TurnTo"/> on it rests.</summary>
    private float since = TurnTo;

    public DoorKeyView() {
        Size = new Vector2I(Side, Side); TransparentBg = true; OwnWorld3D = true; Msaa3D = Msaa.Msaa4X; RenderTargetUpdateMode = UpdateMode.Once;
        // Seen as the game sees the world: from above and from the right.
        float pitch = Mathf.DegToRad(30), yaw = Mathf.DegToRad(38);
        var back = new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
        AddChild(new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 1.42f, Near = .1f, Far = 20, Current = true, Position = new Vector3(.04f, .02f, .16f) + back * 6, Basis = Basis.LookingAt(-back, Vector3.Up) });
        AddChild(new DirectionalLight3D { LightEnergy = 1.7f, LightColor = new Color("fff1dc"), Basis = Basis.LookingAt(new Vector3(.55f, -.8f, -.75f).Normalized(), Vector3.Up) });
        var environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c7d3dc"),
            AmbientLightEnergy = .9f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        var plate = new Node3D(); AddChild(plate);
        // A brass plate with rounded ends, held by two screws, around a dark keyhole.
        var brass = Metal("c9a257", .45f, .35f); var dark = Mat("1d1a17", .9f); var iron = Metal("aeb4b8", .45f, .4f);
        Box(plate, new Vector3(.52f, .78f, .05f), Vector3.Zero, brass);
        foreach (float y in new[] { .39f, -.39f }) {
            Cylinder(plate, .26f, .26f, .05f, new Vector3(0, y, 0), brass, new Vector3(90, 0, 0), 24);
            Cylinder(plate, .04f, .04f, .02f, new Vector3(0, y * 1.28f, .03f), iron, new Vector3(90, 0, 0), 12);
        }
        Cylinder(plate, .1f, .1f, .012f, new Vector3(0, .08f, .026f), dark, new Vector3(90, 0, 0), 20);
        Box(plate, new Vector3(.075f, .26f, .012f), new Vector3(0, -.07f, .026f), dark);
        // The key turns about its shaft, which goes into the round of the keyhole; its grip is a flat tab to one side of the shaft.
        key = Pivot(plate, new Vector3(0, .08f, .03f));
        Cylinder(key, .035f, .035f, .34f, new Vector3(0, 0, .17f), iron, new Vector3(90, 0, 0), 12);
        Cylinder(key, .055f, .055f, .04f, new Vector3(0, 0, .32f), iron, new Vector3(90, 0, 0), 12);
        Box(key, new Vector3(.045f, .24f, .15f), new Vector3(0, .1f, .4f), iron);
        Cylinder(key, .15f, .15f, .045f, new Vector3(0, .27f, .4f), iron, new Vector3(0, 0, 90), 20);
        Cylinder(key, .06f, .06f, .05f, new Vector3(0, .29f, .4f), dark, new Vector3(0, 0, 90), 14);
        Pose();
    }

    public override void _Ready() => SetProcess(false);

    /// <summary>Shows the key as it rests with the door locked or not, with no turn.</summary>
    public void Rest(bool locked) {
        this.locked = locked; since = TurnTo;
        Pose();
    }

    /// <summary>Turns the key to lock or unlock, starting now, as the lock's sound starts.</summary>
    public void Turn(bool locked) {
        if (locked == this.locked) return;
        this.locked = locked; since = 0;
        SetProcess(true);
    }

    public override void _Process(double delta) {
        since = Math.Min(TurnTo, since + (float)delta);
        Pose();
        if (since >= TurnTo) SetProcess(false);
    }

    /// <summary>Puts the key where it is this long after it was last turned, and draws the picture again.</summary>
    private void Pose() {
        float turned = Mathf.SmoothStep(TurnFrom, TurnTo, since), jog = since < JogTo ? MathF.Sin(MathF.PI * Mathf.SmoothStep(JogFrom, JogTo, since)) : 0;
        float degrees = (locked ? turned : 1 - turned) * 180 + (locked ? 1 : -1) * jog * JogDegrees;
        key.RotationDegrees = new Vector3(0, 0, degrees);
        RenderTargetUpdateMode = UpdateMode.Once;
    }
}
