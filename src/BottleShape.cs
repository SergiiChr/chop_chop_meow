using Godot;

namespace ChopChopMeow;

/// <summary>A reusable steel flask with a wood stopper and a three-liter sight gauge for water or jam.</summary>
internal static class BottleShape {
    private enum Mark { Empty, Water, Jam }
    private const int LooksPerBar = 3;

    private static Material GaugeColor(Mark mark) => ModelKit.Mat(mark switch {
        Mark.Water => "43b9e6",
        Mark.Jam => BerryJam.Color,
        _ => "293e46",
    }, .3f);

    /// <summary>The liters and liquid a bottle's look shows, such as two of water for <c>water_2</c>.</summary>
    private static (int Liters, Mark Filled) Contents(string look) {
        int jam = System.Array.IndexOf(BerryJam.Looks, look);
        return jam > 0 ? (jam, Mark.Jam) : (System.Math.Max(0, System.Array.IndexOf(Water.Looks, look)), Mark.Water);
    }

    public static Node3D Create(string item, bool capped = true, bool adjustable = false) {
        var (liters, filled) = Contents(item);
        var root = new Node3D();
        var steel = ModelKit.Metal(VesselShape.Steel, VesselShape.Finish.Roughness, VesselShape.Finish.Metallic);
        var rim = ModelKit.Metal(VesselShape.Edge, VesselShape.Finish.Roughness, VesselShape.Finish.Metallic);
        ModelKit.Cylinder(root, .20f, .20f, .48f, new(0, .28f, 0), steel, sides: 8);
        ModelKit.Cylinder(root, .20f, .085f, .14f, new(0, .59f, 0), steel, sides: 8);
        if (capped) {
            ModelKit.Cylinder(root, .085f, .085f, .10f, new(0, .71f, 0), rim, sides: 8);
            ModelKit.Cylinder(root, .10f, .10f, .075f, new(0, .79f, 0), ModelKit.Wood("90643d"), sides: 8);
        } else {
            var sides = new CylinderMesh { BottomRadius = .085f, TopRadius = .085f, Height = .1f, RadialSegments = 8, Rings = 0, CapTop = false };
            ModelKit.Add(root, sides, rim, new(0, .71f, 0));
            ModelKit.Cylinder(root, .082f, .082f, .01f, new(0, .691f, 0), ModelKit.Mat("293e46"), sides: 8);
        }
        ModelKit.Cylinder(root, .21f, .21f, .045f, new(0, .055f, 0), rim, sides: 8);
        // A broad front sight gauge makes the fill state visible even at inventory scale.
        ModelKit.Box(root, new(.17f, .39f, .025f), new(0, .29f, .20f), rim);
        for (int i = 0; i < Water.Capacity; i++) {
            var center = new Vector3(0, .17f + i * .12f, .22f);
            var size = new Vector3(.125f, .095f, .035f);
            Mark shown = i < liters ? filled : Mark.Empty;
            // Fading keeps each mesh's original material, so an adjustable gauge changes which mesh is shown.
            if (adjustable) for (int look = 0; look < LooksPerBar; look++)
                ModelKit.Box(root, size, center, GaugeColor((Mark)look)).Visible = (Mark)look == shown;
            else ModelKit.Box(root, size, center, GaugeColor(shown));
        }
        return root;
    }

    /// <summary>An adjustable bottle ends with three meshes per gauge bar, one for each possible appearance.</summary>
    public static void SetGauge(Node3D bottle, string item) {
        var (liters, filled) = Contents(item);
        int first = bottle.GetChildCount() - LooksPerBar * Water.Capacity;
        for (int i = 0; i < Water.Capacity; i++) {
            Mark shown = i < liters ? filled : Mark.Empty;
            for (int look = 0; look < LooksPerBar; look++)
                bottle.GetChild<MeshInstance3D>(first + LooksPerBar * i + look).Visible = (Mark)look == shown;
        }
    }
}
