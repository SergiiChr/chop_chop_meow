using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Steel vessels shared by the world, the tool rack, carried props, icons and heaps.</summary>
internal static class VesselShape {
    public const float BarrelHeight = .56f, BarrelRadius = .19f;
    public const float BucketRim = .271f, BucketRadius = .157f;
    public static readonly Vector3 BarrelSpout = new(0, .309f, .267f);
    public static readonly Vector3 BarrelFillOpening = new(.086f, .563f, 0);
    public static readonly Vector3 BucketHinge = new(0, .247f, 0);
    public static readonly Vector3 BucketGrip = new(0, .455f, 0);

    private static Mesh? barrel, barrelHeap, bucket, bail;
    private static readonly Ring[] BarrelProfile = [
        new(0, 0, Dark), new(.172f, 0, Dark), new(.186f, .008f, Edge),
        new(.19f, .019f, Edge), new(.177f, .03f, Steel), new(.177f, .173f, Steel),
        new(.187f, .181f, Edge), new(.187f, .194f, Edge), new(.177f, .202f, Steel),
        new(.177f, .354f, Steel), new(.187f, .362f, Edge), new(.187f, .375f, Edge),
        new(.177f, .383f, Steel), new(.177f, .531f, Steel), new(.19f, .541f, Edge),
        new(.186f, BarrelHeight, Edge), new(.169f, BarrelHeight, Edge),
        new(.164f, .548f, Dark), new(0, .548f, Dark)];
    public static Mesh BarrelBody => barrel ??= Profile(BarrelProfile, 14);
    public static Mesh BarrelHeap => barrelHeap ??= Profile(BarrelProfile, 10);
    public static Mesh BucketBody => bucket ??= Profile([
        new(0, 0, Dark), new(.098f, 0, Dark), new(.107f, .007f, Edge),
        new(.105f, .02f, Edge), new(.149f, .247f, Steel), new(BucketRadius, .253f, Edge),
        new(BucketRadius, .268f, Edge), new(.15f, BucketRim, Edge), new(.143f, .265f, Edge),
        new(.145f, .248f, Dark), new(.096f, .024f, Dark), new(0, .024f, Dark)], 14);
    public static Mesh BucketBail => bail ??= BuildBail();
    public static readonly StandardMaterial3D Finish = Vertex(Surface.Metal, .53f, .37f);

    private static readonly TorusMesh FillSeat = new() { InnerRadius = .020f, OuterRadius = .036f, Rings = 10, RingSegments = 5 };
    private const string Steel = "687d89", Edge = "a0abb0", Dark = "52616b", Wire = "b4bec1", Grip = "9b734d";

    public static (Node3D Vessel, Node3D Bung) AddBarrel(Node3D parent, Vector3 at) {
        var vessel = Pivot(parent, at);
        Add(vessel, BarrelBody, Finish, Vector3.Zero);
        var seat = BarrelFillOpening - Vector3.Up * .008f;
        Add(vessel, FillSeat, Metal(Dark, .5f, .5f), seat);
        Post(vessel, .021f, .021f, .003f, seat - Vector3.Up * .007f, Mat("161e24"), 8);
        var bung = Pivot(vessel, BarrelFillOpening - Vector3.Up * .002f);
        Post(bung, .027f, .024f, .013f, Vector3.Zero, Metal(Edge, .5f, .5f), 6);
        AddSpigot(vessel);
        return (vessel, bung);
    }

    private static void AddSpigot(Node3D vessel) {
        var fitting = Metal("a09377", .45f, .55f);
        var across = new Vector3(90, 0, 0);
        Cylinder(vessel, .032f, .032f, .025f, new Vector3(0, .352f, .181f), fitting, across, 8);
        Cylinder(vessel, .017f, .017f, .092f, new Vector3(0, .352f, .224f), fitting, across, 8);
        Post(vessel, .019f, .022f, .052f, BarrelSpout, fitting, 8);
        Box(vessel, new Vector3(.073f, .012f, .023f), new Vector3(.019f, .376f, .255f), Metal("745145", .55f, .5f));
    }

    public static Node3D AddBucket(Node3D parent, Vector3 at) {
        var vessel = Pivot(parent, at);
        Add(vessel, BucketBody, Finish, Vector3.Zero);
        Add(vessel, BucketBail, Finish, Vector3.Zero);
        return vessel;
    }

    private readonly record struct Ring(float Radius, float Height, string Color);

    /// <summary>Runs from the underside around the rolled rim and back into the vessel.</summary>
    private static ArrayMesh Profile(ReadOnlySpan<Ring> rings, int sides) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        for (int row = 1; row < rings.Length; row++) {
            var from = rings[row - 1]; var to = rings[row];
            for (int side = 0; side < sides; side++) {
                float a = Mathf.Tau * side / sides, b = Mathf.Tau * (side + 1) / sides;
                var p = Point(from, a); var q = Point(from, b);
                var r = Point(to, b); var t = Point(to, a);
                if (from.Radius > 0) Triangle(s, p, q, r, from.Color);
                if (to.Radius > 0) Triangle(s, p, r, t, from.Color);
            }
        }
        return s.Commit();
    }

    private static Vector3 Point(Ring ring, float angle) =>
        new(MathF.Cos(angle) * ring.Radius, ring.Height, MathF.Sin(angle) * ring.Radius);

    /// <summary>A wire bail in the XY plane. Its middle has a thicker grip.</summary>
    private static ArrayMesh BuildBail() {
        const int sides = 6;
        float[] angles = [0, 15, 30, 45, 67, 71, 90, 109, 113, 135, 150, 165, 180];
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        for (int row = 1; row < angles.Length; row++) {
            float a = Mathf.DegToRad(angles[row - 1]), b = Mathf.DegToRad(angles[row]);
            for (int side = 0; side < sides; side++) {
                float u = Mathf.Tau * side / sides, v = Mathf.Tau * (side + 1) / sides;
                var p = WirePoint(row - 1, a, u); var q = WirePoint(row - 1, a, v);
                var r = WirePoint(row, b, v); var t = WirePoint(row, b, u);
                string color = row is >= 5 and <= 8 ? Grip : Wire;
                Triangle(s, p, q, r, color); Triangle(s, p, r, t, color);
            }
        }
        return s.Commit();
    }

    private static Vector3 WirePoint(int row, float along, float around) {
        float radius = row is >= 5 and <= 7 ? .016f : .010f;
        var outward = new Vector3(MathF.Cos(along), MathF.Sin(along), 0);
        var center = BucketHinge + new Vector3(.151f * outward.X, (BucketGrip.Y - BucketHinge.Y) * outward.Y, 0);
        return center + radius * (outward * MathF.Cos(around) + Vector3.Back * MathF.Sin(around));
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, string tint) {
        s.SetColor(new Color(tint));
        s.SetNormal((c - a).Cross(b - a).Normalized());
        s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
