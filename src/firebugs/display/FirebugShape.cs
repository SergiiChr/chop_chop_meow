using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>How a firebug is drawn in the trial of its two looks.</summary>
public enum FirebugLook {
    /// <summary>The game's own 3D glow: a dark body, a bright heart and a soft halo.</summary>
    Glow,
    /// <summary>The pixel firefly of the pixelfx library, drawn as a flat picture facing the camera.</summary>
    Sprite
}

/// <summary>
/// A firebug's model and how it flies and glows.
/// Its glow rhythm, light trail and colours follow the fireflies of pixelfx, Patch's pixel effects library (MIT),
/// which adapt Sprite Fusion's open-source fireflies, MIT License, Copyright (c) 2026 Hugo Duprez.
/// </summary>
public static class FirebugShape {
    /// <summary>Which look firebugs are built with: the 3D glow, or the pixel sprite with <c>--firebug-look sprite</c>.</summary>
    public static readonly FirebugLook Look = Launch.Value("--firebug-look") == "sprite" ? FirebugLook.Sprite : FirebugLook.Glow;
    /// <summary>Metres off the ground a firebug flies, the height a net's hoop is swept at, and how far up and down of that it bobs.</summary>
    public const float Height = 1.25f, Bob = .2f;
    /// <summary>How bright a firebug stays between flashes, of its full glow.</summary>
    private const float Ember = .12f;
    /// <summary>Glow levels and wing beats in the sprite sheet, and metres one of its pixels covers.</summary>
    private const int SpriteLevels = 8, SpriteBeats = 2;
    private const float PixelSize = .028f;
    /// <summary>Seconds each dot of the trail lags the one before it.</summary>
    private const float TrailLag = .05f;
    private const int TrailDots = 4;
    // The pixelfx palette, from the dark body to the white heart.
    private static readonly Color Glow3 = new("9ad02a"), Glow4 = new("d6ff5a"), Glow5 = new("f4ffb8");
    private static readonly QuadMesh Quad = new() { Size = Vector2.One };
    private static readonly StandardMaterial3D Soft = Shine(Glow3);
    private static readonly StandardMaterial3D Lantern = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Glow5, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
    private static readonly Texture2D Sheet = GD.Load<Texture2D>("res://assets/models/firebug/firebug_sprite.png");

    /// <summary>A light that adds to what is behind it and always faces the camera, brightest in the middle and fading to its rim.</summary>
    private static StandardMaterial3D Shine(Color colour) {
        var falloff = new Gradient { Offsets = [0, .35f, 1], Colors = [Colors.White, new Color(1, 1, 1, .3f), new Color(1, 1, 1, 0)] };
        return new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true, AlbedoColor = colour, DisableReceiveShadows = true,
            AlbedoTexture = new GradientTexture2D { Gradient = falloff, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1, .5f), Width = 64, Height = 64 },
        };
    }

    public static FirebugProp Build() {
        var prop = new FirebugProp { Fly = new Node3D() };
        prop.Root.AddChild(prop.Fly);
        if (Look == FirebugLook.Sprite) {
            prop.Sprite = new Sprite3D {
                Texture = Sheet, Hframes = SpriteLevels, Vframes = SpriteBeats, PixelSize = PixelSize, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            prop.Fly.AddChild(prop.Sprite);
        } else {
            // The lantern covers the dark body while it is lit and lets it show between flashes.
            Sphere(prop.Fly, .026f, Vector3.Zero, Mat("2a1d18"), new Vector3(1, .7f, 1.6f), 6);
            prop.Heart = Sphere(prop.Fly, .034f, Vector3.Zero, Lantern, new Vector3(1, .8f, 1.5f), 8);
            prop.Halo = Add(prop.Fly, Quad, Soft, Vector3.Zero);
            prop.Trail = new MeshInstance3D[TrailDots]; prop.Trailed = new Vector3[TrailDots];
            for (int i = 0; i < TrailDots; i++) prop.Trail[i] = Add(prop.Root, Quad, Soft, Vector3.Zero, scale: Vector3.One * (.16f - .025f * i));
            NoShadows(prop.Fly);
            foreach (var dot in prop.Trail) dot.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        prop.Light = new OmniLight3D { OmniRange = 3.2f, LightColor = Glow4, LightEnergy = 0, ShadowEnabled = false, OmniAttenuation = 1.4f };
        prop.Fly.AddChild(prop.Light);
        return prop;
    }

    /// <summary>
    /// How brightly a firebug glows at a time, from 0 to 1: a quick rise, a hold, a slow fade and then dark, on a
    /// rhythm of its own; one in three flashes twice.
    /// </summary>
    public static float Glow(int id, float time) {
        float period = 12f / (3 + (int)(Thing.Variation(id, 21) * 4));
        float turn = (time / period + Thing.Variation(id, 22)) % 1;
        float glow = Pulse(turn);
        return Thing.Variation(id, 23) < .35f ? Math.Max(glow, Pulse(turn - .22f) * .8f) : glow;
    }

    private static float Pulse(float turn) => turn < 0 ? 0 : turn < .06f ? turn / .06f : turn < .14f ? 1 : turn < .4f ? 1 - (turn - .14f) / .26f : 0;

    /// <summary>
    /// Moves a firebug's model to where the world has it, as fast as a dart and no faster, bobbing as it flies, and
    /// sets its glow, the light it throws and the trail behind it.
    /// </summary>
    /// <param name="at">Where the world has it on the ground now, hovering included.</param>
    /// <param name="elapsed">Seconds the view has run, which its bobbing and glow go by.</param>
    public static void Fly(FirebugProp prop, int id, Vector2 at, float dartSpeed, float elapsed, float dt) {
        var target = WorldView.To3(at, Height + Bob * MathF.Sin(elapsed * (1.1f + Thing.Variation(id, 24)) + id));
        // A dart is drawn at its own speed, with a little more to catch up, so the steps of the world's updates never show.
        var drawn = prop.Drawn is Vector3 before && before.DistanceTo(target) < 4 ? before.MoveToward(target, dartSpeed * 1.4f * dt) : target;
        prop.Drawn = drawn;
        prop.Fly.Position = drawn - prop.Root.Position;
        float glow = Glow(id, elapsed), shown = Ember + (1 - Ember) * glow;
        prop.Light.LightEnergy = 1.6f * glow;
        if (prop.Sprite is Sprite3D sprite) {
            sprite.Frame = (int)MathF.Round(shown * (SpriteLevels - 1)) + SpriteLevels * ((int)(elapsed * 30) % SpriteBeats);
            return;
        }
        prop.Halo!.Transparency = 1 - .75f * shown; prop.Heart!.Transparency = 1 - shown;
        prop.Halo.Scale = Vector3.One * (.5f + 1.1f * shown);
        // Each dot follows the one before it, so the trail stretches out behind a dart and closes up as it hovers.
        var ahead = drawn;
        for (int i = 0; i < prop.Trail.Length; i++) {
            var dot = prop.Trailed[i] == default ? ahead : prop.Trailed[i].Lerp(ahead, Math.Min(1, dt / TrailLag));
            prop.Trailed[i] = dot;
            prop.Trail[i].Position = dot - prop.Root.Position;
            // It shows only while the bug is lit, fainter along its length and where a dot has caught up with the one ahead.
            float apart = Math.Min(1, dot.DistanceTo(ahead) / .08f);
            prop.Trail[i].Transparency = 1 - Math.Clamp((glow - .3f) * (1 - (i + 1) / 5f) * 1.3f, 0, 1) * apart;
            ahead = dot;
        }
    }
}
