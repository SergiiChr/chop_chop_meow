using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Where a blow lands on what it works: the spot on a trunk, a rock, an anvil, a bench, a stump or a bush that a tool's
/// working end goes to, for someone working it from where they stand.
/// </summary>
public sealed class StrikePoints {
    /// <summary>The height a pick lands on a rock's face, and a shovel's blade at the foot of a stump or bush, measured over the ground.</summary>
    private const float RockFace = .45f, Foot = .05f;
    /// <summary>How far in from the edge of a work top a hammer lands, and how far out from a stump's or bush's foot a shovel goes in.</summary>
    private const float TopInset = .08f, FootOut = .04f;
    /// <summary>How finely a worker's place is told apart before the spot is worked out again, in metres.</summary>
    private const float Cell = .05f;
    /// <summary>How many steps a swing is followed in, looking for where it first meets what it works.</summary>
    private const int SwingSteps = 32;
    /// <summary>How many steps a hammer's spot is moved in by from the edge of a work top's box, looking for the top under it.</summary>
    private const int TopSteps = 10;

    /// <summary>The triangles of each mesh, in its own frame, three corners each.</summary>
    private static readonly Dictionary<Mesh, Vector3[]> triangles = new();
    private readonly Dictionary<(int Thing, int X, int Y, ActorAction Action), Vector3?> known = new();

    /// <summary>
    /// The point in the world where <paramref name="action"/>'s tool lands on <paramref name="subject"/>, drawn as <paramref name="prop"/>,
    /// for someone working it from <paramref name="from"/>, turned to face it as they come to; null for an action that lands on nothing there.
    /// </summary>
    public Vector3? At(Thing? subject, Vector2 from, ActorAction action, Prop? prop) {
        if (subject == null || prop == null || !Aimed(action, subject.Kind)) return null;
        var key = (subject.Id, (int)MathF.Round(from.X / Cell), (int)MathF.Round(from.Y / Cell), action);
        if (known.TryGetValue(key, out var spot)) return spot;
        // Spots are kept while people work in place; a long day's worth is thrown away at once.
        if (known.Count > 512) known.Clear();
        var rig = new Transform3D(Basis.FromEuler(new Vector3(0, ActorAnimation.Yaw(subject.Pos - from, 0), 0)), WorldView.To3(from));
        spot = action == ActorAction.Dig ? AtTheFoot(subject, from, prop) : OnTheSwing(subject, rig, action, prop) ?? Facing(subject, from, action, prop);
        known[key] = spot;
        return spot;
    }

    /// <summary>Whether a blow of this action lands on a thing of this kind.</summary>
    private static bool Aimed(ActorAction action, ThingKind kind) => action switch {
        ActorAction.Chop or ActorAction.SteelChop => kind == ThingKind.Tree,
        ActorAction.Mine or ActorAction.SteelMine => kind is ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin,
        ActorAction.Hammer => kind is ThingKind.Anvil or ThingKind.Bench,
        ActorAction.Dig => kind is ThingKind.Stump or ThingKind.Bush,
        _ => false
    };

    /// <summary>Where the blow's own swing first meets what it works, or null where the swing passes it by.</summary>
    private static Vector3? OnTheSwing(Thing subject, Transform3D rig, ActorAction action, Prop prop) {
        var trunk = subject.Kind == ThingKind.Tree ? WorldModels.Trunk(subject.Id) : default;
        var axis = WorldView.To3(subject.Pos) + trunk.Seat;
        var last = rig * ActorAnimation.Swing(action, 0)!.Value;
        for (int i = 1; i <= SwingSteps; i++) {
            var next = rig * ActorAnimation.Swing(action, i / (float)SwingSteps)!.Value;
            if (subject.Kind == ThingKind.Tree) {
                // A trunk stands upright: the axe meets it where it comes within the trunk's thickness of its middle.
                if (InTrunk(next, axis, trunk.Radius)) return Into(last, next, axis, trunk.Radius);
            } else if (Across(prop, last, next) is float t) return last.Lerp(next, t);
            last = next;
        }
        return null;
    }

    /// <summary>Whether a point is within a trunk of <paramref name="radius"/> standing upright through <paramref name="axis"/>.</summary>
    private static bool InTrunk(Vector3 at, Vector3 axis, float radius) => new Vector2(at.X - axis.X, at.Z - axis.Z).Length() <= radius;

    /// <summary>The first point between two places a swing passes where it is within the trunk, found by halving.</summary>
    private static Vector3 Into(Vector3 outside, Vector3 inside, Vector3 axis, float radius) {
        for (int i = 0; i < 12; i++) {
            var mid = outside.Lerp(inside, .5f);
            if (InTrunk(mid, axis, radius)) inside = mid; else outside = mid;
        }
        return inside;
    }

    /// <summary>
    /// Where a blow that its swing does not take to what it works lands: the side facing the worker, at the height the swing
    /// comes down to on a trunk, a rock's face or a work top's near edge.
    /// </summary>
    private static Vector3? Facing(Thing subject, Vector2 from, ActorAction action, Prop prop) {
        var spot = WorldView.To3(subject.Pos);
        switch (subject.Kind) {
            case ThingKind.Tree: {
                    var (seat, radius) = WorldModels.Trunk(subject.Id);
                    var trunk = new Vector2(spot.X + seat.X, spot.Z + seat.Z);
                    return WorldView.To3(trunk + (from - trunk).Normalized() * radius, ActorAnimation.Swing(action, 1)?.Y ?? 1);
                }
            case ThingKind.Anvil or ThingKind.Bench: {
                    var box = Bounds(prop);
                    var near = new Vector2(Math.Clamp(from.X, box.Position.X + TopInset, box.End.X - TopInset), Math.Clamp(from.Y, box.Position.Z + TopInset, box.End.Z - TopInset));
                    // What stands on the work top widens the box round it: walk in toward the middle until the top is under the hammer.
                    var middle = new Vector2(box.GetCenter().X, box.GetCenter().Z);
                    for (int i = 0; i <= TopSteps; i++) {
                        var at = near.Lerp(middle, i / (float)TopSteps);
                        if (Down(prop, at) is float top) return WorldView.To3(at, top);
                    }
                    return null;
                }
            default: {
                    var toward = (from - subject.Pos).Normalized();
                    return Out(prop, new Vector3(subject.Pos.X, RockFace, subject.Pos.Y), new Vector3(toward.X, 0, toward.Y)) is float reach ? WorldView.To3(subject.Pos + toward * reach, RockFace) : null;
                }
        }
    }

    /// <summary>Where a shovel goes into the ground: just out from the foot of a stump or bush, on the side facing the digger.</summary>
    private static Vector3? AtTheFoot(Thing subject, Vector2 from, Prop prop) {
        var spot = WorldView.To3(subject.Pos);
        var centre = prop is DugProp dug ? new Vector2(spot.X + dug.Seat.X, spot.Z + dug.Seat.Z) : subject.Pos;
        var toward = (from - centre).Normalized();
        return Out(prop, new Vector3(centre.X, Foot, centre.Y), new Vector3(toward.X, 0, toward.Y)) is float reach ? WorldView.To3(centre + toward * (reach + FootOut)) : null;
    }

    /// <summary>How far along the line between two points it first crosses a face of a prop's model, as a share of the way, or null where it crosses none.</summary>
    private static float? Across(Prop prop, Vector3 from, Vector3 to) {
        float length = from.DistanceTo(to);
        if (length < 1e-5f) return null;
        float? first = null;
        foreach (var (mesh, place) in Meshes(prop.Body))
            if (Farthest(mesh, place, from, (to - from) / length, nearest: true) is float t && t <= length && (first == null || t < first * length)) first = t / length;
        return first;
    }

    /// <summary>The box round every mesh of a prop's model, in the world.</summary>
    private static Aabb Bounds(Prop prop) {
        Aabb? box = null;
        foreach (var (mesh, place) in Meshes(prop.Body)) {
            var part = place * mesh.GetAabb();
            box = box is Aabb so ? so.Merge(part) : part;
        }
        return box ?? new Aabb(prop.Root.GlobalPosition, Vector3.Zero);
    }

    /// <summary>How high the highest face of a prop's model is straight above a ground spot, or null over none.</summary>
    private static float? Down(Prop prop, Vector2 at) {
        float? top = null;
        var from = new Vector3(at.X, 10, at.Y);
        foreach (var (mesh, place) in Meshes(prop.Body))
            if (Farthest(mesh, place, from, Vector3.Down, nearest: true) is float t) top = Math.Max(top ?? float.MinValue, 10 - t);
        return top;
    }

    /// <summary>How far out along a level line from <paramref name="from"/> a prop's model ends, or null where the line meets none of it.</summary>
    private static float? Out(Prop prop, Vector3 from, Vector3 way) {
        float? reach = null;
        foreach (var (mesh, place) in Meshes(prop.Body))
            if (Farthest(mesh, place, from, way, nearest: false) is float t) reach = Math.Max(reach ?? 0, t);
        return reach;
    }

    /// <summary>The meshes <see cref="Meshes"/> last found, kept so a search makes no garbage.</summary>
    private static readonly List<(Mesh Mesh, Transform3D Place)> found = new();

    /// <summary>Every mesh drawn under a node, with where it is placed in the world.</summary>
    private static List<(Mesh Mesh, Transform3D Place)> Meshes(Node node) {
        found.Clear();
        AddMeshes(node);
        return found;
    }

    /// <summary>Adds the meshes drawn under a node to <see cref="found"/>, walking its children by index.</summary>
    private static void AddMeshes(Node node) {
        if (node is MeshInstance3D { Mesh: Mesh mesh, Visible: true } drawn) found.Add((mesh, drawn.GlobalTransform));
        for (int i = 0; i < node.GetChildCount(); i++) AddMeshes(node.GetChild(i));
    }

    /// <summary>The distance along a line to the nearest or the farthest face of a mesh it crosses, or null when it crosses none.</summary>
    private static float? Farthest(Mesh mesh, Transform3D place, Vector3 from, Vector3 way, bool nearest) {
        if (!triangles.TryGetValue(mesh, out var corners)) triangles[mesh] = corners = Corners(mesh);
        var inverse = place.AffineInverse();
        Vector3 origin = inverse * from, direction = inverse.Basis * way;
        // The line's length in the mesh's frame, which a scale stretches.
        float scale = direction.Length();
        direction /= scale;
        float? best = null;
        for (int i = 0; i + 2 < corners.Length; i += 3)
            if (NatureShape.Hit(origin, direction, corners[i], corners[i + 1], corners[i + 2]) is float t && t > 0 && (best == null || (nearest ? t < best : t > best))) best = t;
        return best / scale;
    }

    private static Vector3[] Corners(Mesh mesh) {
        var corners = new List<Vector3>();
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++) {
            var arrays = mesh.SurfaceGetArrays(surface);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var order = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (order.Length == 0) corners.AddRange(points);
            else foreach (int i in order) corners.Add(points[i]);
        }
        return corners.ToArray();
    }
}
