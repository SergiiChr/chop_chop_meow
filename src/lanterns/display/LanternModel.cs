using Godot;

namespace ChopChopMeow;

/// <summary>The lantern's shell, glowing firebugs and light, with the dark shell still visible when the firebugs die.</summary>
internal sealed class LanternModel {
    internal readonly Node3D Node;
    internal readonly OmniLight3D Light;
    private readonly MultiMesh firebugs;
    private readonly MeshInstance3D glass;
    private static readonly StandardMaterial3D[] GlassFinishes = BuildGlassFinishes();
    private const float FullBrightness = 1.2f;

    internal int BugsShown => firebugs.VisibleInstanceCount;

    internal LanternModel(Node3D parent, bool compact = false, bool placed = false) {
        Node = LanternShape.Build(out var dots, out glass, compact);
        firebugs = dots.Multimesh;
        parent.AddChild(Node);
        Light = new OmniLight3D {
            Position = LanternShape.LightCenter,
            OmniRange = ActorRig.TorchLightRange,
            OmniAttenuation = ActorRig.TorchLightAttenuation,
            LightColor = LanternShape.LightColor,
            LightEnergy = 0,
            ShadowEnabled = true,
            ShadowBias = .01f,
            ShadowNormalBias = .03f,
            // Keep the point light off the shell, so its own glare does not overpower the firebugs through the glass.
            LightCullMask = 0xFFFFF & ~(LanternShape.TrimLayer | LanternShape.EndsLayer),
            // On the ground the foot blocks light beneath it. Off the ground its hard shadow would become a large square.
            ShadowCasterMask = 0xFFFFF & ~(LanternShape.TrimLayer | (placed ? 0 : LanternShape.EndsLayer)),
            Visible = false,
        };
        Node.AddChild(Light);
    }

    internal void Sync(int count, float torchEnergy) {
        bool lit = count > 0;
        if (BugsShown != count) {
            firebugs.VisibleInstanceCount = count;
            glass.MaterialOverride = GlassFinishes[count];
        }
        if (Light.Visible != lit) Light.Visible = lit;
        float energy = torchEnergy * (FullBrightness * count / Lanterns.Capacity);
        if (lit && Light.LightEnergy != energy) Light.LightEnergy = energy;
    }

    /// <summary>The firebugs light the glass from inside. Each live firebug adds the same amount of glow.</summary>
    private static StandardMaterial3D[] BuildGlassFinishes() {
        var finishes = new StandardMaterial3D[Lanterns.Capacity + 1];
        finishes[0] = GlassJarShape.Finish;
        for (int count = 1; count < finishes.Length; count++) {
            var glass = (StandardMaterial3D)GlassJarShape.Finish.Duplicate();
            glass.EmissionEnabled = true;
            glass.Emission = new Color("92de47");
            glass.EmissionEnergyMultiplier = 1.5f * count / Lanterns.Capacity;
            finishes[count] = glass;
        }
        return finishes;
    }
}
