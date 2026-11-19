using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The ground through the Terrain3D plugin: the valley's heights with its mountains and the beds under the water, the
/// meadow, photoscans, woods and beds drawn on it, and the point its finest detail follows.
/// </summary>
internal sealed class TerrainView {
    private static readonly StringName Traffic = "traffic", Gravel = "gravel";
    private readonly Rid materialRid;
    private readonly Node3D target;
    // RenderingServer uniforms take RIDs, so retain the texture resources for their lifetime.
    private readonly List<Texture2D> maps = new();
    /// <summary>The ground photoscans drawn as they are, each with its colour, OpenGL normal and height maps beside the terrain, and the world units one tile spans, about its real size.</summary>
    private static readonly (string Name, float Tile)[] GroundScans = { ("path", 2), ("moss", 15), ("forest", 2) };
    /// <summary>World units out from the foot of the ranges that the woods' floor reaches, where <see cref="World"/> scatters trees at better than even odds.</summary>
    private const float WoodsBand = 10;
    /// <summary>World units out from the foot of the ranges that the woods map measures; the shader's frayed band ends within it.</summary>
    private const float WoodsReach = 16;
    /// <summary>The ground texture sets, in Terrain3D id order, with the world units one tile spans: the auto shader puts grass on flat ground and rock on slopes.</summary>
    private static readonly (string Name, float Tile)[] GroundTextures = { ("grass", 4), ("rock", 6) };
    /// <summary>Soft noise over the whole map, about fifteen tiles a blob, for lusher and drier patches of meadow.</summary>
    private readonly NoiseTexture2D terrainPatches = new() { Width = 256, Height = 256, Noise = new FastNoiseLite { Seed = 913, Frequency = .05f, FractalOctaves = 3 } };

    /// <summary>
    /// Adds Terrain3D's clipmaps for the valley under <paramref name="parent"/>, seen by <paramref name="camera"/> and lit
    /// by <paramref name="sun"/>, with the meadow's <paramref name="grass"/> and the worn ground and shade maps, its finest
    /// detail first under <paramref name="start"/>.
    /// </summary>
    public TerrainView(Node3D parent, Camera3D camera, DirectionalLight3D sun, Texture2D grass, Texture2D disturbance, Texture2D sceneryShade, Texture2D objectShade, Vector2 start) {
        const string extension = "res://addons/terrain_3d/terrain.gdextension";
        if (!FileAccess.FileExists(extension)) throw new InvalidOperationException("Terrain3D is missing. Launch with ./build.py setup to install the pinned runtime.");
        var loaded = GDExtensionManager.LoadExtension(extension);
        if (loaded is not (GDExtensionManager.LoadStatus.Ok or GDExtensionManager.LoadStatus.AlreadyLoaded))
            throw new InvalidOperationException($"Terrain3D extension could not load: {loaded}");
        var terrain = (Node3D)ClassDB.Instantiate("Terrain3D").AsGodotObject()!;
        terrain.Name = "Landscape";
        target = new Node3D { Name = "TerrainDetailTarget", Position = To3(start) };
        parent.AddChild(target);
        terrain.Set("clipmap_target", target);
        terrain.Set("free_editor_textures", false);
        terrain.Set("collision_mode", 0);
        terrain.Call("set_camera", camera);
        parent.AddChild(terrain);
        var data = terrain.Get("data").AsGodotObject()!;
        const int regionSize = 256;
        var samples = new float[regionSize * regionSize];
        var bytes = new byte[samples.Length * sizeof(float)];
        // Padding regions cover both ranges beyond the 216-tile map, avoiding a visible terrain boundary.
        for (int rz = -1; rz <= 1; rz++)
            for (int rx = -1; rx <= 1; rx++) {
                // Rows are independent and the noise lookups are read-only, so the heights fill on all cores.
                int originX = rx * regionSize, originZ = rz * regionSize;
                Parallel.For(0, regionSize, z => {
                    for (int x = 0; x < regionSize; x++)
                        samples[z * regionSize + x] = GroundHeight(new Vector2(originX + x, originZ + z));
                });
                MemoryMarshal.AsBytes(samples.AsSpan()).CopyTo(bytes);
                using var heights = Image.CreateFromData(regionSize, regionSize, false, Image.Format.Rf, bytes);
                var region = data.Call("add_region_blank", new Vector2I(rx, rz), false).AsGodotObject()!;
                region.Call("set_height_map", heights);
                region.Call("calc_height_range");
            }
        data.Call("update_maps");
        float floor = data.Call("get_height", To3(Terrain.Spawn)).AsSingle();
        if (!float.IsFinite(floor) || Math.Abs(floor) > .001f) throw new InvalidOperationException($"Terrain3D playable floor moved to {floor}");
        foreach (var p in new[] { new Vector2(-20, 80), new Vector2(70, 30), new Vector2(260, 150), Terrain.Lake.Round() }) {
            float expected = GroundHeight(p), actual = data.Call("get_height", To3(p)).AsSingle();
            if (!float.IsFinite(actual) || Math.Abs(actual - expected) > .001f)
                throw new InvalidOperationException($"Terrain3D region height mismatch at {p}: {actual} instead of {expected}");
        }

        // The include keeps the meadow's own colours and takes only grain and bump from these photoscans.
        var assets = ClassDB.Instantiate("Terrain3DAssets").AsGodotObject()!;
        terrain.Set("assets", assets);
        for (int id = 0; id < GroundTextures.Length; id++) {
            var (name, tile) = GroundTextures[id];
            var asset = ClassDB.Instantiate("Terrain3DTextureAsset").AsGodotObject()!;
            asset.Set("name", name);
            asset.Set("id", id);
            asset.Set("albedo_texture", GD.Load<Texture2D>($"res://assets/environment/terrain3d/textures/{name}_albedo.png"));
            asset.Set("normal_texture", GD.Load<Texture2D>($"res://assets/environment/terrain3d/textures/{name}_normal.png"));
            asset.Set("uv_scale", 1 / tile);
            asset.Set("detiling_rotation", .25f);
            asset.Set("detiling_shift", .25f);
            assets.Call("set_texture_asset", id, asset);
        }
        var material = terrain.Get("material").AsGodotObject()!;
        material.Set("show_checkered", false);
        // Set before the override is generated, so the generated code blends the base rock into the overlay grass by slope.
        material.Set("auto_shader_enabled", true);
        material.Call("set_shader_param", "auto_base_texture", 1);
        material.Call("set_shader_param", "auto_overlay_texture", 0);
        // Terrain3D damps the ground's specular where it faces away from this light; without it the shader normalises a zero vector.
        terrain.Set("light_target", sun);
        material.Call("set_shader_override_enabled", true);
        var shader = material.Get("shader_override").As<Shader>();
        string code = shader.Code;
        int fragment = code.LastIndexOf("\nvoid fragment()", StringComparison.Ordinal);
        if (fragment < 0) throw new InvalidOperationException("Pinned Terrain3D shader no longer has the expected fragment entry point.");
        code = code.Insert(fragment, GD.Load<ShaderInclude>("res://assets/environment/terrain3d/uniforms.gdshaderinc").Code);
        int end = code.LastIndexOf("\n}", StringComparison.Ordinal);
        if (end < fragment) throw new InvalidOperationException("Pinned Terrain3D shader fragment boundary changed.");
        shader.Code = code.Insert(end, GD.Load<ShaderInclude>("res://assets/environment/terrain3d/fragment.gdshaderinc").Code);
        materialRid = material.Call("get_material_rid").AsRid();
        void Use(string name, Texture2D texture) { maps.Add(texture); RenderingServer.MaterialSetParam(materialRid, name, texture.GetRid()); }
        Use("grass", grass);
        Use("patches", terrainPatches);
        Use("disturbance", disturbance);
        Use("shade", sceneryShade);
        Use("contact", objectShade);
        void Scan(string name, float tile, Func<string, Texture2D> load) {
            RenderingServer.MaterialSetParam(materialRid, $"{name}_tile", tile);
            foreach (var map in new[] { "albedo", "normal", "height" }) Use($"{name}_{map}", load(map));
        }
        foreach (var (scan, tile) in GroundScans) Scan(scan, tile, map => GD.Load<Texture2D>($"res://assets/environment/terrain3d/{scan}_{map}.jpg"));
        // Gravel paths take the scan the models' gravel is drawn with, at the same size.
        Scan("gravel_scan", ModelKit.ScanTile, map => ModelKit.ScanMap(ChopChopMeow.Scan.Gravel, map == "normal" ? "bump" : map));
        foreach (var look in new[] { ShoreLook.Rocks, ShoreLook.Sand }) {
            Scan(look.Name, look.Tile, map => ShorelineAppearance.Map(look, map));
            RenderingServer.MaterialSetParam(materialRid, $"{look.Name}_wet", look.Wet);
        }
        RenderingServer.MaterialSetParam(materialRid, "scan_relief", ModelKit.ScanRelief);
        // One texel a world unit over all nine regions: how far each texel's middle, where filtering reads it, lies from
        // the foot of the nearer range, which is where trees thicken into woods.
        const int span = 3 * regionSize;
        var woods = new byte[span * span];
        Parallel.For(0, span, z => {
            for (int x = 0; x < span; x++) {
                float from = Terrain.FromMountains(new Vector2(x - regionSize + .5f, z - regionSize + .5f));
                woods[z * span + x] = (byte)Math.Round(Math.Clamp(from / WoodsReach, 0, 1) * 255);
            }
        });
        Use("woods_distance", ImageTexture.CreateFromImage(Image.CreateFromData(span, span, false, Image.Format.R8, woods)));
        RenderingServer.MaterialSetParam(materialRid, "map_corner", new Vector2(-regionSize, -regionSize));
        RenderingServer.MaterialSetParam(materialRid, "map_span", (float)span);
        RenderingServer.MaterialSetParam(materialRid, "woods_reach", WoodsReach);
        RenderingServer.MaterialSetParam(materialRid, "woods_band", WoodsBand);
        Use("beds", BedMap.Bake(new Vector2(-regionSize, -regionSize), span));
        // The shader's tunnel cut-outs hold exactly one entry per cliff.
        if (World.Cliffs.Length != 5) throw new InvalidOperationException($"Terrain3D mine cut-outs expect 5 cliffs, not {World.Cliffs.Length}.");
        var mines = new Vector4[World.Cliffs.Length];
        for (int i = 0; i < mines.Length; i++) {
            var cliff = World.Cliffs[i]; var at = World.MineSpot(cliff);
            mines[i] = new Vector4(at.X, at.Y, cliff.Face.X, cliff.Face.Y);
        }
        RenderingServer.MaterialSetParam(materialRid, "mines", mines);
        GD.Print($"TERRAIN3D: v1.1.0-rc1, 9 regions, {regionSize}² samples/region; playable floor {floor:0.000}");
    }

    /// <summary>The ground's height: the mountains, the flat valley floor between them, and the beds under the water.</summary>
    private static float GroundHeight(Vector2 p) => Math.Max(0, MountainShape.Height(p) - .03f) - Terrain.BedDepth(p);

    /// <summary>Wears footpaths into the ground where <paramref name="traffic"/>, the map of how much the ground has been walked, says.</summary>
    public void SetTraffic(Texture2D traffic) => RenderingServer.MaterialSetParam(materialRid, Traffic, traffic.GetRid());

    /// <summary>Draws gravel where <paramref name="gravel"/>, the map of how far each spot is inside the gravel paths' edge, says.</summary>
    public void SetGravel(Texture2D gravel) => RenderingServer.MaterialSetParam(materialRid, Gravel, gravel.GetRid());

    /// <summary>Keeps Terrain3D's finest detail under <paramref name="at"/>.</summary>
    public void Follow(Vector2 at) => target.Position = To3(at);

    private static Vector3 To3(Vector2 p) => new(p.X, 0, p.Y);
}
