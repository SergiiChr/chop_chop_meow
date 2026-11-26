using Godot;

namespace ChopChopMeow;

/// <summary>The lantern's shell, glowing firebugs and light, with the dark shell still visible when the firebugs die.</summary>
internal sealed class LanternModel {
    internal readonly Node3D Node;
    internal readonly OmniLight3D Light;
    private readonly MultiMesh firebugs;
    private const float FullBrightness = 1.2f;

    internal int BugsShown => firebugs.VisibleInstanceCount;

    internal LanternModel(Node3D parent, bool compact = false) {
        Node = LanternShape.Build(out var dots, compact);
        firebugs = dots.Multimesh;
        parent.AddChild(Node);
        Light = new OmniLight3D {
            Position = Vector3.Up * .26f,
            OmniRange = ActorRig.TorchLightRange,
            OmniAttenuation = ActorRig.TorchLightAttenuation,
            LightColor = LanternShape.LightColor,
            LightEnergy = 0,
            ShadowEnabled = true,
            LightCullMask = 0xFFFFF & ~LanternShape.Layer,
            ShadowCasterMask = 0xFFFFF & ~LanternShape.Layer,
            Visible = false,
        };
        Node.AddChild(Light);
    }

    internal void Sync(int count, float torchEnergy) {
        if (BugsShown != count) firebugs.VisibleInstanceCount = count;
        bool lit = count > 0;
        if (Light.Visible != lit) Light.Visible = lit;
        float energy = torchEnergy * (FullBrightness * count / Lanterns.Capacity);
        if (lit && Light.LightEnergy != energy) Light.LightEnergy = energy;
    }
}
