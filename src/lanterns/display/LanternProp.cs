namespace ChopChopMeow;

/// <summary>A lantern set on the ground, with its light turned by the world view.</summary>
internal sealed class LanternProp : Prop {
    internal readonly LanternModel Model;

    internal LanternProp() => Model = new LanternModel(Body, placed: true);
}
