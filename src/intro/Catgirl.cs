using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

internal enum CatgirlOutfit { Maid, Sailor, Hoodie }

/// <summary>What a catgirl's hand does: spread open, shut, one finger pointing, or finger and thumb closed in a ring.</summary>
internal enum HandShape { Open, Fist, Point, Ring }

/// <summary>
/// A catgirl dancing on the web page in the intro film: the game's person model in its slim build, with a face, hair,
/// cat ears, a tail, hands with fingers and an outfit of her own.
/// </summary>
internal sealed class Catgirl {
    public readonly ActorRig Rig;
    private readonly CatgirlOutfit outfit;
    private readonly Node3D head, torso, chest;
    private readonly StandardMaterial3D skin = Mat(ActorRig.CatgirlSkin), locks;
    private readonly List<Node3D> tail = new(), lids = new(), tresses = new();
    private readonly Hand left, right;
    private readonly Vector3 home;
    /// <summary>How far each joint of the tail bends at rest, in degrees: back and down from the spine, then curling up.</summary>
    private static readonly float[] TailCurl = { -115, 28, 30, 30, 25 };

    /// <param name="at">Where she stands on the stage.</param>
    public Catgirl(CatgirlOutfit outfit, Vector3 at) {
        this.outfit = outfit;
        var (hair, eyes, clothes) = outfit switch {
            CatgirlOutfit.Maid => ("f29ab8", "3f8fd6", new CatgirlClothes("25222c", "25222c", "f6f1f4", "1d1b20", false, false)),
            CatgirlOutfit.Sailor => ("e8c56a", "d98a2b", new CatgirlClothes("f4f2ee", "2b3a67", ActorRig.CatgirlSkin, "6b4428", false, true)),
            _ => ("e9e6f2", "8a5fd0", new CatgirlClothes("f0b63f", "f0b63f", ActorRig.CatgirlSkin, "f4f4f4", true, true)),
        };
        Rig = new ActorRig(ActorSkin.Catgirl, clothes: clothes);
        home = at;
        head = Rig.Head; torso = Rig.Torso; chest = Rig.Chest;
        locks = Mat(hair);
        // A smaller head on the same body makes her taller for her head, as a grown girl in a cartoon is.
        head.Scale = Vector3.One * .92f;
        BuildFace(hair, eyes);
        BuildHair();
        BuildTail();
        BuildOutfit();
        left = new Hand(Rig, true, skin); right = new Hand(Rig, false, skin);
    }

    private void BuildFace(string hair, string eyes) {
        StandardMaterial3D white = Mat("ffffff"), dark = Mat("2c2530"), iris = Mat(eyes);
        foreach (float side in new[] { -1f, 1f }) {
            // An eye follows the head's curve: white, iris, pupil and a glint, under a lash with a flick at its outer end.
            var eye = Pivot(head, new Vector3(side * .047f, .135f, .106f), new Vector3(0, side * 12, 0));
            Box(eye, new Vector3(.04f, .044f, .008f), Vector3.Zero, white);
            Box(eye, new Vector3(.03f, .037f, .009f), new Vector3(0, -.002f, .002f), iris);
            Box(eye, new Vector3(.013f, .02f, .01f), new Vector3(0, -.003f, .003f), dark);
            Box(eye, new Vector3(.009f, .009f, .011f), new Vector3(.007f, .009f, .004f), white);
            Box(eye, new Vector3(.046f, .008f, .012f), new Vector3(0, .024f, .002f), dark);
            Box(eye, new Vector3(.014f, .007f, .012f), new Vector3(side * .026f, .028f, .001f), dark, new Vector3(0, 0, side * 32));
            // The lid hangs from the eye's top edge, so stretching it shuts the eye.
            var lid = Pivot(eye, new Vector3(0, .023f, .007f));
            Box(lid, new Vector3(.042f, .046f, .006f), new Vector3(0, -.023f, 0), skin);
            lids.Add(lid);
            Box(head, new Vector3(.034f, .006f, .008f), new Vector3(side * .047f, .178f, .101f), Mat(hair), new Vector3(0, 0, -side * 8));
            Box(head, new Vector3(.03f, .013f, .008f), new Vector3(side * .08f, .105f, .087f), Mat("f4a3ae"), new Vector3(0, side * 40, 0));
        }
        Box(head, new Vector3(.02f, .007f, .008f), new Vector3(0, .083f, .114f), Mat("b5475a"));
    }

    /// <summary>A cap, a fringe of five strands and a lock by each cheek on everyone; then twin tails, a ponytail or a bob.</summary>
    private void BuildHair() {
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
            Sphere(ear, .018f, new Vector3(0, .002f, .026f), Mat("ffffff"), null, 8);
            switch (outfit) {
                case CatgirlOutfit.Maid:
                    // A twin tail tied high at the side with a ribbon, hanging in two lengths.
                    Sphere(head, .03f, new Vector3(side * .125f, .235f, -.03f), Mat("f4f2ee"), null, 8);
                    Tress(new Vector3(side * .13f, .235f, -.03f), new Vector3(0, 0, side * 14), .04f, .032f, .21f);
                    break;
                case CatgirlOutfit.Hoodie:
                    // A bob: a rounded fall by each cheek, turned in at the jaw.
                    Sphere(head, .06f, new Vector3(side * .1f, .085f, -.005f), locks, new Vector3(.7f, 1.5f, 1.25f), 10);
                    break;
            }
        }
        // A ponytail tied at the crown.
        if (outfit != CatgirlOutfit.Sailor) return;
        Sphere(head, .034f, new Vector3(0, .26f, -.1f), Mat("c8372f"), null, 8);
        Tress(new Vector3(0, .26f, -.11f), new Vector3(-28, 0, 0), .045f, .034f, .23f);
    }

    /// <summary>A length of hair in two parts hanging from a tie, which swings as she sways.</summary>
    private void Tress(Vector3 tie, Vector3 turn, float thick, float thin, float length) {
        var root = Pivot(head, tie);
        var hang = Pivot(root, Vector3.Zero, turn);
        Limb(hang, thick, length, Vector3.Zero, locks);
        Limb(hang, thin, length, new Vector3(0, -length + thick * 1.5f, 0), locks);
        tresses.Add(root);
    }

    /// <summary>The tail: capsules end to end, each on a joint at the end of the one before, thinning to a white tip.</summary>
    private void BuildTail() {
        const float segment = .1f;
        var joint = Pivot(torso, new Vector3(0, .06f, -.09f));
        for (int i = 0; i < TailCurl.Length; i++) {
            if (i > 0) joint = Pivot(joint, new Vector3(0, segment, 0));
            float thick = .03f - i * .0015f;
            Limb(joint, thick, segment + thick * 2, new Vector3(0, segment, 0), locks);
            tail.Add(joint);
        }
        Sphere(joint, .027f, new Vector3(0, segment, 0), Mat("ffffff"), null, 8);
    }

    /// <summary>What she wears over the body's own colours: what is above the waist rides on the chest, the rest on the torso under it.</summary>
    private void BuildOutfit() {
        StandardMaterial3D white = Cloth("f4f2ee");
        switch (outfit) {
            case CatgirlOutfit.Maid: {
                    var black = Cloth("25222c");
                    // A flared skirt over a petticoat, an apron with a bib, a waistband tied in a bow at the back, puffed sleeves and a collar.
                    Cylinder(torso, .3f, .1f, .3f, new Vector3(0, -.03f, 0), black, sides: 16);
                    Cylinder(torso, .31f, .29f, .03f, new Vector3(0, -.185f, 0), white, sides: 16);
                    Box(torso, new Vector3(.2f, .25f, .012f), new Vector3(0, -.03f, .207f), white, new Vector3(-33.7f, 0, 0));
                    Box(chest, new Vector3(.12f, .13f, .012f), new Vector3(0, .31f, .112f), white);
                    Cylinder(torso, .102f, .102f, .03f, new Vector3(0, .13f, 0), white, sides: 14);
                    foreach (float side in new[] { -1f, 1f }) {
                        Box(torso, new Vector3(.1f, .06f, .02f), new Vector3(side * .05f, .14f, -.1f), white, new Vector3(0, 0, side * 20));
                        Sphere(chest, .06f, new Vector3(side * .155f, .44f, 0), black, null, 10);
                        Box(chest, new Vector3(.03f, .022f, .012f), new Vector3(side * .02f, .5f, .07f), Cloth("c8372f"), new Vector3(0, 0, side * 18));
                    }
                    Cylinder(chest, .05f, .05f, .02f, new Vector3(0, .535f, 0), white);
                    // A frilled band over the crown, from ear to ear.
                    for (int frill = -2; frill <= 2; frill++) {
                        float angle = Mathf.DegToRad(frill * 25);
                        Box(head, new Vector3(.055f, .03f, .015f), new Vector3(MathF.Sin(angle) * .125f, .185f + MathF.Cos(angle) * .105f, .03f), white, new Vector3(0, 0, -frill * 25));
                    }
                    break;
                }
            case CatgirlOutfit.Sailor: {
                    StandardMaterial3D navy = Cloth("2b3a67"), red = Cloth("c8372f");
                    // A pleated skirt with a white stripe, the blouse's hem over it, a sailor collar and a knotted scarf.
                    Cylinder(torso, .27f, .1f, .25f, new Vector3(0, -.005f, 0), navy, sides: 18);
                    Cylinder(torso, .254f, .246f, .014f, new Vector3(0, -.1f, 0), white, sides: 18);
                    Cylinder(torso, .106f, .1f, .04f, new Vector3(0, .14f, 0), white, sides: 14);
                    Box(chest, new Vector3(.2f, .12f, .012f), new Vector3(0, .44f, -.084f), navy);
                    foreach (float side in new[] { -1f, 1f }) {
                        Box(chest, new Vector3(.075f, .016f, .15f), new Vector3(side * .09f, .503f, 0), navy, new Vector3(0, 0, -side * 22));
                        Box(chest, new Vector3(.036f, .13f, .012f), new Vector3(side * .045f, .435f, .088f), navy, new Vector3(0, 0, side * 22));
                        Box(chest, new Vector3(.022f, .07f, .012f), new Vector3(side * .016f, .315f, .116f), red, new Vector3(0, 0, side * 12));
                    }
                    Box(chest, new Vector3(.04f, .04f, .02f), new Vector3(0, .37f, .118f), red);
                    break;
                }
            default: {
                    StandardMaterial3D hoodie = Cloth("f0b63f"), trim = Cloth("d99f2b");
                    // A hoodie several sizes too large, down to the thighs: its body in two lengths that overlap at the waist, a hem
                    // band, a hood, a pouch pocket and two cords.
                    Cylinder(torso, .2f, .174f, .33f, new Vector3(0, .095f, 0), hoodie, sides: 14).Scale = new Vector3(1, 1, .78f);
                    Cylinder(chest, .18f, .155f, .3f, new Vector3(0, .34f, 0), hoodie, sides: 14).Scale = new Vector3(1, 1, .78f);
                    Cylinder(torso, .205f, .2f, .05f, new Vector3(0, -.08f, 0), trim, sides: 14).Scale = new Vector3(1, 1, .78f);
                    Sphere(chest, .1f, new Vector3(0, .5f, -.09f), hoodie, new Vector3(1.3f, .7f, .8f), 10);
                    Box(torso, new Vector3(.2f, .09f, .02f), new Vector3(0, .08f, .152f), trim);
                    foreach (float side in new[] { -1f, 1f }) Limb(chest, .006f, .12f, new Vector3(side * .03f, .48f, .125f), white);
                    break;
                }
        }
    }

    /// <summary>Poses her for a moment of a dance; <paramref name="time"/> is her own clock, for her blinks.</summary>
    public void Show(in DanceFrame frame, float time) {
        Rig.Show(frame.Pose, null, false);
        Rig.RollArm(true, frame.RollL); Rig.RollArm(false, frame.RollR);
        Rig.BendChest(frame.Chest.X, frame.Chest.Y, frame.Chest.Z);
        Rig.TiltHips(frame.HipTilt);
        Rig.SplayLeg(true, frame.SplayL); Rig.SplayLeg(false, frame.SplayR);
        Rig.Root.Position = home + new Vector3(frame.Shift, 0, 0);
        head.RotationDegrees += frame.Head;
        left.Shape = frame.HandL; right.Shape = frame.HandR;
        left.Bend = frame.WristL; right.Bend = frame.WristR;
        // The tail's tip and her tied hair trail behind the sway.
        for (int i = 0; i < tail.Count; i++) tail[i].RotationDegrees = new Vector3(TailCurl[i], 0, Mathf.Lerp(14 * frame.Swing, 38 * frame.Trail, i / (tail.Count - 1f)));
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
            // The palm runs from the wrist down past where a fist would be; the index finger is the one next to the thumb, ahead.
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

        /// <summary>Degrees the hand bends toward its palm at the wrist; negative bends it back.</summary>
        public float Bend { set => wrist.RotationDegrees = new Vector3(0, 0, value * inward); }

        public HandShape Shape {
            set {
                if (value == shape) return;
                shape = value; Curl();
            }
        }

        private void Curl() {
            // How far the index finger and the other three bend at each joint, and how far the thumb turns in, in degrees.
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
