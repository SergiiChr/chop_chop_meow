using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>The outline before digging and the nub of disturbed earth after the acorn goes in.</summary>
internal static class TreePlantingShape {
    private static readonly StandardMaterial3D Mark = Flat(new Color("eed6a0"));
    private static readonly StandardMaterial3D Soil = Mat("664533", .95f);
    private static readonly StandardMaterial3D Shoot = Mat("928049", .9f);

    public static void Outline(Node3D body) {
        var ring = Torus(body, .38f, .45f, Vector3.Up * .03f, Mark);
        ring.Scale = new Vector3(1, .45f, 1);
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    public static Node3D Nub(Node3D body) {
        var nub = Pivot(body, Vector3.Zero);
        Sphere(nub, .24f, Vector3.Zero, Soil, new Vector3(1, .43f, .82f), segments: 8);
        Cylinder(nub, .045f, .025f, .11f, new Vector3(0, .12f, 0), Shoot, new Vector3(0, 0, -14), sides: 6);
        return nub;
    }
}
