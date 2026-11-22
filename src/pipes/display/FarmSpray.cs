using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Two sprinkler heads sweep arcing jets across their halves of the plot.</summary>
internal static class FarmSpray {
    private static readonly StringName SprayTime = "spray_time";
    private static Mesh? jets;
    private static Material? water;

    public static MeshInstance3D Build(Node3D parent, ReadOnlySpan<Vector3> nozzles, float bedHalf) {
        var spray = Add(parent, jets ??= Jets(nozzles), water ??= Water(bedHalf), Vector3.Zero);
        spray.Name = "FarmSpray";
        spray.CustomAabb = new Aabb(new Vector3(-bedHalf - .04f, 0, -bedHalf - .04f),
            new Vector3(2 * bedHalf + .08f, nozzles[0].Y + .65f, 2 * bedHalf + .08f));
        spray.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        spray.Visible = false;
        return spray;
    }

    public static void SetTime(MeshInstance3D spray, float elapsed) => spray.SetInstanceShaderParameter(SprayTime, elapsed);

    private static ArrayMesh Jets(ReadOnlySpan<Vector3> nozzles) {
        const int Segments = 7;
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var nozzle in nozzles) {
            for (int jet = 0; jet < 3; jet++) {
                var aim = new Vector2(jet / 3f + (nozzle.X > 0 ? .125f : 0), jet == 2 ? .55f : 1);
                for (int i = 0; i < Segments; i++) {
                    float a = i / (float)Segments, b = (i + 1) / (float)Segments;
                    Corner(a, -1); Corner(a, 1); Corner(b, 1);
                    Corner(a, -1); Corner(b, 1); Corner(b, -1);
                }

                void Corner(float along, float side) {
                    mesh.SetUV(new Vector2(along, side));
                    mesh.SetUV2(aim);
                    mesh.AddVertex(nozzle + new Vector3(along, side * .025f, 0));
                }
            }
        }
        return mesh.Commit();
    }

    private static ShaderMaterial Water(float bedHalf) {
        var material = new ShaderMaterial {
            Shader = new Shader { Code = """
                shader_type spatial;
                render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never;
                instance uniform float spray_time;
                uniform float bed_half;
                varying vec2 ribbon;
                varying float phase;

                // A turn traces the perimeter of one half-bed, reaching the corners as well as the sides.
                vec2 edge(float turn) {
                    float side = fract(turn) * 4.0;
                    float along = fract(side) * 2.0 - 1.0;
                    if (side < 1.0) return vec2(1.0, along);
                    if (side < 2.0) return vec2(-along, 1.0);
                    if (side < 3.0) return vec2(-1.0, -along);
                    return vec2(along, -1.0);
                }

                void vertex() {
                    ribbon = UV;
                    phase = UV2.x;
                    float travel = UV.x;
                    vec3 nozzle = VERTEX - vec3(travel, UV.y * .025, 0.0);
                    // Water already in the air keeps the heading the nozzle had when it left.
                    vec2 reach = edge(phase + (spray_time - travel * .46) * .15)
                        * vec2(bed_half * .5, bed_half) * UV2.y;
                    float lift = 3.30 * UV2.y;
                    vec3 landing = vec3(reach.x, .08 - nozzle.y, reach.y);
                    vec3 at = nozzle + landing * travel;
                    at.y += lift * travel * (1.0 - travel);
                    vec3 tangent = landing + vec3(0.0, lift * (1.0 - 2.0 * travel), 0.0);
                    vec2 direction = (mat3(MODELVIEW_MATRIX) * tangent).xy;
                    vec2 across = vec2(-direction.y, direction.x) / max(length(direction), .001);
                    vec4 view = MODELVIEW_MATRIX * vec4(at, 1.0);
                    float pixel = 2.0 / (VIEWPORT_SIZE.y * abs(PROJECTION_MATRIX[1][1]));
                    view.xy += across * UV.y * max(mix(.017, .032, travel), .8 * pixel);
                    POSITION = PROJECTION_MATRIX * view;
                }

                void fragment() {
                    // A smooth jet at the spout separates into bright, travelling beads as it falls.
                    float bead = .5 + .5 * cos(TAU * (ribbon.x * 7.0 - spray_time * 4.0 + phase));
                    float broken = smoothstep(.12, .68, ribbon.x);
                    float core = 1.0 - smoothstep(.18, 1.0, abs(ribbon.y));
                    ALBEDO = mix(vec3(.22, .59, .78), vec3(.88, .97, 1.0), core * (.55 + .45 * bead));
                    ALPHA = (.76 + .20 * core) * mix(1.0, smoothstep(.24, .70, bead), broken)
                        * (1.0 - smoothstep(.94, 1.0, ribbon.x));
                }
                """ }
        };
        material.SetShaderParameter("bed_half", bedHalf);
        return material;
    }
}
