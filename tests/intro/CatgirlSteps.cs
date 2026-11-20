using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static ModelSteps;

/// <summary>Steps for the checks of the intro film's catgirls: their bodies as the screen draws them.</summary>
public static class CatgirlSteps {
    /// <summary>
    /// A catgirl's body as drawn in the pose she shows: each point of its surface on the stage, and the corners of its triangles.
    /// The points are skinned as the screen draws them: each moves with its bones, by its weight on each.
    /// </summary>
    internal static (Vector3[] Points, int[] Triangles) DrawnBody(Catgirl dancer) {
        var body = AllMeshes(dancer.Rig.Root).Single(mesh => mesh.Name == "HumanBody");
        var skeleton = (Skeleton3D)body.GetParent();
        var arrays = body.Mesh.SurfaceGetArrays(0);
        var built = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
        var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
        // Each bind of the skin takes a point from where the mesh was built to where its bone is now.
        // A skeleton outside the scene does not work out where its bones are on the body, so each is followed up its parents.
        var binds = new Transform3D[body.Skin.GetBindCount()];
        for (int i = 0; i < binds.Length; i++) {
            var pose = Transform3D.Identity;
            for (int bone = body.Skin.GetBindBone(i); bone >= 0; bone = skeleton.GetBoneParent(bone)) pose = skeleton.GetBonePose(bone) * pose;
            binds[i] = pose * body.Skin.GetBindPose(i);
        }
        var toStage = dancer.Rig.Root.Transform * InRig(dancer.Rig, body);
        var points = new Vector3[built.Length];
        int perPoint = bones.Length / built.Length;
        for (int point = 0; point < built.Length; point++) {
            var skinned = Vector3.Zero;
            for (int k = 0; k < perPoint; k++) skinned += weights[point * perPoint + k] * (binds[bones[point * perPoint + k]] * built[point]);
            points[point] = toStage * skinned;
        }
        return (points, arrays[(int)Mesh.ArrayType.Index].AsInt32Array());
    }

    /// <summary>
    /// Each edge of a surface, by the two points it joins, with how many triangles share it and how often they run along it
    /// from the lower point to the higher one less how often they run back.
    /// </summary>
    public static Dictionary<(int Low, int High), (int Faces, int Winding)> Edges(int[] triangles) {
        var edges = new Dictionary<(int Low, int High), (int Faces, int Winding)>();
        for (int i = 0; i < triangles.Length; i += 3)
            for (int corner = 0; corner < 3; corner++) {
                int from = triangles[i + corner], to = triangles[i + (corner + 1) % 3];
                var key = (Math.Min(from, to), Math.Max(from, to));
                var edge = edges.GetValueOrDefault(key);
                edges[key] = (edge.Faces + 1, edge.Winding + (from < to ? 1 : -1));
            }
        return edges;
    }
}
