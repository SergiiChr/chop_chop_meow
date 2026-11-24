using Godot;

namespace ChopChopMeow;

/// <summary>The visible sky in first person, with a disk following the world light's day and night path.</summary>
internal sealed class FirstPersonSky {
    private static readonly Color DayTop = new("5487be"), DayHorizon = new("aac0c8"), DuskHorizon = new("d99469");
    private static readonly Color NightTop = new("130e26"), NightHorizon = new("333958"), Moon = new("dbe6ff");
    private const float SunDiameter = 2.4f, MoonDiameter = 1.6f;

    private readonly ProceduralSkyMaterial material = new() {
        SkyEnergyMultiplier = .5f,
        GroundEnergyMultiplier = .5f,
        SunAngleMax = 5,
        SunCurve = .45f,
    };
    private readonly DirectionalLight3D disk = new() {
        SkyMode = DirectionalLight3D.SkyModeEnum.SkyOnly,
        Visible = false,
    };
    private Color lastTop, lastHorizon, lastBounce;
    private bool? lastMoon;

    public Sky Background { get; }

    public FirstPersonSky(Node3D parent) {
        Background = new Sky { SkyMaterial = material, RadianceSize = Sky.RadianceSizeEnum.Size64 };
        parent.AddChild(disk);
    }

    public void Show(bool visible) => disk.Visible = visible;

    public void Update(Basis direction, Color bounce, Color sun, float night, float lowSun, float height, bool moon, float moonlight) {
        var top = DayTop.Lerp(NightTop, night);
        var horizon = DayHorizon.Lerp(DuskHorizon, lowSun * .65f).Lerp(NightHorizon, night);
        if (top != lastTop) material.SkyTopColor = lastTop = top;
        if (horizon != lastHorizon) material.SkyHorizonColor = lastHorizon = horizon;
        if (bounce != lastBounce) { lastBounce = bounce; material.GroundBottomColor = material.GroundHorizonColor = bounce; }
        if (moon != lastMoon) {
            lastMoon = moon;
            disk.LightAngularDistance = moon ? MoonDiameter : SunDiameter;
        }
        disk.Basis = direction;
        disk.LightColor = moon ? Moon : sun;
        disk.LightEnergy = (moon ? Mathf.Lerp(.18f, 1.6f, moonlight) : 4f) * Mathf.SmoothStep(0, .2f, height);
    }
}
