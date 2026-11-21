using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Surface fittings on buildings connected to pipework beneath the center of their base.</summary>
internal sealed class InstalledPlumbingShape {
    private static readonly Material Steel = Metal("8e9fa8", .55f, .42f);
    private static readonly Material Brass = Metal("b08c51", .5f, .50f);
    private static readonly CylinderMesh Tube = new() {
        BottomRadius = 1,
        TopRadius = 1,
        Height = 1,
        RadialSegments = 6,
        Rings = 0,
        CapTop = false,
        CapBottom = false,
    };
    private const float SprinklerY = 1.135f;
    private static readonly Vector3 LeftNozzle = new(-1.39f, SprinklerY, 0);
    private static readonly Vector3 RightNozzle = new(1.39f, SprinklerY, 0);
    private readonly Node3D root;
    private readonly MeshInstance3D? spray;

    private InstalledPlumbingShape(Node3D root, MeshInstance3D? spray) => (this.root, this.spray) = (root, spray);

    public static InstalledPlumbingShape? Build(ThingKind kind, Node3D body) {
        if (kind is not (ThingKind.Hut or ThingKind.Cooktable or ThingKind.Farm)) return null;
        var root = Pivot(body, Vector3.Zero);
        MeshInstance3D? spray = null;
        switch (kind) {
            case ThingKind.Hut: Cabin(root); break;
            case ThingKind.Cooktable: Cooktable(root); break;
            case ThingKind.Farm: spray = Farm(root); break;
        }
        return new InstalledPlumbingShape(root, spray);
    }

    public void Show(bool installed) {
        if (root.Visible != installed) root.Visible = installed;
    }

    public void Water(bool flowing, float elapsed) {
        if (spray == null) return;
        if (spray.Visible != flowing) spray.Visible = flowing;
        if (flowing) FarmSpray.SetTime(spray, elapsed);
    }

    private static void Cabin(Node3D root) {
        // Take the supply into the barrel's end so its top opening remains free for hand filling.
        var top = new Vector3(0, 3.09f, 0);
        var bend = top with { Z = CabinWaterModel.FillOpening.Z };
        var barrelPort = bend with { X = -.275f };
        Pipe(root, Vector3.Zero, top, .048f, Steel);
        Pipe(root, top, bend, .048f, Steel);
        Pipe(root, bend, barrelPort, .048f, Steel);
        var throughRoof = new Vector3(0, CabinShape.RoofSurface(0), 0);
        Box(root, new Vector3(.19f, .036f, .19f), throughRoof, Steel, new Vector3(CabinShape.RoofPitchDegrees, 0, 0));
        Post(root, .071f, .061f, .067f, throughRoof, Brass, 6);
        Cylinder(root, .068f, .068f, .067f, barrelPort, Brass, new Vector3(0, 0, 90), 6);
    }

    private static void Cooktable(Node3D root) {
        var baseAt = new Vector3(0, .068f, 0);
        var foot = new Vector3(.55f, baseAt.Y, -.195f);
        var rise = foot with { Y = .96f };
        var nose = rise with { Z = .045f, Y = .91f };
        Pipe(root, Vector3.Zero, baseAt, .037f, Steel);
        Pipe(root, baseAt, foot, .037f, Steel);
        Pipe(root, foot, rise, .037f, Steel);
        Pipe(root, rise, rise with { Z = -.018f }, .038f, Brass);
        Pipe(root, rise with { Z = -.018f }, nose, .035f, Brass);
        Post(root, .04f, .033f, .061f, nose - Vector3.Up * .039f, Brass, 8);
        Post(root, .067f, .054f, .027f, new Vector3(foot.X, .645f, foot.Z), Steel, 8);
        Box(root, new Vector3(.14f, .032f, .04f), rise + new Vector3(.034f, .06f, -.022f), Metal("487f84", .58f, .37f));
        Post(root, .025f, .025f, .049f, rise + Vector3.Up * .01f, Brass, 6);
    }

    private static MeshInstance3D Farm(Node3D root) {
        const float barY = SprinklerY - .10f;
        Pipe(root, Vector3.Zero, Vector3.Up * barY, .043f, Steel);
        Pipe(root, LeftNozzle with { Y = barY }, RightNozzle with { Y = barY }, .035f, Steel);
        Box(root, new Vector3(.21f, .045f, .21f), Vector3.Up * .034f, Steel);
        foreach (var end in new[] { LeftNozzle, RightNozzle })
            Post(root, .062f, .043f, SprinklerY - barY, end with { Y = barY }, Brass, 6);
        return FarmSpray.Build(root, LeftNozzle, RightNozzle);
    }

    private static void Pipe(Node3D parent, Vector3 from, Vector3 to, float radius, Material metal) {
        var span = to - from;
        var aim = new Basis(new Quaternion(Vector3.Up, span.Normalized()));
        var mesh = Add(parent, Tube, metal, (from + to) / 2);
        mesh.Transform = new Transform3D(
            aim * Basis.FromScale(new Vector3(radius, span.Length() + radius * 1.25f, radius)), mesh.Position);
    }
}
