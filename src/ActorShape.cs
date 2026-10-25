using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Smooth elliptical profiles, with optional two-bone weights across a limb's joint.</summary>
internal static class ActorShape
{
    internal readonly record struct Ring(float Y, float Width, float Depth, Color Color, float Forward = 0, float Sideways = 0);

    private static readonly Dictionary<string, ArrayMesh> meshes = new();
    private static readonly StandardMaterial3D Surface = new() { VertexColorUseAsAlbedo = true, Roughness = .85f };

    /// <summary>A cloth panel fitted to the shared torso profile, with no separate block over the shirt.</summary>
    public static void Bib(Node3D parent, string color)
    {
        if (!meshes.TryGetValue("bib", out var mesh))
        {
            var rows = new[] { (.39f, .065f, .174f, .102f), (.36f, .105f, .171f, .104f),
                (.23f, .105f, .145f, .095f), (.14f, .115f, .14f, .091f), (.12f, .105f, .136f, .088f) };
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var (y, halfWidth, bodyWidth, bodyDepth) in rows)
                for (int column = 0; column < 5; column++)
                {
                    float x = (column / 2f - 1) * halfWidth;
                    float z = bodyDepth * MathF.Sqrt(1 - x * x / (bodyWidth * bodyWidth));
                    surface.SetNormal(new Vector3(x / (bodyWidth * bodyWidth), 0, z / (bodyDepth * bodyDepth)).Normalized());
                    surface.AddVertex(new Vector3(x, y, z + .006f));
                }
            for (int row = 0; row < rows.Length - 1; row++)
                for (int column = 0; column < 4; column++)
                {
                    int a = row * 5 + column, b = a + 5;
                    surface.AddIndex(a); surface.AddIndex(a + 1); surface.AddIndex(b);
                    surface.AddIndex(a + 1); surface.AddIndex(b + 1); surface.AddIndex(b);
                }
            meshes["bib"] = mesh = surface.Commit();
        }
        parent.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = ModelKit.Mat(color) });
    }

    /// <summary>A thin, closed webbing strip swept along a smooth path, including its edge thickness.</summary>
    public static void Strap(Node3D parent, Vector3[] anchors, string color = "485335")
    {
        string key = $"strap|{string.Join("|", anchors)}";
        if (!meshes.TryGetValue(key, out var mesh)) meshes[key] = mesh = BuildStrap(anchors);
        parent.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = ModelKit.Mat(color) });
    }

    private static ArrayMesh BuildStrap(Vector3[] anchors)
    {
        const int subdivisions = 2;
        var points = new Vector3[(anchors.Length - 1) * subdivisions + 1];
        for (int i = 0; i < points.Length - 1; i++)
        {
            int segment = i / subdivisions;
            points[i] = anchors[segment].CubicInterpolate(anchors[segment + 1],
                anchors[Math.Max(0, segment - 1)], anchors[Math.Min(anchors.Length - 1, segment + 2)],
                (i % subdivisions) / (float)subdivisions);
        }
        points[^1] = anchors[^1];
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var corners = new Vector3[points.Length, 4];
        var directions = new Vector3[points.Length];
        for (int row = 0; row < points.Length; row++)
        {
            var direction = (points[Math.Min(row + 1, points.Length - 1)] - points[Math.Max(0, row - 1)]).Normalized();
            directions[row] = direction;
            // Keep the broad webbing face against the chest, shoulder and side of the torso.
            var bodyNormal = new Vector3(points[row].X / (.17f * .17f),
                Math.Max(0, points[row].Y - .44f) / (.08f * .08f), points[row].Z / (.1f * .1f));
            var outward = bodyNormal.Slide(direction).Normalized();
            var across = outward.Cross(direction).Normalized();
            var offsets = new[] { across * .015f + outward * .004f, -across * .015f + outward * .004f,
                -across * .015f - outward * .004f, across * .015f - outward * .004f };
            for (int face = 0; face < 4; face++)
            {
                corners[row, face] = points[row] + offsets[face];
                surface.SetNormal((offsets[face] + offsets[(face + 1) % 4]).Normalized());
                surface.AddVertex(points[row] + offsets[face]);
                surface.AddVertex(points[row] + offsets[(face + 1) % 4]);
            }
        }
        for (int row = 0; row < points.Length - 1; row++)
            for (int face = 0; face < 4; face++)
            {
                int a = row * 8 + face * 2, b = (row + 1) * 8 + face * 2;
                surface.AddIndex(a); surface.AddIndex(b); surface.AddIndex(a + 1);
                surface.AddIndex(a + 1); surface.AddIndex(b); surface.AddIndex(b + 1);
            }
        for (int end = 0; end < 2; end++)
        {
            int row = end == 0 ? 0 : points.Length - 1, start = points.Length * 8 + end * 4;
            surface.SetNormal(directions[row] * (end == 0 ? -1 : 1));
            for (int corner = 0; corner < 4; corner++) surface.AddVertex(corners[row, corner]);
            surface.AddIndex(start); surface.AddIndex(start + (end == 0 ? 1 : 2)); surface.AddIndex(start + (end == 0 ? 2 : 1));
            surface.AddIndex(start); surface.AddIndex(start + (end == 0 ? 2 : 3)); surface.AddIndex(start + (end == 0 ? 3 : 2));
        }
        return surface.Commit();
    }

    private static ArrayMesh BuildProfile(Ring[] rings, float joint)
    {
        const int sides = 12;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < rings.Length; i++)
        {
            var ring = rings[i];
            var above = rings[Math.Max(0, i - 1)];
            var below = rings[Math.Min(rings.Length - 1, i + 1)];
            float height = above.Y - below.Y;
            for (int side = 0; side < sides; side++)
            {
                float angle = Mathf.Tau * side / sides, x = MathF.Cos(angle), z = MathF.Sin(angle);
                var tangent = new Vector3(-ring.Width * z, 0, ring.Depth * x);
                var down = new Vector3((below.Width - above.Width) * x + below.Sideways - above.Sideways, -height, (below.Depth - above.Depth) * z + below.Forward - above.Forward);
                surface.SetNormal(tangent.Cross(down).Normalized());
                surface.SetColor(ring.Color);
                if (joint > 0)
                {
                    float weight = Mathf.SmoothStep(joint - .065f, joint + .065f, -ring.Y);
                    surface.SetBones(new[] { 0, 1, 0, 0 });
                    surface.SetWeights(new[] { 1 - weight, weight, 0f, 0f });
                }
                surface.AddVertex(new Vector3(ring.Width * x + ring.Sideways, ring.Y, ring.Depth * z + ring.Forward));
            }
        }
        for (int row = 0; row < rings.Length - 1; row++)
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides, a = row * sides + side, b = (row + 1) * sides + side;
                int c = row * sides + next, d = (row + 1) * sides + next;
                surface.AddIndex(a); surface.AddIndex(b); surface.AddIndex(c);
                surface.AddIndex(c); surface.AddIndex(b); surface.AddIndex(d);
            }
        // Close the profiles, including the soles and the pack's rounded top.
        for (int end = 0; end < 2; end++)
        {
            int row = end == 0 ? 0 : rings.Length - 1;
            var ring = rings[row];
            surface.SetNormal(end == 0 ? Vector3.Up : Vector3.Down);
            surface.SetColor(ring.Color);
            if (joint > 0)
            {
                surface.SetBones(new[] { 0, 1, 0, 0 });
                float weight = Mathf.SmoothStep(joint - .065f, joint + .065f, -ring.Y);
                surface.SetWeights(new[] { 1 - weight, weight, 0f, 0f });
            }
            int center = rings.Length * sides + end;
            surface.AddVertex(new Vector3(ring.Sideways, ring.Y, ring.Forward));
            for (int side = 0; side < sides; side++)
            {
                int a = row * sides + side, b = row * sides + (side + 1) % sides;
                surface.AddIndex(center); surface.AddIndex(end == 0 ? a : b); surface.AddIndex(end == 0 ? b : a);
            }
        }
        return surface.Commit();
    }

    public static void Add(Node3D parent, Ring[] rings, float joint = 0, Material? material = null)
    {
        string key = $"profile|{joint}|{string.Join("|", rings)}";
        if (!meshes.TryGetValue(key, out var profile)) meshes[key] = profile = BuildProfile(rings, joint);
        var mesh = new MeshInstance3D { Mesh = profile, MaterialOverride = material ?? Surface };
        if (joint > 0)
        {
            var skin = new Skin();
            skin.AddBind(0, Transform3D.Identity);
            skin.AddBind(1, new Transform3D(Basis.Identity, new Vector3(0, joint, 0)));
            mesh.Skin = skin;
            mesh.Skeleton = new NodePath("..");
        }
        parent.AddChild(mesh);
    }

    public static Skeleton3D Limb(Node3D parent, Ring[] rings, float joint)
    {
        var skeleton = new Skeleton3D();
        parent.AddChild(skeleton);
        skeleton.AddBone("upper"); skeleton.AddBone("lower");
        skeleton.SetBoneParent(1, 0);
        skeleton.SetBoneRest(0, Transform3D.Identity);
        skeleton.SetBoneRest(1, new Transform3D(Basis.Identity, new Vector3(0, -joint, 0)));
        skeleton.ResetBonePoses();
        Add(skeleton, rings, joint);
        return skeleton;
    }
}
