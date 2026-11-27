using Godot;

internal static class LanternModelSteps {
    /// <summary>Triangles drawn from the rig after hiding its unused tools and attachment points.</summary>
    internal static int VisibleTriangles(Node at) {
        if (at is Node3D { Visible: false }) return 0;
        int count = at switch {
            MeshInstance3D { Mesh: Mesh mesh } => mesh.GetFaces().Length / 3,
            MultiMeshInstance3D { Multimesh: { Mesh: Mesh mesh } multi } => multi.VisibleInstanceCount * (mesh.GetFaces().Length / 3),
            _ => 0,
        };
        for (int i = 0; i < at.GetChildCount(); i++) count += VisibleTriangles(at.GetChild(i));
        return count;
    }
}
