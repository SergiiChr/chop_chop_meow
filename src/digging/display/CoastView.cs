using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The sea surface, wet sand and bare soil left behind by a shovel.</summary>
internal sealed class CoastView {
    private static readonly StringName Tide = "coastal_tide", LowShift = "low_tide_shift";
    private const float LowLevel = .018f, HighLevel = .5f;
    // The first corner is at (-0.5, -0.5), half a tile before the first tile centre.
    private const int CornerMapSize = Terrain.Size + 1;
    private readonly ShaderMaterial bank, waves;
    private readonly Node3D water;
    private readonly byte[] corners = new byte[CornerMapSize * CornerMapSize];
    private readonly Image cornerImage;
    private readonly ImageTexture cornerTexture;
    private readonly MultiMesh soil;
    private readonly HashSet<int> stripped = [];
    private int gravelRevision = -1;
    private float lastTide = -1;

    internal CoastView(Node3D parent, Node3D warm, Node3D water, ShaderMaterial bank, ShaderMaterial waves) {
        this.water = water;
        this.bank = bank;
        this.waves = waves;

        cornerImage = Image.CreateFromData(CornerMapSize, CornerMapSize, false, Image.Format.R8, corners);
        cornerTexture = ImageTexture.CreateFromImage(cornerImage);
        var tile = SandCut(cornerTexture);
        int count = 0;
        for (int z = 1; z < Terrain.Size - 1; z++)
            for (int x = 1; x < Terrain.Size - 1; x++)
                if (Beach.IsSand(new Vector2(x, z))) count++;
        soil = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = tile, InstanceCount = count, VisibleInstanceCount = 0 };
        parent.AddChild(new MultiMeshInstance3D { Multimesh = soil, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        warm.AddChild(new MultiMeshInstance3D {
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = tile, InstanceCount = 1 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private static PlaneMesh SandCut(ImageTexture cornerTexture) {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/environment/lake/sand_cut.gdshader") };
        material.SetShaderParameter("cut_corners", cornerTexture);
        material.SetShaderParameter("sand_albedo", ShorelineAppearance.Map(ShoreLook.Sand, "albedo"));
        material.SetShaderParameter("sand_tile", ShoreLook.Sand.Tile);
        material.SetShaderParameter("soil_albedo", ModelKit.ScanMap(Scan.Soil, "albedo"));
        material.SetShaderParameter("soil_normal", ModelKit.ScanMap(Scan.Soil, "bump"));
        material.SetShaderParameter("soil_tile", ModelKit.ScanTile);
        return new PlaneMesh { Size = Vector2.One, Material = material };
    }

    internal void Update(Beach beach, GravelPaths paths, TerrainView terrain) {
        float tide = beach.Tide;
        if (lastTide < 0) {
            bank.SetShaderParameter(LowShift, Terrain.LowTideShift);
            waves.SetShaderParameter(LowShift, Terrain.LowTideShift);
        }
        if (lastTide != tide) {
            lastTide = tide;
            bank.SetShaderParameter(Tide, tide);
            waves.SetShaderParameter(Tide, tide);
            terrain.SetCoastalTide(tide);
            water.Position = Vector3.Up * Mathf.Lerp(LowLevel, HighLevel, tide);
        }
        var squares = beach.StrippedSquares;
        int currentGravel = paths.Revision;
        if (gravelRevision == currentGravel && stripped.SetEquals(squares)) return;
        gravelRevision = currentGravel;
        stripped.Clear(); stripped.UnionWith(squares);
        Array.Clear(corners);
        int slot = 0;
        foreach (int index in squares) {
            int x = index % Terrain.Size, z = index / Terrain.Size;
            if (paths.Laid(x, z)) continue;
            int corner = z * CornerMapSize + x;
            // At each corner the four tile bits go across x, then down z.
            corners[corner] |= 8;
            corners[corner + 1] |= 4;
            corners[corner + CornerMapSize] |= 2;
            corners[corner + CornerMapSize + 1] |= 1;
            soil.SetInstanceTransform(slot++, new Transform3D(Basis.Identity, new Vector3(x, .016f, z)));
        }
        cornerImage.SetData(CornerMapSize, CornerMapSize, false, Image.Format.R8, corners);
        cornerTexture.Update(cornerImage);
        soil.VisibleInstanceCount = slot;
    }
}
