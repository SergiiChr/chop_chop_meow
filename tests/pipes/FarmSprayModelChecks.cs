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
        var ribbon = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var outlets = new HashSet<int>();
        for (int i = 0; i < vertices.Length; i++) {
            if (ribbon[i].X != 0) continue;
            var origin = vertices[i];
            Require(Mathf.Abs(origin.Z) < .02f && Mathf.Abs(origin.X) is > 1.3f and < 1.5f && origin.Y > 1,
                "The water should start at each end of the T above the crops.");
            outlets.Add(Math.Sign(origin.X));
        }
        Require(outlets.Count == 2, "Both ends of the T should spray.");
        Require(spray.CustomAabb.Size.X > 5.5f && spray.CustomAabb.Size.Z > 5.5f && spray.CustomAabb.End.Y > 1.4f,
            "The sprinkler should cover the planted rows and keep the arcs above the heads visible.");

        foreach (var mesh in ModelSteps.AllMeshes(spray.GetParent())) {
            var bounds = mesh.Transform * (mesh == spray ? spray.CustomAabb : mesh.Mesh!.GetAabb());
            if (mesh != spray)
                Require(bounds.Position.Z > -.16f && bounds.End.Z < .16f, "The central T should leave both sides of the plot free of framing.");
            Require(bounds.Position.X >= -3 && bounds.Position.Z >= -3 && bounds.End.X <= 3 && bounds.End.Z <= 3,
                "The sprinkler and its spray should fit inside the farm's six-meter square.");
        }
        plumbing.Water(false, .6f);
        Require(!spray.Visible, "The sprinklers should stop when flow stops.");
        farm.Root.Free();
    }
}
