using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Flat-shore lake geometry and a small, lake-specific animated material.</summary>
public static class LakeAppearance {
    private const string ShaderPath = "res://assets/environment/lake/lake.gdshader";
    private const int Segments = 192;
    private const int Rings = 8;
    private static Shader? shader;

    /// <summary>Adds the lake's single water surface. Banks and shoreline props stay with the caller.</summary>
    public static void BuildLakeAppearance(Node3D parent) {
        shader ??= GD.Load<Shader>(ShaderPath);
        if (shader == null) throw new InvalidOperationException($"Could not load lake shader at {ShaderPath}");

        var material = CreateMaterial();

        var surface = new MeshInstance3D {
            Name = "lake_water_surface",
            Mesh = BuildSurface(),
            MaterialOverride = material,
            Position = new Vector3(Terrain.Lake.X, .025f, Terrain.Lake.Y),
        };
        parent.AddChild(surface);
    }

    internal static ShaderMaterial CreateMaterial() {
        shader ??= GD.Load<Shader>(ShaderPath);
        var material = new ShaderMaterial { Shader = shader };
        if (HasEnvironmentReviewFlag()) material.SetShaderParameter("review_time", 1.25f);
        foreach (string argument in OS.GetCmdlineUserArgs())
            if (argument == "--shoreline-baseline") material.SetShaderParameter("soft_shore", false);
        return material;
    }

    private static bool HasEnvironmentReviewFlag() {
        foreach (string argument in OS.GetCmdlineUserArgs())
            if (argument == "--environment-review") return true;
        return false;
    }

    private static ArrayMesh BuildSurface() {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int ring = 0; ring < Rings; ring++) {
            float innerRadius = ring / (float)Rings;
            float outerRadius = (ring + 1) / (float)Rings;
            for (int segment = 0; segment < Segments; segment++) {
                float angleA = segment * Mathf.Tau / Segments;
                float angleB = (segment + 1) * Mathf.Tau / Segments;
                var innerA = Vertex(innerRadius, angleA);
                var outerA = Vertex(outerRadius, angleA);
                var outerB = Vertex(outerRadius, angleB);
                var innerB = Vertex(innerRadius, angleB);
                AddTriangle(tool, innerA, outerA, outerB);
                if (ring > 0) AddTriangle(tool, innerA, outerB, innerB);
            }
        }
        return tool.Commit();
    }

    private static Vector3 Vertex(float normalizedRadius, float angle) {
        float shore = Terrain.Shore(angle) * normalizedRadius;
        return new Vector3(MathF.Cos(angle) * shore, 0, MathF.Sin(angle) * shore);
    }

    private static void AddTriangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c) {
        AddVertex(tool, a, Uv(a));
        AddVertex(tool, b, Uv(b));
        AddVertex(tool, c, Uv(c));
    }

    private static Vector2 Uv(Vector3 position) =>
        new Vector2(position.X / Terrain.Shore(MathF.Atan2(position.Z, position.X)), position.Z / Terrain.Shore(MathF.Atan2(position.Z, position.X)));

    private static void AddVertex(SurfaceTool tool, Vector3 position, Vector2 uv) {
        tool.SetNormal(Vector3.Up);
        tool.SetUV(uv);
        tool.AddVertex(position);
    }
}
