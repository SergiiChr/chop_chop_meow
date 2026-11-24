using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The sea surface, wet sand and bare soil left behind by a shovel.</summary>
internal sealed class CoastView {
    private static readonly StringName Tide = "coastal_tide", LowShift = "low_tide_shift";
    private const float LowLevel = .018f, HighLevel = .5f;
    private readonly ShaderMaterial bank, waves;
    private readonly Node3D water;
    private readonly MultiMesh soil;
    private readonly HashSet<int> drawn = [];
    private float lastTide = -1;

    internal CoastView(Node3D parent, Node3D warm, Node3D water, ShaderMaterial bank, ShaderMaterial waves) {
        this.water = water;
        this.bank = bank;
        this.waves = waves;

        var tile = new PlaneMesh { Size = Vector2.One, Material = ModelKit.Scanned(Scan.Soil, 1) };
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

    internal void Update(Beach beach, TerrainView terrain) {
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
        if (drawn.SetEquals(squares)) return;
        drawn.Clear(); drawn.UnionWith(squares);
        int slot = 0;
        foreach (int index in squares) {
            var at = Beach.Position(index);
            soil.SetInstanceTransform(slot++, new Transform3D(Basis.Identity, new Vector3(at.X, .016f, at.Y)));
        }
        soil.VisibleInstanceCount = slot;
    }
}
