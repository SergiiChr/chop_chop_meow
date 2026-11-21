using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A few short arcs of drops sweep from two fixed irrigation nozzles while water is supplied.</summary>
internal static class FarmSpray {
    private static readonly StringName SprayTime = "spray_time";
    private static Mesh? drops;
    private static Material? water;

    public static MeshInstance3D Build(Node3D parent, Vector3 left, Vector3 right) {
        var spray = Add(parent, drops ??= Drops(left, right), water ??= Water(), Vector3.Zero);
        spray.CustomAabb = new Aabb(new Vector3(-3, 0, -2.6f), new Vector3(6, 1.6f, 5.2f));
        spray.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        spray.Visible = false;
        return spray;
    }

    public static void SetTime(MeshInstance3D spray, float elapsed) => spray.SetInstanceShaderParameter(SprayTime, elapsed);

    private static ArrayMesh Drops(Vector3 left, Vector3 right) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var nozzle in new[] { left, right })
            for (int jet = 0; jet < 3; jet++)
                for (int drop = 0; drop < 6; drop++) {
                    float delay = drop / 6f + jet * .038f;
                    var timing = new Vector2(jet / 3f + nozzle.X * .1f, delay);
                    void Point(float forward, float up) {
                        s.SetUV(timing);
                        s.SetUV2(new Vector2(forward, up));
                        s.AddVertex(nozzle + new Vector3(forward, up, 0));
                    }
                    Point(-.053f, -.012f); Point(.046f, .018f); Point(.046f, -.004f);
                    Point(-.053f, -.012f); Point(-.053f, .006f); Point(.046f, .018f);
                }
        return s.Commit();
    }

    private static ShaderMaterial Water() => new() {
        Shader = new Shader { Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never;
            instance uniform float spray_time;
            varying float flight;

            void vertex() {
                flight = fract(spray_time * 1.5 + UV.y);
                float angle = TAU * (UV.x + spray_time * 0.28);
                vec2 heading = vec2(cos(angle), sin(angle));
                float reach = 1.19 + 0.12 * sin(UV.x * 35.0);
                float drift = flight + UV2.x;
                VERTEX.xz += heading * vec2(reach, reach * 1.75) * drift - vec2(UV2.x, 0.0);
                VERTEX.y += drift * (0.71 - 1.81 * drift);
            }

            void fragment() {
                ALBEDO = vec3(0.36, 0.69, 0.88);
                ALPHA = 0.87 * smoothstep(0.0, 0.08, flight) * (1.0 - smoothstep(0.86, 1.0, flight));
            }
            """ }
    };
}
