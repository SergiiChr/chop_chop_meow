using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Low-poly primitive helpers shared by every code-built model.
/// Meshes and materials are cached by their parameters, so hundreds of trees share a handful of resources.
/// </summary>
public static class ModelKit {
    // Keyed by their parameters as values, so looking one up allocates nothing even when an effect asks every frame.
    private static readonly Dictionary<(string, float, float, float), StandardMaterial3D> materials = new();
    private static readonly Dictionary<(Color, bool), StandardMaterial3D> flats = new();
    private static readonly Dictionary<(ulong, int), Material> faded = new();
    private static readonly Dictionary<string, Mesh> meshes = new();
    /// <summary>Where a faded mesh keeps its own material; made once, since a plain string converts to a new StringName on every call.</summary>
    public static readonly StringName BaseMaterial = "base";

    public static StandardMaterial3D Mat(string hex, float roughness = .85f, float metallic = 0, float glow = 0) {
        var key = (hex, roughness, metallic, glow);
        if (materials.TryGetValue(key, out var cached)) return cached;
        var color = new Color(hex);
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness, Metallic = metallic };
        if (glow > 0) { material.EmissionEnabled = true; material.Emission = color; material.EmissionEnergyMultiplier = glow; }
        materials[key] = material;
        return material;
    }

    /// <summary>Unshaded, alpha-blended material for flat markers, flames and smoke.</summary>
    public static StandardMaterial3D Flat(Color color, bool lit = false) {
        if (flats.TryGetValue((color, lit), out var cached)) return cached;
        var material = new StandardMaterial3D {
            AlbedoColor = color,
            Transparency = color.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            ShadingMode = lit ? BaseMaterial3D.ShadingModeEnum.PerPixel : BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        flats[(color, lit)] = material;
        return material;
    }

    private static Mesh Cached(string key, System.Func<Mesh> create) {
        if (!meshes.TryGetValue(key, out var mesh)) meshes[key] = mesh = create();
        return mesh;
    }

    public static Node3D Pivot(Node3D parent, Vector3 position, Vector3? rotationDegrees = null) {
        var node = new Node3D { Position = position, RotationDegrees = rotationDegrees ?? Vector3.Zero };
        parent.AddChild(node);
        return node;
    }

    public static MeshInstance3D Add(Node3D parent, Mesh mesh, Material material, Vector3 position, Vector3? rotationDegrees = null, Vector3? scale = null) {
        var instance = new MeshInstance3D {
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            Scale = scale ?? Vector3.One,
        };
        parent.AddChild(instance);
        return instance;
    }

    public static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 position, Material material, Vector3? rotationDegrees = null) =>
        Add(parent, Cached($"box{size}", () => new BoxMesh { Size = size }), material, position, rotationDegrees);

    /// <summary>A cylinder or cone centered on <paramref name="center"/>.</summary>
    public static MeshInstance3D Cylinder(Node3D parent, float bottom, float top, float height, Vector3 center, Material material, Vector3? rotationDegrees = null, int sides = 10) {
        var mesh = Cached($"cyl{bottom}|{top}|{height}|{sides}", () => new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = sides, Rings = 1 });
        return Add(parent, mesh, material, center, rotationDegrees);
    }

    /// <summary>A cylinder or cone standing on <paramref name="baseAt"/>.</summary>
    public static MeshInstance3D Post(Node3D parent, float bottom, float top, float height, Vector3 baseAt, Material material, int sides = 10) =>
        Cylinder(parent, bottom, top, height, baseAt + new Vector3(0, height / 2, 0), material, null, sides);

    public static MeshInstance3D Sphere(Node3D parent, float radius, Vector3 position, Material material, Vector3? scale = null, int segments = 10) {
        var mesh = Cached($"sph{radius}|{segments}", () => new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = segments, Rings = segments / 2 });
        return Add(parent, mesh, material, position, null, scale);
    }

    /// <summary>A capsule hanging downward from <paramref name="position"/>, so limbs rotate about their joint.</summary>
    public static MeshInstance3D Limb(Node3D parent, float radius, float length, Vector3 position, Material material) {
        var mesh = Cached($"cap{radius}|{length}", () => new CapsuleMesh { Radius = radius, Height = length, RadialSegments = 8, Rings = 2 });
        return Add(parent, mesh, material, position - new Vector3(0, length / 2 - radius, 0));
    }

    public static MeshInstance3D Torus(Node3D parent, float inner, float outer, Vector3 position, Material material) {
        var mesh = Cached($"torus{inner}|{outer}", () => new TorusMesh { InnerRadius = inner, OuterRadius = outer, Rings = 24, RingSegments = 6 });
        return Add(parent, mesh, material, position);
    }

    /// <summary>
    /// Fades a whole model by swapping in alpha-blended copies of its materials.
    /// Copies are cached per tenth of transparency, so fading trees still share materials.
    /// </summary>
    public static void SetFade(Node node, float transparency) {
        if (node is MeshInstance3D mesh && mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly && mesh.MaterialOverride is StandardMaterial3D current) {
            if (!mesh.HasMeta(BaseMaterial)) mesh.SetMeta(BaseMaterial, current);
            SetFade(mesh, (StandardMaterial3D)mesh.GetMeta(BaseMaterial), transparency);
        }
        foreach (var child in node.GetChildren()) SetFade(child, transparency);
    }

    /// <summary>Fades one mesh whose own material is <paramref name="original"/>.</summary>
    public static void SetFade(MeshInstance3D mesh, StandardMaterial3D original, float transparency) {
        int step = (int)MathF.Round(Math.Clamp(transparency, 0, 1) * 10);
        mesh.MaterialOverride = step == 0 ? original : Faded(original, step);
    }

    /// <summary>A model's meshes with their own materials, gathered once so that fading it later walks no nodes: walking them each frame allocates.</summary>
    public static (MeshInstance3D Mesh, StandardMaterial3D Material)[] FadeParts(Node node) {
        var parts = new List<(MeshInstance3D, StandardMaterial3D)>();
        void Gather(Node at) {
            if (at is MeshInstance3D mesh && mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly && mesh.MaterialOverride is StandardMaterial3D current)
                parts.Add((mesh, mesh.HasMeta(BaseMaterial) ? (StandardMaterial3D)mesh.GetMeta(BaseMaterial) : current));
            foreach (var child in at.GetChildren()) Gather(child);
        }
        Gather(node);
        foreach (var (_, material) in parts) WarmFades(material);
        return parts.ToArray();
    }

    /// <summary>Makes every faded copy of a material now, while loading, rather than the first time it fades in play.</summary>
    public static void WarmFades(StandardMaterial3D material) {
        for (int step = 1; step <= 10; step++) Faded(material, step);
    }

    public static void SetFade((MeshInstance3D Mesh, StandardMaterial3D Material)[] parts, float transparency) {
        foreach (var (mesh, material) in parts) SetFade(mesh, material, transparency);
    }

    /// <summary>Keep original cutout shadows when camera occlusion fades the visible material to alpha blending.</summary>
    public static void PreserveShadows(Node3D node) {
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++) {
            if (node.GetChild(i) is not Node3D child) continue;
            PreserveShadows(child);
            if (child is not MeshInstance3D mesh || mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.On) continue;
            // Shares geometry/material; one shadow draw replaces the visible mesh's original shadow draw.
            node.AddChild(new MeshInstance3D {
                Mesh = mesh.Mesh,
                MaterialOverride = mesh.MaterialOverride,
                Transform = mesh.Transform,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly
            });
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    private static Material Faded(StandardMaterial3D original, int step) {
        var key = (original.GetInstanceId(), step);
        if (faded.TryGetValue(key, out var cached)) return cached;
        var copy = (StandardMaterial3D)original.Duplicate();
        copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        copy.AlbedoColor = original.AlbedoColor with { A = original.AlbedoColor.A * (1 - step / 10f) };
        faded[key] = copy;
        return copy;
    }

    public static void SetOverride(Node node, Material? material) {
        if (node is GeometryInstance3D geometry) geometry.MaterialOverlay = material;
        if (node is MeshInstance3D mesh && material != null) mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (var child in node.GetChildren()) SetOverride(child, material);
    }
}
