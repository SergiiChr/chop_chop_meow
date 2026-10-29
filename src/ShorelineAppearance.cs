using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Soft, irregular wet banks along the same fixed boundaries used by movement.</summary>
internal static class ShorelineAppearance {
    private static Shader? bankShader;

    private static ShaderMaterial BankMaterial(bool coast) {
        bankShader ??= GD.Load<Shader>("res://assets/environment/lake/bank.gdshader");
        var material = new ShaderMaterial { Shader = bankShader };
        material.SetShaderParameter("dry_color", new Color(coast ? "b5a47b" : "827956"));
        material.SetShaderParameter("wet_color", new Color(coast ? "6d7155" : "50553d"));
        return material;
    }

    private static void Vertex(SurfaceTool tool, Vector2 p, float distance, float width) {
        tool.SetNormal(Vector3.Up);
        tool.SetUV(new Vector2(distance, width));
        tool.AddVertex(new Vector3(p.X, .013f, p.Y));
    }

    public static void BuildLakeBank(Node3D parent) {
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 192, rings = 5;
        void At(float angle, float t) {
            float width = 1.25f + .3f * MathF.Sin(angle * 5 + .8f) + .2f * MathF.Sin(angle * 11);
            float distance = Mathf.Lerp(-.35f, width, t);
            var p = Terrain.Lake + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (Terrain.Shore(angle) + distance);
            Vertex(tool, p, distance, width);
        }
        for (int i = 0; i < segments; i++)
            for (int ring = 0; ring < rings; ring++) {
                float a = i * Mathf.Tau / segments, b = (i + 1) * Mathf.Tau / segments;
                float inner = ring / (float)rings, outer = (ring + 1) / (float)rings;
                At(a, inner); At(a, outer); At(b, outer);
                At(a, inner); At(b, outer); At(b, inner);
            }
        parent.AddChild(new MeshInstance3D { Name = "SoftLakeBank", Mesh = tool.Commit(), MaterialOverride = BankMaterial(false), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public static void BuildCoast(Node3D parent, bool still) {
        var bank = new SurfaceTool(); bank.Begin(Mesh.PrimitiveType.Triangles);
        const float step = .75f;
        void At(float across, float t) {
            float width = 3.65f + .35f * MathF.Sin(across * .31f) + .2f * MathF.Sin(across * .83f);
            float distance = Mathf.Lerp(-.35f, width, t);
            Vertex(bank, Terrain.Point(Terrain.Coast(across) - 1.2f - distance, across), distance, width);
        }
        for (float across = -300; across < 300; across += step)
            for (int strip = 0; strip < 6; strip++) {
                float a = strip / 6f, b = (strip + 1) / 6f;
                At(across, a); At(across, b); At(across + step, b);
                At(across, a); At(across + step, b); At(across + step, a);
            }
        parent.AddChild(new MeshInstance3D { Name = "SoftCoastalBank", Mesh = bank.Commit(), MaterialOverride = BankMaterial(true), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        var water = new SurfaceTool(); water.Begin(Mesh.PrimitiveType.Triangles);
        float[] bands = { 0, .3f, 1, 2.5f, 6, 14, 40, 400 };
        void WaterAt(float across, float distance) {
            var p = Terrain.Point(Terrain.Coast(across) - 1.2f + distance, across);
            water.SetNormal(Vector3.Up); water.SetUV(new Vector2(across, distance));
            water.AddVertex(new Vector3(p.X, .025f, p.Y));
        }
        for (float across = -300; across < 300; across += step)
            for (int strip = 0; strip + 1 < bands.Length; strip++) {
                float a = bands[strip], b = bands[strip + 1];
                WaterAt(across, a); WaterAt(across + step, b); WaterAt(across, b);
                WaterAt(across, a); WaterAt(across + step, a); WaterAt(across + step, b);
            }
        var material = LakeAppearance.CreateMaterial(still);
        material.SetShaderParameter("coastal", true);
        parent.AddChild(new MeshInstance3D { Name = "CoastalWater", Mesh = water.Commit(), MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
