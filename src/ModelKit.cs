using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The photoscan a material takes its grain and bump from; its colour stays the model's own.
/// Foliage is drawn by the shrub shader.
/// </summary>
public enum Surface { Flat, Wood, Metal, Stone, Cloth, Earth, Foliage }

/// <summary>A photoscan drawn as it is, in its own colours, with bumps and parallax.</summary>
public enum Scan { Rock, Coal, Soil }

/// <summary>
/// Low-poly primitive helpers shared by every code-built model.
/// Meshes and materials are cached by their parameters, so hundreds of trees share a handful of resources.
/// </summary>
public static class ModelKit {
    // Keyed by their parameters as values, so looking one up allocates nothing even when an effect asks every frame.
    private static readonly Dictionary<(string, float, float, float, Surface), StandardMaterial3D> materials = new();
    /// <summary>How deep a surface's bump reads; the ground uses the same depth.</summary>
    private const float BumpDepth = .5f;
    private static readonly Dictionary<(Color, bool), StandardMaterial3D> flats = new();
    private static readonly Dictionary<(ulong, int), Material> faded = new();
    private static readonly Dictionary<string, Mesh> meshes = new();
    /// <summary>Where a faded mesh keeps its own material; made once, since a plain string converts to a new StringName on every call.</summary>
    public static readonly StringName BaseMaterial = "base";

    public static StandardMaterial3D Wood(string hex, float roughness = .85f) => Mat(hex, roughness, surface: Surface.Wood);
    public static StandardMaterial3D Metal(string hex, float roughness, float metallic) => Mat(hex, roughness, metallic, surface: Surface.Metal);
    public static StandardMaterial3D Stone(string hex, float roughness = .95f, float metallic = 0) => Mat(hex, roughness, metallic, surface: Surface.Stone);
    public static StandardMaterial3D Cloth(string hex, float roughness = .85f) => Mat(hex, roughness, surface: Surface.Cloth);

    public static StandardMaterial3D Mat(string hex, float roughness = .85f, float metallic = 0, float glow = 0, Surface surface = Surface.Flat) {
        var key = (hex, roughness, metallic, glow, surface);
        if (materials.TryGetValue(key, out var cached)) return cached;
        var color = new Color(hex);
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness, Metallic = metallic };
        if (glow > 0) { material.EmissionEnabled = true; material.Emission = color; material.EmissionEnergyMultiplier = glow; }
        Dress(material, surface);
        materials[key] = material;
        return material;
    }

    /// <summary>A material for a mesh that carries its own colours, tinted and dressed with a surface's grain and bump.</summary>
    public static StandardMaterial3D Vertex(Surface surface, float roughness, float metallic = 0, string tint = "ffffff") {
        var material = new StandardMaterial3D { AlbedoColor = new Color(tint), VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness, Metallic = metallic };
        Dress(material, surface);
        return material;
    }

    /// <summary>World units one tile of the surface spans: wood grain runs the length of a plank, metal marks are finer, and a cloth weave is drawn coarse so it shows at the game's distance.</summary>
    public static float Tile(Surface surface) => surface switch { Surface.Wood or Surface.Cloth => 1.2f, Surface.Metal => .6f, _ => 1 };

    /// <summary>World units one tile of a scan spans, about its real size.</summary>
    public const float ScanTile = 2;
    /// <summary>How deep a scan's parallax reads, in the engine's units.</summary>
    private const float ScanDepth = 5;
    private static readonly Dictionary<(Scan, float), StandardMaterial3D> scans = new();

    /// <summary>
    /// A material that draws a scan as it is, times the mesh's own colours, with bumps and parallax.
    /// The mesh carries texture coordinates in world units and tangents.
    /// </summary>
    public static StandardMaterial3D Scanned(Scan scan, float roughness) {
        if (scans.TryGetValue((scan, roughness), out var cached)) return cached;
        Texture2D Map(string map) => GD.Load<Texture2D>($"res://assets/models/surfaces/{scan.ToString().ToLowerInvariant()}_{map}.png");
        return scans[(scan, roughness)] = new StandardMaterial3D {
            AlbedoTexture = Map("albedo"),
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = roughness,
            NormalEnabled = true,
            NormalTexture = Map("bump"),
            Uv1Scale = Vector3.One / ScanTile,
            HeightmapEnabled = true,
            HeightmapTexture = Map("height"),
            HeightmapDeepParallax = true,
            HeightmapScale = ScanDepth,
            HeightmapMinLayers = 16,
            HeightmapMaxLayers = 64,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    /// <summary>A surface's grain, a multiplier around one; the engine hands out one copy per file.</summary>
    public static Texture2D Grain(Surface surface) => GD.Load<Texture2D>($"res://assets/models/surfaces/{surface.ToString().ToLowerInvariant()}_grain.exr");
    /// <summary>A surface's bump, an OpenGL normal map.</summary>
    private static Texture2D Bump(Surface surface) => GD.Load<Texture2D>($"res://assets/models/surfaces/{surface.ToString().ToLowerInvariant()}_bump.png");

    /// <summary>
    /// Lays the surface's grain and bump over the material in its own space, so the marks stay put when the model moves.
    /// They go in the detail layer, which leaves the albedo texture free for a model's own colours.
    /// </summary>
    public static void Dress(StandardMaterial3D material, Surface surface) {
        if (surface == Surface.Flat) return;
        material.DetailEnabled = true; material.DetailBlendMode = BaseMaterial3D.BlendModeEnum.Mul;
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        material.DetailAlbedo = Grain(surface); material.DetailNormal = Bump(surface);
        material.DetailUVLayer = BaseMaterial3D.DetailUV.UV2; material.UV2Triplanar = true; material.UV2Scale = Vector3.One / Tile(surface);
        // The depth applies to the bump once it is mixed into the normal map, which has no texture of its own here.
        material.NormalEnabled = true; material.NormalScale = BumpDepth;
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
        if (node is MeshInstance3D mesh && mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly && Fadeable(mesh.MaterialOverride)) {
            if (!mesh.HasMeta(BaseMaterial)) mesh.SetMeta(BaseMaterial, mesh.MaterialOverride);
            SetFade(mesh, (Material)mesh.GetMeta(BaseMaterial), transparency);
        }
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++) SetFade(node.GetChild(i), transparency);
    }

    /// <summary>Fades one mesh whose own material is <paramref name="original"/>.</summary>
    public static void SetFade(MeshInstance3D mesh, Material original, float transparency) {
        int step = (int)MathF.Round(Math.Clamp(transparency, 0, 1) * 10);
        mesh.MaterialOverride = step == 0 ? original : Faded(original, step);
    }

    /// <summary>A model's meshes with their own materials, gathered once so that fading it later walks no nodes: walking them each frame allocates.</summary>
    public static (MeshInstance3D Mesh, Material Material)[] FadeParts(Node node) {
        var parts = new List<(MeshInstance3D, Material)>();
        void Gather(Node at) {
            if (at is MeshInstance3D mesh && mesh.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly && Fadeable(mesh.MaterialOverride))
                parts.Add((mesh, mesh.HasMeta(BaseMaterial) ? (Material)mesh.GetMeta(BaseMaterial) : mesh.MaterialOverride));
            foreach (var child in at.GetChildren()) Gather(child);
        }
        Gather(node);
        foreach (var (_, material) in parts) WarmFades(material);
        return parts.ToArray();
    }

    /// <summary>Makes every faded copy of a material now, while loading, rather than the first time it fades in play.</summary>
    public static void WarmFades(Material material) {
        for (int step = 1; step <= 10; step++) Faded(material, step);
    }

    public static void SetFade((MeshInstance3D Mesh, Material Material)[] parts, float transparency) {
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

    /// <summary>Standard materials and Tree3D foliage, the only shader material with a blended twin.</summary>
    private static bool Fadeable(Material? material) => material is StandardMaterial3D || material is ShaderMaterial { Shader: var shader } && shader == Tree3DShape.Foliage;

    private static Material Faded(Material original, int step) {
        var key = (original.GetInstanceId(), step);
        if (faded.TryGetValue(key, out var cached)) return cached;
        if (original is ShaderMaterial foliage) {
            var leaves = (ShaderMaterial)foliage.Duplicate();
            leaves.Shader = Tree3DShape.FadingFoliage;
            leaves.SetShaderParameter(Tree3DShape.Opacity, 1 - step / 10f);
            return faded[key] = leaves;
        }
        var copy = (StandardMaterial3D)original.Duplicate();
        copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        copy.AlbedoColor = copy.AlbedoColor with { A = copy.AlbedoColor.A * (1 - step / 10f) };
        faded[key] = copy;
        // Asking for the material's RID builds its shader now rather than on its first draw, which would stall that frame.
        _ = copy.GetRid();
        return copy;
    }

    /// <summary>Builds the shaders of every material in a model now, while loading, rather than on its first draw.</summary>
    public static void BuildShaders(Node node) {
        if (node is MeshInstance3D mesh) {
            _ = mesh.MaterialOverride?.GetRid();
            if (mesh.Mesh is Mesh shape) for (int surface = 0; surface < shape.GetSurfaceCount(); surface++) _ = shape.SurfaceGetMaterial(surface)?.GetRid();
        }
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++) BuildShaders(node.GetChild(i));
    }

    public static void SetOverride(Node node, Material? material) {
        if (node is GeometryInstance3D geometry) geometry.MaterialOverlay = material;
        if (node is MeshInstance3D mesh && material != null) mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++) SetOverride(node.GetChild(i), material);
    }
}
