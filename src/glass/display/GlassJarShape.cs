using Godot;

namespace ChopChopMeow;

/// <summary>The open glass jar, standing on its base for heaps and held poses.</summary>
internal static class GlassJarShape {
    private const string Folder = "res://assets/models/glass_jar/";
    private static ArrayMesh? mesh, centered, pile;

    public static ArrayMesh Mesh => mesh ??= LoadMesh(Folder + "glass_jar.glb");
    public static ArrayMesh Centered => centered ??= CenterMesh();
    public static ArrayMesh Pile => pile ??= LoadMesh(Folder + "glass_jar_pile.glb");

    public static readonly StandardMaterial3D Finish = new() {
        AlbedoColor = new Color(.89f, .96f, 1f, .38f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = .19f,
        MetallicSpecular = .8f,
        RimEnabled = true,
        Rim = .5f,
        RimTint = .1f,
    };

    private static ArrayMesh LoadMesh(string scene) {
        var node = GD.Load<PackedScene>(scene).Instantiate();
        var instance = node as MeshInstance3D ?? node.GetChild<MeshInstance3D>(0);
        var result = (ArrayMesh)instance.Mesh;
        node.Free();
        return result;
    }

    private static ArrayMesh CenterMesh() {
        var source = Mesh;
        var arrays = source.SurfaceGetArrays(0);
        int vertex = (int)Godot.Mesh.ArrayType.Vertex;
        var points = arrays[vertex].AsVector3Array();
        var bounds = source.GetAabb();
        var middle = bounds.Position + bounds.Size / 2;
        for (int i = 0; i < points.Length; i++) points[i] -= middle;
        arrays[vertex] = points;
        var result = new ArrayMesh();
        result.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        return result;
    }
}
