using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>The campfire instruments and the points the seated musician reaches on them.</summary>
internal static class CampfireInstrumentShape {
    public static ReadOnlySpan<CampfireInstrument> All => [CampfireInstrument.Harmonica, CampfireInstrument.Guitar, CampfireInstrument.Drum];
    internal const float GuitarStrings = .058f, GuitarSoundHole = .062f;
    internal static readonly Vector3 GuitarNeckGrip = new(.33f, -.069f, .039f);
    internal const float HarmonicaMouthpiece = -.024f, HarmonicaGrip = .066f;
    internal const float DrumHead = .066f, DrumRadius = .152f;

    private static readonly Dictionary<CampfireInstrument, (Mesh Mesh, Material Material)[]> Models = new();
    private static readonly StandardMaterial3D GuitarWood = Vertex(Surface.Wood, .62f);
    private static readonly Vector3 FaceOut = new(90, 0, 0);
    private static Mesh? guitarFingers;

    public static Node3D Create(CampfireInstrument instrument, string? skin = null) {
        var root = new Node3D { Name = instrument.ToString() };
        if (!Models.TryGetValue(instrument, out var meshes)) Models[instrument] = meshes = Bake(instrument);
        foreach (var (mesh, material) in meshes) Add(root, mesh, material, Vector3.Zero);
        if (instrument == CampfireInstrument.Guitar && skin != null) {
            var material = Mat(skin, .85f);
            Add(root, guitarFingers ??= FrettingFingers(material), material, Vector3.Zero).Name = "FrettingFingers";
        }
        return root;
    }

    /// <summary>Fingertips curl from the worker's palm below the neck over the outside edge of the fingerboard.</summary>
    private static Mesh FrettingFingers(Material skin) {
        var parts = new Node3D();
        using var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        for (int finger = 0; finger < 4; finger++) {
            float x = GuitarNeckGrip.X + (finger - 1.5f) * .0175f;
            float reach = finger is 1 or 2 ? -.006f : -.012f;
            var baseAt = new Vector3(x, -.061f, .043f);
            var knuckle = new Vector3(x, -.033f, .064f);
            var tip = new Vector3(x - .002f, reach, .064f);
            Link(baseAt, knuckle, .008f, .007f);
            Link(knuckle, tip, .007f, .0058f);
        }
        parts.Free();
        return mesh.Commit();

        void Link(Vector3 from, Vector3 to, float start, float end) {
            var span = to - from;
            var link = Cylinder(parts, start, end, span.Length(), (from + to) * .5f, skin, sides: 6);
            link.Basis = new Basis(new Quaternion(Vector3.Up, span.Normalized()));
            mesh.AppendFrom(link.Mesh, 0, link.Transform);
        }
    }

    /// <summary>Joins the small parts that share a material so each worker keeps only a handful of mesh nodes per prop.</summary>
    private static (Mesh, Material)[] Bake(CampfireInstrument instrument) {
        var parts = new Node3D();
        switch (instrument) {
            case CampfireInstrument.Harmonica: Harmonica(parts); break;
            case CampfireInstrument.Guitar: Guitar(parts); break;
            case CampfireInstrument.Drum: Drum(parts); break;
        }
        var byMaterial = new Dictionary<Material, SurfaceTool>();
        Join(parts, Transform3D.Identity);
        parts.Free();
        var meshes = new (Mesh, Material)[byMaterial.Count];
        int index = 0;
        foreach (var (material, tool) in byMaterial) {
            meshes[index++] = (tool.Commit(), material);
            tool.Dispose();
        }
        return meshes;

        void Join(Node3D node, Transform3D fromParent) {
            var at = fromParent * node.Transform;
            if (node is MeshInstance3D { MaterialOverride: Material material, Mesh: Mesh mesh }) {
                if (!byMaterial.TryGetValue(material, out var tool)) {
                    byMaterial[material] = tool = new SurfaceTool();
                    tool.Begin(Mesh.PrimitiveType.Triangles);
                }
                tool.AppendFrom(mesh, 0, at);
            }
            for (int i = 0; i < node.GetChildCount(); i++) Join(node.GetChild<Node3D>(i), at);
        }
    }

    private static void Harmonica(Node3D root) {
        var steel = Metal("b7c5ce", .33f, .55f);
        Box(root, new(.145f, .017f, .048f), Vector3.Zero, Mat("3c2c28"));
        foreach (float side in new[] { -1f, 1f }) {
            Box(root, new(.154f, .007f, .038f), new(0, side * .011f, .004f), steel);
            Box(root, new(.137f, .006f, .027f), new(0, side * .016f, .006f), steel);
        }
        var brass = Metal("b28b46", .48f, .4f);
        for (int i = 0; i < 9; i++) Box(root, new(.003f, .012f, .002f), new((i - 4) * .013f, 0, HarmonicaMouthpiece), brass);
    }

    private static void Guitar(Node3D root) {
        Add(root, GuitarBody(), GuitarWood, Vector3.Zero);
        var neck = Wood("6a3b26", .72f);
        var fingerboard = Wood("412a22");
        var metal = Metal("d0c3ac", .47f, .55f);
        Cylinder(root, .052f, .052f, .003f, new(GuitarSoundHole, 0, .043f), Wood("925425"), FaceOut, 14);
        Cylinder(root, .042f, .042f, .003f, new(GuitarSoundHole, 0, .045f), Mat("29221c"), FaceOut, 14);
        Box(root, new(.31f, .041f, .036f), new(.285f, 0, .027f), neck);
        Box(root, new(.334f, .041f, .011f), new(.273f, 0, .049f), fingerboard);
        Box(root, new(.099f, .063f, .028f), new(.49f, 0, .034f), neck);
        Box(root, new(.030f, .11f, .014f), new(-.101f, 0, .047f), fingerboard);
        for (int i = 0; i < 9; i++)
            Box(root, new(.002f, .043f, .002f), new(.435f - .031f * i + .001f * i * i, 0, .055f), metal);
        for (int i = 0; i < 6; i++)
            Box(root, new(.586f, .0012f, .0012f), new(.19f, (i - 2.5f) * .005f, GuitarStrings), metal);
        for (int i = 0; i < 3; i++)
            foreach (float side in new[] { -1f, 1f })
                Box(root, new(.014f, .012f, .014f), new(.465f + .028f * i, side * .035f, .04f), metal);
    }

    private static ArrayMesh GuitarBody() {
        // A wider bout at the bridge and a smaller one at the neck, meeting at the narrow waist.
        Vector2[] outline = [
            new(.189f, 0), new(.18f, .064f), new(.153f, .105f), new(.106f, .116f), new(.069f, .104f),
            new(.026f, .088f), new(-.016f, .102f), new(-.058f, .134f), new(-.111f, .145f),
            new(-.164f, .128f), new(-.202f, .088f), new(-.218f, .034f), new(-.218f, -.034f),
            new(-.202f, -.088f), new(-.164f, -.128f), new(-.111f, -.145f), new(-.058f, -.134f),
            new(-.016f, -.102f), new(.026f, -.088f), new(.069f, -.104f), new(.106f, -.116f),
            new(.153f, -.105f), new(.18f, -.064f)
        ];
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        var top = new Color("dda362");
        var side = new Color("90502b");
        var binding = new Color("f0c88e");
        var faces = Geometry2D.TriangulatePolygon(outline);
        for (int i = 0; i < faces.Length; i += 3) {
            Triangle(Point(faces[i], .041f), Point(faces[i + 1], .041f), Point(faces[i + 2], .041f), Vector3.Back, top);
            Triangle(Point(faces[i], -.039f), Point(faces[i + 1], -.039f), Point(faces[i + 2], -.039f), Vector3.Forward, side);
        }
        for (int i = 0; i < outline.Length; i++) {
            int next = (i + 1) % outline.Length;
            var edge = outline[next] - outline[i];
            var normal = new Vector3(edge.Y, -edge.X, 0).Normalized();
            Join(i, next, -.039f, .035f, normal, side);
            Join(i, next, .035f, .041f, normal, binding);
        }
        return mesh.Commit();

        Vector3 Point(int i, float z) => new(outline[i].X, outline[i].Y, z);
        void Join(int i, int next, float lo, float hi, Vector3 normal, Color color) {
            Triangle(Point(i, lo), Point(next, lo), Point(next, hi), normal, color);
            Triangle(Point(i, lo), Point(next, hi), Point(i, hi), normal, color);
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color) {
            if ((b - a).Cross(c - a).Dot(normal) > 0) (b, c) = (c, b);
            mesh.SetNormal(normal);
            mesh.SetColor(color);
            mesh.SetUV(new Vector2(a.X, a.Y + a.Z)); mesh.AddVertex(a);
            mesh.SetUV(new Vector2(b.X, b.Y + b.Z)); mesh.AddVertex(b);
            mesh.SetUV(new Vector2(c.X, c.Y + c.Z)); mesh.AddVertex(c);
        }
    }

    private static void Drum(Node3D root) {
        Cylinder(root, .127f, DrumRadius, .115f, Vector3.Zero, Wood("815235"), sides: 12);
        Cylinder(root, .135f, .138f, .016f, new(0, -.052f, 0), Wood("583725"), sides: 12);
        Cylinder(root, .154f, .156f, .018f, new(0, DrumHead - .009f, 0), Cloth("dbc095", .82f), sides: 12);
        var cord = Cloth("bd9a69");
        for (int i = 0; i < 12; i++) {
            float turn = i * 30;
            var lace = Pivot(root, Vector3.Zero, Vector3.Up * turn);
            Box(lace, new(.008f, .104f, .008f), new(0, .001f, .143f), cord, new(12, 0, 12 * (i % 2 == 0 ? 1 : -1)));
        }
    }
}
