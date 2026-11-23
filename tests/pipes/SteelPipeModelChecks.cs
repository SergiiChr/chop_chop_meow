using System.Linq;
using Godot;
using static Checks;

namespace ChopChopMeow;

public static class SteelPipeModelChecks {
    public static void Run() {
        Test(CheckStraightAndTeeMeetWithAnOpenBore);
        Test(CheckClosedTeeSideHasNoPort);
    }

    private static void CheckStraightAndTeeMeetWithAnOpenBore() {
        const int east = 1, south = 4, west = 16;
        var parent = new Node3D();
        try {
            var tee = SteelPipeShape.Build(parent, east | south | west, east);
            var straight = SteelPipeShape.Build(parent, east | west, west);
            var teeFaces = tee.Mesh.GetFaces();
            var straightFaces = straight.Mesh.GetFaces();
            var fromTee = teeFaces.Where(p => Mathf.IsEqualApprox(p.X, .5f)).Distinct().ToArray();
            var fromStraight = straightFaces.Where(p => Mathf.IsEqualApprox(p.X, -.5f))
                .Select(p => p + Vector3.Right).Distinct().ToArray();
            Require(fromTee.Length == 16 && fromStraight.Length == 16,
                "Both joined ends must have eight points on the bore and eight on the outer wall.");
            Require(fromTee.All(p => fromStraight.Any(q => q.DistanceSquaredTo(p) < 1e-10f)),
                "The straight and tee must meet at identical points, inside and out.");
            Require(!teeFaces.Chunk(3).Any(face => face.All(p => Mathf.IsEqualApprox(p.X, .5f)))
                && !straightFaces.Chunk(3).Any(face => face.All(p => Mathf.IsEqualApprox(p.X, -.5f))),
                "No face may close a joined end.");
        } finally { parent.Free(); }
    }

    private static void CheckClosedTeeSideHasNoPort() {
        const int east = 1, south = 4, west = 16, north = 64;
        var parent = new Node3D();
        try {
            var tee = SteelPipeShape.Build(parent, east | south | west);
            var cross = SteelPipeShape.Build(parent, east | south | west | north);
            var teeBounds = tee.Mesh.GetAabb();
            var crossBounds = cross.Mesh.GetAabb();
            Require(teeBounds.Position.Z > -.05f && teeBounds.End.Z > .44f && crossBounds.Position.Z < -.44f,
                "The cross must show all four arms and the tee must leave its closed north side bare.");
            Require(teeBounds.Size.Y < .1f && crossBounds.Size.Y < .1f
                && SteelPipeShape.Loose.GetAabb().Size.Y < .1f && SteelPipeShape.Heap.GetAabb().Size.Y < .1f,
                "The pipe walls must stay slim through junctions, ends, and stockpiles.");
        } finally { parent.Free(); }
    }
}
