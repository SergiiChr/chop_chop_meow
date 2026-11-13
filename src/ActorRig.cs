using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

public enum ActorSkin { Player, Worker, Courier, Boss }

/// <summary>
/// A jointed low-poly person about 1.65 units tall, posed every frame from <see cref="ActorAnimation.PoseAt"/>.
/// The rig faces +Z; the hips are lifted so the lower foot always stays on the ground.
/// </summary>
public sealed class ActorRig {
    public readonly Node3D Root = new();
    /// <summary>How far out from the spine and up from the hips a shoulder sits, and the lengths from shoulder to elbow and elbow to fist, in metres.</summary>
    public const float ShoulderOut = .17f, ShoulderUp = .45f, UpperArm = .28f, Forearm = .27f;
    /// <summary>How far ahead of the forearm's line a held thing sits in the fist, in metres.</summary>
    public const float Palm = .01f;
    private readonly Node3D hips, torso, head, shoulderL, shoulderR, elbowL, elbowR, legL, legR, kneeL, kneeR, crate, rock;
    /// <summary>What each tool puts in the hands, by its name: one piece, or two for a tool that fills both.</summary>
    private readonly List<(string Name, Node3D Node)> tools = new();
    private readonly Node3D torch;
    private Node3D shovel = null!;
    private readonly List<(Skeleton3D Skeleton, Node3D Joint)> limbs = new();
    // The leg skins, whose third bone turns the foot at the ankle.
    private readonly Skeleton3D footL, footR;
    /// <summary>The torch flame and its light, animated by the view while the torch burns.</summary>
    public readonly Node3D Flame;
    public readonly OmniLight3D TorchLight;
    /// <summary>The torso and head, for a film to dress and turn beyond what a <see cref="Pose"/> holds.</summary>
    public Node3D Torso => torso;
    public Node3D Head => head;
    /// <summary>The player's backpack and its straps, which a film hides before the journey starts; null on other skins.</summary>
    public Node3D? Pack { get; private set; }

    /// <param name="shirtColor">Tells players apart; each skin has its own otherwise.</param>
    public ActorRig(ActorSkin skin, string? shirtColor = null) {
        var (skinTone, hair, shirt, legs, boots) = skin switch {
            ActorSkin.Worker => ("c48d66", "2e2420", "e9dfc6", "3c5e94", "5c3a22"),
            ActorSkin.Courier => ("b98060", "3a2c22", "6d7378", "3b3f45", "2d2a28"),
            ActorSkin.Boss => ("dba083", "8c8a86", "ece8dc", "2c2f36", "1d1c1c"),
            _ => ("d9a27c", "5a3b25", "2f7f86", "34465e", "6b4428"),
        };
        shirt = shirtColor ?? shirt;
        hips = Pivot(Root, new Vector3(0, ActorAnimation.Thigh + ActorAnimation.Shin + ActorAnimation.Sole, 0));
        (legL, kneeL, footL) = Leg(.09f, legs, boots);
        (legR, kneeR, footR) = Leg(-.09f, legs, boots);
        torso = Pivot(hips, Vector3.Zero);
        HumanShape.Torso(torso, shirt, legs);
        Cylinder(torso, .055f, .05f, .08f, new Vector3(0, .54f, 0), Mat(skinTone));
        head = Pivot(torso, new Vector3(0, .56f, 0));
        Sphere(head, .12f, new Vector3(0, .13f, 0), Mat(skinTone), new Vector3(.95f, 1.05f, 1), 16);
        Sphere(head, .023f, new Vector3(0, .12f, .115f), Mat(skinTone), new Vector3(.7f, 1, 1), 8);
        shoulderL = Pivot(torso, new Vector3(ShoulderOut, ShoulderUp, 0));
        shoulderR = Pivot(torso, new Vector3(-ShoulderOut, ShoulderUp, 0));
        elbowL = Arm(shoulderL, shirt, skinTone, skin == ActorSkin.Player);
        elbowR = Arm(shoulderR, shirt, skinTone, skin == ActorSkin.Player);
        switch (skin) {
            case ActorSkin.Player:
                Pack = BuildPlayerDetails(skinTone, hair);
                break;
            case ActorSkin.Worker:
                BuildWorkerDetails(skinTone, hair, legs);
                HardHat(head);
                break;
            case ActorSkin.Courier:
                BuildFace(skinTone, hair);
                Add(torso, CourierShape.Vest, CourierShape.VestMaterial, Vector3.Zero);
                Add(head, CourierShape.Cap, CourierShape.CapMaterial, Vector3.Zero);
                break;
            case ActorSkin.Boss:
                BuildBossDetails(skinTone, hair, shirt);
                break;
        }
        crate = Pivot(torso, new Vector3(0, .37f, .33f));
        // Reuse the compact shipping crate, fitted to the existing centered carry volume.
        Add(crate, CrateShape.Cargo, CrateShape.Timber, new Vector3(0, -.14f, 0), scale: new Vector3(.7f, .62f, .73f));
        // A low boulder to sit on, under the hips.
        rock = Pivot(Root, Vector3.Zero);
        Sphere(rock, .24f, new Vector3(0, .1f, -.04f), Stone("8d918c", .85f), new Vector3(1.15f, .85f, 1));
        Sphere(rock, .13f, new Vector3(.15f, .05f, .1f), Stone("7a7e79", .85f), new Vector3(1, .7f, 1));
        BuildTools();
        (torch, Flame, TorchLight) = BuildTorch();
        BuildMortar();
    }

    /// <summary>
    /// The mortar's bowl standing on the left fist and its pestle in the right.
    /// Each is turned against its forearm in the grinding pose, so there the bowl sits level and the pestle leans from the bowl's floor up to the fist.
    /// </summary>
    private void BuildMortar() {
        Node3D bowl = new() { Visible = false }, pestle = new() { Visible = false };
        Hold(bowl, right: false); Hold(pestle);
        tools.Add(("mortar", bowl)); tools.Add(("mortar", pestle));
        // The rig stands in the grinding pose to read how its forearms are turned there, until it is first shown.
        Show(ActorAnimation.GrindRest, null, false);
        bowl.Basis = (torso.Basis * shoulderL.Basis * elbowL.Basis).Inverse();
        pestle.Basis = (torso.Basis * shoulderR.Basis * elbowR.Basis).Inverse() * new Basis(Vector3.Back, Mathf.DegToRad(ActorAnimation.PestleLean));
        Add(bowl, CookShape.Bowl, CookShape.Ceramic, new Vector3(0, ActorAnimation.BowlOverFist, 0));
        Add(pestle, CookShape.Pestle, CookShape.Ceramic, new Vector3(0, -ActorAnimation.PestleGrip, 0));
    }

    /// <summary>Keep face cues and a simple canvas pack; silhouette and smooth transitions carry the design.</summary>
    private Node3D BuildPlayerDetails(string skinTone, string hair) {
        Sphere(head, .126f, new Vector3(0, .19f, -.025f), Mat(hair), new Vector3(1, .68f, 1.02f), 16);
        BuildFace(skinTone, hair);
        var worn = Pivot(torso, Vector3.Zero);
        foreach (float side in new[] { -1f, 1f }) {
            // Each continuous loop is buried in the pack at both ends and wraps the shoulder and ribs.
            ActorShape.Strap(worn, new[] {
                new Vector3(side * .09f, .46f, -.16f), new Vector3(side * .1f, .497f, -.065f),
                new Vector3(side * .105f, .517f, -.015f), new Vector3(side * .105f, .49f, .055f),
                new Vector3(side * .092f, .39f, .098f), new Vector3(side * .09f, .26f, .091f),
                new Vector3(side * .14f, .205f, .04f), new Vector3(side * .15f, .21f, -.04f),
                new Vector3(side * .115f, .22f, -.15f) });
        }
        var pack = Pivot(worn, new Vector3(0, .32f, -.17f));
        ActorShape.Backpack(pack);
        return worn;
    }

    /// <summary>An office boss: a grey fringe around a bald crown, a moustache, a paunch under the shirt and a tie.</summary>
    private void BuildBossDetails(string skinTone, string hair, string shirt) {
        BuildFace(skinTone, hair);
        Sphere(head, .122f, new Vector3(0, .15f, -.04f), Mat(hair), new Vector3(1.02f, .62f, .88f), 16);
        Box(head, new Vector3(.075f, .016f, .014f), new Vector3(0, .088f, .112f), Mat(hair));
        Sphere(torso, .15f, new Vector3(0, .23f, .045f), Cloth(shirt), new Vector3(1.05f, 1, .85f), 12);
        Tie(torso, "8e2a2a").Position += new Vector3(0, 0, .045f);
    }

    /// <summary>A knotted tie down the front of a shirt; returned so it can be loosened or taken off.</summary>
    public static Node3D Tie(Node3D torso, string color) {
        var tie = Pivot(torso, Vector3.Zero);
        Box(tie, new Vector3(.045f, .035f, .02f), new Vector3(0, .5f, .078f), Cloth(color));
        Box(tie, new Vector3(.05f, .25f, .012f), new Vector3(0, .365f, .1f), Cloth(color), new Vector3(-5, 0, 0));
        return tie;
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
        Hold(holder, right: false);
        holder.Visible = false;
        return (holder, flame, light);
    }

    /// <summary>A torch gripped at the origin, shaft along +Z, with its unlit flame light; the icon renderer shows it on its own.</summary>
    public static (Node3D Torch, Node3D Flame, OmniLight3D Light) Torch() {
        var holder = new Node3D();
        Cylinder(holder, .024f, .03f, .52f, new Vector3(0, 0, .12f), Wood("6e4a2e", .8f), new Vector3(90, 0, 0), 6);
        Cylinder(holder, .045f, .04f, .1f, new Vector3(0, 0, .37f), Wood("3a2a20", .9f), new Vector3(90, 0, 0), 7);
        var flame = Pivot(holder, new Vector3(0, 0, .42f), new Vector3(90, 0, 0));
        // The flame wraps its own light, so it casts no shadow; everything else around the torch does.
        Post(flame, .07f, 0, .24f, Vector3.Zero, Flat(new Color("ff8a2a", .9f)), 7).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Post(flame, .045f, 0, .17f, new Vector3(.015f, 0, .01f), Flat(new Color("ffd36a", .95f)), 7).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var light = new OmniLight3D { Position = new Vector3(0, .2f, 0), OmniRange = 5, LightColor = new Color("ff9a45"), LightEnergy = 0, ShadowEnabled = true, OmniAttenuation = 1.4f };
        flame.AddChild(light);
        return (holder, flame, light);
    }

    private (Node3D Hip, Node3D Knee, Skeleton3D Skin) Leg(float x, string trousers, string boots) {
        var hip = Pivot(hips, new Vector3(x, -.02f, 0));
        var knee = Pivot(hip, new Vector3(0, -ActorAnimation.Thigh, 0));
        var skin = HumanShape.Leg(hip, ActorAnimation.Thigh, ActorAnimation.Thigh + ActorAnimation.Shin, trousers, boots);
        limbs.Add((skin, knee));
        return (hip, knee, skin);
    }

    /// <summary>Every role shares the continuous arm and its skin weights; the palette selects sleeve length.</summary>
    private Node3D Arm(Node3D shoulder, string sleeve, string skinTone, bool shortSleeve) {
        var elbow = Pivot(shoulder, new Vector3(0, -UpperArm, 0));
        limbs.Add((HumanShape.Arm(shoulder, UpperArm, shoulder.Position.X > 0, sleeve, skinTone, shortSleeve), elbow));
        return elbow;
    }

    /// <summary>Tools hang from the right fist along the forearm; blades face local -Z, the direction of a downward stroke.</summary>
    private void BuildTools() {
        foreach (var name in new[] { "steel_axe", "steel_pick", "steel_shovel", "wood_hammer", "steel_hammer", "protest_sign" }) {
            var tool = name == "protest_sign" ? ProtestSign() : Tool(name);
            Hold(tool);
            tool.Visible = false;
            tools.Add((name, tool));
            if (name == "steel_shovel") shovel = tool;
        }
    }

    /// <summary>Where a point on a forearm is from the rig's root, under the pose just shown.</summary>
    private Vector3 FromForearm(bool left, Vector3 point) => hips.Transform * (torso.Transform * ((left ? shoulderL : shoulderR).Transform * ((left ? elbowL : elbowR).Transform * point)));

    /// <summary>Where a fist is from the rig's root, under the pose just shown.</summary>
    public Vector3 Fist(bool left) => FromForearm(left, new Vector3(0, -Forearm, Palm));

    /// <summary>Where an elbow is from the rig's root, under the pose just shown.</summary>
    public Vector3 Elbow(bool left) => FromForearm(left, Vector3.Zero);

    /// <summary>Where the tip of the shovel's blade is from the rig's root, under the pose just shown.</summary>
    public Vector3 ShovelTip => FromForearm(false, shovel.Transform * new Vector3(0, -ActorAnimation.ShovelLength, 0));

    /// <summary>
    /// Lays the shovel's shaft from the right fist through the left, as both hands hold it, and turns the blade's face up and away from the digger.
    /// </summary>
    private void LayShovel() {
        var along = (Fist(true) - Fist(false)).Normalized();
        // The face looks ahead with the shaft upright and at the sky with it level.
        var face = along.Cross(Vector3.Right).Normalized();
        var held = new Basis(along.Cross(face), -along, -face);
        shovel.Basis = (torso.Basis * shoulderR.Basis * elbowR.Basis).Inverse() * held;
    }

    /// <summary>
    /// Turns an upper arm about its own length, on top of the pose just shown.
    /// A <see cref="Pose"/> has no such turn, so an elbow folds only forward; with it a forearm can lie across the chest.
    /// </summary>
    public void RollArm(bool left, float degrees) => (left ? shoulderL : shoulderR).RotateObjectLocal(Vector3.Up, Mathf.DegToRad(degrees));

    /// <summary>Puts something gripped at its origin into the right fist, or the left one.</summary>
    public void Hold(Node3D item, bool right = true) {
        item.Position = new Vector3(0, -Forearm, Palm);
        (right ? elbowR : elbowL).AddChild(item);
    }

    /// <summary>A worker's hard hat on a head: a shell with a ridge from front to back, over a round brim. The icon renderer shows it on its own.</summary>
    public static void HardHat(Node3D head) {
        Sphere(head, .14f, new Vector3(0, .2f, 0), Mat("e8b323", .5f), new Vector3(1, .62f, 1.08f), 16);
        Sphere(head, .14f, new Vector3(0, .204f, 0), Mat("c9981a", .5f), new Vector3(.2f, .66f, 1.1f), 16);
        Cylinder(head, .17f, .17f, .02f, new Vector3(0, .17f, .02f), Mat("e8b323", .5f), null, 20);
    }

    /// <summary>A held tool gripped at the origin, handle down along -Y and blade toward -Z; the icon renderer shows it on its own.</summary>
    public static Node3D Tool(string name) {
        if (name == "steel_shovel") return Shovel();
        if (name is "steel_hammer" or "wood_hammer") return Hammer(name == "steel_hammer");
        if (name is "stone_chisel" or "steel_chisel") return Chisel(name == "steel_chisel");
        bool steelTool = name.StartsWith("steel");
        string handle = steelTool ? "e87d1e" : "8a5a32", head = steelTool ? "c9d3d8" : "5b5f62";
        float metal = steelTool ? .85f : .45f;
        var tool = new Node3D();
        const float length = .72f;
        Cylinder(tool, .022f, .022f, length, new Vector3(0, .08f - length / 2, 0), Wood(handle, .7f));
        if (steelTool) Cylinder(tool, .027f, .027f, .16f, new Vector3(0, -.02f, 0), Mat("222222"));
        float end = .06f - length;
        var steel = Metal(head, .35f, metal);
        if (name.EndsWith("axe")) {
            Box(tool, new Vector3(.045f, .1f, .15f), new Vector3(0, end, -.05f), steel);
            Box(tool, new Vector3(.03f, .17f, .05f), new Vector3(0, end, -.14f), Metal(steelTool ? "e9eef0" : "8d9294", .3f, metal));
        } else {
            Cylinder(tool, .035f, 0, .3f, new Vector3(0, end, -.15f), steel, new Vector3(-90, 0, 0), 6);
            Cylinder(tool, .035f, 0, .22f, new Vector3(0, end, .11f), steel, new Vector3(90, 0, 0), 6);
        }
        return tool;
    }

    /// <summary>
    /// A steel spade gripped at the origin by the bar across the top of its handle, shaft down along -Y to a pointed blade whose face looks toward -Z.
    /// </summary>
    private static Node3D Shovel() {
        var tool = new Node3D();
        const float length = ActorAnimation.ShovelLength, blade = .27f, shaft = length - blade;
        var wood = Wood("e87d1e", .7f);
        var steel = Metal("c9d3d8", .35f, .85f);
        // A bar to hold, on a fork that meets the shaft.
        Cylinder(tool, .017f, .017f, .11f, Vector3.Zero, wood, new Vector3(0, 0, 90), 6);
        foreach (float side in new[] { -1f, 1f }) Cylinder(tool, .012f, .012f, .085f, new Vector3(side * .03f, -.035f, 0), wood, new Vector3(0, 0, side * 35), 5);
        Cylinder(tool, .02f, .02f, shaft - .06f, new Vector3(0, -.06f - (shaft - .06f) / 2, 0), wood, sides: 6);
        Cylinder(tool, .027f, .027f, .1f, new Vector3(0, -.22f, 0), Mat("222222"), sides: 6);
        // The socket runs down the back of the blade, which narrows to a point.
        Cylinder(tool, .016f, .024f, .14f, new Vector3(0, -shaft - .03f, .008f), steel, sides: 6);
        Box(tool, new Vector3(.2f, .012f, .012f), new Vector3(0, -shaft + .006f, 0), steel);
        Box(tool, new Vector3(.19f, .17f, .008f), new Vector3(0, -shaft - .085f, 0), steel);
        Cylinder(tool, 0, .095f, blade - .17f, new Vector3(0, -shaft - .17f - (blade - .17f) / 2, 0), Metal("e9eef0", .3f, .85f), null, 4).Scale = new Vector3(1, 1, .06f);
        return tool;
    }

    /// <summary>
    /// A claw hammer with a rubber grip, or a wooden mallet with a banded barrel head, gripped at the origin with the striking face toward -Z.
    /// </summary>
    private static Node3D Hammer(bool steel) {
        var tool = new Node3D();
        const float length = .38f, end = .06f - length;
        Cylinder(tool, .02f, .024f, length, new Vector3(0, .08f - length / 2, 0), steel ? Mat("e87d1e", .7f) : Wood("8a5a32", .7f));
        if (steel) {
            var metal = Metal("c9d3d8", .35f, .85f);
            Cylinder(tool, .027f, .027f, .12f, new Vector3(0, -.01f, 0), Mat("222222"));
            Cylinder(tool, .028f, .028f, .1f, new Vector3(0, end, -.03f), metal, new Vector3(90, 0, 0), 8);
            Cylinder(tool, .034f, .034f, .02f, new Vector3(0, end, -.085f), metal, new Vector3(90, 0, 0), 8);
            // The claw tapers away from the face and curves down toward the handle.
            Cylinder(tool, .024f, .006f, .1f, new Vector3(0, end - .015f, .06f), metal, new Vector3(110, 0, 0), 6).Scale = new Vector3(.6f, 1, 1);
        } else {
            Cylinder(tool, .052f, .052f, .15f, new Vector3(0, end, 0), Wood("b98552", .8f), new Vector3(90, 0, 0), 10);
            foreach (float z in new[] { -.06f, .06f }) Cylinder(tool, .055f, .055f, .016f, new Vector3(0, end, z), Wood("7a5233", .8f), new Vector3(90, 0, 0), 10);
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
            var metal = Metal("c9d3d8", .3f, .85f);
            Cylinder(tool, .02f, .025f, .14f, Vector3.Zero, Mat("e87d1e", .7f), sides: 8);
            Cylinder(tool, .026f, .026f, .02f, new Vector3(0, .075f, 0), Mat("222222"), sides: 8);
            Cylinder(tool, .021f, .021f, .02f, new Vector3(0, -.08f, 0), metal, sides: 8);
            Box(tool, new Vector3(.016f, .1f, .016f), new Vector3(0, -.14f, 0), metal);
            Box(tool, new Vector3(.012f, .06f, .05f), new Vector3(0, -.21f, 0), metal);
            Box(tool, new Vector3(.005f, .016f, .05f), new Vector3(0, -.248f, 0), Metal("e9eef0", .25f, .85f));
        } else {
            var stone = Stone("9aa29f", .9f);
            Cylinder(tool, .02f, .024f, .18f, new Vector3(0, -.01f, 0), Wood("8a5a32", .8f), sides: 8);
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
        Cylinder(sign, .02f, .02f, 1.3f, new Vector3(0, .033f, .45f), Wood("8a5a32", .8f), new Vector3(90, 0, 0), 6);
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
        torso.RotationDegrees = new Vector3(pose.Lean, pose.Twist, pose.Sway);
        // The head stays nearer upright than the body under it.
        head.RotationDegrees = new Vector3(pose.Head - pose.Lean * .4f, -pose.Twist * .5f, -pose.Sway * .6f);
        shoulderL.RotationDegrees = new Vector3(-pose.RaiseL, 0, pose.OutL);
        shoulderR.RotationDegrees = new Vector3(-pose.RaiseR, 0, -pose.OutR);
        elbowL.RotationDegrees = new Vector3(-pose.ElbowL, 0, 0);
        elbowR.RotationDegrees = new Vector3(-pose.ElbowR, 0, 0);
        legL.Basis = HipTurn(pose.Splay, pose.SwingL, pose.TurnL, pose.RollL);
        legR.Basis = HipTurn(-pose.Splay, pose.SwingR, -pose.TurnR, -pose.RollR);
        kneeL.RotationDegrees = new Vector3(pose.KneeL, 0, 0);
        kneeR.RotationDegrees = new Vector3(pose.KneeR, 0, 0);
        foreach (var (skeleton, joint) in limbs)
            skeleton.SetBonePoseRotation(1, joint.Quaternion);
        footL.SetBonePoseRotation(2, new Quaternion(Vector3.Right, Mathf.DegToRad(pose.FootL)));
        footR.SetBonePoseRotation(2, new Quaternion(Vector3.Right, Mathf.DegToRad(pose.FootR)));
        hips.Position = new Vector3(0, pose.HipHeight, 0);
        rock.Visible = onRock;
        foreach (var (name, node) in tools) node.Visible = name == tool;
        // A shovel is only ever dug with, held in both fists.
        if (shovel.Visible) LayShovel();
        crate.Visible = carrying;
        torch.Visible = lit;
    }

    /// <summary>Tips the leg sideways, turns the thigh outward, swings it forward, then rolls it about its own length; the left side turns toward +X.</summary>
    private static Basis HipTurn(float splay, float swing, float turn, float roll) =>
        new Basis(Vector3.Back, Mathf.DegToRad(splay)) * new Basis(Vector3.Up, Mathf.DegToRad(turn)) * new Basis(Vector3.Right, Mathf.DegToRad(-swing)) * new Basis(Vector3.Up, Mathf.DegToRad(roll));
}
