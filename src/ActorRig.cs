using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

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
    private readonly Node3D hips, torso, head, shoulderL, shoulderR, elbowL, elbowR, legL, legR, kneeL, kneeR, crate;
    private readonly Dictionary<string, Node3D> tools = new();

    public ActorRig(ActorSkin skin)
    {
        var (skinTone, hair, shirt, legs, boots) = skin switch
        {
            ActorSkin.Worker => ("c48d66", "2e2420", "e9dfc6", "3c5e94", "5c3a22"),
            ActorSkin.Courier => ("b98060", "3a2c22", "6d7378", "3b3f45", "2d2a28"),
            _ => ("d9a27c", "5a3b25", "2f7f86", "34465e", "6b4428"),
        };
        hips = Pivot(Root, new Vector3(0, Thigh + Shin + Sole, 0));
        (legL, kneeL) = Leg(.09f, legs, boots);
        (legR, kneeR) = Leg(-.09f, legs, boots);
        torso = Pivot(hips, Vector3.Zero);
        Box(torso, new Vector3(.3f, .16f, .19f), new Vector3(0, .02f, 0), Mat(legs));
        Add(torso, new CapsuleMesh { Radius = .16f, Height = .52f, RadialSegments = 10, Rings = 3 }, Mat(shirt), new Vector3(0, .3f, 0), null, new Vector3(1, 1, .64f));
        Cylinder(torso, .055f, .05f, .08f, new Vector3(0, .54f, 0), Mat(skinTone));
        head = Pivot(torso, new Vector3(0, .56f, 0));
        Sphere(head, .12f, new Vector3(0, .13f, 0), Mat(skinTone), new Vector3(.95f, 1.05f, 1));
        Box(head, new Vector3(.035f, .05f, .05f), new Vector3(0, .12f, .12f), Mat(skinTone));
        shoulderL = Pivot(torso, new Vector3(.19f, .45f, 0));
        shoulderR = Pivot(torso, new Vector3(-.19f, .45f, 0));
        elbowL = Arm(shoulderL, shirt, skinTone, skin == ActorSkin.Player);
        elbowR = Arm(shoulderR, shirt, skinTone, skin == ActorSkin.Player);
        Box(torso, new Vector3(.31f, .045f, .2f), new Vector3(0, .1f, 0), Mat(skin == ActorSkin.Player ? "4a3020" : legs));
        switch (skin)
        {
            case ActorSkin.Player:
                Sphere(head, .128f, new Vector3(0, .17f, -.02f), Mat(hair), new Vector3(1, .75f, 1.02f));
                Box(head, new Vector3(.16f, .05f, .04f), new Vector3(0, .06f, .09f), Mat(hair));
                // A small daypack sells the 24-slot backpack without hiding the shirt colour.
                Box(torso, new Vector3(.26f, .3f, .12f), new Vector3(0, .3f, -.16f), Mat("5f6b3a"));
                Box(torso, new Vector3(.2f, .1f, .04f), new Vector3(0, .22f, -.23f), Mat("4d5730"));
                break;
            case ActorSkin.Worker:
                Box(torso, new Vector3(.22f, .22f, .04f), new Vector3(0, .26f, .1f), Mat(legs));
                Box(torso, new Vector3(.04f, .26f, .2f), new Vector3(.08f, .4f, 0), Mat(legs));
                Box(torso, new Vector3(.04f, .26f, .2f), new Vector3(-.08f, .4f, 0), Mat(legs));
                Sphere(head, .14f, new Vector3(0, .2f, 0), Mat("e8b323", .5f), new Vector3(1, .62f, 1.08f));
                Cylinder(head, .17f, .17f, .02f, new Vector3(0, .17f, .02f), Mat("e8b323", .5f), null, 14);
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
        BuildTools();
    }

    private (Node3D Hip, Node3D Knee) Leg(float x, string trousers, string boots)
    {
        var hip = Pivot(hips, new Vector3(x, -.02f, 0));
        Limb(hip, .08f, Thigh + .08f, new Vector3(0, .04f, 0), Mat(trousers));
        var knee = Pivot(hip, new Vector3(0, -Thigh, 0));
        Limb(knee, .066f, Shin + .05f, new Vector3(0, .03f, 0), Mat(trousers));
        Box(knee, new Vector3(.12f, Sole + .02f, .24f), new Vector3(0, -Shin - Sole / 2 + .01f, .05f), Mat(boots));
        return (hip, knee);
    }

    /// <summary>Builds an arm hanging from the shoulder and returns its elbow; short sleeves show skin below the bicep.</summary>
    private static Node3D Arm(Node3D shoulder, string sleeve, string skinTone, bool shortSleeve)
    {
        Sphere(shoulder, .078f, Vector3.Zero, Mat(sleeve));
        Limb(shoulder, .066f, shortSleeve ? .17f : .3f, new Vector3(0, .02f, 0), Mat(sleeve));
        if (shortSleeve) Limb(shoulder, .058f, .18f, new Vector3(0, -.1f, 0), Mat(skinTone));
        var elbow = Pivot(shoulder, new Vector3(0, -.28f, 0));
        Limb(elbow, .055f, .27f, new Vector3(0, .02f, 0), Mat(shortSleeve ? skinTone : sleeve));
        Sphere(elbow, .058f, new Vector3(0, -.27f, .01f), Mat(skinTone));
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

    public void Show(Pose pose, string? tool, bool carrying)
    {
        torso.RotationDegrees = new Vector3(pose.Lean, pose.Twist, 0);
        head.RotationDegrees = new Vector3(pose.Head - pose.Lean * .4f, -pose.Twist * .5f, 0);
        shoulderL.RotationDegrees = new Vector3(-pose.RaiseL, 0, pose.OutL);
        shoulderR.RotationDegrees = new Vector3(-pose.RaiseR, 0, -pose.OutR);
        elbowL.RotationDegrees = new Vector3(-pose.ElbowL, 0, 0);
        elbowR.RotationDegrees = new Vector3(-pose.ElbowR, 0, 0);
        legL.RotationDegrees = new Vector3(-pose.SwingL, 0, 0);
        legR.RotationDegrees = new Vector3(-pose.SwingR, 0, 0);
        kneeL.RotationDegrees = new Vector3(pose.KneeL, 0, 0);
        kneeR.RotationDegrees = new Vector3(pose.KneeR, 0, 0);
        hips.Position = new Vector3(0, Math.Max(Reach(pose.SwingL, pose.KneeL), Reach(pose.SwingR, pose.KneeR)) + Sole, 0);
        foreach (var (name, node) in tools) node.Visible = name == tool;
        crate.Visible = carrying;
    }

    private static float Reach(float swing, float knee) =>
        Thigh * MathF.Cos(Mathf.DegToRad(swing)) + Shin * MathF.Cos(Mathf.DegToRad(swing - knee));
}
