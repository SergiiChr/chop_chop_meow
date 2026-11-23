using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Visible plumbing on buildings connected to pipework beneath the center of their base.</summary>
internal sealed class InstalledPlumbingShape {
    private static readonly Material Steel = Metal("8e9fa8", .55f, .42f);
    private static readonly Material Brass = Metal("b08c51", .5f, .50f);
    private static readonly Material Handle = Metal("487f84", .58f, .37f);
    private static readonly CylinderMesh Tube = new() {
        BottomRadius = 1,
        TopRadius = 1,
        Height = 1,
        RadialSegments = 6,
        Rings = 0,
        CapTop = false,
        CapBottom = false,
    };
    private const float NozzleHeight = .25f, Quarter = (Farms.HalfSize - .15f) / 2;
    private static readonly Vector3[] Nozzles = [
        new(-Quarter, NozzleHeight, -Quarter), new(Quarter, NozzleHeight, -Quarter),
        new(-Quarter, NozzleHeight, Quarter), new(Quarter, NozzleHeight, Quarter),
    ];
    private static readonly Material NozzleCase = Metal("3e4545", .68f, .35f);
    private static readonly Material NozzleCap = Metal("616b68", .58f, .45f);
    private static readonly CylinderMesh SprinklerLid = new() {
        BottomRadius = .085f,
        TopRadius = .068f,
        Height = .035f,
        RadialSegments = 6,
        Rings = 0,
        CapBottom = false,
    };
    private readonly Node3D root;
    private readonly MeshInstance3D? spray;

    private InstalledPlumbingShape(Node3D root, MeshInstance3D? spray) => (this.root, this.spray) = (root, spray);

    public static InstalledPlumbingShape? Build(ThingKind kind, Node3D body) {
        if (kind is not (ThingKind.Hut or ThingKind.Barrel or ThingKind.Cooktable or ThingKind.Farm)) return null;
        var root = Pivot(body, Vector3.Zero);
        MeshInstance3D? spray = null;
        switch (kind) {
            case ThingKind.Hut: Cabin(root); break;
            case ThingKind.Barrel: Barrel(root); break;
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
        // From below the middle of the floor to the exterior corner beside the ladder.
        var foot = new Vector3(1.26f, .14f, 1.08f);
        var top = foot with { Y = 3.09f };
        var roofBend = top with { Z = CabinWaterModel.FillOpening.Z };
        var barrelPort = roofBend with { X = -.275f };
        Pipe(root, Vector3.Zero, foot, .047f, Steel);
        Pipe(root, foot with { Y = .03f }, top, .047f, Steel);
        Pipe(root, top, roofBend, .047f, Steel);
        Pipe(root, roofBend, barrelPort, .047f, Steel);
        foreach (float y in new[] { .85f, 2.04f }) {
            var strap = foot with { Y = y };
            Box(root, new Vector3(.158f, .046f, .092f), new Vector3(1.219f, y, 1.018f), Steel);
            Pipe(root, strap - Vector3.Up * .013f, strap + Vector3.Up * .013f, .060f, Steel);
        }
        // The feed uses the barrel's end and leaves its hand-fill neck clear.
        Cylinder(root, .068f, .068f, .067f, barrelPort, Brass, new Vector3(0, 0, 90), 6);
    }

    private static void Barrel(Node3D root) {
        var foot = new Vector3(.34f, .023f, 0);
        var valve = foot with { Y = .147f };
        var inlet = valve with { X = .167f };
        Pipe(root, Vector3.Zero, foot, .033f, Steel);
        Pipe(root, foot, valve, .033f, Steel);
        Pipe(root, valve, inlet, .034f, Steel);
        Box(root, new Vector3(.14f, .024f, .14f), foot, Steel);
        Cylinder(root, .051f, .051f, .048f, inlet, Steel, new Vector3(0, 0, 90), 8);
        Post(root, .046f, .038f, .06f, valve - Vector3.Up * .029f, Brass, 8);
        Post(root, .022f, .022f, .061f, valve + Vector3.Up * .021f, Brass, 6);
        Box(root, new Vector3(.13f, .025f, .04f), valve + new Vector3(.036f, .086f, 0), Handle);
    }

    private static void Cooktable(Node3D root) {
        var baseAt = new Vector3(0, .068f, 0);
        var foot = new Vector3(.55f, baseAt.Y, -.195f);
        var rise = foot with { Y = .96f };
        var bend = new Vector3(.50f, .957f, -.037f);
        var nose = new Vector3(.43f, .91f, .06f);
        Pipe(root, Vector3.Zero, baseAt, .037f, Steel);
        Pipe(root, baseAt, foot, .037f, Steel);
        Pipe(root, foot, rise, .037f, Steel);
        Pipe(root, rise, bend, .038f, Brass);
        Pipe(root, bend, nose, .035f, Brass);
        Post(root, .04f, .033f, .061f, nose - Vector3.Up * .039f, Brass, 8);
        Post(root, .067f, .054f, .027f, new Vector3(foot.X, .645f, foot.Z), Steel, 8);
        Box(root, new Vector3(.14f, .032f, .04f), rise + new Vector3(.034f, .06f, -.022f), Handle);
        Post(root, .025f, .025f, .049f, rise + Vector3.Up * .01f, Brass, 6);
        Box(root, new Vector3(.34f, .012f, .30f), new Vector3(.355f, .648f, .055f), Steel);
        foreach (float x in new[] { .208f, .502f })
            foreach (float z in new[] { -.073f, .183f })
                Box(root, new Vector3(.022f, .008f, .022f), new Vector3(x, .657f, z), Brass);
    }

    private static MeshInstance3D Farm(Node3D root) {
        foreach (var nozzle in Nozzles) {
            Add(root, Tube, NozzleCase, nozzle with { Y = .125f }).Scale = new Vector3(.067f, .18f, .067f);
            Add(root, SprinklerLid, NozzleCap, nozzle - Vector3.Up * (SprinklerLid.Height / 2));
        }
        return FarmSpray.Build(root, Nozzles, Quarter);
    }

    private static void Pipe(Node3D parent, Vector3 from, Vector3 to, float radius, Material metal) {
        var span = to - from;
        var aim = new Basis(new Quaternion(Vector3.Up, span.Normalized()));
        var mesh = Add(parent, Tube, metal, (from + to) / 2);
        mesh.Transform = new Transform3D(
            aim * Basis.FromScale(new Vector3(radius, span.Length() + radius * 1.25f, radius)), mesh.Position);
    }
}
