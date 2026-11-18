using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// The round lock of a cabin's door with its flat key in it, drawn from above and from the left into a picture the cabin's page shows.
/// The key's teeth point up while the door is unlocked; it turns half a turn to the right, teeth down, as it locks, in time with the lock's sound.
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
        // Seen from above and from the left, so the key points down to the right.
        float pitch = Mathf.DegToRad(30), yaw = Mathf.DegToRad(-38);
        var back = new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
        AddChild(new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = .97f, Near = .1f, Far = 20, Current = true, Position = new Vector3(.033f, -.039f, .055f) + back * 6, Basis = Basis.LookingAt(-back, Vector3.Up) });
        AddChild(new DirectionalLight3D { LightEnergy = 1.7f, LightColor = new Color("fff1dc"), Basis = Basis.LookingAt(new Vector3(.55f, -.8f, -.75f).Normalized(), Vector3.Up) });
        var environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c7d3dc"),
            AmbientLightEnergy = .9f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        var body = new Node3D(); AddChild(body);
        // A round lock: a dark collar that narrows away from the door, around a bright steel face with a maker's tag stamped over the plug the key goes into.
        var collar = Metal("403d39", .6f, .4f); var steel = Metal("d3d1c9", .4f, .45f); var stamped = Metal("b4b2aa", .45f, .45f); var dark = Mat("1d1a17", .9f); var nickel = Metal("85847e", .45f, .4f);
        var outward = new Vector3(90, 0, 0);
        Cylinder(body, .43f, .37f, .12f, new Vector3(0, 0, .06f), collar, outward, 32);
        Cylinder(body, .3f, .3f, .02f, new Vector3(0, 0, .125f), steel, outward, 32);
        Box(body, new Vector3(.22f, .075f, .012f), new Vector3(0, .13f, .137f), stamped);
        // The plug turns with the flat key in it. The key's teeth run along one edge of its blade, and its grip is a flat ring.
        key = Pivot(body, new Vector3(0, -.09f, .135f));
        Cylinder(key, .105f, .105f, .02f, new Vector3(0, 0, .005f), dark, outward, 20);
        Box(key, new Vector3(.024f, .075f, .2f), new Vector3(0, -.012f, .1f), nickel);
        foreach (var (along, height) in new[] { (.035f, .014f), (.07f, .03f), (.105f, .02f), (.14f, .034f) })
            Box(key, new Vector3(.024f, height, .028f), new Vector3(0, .0255f + height / 2, along), nickel);
        Box(key, new Vector3(.024f, .16f, .034f), new Vector3(0, 0, .2f), nickel);
        Box(key, new Vector3(.028f, .12f, .07f), new Vector3(0, 0, .24f), nickel);
        var grip = Torus(key, .05f, .19f, new Vector3(0, 0, .43f), nickel);
        grip.RotationDegrees = new Vector3(0, 0, 90); grip.Scale = new Vector3(1, .2f, 1);
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
        // Seen from the front, a turn to the right goes against the angle's own direction.
        key.RotationDegrees = new Vector3(0, 0, -degrees);
        RenderTargetUpdateMode = UpdateMode.Once;
    }
}
