using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A glass jar in a wooden foot, with a steel lid and handle and ten firebugs visible inside.</summary>
internal static class LanternShape {
    private const float JarScale = 1.8f, JarBase = .032f, LidHeight = .367f;
    internal const float GripHeight = .478f;
    /// <summary>When placed, the foot and lid block the lantern's own point light. The glass and trim do not.</summary>
    internal const uint TrimLayer = 1 << 13, EndsLayer = 1 << 14;
    internal static readonly Vector3 LightCenter = Vector3.Up * .212f;
    internal static readonly Color LightColor = new("adff76");
    private static readonly StandardMaterial3D FrameWood = Wood("805333", .83f);
    private static readonly StandardMaterial3D Hardware = Metal("647075", .5f, .7f);
    private static readonly StandardMaterial3D BugGlow = Flat(new Color("eaff9e"));
    private static readonly Vector3[] BugSpots = [
        new(-.047f, .096f, .044f), new(.029f, .117f, -.054f), new(.051f, .143f, .031f),
        new(-.056f, .173f, -.035f), new(.013f, .191f, .065f), new(-.009f, .215f, -.064f),
        new(.063f, .234f, .005f), new(-.054f, .247f, .046f), new(.025f, .283f, .048f), new(-.026f, .306f, -.041f),
    ];
    private static readonly CylinderMesh CompactLid = new() {
        BottomRadius = .096f,
        TopRadius = .092f,
        Height = .026f,
        RadialSegments = 6,
        Rings = 0,
    };
    private static ArrayMesh? bugs, compactGlass, heap;

    internal static Node3D Build(out MultiMeshInstance3D firebugs, out MeshInstance3D glass, bool compact = false) {
        var root = new Node3D();
        var foot = Box(root, new(.227f, .048f, .227f), Vector3.Up * .024f, FrameWood);
        glass = compact ? Add(root, compactGlass ??= CompactGlass(), GlassJarShape.Finish, Vector3.Zero)
            : Add(root, GlassJarShape.Pile, GlassJarShape.Finish, Vector3.Up * JarBase, scale: Vector3.One * JarScale);
        foreach (float side in new[] { -1f, 1f }) {
            if (!compact) {
                Box(root, new(.025f, .25f, .035f), new(side * .096f, .15f, 0), FrameWood);
                Box(root, new(.029f, .029f, .043f), new(side * .095f, .27f, 0), Hardware);
                Brace(new(side * .069f, LidHeight, 0), new(side * .076f, .436f, 0));
                Brace(new(side * .076f, .436f, 0), new(side * .042f, GripHeight, 0));
            } else Brace(new(side * .083f, LidHeight, 0), new(side * .042f, GripHeight, 0));
        }
        var lid = compact ? Add(root, CompactLid, Hardware, Vector3.Up * LidHeight)
            : Cylinder(root, .096f, .092f, .026f, Vector3.Up * LidHeight, Hardware, sides: 10);
        Box(root, new(.093f, .026f, .027f), Vector3.Up * GripHeight, FrameWood);
        for (int i = 0; i < root.GetChildCount(); i++) {
            var part = root.GetChild<MeshInstance3D>(i);
            part.Layers = part == foot || part == lid ? EndsLayer : TrimLayer;
        }
        var dots = new MultiMesh {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = bugs ??= Bug(),
            InstanceCount = BugSpots.Length,
            VisibleInstanceCount = 0,
        };
        for (int i = 0; i < BugSpots.Length; i++) dots.SetInstanceTransform(i, new Transform3D(Basis.Identity, BugSpots[i]));
        firebugs = new MultiMeshInstance3D { Multimesh = dots, MaterialOverride = BugGlow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(firebugs);
        return root;

        void Brace(Vector3 from, Vector3 to) {
            var along = to - from;
            var brace = Box(root, new(.012f, along.Length() + .007f, .012f), (from + to) / 2, Hardware);
            brace.Rotation = new(0, 0, Mathf.Atan2(-along.X, along.Y));
        }
    }

    /// <summary>The jar's profile without its hidden lip, floor or inside wall, for worn lanterns and heaps.</summary>
    private static ArrayMesh CompactGlass() {
        const int sides = 8;
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < sides; i++) {
            Vector3 near = Radial(i), far = Radial(i + 1);
            Strip(near, far, JarBase, .093f, .066f, .102f);
            Strip(near, far, .066f, .102f, .267f, .102f);
            Strip(near, far, .267f, .102f, LidHeight, .091f);
        }
        return surface.Commit();

        static Vector3 Radial(int side) => Vector3.Right.Rotated(Vector3.Up, Mathf.Tau * side / sides);
        void Strip(Vector3 a, Vector3 b, float bottom, float wide, float top, float narrow) {
            Vector3 an = a * wide + Vector3.Up * bottom, bn = b * wide + Vector3.Up * bottom;
            Vector3 af = a * narrow + Vector3.Up * top, bf = b * narrow + Vector3.Up * top;
            surface.SetSmoothGroup(0);
            surface.SetNormal(a); surface.AddVertex(an);
            surface.SetNormal(b); surface.AddVertex(bf);
            surface.SetNormal(b); surface.AddVertex(bn);
            surface.SetNormal(a); surface.AddVertex(an);
            surface.SetNormal(a); surface.AddVertex(af);
            surface.SetNormal(b); surface.AddVertex(bf);
        }
    }

    private static ArrayMesh Bug() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        // Two crossed cards per firebug keep the dot visible from either side of the jar.
        Card(Vector3.Right * .007f);
        Card(Vector3.Back * .007f);
        return surface.Commit();

        void Card(Vector3 across) {
            var up = Vector3.Up * .009f;
            surface.AddVertex(-across); surface.AddVertex(up); surface.AddVertex(across);
            surface.AddVertex(-across); surface.AddVertex(across); surface.AddVertex(-up);
        }
    }

    /// <summary>Lanterns in storage never draw their bugs or light, and share a single mesh carrying three materials.</summary>
    internal static ArrayMesh Heap => heap ??= BuildHeap();

    private static ArrayMesh BuildHeap() {
        var model = Build(out _, out _, compact: true);
        try {
            var mesh = new ArrayMesh();
            Merge(FrameWood);
            Merge(Hardware);
            Merge(GlassJarShape.Finish);
            return mesh;

            void Merge(Material material) {
                var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
                for (int i = 0; i < model.GetChildCount(); i++) {
                    if (model.GetChild(i) is MeshInstance3D part && part.MaterialOverride == material)
                        tool.AppendFrom(part.Mesh, 0, part.Transform);
                }
                tool.SetMaterial(material);
                tool.Commit(mesh);
            }
        } finally { model.Free(); }
    }
}
