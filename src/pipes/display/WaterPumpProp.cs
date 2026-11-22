namespace ChopChopMeow;

/// <summary>The pump in the world, with its lever driven by the operator's stroke.</summary>
internal sealed class WaterPumpProp : Prop {
    public readonly WaterPumpShape Pump;
    private float lastPress, returnTime;

    /// <summary>Lets go of an interrupted stroke without the lever snapping back to the top.</summary>
    public void ShowStroke(float? pressed, float dt) {
        if (pressed is float active) (lastPress, returnTime) = (active, 0);
        else returnTime = System.Math.Min(ActorAnimation.BlendSeconds, returnTime + dt);
        Pump.SetPress(lastPress * (1 - WorldView.Ease(returnTime / ActorAnimation.BlendSeconds)));
    }

    public WaterPumpProp() => Pump = new WaterPumpShape(Body);
}
