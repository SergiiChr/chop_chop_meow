using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ActorShape;

namespace ChopChopMeow;

/// <summary>One neutral human core: outfits use palette textures while geometry and skin weights are shared.</summary>
internal static class HumanShape {
    private const float Cloth = 1f / 6, Middle = .5f, End = 5f / 6;
    private static readonly Color Neutral = Colors.White;
    private static readonly Dictionary<(string, string, string), StandardMaterial3D> palettes = new();
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

    public static void Torso(Node3D parent, string shirt, string trousers) {
        ActorShape.Add(parent, hips, material: ModelKit.Mat(trousers)).Name = "HumanHips";
        ActorShape.Add(parent, torso, material: ModelKit.Mat(shirt)).Name = "HumanTorso";
    }
    public static Skeleton3D Leg(Node3D parent, float joint, string trousers, string boots) {
        var skeleton = ActorShape.Limb(parent, leg, joint, Palette(trousers, boots, "302d29"));
        skeleton.GetChild<MeshInstance3D>(0).Name = "HumanLeg";
        return skeleton;
    }
    public static Skeleton3D Arm(Node3D parent, float joint, bool left, string shirt, string skin, bool shortSleeve) {
        var skeleton = ActorShape.Limb(parent, left ? leftArm : rightArm, joint, Palette(shirt, shortSleeve ? skin : shirt, skin));
        skeleton.GetChild<MeshInstance3D>(0).Name = left ? "HumanArmLeft" : "HumanArmRight";
        return skeleton;
    }
    private static StandardMaterial3D Palette(string cloth, string middle, string end) {
        var key = (cloth, middle, end);
        if (palettes.TryGetValue(key, out var material)) return material;
        // Texel centers map the same neutral limb to clothing, exposed skin, boots and soles.
        // StandardMaterial3D keeps the existing fog/trade-fade path and blends the original color transitions.
        var image = Image.CreateFromData(3, 1, false, Image.Format.Rgba8, new byte[12]);
        image.SetPixel(0, 0, new Color(cloth)); image.SetPixel(1, 0, new Color(middle)); image.SetPixel(2, 0, new Color(end));
        material = new StandardMaterial3D { AlbedoTexture = ImageTexture.CreateFromImage(image), TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false, Roughness = .85f };
        palettes[key] = material;
        return material;
    }
}
