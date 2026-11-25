using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// A firebug's model and how it flies and glows: a dark body, a lantern over it and a soft halo.
/// Its colours follow the fireflies of pixelfx, Patch's pixel effects library (MIT).
/// </summary>
public static class FirebugShape {
    /// <summary>Metres off the ground a firebug flies, the height a net's hoop is swept at, and how far up and down of that it bobs.</summary>
    public const float Height = 1.25f, Bob = .2f;
    /// <summary>How bright a firebug glows at rest, of a full glow, and how much brighter than that a flash is.</summary>
    private const float Ember = .12f, FlashGain = .1f;
    /// <summary>Seconds from one flash to the next, and the seconds a flash rises, holds and fades.</summary>
    private const float FlashEvery = 10, FlashRise = .2f, FlashHold = .25f, FlashFade = .75f;
    // The pixelfx palette's glow and its white heart.
    private static readonly Color HaloColour = new("9ad02a"), LanternColour = new("f4ffb8");
    private static readonly QuadMesh Quad = new() { Size = Vector2.One };
    private static readonly StandardMaterial3D Soft = Shine(HaloColour);
    private static readonly StandardMaterial3D Lantern = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = LanternColour, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };

    /// <summary>A light that adds to what is behind it and always faces the camera, brightest in the middle and fading to its rim.</summary>
    private static StandardMaterial3D Shine(Color colour) {
        var falloff = new Gradient { Offsets = [0, .35f, 1], Colors = [Colors.White, new Color(1, 1, 1, .3f), new Color(1, 1, 1, 0)] };
        return new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true,
            AlbedoColor = colour,
            DisableReceiveShadows = true,
            AlbedoTexture = new GradientTexture2D { Gradient = falloff, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1, .5f), Width = 64, Height = 64 },
        };
    }

    public static FirebugProp Build() {
        var prop = new FirebugProp { Fly = new Node3D() };
        prop.Root.AddChild(prop.Fly);
        // The lantern lies over the dark body, which shows through it.
        Sphere(prop.Fly, .026f, Vector3.Zero, Mat("2a1d18"), new Vector3(1, .7f, 1.6f), 6);
        prop.Heart = Sphere(prop.Fly, .034f, Vector3.Zero, Lantern, new Vector3(1, .8f, 1.5f), 8);
        prop.Halo = Add(prop.Fly, Quad, Soft, Vector3.Zero);
        NoShadows(prop.Root);
        return prop;
    }

    /// <summary>How far into a flash a firebug is at a time, from 0 at rest to 1: a quick rise, a hold and a slow fade, once every <see cref="FlashEvery"/> seconds at a moment of its own.</summary>
    public static float Flash(int id, float time) {
        float since = (time + Thing.Variation(id, 22) * FlashEvery) % FlashEvery;
        if (since < FlashRise) return since / FlashRise;
        since -= FlashRise + FlashHold;
        return since < 0 ? 1 : Math.Max(0, 1 - since / FlashFade);
    }

    /// <summary>
    /// Moves a firebug's model to where the world has it, as fast as a dart and no faster, bobbing as it flies, and
    /// sets its glow.
    /// </summary>
    /// <param name="at">Where the world has it on the ground now, hovering included.</param>
    /// <param name="elapsed">Seconds the view has run, which its bobbing and glow go by.</param>
    public static void Fly(FirebugProp prop, int id, Vector2 at, float dartSpeed, float elapsed, float dt) {
        var target = WorldView.To3(at, Height + Bob * MathF.Sin(elapsed * (1.1f + Thing.Variation(id, 24)) + id));
        // A dart is drawn at its own speed, with a little more to catch up, so the steps of the world's updates never show.
        var drawn = prop.Drawn is Vector3 before && before.DistanceTo(target) < 4 ? before.MoveToward(target, dartSpeed * 1.4f * dt) : target;
        prop.Drawn = drawn;
        prop.Fly.Position = drawn - prop.Root.Position;
        float glow = Ember * (1 + FlashGain * Flash(id, elapsed));
        prop.Halo.Transparency = 1 - .75f * glow; prop.Heart.Transparency = 1 - glow;
        prop.Halo.Scale = Vector3.One * (.5f + 1.1f * glow);
    }
}
