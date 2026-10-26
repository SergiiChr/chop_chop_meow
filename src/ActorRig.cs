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
public sealed class ActorRig {
    private const float Thigh = .38f, Shin = .37f, Sole = .07f;
    public readonly Node3D Root = new();
    private readonly Node3D hips, torso, head, shoulderL, shoulderR, elbowL, elbowR, legL, legR, kneeL, kneeR, crate, rock;
    private readonly Dictionary<string, Node3D> tools = new();
    private readonly Node3D torch;
    private readonly List<(Skeleton3D Skeleton, Node3D Joint)> limbs = new();
    /// <summary>The torch flame and its light, animated by the view while the torch burns.</summary>
    public readonly Node3D Flame;
    public readonly OmniLight3D TorchLight;

    /// <param name="shirtColor">Tells players apart; each skin has its own otherwise.</param>
    public ActorRig(ActorSkin skin, string? shirtColor = null) {
        var (skinTone, hair, shirt, legs, boots) = skin switch {
            ActorSkin.Worker => ("c48d66", "2e2420", "e9dfc6", "3c5e94", "5c3a22"),
            ActorSkin.Courier => ("b98060", "3a2c22", "6d7378", "3b3f45", "2d2a28"),
            _ => ("d9a27c", "5a3b25", "2f7f86", "34465e", "6b4428"),
        };
        shirt = shirtColor ?? shirt;
        hips = Pivot(Root, new Vector3(0, Thigh + Shin + Sole, 0));
        (legL, kneeL) = Leg(.09f, legs, boots);
        (legR, kneeR) = Leg(-.09f, legs, boots);
        torso = Pivot(hips, Vector3.Zero);
        HumanShape.Torso(torso, shirt, legs);
        Cylinder(torso, .055f, .05f, .08f, new Vector3(0, .54f, 0), Mat(skinTone));
        head = Pivot(torso, new Vector3(0, .56f, 0));
        Sphere(head, .12f, new Vector3(0, .13f, 0), Mat(skinTone), new Vector3(.95f, 1.05f, 1), 16);
        Sphere(head, .023f, new Vector3(0, .12f, .115f), Mat(skinTone), new Vector3(.7f, 1, 1), 8);
        const float shoulder = .17f;
        shoulderL = Pivot(torso, new Vector3(shoulder, .45f, 0));
        shoulderR = Pivot(torso, new Vector3(-shoulder, .45f, 0));
        elbowL = Arm(shoulderL, shirt, skinTone, skin == ActorSkin.Player);
        elbowR = Arm(shoulderR, shirt, skinTone, skin == ActorSkin.Player);
        switch (skin) {
            case ActorSkin.Player:
                BuildPlayerDetails(skinTone, hair);
                break;
            case ActorSkin.Worker:
                BuildWorkerDetails(skinTone, hair, legs);
                Sphere(head, .14f, new Vector3(0, .2f, 0), Mat("e8b323", .5f), new Vector3(1, .62f, 1.08f), 16);
                Cylinder(head, .17f, .17f, .02f, new Vector3(0, .17f, .02f), Mat("e8b323", .5f), null, 20);
                break;
            case ActorSkin.Courier:
                BuildFace(skinTone, hair);
                Add(torso, CourierShape.Vest, CourierShape.VestMaterial, Vector3.Zero);
                Add(head, CourierShape.Cap, CourierShape.CapMaterial, Vector3.Zero);
                break;
        }
        crate = Pivot(torso, new Vector3(0, .37f, .33f));
        // Reuse the compact shipping crate, fitted to the existing centered carry volume.
        Add(crate, CrateShape.Cargo, CrateShape.Timber, new Vector3(0, -.14f, 0), scale: new Vector3(.7f, .62f, .73f));
        // A low boulder to sit on, under the hips.
        rock = Pivot(Root, Vector3.Zero);
        Sphere(rock, .24f, new Vector3(0, .1f, -.04f), Mat("8d918c"), new Vector3(1.15f, .85f, 1));
        Sphere(rock, .13f, new Vector3(.15f, .05f, .1f), Mat("7a7e79"), new Vector3(1, .7f, 1));
        BuildTools();
        (torch, Flame, TorchLight) = BuildTorch();
    }

    /// <summary>Keep face cues and a simple canvas pack; silhouette and smooth transitions carry the design.</summary>
    private void BuildPlayerDetails(string skinTone, string hair) {
        Sphere(head, .126f, new Vector3(0, .19f, -.025f), Mat(hair), new Vector3(1, .68f, 1.02f), 16);
        BuildFace(skinTone, hair);
        foreach (float side in new[] { -1f, 1f }) {
            // Each continuous loop is buried in the pack at both ends and wraps the shoulder and ribs.
            ActorShape.Strap(torso, new[] {
                new Vector3(side * .09f, .46f, -.16f), new Vector3(side * .1f, .497f, -.065f),
                new Vector3(side * .105f, .517f, -.015f), new Vector3(side * .105f, .49f, .055f),
                new Vector3(side * .092f, .39f, .098f), new Vector3(side * .09f, .26f, .091f),
                new Vector3(side * .14f, .205f, .04f), new Vector3(side * .15f, .21f, -.04f),
                new Vector3(side * .115f, .22f, -.15f) });
        }
        var pack = Pivot(torso, new Vector3(0, .32f, -.17f));
        ActorShape.Backpack(pack);
    }

    private void BuildFace(string skinTone, string hair) {
        foreach (float side in new[] { -1f, 1f }) {
            Sphere(head, .025f, new Vector3(side * .11f, .125f, 0), Mat(skinTone), new Vector3(.7f, 1, .8f), 8);
            Box(head, new Vector3(.022f, .024f, .012f), new Vector3(side * .046f, .145f, .108f), Mat("302b28"));
            Box(head, new Vector3(.036f, .009f, .014f), new Vector3(side * .047f, .171f, .102f), Mat(hair));
        }
    }

    private void BuildWorkerDetails(string skinTone, string hair, string overalls) {
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
    private (Node3D Torch, Node3D Flame, OmniLight3D Light) BuildTorch() {
        var (holder, flame, light) = Torch();
        holder.Position = new Vector3(0, -.27f, .01f);
        elbowL.AddChild(holder);
        holder.Visible = false;
        return (holder, flame, light);
    }

    /// <summary>A torch gripped at the origin, shaft along +Z, with its unlit flame light; the icon renderer shows it on its own.</summary>
    public static (Node3D Torch, Node3D Flame, OmniLight3D Light) Torch() {
        var holder = new Node3D();
        Cylinder(holder, .024f, .03f, .52f, new Vector3(0, 0, .12f), Mat("6e4a2e", .8f), new Vector3(90, 0, 0), 6);
        Cylinder(holder, .045f, .04f, .1f, new Vector3(0, 0, .37f), Mat("3a2a20", .9f), new Vector3(90, 0, 0), 7);
        var flame = Pivot(holder, new Vector3(0, 0, .42f), new Vector3(90, 0, 0));
        Post(flame, .07f, 0, .24f, Vector3.Zero, Flat(new Color("ff8a2a", .9f)), 7);
        Post(flame, .045f, 0, .17f, new Vector3(.015f, 0, .01f), Flat(new Color("ffd36a", .95f)), 7);
        var light = new OmniLight3D { Position = new Vector3(0, .2f, 0), OmniRange = 5, LightColor = new Color("ff9a45"), LightEnergy = 0, ShadowEnabled = false, OmniAttenuation = 1.4f };
        flame.AddChild(light);
        return (holder, flame, light);
    }

    private (Node3D Hip, Node3D Knee) Leg(float x, string trousers, string boots) {
        var hip = Pivot(hips, new Vector3(x, -.02f, 0));
        var knee = Pivot(hip, new Vector3(0, -Thigh, 0));
        limbs.Add((HumanShape.Leg(hip, Thigh, trousers, boots), knee));
        return (hip, knee);
    }

    /// <summary>Every role shares the continuous arm and its skin weights; the palette selects sleeve length.</summary>
    private Node3D Arm(Node3D shoulder, string sleeve, string skinTone, bool shortSleeve) {
        var elbow = Pivot(shoulder, new Vector3(0, -.28f, 0));
        limbs.Add((HumanShape.Arm(shoulder, .28f, shoulder.Position.X > 0, sleeve, skinTone, shortSleeve), elbow));
        return elbow;
    }

    /// <summary>Tools hang from the right fist along the forearm; blades face local -Z, the direction of a downward stroke.</summary>
    private void BuildTools() {
        foreach (var name in new[] { "steel_axe", "steel_pick", "wood_hammer", "steel_hammer", "protest_sign" }) {
            var tool = name == "protest_sign" ? ProtestSign() : Tool(name);
            tool.Position = new Vector3(0, -.27f, .01f);
            elbowR.AddChild(tool);
            tool.Visible = false;
            tools[name] = tool;
        }
    }

    /// <summary>A held tool gripped at the origin, handle down along -Y and blade toward -Z; the icon renderer shows it on its own.</summary>
    public static Node3D Tool(string name) {
        if (name is "steel_hammer" or "wood_hammer") return Hammer(name == "steel_hammer");
        if (name is "stone_chisel" or "steel_chisel") return Chisel(name == "steel_chisel");
        bool steelTool = name.StartsWith("steel");
        string handle = steelTool ? "e87d1e" : "8a5a32", head = steelTool ? "c9d3d8" : "5b5f62";
        float metal = steelTool ? .85f : .45f;
        var tool = new Node3D();
        const float length = .72f;
        Cylinder(tool, .022f, .022f, length, new Vector3(0, .08f - length / 2, 0), Mat(handle, .7f));
        if (steelTool) Cylinder(tool, .027f, .027f, .16f, new Vector3(0, -.02f, 0), Mat("222222"));
        float end = .06f - length;
        var steel = Mat(head, .35f, metal);
        if (name.EndsWith("axe")) {
            Box(tool, new Vector3(.045f, .1f, .15f), new Vector3(0, end, -.05f), steel);
            Box(tool, new Vector3(.03f, .17f, .05f), new Vector3(0, end, -.14f), Mat(steelTool ? "e9eef0" : "8d9294", .3f, metal));
        } else {
            Cylinder(tool, .035f, 0, .3f, new Vector3(0, end, -.15f), steel, new Vector3(-90, 0, 0), 6);
            Cylinder(tool, .035f, 0, .22f, new Vector3(0, end, .11f), steel, new Vector3(90, 0, 0), 6);
        }
        return tool;
    }

    /// <summary>
    /// A claw hammer with a rubber grip, or a wooden mallet with a banded barrel head, gripped at the origin with the striking face toward -Z.
    /// </summary>
    private static Node3D Hammer(bool steel) {
        var tool = new Node3D();
        const float length = .38f, end = .06f - length;
        Cylinder(tool, .02f, .024f, length, new Vector3(0, .08f - length / 2, 0), Mat(steel ? "e87d1e" : "8a5a32", .7f));
        if (steel) {
            var metal = Mat("c9d3d8", .35f, .85f);
            Cylinder(tool, .027f, .027f, .12f, new Vector3(0, -.01f, 0), Mat("222222"));
            Cylinder(tool, .028f, .028f, .1f, new Vector3(0, end, -.03f), metal, new Vector3(90, 0, 0), 8);
            Cylinder(tool, .034f, .034f, .02f, new Vector3(0, end, -.085f), metal, new Vector3(90, 0, 0), 8);
            // The claw tapers away from the face and curves down toward the handle.
            Cylinder(tool, .024f, .006f, .1f, new Vector3(0, end - .015f, .06f), metal, new Vector3(110, 0, 0), 6).Scale = new Vector3(.6f, 1, 1);
        } else {
            Cylinder(tool, .052f, .052f, .15f, new Vector3(0, end, 0), Mat("b98552", .8f), new Vector3(90, 0, 0), 10);
            foreach (float z in new[] { -.06f, .06f }) Cylinder(tool, .055f, .055f, .016f, new Vector3(0, end, z), Mat("7a5233", .8f), new Vector3(90, 0, 0), 10);
        }
        return tool;
    }

    /// <summary>
    /// A steel chisel with a capped handle and flat blade, or a knapped stone blade bound into a wooden handle with cord.
    /// It is gripped at the origin with the blade along -Y, wide along Z and thin along X, so it lies flat when turned onto its side.
    /// </summary>
    private static Node3D Chisel(bool steel) {
        var tool = new Node3D();
        if (steel) {
            var metal = Mat("c9d3d8", .3f, .85f);
            Cylinder(tool, .02f, .025f, .14f, Vector3.Zero, Mat("e87d1e", .7f), sides: 8);
            Cylinder(tool, .026f, .026f, .02f, new Vector3(0, .075f, 0), Mat("222222"), sides: 8);
            Cylinder(tool, .021f, .021f, .02f, new Vector3(0, -.08f, 0), metal, sides: 8);
            Box(tool, new Vector3(.016f, .1f, .016f), new Vector3(0, -.14f, 0), metal);
            Box(tool, new Vector3(.012f, .06f, .05f), new Vector3(0, -.21f, 0), metal);
            Box(tool, new Vector3(.005f, .016f, .05f), new Vector3(0, -.248f, 0), Mat("e9eef0", .25f, .85f));
        } else {
            var stone = Mat("9aa29f", .9f);
            Cylinder(tool, .02f, .024f, .18f, new Vector3(0, -.01f, 0), Mat("8a5a32", .8f), sides: 8);
            // The blade's butt sits in the split handle end under the cord wrap.
            Cylinder(tool, .014f, .032f, .12f, new Vector3(0, -.15f, 0), stone, sides: 5).Scale = new Vector3(.45f, 1, 1);
            Cylinder(tool, .028f, .028f, .05f, new Vector3(0, -.1f, 0), Mat("c9b48a", .95f), sides: 8);
        }
        return tool;
    }

    /// <summary>
    /// A cardboard sign on a pole gripped at the origin, pole along +Z like the torch, so a level forearm holds it upright.
    /// The board faces local -Y, which is forward in that pose, and reads the same from behind.
    /// </summary>
    private static Node3D ProtestSign() {
        var sign = new Node3D();
        Cylinder(sign, .02f, .02f, 1.3f, new Vector3(0, .033f, .45f), Mat("8a5a32", .8f), new Vector3(90, 0, 0), 6);
        Box(sign, new Vector3(.72f, .025f, .4f), new Vector3(0, 0, 1.08f), Mat("d9c49a", .95f));
        foreach (float side in new[] { -1f, 1f })
            sign.AddChild(new Label3D {
                Text = "No pay -\nno work",
                // Text faces the label's +Z, so each side turns it out of the board with its top along the pole.
                Transform = new Transform3D(new Basis(new Vector3(-side, 0, 0), new Vector3(0, 0, 1), new Vector3(0, side, 0)), new Vector3(0, side * .014f, 1.08f)),
                FontSize = 40,
                PixelSize = .003f,
                Modulate = new Color("2b211b"),
                OutlineSize = 0,
                DoubleSided = false,
                Shaded = true
            });
        return sign;
    }

    public void Show(Pose pose, string? tool, bool carrying, bool onRock = false, bool lit = false) {
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
        foreach (var (skeleton, joint) in limbs)
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
