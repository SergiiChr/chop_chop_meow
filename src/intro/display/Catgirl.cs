using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;
using Ring = ChopChopMeow.ActorShape.Ring;

namespace ChopChopMeow;

internal enum CatgirlOutfit { Maid, Blouse, Hoodie }

/// <summary>
/// What a catgirl's hand does: spread open, shut, one finger pointing, or finger and thumb closed in a ring.
/// </summary>
internal enum HandShape { Open, Fist, Point, Ring }

/// <summary>
/// A catgirl dancing on the web page in the intro film: the game's person model in its slim build.
/// She has a face, hair, cat ears, a tail, hands with fingers and an outfit of her own.
/// The outfits are a maid's dress, a sleeveless blouse with tailored shorts and thigh-high socks, and a long black hoodie.
/// </summary>
internal sealed class Catgirl {
    /// <summary>The colours of trims, ties and buttons the outfits share.</summary>
    private const string Lace = "f4f2ee", Red = "c8372f", Snow = "ffffff";
    /// <summary>What she looks like: her body's colours, the colour of her eyes and the trim of her outfit.</summary>
    private readonly record struct Look(Outfit Body, string Eyes, string Trim);

    public readonly ActorRig Rig;
    private readonly CatgirlOutfit outfit;
    private readonly Look look;
    private readonly StandardMaterial3D skin, locks, snow = Mat(Snow);
    private readonly List<Node3D> tail = new(), eyes = new(), tresses = new();
    private readonly Hand left, right;
    private readonly Vector3 home;
    /// <summary>
    /// How far each joint of the tail bends at rest, in degrees: back and down from the spine, then curling up.
    /// </summary>
    private static readonly float[] TailCurl = { -115, 28, 30, 30, 25 };

    private static Look LookOf(CatgirlOutfit outfit) {
        const string skin = "e8bdab";
        return outfit switch {
            CatgirlOutfit.Maid => new(new(skin, "f29ab8", "25222c", "25222c", "f6f1f4", "1d1b20", Sleeves.Short), "3f8fd6", Lace),
            CatgirlOutfit.Blouse => new(new(skin, "222940", "f3f1ee", "292831", "292831", "342b29", Sleeves.None, Socks: "18222c"), "71897a", "242833"),
            _ => new(new(skin, "c7bedc", "191920", "191920", null, "e6e3ee", Sleeves.Long), "8a5fd0", "262631"),
        };
    }

    /// <param name="at">Where she stands on the stage.</param>
    public Catgirl(CatgirlOutfit outfit, Vector3 at) {
        this.outfit = outfit;
        look = LookOf(outfit);
        Rig = new ActorRig(look.Body, HumanShape.Slim, joinBody: true);
        home = at;
        skin = Mat(look.Body.Skin); locks = Mat(look.Body.Hair);
        BuildFace();
        BuildHair();
        BuildTail();
        BuildOutfit();
        var cuff = outfit == CatgirlOutfit.Hoodie ? Cloth(look.Trim) : null;
        left = new Hand(Rig, true, skin, cuff); right = new Hand(Rig, false, skin, cuff);
    }

    private void BuildFace() {
        var head = Rig.Head;
        var ink = Mat("25232f");
        var iris = Mat(look.Eyes);
        var eyeWhite = Mat("f5e9e4");
        foreach (float side in new[] { -1f, 1f }) {
            var eye = Pivot(head, new Vector3(side * .046f, .148f, .108f), new Vector3(0, side * 16, side * 9));
            Sphere(eye, .025f, Vector3.Zero, ink, new Vector3(1, .52f, .2f), 16);
            Sphere(eye, .023f, new Vector3(0, -.002f, .003f), eyeWhite, new Vector3(1, .44f, .18f), 16);
            Sphere(eye, .010f, new Vector3(-side * .002f, 0, .007f), iris, new Vector3(.85f, 1, .24f), 12);
            Sphere(eye, .0055f, new Vector3(-side * .002f, .001f, .009f), ink, new Vector3(.83f, 1, .35f), 10);
            Sphere(eye, .003f, new Vector3(.001f, .004f, .011f), snow, new Vector3(1, 1.2f, .45f), 8);
            eyes.Add(eye);
            Accent(head, new Vector3(side * .027f, .178f, .112f), new Vector3(side * .064f, .179f, .099f), .0035f, locks);
        }
        var mouth = Mat("955562");
        Accent(head, new Vector3(-.014f, .074f, .101f), new Vector3(0, .07f, .105f), .0025f, mouth);
        Accent(head, new Vector3(0, .07f, .105f), new Vector3(.019f, .076f, .101f), .0025f, mouth);
    }

    /// <summary>A narrow raised edge along the face or fabric.</summary>
    private static void Accent(Node3D parent, Vector3 from, Vector3 to, float radius, Material material) {
        var along = to - from;
        var stroke = Limb(parent, radius, along.Length() + radius * 2, Vector3.Zero, material);
        stroke.Quaternion = new Quaternion(Vector3.Down, along.Normalized());
        stroke.Position = (from + to) / 2;
    }

    /// <summary>A tapered lock shaped from rounded cross-sections.</summary>
    private void Lock(Node3D head, params Ring[] profile) => ActorShape.Add(head, profile, material: locks);

    private void BuildHair() {
        var head = Rig.Head;
        var white = Colors.White;
        Sphere(head, .124f, new Vector3(0, .175f, -.019f), locks, new Vector3(.99f, .82f, 1.04f), 20);
        Sphere(head, .113f, new Vector3(0, .127f, -.043f), locks, new Vector3(1.01f, .97f, .76f), 16);
        // The fringe parts off-centre and curves across the brow.
        Lock(head, new(.254f, .015f, .012f, white, .045f, -.03f), new(.225f, .045f, .027f, white, .078f, -.019f),
            new(.191f, .037f, .025f, white, .107f, .014f), new(.153f, .025f, .016f, white, .122f, .039f),
            new(.118f, .011f, .009f, white, .117f, .061f), new(.093f, .001f, .001f, white, .092f, .074f));
        Lock(head, new(.241f, .017f, .02f, white, .058f, -.039f), new(.21f, .034f, .025f, white, .09f, -.067f),
            new(.171f, .023f, .018f, white, .083f, -.083f), new(.127f, .014f, .013f, white, .072f, -.094f),
            new(.085f, .001f, .001f, white, .071f, -.085f));
        foreach (float side in new[] { -1f, 1f }) {
            Lock(head, new(.213f, .016f, .032f, white, .009f, side * .09f),
                new(.162f, .023f, .042f, white, .029f, side * .101f), new(.102f, .016f, .032f, white, .033f, side * .102f),
                new(.059f, .002f, .005f, white, .044f, side * .082f));
            var ear = Pivot(head, new Vector3(side * .076f, .237f, -.015f), new Vector3(0, 0, -side * 18));
            Cylinder(ear, .048f, 0, .094f, new Vector3(0, .038f, 0), locks, sides: 6).Scale = new Vector3(1, 1, .57f);
            Cylinder(ear, .028f, 0, .062f, new Vector3(0, .024f, .017f), Mat("c78f9d"), sides: 6).Scale = new Vector3(1, 1, .4f);
            if (outfit == CatgirlOutfit.Maid) {
                Sphere(head, .027f, new Vector3(side * .118f, .213f, -.043f), Mat(look.Trim), null, 10);
                Tress(new Vector3(side * .12f, .213f, -.043f), new Vector3(0, 0, side * 14), .036f, .027f, .2f);
            }
        }
        if (outfit == CatgirlOutfit.Blouse)
            Sphere(head, .065f, new Vector3(0, .095f, -.112f), locks, new Vector3(1.15f, .76f, .87f), 16);
    }

    /// <summary>A length of hair in two parts hanging from a tie, which swings as she sways.</summary>
    private void Tress(Vector3 tie, Vector3 turn, float thick, float thin, float length) {
        var root = Pivot(Rig.Head, tie);
        var hang = Pivot(root, Vector3.Zero, turn);
        Limb(hang, thick, length, Vector3.Zero, locks);
        Limb(hang, thin, length, new Vector3(0, -length + thick * 1.5f, 0), locks);
        tresses.Add(root);
    }

    /// <summary>
    /// The tail: capsules end to end, each on a joint at the end of the one before, thinning to a white tip.
    /// </summary>
    private void BuildTail() {
        const float segment = .1f;
        var joint = Pivot(Rig.Torso, new Vector3(0, .06f, -.09f));
        for (int i = 0; i < TailCurl.Length; i++) {
            if (i > 0) joint = Pivot(joint, new Vector3(0, segment, 0));
            float thick = .03f - i * .0015f;
            Limb(joint, thick, segment + thick * 2, new Vector3(0, segment, 0), locks);
            tail.Add(joint);
        }
        Sphere(joint, .027f, new Vector3(0, segment, 0), snow, null, 8);
    }

    /// <summary>
    /// What she wears over the body's colours: things above the waist ride on the chest, the rest on the torso.
    /// </summary>
    private void BuildOutfit() {
        if (outfit == CatgirlOutfit.Blouse) {
            DressBlouse(Rig.Chest, Mat(look.Body.Top), Mat(look.Trim));
            return;
        }
        Node3D head = Rig.Head;
        var torso = FittedTo(Rig.Torso);
        var chest = FittedTo(Rig.Chest);
        StandardMaterial3D top = Cloth(look.Body.Top), trim = Cloth(look.Trim);
        switch (outfit) {
            case CatgirlOutfit.Maid: {
                    // A flared skirt over a petticoat, an apron with a bib, and a waistband tied in a bow at the back.
                    // Puffed sleeves and a collar.
                    Cylinder(torso, .3f, .125f, .3f, new Vector3(0, -.03f, 0), top, sides: 16);
                    Cylinder(torso, .31f, .29f, .03f, new Vector3(0, -.185f, 0), trim, sides: 16);
                    Box(torso, new Vector3(.2f, .25f, .012f), new Vector3(0, -.03f, .227f), trim, new Vector3(-30.3f, 0, 0));
                    Box(chest, new Vector3(.12f, .13f, .012f), new Vector3(0, .31f, .112f), trim);
                    Cylinder(torso, .12f, .12f, .03f, new Vector3(0, .13f, 0), trim, sides: 14);
                    foreach (float side in new[] { -1f, 1f }) {
                        Box(torso, new Vector3(.1f, .06f, .02f), new Vector3(side * .05f, .14f, -.1f), trim, new Vector3(0, 0, side * 20));
                        Sphere(chest, .05f, new Vector3(side * .136f, .447f, -.026f), top, new Vector3(1.03f, .95f, .86f), 12);
                        Box(chest, new Vector3(.03f, .022f, .012f), new Vector3(side * .02f, .5f, .07f), Cloth(Red), new Vector3(0, 0, side * 18));
                    }
                    Cylinder(chest, .05f, .05f, .02f, new Vector3(0, .535f, 0), trim);
                    // A frilled band over the crown, from ear to ear.
                    for (int frill = -2; frill <= 2; frill++) {
                        float angle = Mathf.DegToRad(frill * 25);
                        Box(head, new Vector3(.055f, .03f, .015f), new Vector3(MathF.Sin(angle) * .125f, .185f + MathF.Cos(angle) * .105f, .03f), trim, new Vector3(0, 0, -frill * 25));
                    }
                    break;
                }
            case CatgirlOutfit.Hoodie:
                DressHoodie(torso, chest, top, trim);
                break;
        }
    }

    private static Node3D FittedTo(Node3D joint) {
        var clothes = Pivot(joint, Vector3.Zero);
        clothes.Scale = new Vector3(.92f, .78f, 1.18f);
        return clothes;
    }

    private static void DressBlouse(Node3D chest, Material top, Material ribbon) {
        var seam = Mat("d7d2d0");
        Cylinder(chest, .043f, .035f, .022f, new Vector3(0, .42f, .003f), top, sides: 16);
        foreach (float side in new[] { -1f, 1f }) {
            Box(chest, new Vector3(.035f, .032f, .009f), new Vector3(side * .02f, .404f, .035f), top, new Vector3(-24, 0, side * 25));
            Sphere(chest, .015f, new Vector3(side * .015f, .386f, .035f), ribbon, new Vector3(1.15f, .49f, .39f), 12);
            var fold = new Vector3(side * .008f, .35f, .058f);
            Accent(chest, new Vector3(side * .004f, .385f, .041f), fold, .0043f, ribbon);
            Accent(chest, fold, new Vector3(side * .013f, .329f, .088f), .004f, ribbon);
        }
        Sphere(chest, .008f, new Vector3(0, .386f, .04f), ribbon, new Vector3(1, .85f, .66f), 10);
        Accent(chest, new Vector3(0, .327f, .091f), new Vector3(0, .304f, .113f), .0016f, seam);
        Accent(chest, new Vector3(0, .304f, .113f), new Vector3(0, .275f, .12f), .0016f, seam);
        Accent(chest, new Vector3(0, .275f, .12f), new Vector3(0, .218f, .108f), .0016f, seam);
        Accent(chest, new Vector3(0, .218f, .108f), new Vector3(0, .181f, .098f), .0016f, seam);
    }

    private static void DressHoodie(Node3D torso, Node3D chest, Material fleece, Material trim) {
        var white = Colors.White;
        ActorShape.Add(chest, new Ring[] {
            new(.542f, .076f, .063f, white), new(.501f, .147f, .108f, white),
            new(.475f, .182f, .122f, white), new(.438f, .189f, .134f, white),
            new(.33f, .169f, .141f, white), new(.172f, .153f, .124f, white),
        }, material: fleece);
        ActorShape.Add(torso, new Ring[] {
            new(.265f, .158f, .127f, white), new(.14f, .171f, .136f, white),
            new(.051f, .187f, .154f, white, .008f), new(-.047f, .209f, .18f, white, .026f),
            new(-.112f, .221f, .202f, white, .045f), new(-.174f, .217f, .211f, white, .055f),
            new(-.215f, .211f, .205f, white, .059f),
        }, material: fleece);
        Cylinder(torso, .212f, .218f, .038f, new Vector3(0, -.201f, .057f), trim, sides: 16).Scale = new Vector3(1, 1, .98f);
        Sphere(chest, .125f, new Vector3(0, .481f, -.104f), fleece, new Vector3(1.2f, .72f, .8f), 16);
        Sphere(chest, .089f, new Vector3(0, .522f, -.125f), trim, new Vector3(1.23f, .27f, .71f), 14);
        Sphere(torso, .102f, new Vector3(0, -.013f, .166f), trim, new Vector3(1.18f, .63f, .32f), 16);
        foreach (float side in new[] { -1f, 1f })
            Accent(torso, new Vector3(side * .074f, .02f, .184f), new Vector3(side * .098f, -.027f, .181f), .003f, fleece);
        var cord = Cloth("51515e");
        foreach (float side in new[] { -1f, 1f }) {
            Accent(chest, new Vector3(side * .033f, .527f, .087f), new Vector3(side * .039f, .476f, .121f), .005f, cord);
            Accent(chest, new Vector3(side * .039f, .476f, .121f), new Vector3(side * .045f, .407f, .137f), .004f, cord);
        }
    }

    /// <summary>Poses her for a moment of a dance; <paramref name="time"/> is her own clock, for her blinks.</summary>
    public void Show(in DanceFrame frame, float time) {
        Rig.Show(frame.Pose, null, false);
        Rig.RollArm(true, frame.RollL); Rig.RollArm(false, frame.RollR);
        Rig.BendChest(frame.Chest.X, frame.Chest.Y, frame.Chest.Z);
        Rig.TurnHips(frame.Hips.X, frame.Hips.Y, frame.Hips.Z);
        // Each foot tips back by as much as its leg leans out, so its sole lies flat on the floor.
        Rig.RollFoot(true, -frame.Pose.SplayL - frame.Hips.Z); Rig.RollFoot(false, -frame.Pose.SplayR + frame.Hips.Z);
        Rig.MoveShoulder(true, frame.ShoulderL.X, frame.ShoulderL.Y); Rig.MoveShoulder(false, frame.ShoulderR.X, frame.ShoulderR.Y);
        Rig.Root.Position = home + new Vector3(frame.Shift, 0, frame.Depth);
        Rig.Root.RotationDegrees = new Vector3(0, frame.Facing, 0);
        Rig.Head.RotationDegrees += frame.Head;
        left.Shape = frame.HandL; right.Shape = frame.HandR;
        left.Turn(frame.WristL); right.Turn(frame.WristR);
        Rig.PoseJoinedBody(left.WristTransform, right.WristTransform);
        // The tail's tip and her tied hair trail behind the sway.
        for (int i = 0; i < tail.Count; i++)
            tail[i].RotationDegrees = new Vector3(TailCurl[i], 0, Mathf.Lerp(14 * frame.Swing, 38 * frame.Trail, i / (tail.Count - 1f)));
        foreach (var tress in tresses) tress.RotationDegrees = new Vector3(0, 0, -12 * frame.Trail);
        // A quick blink every few seconds.
        float shut = MathF.Pow(Math.Max(0, MathF.Sin(time * 2.3f)), 40);
        foreach (var eye in eyes) eye.Scale = new Vector3(1, 1 - .9f * shut, 1);
    }

    /// <summary>Four fingers and a thumb on the palm carried by the skinned body.</summary>
    private sealed class Hand {
        private readonly Node3D[] fingers = new Node3D[4], tips = new Node3D[4];
        private readonly Node3D thumb, wrist;
        /// <summary>Which way the fingers curl: toward the body, which is -X for the left hand.</summary>
        private readonly float inward;
        private HandShape shape = HandShape.Open;

        public Hand(ActorRig rig, bool isLeft, Material skin, Material? cuff) {
            inward = isLeft ? -1 : 1;
            // The wrist is where the forearm ends, a little above where a fist would be.
            wrist = new Node3D(); rig.Hold(wrist, right: !isLeft);
            wrist.Position += new Vector3(0, .05f, 0);
            if (cuff != null) Cylinder(wrist.GetParent<Node3D>(), .025f, .026f, .043f, new Vector3(0, wrist.Position.Y + .006f, 0), cuff, sides: 12);
            var root = Pivot(wrist, new Vector3(0, -.05f, 0));
            // The index finger is the one next to the thumb, ahead.
            for (int i = 0; i < fingers.Length; i++) {
                fingers[i] = Pivot(root, new Vector3(0, -.02f, .019f - i * .0127f));
                float length = i == 3 ? .024f : .03f;
                Limb(fingers[i], .006f, length + .005f, Vector3.Zero, skin);
                tips[i] = Pivot(fingers[i], new Vector3(0, -length + .003f, 0));
                Limb(tips[i], .0055f, length - .003f, Vector3.Zero, skin);
            }
            thumb = Pivot(root, new Vector3(0, .02f, .026f));
            Accent(thumb, Vector3.Zero, new Vector3(0, -.03f, .019f), .008f, skin);
            Curl();
        }

        /// <summary>
        /// Turns the hand at the wrist, in degrees: a bend to the palm, a twist of the forearm and a tip to the thumb.
        /// The twist brings the thumb in toward the body.
        /// </summary>
        public void Turn(Vector3 wristTurn) => wrist.RotationDegrees = new Vector3(wristTurn.Z, wristTurn.Y * inward, wristTurn.X * inward);

        public Transform3D WristTransform => wrist.Transform;

        public HandShape Shape {
            set {
                if (value == shape) return;
                shape = value; Curl();
            }
        }

        private void Curl() {
            // How far the index finger and the other three bend at each joint, and the thumb turns in, in degrees.
            var (index, others, thumbIn) = shape switch {
                HandShape.Fist => (80f, 80f, 40f),
                HandShape.Point => (0f, 85f, 45f),
                HandShape.Ring => (55f, 8f, 55f),
                _ => (6f, 6f, 0f),
            };
            for (int i = 0; i < fingers.Length; i++) {
                float bend = (i == 0 ? index : others) * inward;
                float towardThumb = shape == HandShape.Ring && i == 0 ? -22 : 0;
                fingers[i].RotationDegrees = new Vector3(towardThumb, 0, bend);
                tips[i].RotationDegrees = new Vector3(0, 0, bend);
            }
            thumb.RotationDegrees = new Vector3(shape == HandShape.Ring ? 24 : 0, thumbIn * inward, 0);
        }
    }
}
