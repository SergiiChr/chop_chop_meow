using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static WorldSteps;

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

    public static void Visit(Node node, List<MeshInstance3D> result) {
        if (node is MeshInstance3D mesh && mesh.Name.ToString().StartsWith("Human", StringComparison.Ordinal)) result.Add(mesh);
        for (int i = 0; i < node.GetChildCount(); i++) Visit(node.GetChild(i), result);
    }

    public static MeshInstance3D[] Parts(Prop prop) => Enumerable.Range(0, 3)
        .Select(index => prop.Body.GetChild(index) as MeshInstance3D
            ?? throw new InvalidOperationException("Nature model check failed: ore mesh instance was not found"))
        .ToArray();

    public static string PartName(int index) => index switch { 0 => "host", 1 => "seam", _ => "chip" };
}
