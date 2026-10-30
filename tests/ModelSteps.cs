using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Finds the parts of a built model for the model checks.</summary>
internal static class ModelSteps {
    public static MeshInstance3D? FindCargo(Node node) {
        if (node is MeshInstance3D mesh && mesh.Mesh?.GetInstanceId() == CrateShape.Cargo.GetInstanceId()) return mesh;
        for (int i = 0; i < node.GetChildCount(); i++)
            if (FindCargo(node.GetChild(i)) is { } found) return found;
        return null;
    }

    public static List<MeshInstance3D> Core(ActorRig rig) {
        var result = new List<MeshInstance3D>();
        Visit(rig.Root, result);
        return result;
    }

    private static void Visit(Node node, List<MeshInstance3D> result) {
        if (node is MeshInstance3D mesh && mesh.Name.ToString().StartsWith("Human", StringComparison.Ordinal)) result.Add(mesh);
        for (int i = 0; i < node.GetChildCount(); i++) Visit(node.GetChild(i), result);
    }

    public static (Vector3 Min, Vector3 Max) BoundsInTorso(MeshInstance3D mesh) {
        var bounds = mesh.Mesh!.GetAabb();
        var toTorso = ((Node3D)mesh.GetParent()).Transform * mesh.Transform;
        var first = toTorso * bounds.Position;
        var min = first; var max = first;
        for (int corner = 1; corner < 8; corner++) {
            var point = toTorso * (bounds.Position + new Vector3(
                (corner & 1) != 0 ? bounds.Size.X : 0,
                (corner & 2) != 0 ? bounds.Size.Y : 0,
                (corner & 4) != 0 ? bounds.Size.Z : 0));
            min = new Vector3(Math.Min(min.X, point.X), Math.Min(min.Y, point.Y), Math.Min(min.Z, point.Z));
            max = new Vector3(Math.Max(max.X, point.X), Math.Max(max.Y, point.Y), Math.Max(max.Z, point.Z));
        }
        return (min, max);
    }

    public static MeshInstance3D[] Parts(Prop prop) {
        var parts = Enumerable.Range(0, 3).Select(index => prop.Body.GetChild(index) as MeshInstance3D).ToArray();
        Require(parts.All(part => part != null), "the ore mesh instances should be found");
        return parts!;
    }

    public static string PartName(int index) => index switch { 0 => "host", 1 => "seam", _ => "chip" };
}
