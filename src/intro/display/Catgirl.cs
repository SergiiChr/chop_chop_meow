using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

internal enum CatgirlOutfit { Maid, Shorts, Hoodie }

/// <summary>
/// What a catgirl's hand does: spread open, shut, one finger pointing, or finger and thumb closed in a ring.
/// </summary>
internal enum HandShape { Open, Fist, Point, Ring }

/// <summary>
/// A catgirl dancing on the web page in the intro film: the game's person model in its slim build, with a face, hair,
/// cat ears, a tail, hands with fingers and an outfit of her own.
/// The outfits are a maid's dress, a sleeveless shirt with shorts and knee socks, and a long hoodie.
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
    private readonly List<Node3D> tail = new(), lids = new(), tresses = new();
    private readonly Hand left, right;
    private readonly Vector3 home;
    /// <summary>
    /// How far each joint of the tail bends at rest, in degrees: back and down from the spine, then curling up.
    /// </summary>
    private static readonly float[] TailCurl = { -115, 28, 30, 30, 25 };

    private static Look LookOf(CatgirlOutfit outfit) {
        const string skin = "f0c9ae";
        return outfit switch {
            CatgirlOutfit.Maid => new(new(skin, "f29ab8", "25222c", "25222c", "f6f1f4", "1d1b20", Sleeves.Short), "3f8fd6", Lace),
            CatgirlOutfit.Shorts => new(new(skin, "e8c56a", "f6f4f0", "1c1b20", null, Red, Sleeves.None), "d98a2b", Red),
            _ => new(new(skin, "e9e6f2", "f0b63f", "f0b63f", null, "f4f4f4", Sleeves.Long), "8a5fd0", "d99f2b"),
        };
    }

    /// <param name="at">Where she stands on the stage.</param>
    public Catgirl(CatgirlOutfit outfit, Vector3 at) {
        this.outfit = outfit;
        look = LookOf(outfit);
        Rig = new ActorRig(look.Body, HumanShape.Slim);
        home = at;
        skin = Mat(look.Body.Skin); locks = Mat(look.Body.Hair);
        // A smaller head on the same body makes her taller for her head, as a grown girl in a cartoon is.
        Rig.Head.Scale = Vector3.One * .92f;
        BuildFace();
        BuildHair();
        BuildTail();
        BuildOutfit();
        left = new Hand(Rig, true, skin); right = new Hand(Rig, false, skin);
    }

    private void BuildFace() {
        var head = Rig.Head;
        StandardMaterial3D dark = Mat("2c2530"), iris = Mat(look.Eyes);
        foreach (float side in new[] { -1f, 1f }) {
            // An eye follows the head's curve: white, iris, pupil and
            // a glint, under a lash with a flick at its outer end.
            var eye = Pivot(head, new Vector3(side * .047f, .135f, .106f), new Vector3(0, side * 12, 0));
            Box(eye, new Vector3(.04f, .044f, .008f), Vector3.Zero, snow);
            Box(eye, new Vector3(.03f, .037f, .009f), new Vector3(0, -.002f, .002f), iris);
            Box(eye, new Vector3(.013f, .02f, .01f), new Vector3(0, -.003f, .003f), dark);
            Box(eye, new Vector3(.009f, .009f, .011f), new Vector3(.007f, .009f, .004f), snow);
            Box(eye, new Vector3(.046f, .008f, .012f), new Vector3(0, .024f, .002f), dark);
            Box(eye, new Vector3(.014f, .007f, .012f), new Vector3(side * .026f, .028f, .001f), dark, new Vector3(0, 0, side * 32));
            // The lid hangs from the eye's top edge, so stretching it shuts the eye.
            var lid = Pivot(eye, new Vector3(0, .023f, .007f));
            Box(lid, new Vector3(.042f, .046f, .006f), new Vector3(0, -.023f, 0), skin);
            lids.Add(lid);
            Box(head, new Vector3(.034f, .006f, .008f), new Vector3(side * .047f, .178f, .101f), locks, new Vector3(0, 0, -side * 8));
            Box(head, new Vector3(.03f, .013f, .008f), new Vector3(side * .08f, .105f, .087f), Mat("f4a3ae"), new Vector3(0, side * 40, 0));
        }
        Box(head, new Vector3(.02f, .007f, .008f), new Vector3(0, .083f, .114f), Mat("b5475a"));
    }

    /// <summary>
    /// A cap, a fringe of five strands and a lock by each cheek on everyone; then twin tails, a ponytail or a bob.
    /// </summary>
    private void BuildHair() {
        var head = Rig.Head;
        Sphere(head, .13f, new Vector3(0, .185f, -.022f), locks, new Vector3(1.04f, .76f, 1.06f), 16);
        for (int strand = -2; strand <= 2; strand++)
            Box(head, new Vector3(.038f, .062f, .018f), new Vector3(strand * .035f, .198f - Math.Abs(strand) * .006f, .102f - Math.Abs(strand) * .007f), locks, new Vector3(-10, strand * 14, strand * -6));
        Sphere(head, .118f, new Vector3(0, .1f, -.05f), locks, new Vector3(1.05f, 1, .75f), 12);
        foreach (float side in new[] { -1f, 1f }) {
            Limb(head, .024f, .17f, new Vector3(side * .115f, .17f, .04f), locks);
            // An ear: a flattened pyramid of hair with a pink one set into its front and a white tuft at its foot.
            var ear = Pivot(head, new Vector3(side * .078f, .265f, -.01f), new Vector3(0, 0, -side * 16));
            Cylinder(ear, .055f, 0, .11f, new Vector3(0, .045f, 0), locks, sides: 4).Scale = new Vector3(1, 1, .55f);
            Cylinder(ear, .034f, 0, .075f, new Vector3(0, .035f, .015f), Mat("f7b3c4"), sides: 4).Scale = new Vector3(1, 1, .4f);
            Sphere(ear, .018f, new Vector3(0, .002f, .026f), snow, null, 8);
            switch (outfit) {
                case CatgirlOutfit.Maid:
                    // A twin tail tied high at the side with a ribbon, hanging in two lengths.
                    Sphere(head, .03f, new Vector3(side * .125f, .235f, -.03f), Mat(look.Trim), null, 8);
                    Tress(new Vector3(side * .13f, .235f, -.03f), new Vector3(0, 0, side * 14), .04f, .032f, .21f);
                    break;
                case CatgirlOutfit.Hoodie:
                    // A bob: a rounded fall by each cheek, turned in at the jaw.
                    Sphere(head, .06f, new Vector3(side * .1f, .085f, -.005f), locks, new Vector3(.7f, 1.5f, 1.25f), 10);
                    break;
            }
        }
        // A ponytail tied at the crown.
        if (outfit != CatgirlOutfit.Shorts) return;
        Sphere(head, .034f, new Vector3(0, .26f, -.1f), Mat(look.Trim), null, 8);
        Tress(new Vector3(0, .26f, -.11f), new Vector3(-28, 0, 0), .045f, .034f, .23f);
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
    /// What she wears over the body's own colours: what is above the
    /// waist rides on the chest, the rest on the torso under it.
    /// </summary>
    private void BuildOutfit() {
        Node3D head = Rig.Head, torso = Rig.Torso, chest = Rig.Chest;
        StandardMaterial3D top = Cloth(look.Body.Top), hips = Cloth(look.Body.Hips), trim = Cloth(look.Trim);
        switch (outfit) {
            case CatgirlOutfit.Maid: {
                    // A flared skirt over a petticoat, an apron with a bib, a waistband tied in a bow at the back,
                    // puffed sleeves and a collar.
                    Cylinder(torso, .3f, .1f, .3f, new Vector3(0, -.03f, 0), top, sides: 16);
                    Cylinder(torso, .31f, .29f, .03f, new Vector3(0, -.185f, 0), trim, sides: 16);
                    Box(torso, new Vector3(.2f, .25f, .012f), new Vector3(0, -.03f, .207f), trim, new Vector3(-33.7f, 0, 0));
                    Box(chest, new Vector3(.12f, .13f, .012f), new Vector3(0, .31f, .112f), trim);
                    Cylinder(torso, .102f, .102f, .03f, new Vector3(0, .13f, 0), trim, sides: 14);
                    foreach (float side in new[] { -1f, 1f }) {
                        Box(torso, new Vector3(.1f, .06f, .02f), new Vector3(side * .05f, .14f, -.1f), trim, new Vector3(0, 0, side * 20));
                        Sphere(chest, .06f, new Vector3(side * .155f, .44f, 0), top, null, 10);
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
            case CatgirlOutfit.Shorts: {
                    // A sleeveless shirt: a cap over each shoulder where a sleeve
                    // would start, a small collar and three buttons.
                    foreach (float side in new[] { -1f, 1f }) {
                        Sphere(chest, .05f, new Vector3(side * .14f, .455f, 0), top, new Vector3(.8f, .8f, 1.1f), 10);
                        Box(chest, new Vector3(.05f, .03f, .012f), new Vector3(side * .032f, .515f, .062f), top, new Vector3(-20, 0, side * 28));
                    }
                    for (int button = 0; button < 3; button++) Box(chest, new Vector3(.012f, .012f, .008f), new Vector3(0, .44f - button * .075f, .11f - button * .012f), Mat("9aa0a8"));
                    // High shorts with a red belt, a wide cuff round each thigh, and socks to just under the knee.
                    Cylinder(torso, .118f, .108f, .06f, new Vector3(0, .15f, 0), hips, sides: 14).Scale = new Vector3(1, 1, .7f);
                    Cylinder(torso, .113f, .113f, .022f, new Vector3(0, .185f, 0), trim, sides: 14).Scale = new Vector3(1, 1, .7f);
                    Box(torso, new Vector3(.03f, .026f, .01f), new Vector3(0, .185f, .08f), Metal("e8c14a", .3f, .8f));
                    foreach (bool isLeft in new[] { true, false }) {
                        Cylinder(Rig.Thigh(isLeft), .094f, .086f, .18f, new Vector3(0, -.07f, 0), hips, sides: 12);
                        Cylinder(Rig.Shin(isLeft), .051f, .062f, .3f, new Vector3(0, -.195f, .004f), hips, sides: 10);
                        Cylinder(Rig.Shin(isLeft), .065f, .065f, .022f, new Vector3(0, -.05f, .004f), hips, sides: 10);
                    }
                    break;
                }
            default: {
                    // A hoodie several sizes too large, down to the thighs: its body in two lengths that overlap at the
                    // waist, a hem
                    // band, a hood, a pouch pocket and two cords.
                    Cylinder(torso, .2f, .174f, .33f, new Vector3(0, .095f, 0), top, sides: 14).Scale = new Vector3(1, 1, .78f);
                    Cylinder(chest, .18f, .155f, .3f, new Vector3(0, .34f, 0), top, sides: 14).Scale = new Vector3(1, 1, .78f);
                    Cylinder(torso, .205f, .2f, .05f, new Vector3(0, -.08f, 0), trim, sides: 14).Scale = new Vector3(1, 1, .78f);
                    Sphere(chest, .1f, new Vector3(0, .5f, -.09f), top, new Vector3(1.3f, .7f, .8f), 10);
                    Box(torso, new Vector3(.2f, .09f, .02f), new Vector3(0, .08f, .152f), trim);
                    foreach (float side in new[] { -1f, 1f }) Limb(chest, .006f, .12f, new Vector3(side * .03f, .48f, .125f), Cloth(Lace));
                    break;
                }
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
        Rig.Root.Position = home + new Vector3(frame.Shift, frame.Lift, 0);
        Rig.Root.RotationDegrees = new Vector3(0, frame.Facing, 0);
        Rig.Head.RotationDegrees += frame.Head;
        left.Shape = frame.HandL; right.Shape = frame.HandR;
        left.Turn(frame.WristL); right.Turn(frame.WristR);
        // The tail's tip and her tied hair trail behind the sway.
        for (int i = 0; i < tail.Count; i++)
            tail[i].RotationDegrees = new Vector3(TailCurl[i], 0, Mathf.Lerp(14 * frame.Swing, 38 * frame.Trail, i / (tail.Count - 1f)));
        foreach (var tress in tresses) tress.RotationDegrees = new Vector3(0, 0, -12 * frame.Trail);
        // A quick blink every few seconds.
        float shut = MathF.Pow(Math.Max(0, MathF.Sin(time * 2.3f)), 40);
        foreach (var lid in lids) lid.Scale = new Vector3(1, Math.Max(.02f, shut), 1);
    }

    /// <summary>A hand on the end of a forearm: a palm, four fingers of two joints each and a thumb.</summary>
    private sealed class Hand {
        private readonly Node3D[] fingers = new Node3D[4], tips = new Node3D[4];
        private readonly Node3D thumb, wrist;
        /// <summary>Which way the fingers curl: toward the body, which is -X for the left hand.</summary>
        private readonly float inward;
        private HandShape shape = HandShape.Open;

        public Hand(ActorRig rig, bool isLeft, Material skin) {
            inward = isLeft ? -1 : 1;
            // The wrist is where the forearm ends, a little above where a fist would be.
            wrist = new Node3D(); rig.Hold(wrist, right: !isLeft);
            wrist.Position += new Vector3(0, .05f, 0);
            var root = Pivot(wrist, new Vector3(0, -.05f, 0));
            // The palm runs from the wrist down past where a fist would be.
            // The index finger is the one next to the thumb, ahead.
            Box(root, new Vector3(.018f, .07f, .05f), new Vector3(0, .015f, 0), skin);
            for (int i = 0; i < fingers.Length; i++) {
                fingers[i] = Pivot(root, new Vector3(0, -.02f, .019f - i * .0127f));
                Box(fingers[i], new Vector3(.013f, .03f, .011f), new Vector3(0, -.015f, 0), skin);
                tips[i] = Pivot(fingers[i], new Vector3(0, -.03f, 0));
                Box(tips[i], new Vector3(.012f, .026f, .01f), new Vector3(0, -.013f, 0), skin);
            }
            thumb = Pivot(root, new Vector3(0, .02f, .026f));
            Box(thumb, new Vector3(.013f, .036f, .013f), new Vector3(0, -.014f, .008f), skin, new Vector3(-38, 0, 0));
            Curl();
        }

        /// <summary>
        /// Turns the hand at the wrist: a bend toward its palm, a twist of the forearm that brings the thumb in toward
        /// the body, and a tip toward the thumb's side, in degrees.
        /// </summary>
        public void Turn(Vector3 wristTurn) => wrist.RotationDegrees = new Vector3(wristTurn.Z, wristTurn.Y * inward, wristTurn.X * inward);

        public HandShape Shape {
            set {
                if (value == shape) return;
                shape = value; Curl();
            }
        }

        private void Curl() {
            // How far the index finger and the other three bend at each
            // joint, and how far the thumb turns in, in degrees.
            var (index, others, thumbIn) = shape switch {
                HandShape.Fist => (80f, 80f, 40f),
                HandShape.Point => (0f, 85f, 45f),
                HandShape.Ring => (55f, 8f, 55f),
                _ => (6f, 6f, 0f),
            };
            for (int i = 0; i < fingers.Length; i++) {
                float bend = (i == 0 ? index : others) * inward;
                fingers[i].RotationDegrees = new Vector3(0, 0, bend); tips[i].RotationDegrees = new Vector3(0, 0, bend);
            }
            thumb.RotationDegrees = new Vector3(0, thumbIn * inward, 0);
        }
    }
}
