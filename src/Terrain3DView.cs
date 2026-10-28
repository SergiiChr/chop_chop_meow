using System;
using System.Runtime.InteropServices;
using Godot;

namespace ChopChopMeow;

public partial class WorldView {
    private Rid terrainMaterialRid;
    private Node3D? terrainTarget;
    // RenderingServer uniforms take RIDs, so retain the texture resource for their lifetime.
    private Texture2D? terrainGrass;
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
        terrain.Set("collision_mode", 0); // Simulation still owns movement/building rules on the level valley floor.
        terrain.Call("set_camera", camera);
        parent.AddChild(terrain);
        var data = terrain.Get("data").AsGodotObject()!;
        const int regionSize = 256;
        var samples = new float[regionSize * regionSize];
        var bytes = new byte[samples.Length * sizeof(float)];
        // Padding regions cover both ranges beyond the 216-tile map, avoiding a visible terrain boundary.
        for (int rz = -1; rz <= 1; rz++)
            for (int rx = -1; rx <= 1; rx++) {
                for (int z = 0; z < regionSize; z++)
                    for (int x = 0; x < regionSize; x++) {
                        var p = new Vector2(rx * regionSize + x, rz * regionSize + z);
                        samples[z * regionSize + x] = Math.Max(0, MountainShape.Height(p) - .03f);
                    }
                MemoryMarshal.AsBytes(samples.AsSpan()).CopyTo(bytes);
                using var heights = Image.CreateFromData(regionSize, regionSize, false, Image.Format.Rf, bytes);
                var region = data.Call("add_region_blank", new Vector2I(rx, rz), false).AsGodotObject()!;
                region.Call("set_height_map", heights);
                region.Call("calc_height_range");
            }
        data.Call("update_maps");
        float floor = data.Call("get_height", To3(World.Spawn)).AsSingle();
        if (!float.IsFinite(floor) || Math.Abs(floor) > .001f) throw new InvalidOperationException($"Terrain3D playable floor moved to {floor}");
        foreach (var p in new[] { new Vector2(-20, 80), new Vector2(70, 30), new Vector2(260, 150) }) {
            float expected = Math.Max(0, MountainShape.Height(p) - .03f), actual = data.Call("get_height", To3(p)).AsSingle();
            if (!float.IsFinite(actual) || Math.Abs(actual - expected) > .001f)
                throw new InvalidOperationException($"Terrain3D region height mismatch at {p}: {actual} instead of {expected}");
        }

        var material = terrain.Get("material").AsGodotObject()!;
        material.Set("show_checkered", false);
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
        RenderingServer.MaterialSetParam(terrainMaterialRid, "trial_grass", grass.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "trial_patches", terrainPatches.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "trial_disturbance", disturbanceTexture.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "trial_shade", sceneryShadeTexture.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterialRid, "trial_contact", objectShadeTexture.GetRid());
        var mines = new Vector4[World.Cliffs.Length];
        for (int i = 0; i < mines.Length; i++) {
            var cliff = World.Cliffs[i]; var at = World.MineSpot(cliff);
            mines[i] = new Vector4(at.X, at.Y, cliff.Face.X, cliff.Face.Y);
        }
        RenderingServer.MaterialSetParam(terrainMaterialRid, "trial_mines", mines);
        GD.Print($"TERRAIN3D: v1.1.0-rc1, 9 regions, {regionSize}² samples/region; playable floor {floor:0.000}");
    }

    /// <summary>Keeps Terrain3D's finest detail under the view.</summary>
    private void UpdateTerrainTarget() {
        if (terrainTarget != null) terrainTarget.Position = To3(EnvironmentReview ? EnvironmentCenter ?? World.Player : World.Player);
    }
}
