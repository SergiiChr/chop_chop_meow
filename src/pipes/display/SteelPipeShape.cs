using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Thin, open steel tubes for the underground layer, the plans and storage piles.</summary>
internal static class SteelPipeShape {
    private const int East = 1, South = 4, West = 16, North = 64;
    private const int EastWest = East | West, NorthSouth = North | South;
    internal const float CenterHeight = .065f, OuterRadius = .047f;
    private const float InnerRadius = .033f;
    private const float LooseEnd = .45f, JoinedEnd = .5f;
    private const int Sides = 8;
    private static readonly Color Steel = new("8e9fa8"), Inside = new("566872");
    private static readonly Vector3 Center = Vector3.Up * CenterHeight;
    private static readonly Vector3[] Directions = [Vector3.Right, Vector3.Back, Vector3.Left, Vector3.Forward];
    private static readonly Vector2[] Circle = Ring(Sides);
    private static readonly Dictionary<(int Ports, int Links), ArrayMesh> Tiles = new();
    public static readonly StandardMaterial3D Finish = Vertex(Surface.Metal, .52f, .47f);
    private static ArrayMesh? loose, heap;
    public static Mesh Loose => loose ??= Straight(LooseEnd, LooseEnd, Circle, interior: true);
    public static Mesh Heap => heap ??= Straight(LooseEnd, LooseEnd, Ring(6), interior: false);

    private readonly record struct VertexAt(Vector3 Position, Vector3 Normal);

    public static MeshInstance3D Build(Node3D parent, int ports = EastWest, int links = 0) {
        var pipe = Add(parent, Loose, Finish, Vector3.Zero);
        SetConnections(pipe, ports, links);
        return pipe;
    }

    /// <summary>
    /// Cardinal bits run clockwise from +X toward +Z.
    /// Joined walls meet at the tile boundary, and free ends stop short with their cut edges visible.
    /// </summary>
    public static void SetConnections(MeshInstance3D pipe, int ports, int links) {
        ports &= EastWest | NorthSouth;
        links &= ports;
        bool straight = ports is EastWest or NorthSouth;
        if (straight && links == 0) pipe.Mesh = Loose;
        else {
            if (!Tiles.TryGetValue((ports, links), out var tile)) {
                tile = straight
                    ? Straight(End(links, ports == EastWest ? West : South), End(links, ports == EastWest ? East : North),
                        Circle, interior: true)
                    : Junction(ports, links);
                Tiles[(ports, links)] = tile;
            }
            pipe.Mesh = tile;
        }
        pipe.Rotation = new Vector3(0, ports == NorthSouth ? Mathf.Pi / 2 : 0, 0);
    }

    /// <summary>Bounds for one tube from the middle of its tile to the open end.</summary>
    internal static Aabb ArmBounds(Vector2I offset, int port, int links) {
        var end = new Vector3(offset.X, 0, offset.Y) * End(links, port);
        var radius = new Vector3(offset.X == 0 ? OuterRadius : 0, OuterRadius, offset.Y == 0 ? OuterRadius : 0);
        var start = end.Min(Vector3.Zero) - radius;
        return new Aabb(Center + start, end.Max(Vector3.Zero) + radius - start);
    }

    private static float End(int links, int port) => (links & port) == 0 ? LooseEnd : JoinedEnd;

    private static ArrayMesh Straight(float from, float to, Vector2[] ring, bool interior) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        Wall(OuterRadius, Steel, 1);
        // Piles show the open cut edges with six sides, with the bore wall left out.
        if (interior) Wall(InnerRadius, Inside, -1);
        if (from == LooseEnd) Lip(s, Vector3.Left, from, ring);
        if (to == LooseEnd) Lip(s, Vector3.Right, to, ring);
        return s.Commit();

        void Wall(float radius, Color color, int facing) {
            s.SetColor(color);
            for (int side = 0; side < ring.Length; side++) {
                var a = Around(Vector3.Back, ring[side]); var b = Around(Vector3.Back, ring[(side + 1) % ring.Length]);
                VertexAt Point(float x, Vector3 radial) =>
                    new(Center + Vector3.Right * x + radial * radius, radial * facing);
                Quad(s, Point(-from, a), Point(to, a), Point(to, b), Point(-from, b));
            }
        }
    }

    /// <summary>Both walls follow the perimeter of each horizontal slice through the intersecting tubes.</summary>
    private static ArrayMesh Junction(int ports, int links) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        Wall(OuterRadius, Steel, 1);
        Wall(InnerRadius, Inside, -1);
        for (int i = 0; i < Directions.Length; i++) {
            int bit = 1 << (2 * i);
            if ((ports & bit) != 0 && (links & bit) == 0) Lip(s, Directions[i], LooseEnd, Circle);
        }
        return s.Commit();

        void Wall(float radius, Color color, int facing) {
            s.SetColor(color);
            for (int i = 0; i < Directions.Length; i++) {
                var along = Directions[i];
                var across = along.Cross(Vector3.Up);
                int bit = 1 << (2 * i);
                float length = End(links, bit);
                for (int level = 0; level < Sides / 2; level++) {
                    var top = Circle[level]; var bottom = Circle[level + 1];
                    if ((ports & bit) == 0) {
                        // A missing branch is a continuous side of the pipe running across its mouth.
                        VertexAt At(Vector2 arc, int side) =>
                            new(Center + (Around(along, arc) + across * (side * arc.X)) * radius,
                                Around(along, arc) * facing);
                        Quad(s, At(top, -1), At(top, 1), At(bottom, 1), At(bottom, -1));
                        continue;
                    }
                    for (int side = -1; side <= 1; side += 2) {
                        var flank = across * side;
                        VertexAt At(Vector2 arc, float reach) =>
                            new(Center + Around(flank, arc) * radius + along * reach, Around(flank, arc) * facing);
                        Quad(s, At(top, top.X * radius), At(top, length),
                            At(bottom, length), At(bottom, bottom.X * radius));
                    }
                }
            }
        }
    }

    private static void Lip(SurfaceTool s, Vector3 along, float length, Vector2[] ring) {
        s.SetColor(Steel);
        var at = Center + along * length;
        var across = along.Cross(Vector3.Up);
        for (int i = 0; i < ring.Length; i++) {
            var a = Around(across, ring[i]); var b = Around(across, ring[(i + 1) % ring.Length]);
            Quad(s, new(at + a * OuterRadius, along), new(at + b * OuterRadius, along),
                new(at + b * InnerRadius, along), new(at + a * InnerRadius, along));
        }
    }

    private static Vector3 Around(Vector3 across, Vector2 arc) => across * arc.X + Vector3.Up * arc.Y;

    private static Vector2[] Ring(int sides) {
        var ring = new Vector2[sides];
        for (int i = 0; i <= sides / 2; i++) {
            float angle = i * Mathf.Tau / sides;
            float span = i == 0 || i == sides / 2 ? 0 : MathF.Sin(angle);
            ring[i] = new Vector2(span, MathF.Cos(angle));
            if (span != 0) ring[sides - i] = new Vector2(-span, ring[i].Y);
        }
        return ring;
    }

    private static void Quad(SurfaceTool s, VertexAt a, VertexAt b, VertexAt c, VertexAt d) {
        Triangle(s, a, b, c); Triangle(s, a, c, d);
    }

    private static void Triangle(SurfaceTool s, VertexAt a, VertexAt b, VertexAt c) {
        var normal = (c.Position - a.Position).Cross(b.Position - a.Position);
        if (normal.LengthSquared() < 1e-15f) return;
        if (normal.Dot(a.Normal + b.Normal + c.Normal) < 0) (b, c) = (c, b);
        s.SetNormal(a.Normal); s.AddVertex(a.Position);
        s.SetNormal(b.Normal); s.AddVertex(b.Position);
        s.SetNormal(c.Normal); s.AddVertex(c.Position);
    }
}
