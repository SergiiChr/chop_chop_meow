using Godot;

namespace ChopChopMeow;

/// <summary>A reusable tin flask with a wood stopper and a three-liter sight gauge.</summary>
internal static class BottleShape {
    private static Material GaugeColor(bool filled) => ModelKit.Mat(filled ? "43b9e6" : "293e46", .3f);
    public static Node3D Create(int liters, bool capped = true, bool adjustable = false) {
        var root = new Node3D();
        var tin = ModelKit.Metal("bbcaca", .35f, .65f);
        var rim = ModelKit.Metal("637878", .45f, .55f);
        ModelKit.Cylinder(root, .20f, .20f, .48f, new(0, .28f, 0), tin, sides: 8);
        ModelKit.Cylinder(root, .20f, .085f, .14f, new(0, .59f, 0), tin, sides: 8);
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
            bool filled = i < liters;
            if (adjustable) {
                // Fading keeps each mesh's original material, so an adjustable gauge changes which mesh is shown.
                ModelKit.Box(root, size, center, GaugeColor(false)).Visible = !filled;
                ModelKit.Box(root, size, center, GaugeColor(true)).Visible = filled;
            } else ModelKit.Box(root, size, center, GaugeColor(filled));
        }
        return root;
    }

    /// <summary>An adjustable bottle ends with one empty and one full mesh per mark, from lowest to highest.</summary>
    public static void SetGauge(Node3D bottle, int liters) {
        int first = bottle.GetChildCount() - 2 * Water.Capacity;
        for (int i = 0; i < Water.Capacity; i++) {
            bottle.GetChild<MeshInstance3D>(first + 2 * i).Visible = i >= liters;
            bottle.GetChild<MeshInstance3D>(first + 2 * i + 1).Visible = i < liters;
        }
    }
}
