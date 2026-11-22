using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;

public static class FarmSprayModelChecks {
    public static void Run() {
        var farm = (FarmProp)WorldModels.Build(new Thing { Id = 1, Kind = ThingKind.Farm });
        farm.Crops[0].Visible = farm.Crops[3].Visible = true;
        farm.Crops[1].Free(); farm.Crops[2].Free();
        var plumbing = farm.Plumbing!;
        plumbing.Show(true);
        Require(farm.Body.FindChild("FarmSpray", true, false) is MeshInstance3D, "An equipped farm should have a spray mesh.");
        var spray = (MeshInstance3D)farm.Body.FindChild("FarmSpray", true, false)!;
        Require(!spray.Visible, "An equipped farm should start with its spray off.");
        plumbing.Water(true, .5f);
        Require(spray.Visible, "A receiving farm should show its spray.");
        int triangles = ModelSteps.Triangles(farm.Root);
        Require(triangles <= 2500, $"The fitted mature farm with active spray must stay within 2,500 triangles, got {triangles}.");

        var arrays = spray.Mesh!.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var offsets = arrays[(int)Mesh.ArrayType.TexUV2].AsVector2Array();
        var quadrants = new HashSet<Vector2I>();
        for (int i = 0; i < vertices.Length; i++) {
            var origin = new Vector2(vertices[i].X - offsets[i].X, vertices[i].Z);
            Require(Mathf.IsEqualApprox(Mathf.Abs(origin.X), 1.425f) && Mathf.IsEqualApprox(Mathf.Abs(origin.Y), 1.425f),
                "The spray should start in the center of each cropped quadrant.");
            quadrants.Add(new Vector2I(Math.Sign(origin.X), Math.Sign(origin.Y)));
        }
        Require(quadrants.Count == 4, "The farm needs one head in each quadrant.");

        foreach (var mesh in ModelSteps.AllMeshes(spray.GetParent())) {
            var bounds = mesh.Transform * (mesh == spray ? spray.CustomAabb : mesh.Mesh!.GetAabb());
            Require(bounds.Position.X >= -3 && bounds.Position.Z >= -3 && bounds.End.X <= 3 && bounds.End.Z <= 3,
                "The equipped sprinkler frame and spray should fit inside the farm's six-meter square.");
        }
        plumbing.Water(false, .6f);
        Require(!spray.Visible, "The sprinklers should stop when flow stops.");
        farm.Root.Free();
    }
}
