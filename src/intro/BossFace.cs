using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>The boss's mouth and face in the film, including the teeth that come loose in the close-up.</summary>
internal sealed class BossFace {
    public const int ToothCount = 4;
    private const float GrimaceRadius = .036f;
    private static readonly Vector3 Centre = new(0, .13f, 0);
    private static readonly StringName Tint = "tint", Roughness = "microroughness", Crease = "crease",
        AcrossCrease = "across_crease", Outward = "face_out", Fold = "fold";
    private readonly ShaderMaterial skin = new() {
        Shader = new Shader { Code = """
            shader_type spatial;
            render_mode world_vertex_coords;
            instance uniform vec4 tint : source_color = vec4(1.0);
            instance uniform float microroughness = 0.85;
            uniform vec3 crease;
            uniform vec3 across_crease;
            uniform vec3 face_out;
            uniform float fold = 0.0;

            // Pinch the middle of the cheek away from the barrel and bring both lips of the crease around it.
            vec3 wrap(vec3 point) {
                vec3 from_crease = point - crease;
                float height = abs(dot(from_crease, across_crease));
                float side = smoothstep(-0.115, 0.053, dot(from_crease, face_out));
                float dent = 0.080 * (1.0 - smoothstep(0.009, 0.073, height));
                float swell = 0.027 * smoothstep(0.022, 0.064, height) * (1.0 - smoothstep(0.082, 0.126, height));
                return point + face_out * (swell - dent) * side * fold;
            }
            void vertex() {
                if (fold > 0.0) {
                    vec3 around = abs(NORMAL.y) < 0.95 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
                    vec3 right = normalize(cross(around, NORMAL));
                    vec3 up = cross(NORMAL, right);
                    vec3 bent = wrap(VERTEX);
                    NORMAL = normalize(cross(wrap(VERTEX + 0.002 * right) - bent, wrap(VERTEX + 0.002 * up) - bent));
                    VERTEX = bent;
                }
            }
            void fragment() {
                ALBEDO = tint.rgb;
                ROUGHNESS = microroughness;
            }
            """ },
    };
    private readonly List<(MeshInstance3D Part, Vector3[] Surface)> contactSurfaces = [];
    private readonly Node3D face, mouth;
    private Vector3 creaseInHead = Centre, acrossCreaseInHead = Vector3.Up, outwardInHead = Vector3.Back;
    private Vector3 crease, acrossCrease, outward;
    private float fold;
    private readonly MeshInstance3D yell, grimace;
    private readonly FilmFace expression;
    private readonly MeshInstance3D[] teeth = new MeshInstance3D[ToothCount];

    public BossFace(ActorRig boss) {
        expression = new FilmFace(boss, ActorSkin.Boss, showMouth: false);
        var head = boss.Head;
        var parts = head.GetChildren();
        face = Pivot(head, Vector3.Zero);
        foreach (var part in parts) part.Reparent(face, false);
        mouth = Pivot(face, new Vector3(0, .066f, .112f));
        var inside = Mat("3a1512", .9f);
        yell = Sphere(mouth, .031f, Vector3.Zero, inside, new Vector3(1, .64f, .23f), 16);
        grimace = Sphere(mouth, GrimaceRadius, new Vector3(0, 0, -.006f), inside, new Vector3(1, .49f, .25f), 12);
        grimace.Hide();
        foreach (var part in parts) {
            if (part is MeshInstance3D { Mesh: SphereMesh { Radius: .12f } sphere } headMesh)
                headMesh.Mesh = new SphereMesh { Radius = sphere.Radius, Height = sphere.Height, RadialSegments = 32, Rings = 22 };
        }
        FitCreaseMaterial(face);
        var ivory = new Color("f1e5c9");
        ActorShape.Ring[] incisor = [
            new(.014f, .0014f, .0015f, ivory, -.0025f), new(.005f, .0045f, .0036f, ivory, -.001f),
            new(0, .0068f, .0044f, ivory), new(-.0063f, .0066f, .0034f, ivory, .0008f),
            new(-.008f, .0052f, .0018f, ivory, .001f)
        ];
        for (int i = 0; i < teeth.Length; i++) {
            var tooth = ActorShape.Add(mouth, incisor);
            tooth.Hide();
            teeth[i] = tooth;
        }
    }

    private void FitCreaseMaterial(Node3D at) {
        if (at is MeshInstance3D { MaterialOverride: StandardMaterial3D paint } mesh) {
            mesh.MaterialOverride = skin;
            mesh.SetInstanceShaderParameter(Tint, paint.AlbedoColor);
            mesh.SetInstanceShaderParameter(Roughness, paint.Roughness);
            mesh.CustomAabb = mesh.Mesh.GetAabb().Grow(.15f);
            contactSurfaces.Add((mesh, mesh.Mesh.GetFaces()));
        }
        foreach (var child in at.GetChildren())
            if (child is Node3D part) FitCreaseMaterial(part);
    }

    /// <summary>Poses the crease across the line of the barrel as it slides.</summary>
    public void SetCrease(Vector3 contact, Vector3 axis, Vector3 normal) {
        var inverse = face.GlobalBasis.Inverse();
        outwardInHead = (inverse * normal).Normalized();
        acrossCreaseInHead = (inverse * axis.Cross(normal)).Normalized();
        var pointInHead = face.ToLocal(contact);
        creaseInHead = pointInHead - outwardInHead * (pointInHead - Centre).Dot(outwardInHead);
    }

    /// <summary>The mouth anchors the camera before the crease displaces its surface.</summary>
    public Vector3 MouthPosition => mouth.GlobalPosition;

    /// <summary>Places the barrel's inner surface against the drawn, deformed face.</summary>
    public float BarrelOffset(Vector3 grip, Vector3 along, Vector3 outward) {
        var across = along.Cross(outward);
        float contact = float.NegativeInfinity;
        foreach (var (part, surface) in contactSurfaces) {
            if (!part.IsVisibleInTree()) continue;
            var transform = part.GlobalTransform;
            foreach (var vertex in surface) {
                var fromGrip = Wrap(transform * vertex) - grip;
                float axial = fromGrip.Dot(along);
                float station = Mathf.Clamp(axial, IntroSets.BatBarrelStart, IntroSets.BatBarrelEnd);
                float taper = (station - IntroSets.BatBarrelStart) / (IntroSets.BatBarrelEnd - IntroSets.BatBarrelStart);
                float radius = Mathf.Lerp(IntroSets.BatBarrelNarrowRadius, IntroSets.BatBarrelWideRadius, taper) + .002f;
                float side = fromGrip.Dot(across);
                float beyond = axial - station;
                float heightSquared = radius * radius - side * side - beyond * beyond;
                if (heightSquared > 0) contact = Mathf.Max(contact, fromGrip.Dot(outward) + Mathf.Sqrt(heightSquared));
            }
        }
        return contact;
    }

    private Vector3 Wrap(Vector3 position) {
        if (fold == 0) return position;
        // Match the shader so the mouth and teeth follow the same crease as their drawn meshes.
        var fromCrease = position - crease;
        float height = Mathf.Abs(fromCrease.Dot(acrossCrease));
        float side = Mathf.SmoothStep(-.115f, .053f, fromCrease.Dot(outward));
        float dent = .080f * (1 - Mathf.SmoothStep(.009f, .073f, height));
        float swell = .027f * Mathf.SmoothStep(.022f, .064f, height) * (1 - Mathf.SmoothStep(.082f, .126f, height));
        return position + outward * ((swell - dent) * side * fold);
    }

    public void OpenMouth(float width, float height, float worry) {
        mouth.Scale = new Vector3(width, height, 1);
        expression.Look(.98f + height * .14f, -.3f, worry);
    }

    /// <summary>Pinches the cheek between the forehead and jaw, then eases the crease out as the head recoils.</summary>
    public void Deform(float squash, float recoil) {
        fold = squash;
        crease = face.ToGlobal(creaseInHead);
        acrossCrease = (face.GlobalBasis * acrossCreaseInHead).Normalized();
        outward = (face.GlobalBasis * outwardInHead).Normalized();
        skin.SetShaderParameter(Crease, crease);
        skin.SetShaderParameter(AcrossCrease, acrossCrease);
        skin.SetShaderParameter(Outward, outward);
        skin.SetShaderParameter(Fold, fold);
        yell.Hide();
        grimace.Show();
        mouth.Position = new Vector3(-.011f * squash, .072f - .008f * squash, .111f);
        mouth.RotationDegrees = new Vector3(0, -12 * squash, -28 * squash);
        mouth.Scale = new Vector3(1.03f + .26f * squash, 1.02f + .3f * recoil, 1);
        expression.Look(1.13f - .92f * squash, -.35f, .64f);
    }

    /// <summary>Frees a tooth at its drawn position so it can tumble with the other flying pieces.</summary>
    public Node3D ReleaseTooth(int index, Node3D stage, Vector3 towardCamera) {
        var tooth = teeth[index];
        // Sample the front of the mouth mesh itself, then use the same crease the shader draws.
        float x = (index - (ToothCount - 1) * .5f) * .007f;
        const float y = -.009f;
        float z = Mathf.Sqrt(GrimaceRadius * GrimaceRadius - x * x - y * y);
        var position = Wrap(grimace.ToGlobal(new Vector3(x, y, z))) + towardCamera * .004f;
        var direction = tooth.GlobalBasis.Orthonormalized();
        tooth.Reparent(stage, false);
        tooth.GlobalTransform = new Transform3D(direction, position);
        tooth.Scale = new Vector3(1.42f, 1.56f, 1.35f);
        tooth.Show();
        return tooth;
    }
}
