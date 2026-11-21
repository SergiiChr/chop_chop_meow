using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Steel pipes on the underground view and the loose pipe shown while placing.</summary>
internal static class SteelPipeShape {
    private const int Directions = 8, Sides = 8;
    private const float BarrelHeight = .128f, BarrelRadius = .075f;
    private const string Steel = "778e9a", Collar = "a7b7bd", Dark = "384b57";
    private const int EastWest = 0b0001_0001, NorthSouth = 0b0100_0100;
    private static readonly Dictionary<(int Ports, int Links), ArrayMesh> Tiles = new();
    public static readonly StandardMaterial3D Finish = Vertex(Surface.Metal, .52f, .47f);
    private static ArrayMesh? loose, heap;
    public static Mesh Loose => loose ??= BuildLoose();
    public static Mesh Heap => heap ??= BuildHeap();
    private readonly record struct Ring(float At, float Radius, string Color);

    public static MeshInstance3D Build(Node3D parent, int ports = EastWest, int links = 0) {
        var pipe = Add(parent, Loose, Finish, Vector3.Zero);
        SetConnections(pipe, ports, links);
        return pipe;
    }

    /// <summary>
    /// Closed sides are bare, unconnected cardinal ports end in hollow collars, and links meet halfway to the neighboring tile.
    /// Both masks run clockwise from +X toward +Z.
    /// </summary>
    public static void SetConnections(MeshInstance3D pipe, int ports, int links) {
        ports &= EastWest | NorthSouth;
        bool isolatedStraight = links == 0 && ports is EastWest or NorthSouth;
        if (isolatedStraight) pipe.Mesh = Loose;
        else {
            if (!Tiles.TryGetValue((ports, links), out var tile)) Tiles[(ports, links)] = tile = Tile(ports, links);
            pipe.Mesh = tile;
        }
        pipe.Rotation = new Vector3(0, isolatedStraight && ports == NorthSouth ? Mathf.Pi / 2 : 0, 0);
    }

    private static ArrayMesh BuildLoose() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        Profile(s, Vector3.Up * BarrelHeight, Vector3.Right, Vector3.Up, [
            new(-.39f, .052f, Dark), new(-.45f, .052f, Dark), new(-.45f, .086f, Collar),
            new(-.432f, .099f, Collar), new(-.382f, .099f, Collar), new(-.367f, BarrelRadius, Steel),
            new(.367f, BarrelRadius, Steel), new(.382f, .099f, Collar), new(.432f, .099f, Collar),
            new(.45f, .086f, Collar), new(.45f, .052f, Dark), new(.39f, .052f, Dark)]);
        return s.Commit();
    }

    private static ArrayMesh Tile(int ports, int links) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        var middle = Vector3.Up * BarrelHeight;
        // The inspection cover hides where the pipe branches meet.
        Profile(s, Vector3.Zero, Vector3.Up, Vector3.Right, [
            new(.045f, 0, Dark), new(.045f, .16f, Dark), new(.075f, .195f, Steel), new(.182f, .195f, Steel),
            new(.187f, .207f, Dark), new(.222f, .207f, Collar), new(.238f, .189f, Collar),
            new(.238f, 0, Steel)]);
        for (int bit = 0; bit < Directions; bit++) {
            int direction = 1 << bit;
            if (((ports | links) & direction) == 0) continue;
            float angle = bit * Mathf.Tau / Directions;
            var along = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            if ((links & direction) != 0) {
                float edge = (bit % 2 == 0 ? 1 : Mathf.Sqrt2) / 2;
                Profile(s, middle, along, Vector3.Up, [
                    new(.115f, BarrelRadius, Steel), new(edge - .129f, BarrelRadius, Steel),
                    new(edge - .112f, .096f, Collar), new(edge - .068f, .096f, Collar),
                    new(edge - .05f, BarrelRadius, Steel), new(edge, BarrelRadius, Steel)]);
            } else {
                Profile(s, middle, along, Vector3.Up, [
                    new(.115f, BarrelRadius, Steel), new(.345f, BarrelRadius, Steel), new(.365f, .096f, Collar),
                    new(.416f, .096f, Collar), new(.435f, .084f, Collar),
                    new(.435f, .052f, Dark), new(.377f, .052f, Dark)]);
            }
        }
        return s.Commit();
    }

    /// <summary>Six sides and straight collar edges give a pile room to show stacked rows.</summary>
    private static ArrayMesh BuildHeap() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        Profile(s, Vector3.Zero, Vector3.Right, Vector3.Up, [
            new(-.45f, .057f, Dark), new(-.45f, .099f, Collar), new(-.40f, BarrelRadius, Steel),
            new(.40f, BarrelRadius, Collar), new(.45f, .099f, Collar), new(.45f, .057f, Dark)], 6);
        return s.Commit();
    }

    private static void Profile(SurfaceTool s, Vector3 center, Vector3 along, Vector3 radial,
        ReadOnlySpan<Ring> rings, int sides = Sides) {
        var tangent = along.Cross(radial);
        Vector3 Point(Ring ring, int side) {
            float angle = side * Mathf.Tau / sides;
            return center + ring.At * along + ring.Radius * (radial * MathF.Cos(angle) + tangent * MathF.Sin(angle));
        }
        for (int row = 1; row < rings.Length; row++) {
            var from = rings[row - 1]; var to = rings[row];
            s.SetColor(new Color(from.Color));
            for (int side = 0; side < sides; side++) {
                var a = Point(from, side); var b = Point(to, side);
                var c = Point(to, side + 1); var d = Point(from, side + 1);
                if (to.Radius > 0) Triangle(s, a, b, c);
                if (from.Radius > 0) Triangle(s, a, c, d);
            }
        }
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c) {
        s.SetNormal((c - a).Cross(b - a).Normalized());
        s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
