using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ActorShape;

namespace ChopChopMeow;

/// <summary>
/// One neutral human core: outfits use palette textures while geometry and skin weights are shared.
/// It comes in the game's build and a slim one for the intro film's catgirls.
/// The slim one has a waist, a bust, shaped shoulders, tapered limbs and wrists that end without a fist.
/// </summary>
internal static class HumanShape {
    private const float Cloth = 1f / 6, Middle = .5f, End = 5f / 6;
    private const float NeckTone = .125f, ShirtTone = .375f, BandTone = .625f, HipsTone = .875f;
    private static readonly Color Neutral = Colors.White;
    private static readonly Dictionary<string, StandardMaterial3D> palettes = new();

    /// <summary>One colour of a palette, and whether it is cloth, which takes the weave, or bare skin and shoes.</summary>
    internal readonly record struct Tone(string Color, bool Cloth);
    private static readonly Ring[] hips = {
        new(.13f,.13f,.085f,Neutral),new(.08f,.155f,.1f,Neutral),
        new(.02f,.165f,.09f,Neutral),new(-.015f,.165f,.088f,Neutral)
    };
    private static readonly Ring[] torso = {
        new(.54f,.055f,.05f,Neutral),new(.5f,.12f,.075f,Neutral),
        new(.44f,.177f,.095f,Neutral),new(.35f,.17f,.105f,Neutral),
        new(.23f,.145f,.095f,Neutral),new(.13f,.14f,.09f,Neutral),new(.105f,.13f,.085f,Neutral)
    };
    private static readonly Ring[] leg = {
        new(.08f,.04f,.04f,Neutral,Tone:Cloth),new(.035f,.082f,.085f,Neutral,Tone:Cloth),
        new(-.1f,.087f,.09f,Neutral,Tone:Cloth),new(-.25f,.075f,.08f,Neutral,Tone:Cloth),
        new(-.32f,.068f,.073f,Neutral,Tone:Cloth),new(-.38f,.065f,.07f,Neutral,Tone:Cloth),
        new(-.44f,.063f,.067f,Neutral,Tone:Cloth),new(-.52f,.064f,.067f,Neutral,Tone:Cloth),
        new(-.63f,.055f,.058f,Neutral,Tone:Cloth),new(-.635f,.057f,.06f,Neutral,Tone:Middle),
        new(-.69f,.06f,.065f,Neutral,Tone:Middle),new(-.735f,.069f,.108f,Neutral,.035f,Tone:Middle),
        new(-.77f,.07f,.123f,Neutral,.045f,Tone:Middle),new(-.78f,.071f,.123f,Neutral,.045f,Tone:End),
        new(-.8f,.058f,.108f,Neutral,.045f,Tone:End),new(-.8f,.001f,.001f,Neutral,.045f,Tone:End)
    };
    private static readonly Ring[] leftArm = ArmProfile(-1), rightArm = ArmProfile(1);
    private static Ring[] ArmProfile(float inward) => new[] {
        new Ring(.065f,.012f,.012f,Neutral,Sideways:inward*.065f,Tone:Cloth),new(.035f,.06f,.062f,Neutral,Sideways:inward*.025f,Tone:Cloth),
        new(-.025f,.077f,.073f,Neutral,Tone:Cloth),new(-.11f,.07f,.065f,Neutral,Tone:Cloth),
        new(-.16f,.065f,.06f,Neutral,Tone:Cloth),new(-.165f,.061f,.057f,Neutral,Tone:Middle),
        new(-.22f,.057f,.054f,Neutral,Tone:Middle),new(-.28f,.051f,.051f,Neutral,Tone:Middle),
        new(-.34f,.057f,.054f,Neutral,Tone:Middle),new(-.41f,.051f,.049f,Neutral,Tone:Middle),
        new(-.49f,.037f,.038f,Neutral,Tone:Middle),new(-.525f,.046f,.04f,Neutral,.008f,Tone:End),
        new(-.565f,.044f,.041f,Neutral,.015f,Tone:End),new(-.59f,.026f,.025f,Neutral,.015f,Tone:End),
        new(-.6f,.001f,.001f,Neutral,.015f,Tone:End)
    };

    /// <summary>The build of every person in the game.</summary>
    public static readonly BodyBuild Standard = new(hips, torso, leg, leftArm, rightArm, ActorRig.ShoulderOut, 1, .023f);

    /// <summary>The intro film's catgirls' build, made the first time it is asked for.</summary>
    public static BodyBuild Slim => SlimBuild.Build;

    private static class SlimBuild {
        // Down to the rim the legs hang from; the leg openings take the body on from there.
        private static readonly Ring[] hips = {
            new(.105f, .098f, .075f, Neutral, .022f), new(.086f, .107f, .082f, Neutral, .017f),
            new(.066f, .116f, .089f, Neutral, .012f), new(.046f, .123f, .096f, Neutral, .005f),
            new(.028f, .127f, .101f, Neutral, -.001f),
        };
        // Front and profile sections from the shoulder girdle through the ribcage into the waist.
        private static readonly Ring[] chest = {
            new(.402f, .045f, .041f, Neutral, -.008f),
            new(.391f, .073f, .047f, Neutral, -.015f), new(.379f, .103f, .054f, Neutral, -.023f),
            new(.365f, .129f, .062f, Neutral, -.023f), new(.349f, .133f, .069f, Neutral, -.017f),
            new(.328f, .12f, .08f, Neutral, .007f),
            new(.305f, .114f, .084f, Neutral, .027f, FrontLobes: .024f),
            new(.276f, .112f, .082f, Neutral, .036f, FrontLobes: .032f),
            new(.249f, .108f, .079f, Neutral, .035f, FrontLobes: .023f),
            new(.218f, .104f, .073f, Neutral, .032f, FrontLobes: .009f),
            new(.179f, .097f, .068f, Neutral, .028f), new(.147f, .093f, .069f, Neutral, .026f),
            new(.129f, .093f, .07f, Neutral, .024f),
        };
        private static readonly Ring[] waistband = {
            new(.126f, .094f, .071f, Neutral, .024f, Tone: ShirtTone),
            new(.124f, .094f, .071f, Neutral, .024f, Tone: BandTone),
            new(.108f, .097f, .074f, Neutral, .022f, Tone: BandTone),
        };
        private static readonly Ring[] neck = {
            new(.467f, .035f, .037f, Neutral, .014f, Tone: NeckTone),
            new(.446f, .032f, .036f, Neutral, .009f, Tone: NeckTone),
            new(.43f, .032f, .036f, Neutral, .005f, Tone: NeckTone),
            new(.418f, .034f, .037f, Neutral, .001f, Tone: NeckTone),
            new(.411f, .037f, .038f, Neutral, -.003f, Tone: NeckTone),
            new(.409f, .038f, .039f, Neutral, -.004f, Tone: ShirtTone),
        };

        // From front, the thigh fills out to the side. In profile, the calf fills out behind the shin.
        // A shallow kneecap continues into the shin, with room at the back for the knee to fold.
        private static readonly Ring[] legs = {
            new(.058f, .024f, .034f, Neutral, -.006f, -.022f, Cloth),
            new(.037f, .048f, .066f, Neutral, -.010f, -.012f, Cloth),
            new(.012f, .065f, .084f, Neutral, -.006f, -.003f, Cloth),
            new(-.027f, .073f, .087f, Neutral, -.002f, .006f, Cloth),
            new(-.082f, .076f, .082f, Neutral, .001f, .009f, Cloth),
            new(-.136f, .077f, .075f, Neutral, .003f, .012f, Cloth),
            new(-.196f, .07f, .066f, Neutral, .002f, .012f, Cloth),
            new(-.245f, .061f, .059f, Neutral, .002f, .01f, Cloth),
            new(-.294f, .052f, .05f, Neutral, .002f, .005f, Cloth),
            new(-.332f, .046f, .044f, Neutral, .002f, Tone: Cloth),
            new(-.350f, .043f, .041f, Neutral, .004f, Tone: Cloth),
            new(-.365f, .041f, .041f, Neutral, .006f, Tone: Cloth),
            new(-.380f, .04f, .04f, Neutral, .008f, Tone: Cloth),
            new(-.397f, .039f, .04f, Neutral, .005f, Tone: Cloth),
            new(-.417f, .038f, .04f, Neutral, .001f, Tone: Cloth),
            new(-.444f, .041f, .044f, Neutral, -.007f, .004f, Cloth),
            new(-.478f, .047f, .048f, Neutral, -.019f, .007f, Cloth),
            new(-.524f, .048f, .05f, Neutral, -.025f, .005f, Cloth),
            new(-.57f, .046f, .044f, Neutral, -.024f, .003f, Cloth),
            new(-.62f, .038f, .036f, Neutral, -.016f, Tone: Cloth),
            new(-.675f, .03f, .03f, Neutral, -.009f, Tone: Cloth),
            new(-.709f, .027f, .027f, Neutral, -.005f, Tone: Cloth),
            new(-.727f, .026f, .027f, Neutral, -.004f, Tone: Cloth),
            new(-.733f, .027f, .029f, Neutral, -.004f, Tone: Middle),
            new(-.764f, .032f, .038f, Neutral, .003f, Tone: Middle),
            new(-.788f, .041f, .061f, Neutral, .02f, Tone: Middle),
            new(-.811f, .046f, .091f, Neutral, .039f, Tone: Middle),
            new(-.832f, .048f, .1f, Neutral, .045f, Tone: Middle),
            new(-.839f, .048f, .101f, Neutral, .045f, Tone: End),
            new(-.85f, .044f, .095f, Neutral, .045f, Tone: End),
            new(-.85f, .001f, .001f, Neutral, .045f, Tone: End),
        };

        private static readonly Ring[] face = {
            new(.253f, .012f, .012f, Neutral, -.007f), new(.237f, .067f, .071f, Neutral, -.007f),
            new(.207f, .096f, .102f, Neutral, -.003f), new(.166f, .107f, .116f, Neutral, .002f),
            new(.127f, .104f, .109f, Neutral, .006f), new(.101f, .093f, .099f, Neutral, .01f),
            new(.071f, .073f, .083f, Neutral, .018f), new(.045f, .043f, .067f, Neutral, .022f),
            new(.034f, .017f, .047f, Neutral, .023f)
        };

        public static readonly BodyBuild Build = new(hips, chest, legs, Arm(-1), Arm(1), .123f, 1, .011f, face) {
            LegSide = .077f,
            Shin = .42f,
            ShoulderUp = .35f,
            ShoulderDepth = -.032f,
            UpperArm = .221f,
            Forearm = .275f,
            Waist = .113f,
            HeadOffset = new(0, .42f, .008f),
            HeadScale = new(.675f, .87f, .76f),
            RightLeg = Array.ConvertAll(legs, ring => ring with { Sideways = -ring.Sideways }),
            // The arm's opening takes the five chest rings from the collarbone down past the armpit.
            Trunk = new([
                .. neck,
                .. Array.ConvertAll(chest, ring => ring with { Tone = ShirtTone }),
                .. waistband,
                .. Array.ConvertAll(hips, ring => ring with { Tone = HipsTone }),
            ], ShoulderAbove: .035f, ShoulderBelow: .06f, LimbSeam: .055f, Crotch: [
                new(0, -.002f, .079f), new(0, -.048f, .041f), new(0, -.063f, .007f), new(0, -.046f, -.033f), new(0, .002f, -.076f),
            ]),
        };

        /// <summary>The round shoulder draws into the slimmer elbow, with a fuller forearm just below it.</summary>
        private static Ring[] Arm(float inward) => new Ring[] {
            new(.032f, .012f, .017f, Neutral, Sideways: inward * .018f, Tone: Cloth),
            new(.022f, .026f, .032f, Neutral, Sideways: inward * .009f, Tone: Cloth),
            new(0, .035f, .041f, Neutral, Tone: Cloth),
            new(-.035f, .033f, .038f, Neutral, Tone: Cloth), new(-.075f, .03f, .033f, Neutral, Tone: Cloth),
            new(-.113f, .027f, .029f, Neutral, Tone: Cloth), new(-.12f, .027f, .029f, Neutral, Tone: Middle),
            new(-.172f, .025f, .027f, Neutral, Tone: Middle),
            new(-.198f, .024f, .026f, Neutral, Tone: Middle), new(-.221f, .03f, .031f, Neutral, Tone: Middle),
            new(-.25f, .029f, .03f, Neutral, Tone: Middle), new(-.283f, .027f, .029f, Neutral, Tone: Middle),
            new(-.335f, .022f, .025f, Neutral, Tone: Middle), new(-.391f, .018f, .021f, Neutral, Tone: Middle),
            new(-.426f, .017f, .019f, Neutral, .003f, Tone: Middle), new(-.446f, .021f, .021f, Neutral, .006f, Tone: Middle),
            new(-.455f, .017f, .015f, Neutral, .008f, Tone: Middle),
        };
    }

    /// <param name="chest">
    /// What turns at the waist: the build's chest goes on it, its hips on <paramref name="pelvis"/>.
    /// </param>
    public static void Torso(Node3D pelvis, Node3D chest, Outfit outfit, BodyBuild build) {
        ActorShape.Add(pelvis, build.Hips, material: ModelKit.Cloth(outfit.Hips)).Name = "HumanHips";
        ActorShape.Add(chest, build.Chest, material: ModelKit.Cloth(outfit.Top)).Name = "HumanTorso";
    }

    /// <summary>The joined trunk's colours: the neck's skin, then the top, a waistband and the hips, all three woven.</summary>
    internal static Tone[] TrunkTones(Outfit outfit) {
        string band = outfit.Top == outfit.Hips ? outfit.Hips : new Color(outfit.Hips).Darkened(.23f).ToHtml(false);
        return [new(outfit.Skin, false), new(outfit.Top, true), new(band, true), new(outfit.Hips, true)];
    }

    /// <summary>Draws the leg's clothing on the same bending mesh as the leg itself.</summary>
    public static Skeleton3D Leg(Node3D parent, float joint, float ankle, Outfit outfit, BodyBuild build, bool left) {
        var (rings, tones) = LegShape(ankle, outfit, build, left);
        var skeleton = ActorShape.Limb(parent, rings, joint, Palette(tones), ankle);
        skeleton.GetChild<MeshInstance3D>(0).Name = "HumanLeg";
        return skeleton;
    }

    /// <summary>A leg's rings and the colours they take, down to a sole <paramref name="ankle"/> below the hip.</summary>
    internal static (Ring[] Profile, Tone[] Tones) LegShape(float ankle, Outfit outfit, BodyBuild build, bool left) {
        var rings = left ? build.Leg : build.RightLeg ?? build.Leg;
        if (outfit.Socks is not { } socks) return (rings, Tones(outfit.Legs ?? outfit.Skin, outfit.Shoes, "302d29", false, outfit.Legs is not null));
        return (WithSocks(rings, .043f - ankle), [new(outfit.Legs ?? outfit.Skin, false), new(outfit.Skin, false), new(socks, false), new(outfit.Shoes, false)]);
    }
    /// <summary>The edges of the shorts, thigh-high socks and shoes follow the same leg as it bends.</summary>
    private static Ring[] WithSocks(Ring[] leg, float shoeTop) {
        const float shortsHem = -.193f, sockTop = -.282f;
        const float shorts = .125f, skin = .375f, sock = .625f, shoe = .875f;
        var rings = new List<Ring>(leg.Length + 6);
        for (int i = 0; i < leg.Length; i++) {
            var ring = leg[i];
            if (i > 0) {
                if (leg[i - 1].Y > shortsHem && ring.Y < shortsHem) AddEdge(leg[i - 1], ring, shortsHem, shorts, skin);
                if (leg[i - 1].Y > sockTop && ring.Y < sockTop) AddEdge(leg[i - 1], ring, sockTop, skin, sock);
                if (leg[i - 1].Y > shoeTop && ring.Y < shoeTop) AddEdge(leg[i - 1], ring, shoeTop, sock, shoe);
            }
            float tone = ring.Y > shortsHem ? shorts : ring.Y > sockTop ? skin : ring.Y > shoeTop ? sock : shoe;
            rings.Add(ring with { Tone = tone });
        }

        void AddEdge(Ring above, Ring below, float at, float upperTone, float lowerTone) {
            float blend = (at - above.Y) / (below.Y - above.Y);
            var edge = new Ring(at, Mathf.Lerp(above.Width, below.Width, blend), Mathf.Lerp(above.Depth, below.Depth, blend), Neutral,
                Mathf.Lerp(above.Forward, below.Forward, blend), Mathf.Lerp(above.Sideways, below.Sideways, blend), upperTone);
            rings.Add(edge with { Y = at + .0015f });
            rings.Add(edge with { Y = at - .0015f, Tone = lowerTone });
        }
        return rings.ToArray();
    }

    /// <param name="sleeves">How far the shirt covers the arm; with none it is all skin and no weave.</param>
    public static Skeleton3D Arm(Node3D parent, float joint, bool left, string shirt, string skin, Sleeves sleeves, BodyBuild build) {
        var rings = left ? build.LeftArm : build.RightArm;
        var skeleton = ActorShape.Limb(parent, rings, joint, Palette(ArmTones(shirt, skin, sleeves)));
        skeleton.GetChild<MeshInstance3D>(0).Name = left ? "HumanArmLeft" : "HumanArmRight";
        return skeleton;
    }

    /// <summary>An arm's colours from the shoulder: sleeve, forearm and hand.</summary>
    internal static Tone[] ArmTones(string shirt, string skin, Sleeves sleeves) => sleeves switch {
        Sleeves.None => Tones(skin, skin, skin, false, false),
        Sleeves.Short => Tones(shirt, skin, skin, false),
        _ => Tones(shirt, shirt, skin, true),
    };

    /// <param name="middleCloth">Whether the middle tone is cloth too; the weave covers the cloth tones and leaves skin and boots smooth.</param>
    /// <param name="firstCloth">Whether the first tone is cloth; a bare leg's is skin.</param>
    private static Tone[] Tones(string cloth, string middle, string end, bool middleCloth, bool firstCloth = true) =>
        [new(cloth, firstCloth), new(middle, middleCloth), new(end, false)];

    private static StandardMaterial3D Palette(Tone[] colors) {
        string key = string.Join('|', colors);
        if (palettes.TryGetValue(key, out var material)) return material;
        // Texel centres map the mesh to skin, clothing and shoes. The second texture confines weaving to fabric.
        int width = colors.Length;
        var image = Image.CreateFromData(width, 1, false, Image.Format.Rgba8, new byte[width * 4]);
        var weave = Image.CreateFromData(width, 1, false, Image.Format.Rgba8, new byte[width * 4]);
        for (int i = 0; i < width; i++) {
            image.SetPixel(i, 0, new Color(colors[i].Color));
            weave.SetPixel(i, 0, colors[i].Cloth ? Colors.White : Colors.Black);
        }
        material = new StandardMaterial3D { AlbedoTexture = ImageTexture.CreateFromImage(image), DetailMask = ImageTexture.CreateFromImage(weave), Roughness = .85f };
        ModelKit.Dress(material, Surface.Cloth);
        // A limb is a few centimetres wide: plain filtering, since an anisotropic footprint would reach past the end texels and wrap.
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        palettes[key] = material;
        return material;
    }
}
