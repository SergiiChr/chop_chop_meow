using System.Linq;
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
        Require(farm.Body.FindChild("FarmSpray", true, false) is MeshInstance3D, "A plumbed farm should have a spray mesh.");
        var spray = (MeshInstance3D)farm.Body.FindChild("FarmSpray", true, false)!;
        Require(!spray.Visible, "A plumbed farm should start with its spray off.");
        plumbing.Water(true, .5f);
        Require(spray.Visible, "A receiving farm should show its spray.");
        int triangles = ModelSteps.Triangles(farm.Root);
        Require(triangles <= 2500, $"The fitted mature farm with active spray must stay within 2,500 triangles, got {triangles}.");

        var arrays = spray.Mesh!.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var sprinklerMeshes = ModelSteps.AllMeshes(spray.GetParent()).Where(mesh => mesh != spray)
            .Select(mesh => mesh.Transform * mesh.Mesh!.GetAabb()).ToArray();
        foreach (float x in new[] { -1.425f, 1.425f }) {
            foreach (float z in new[] { -1.425f, 1.425f }) {
                var center = new Vector3(x, 0, z);
                var head = sprinklerMeshes.Where(bounds => (bounds.GetCenter() with { Y = 0 }).DistanceTo(center) < .12f).ToArray();
                Require(head.Length > 0, "Each quarter of the farm should have a sprinkler in its center.");
                float top = head.Max(bounds => bounds.End.Y);
                Require(top is > .12f and < .32f, "Each sprinkler should stick just above the tilled soil.");
                Require(vertices.Any(v => v.DistanceTo(center with { Y = top }) < .015f),
                    "Spray should start at the top of each sprinkler.");
            }
        }
        foreach (var bounds in sprinklerMeshes)
            Require(Mathf.Abs(Mathf.Abs(bounds.GetCenter().X) - 1.425f) < .12f
                && Mathf.Abs(Mathf.Abs(bounds.GetCenter().Z) - 1.425f) < .12f
                && bounds.Size.X < .25f && bounds.Size.Z < .25f,
                "Only the four short sprinkler heads should be visible above the bed.");
        var surfaceOnSoil = spray.Mesh.GetFaces().Chunk(3).Any(face =>
            face.All(v => v.Y is >= .02f and < .12f) && face[0].DistanceTo(face[1]) > .7f);
        Require(!surfaceOnSoil, "The watering effect should leave the tilled soil clear of surface ripples.");
        foreach (var vertex in vertices) {
            var fromNearestHead = new Vector2(Mathf.Abs(vertex.X) - 1.425f, Mathf.Abs(vertex.Z) - 1.425f);
            Require(fromNearestHead.Length() <= 1.44f, "Each sprinkler fan should fit its circular reach.");
        }
        var sheet = spray.CustomAabb.HasVolume() ? spray.CustomAabb : spray.Mesh.GetAabb();
        Require(sheet.Size.X is > 5.4f and <= 6 && sheet.Size.Z is > 5.4f and <= 6 && sheet.End.Y > .6f,
            "The water should fan up from the heads and reach the outer rows of the plot.");
        plumbing.Water(false, .6f);
        Require(!spray.Visible, "The sprinklers should stop when flow stops.");
        farm.Root.Free();
    }
}
