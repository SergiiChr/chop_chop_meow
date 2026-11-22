using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Rotating fans of drops irrigate the four equal squares of soil under a sprinkler frame.</summary>
internal static class FarmSpray {
    private static readonly StringName SprayTime = "spray_time";
    private static Mesh? drops;
    private static Material? water;

    public static MeshInstance3D Build(Node3D parent, ReadOnlySpan<Vector3> nozzles, float quadrantExtent) {
        var spray = Add(parent, drops ??= Drops(nozzles), water ??= Water(quadrantExtent), Vector3.Zero);
        float edge = quadrantExtent * 2;
        spray.Name = "FarmSpray";
        spray.CustomAabb = new Aabb(new Vector3(-edge, 0, -edge), new Vector3(edge * 2, nozzles[0].Y, edge * 2));
        spray.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        spray.Visible = false;
        return spray;
    }

    public static void SetTime(MeshInstance3D spray, float elapsed) => spray.SetInstanceShaderParameter(SprayTime, elapsed);

    private static ArrayMesh Drops(ReadOnlySpan<Vector3> nozzles) {
        const int Jets = 4, Depths = 3;
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        for (int head = 0; head < nozzles.Length; head++)
            for (int jet = 0; jet < Jets; jet++)
                for (int depth = 0; depth < Depths; depth++) {
                    var nozzle = nozzles[head];
                    var timing = new Vector2(jet / (float)Jets + head * .073f + depth * .035f, depth / (float)Depths + jet * .14f + head * .11f);
                    float range = Mathf.Sqrt((depth + 1f) / Depths);
                    void Point(float lead, float across) {
                        mesh.SetColor(new Color(range, 0, 0));
                        mesh.SetUV(timing);
                        mesh.SetUV2(new Vector2(lead, across));
                        mesh.AddVertex(nozzle + new Vector3(lead, across, 0));
                    }
                    Point(-.082f, 0); Point(.038f, -.015f); Point(.038f, .015f);
                }
        return mesh.Commit();
    }

    private static ShaderMaterial Water(float quadrantExtent) {
        var material = new ShaderMaterial {
            Shader = new Shader { Code = """
                shader_type spatial;
                render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never;
                instance uniform float spray_time;
                uniform float quadrant_extent;
                varying float flight;

                vec2 square_edge(float phase) {
                    float side = phase * 4.0;
                    float along = fract(side) * 2.0 - 1.0;
                    if (side < 1.0) return vec2(1.0, along);
                    if (side < 2.0) return vec2(-along, 1.0);
                    if (side < 3.0) return vec2(-1.0, -along);
                    return vec2(along, -1.0);
                }

                void vertex() {
                    flight = fract(spray_time * 1.65 + UV.y);
                    float travel = clamp(flight + UV2.x, 0.0, 1.0);
                    vec2 edge = square_edge(fract(UV.x + spray_time * 0.19));
                    vec2 across = normalize(vec2(-edge.y, edge.x));
                    float width = min(1.0, 5.0 * min(travel, 1.0 - travel));
                    VERTEX.xz += edge * (quadrant_extent * COLOR.r * travel) + across * (UV2.y * width) - vec2(UV2.x, 0.0);
                    VERTEX.y = mix(VERTEX.y - UV2.y, 0.08, travel) + 0.38 * travel * (1.0 - travel);
                }

                void fragment() {
                    ALBEDO = vec3(0.59, 0.83, 0.96);
                    ALPHA = 0.89 * smoothstep(0.0, 0.08, flight) * (1.0 - smoothstep(0.91, 1.0, flight));
                }
                """ }
        };
        material.SetShaderParameter("quadrant_extent", quadrantExtent);
        return material;
    }
}
