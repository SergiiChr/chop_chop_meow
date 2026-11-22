using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A closed pressure pump with a bolted chamber, a sliding piston and a hand lever.</summary>
internal sealed class WaterPumpShape {
    private const float ChamberZ = .38f, SealHeight = .606f;
    private const float LeverLength = 2.03f, ToLink = .53f, LinkLength = .995f;
    private const float RaisedAngle = 14, PressedAngle = 26, HalfGrip = .104f;
    private static readonly Vector3 FixedPin = new(.105f, .596f, 0);
    private static readonly Vector3 Axle = new(90, 0, 0);
    private static Mesh? outlet;
    private readonly Node3D needle, piston, linkage;
    private readonly MeshInstance3D[] picking;
    private readonly record struct Stroke(Vector3 PistonPin, Vector3 RockerPin, Vector3 Grip, float Tilt);

    public Node3D Lever { get; }
    public ReadOnlySpan<MeshInstance3D> Picking => picking;

    public WaterPumpShape(Node3D parent) {
        var chamber = Metal("798083", .63f, .48f);
        var steel = Metal("9c9fa0", .48f, .60f);
        var dark = Metal("515758", .63f, .45f);
        var polished = Metal("c7cccf", .32f, .68f);
        var brass = Metal("a18652", .54f, .48f);

        // The chamber's skirt extends below the lake surface to meet the shallow bed.
        var baseAt = new Vector3(0, -.13f, ChamberZ);
        Post(parent, .280f, .280f, .05f, baseAt, dark, 14);
        var shell = Post(parent, .261f, .261f, .397f, baseAt + Vector3.Up * .031f, chamber, 14);
        var lid = Post(parent, .284f, .278f, .054f, new Vector3(0, .280f, ChamberZ), steel, 14);
        for (int i = 0; i < 7; i++) {
            float a = i * Mathf.Tau / 7;
            var bolt = new Vector3(.225f * MathF.Sin(a), .334f, ChamberZ + .225f * MathF.Cos(a));
            Post(parent, .029f, .026f, .020f, bolt, dark, 6);
        }

        Post(parent, .113f, .108f, .042f, new Vector3(0, .332f, ChamberZ), dark, 10);
        Post(parent, .077f, .077f, .213f, new Vector3(0, .366f, ChamberZ), chamber, 10);
        var seal = Post(parent, .099f, .099f, .098f, new Vector3(0, .504f, ChamberZ), steel, 10);
        Post(parent, .055f, .055f, .017f, new Vector3(0, SealHeight - .016f, ChamberZ), dark, 8);

        // The pressure outlet bends down onto the middle of the tile and joins the buried riser.
        var elbow = Add(parent, outlet ??= Outlet(), steel, Vector3.Zero);
        Cylinder(parent, .075f, .075f, .075f, new Vector3(0, .442f, .285f), dark, Axle, 10);
        needle = Gauge(parent, brass, steel, dark, out var dial);

        // The linkage is worked in a vertical plane toward the operator (-Z).
        var rig = Pivot(parent, Vector3.Zero, new Vector3(0, 90, 0));
        foreach (float side in new[] { -.056f, .056f })
            Bar(rig, new Vector3(-ChamberZ + .03f, .545f, side), FixedPin with { Z = side }, .076f, .032f, dark);
        Pin(rig, FixedPin, .042f, .168f, steel);

        piston = Pivot(rig, Vector3.Zero);
        piston.Name = "Piston";
        var shaft = Post(piston, .030f, .030f, 1.25f, Vector3.Down * 1.25f, polished, 8);
        Pin(piston, Vector3.Zero, .051f, .093f, steel);

        linkage = Pivot(rig, FixedPin);
        linkage.Name = "Rocker";
        var link = Box(linkage, new Vector3(LinkLength, .059f, .043f), Vector3.Right * (LinkLength / 2), dark);
        Pin(linkage, Vector3.Right * LinkLength, .044f, .128f, steel).Name = "RockerPin";

        Lever = Pivot(rig, Vector3.Zero);
        var bar = Box(Lever, new Vector3(LeverLength, .078f, .054f), Vector3.Right * (LeverLength / 2), steel);
        var handle = Cylinder(Lever, .034f, .034f, .338f, Vector3.Right * LeverLength, Wood("997450"), Axle, 8);
        handle.Name = "PumpHandle";
        picking = [shell, lid, seal, elbow, dial, shaft, link, bar, handle];
        SetPress(0);
        SetPressure(0);
    }

    /// <summary>The rocker swings on a fixed lower pin, and the polished rod moves vertically through its seal.</summary>
    private static Stroke At(float pressed) {
        float tilt = Mathf.DegToRad(Mathf.Lerp(RaisedAngle, PressedAngle, pressed));
        var along = new Vector3(MathF.Cos(tilt), -MathF.Sin(tilt), 0);
        float offset = -ChamberZ + ToLink * along.X - FixedPin.X;
        var rock = new Vector3(FixedPin.X + offset, FixedPin.Y + MathF.Sqrt(LinkLength * LinkLength - offset * offset), 0);
        var pin = rock - ToLink * along;
        return new Stroke(pin, rock, pin + along * LeverLength, -tilt);
    }

    /// <summary>Contact center for either hand in the pump's own frame.</summary>
    public static Vector3 GripAt(float pressed, bool left) {
        var at = At(pressed).Grip;
        return new Vector3(left ? HalfGrip : -HalfGrip, at.Y, -at.X);
    }

    public void SetPress(float pressed) {
        var stroke = At(pressed);
        Lever.Position = stroke.PistonPin;
        Lever.Rotation = new Vector3(0, 0, stroke.Tilt);
        piston.Position = stroke.PistonPin;
        var rocker = stroke.RockerPin - FixedPin;
        linkage.Rotation = new Vector3(0, 0, MathF.Atan2(rocker.Y, rocker.X));
    }

    public void SetPressure(float pressure) => needle.RotationDegrees = new Vector3(0, 0, WorldModels.Gauge(pressure));

    private static void Bar(Node3D parent, Vector3 from, Vector3 to, float width, float depth, Material material) {
        var span = to - from;
        Box(parent, new Vector3(span.Length() + width * .3f, width, depth), (from + to) / 2, material,
            new Vector3(0, 0, Mathf.RadToDeg(MathF.Atan2(span.Y, span.X))));
    }

    private static MeshInstance3D Pin(Node3D at, Vector3 where, float radius, float width, Material metal) =>
        Cylinder(at, radius, radius, width, where, metal, Axle, 8);

    private static Node3D Gauge(Node3D parent, Material brass, Material steel, Material dark, out MeshInstance3D dial) {
        Cylinder(parent, .046f, .046f, .16f, new Vector3(.122f, .448f, ChamberZ), steel, new Vector3(0, 0, 90), 8);
        Post(parent, .044f, .035f, .144f, new Vector3(.195f, .430f, ChamberZ), brass, 8);
        var gauge = Pivot(parent, new Vector3(.195f, .639f, ChamberZ), new Vector3(-36, 43, 0));
        dial = Cylinder(gauge, .107f, .107f, .035f, Vector3.Zero, dark, Axle, 12);
        Cylinder(gauge, .094f, .094f, .006f, Vector3.Back * .018f, Mat("efe5ca", .76f), Axle, 12);
        for (int step = 0; step <= 8; step++) {
            float angle = WorldModels.Gauge(step / 8f);
            bool major = step % 2 == 0;
            var at = (Vector3.Up * .080f).Rotated(Vector3.Back, Mathf.DegToRad(angle));
            Box(gauge, new Vector3(major ? .007f : .004f, major ? .018f : .010f, .004f),
                at + Vector3.Back * .022f, dark, new Vector3(0, 0, angle));
        }
        var needle = Pivot(gauge, Vector3.Back * .028f);
        Box(needle, new Vector3(.007f, .103f, .005f), Vector3.Up * .030f, Metal("963a32", .62f, .26f));
        Cylinder(gauge, .012f, .012f, .007f, Vector3.Back * .032f, brass, Axle, 8);
        return needle;
    }

    private static ArrayMesh Outlet() {
        const float bend = .123f, radius = .064f;
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        var from = new Vector3(0, .442f, .317f);
        var up = Vector3.Up;
        float fromRadius = radius;
        for (int step = 0; step <= 5; step++) {
            float angle = step * Mathf.Pi / 10;
            var radial = new Vector3(0, MathF.Cos(angle), -MathF.Sin(angle));
            Extend(new Vector3(0, .442f - bend, bend) + bend * radial, radial, radius);
        }
        Extend(new Vector3(0, .061f, 0), Vector3.Forward, radius);
        Extend(Vector3.Zero, Vector3.Forward, .047f);
        Extend(new Vector3(0, -.025f, 0), Vector3.Forward, .047f);
        return s.Commit();

        void Extend(Vector3 to, Vector3 nextUp, float toRadius) {
            for (int side = 0; side < 10; side++) {
                var a = Point(from, up, fromRadius, side); var b = Point(to, nextUp, toRadius, side);
                var c = Point(to, nextUp, toRadius, side + 1); var d = Point(from, up, fromRadius, side + 1);
                Triangle(a, b, c); Triangle(a, c, d);
            }
            from = to; up = nextUp; fromRadius = toRadius;
        }

        static Vector3 Point(Vector3 at, Vector3 normal, float width, int side) {
            float angle = side * Mathf.Tau / 10;
            return at + width * (normal * MathF.Cos(angle) + Vector3.Right * MathF.Sin(angle));
        }

        void Triangle(Vector3 a, Vector3 b, Vector3 c) {
            s.SetNormal((c - a).Cross(b - a).Normalized());
            s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        }
    }
}
