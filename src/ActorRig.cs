using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;
using static ChopChopMeow.ActorShape;

namespace ChopChopMeow;

public enum ActorSkin { Player, Worker, Courier }

/// <summary>
/// A jointed low-poly person about 1.65 units tall, posed every frame from <see cref="ActorAnimation.PoseAt"/>.
/// The rig faces +Z; the hips are lifted so the lower foot always stays on the ground.
/// </summary>
public sealed class ActorRig
{
    private const float Thigh = .38f, Shin = .37f, Sole = .07f;
    public readonly Node3D Root = new();
    private readonly Node3D hips, torso, head, shoulderL, shoulderR, elbowL, elbowR, legL, legR, kneeL, kneeR, crate, rock;
    private readonly Dictionary<string, Node3D> tools = new();
    private readonly Node3D torch;
    private readonly List<(Skeleton3D Skeleton, Node3D Joint)> smoothLimbs = new();
    /// <summary>The torch flame and its light, animated by the view while the torch burns.</summary>
    public readonly Node3D Flame;
    public readonly OmniLight3D TorchLight;

    public ActorRig(ActorSkin skin)
    {
        var (skinTone, hair, shirt, legs, boots) = skin switch
        {
            ActorSkin.Worker => ("c48d66", "2e2420", "e9dfc6", "3c5e94", "5c3a22"),
            ActorSkin.Courier => ("b98060", "3a2c22", "6d7378", "3b3f45", "2d2a28"),
            _ => ("d9a27c", "5a3b25", "2f7f86", "34465e", "6b4428"),
        };
        bool smooth = skin != ActorSkin.Courier;
        hips = Pivot(Root, new Vector3(0, Thigh + Shin + Sole, 0));
        (legL, kneeL) = Leg(.09f, legs, boots, smooth);
        (legR, kneeR) = Leg(-.09f, legs, boots, smooth);
        torso = Pivot(hips, Vector3.Zero);
        if (smooth)
        {
            var trousers = new Color(legs); var top = new Color(shirt);
            ActorShape.Add(torso, new[] {
                new Ring(.13f, .13f, .085f, trousers), new Ring(.08f, .155f, .1f, trousers),
                new Ring(.02f, .165f, .09f, trousers), new Ring(-.015f, .165f, .088f, trousers) });
            ActorShape.Add(torso, new[] {
                new Ring(.54f, .055f, .05f, top), new Ring(.5f, .12f, .075f, top),
                new Ring(.44f, .177f, .095f, top), new Ring(.35f, .17f, .105f, top),
                new Ring(.23f, .145f, .095f, top), new Ring(.13f, .14f, .09f, top),
                new Ring(.105f, .13f, .085f, top) });
        }
        else
        {
            Box(torso, new Vector3(.3f, .16f, .19f), new Vector3(0, .02f, 0), Mat(legs));
            Add(torso, new CapsuleMesh { Radius = .16f, Height = .52f, RadialSegments = 10, Rings = 3 }, Mat(shirt), new Vector3(0, .3f, 0), null, new Vector3(1, 1, .64f));
        }
        Cylinder(torso, .055f, .05f, .08f, new Vector3(0, .54f, 0), Mat(skinTone));
        head = Pivot(torso, new Vector3(0, .56f, 0));
        Sphere(head, .12f, new Vector3(0, .13f, 0), Mat(skinTone), new Vector3(.95f, 1.05f, 1), smooth ? 16 : 10);
        if (smooth) Sphere(head, .023f, new Vector3(0, .12f, .115f), Mat(skinTone), new Vector3(.7f, 1, 1), 8);
        else Box(head, new Vector3(.035f, .05f, .05f), new Vector3(0, .12f, .12f), Mat(skinTone));
        float shoulder = smooth ? .17f : .19f;
        shoulderL = Pivot(torso, new Vector3(shoulder, .45f, 0));
        shoulderR = Pivot(torso, new Vector3(-shoulder, .45f, 0));
        elbowL = Arm(shoulderL, shirt, skinTone, smooth, skin == ActorSkin.Player);
        elbowR = Arm(shoulderR, shirt, skinTone, smooth, skin == ActorSkin.Player);
        if (!smooth) Box(torso, new Vector3(.31f, .045f, .2f), new Vector3(0, .1f, 0), Mat(legs));
        switch (skin)
        {
            case ActorSkin.Player:
                BuildPlayerDetails(skinTone, hair);
                break;
            case ActorSkin.Worker:
                BuildWorkerDetails(skinTone, hair, legs);
                Sphere(head, .14f, new Vector3(0, .2f, 0), Mat("e8b323", .5f), new Vector3(1, .62f, 1.08f), 16);
                Cylinder(head, .17f, .17f, .02f, new Vector3(0, .17f, .02f), Mat("e8b323", .5f), null, 20);
                break;
            case ActorSkin.Courier:
                Add(torso, new CapsuleMesh { Radius = .165f, Height = .42f, RadialSegments = 10, Rings = 3 }, Mat("f07a1f", .6f), new Vector3(0, .32f, 0), null, new Vector3(1.02f, 1, .67f));
                Box(torso, new Vector3(.34f, .03f, .23f), new Vector3(0, .28f, 0), Mat("e4e6d8", .3f));
                Sphere(head, .13f, new Vector3(0, .17f, 0), Mat("2b5d8a"), new Vector3(1, .6f, 1.02f));
                Box(head, new Vector3(.18f, .02f, .12f), new Vector3(0, .15f, .14f), Mat("2b5d8a"));
                break;
        }
        crate = Pivot(torso, new Vector3(0, .16f, .33f));
        Box(crate, new Vector3(.42f, .28f, .3f), Vector3.Zero, Mat("c9a46e"));
        Box(crate, new Vector3(.44f, .04f, .32f), new Vector3(0, .1f, 0), Mat("a9824f"));
        Box(crate, new Vector3(.44f, .04f, .32f), new Vector3(0, -.1f, 0), Mat("a9824f"));
        // A low boulder to sit on, under the hips.
        rock = Pivot(Root, Vector3.Zero);
        Sphere(rock, .24f, new Vector3(0, .1f, -.04f), Mat("8d918c"), new Vector3(1.15f, .85f, 1));
        Sphere(rock, .13f, new Vector3(.15f, .05f, .1f), Mat("7a7e79"), new Vector3(1, .7f, 1));
        BuildTools();
        (torch, Flame, TorchLight) = BuildTorch();
    }

    /// <summary>Keep face cues and a simple canvas pack; silhouette and smooth transitions carry the design.</summary>
    private void BuildPlayerDetails(string skinTone, string hair)
    {
        Sphere(head, .126f, new Vector3(0, .19f, -.025f), Mat(hair), new Vector3(1, .68f, 1.02f), 16);
        BuildFace(skinTone, hair);
        foreach (float side in new[] { -1f, 1f })
        {
            // Each continuous loop is buried in the pack at both ends and wraps the shoulder and ribs.
            ActorShape.Strap(torso, new[] {
                new Vector3(side * .09f, .46f, -.16f), new Vector3(side * .1f, .497f, -.065f),
                new Vector3(side * .105f, .517f, -.015f), new Vector3(side * .105f, .49f, .055f),
                new Vector3(side * .092f, .39f, .098f), new Vector3(side * .09f, .26f, .091f),
                new Vector3(side * .14f, .205f, .04f), new Vector3(side * .15f, .21f, -.04f),
                new Vector3(side * .115f, .22f, -.15f) });
        }
        var canvas = new Color("6c7849");
        var pack = Pivot(torso, new Vector3(0, .32f, -.17f));
        ActorShape.Add(pack, new[] {
            new Ring(.18f, .025f, .025f, canvas), new Ring(.16f, .115f, .07f, canvas),
            new Ring(.11f, .145f, .09f, canvas), new Ring(-.1f, .14f, .09f, canvas),
            new Ring(-.16f, .11f, .065f, canvas), new Ring(-.18f, .025f, .025f, canvas) });
    }

    private void BuildFace(string skinTone, string hair)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            Sphere(head, .025f, new Vector3(side * .11f, .125f, 0), Mat(skinTone), new Vector3(.7f, 1, .8f), 8);
            Box(head, new Vector3(.022f, .024f, .012f), new Vector3(side * .046f, .145f, .108f), Mat("302b28"));
            Box(head, new Vector3(.036f, .009f, .014f), new Vector3(side * .047f, .171f, .102f), Mat(hair));
        }
    }

    private void BuildWorkerDetails(string skinTone, string hair, string overalls)
    {
        BuildFace(skinTone, hair);
        ActorShape.Bib(torso, overalls);
        foreach (float side in new[] { -1f, 1f })
            ActorShape.Strap(torso, new[] {
                new Vector3(side * .08f, .13f, -.09f), new Vector3(side * .09f, .44f, -.09f),
                new Vector3(side * .09f, .515f, -.02f), new Vector3(side * .08f, .48f, .075f),
                new Vector3(side * .08f, .35f, .1f) }, overalls);
    }

    /// <summary>
    /// A torch held forward in the left fist.
    /// The flame leans back by the forearm's angle in the holding pose, so it burns upright while the arm is raised.
    /// </summary>
    private (Node3D Torch, Node3D Flame, OmniLight3D Light) BuildTorch()
    {
        var holder = Pivot(elbowL, new Vector3(0, -.27f, .01f));
        Cylinder(holder, .024f, .03f, .52f, new Vector3(0, 0, .12f), Mat("6e4a2e", .8f), new Vector3(90, 0, 0), 6);
        Cylinder(holder, .045f, .04f, .1f, new Vector3(0, 0, .37f), Mat("3a2a20", .9f), new Vector3(90, 0, 0), 7);
        var flame = Pivot(holder, new Vector3(0, 0, .42f), new Vector3(90, 0, 0));
        Post(flame, .07f, 0, .24f, Vector3.Zero, Flat(new Color("ff8a2a", .9f)), 7);
        Post(flame, .045f, 0, .17f, new Vector3(.015f, 0, .01f), Flat(new Color("ffd36a", .95f)), 7);
        var light = new OmniLight3D { Position = new Vector3(0, .2f, 0), OmniRange = 5, LightColor = new Color("ff9a45"), LightEnergy = 0, ShadowEnabled = false, OmniAttenuation = 1.4f };
        flame.AddChild(light);
        holder.Visible = false;
        return (holder, flame, light);
    }

    private (Node3D Hip, Node3D Knee) Leg(float x, string trousers, string boots, bool smooth)
    {
        var hip = Pivot(hips, new Vector3(x, -.02f, 0));
        var knee = Pivot(hip, new Vector3(0, -Thigh, 0));
        if (smooth)
        {
            var cloth = new Color(trousers); var leather = new Color(boots); var sole = new Color("302d29");
            var skeleton = ActorShape.Limb(hip, new[] {
                new Ring(.08f, .04f, .04f, cloth), new Ring(.035f, .082f, .085f, cloth),
                new Ring(-.1f, .087f, .09f, cloth), new Ring(-.25f, .075f, .08f, cloth),
                new Ring(-.32f, .068f, .073f, cloth), new Ring(-.38f, .065f, .07f, cloth),
                new Ring(-.44f, .063f, .067f, cloth), new Ring(-.52f, .064f, .067f, cloth),
                new Ring(-.63f, .055f, .058f, cloth), new Ring(-.635f, .057f, .06f, leather),
                new Ring(-.69f, .06f, .065f, leather), new Ring(-.735f, .069f, .108f, leather, .035f),
                new Ring(-.77f, .07f, .123f, leather, .045f), new Ring(-.78f, .071f, .123f, sole, .045f),
                new Ring(-.8f, .058f, .108f, sole, .045f), new Ring(-.8f, .001f, .001f, sole, .045f)
            }, Thigh);
            smoothLimbs.Add((skeleton, knee));
        }
        else
        {
            Limb(hip, .08f, Thigh + .08f, new Vector3(0, .04f, 0), Mat(trousers));
            Limb(knee, .066f, Shin + .05f, new Vector3(0, .03f, 0), Mat(trousers));
            Box(knee, new Vector3(.12f, Sole + .02f, .24f), new Vector3(0, -Shin - Sole / 2 + .01f, .05f), Mat(boots));
        }
        return (hip, knee);
    }

    /// <summary>Builds an arm hanging from the shoulder and returns its elbow; short sleeves show skin below the bicep.</summary>
    private Node3D Arm(Node3D shoulder, string sleeve, string skinTone, bool smooth, bool shortSleeve)
    {
        var elbow = Pivot(shoulder, new Vector3(0, -.28f, 0));
        if (smooth)
        {
            var shirt = new Color(sleeve); var skin = new Color(skinTone);
            var forearm = shortSleeve ? skin : shirt;
            float inward = -MathF.Sign(shoulder.Position.X);
            var skeleton = ActorShape.Limb(shoulder, new[] {
                new Ring(.065f, .012f, .012f, shirt, Sideways: inward * .065f), new Ring(.035f, .06f, .062f, shirt, Sideways: inward * .025f),
                new Ring(-.025f, .077f, .073f, shirt), new Ring(-.11f, .07f, .065f, shirt),
                new Ring(-.16f, .065f, .06f, shirt), new Ring(-.165f, .061f, .057f, forearm),
                new Ring(-.22f, .057f, .054f, forearm), new Ring(-.28f, .051f, .051f, forearm),
                new Ring(-.34f, .057f, .054f, forearm), new Ring(-.41f, .051f, .049f, forearm),
                new Ring(-.49f, .037f, .038f, forearm), new Ring(-.525f, .046f, .04f, skin, .008f),
                new Ring(-.565f, .044f, .041f, skin, .015f), new Ring(-.59f, .026f, .025f, skin, .015f),
                new Ring(-.6f, .001f, .001f, skin, .015f)
            }, .28f);
            smoothLimbs.Add((skeleton, elbow));
        }
        else
        {
            Sphere(shoulder, .078f, Vector3.Zero, Mat(sleeve));
            Limb(shoulder, .066f, .3f, new Vector3(0, .02f, 0), Mat(sleeve));
            Limb(elbow, .055f, .27f, new Vector3(0, .02f, 0), Mat(sleeve));
            Sphere(elbow, .058f, new Vector3(0, -.27f, .01f), Mat(skinTone));
        }
        return elbow;
    }

    /// <summary>Tools hang from the right fist along the forearm; blades face local -Z, the direction of a downward stroke.</summary>
    private void BuildTools()
    {
        foreach (var (name, handle, head, metal) in new[] {
            ("iron_axe", "8a5a32", "5b5f62", .45f), ("steel_axe", "e87d1e", "c9d3d8", .85f),
            ("iron_pick", "8a5a32", "5b5f62", .45f), ("steel_pick", "e87d1e", "c9d3d8", .85f), ("hammer", "8a5a32", "4a4d50", .5f) })
        {
            var tool = Pivot(elbowR, new Vector3(0, -.27f, .01f));
            bool hammer = name == "hammer";
            float length = hammer ? .38f : .72f;
            Cylinder(tool, .022f, .022f, length, new Vector3(0, .08f - length / 2, 0), Mat(handle, .7f));
            if (handle == "e87d1e") Cylinder(tool, .027f, .027f, .16f, new Vector3(0, -.02f, 0), Mat("222222"));
            float end = .06f - length;
            var steel = Mat(head, .35f, metal);
            if (hammer) Box(tool, new Vector3(.06f, .07f, .17f), new Vector3(0, end, -.02f), steel);
            else if (name.EndsWith("axe"))
            {
                Box(tool, new Vector3(.045f, .1f, .15f), new Vector3(0, end, -.05f), steel);
                Box(tool, new Vector3(.03f, .17f, .05f), new Vector3(0, end, -.14f), Mat(name == "steel_axe" ? "e9eef0" : "8d9294", .3f, metal));
            }
            else
            {
                Cylinder(tool, .035f, 0, .3f, new Vector3(0, end, -.15f), steel, new Vector3(-90, 0, 0), 6);
                Cylinder(tool, .035f, 0, .22f, new Vector3(0, end, .11f), steel, new Vector3(90, 0, 0), 6);
            }
            tool.Visible = false;
            tools[name] = tool;
        }
    }

    public void Show(Pose pose, string? tool, bool carrying, bool onRock = false, bool lit = false)
    {
        torso.RotationDegrees = new Vector3(pose.Lean, pose.Twist, 0);
        head.RotationDegrees = new Vector3(pose.Head - pose.Lean * .4f, -pose.Twist * .5f, 0);
        shoulderL.RotationDegrees = new Vector3(-pose.RaiseL, 0, pose.OutL);
        shoulderR.RotationDegrees = new Vector3(-pose.RaiseR, 0, -pose.OutR);
        elbowL.RotationDegrees = new Vector3(-pose.ElbowL, 0, 0);
        elbowR.RotationDegrees = new Vector3(-pose.ElbowR, 0, 0);
        legL.Basis = HipTurn(pose.SwingL, pose.TurnL, pose.RollL);
        legR.Basis = HipTurn(pose.SwingR, -pose.TurnR, -pose.RollR);
        kneeL.RotationDegrees = new Vector3(pose.KneeL, 0, 0);
        kneeR.RotationDegrees = new Vector3(pose.KneeR, 0, 0);
        foreach (var (skeleton, joint) in smoothLimbs)
            skeleton.SetBonePoseRotation(1, joint.Quaternion);
        hips.Position = new Vector3(0, pose.Seat > 0 ? pose.Seat : Math.Max(Reach(pose.SwingL, pose.KneeL), Reach(pose.SwingR, pose.KneeR)) + Sole, 0);
        rock.Visible = onRock;
        foreach (var (name, node) in tools) node.Visible = name == tool;
        crate.Visible = carrying;
        torch.Visible = lit;
    }

    /// <summary>Splays the thigh outward, swings it forward, then rolls it about its own length; the left side turns toward +X.</summary>
    private static Basis HipTurn(float swing, float turn, float roll) =>
        new Basis(Vector3.Up, Mathf.DegToRad(turn)) * new Basis(Vector3.Right, Mathf.DegToRad(-swing)) * new Basis(Vector3.Up, Mathf.DegToRad(roll));

    private static float Reach(float swing, float knee) =>
        Thigh * MathF.Cos(Mathf.DegToRad(swing)) + Shin * MathF.Cos(Mathf.DegToRad(swing - knee));
}
