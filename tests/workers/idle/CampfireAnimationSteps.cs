using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static ModelSteps;

internal static class CampfireAnimationSteps {
    internal static ReadOnlySpan<ActorAction> Seats => [ActorAction.Sit, ActorAction.Perch];

    internal static void Seat(ActorRig rig, ActorAction seat, float elapsed = 0) =>
        rig.Show(ActorAnimation.PoseAt(seat, elapsed, moving: false, eating: 0), null, false, onRock: seat == ActorAction.Perch);

    internal static Node3D Instrument(ActorRig rig, CampfireInstrument instrument) =>
        rig.Root.GetNode<Node3D>(instrument.ToString());

    /// <summary>The hand in the held model's own frame, at the fist or where the front of the palm makes contact.</summary>
    internal static Vector3 HandAt(ActorRig rig, Node3D prop, bool left, bool pad = false) =>
        prop.Transform.AffineInverse() * (pad ? rig.HandPad(left) : rig.Fist(left));

    /// <summary>The drawn arm in the guitar's frame, including the face centres between the skinned vertices.</summary>
    internal static IEnumerable<Vector3> ArmSurface(ActorRig rig, Node3D guitar, bool left) {
        string name = left ? "HumanArmLeft" : "HumanArmRight";
        var mesh = (MeshInstance3D)rig.Root.FindChild(name, recursive: true, owned: false)!;
        var skeleton = (Skeleton3D)mesh.GetParent();
        var arrays = mesh.Mesh.SurfaceGetArrays(0);
        var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        var place = guitar.Transform.AffineInverse() * InRig(rig, mesh);
        var upper = skeleton.GetBonePose(0);
        var lower = upper * skeleton.GetBonePose(1) * mesh.Skin.GetBindPose(1);
        for (int i = 0; i < points.Length; i++) {
            points[i] = place * ((upper * points[i]).Lerp(lower * points[i], weights[i * 4 + 1]));
            yield return points[i];
        }
        for (int i = 0; i < indices.Length; i += 3)
            yield return (points[indices[i]] + points[indices[i + 1]] + points[indices[i + 2]]) / 3;
    }

    /// <summary>The soundboard triangles give the exact outline of the guitar's body as drawn.</summary>
    internal static Vector3[] Soundboard(Node3D guitar) => AllMeshes(guitar).Single(part => part.Mesh.GetAabb().Size.Z > .07f).Mesh.GetFaces();

    /// <summary>Solid wood a surface point has entered, with two millimetres allowed for touching an edge.</summary>
    internal static bool InsideGuitar(Vector3 point, Vector3[] soundboard) {
        if (point.X is > .127f and < .438f && Math.Abs(point.Y) < .0185f && point.Z is > .011f and < .0525f) return true;
        if (point.X is > .108f and < .438f && Math.Abs(point.Y) < .0185f && point.Z is > .0455f and < .0525f) return true;
        if (point.X is > .4425f and < .5375f && Math.Abs(point.Y) < .0295f && point.Z is > .022f and < .046f) return true;
        if (point.X is > -.114f and < -.088f && Math.Abs(point.Y) < .053f && point.Z is > .042f and < .052f) return true;
        if (point.Z is <= -.037f or >= .039f || point.X is <= -.216f or >= .187f || Math.Abs(point.Y) >= .143f) return false;
        var p = new Vector2(point.X, point.Y);
        for (int i = 0; i < soundboard.Length; i += 3) {
            var a = soundboard[i]; var b = soundboard[i + 1]; var c = soundboard[i + 2];
            if (a.Z < .040f || b.Z < .040f || c.Z < .040f) continue;
            if (Geometry2D.PointIsInsideTriangle(p, new(a.X, a.Y), new(b.X, b.Y), new(c.X, c.Y))) return true;
        }
        return false;
    }
}
