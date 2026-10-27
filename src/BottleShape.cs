using Godot;

namespace ChopChopMeow;

/// <summary>A reusable tin flask with a wood stopper and a three-sip sight gauge.</summary>
internal static class BottleShape {
    public static Node3D Create(int sips) {
        var root = new Node3D();
        var tin = ModelKit.Mat("bbcaca", .35f, .65f);
        var rim = ModelKit.Mat("637878", .45f, .55f);
        ModelKit.Cylinder(root, .20f, .20f, .48f, new(0, .28f, 0), tin, sides: 8);
        ModelKit.Cylinder(root, .20f, .085f, .14f, new(0, .59f, 0), tin, sides: 8);
        ModelKit.Cylinder(root, .085f, .085f, .10f, new(0, .71f, 0), rim, sides: 8);
        ModelKit.Cylinder(root, .10f, .10f, .075f, new(0, .79f, 0), ModelKit.Mat("90643d"), sides: 8);
        ModelKit.Cylinder(root, .21f, .21f, .045f, new(0, .055f, 0), rim, sides: 8);
        // A broad front sight gauge makes the fill state visible even at inventory scale.
        ModelKit.Box(root, new(.17f, .39f, .025f), new(0, .29f, .20f), rim);
        for (int i = 0; i < 3; i++)
            ModelKit.Box(root, new(.125f, .095f, .035f), new(0, .17f + i * .12f, .22f),
                ModelKit.Mat(i < sips ? "43b9e6" : "293e46", .3f));
        return root;
    }
}
