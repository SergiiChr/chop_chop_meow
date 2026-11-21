using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A shore pump's piston housing, downturned outlet, rocking hand lever and connecting rod.</summary>
internal sealed class WaterPumpShape {
    /// <summary>The grip has the same hinge offset, reach and travel as the forge bellows.</summary>
    private const float Raised = 18;
    private static readonly Vector3 Hinge = new(0, .26f, -.66f);
    private static readonly Vector3 Crossbar = new(1, .75f, 0);
    private static readonly Vector3 CrankPin = new(-.30f, .062f, 0);
    private static readonly Vector3 RodGuide = new(-.30f, .53f, 0);
    private static Mesh? outlet;
    private readonly MeshInstance3D rod;

    /// <summary>The lever rotates around its Z axis, with the operator on the pump's −Z side.</summary>
    public Node3D Lever { get; }

    public WaterPumpShape(Node3D parent) {
        var cast = Metal("58959b", .57f, .28f);
        var steel = Metal("7e9299", .48f, .48f);
        var dark = Metal("4e6169", .61f, .44f);
        var brass = Metal("9c7b4f", .5f, .51f);

        // The silver foot opposite the operator connects the outlet to the underground pipe network.
        Box(parent, new Vector3(.60f, .075f, .98f), new Vector3(0, .038f, -.05f), dark);
        Post(parent, .163f, .163f, .06f, new Vector3(0, .075f, .41f), steel, 10);
        Add(parent, outlet ??= Outlet(), steel, Vector3.Zero);
        Post(parent, .213f, .213f, .061f, new Vector3(0, .075f, .035f), steel, 10);
        Post(parent, .172f, .137f, .45f, new Vector3(0, .136f, .035f), cast, 12);
        Post(parent, .195f, .181f, .057f, new Vector3(0, .532f, .035f), steel, 12);
        Post(parent, .155f, .155f, .37f, new Vector3(0, .579f, .035f), cast, 12);
        Post(parent, .173f, .148f, .069f, new Vector3(0, .923f, .035f), steel, 12);
        Post(parent, .098f, .082f, .04f, new Vector3(0, .992f, .035f), brass, 8);
        Cylinder(parent, .111f, .111f, .08f, new Vector3(0, .79f, .172f), cast, new Vector3(90, 0, 0));
        foreach (float x in new[] { -.225f, .225f })
            foreach (float z in new[] { -.46f, .21f })
                Post(parent, .035f, .035f, .023f, new Vector3(x, .075f, z), brass, 6);

        var rig = Pivot(parent, Hinge, new Vector3(0, 90, 0));
        var axle = new Vector3(90, 0, 0);
        // The two cheeks carry the lever's axle, leaving a gap for the handle to swing.
        foreach (float z in new[] { -.076f, .076f })
            Bar(rig, new Vector3(-.253f, -.193f, z), new Vector3(0, .012f, z), .095f, .044f, cast);
        Cylinder(rig, .055f, .055f, .213f, Vector3.Zero, brass, axle, 8);

        // A guide fixed to the pump body holds the top of the moving rod, away from the arm's arc.
        Bar(rig, new Vector3(-.55f, .53f, 0), RodGuide, .067f, .10f, cast);
        Post(rig, .065f, .05f, .077f, RodGuide - Vector3.Up * .038f, brass, 8);
        rod = Cylinder(rig, .025f, .025f, 1, Vector3.Zero, steel, sides: 8);

        Lever = Pivot(rig, Vector3.Zero);
        var elbow = new Vector3(.61f, .24f, 0);
        Bar(Lever, CrankPin, Vector3.Zero, .074f, .06f, dark);
        Bar(Lever, Vector3.Zero, elbow, .086f, .06f, dark);
        Bar(Lever, elbow, Crossbar, .069f, .06f, cast);
        Cylinder(Lever, .044f, .044f, .09f, CrankPin, brass, axle, 8);
        Cylinder(Lever, .03f, .03f, .34f, Crossbar, Wood("997450"), axle, 8);
        SetPress(0);
    }

    /// <summary>Follows the shared pumping pose from raised at zero to fully pressed at one.</summary>
    public void SetPress(float pressed) {
        float lift = Raised * (1 - pressed);
        Lever.RotationDegrees = new Vector3(0, 0, lift);
        var pin = CrankPin.Rotated(Vector3.Back, Mathf.DegToRad(lift));
        var span = RodGuide - pin;
        var aim = new Basis(new Quaternion(Vector3.Up, span.Normalized()));
        rod.Transform = new Transform3D(aim * Basis.FromScale(new Vector3(1, span.Length(), 1)), (pin + RodGuide) / 2);
    }

    private static void Bar(Node3D parent, Vector3 from, Vector3 to, float width, float depth, Material material) {
        var span = to - from;
        Box(parent, new Vector3(span.Length() + width * .5f, width, depth), (from + to) / 2, material,
            new Vector3(0, 0, Mathf.RadToDeg(MathF.Atan2(span.Y, span.X))));
    }

    private static ArrayMesh Outlet() {
        const float top = .79f, bend = .133f, corner = .277f, reach = corner + bend, radius = .068f;
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        var from = new Vector3(0, top, .20f);
        var up = Vector3.Up;
        void Extend(Vector3 to, Vector3 nextUp) {
            Vector3 Point(Vector3 at, Vector3 normal, int side) {
                float angle = side * Mathf.Tau / 10;
                return at + radius * (normal * MathF.Cos(angle) + Vector3.Left * MathF.Sin(angle));
            }
            for (int side = 0; side < 10; side++) {
                var a = Point(from, up, side); var b = Point(to, nextUp, side);
                var c = Point(to, nextUp, side + 1); var d = Point(from, up, side + 1);
                Triangle(s, a, b, c); Triangle(s, a, c, d);
            }
            from = to; up = nextUp;
        }
        for (int step = 0; step <= 4; step++) {
            float angle = step * Mathf.Pi / 8;
            var outward = new Vector3(0, MathF.Cos(angle), MathF.Sin(angle));
            Extend(new Vector3(0, top - bend, corner) + bend * outward, outward);
        }
        Extend(new Vector3(0, .125f, reach), Vector3.Back);
        return s.Commit();
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c) {
        s.SetNormal((c - a).Cross(b - a).Normalized());
        s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
