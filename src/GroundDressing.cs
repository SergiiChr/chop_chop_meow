using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Coherent meadow and woodland ground cover, built once; only building footprints update on rare world changes.</summary>
public partial class WorldView {
    private readonly FastNoiseLite meadowNoise = new() { Seed = 4271, Frequency = .17f, FractalOctaves = 2 };
    private readonly FastNoiseLite grainNoise = new() { Seed = 78, Frequency = 2.7f, FractalOctaves = 2 };
    private readonly FastNoiseLite soilNoise = new() { Seed = 91, Frequency = .075f, FractalOctaves = 3 };
    private readonly byte[] disturbed = new byte[Terrain.Size * Terrain.Size];
    private readonly byte[] woodland = new byte[Terrain.Size * Terrain.Size];
    private Image disturbanceImage = null!;
    private ImageTexture disturbanceTexture = null!;
    private ShaderMaterial dressingMaterial = null!;
    private static readonly StringName Disturbance = "disturbance";

    private const string GroundShader = """
        shader_type spatial;
        uniform sampler2D ground_colors : source_color, filter_linear, repeat_disable;
        uniform sampler2D disturbance : filter_linear, repeat_disable;
        varying vec2 world_uv;
        void vertex() { world_uv = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz / 216.0; }
        void fragment() {
            float wear = texture(disturbance, world_uv).r;
            vec3 grass = texture(ground_colors, world_uv).rgb;
            ALBEDO = mix(grass, vec3(0.23, 0.205, 0.14), wear * 0.45);
            ROUGHNESS = 1.0;
        }
        """;
    private const string DressingShader = """
        shader_type spatial;
        render_mode cull_disabled;
        uniform sampler2D disturbance : filter_linear, repeat_disable;
        varying float wear;
        varying vec3 tint;
        void vertex() {
            vec2 uv = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz / 216.0;
            wear = texture(disturbance, uv).r;
            VERTEX *= 1.0 - smoothstep(0.12, 0.65, wear);
            // Match the sRGB vertex/instance colors used by the other nature materials.
            tint = pow(COLOR.rgb, vec3(2.2));
        }
        void fragment() {
            if (wear > 0.64) { discard; }
            ALBEDO = tint;
            ROUGHNESS = 1.0;
        }
        """;

    private float Meadow(Vector2 p) => Math.Clamp((meadowNoise.GetNoise2D(p.X, p.Y) + .28f) * 1.7f, 0, 1);
    private float Woodland(Vector2 p) {
        int x = Math.Clamp((int)p.X, 0, Terrain.Size - 1), y = Math.Clamp((int)p.Y, 0, Terrain.Size - 1);
        return woodland[y * Terrain.Size + x] / 255f;
    }
    private static bool DressingGround(Vector2 p) {
        if (!Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 1) || Terrain.FromMountains(p) < .6f) return false;
        foreach (var cliff in World.Cliffs) {
            var offset = p - World.MineSpot(cliff);
            float depth = offset.Dot(World.MineOut(cliff));
            if (Math.Abs(offset.Dot(cliff.Along)) < 2.8f && depth > -6 && depth < 2.4f) return false;
        }
        return true;
    }

    private void BuildMeadowGround(Node3D parent) {
        // Soft beds belong to actual tree groups, rather than arbitrary spots in the open meadow.
        foreach (var tree in World.S.Things) {
            if (tree.Kind != "tree") continue;
            Stamp(woodland, tree.Pos, 3.5f, .7f);
        }
        const int resolution = Terrain.Size * 4;
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgb8);
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++) {
                var p = new Vector2(x * .25f, y * .25f);
                float meadow = Meadow(p), woods = Woodland(p), variation = soilNoise.GetNoise2D(p.X, p.Y);
                var tint = new Color("7f9462").Lerp(new Color("a2a273"), (1 - meadow) * .8f);
                tint = tint.Lerp(new Color("646d46"), woods * .65f);
                tint = tint.Lightened(variation * .12f + grainNoise.GetNoise2D(p.X, p.Y) * .13f);
                image.SetPixel(x, y, tint);
            }
        disturbanceImage = Image.CreateFromData(Terrain.Size, Terrain.Size, false, Image.Format.R8, disturbed);
        disturbanceTexture = ImageTexture.CreateFromImage(disturbanceImage);
        var ground = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
        ground.SetShaderParameter("ground_colors", ImageTexture.CreateFromImage(image));
        ground.SetShaderParameter(Disturbance, disturbanceTexture);
        dressingMaterial = new ShaderMaterial { Shader = new Shader { Code = DressingShader } };
        dressingMaterial.SetShaderParameter(Disturbance, disturbanceTexture);
        const float span = Terrain.Size * 4;
        Add(parent, new PlaneMesh { Size = new Vector2(span, span) }, ground, new Vector3(Terrain.Size / 2f, 0, Terrain.Size / 2f));
    }

    private static Mesh FernMesh() {
        var mesh = new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        void Leaf(Vector3 a, Vector3 b, Vector3 c) {
            mesh.SetColor(Colors.White); mesh.SetNormal(Vector3.Up); mesh.AddVertex(a); mesh.AddVertex(b); mesh.AddVertex(c);
        }
        for (int frond = 0; frond < 7; frond++) {
            float angle = frond * 2.39996f;
            var along = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            var side = new Vector3(-along.Z, 0, along.X);
            for (int i = 1; i <= 5; i++) {
                float t = i / 6f, width = .16f * (1 - t);
                var stem = along * (t * .64f) + Vector3.Up * (.1f + MathF.Sin(t * 2.6f) * .27f);
                foreach (int sign in new[] { -1, 1 }) Leaf(stem - along * .065f, stem + side * (width * sign) - along * .04f, stem + along * .09f + Vector3.Up * .015f);
            }
        }
        return mesh.Commit();
    }

    private static Mesh TwigMesh() {
        var mesh = new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        void Branch(Vector3 start, Vector3 end, float width) {
            var direction = (end - start).Normalized(); var side = new Vector3(-direction.Z, 0, direction.X) * width;
            mesh.SetColor(Colors.White); mesh.SetNormal(Vector3.Up);
            mesh.AddVertex(start - side); mesh.AddVertex(end - side * .4f); mesh.AddVertex(end + side * .4f);
            mesh.AddVertex(start - side); mesh.AddVertex(end + side * .4f); mesh.AddVertex(start + side);
        }
        Branch(new(-.48f, 0, -.06f), new(.5f, .025f, .1f), .035f);
        Branch(new(.05f, .015f, .02f), new(.36f, .01f, -.32f), .021f);
        Branch(new(-.16f, .01f, 0), new(-.35f, .02f, .23f), .018f);
        return mesh.Commit();
    }

    /// <summary>A soft footprint in a reusable map; max blending keeps overlapping work areas continuous.</summary>
    private static void Stamp(byte[] map, Vector2 at, float radius, float strength) {
        int left = Math.Max(0, (int)(at.X - radius)), right = Math.Min(Terrain.Size - 1, (int)(at.X + radius) + 1);
        int top = Math.Max(0, (int)(at.Y - radius)), bottom = Math.Min(Terrain.Size - 1, (int)(at.Y + radius) + 1);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++) {
                float distance = new Vector2(x + .5f, y + .5f).DistanceTo(at) / radius;
                float edge = Math.Clamp((1 - distance) * 2.4f, 0, 1);
                byte value = (byte)(edge * edge * (3 - 2 * edge) * strength * 255);
                int index = y * Terrain.Size + x;
                map[index] = Math.Max(map[index], value);
            }
    }

    /// <summary>Called only when loading or things are added/removed, never for ordinary frames, jobs or panel refreshes.</summary>
    private void RefreshDressing() {
        if (disturbanceTexture == null) return;
        Array.Clear(disturbed);
        Stamp(disturbed, World.Spawn, 7.5f, .8f);
        float across = Terrain.Across(World.Spawn);
        for (float along = Terrain.Along(World.Spawn); along < Terrain.Coast(across); along += .8f) {
            var at = Terrain.Point(along, across + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f);
            Stamp(disturbed, at, 1.25f, .85f);
        }
        foreach (var cliff in World.Cliffs) {
            var at = World.MineSpot(cliff); var outwards = World.MineOut(cliff);
            for (float d = -3; d < 2; d += .75f) Stamp(disturbed, at + outwards * d, 2.6f, .95f);
        }
        foreach (var t in World.S.Things) {
            if (t.IsResource || t.Kind is "crate" or "grave") continue;
            float radius = t.Kind switch { "hut" or "pile" or "farm" => 3.4f, "mine" => 3, "campfire" => 2.3f, _ => 1.8f };
            Stamp(disturbed, t.Pos, radius, 1);
        }
        disturbanceImage.SetData(Terrain.Size, Terrain.Size, false, Image.Format.R8, disturbed);
        disturbanceTexture.Update(disturbanceImage);
    }
}
