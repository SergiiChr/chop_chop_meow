namespace ChopChopMeow;

/// <summary>The pump in the world, with its lever driven by the operator's stroke.</summary>
internal sealed class WaterPumpProp : Prop {
    public readonly WaterPumpShape Pump;
    public WaterPumpProp() => Pump = new WaterPumpShape(Body);
}
