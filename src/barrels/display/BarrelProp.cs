using Godot;

namespace ChopChopMeow;

/// <summary>A standing steel barrel with a bung that slides aside for a pour.</summary>
internal sealed class BarrelProp : Prop {
    private readonly Node3D bung;
    private readonly Vector3 closed;

    public BarrelProp() {
        (_, bung) = VesselShape.AddBarrel(Body, Vector3.Zero);
        closed = bung.Position;
    }

    public void SetBungOpen(float share) => bung.Position = closed + new Vector3(-.076f, -.013f, .04f) * share;
}
