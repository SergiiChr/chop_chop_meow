using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Each low head throws a crown of water and loose drops onto the soil.</summary>
internal static class FarmSpray {
    private static readonly StringName SprayTime = "spray_time";
    private static ArrayMesh? splash;
    private static ShaderMaterial? water;

    public static MeshInstance3D Build(Node3D parent, ReadOnlySpan<Vector3> nozzles, float reach) {
        splash ??= Splash(nozzles, reach);
        water ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/environment/sprinkler/splash.gdshader") };
        var spray = Add(parent, splash, water, Vector3.Zero);
        spray.Name = "FarmSpray";
        var bounds = splash.GetAabb().Expand(Vector3.Zero);
        spray.CustomAabb = bounds.Expand(new Vector3(0, nozzles[0].Y + 1.18f, 0)).Grow(.06f);
        spray.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        spray.Visible = false;
        return spray;
    }

    public static void SetTime(MeshInstance3D spray, float elapsed) => spray.SetInstanceShaderParameter(SprayTime, elapsed);

    private static ArrayMesh Splash(ReadOnlySpan<Vector3> nozzles, float reach) {
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var head in nozzles) {
            mesh.SetUV2(new Vector2(head.X, head.Z));
            // The shader uses vertex color to distinguish the crown from the airborne drops.
            mesh.SetColor(Colors.Black);
            for (int i = 0; i < 8; i++) {
                Vertex(Vector2.Zero, Vector2.Zero, head.Y);
                for (int j = i; j <= i + 1; j++) {
                    var rim = Vector2.FromAngle(j * Mathf.Tau / 8);
                    Vertex(rim, rim * .61f, head.Y);
                }
            }
            mesh.SetColor(Colors.White);
            for (int i = 0; i < 10; i++) {
                var landing = Vector2.FromAngle(i * Mathf.Tau / 10) * reach;
                // The shader opens this triangle towards the camera around a moving cluster of drops.
                Vertex(new Vector2(-1, -1), landing, head.Y);
                Vertex(new Vector2(3, -1), landing, head.Y);
                Vertex(new Vector2(-1, 3), landing, head.Y);
            }

            void Vertex(Vector2 uv, Vector2 offset, float y) {
                mesh.SetUV(uv);
                mesh.AddVertex(new Vector3(head.X + offset.X, y, head.Z + offset.Y));
            }
        }
        return mesh.Commit();
    }
}
