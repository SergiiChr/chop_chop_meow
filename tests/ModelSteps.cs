using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Finds the parts of a built model for the model checks.</summary>
internal static class ModelSteps {
    /// <summary>Every mesh under a node.</summary>
    public static List<MeshInstance3D> AllMeshes(Node at) {
        var meshes = new List<MeshInstance3D>();
        if (at is MeshInstance3D mesh) meshes.Add(mesh);
        foreach (var child in at.GetChildren()) meshes.AddRange(AllMeshes(child));
        return meshes;
    }

    /// <summary>The triangles of every mesh under a node, shown or hidden, and of every piece a heap under it shows.</summary>
    public static int Triangles(Node at) {
        int count = at switch {
            MeshInstance3D { Mesh: Mesh mesh } => mesh.GetFaces().Length / 3,
            MultiMeshInstance3D { Visible: true, Multimesh: { Mesh: Mesh piece } heap } => heap.VisibleInstanceCount * (piece.GetFaces().Length / 3),
            _ => 0,
        };
        foreach (var child in at.GetChildren()) count += Triangles(child);
        return count;
    }

    /// <summary>The piece a pile heaps up once it holds nothing but the given number of the item, a stack unless told.</summary>
    public static Mesh Piled(PileProp pile, Thing thing, string item, GameData data, int count = 64) {
        thing.Inventory.Clear(); thing.Inventory.Add(item, count);
        WorldModels.SyncPile(pile, thing, data);
        return pile.Stock.Piece!;
    }

    /// <summary>The solid core that fills a canopy's shadow, separate from the per-mesh proxies.</summary>
    public static bool CanopyCore(MeshInstance3D mesh) => mesh.MaterialOverride is ShaderMaterial { Shader: var shader } && shader == Tree3DShape.SolidFoliage;

    /// <summary>The shadow-only meshes of a canopy other than its solid core.</summary>
    public static MeshInstance3D[] CanopyShadows(TreeProp prop) => AllMeshes(prop.Sway).Where(mesh => mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.ShadowsOnly && !CanopyCore(mesh)).ToArray();

    /// <summary>The shadow proxy sharing a visible canopy mesh's parent, mesh, material and transform.</summary>
    public static MeshInstance3D? ShadowProxy(MeshInstance3D[] shadows, MeshInstance3D mesh, Material material) => shadows.FirstOrDefault(candidate =>
        ReferenceEquals(candidate.GetParent(), mesh.GetParent()) && ReferenceEquals(candidate.Mesh, mesh.Mesh) && ReferenceEquals(candidate.MaterialOverride, material) && candidate.Transform.Equals(mesh.Transform));

    /// <summary>How opaque foliage is: code-built canopies fade a standard material's albedo alpha, and Tree3D foliage swaps to a blended shader with an opacity.</summary>
    public static float FoliageOpacity(Material material) => material is ShaderMaterial shader
        ? shader.Shader == Tree3DShape.FadingFoliage ? shader.GetShaderParameter(Tree3DShape.Opacity).AsSingle() : 1
        : ((StandardMaterial3D)material).AlbedoColor.A;

    /// <summary>Whether foliage is drawn blended rather than cut out.</summary>
    public static bool FoliageBlended(Material material) => material is ShaderMaterial shader
        ? shader.Shader == Tree3DShape.FadingFoliage
        : ((StandardMaterial3D)material).Transparency == BaseMaterial3D.TransparencyEnum.Alpha;

    public static MeshInstance3D? FindCargo(Node node) {
        if (node is MeshInstance3D mesh && mesh.Mesh?.GetInstanceId() == CrateShape.Cargo.GetInstanceId()) return mesh;
        for (int i = 0; i < node.GetChildCount(); i++)
            if (FindCargo(node.GetChild(i)) is { } found) return found;
        return null;
    }

    /// <summary>Where a part of a rig is from the rig's root.</summary>
    public static Transform3D InRig(ActorRig rig, Node3D part) {
        var place = part.Transform;
        for (var parent = (Node3D)part.GetParent(); parent != rig.Root; parent = (Node3D)parent.GetParent()) place = parent.Transform * place;
        return place;
    }

    /// <summary>The mortar's bowl in a rig's left hand, and where the pestle's foot in its right hand is from the bowl's own foot, across the bowl and up it.</summary>
    public static (Transform3D Bowl, Vector3 PestleFoot) HeldMortar(ActorRig rig) {
        var meshes = AllMeshes(rig.Root);
        var bowl = InRig(rig, meshes.Single(mesh => mesh.Mesh == CookShape.Bowl));
        return (bowl, bowl.AffineInverse() * InRig(rig, meshes.Single(mesh => mesh.Mesh == CookShape.Pestle)).Origin);
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
