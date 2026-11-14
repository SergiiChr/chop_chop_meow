using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Soft, irregular wet banks along the same fixed boundaries used by movement.</summary>
internal static class ShorelineAppearance {
    private static Shader? bankShader;

    /// <summary>A shore photoscan and how it is laid along the water.</summary>
    /// <param name="Name">The file prefix of its colour, bumps and height maps in the lake folder.</param>
    /// <param name="Tile">World units one tile of the photoscan spans.</param>
    /// <param name="Wet">Brightness at the waterline, where it is wet, as a share of the dry shore's.</param>
    /// <param name="GrassFade">Units over which the shore thins out into the grass.</param>
    /// <param name="Depth">Units of water over which the shore sinks out of sight.</param>
    /// <param name="Wander">Units over which the shore's edges wander in small bites: the waterline half that either side
    /// of its line, the grass edge that much on top of its broad patches.</param>
    internal sealed record ShoreLook(string Name, float Tile, float Wet, float GrassFade, float Depth, float Wander);

    /// <summary>Rippled beach sand along the sea coast.</summary>
    internal static readonly ShoreLook Sand = new("sand", 15, .6f, 2.2f, 3.5f, 1.2f);
    /// <summary>Mossy ground with stones and bare earth around the pond.</summary>
    internal static readonly ShoreLook Rocks = new("rocks", 15, .6f, 1.5f, 2.5f, 0);

    private static Texture2D Map(ShoreLook look, string map) => GD.Load<Texture2D>($"res://assets/environment/lake/{look.Name}_{map}.jpg");

    /// <summary>Gives a bank or water material the shore photoscan, at the same scale and wet shade in both.</summary>
    internal static void UseShore(ShaderMaterial material, ShoreLook look) {
        material.SetShaderParameter("shore_albedo", Map(look, "albedo"));
        material.SetShaderParameter("shore_tile", look.Tile);
        material.SetShaderParameter("shore_wet", look.Wet);
        material.SetShaderParameter("shore_wander", look.Wander);
    }

    /// <summary>Distance from the drawn waterline, seaward, where a bank strip starts under the water.</summary>
    private static float BankStart(ShoreLook look) => -.35f - look.Wander / 2;

    private static ShaderMaterial BankMaterial(ShoreLook look) {
        bankShader ??= GD.Load<Shader>("res://assets/environment/lake/bank.gdshader");
        var material = new ShaderMaterial { Shader = bankShader };
        UseShore(material, look);
        material.SetShaderParameter("shore_normal", Map(look, "normal"));
        material.SetShaderParameter("shore_height", Map(look, "height"));
        material.SetShaderParameter("grass_fade", look.GrassFade);
        material.SetShaderParameter("shore_relief", ModelKit.ScanRelief);
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
            float width = 2.75f + .3f * MathF.Sin(angle * 5 + .8f) + .2f * MathF.Sin(angle * 11);
            float distance = Mathf.Lerp(BankStart(Rocks), width, t);
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
        parent.AddChild(new MeshInstance3D { Name = "SoftLakeBank", Mesh = tool.Commit(), MaterialOverride = BankMaterial(Rocks), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public static void BuildCoast(Node3D parent, bool still) {
        var bank = new SurfaceTool(); bank.Begin(Mesh.PrimitiveType.Triangles);
        const float step = .75f;
        void At(float across, float t) {
            float width = Terrain.SandWidth(across);
            float distance = Mathf.Lerp(BankStart(Sand), width, t);
            Vertex(bank, Terrain.Point(Terrain.Coast(across) - 1.2f - distance, across), distance, width);
        }
        for (float across = -300; across < 300; across += step)
            for (int strip = 0; strip < 6; strip++) {
                float a = strip / 6f, b = (strip + 1) / 6f;
                At(across, a); At(across, b); At(across + step, b);
                At(across, a); At(across + step, b); At(across + step, a);
            }
        parent.AddChild(new MeshInstance3D { Name = "SoftCoastalBank", Mesh = bank.Commit(), MaterialOverride = BankMaterial(Sand), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        var water = new SurfaceTool(); water.Begin(Mesh.PrimitiveType.Triangles);
        // The water starts inland of its line by as far as the waterline wanders, and its shader cuts the wandering edge.
        float[] bands = { -Sand.Wander / 2, .3f, 1, 2.5f, 6, 14, 40, 400 };
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
        UseShore(material, Sand);
        material.SetShaderParameter("shore_depth", Sand.Depth);
        parent.AddChild(new MeshInstance3D { Name = "CoastalWater", Mesh = water.Commit(), MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
