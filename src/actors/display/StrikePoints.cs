using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Where a blow lands on what it works: the spot on a trunk, a rock, an anvil, a bench, a stump or a bush that a tool's
/// working end goes to, for someone working it from where they stand.
/// </summary>
public sealed class StrikePoints {
    /// <summary>The highest a pick lands on a rock's face, and the height of a shovel's blade at the foot of a stump or bush, over the ground.</summary>
    private const float RockFace = .45f, Foot = .05f;
    /// <summary>How far in from the edge of a work top a hammer lands, and how far out from a stump's or bush's foot a shovel goes in.</summary>
    private const float TopInset = .08f, FootOut = .04f;
    /// <summary>How finely a worker's place is told apart before the spot is worked out again, in metres.</summary>
    private const float Cell = .05f;
    /// <summary>How many steps a swing is followed in, looking for where it first meets what it works.</summary>
    private const int SwingSteps = 32;
    /// <summary>How many steps a hammer's spot is moved in by from the edge of a work top's box, looking for the top under it.</summary>
    private const int TopSteps = 10;
    /// <summary>How many spots are kept; past that, the one worked out first is dropped for each new one.</summary>
    private const int Kept = 512;
    /// <summary>
    /// How far a mesh's box is grown before a line is tested against it, so a face lying on the box's side still
    /// counts, in metres.
    /// </summary>
    private const float BoxMargin = .001f;

    /// <summary>The triangles of each mesh, in its own frame, three corners each.</summary>
    private static readonly Dictionary<Mesh, Vector3[]> triangles = new();

    /// <summary>
    /// A mesh drawn in what is worked: its triangles' corners and the box round them in its own frame, and how it is
    /// placed in the world and back.
    /// </summary>
    private readonly record struct Part(Vector3[] Corners, Aabb Box, Transform3D Place, Transform3D Inverse);

    /// <summary>Spots worked out by thing, the cell its worker stands in and action, and the model used and its size then.</summary>
    private readonly Dictionary<(int Thing, int X, int Y, ActorAction Action), (Prop Prop, float Size, Vector3? Spot)> known =
        new();
    /// <summary>How much a model's size changes, as a share, before its spots are worked out again, as a rock shrinks while mined.</summary>
    private const float Resized = .01f;
    /// <summary>The keys of <see cref="known"/> in the order they were added, oldest first.</summary>
    private readonly Queue<(int Thing, int X, int Y, ActorAction Action)> added = new();
    /// <summary>The meshes of the model searched, refilled for each spot so a search makes no garbage.</summary>
    private readonly List<Part> parts = new();

    /// <summary>
    /// The point in the world where <paramref name="action"/>'s tool lands on <paramref name="subject"/>, drawn as <paramref name="prop"/>,
    /// for someone working it from <paramref name="from"/>, turned to face it as they come to; null for an action that lands on nothing there.
    /// A spot found on another model, as in a world loaded since, or on the model at another size, is worked out again.
    /// </summary>
    public Vector3? At(Thing? subject, Vector2 from, ActorAction action, Prop? prop) {
        if (subject == null || prop == null || !ActorAnimation.Works(action).Contains(subject.Kind)) return null;
        var key = (subject.Id, (int)MathF.Round(from.X / Cell), (int)MathF.Round(from.Y / Cell), action);
        float size = prop is TreeProp tree ? tree.GrowthScale : prop.Body.Scale.X;
        if (known.TryGetValue(key, out var was) && was.Prop == prop && Math.Abs(was.Size - size) < Resized) return was.Spot;
        if (!known.ContainsKey(key)) {
            if (added.Count == Kept) known.Remove(added.Dequeue());
            added.Enqueue(key);
        }
        parts.Clear();
        // A struck rock slides a little to the side for a moment as it shakes: its spot is found where it rests, so a blow aimed mid-shake still lands on it.
        AddParts(prop.Body, WorldView.Mined(subject.Kind) ? prop.Root.GlobalTransform.Basis * prop.Body.Position : Vector3.Zero);
        var meshes = CollectionsMarshal.AsSpan(parts);
        var rig = new Transform3D(Basis.FromEuler(new Vector3(0, ActorAnimation.Yaw(subject.Pos - from, 0), 0)), WorldView.To3(from));
        var spot = action == ActorAction.Dig ? AtTheFoot(subject, from, prop, meshes)
            : subject.Kind == ThingKind.Mine ? BesideTheMouth(subject, from)
            : OnTheSwing(subject, rig, action, meshes) ?? Facing(subject, from, action, prop, meshes);
        known[key] = (prop, size, spot);
        return spot;
    }

    /// <summary>Where the blow's own swing first meets what it works, or null where the swing passes it by.</summary>
    private static Vector3? OnTheSwing(Thing subject, Transform3D rig, ActorAction action, ReadOnlySpan<Part> meshes) {
        if (subject.Kind == ThingKind.Tree && !TreePlanting.IsAdult(subject)) return null;
        var trunk = subject.Kind == ThingKind.Tree ? WorldModels.Trunk(subject.Id, subject.TreeStage) : default;
        var axis = WorldView.To3(subject.Pos) + trunk.Seat;
        var last = rig * ActorAnimation.Swing(action, 0)!.Value;
        for (int i = 1; i <= SwingSteps; i++) {
            var next = rig * ActorAnimation.Swing(action, i / (float)SwingSteps)!.Value;
            if (subject.Kind == ThingKind.Tree) {
                // A trunk stands upright: the axe meets it where it comes within the trunk's thickness of its middle.
                if (InTrunk(next, axis, trunk.Radius)) return Into(last, next, axis, trunk.Radius);
            } else if (Across(meshes, last, next) is float t) return last.Lerp(next, t);
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
    private static Vector3? Facing(Thing subject, Vector2 from, ActorAction action, Prop prop,
        ReadOnlySpan<Part> meshes) {
        var spot = WorldView.To3(subject.Pos);
        switch (subject.Kind) {
            case ThingKind.Tree: {
                    var (seat, radius) = WorldModels.Trunk(subject.Id, subject.TreeStage);
                    var trunk = new Vector2(spot.X + seat.X, spot.Z + seat.Z);
                    float height = ActorAnimation.Swing(action, 1)?.Y ?? 1;
                    if (prop is TreeProp tree) height *= Math.Min(tree.GrowthScale, 1);
                    return WorldView.To3(trunk + (from - trunk).Normalized() * radius, height);
                }
            case ThingKind.Anvil or ThingKind.Bench: {
                    var box = prop.Body.GlobalTransform * WorldModels.Bounds(prop.Body);
                    var near = new Vector2(Math.Clamp(from.X, box.Position.X + TopInset, box.End.X - TopInset), Math.Clamp(from.Y, box.Position.Z + TopInset, box.End.Z - TopInset));
                    // What stands on the work top widens the box round it: walk in toward the middle until the top is under the hammer.
                    var middle = new Vector2(box.GetCenter().X, box.GetCenter().Z);
                    for (int i = 0; i <= TopSteps; i++) {
                        var at = near.Lerp(middle, i / (float)TopSteps);
                        if (Down(meshes, at) is float top) return WorldView.To3(at, top);
                    }
                    return null;
                }
            default: {
                    // A rock mined down is lower: the pick comes down halfway up it at most.
                    float height = Math.Min(RockFace, (prop.Body.GlobalTransform * WorldModels.Bounds(prop.Body)).GetCenter().Y);
                    var toward = (from - subject.Pos).Normalized();
                    var face = new Vector3(subject.Pos.X, height, subject.Pos.Y);
                    return Out(meshes, face, new Vector3(toward.X, 0, toward.Y)) is float reach
                        ? WorldView.To3(subject.Pos + toward * reach, height) : null;
                }
        }
    }

    /// <summary>
    /// Where a pick digging a mine's entrance lands: on the cliff's rock face just beside the cave mouth, on the digger's
    /// side, halfway up the mouth; the mine's own model is only a see-through marking.
    /// </summary>
    private static Vector3 BesideTheMouth(Thing mine, Vector2 from) => WorldView.To3(Mines.DigFace(mine, from), MouthBlow);

    /// <summary>How high over the ground a pick digging a mine's entrance lands on the rock, halfway up the cave mouth.</summary>
    private const float MouthBlow = .9f;

    /// <summary>Where a shovel goes into the ground: just out from the foot of a stump or bush, on the side facing the digger.</summary>
    private static Vector3? AtTheFoot(Thing subject, Vector2 from, Prop prop, ReadOnlySpan<Part> meshes) {
        var spot = WorldView.To3(subject.Pos);
        var centre = prop is DugProp dug ? new Vector2(spot.X + dug.Seat.X, spot.Z + dug.Seat.Z) : subject.Pos;
        var toward = (from - centre).Normalized();
        return Out(meshes, new Vector3(centre.X, Foot, centre.Y), new Vector3(toward.X, 0, toward.Y)) is float reach
            ? WorldView.To3(centre + toward * (reach + FootOut)) : null;
    }

    /// <summary>
    /// How far along the line between two points it first crosses a face of a model, as a share of the way, or null
    /// where it crosses none.
    /// </summary>
    private static float? Across(ReadOnlySpan<Part> meshes, Vector3 from, Vector3 to) {
        float length = from.DistanceTo(to);
        if (length < 1e-5f) return null;
        float? first = null;
        foreach (var part in meshes)
            if (Farthest(part, from, (to - from) / length, length, nearest: true) is float t && t <= length
                && (first == null || t < first * length)) first = t / length;
        return first;
    }

    /// <summary>How high the highest face of a model is straight above a ground spot, or null over none.</summary>
    private static float? Down(ReadOnlySpan<Part> meshes, Vector2 at) {
        float? top = null;
        var from = new Vector3(at.X, 10, at.Y);
        foreach (var part in meshes)
            if (Farthest(part, from, Vector3.Down, null, nearest: true) is float t)
                top = Math.Max(top ?? float.MinValue, 10 - t);
        return top;
    }

    /// <summary>
    /// How far out along a level line from <paramref name="from"/> a model ends, or null where the line meets none.
    /// </summary>
    private static float? Out(ReadOnlySpan<Part> meshes, Vector3 from, Vector3 way) {
        float? reach = null;
        foreach (var part in meshes)
            if (Farthest(part, from, way, null, nearest: false) is float t) reach = Math.Max(reach ?? 0, t);
        return reach;
    }

    /// <summary>Adds the meshes drawn under a node to <see cref="parts"/>, walking its children by index, each moved back by <paramref name="shaken"/>.</summary>
    private void AddParts(Node node, Vector3 shaken) {
        if (node is MeshInstance3D { Mesh: Mesh mesh, Visible: true } drawn) {
            if (!triangles.TryGetValue(mesh, out var corners)) triangles[mesh] = corners = Corners(mesh);
            var place = drawn.GlobalTransform;
            place.Origin -= shaken;
            parts.Add(new Part(corners, mesh.GetAabb().Grow(BoxMargin), place, place.AffineInverse()));
        }
        for (int i = 0; i < node.GetChildCount(); i++) AddParts(node.GetChild(i), shaken);
    }

    /// <summary>
    /// The corners of a mesh's triangles as its surfaces store them, three to a triangle.
    /// <see cref="Mesh.GetFaces"/> is not used: it snaps every corner to a tenth of a millimetre.
    /// </summary>
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

    /// <summary>
    /// The distance along a line to the nearest or the farthest face of a mesh it crosses, or null where it crosses
    /// none.
    /// The line runs <paramref name="length"/> metres, or on without end when that is null.
    /// </summary>
    private static float? Farthest(Part part, Vector3 from, Vector3 way, float? length, bool nearest) {
        Vector3 origin = part.Inverse * from, direction = part.Inverse.Basis * way;
        // The line's length in the mesh's frame, which a scale stretches.
        float scale = direction.Length();
        direction /= scale;
        // A line that misses the box round the mesh crosses none of its faces; one without end stops past the box.
        float within = length * scale ?? origin.DistanceTo(part.Box.GetCenter()) + part.Box.Size.Length();
        if (!part.Box.IntersectsSegment(origin, origin + direction * within)) return null;
        float? best = null;
        var corners = part.Corners;
        for (int i = 0; i + 2 < corners.Length; i += 3)
            if (NatureShape.Hit(origin, direction, corners[i], corners[i + 1], corners[i + 2]) is float t && t > 0 && (best == null || (nearest ? t < best : t > best))) best = t;
        return best / scale;
    }
}
