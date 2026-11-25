using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Soft, irregular wet banks along the same fixed boundaries used by movement.</summary>
internal static class ShorelineAppearance {
    private static Shader? bankShader;

    /// <summary>One of a shore photoscan's maps: albedo, normal or height.</summary>
    internal static Texture2D Map(ShoreLook look, string map) => GD.Load<Texture2D>($"res://assets/environment/lake/{look.Name}_{map}.jpg");

    /// <summary>Gives a bank material the shore photoscan, at the scale and wet shade the bed under the water has too.</summary>
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
        tool.AddVertex(new Vector3(p.X, .013f - Terrain.BedDepth(p), p.Y));
    }

    public static void BuildLakeBank(Node3D parent) {
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 192, rings = 5;
        void At(float angle, float t) {
            float width = 2.75f + .3f * MathF.Sin(angle * 5 + .8f) + .2f * MathF.Sin(angle * 11);
            float distance = Mathf.Lerp(BankStart(ShoreLook.Rocks), width, t);
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
        parent.AddChild(new MeshInstance3D { Name = "SoftLakeBank", Mesh = tool.Commit(), MaterialOverride = BankMaterial(ShoreLook.Rocks), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public static CoastView BuildCoast(Node3D parent, Node3D warm, Texture2D gravel, bool still) {
        var bank = new SurfaceTool(); bank.Begin(Mesh.PrimitiveType.Triangles);
        const float step = .75f;
        void At(float across, float t) {
            float width = Terrain.SandWidth(across);
            float distance = Mathf.Lerp(BankStart(ShoreLook.Sand) + Terrain.LowTideShift, width, t);
            Vertex(bank, Terrain.Point(Terrain.Waterline(across) - distance, across), distance, width);
        }
        for (float across = -300; across < 300; across += step)
            for (int strip = 0; strip < 6; strip++) {
                float a = strip / 6f, b = (strip + 1) / 6f;
                At(across, a); At(across, b); At(across + step, b);
                At(across, a); At(across + step, b); At(across + step, a);
            }
        var sand = BankMaterial(ShoreLook.Sand);
        sand.SetShaderParameter("high_tide_margin", Beach.CrestMargin);
        sand.SetShaderParameter("coastal_gravel", gravel);
        parent.AddChild(new MeshInstance3D { Name = "SoftCoastalBank", Mesh = bank.Commit(), MaterialOverride = sand, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // The water mesh extends inland past the widest bank. Its shader cuts the wandering edge as the tide moves.
        // It is dense wherever the map can show it, and coarser out at sea and along the coast past the map's ends.
        float[] sides = [.. LakeAppearance.Steps(-300, -150, 3).SkipLast(1), .. LakeAppearance.Steps(-150, 150, WaterBody.Spacing), .. LakeAppearance.Steps(150, 300, 3).Skip(1)];
        float[] distances = [.. LakeAppearance.Steps(-11, 90, WaterBody.Spacing), 120, 200, 400];
        var material = LakeAppearance.CreateMaterial(WaterBody.Sea, still);
        material.SetShaderParameter("shore_wander", ShoreLook.Sand.Wander);
        var water = new Node3D { Name = "CoastalWater" };
        parent.AddChild(water);
        LakeAppearance.AddTiles(water, "CoastalWater", sides, distances, (side, distance) => {
            var p = Terrain.Point(Terrain.Waterline(side) + distance, side);
            return new Vector3(p.X, 0, p.Y);
        }, material, (side, distance) => new Vector2(Beach.HighTideShift(side), distance));
        return new CoastView(parent, warm, water, sand, material);
    }
}
