using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The cave scenery must join the actual mountain mesh without growing into a tall detached fin.</summary>
public static class MineRockChecks {
    public static void Run() {
        foreach (var cliff in World.Cliffs) {
            var mesh = MineRockShape.Mesh(cliff, new[] { "7d5b45", "8a6650", "6e4f3c" });
            var vertices = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            float rear = float.MaxValue;
            foreach (var vertex in vertices) rear = Math.Min(rear, vertex.Z);
            var across = new Vector2(cliff.Face.Y, -cliff.Face.X);
            int embedded = 0;
            foreach (var vertex in vertices) {
                Require(vertex.IsFinite() && vertex.Y <= 4.31f, "the cave rock must stay a low outcrop at the mountain foot");
                if (Math.Abs(vertex.Z - rear) > .001f) continue;
                var position = World.MineSpot(cliff) + across * vertex.X + cliff.Face * vertex.Z;
                Require(vertex.Y < MountainShape.Surface(position) - .7f, "the whole rear edge must be buried below the actual mountain triangles");
                embedded++;
            }
            Require(embedded > 0, "the cave must have an embedded rear edge");
        }
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
