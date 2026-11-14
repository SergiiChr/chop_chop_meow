using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

public partial class WorldView {
    private Rid terrainMaterialRid;
    private Node3D? terrainTarget;
    // RenderingServer uniforms take RIDs, so retain the texture resource for their lifetime.
    private Texture2D? terrainGrass;
    /// <summary>The ground texture sets, in Terrain3D id order, with the world units one tile spans: the auto shader puts grass on flat ground and rock on slopes, and the include draws dirt on worn ground.</summary>
    private static readonly (string Name, float Tile)[] GroundTextures = { ("grass", 4), ("rock", 6), ("dirt", 4) };
    /// <summary>Soft noise over the whole map, about fifteen tiles a blob, for lusher and drier patches of meadow.</summary>
    private readonly NoiseTexture2D terrainPatches = new() { Width = 256, Height = 256, Noise = new FastNoiseLite { Seed = 913, Frequency = .05f, FractalOctaves = 3 } };

    /// <summary>Native Terrain3D clipmaps render the existing valley, with its flat playable floor and mountain heights.</summary>
    private void BuildTerrain3D(Node3D parent, Texture2D grass) {
        const string extension = "res://addons/terrain_3d/terrain.gdextension";
        if (!FileAccess.FileExists(extension)) throw new InvalidOperationException("Terrain3D is missing. Launch with ./build.py setup to install the pinned runtime.");
        var loaded = GDExtensionManager.LoadExtension(extension);
        if (loaded is not (GDExtensionManager.LoadStatus.Ok or GDExtensionManager.LoadStatus.AlreadyLoaded))
            throw new InvalidOperationException($"Terrain3D extension could not load: {loaded}");
        var terrain = (Node3D)ClassDB.Instantiate("Terrain3D").AsGodotObject()!;
        terrain.Name = "Landscape";
        terrainTarget = new Node3D { Name = "TerrainDetailTarget", Position = To3(World.Player) };
        parent.AddChild(terrainTarget);
        terrain.Set("clipmap_target", terrainTarget);
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
                        samples[z * regionSize + x] = Math.Max(0, MountainShape.Height(new Vector2(originX + x, originZ + z)) - .03f);
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
        foreach (var p in new[] { new Vector2(-20, 80), new Vector2(70, 30), new Vector2(260, 150) }) {
            float expected = Math.Max(0, MountainShape.Height(p) - .03f), actual = data.Call("get_height", To3(p)).AsSingle();
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
        terrainMaterialRid = material.Call("get_material_rid").AsRid();
        terrainGrass = grass;
        RenderingServer.MaterialSetParam(terrainMaterialRid, "grass", grass.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "patches", terrainPatches.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "disturbance", disturbanceTexture.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "shade", sceneryShadeTexture.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "contact", objectShadeTexture.GetRid());
        // The shader's tunnel cut-outs hold exactly one entry per cliff.
        if (World.Cliffs.Length != 5) throw new InvalidOperationException($"Terrain3D mine cut-outs expect 5 cliffs, not {World.Cliffs.Length}.");
        var mines = new Vector4[World.Cliffs.Length];
        for (int i = 0; i < mines.Length; i++) {
            var cliff = World.Cliffs[i]; var at = World.MineSpot(cliff);
            mines[i] = new Vector4(at.X, at.Y, cliff.Face.X, cliff.Face.Y);
        }
        RenderingServer.MaterialSetParam(terrainMaterialRid, "mines", mines);
        GD.Print($"TERRAIN3D: v1.1.0-rc1, 9 regions, {regionSize}² samples/region; playable floor {floor:0.000}");
    }

    /// <summary>Keeps Terrain3D's finest detail under the view.</summary>
    private void UpdateTerrainTarget() {
        if (terrainTarget != null) terrainTarget.Position = To3(EnvironmentReview ? EnvironmentCenter ?? World.Player : World.Player);
    }
}
