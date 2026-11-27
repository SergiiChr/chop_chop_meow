using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static CampfireAnimationSteps;
using static Checks;
using static ModelSteps;

internal static class CampfireAnimationChecks {
    internal static void Run() {
        var rig = new ActorRig(ActorSkin.Worker);
        try {
            Test(() => CheckTheSeatedPlayerOfAnInstrumentKeepsTheirFeetStill(rig));
            Test(() => CheckBothArmsStayOutsideTheGuitar(rig));
            Test(() => CheckBothArmsStayOutsideTheGuitarAsTheWorkerSits(rig));
            Test(() => CheckTheGuitarHandCrossesTheStringsOnTheBeat(rig));
            Test(() => CheckTheFingersCurlOverTheGuitarNeck(rig));
            Test(() => CheckTheHarmonicaStaysAgainstTheMouthWhilePlaying(rig));
            Test(() => CheckTheDrumHandLandsOnTheHeadOnTheBeat(rig));
            Test(() => CheckInstrumentsRestInTheHandsAndGoAwayWhenTheWorkerStands(rig));
            Test(() => CheckAListenerNodsOnTheBeatWithoutMovingTheirHands(rig));
            Test(() => CheckAListenerLowersTheirHandSmoothlyWhenTheSongStarts(rig));
        } finally { rig.Root.Free(); }
    }

    private static void CheckTheFingersCurlOverTheGuitarNeck(ActorRig rig) {
        var guitar = Instrument(rig, CampfireInstrument.Guitar);
        var fingers = guitar.GetNode<MeshInstance3D>("FrettingFingers").Mesh.GetFaces();
        var soundboard = Soundboard(guitar);
        Require(fingers.All(p => !InsideGuitar(p, soundboard)), "the curled fingers must stay outside the guitar's neck");
        Require(fingers.Any(p => p.X is > .25f and < .42f && Math.Abs(p.Y) < .013f && p.Z is > .055f and < .066f),
            "the fingertips should reach the strings from the front of the fingerboard");
        foreach (var seat in Seats) {
            Seat(rig, seat);
            rig.AnimateCampfireMusic(CampfireInstrument.Guitar, 1, 0, 0);
            var palm = HandAt(rig, guitar, true);
            Require(fingers.Any(p => p.DistanceTo(palm) < .02f), "the curled fingers should start inside the fretting palm");
        }
    }

    private static void CheckBothArmsStayOutsideTheGuitar(ActorRig rig) {
        var guitar = Instrument(rig, CampfireInstrument.Guitar);
        var soundboard = Soundboard(guitar);
        int mostInside = 0;
        string worst = "";
        foreach (var seat in Seats)
            for (int breath = 1; breath <= 3; breath += 2)
                for (int frame = 0; frame <= 60; frame++)
                    for (int beatWeight = 0; beatWeight <= 4; beatWeight++) {
                        Seat(rig, seat, breath);
                        float playing = beatWeight * .25f;
                        rig.AnimateCampfireMusic(CampfireInstrument.Guitar, playing, 0, frame / 60f);
                        var left = ArmSurface(rig, guitar, true).Where(p => InsideGuitar(p, soundboard)).ToArray();
                        var right = ArmSurface(rig, guitar, false).Where(p => InsideGuitar(p, soundboard)).ToArray();
                        if (left.Length + right.Length <= mostInside) continue;
                        mostInside = left.Length + right.Length;
                        worst = $"{seat}, frame {frame}, playing {playing}: " +
                            $"{left.Length} left points near {left.FirstOrDefault()} and {right.Length} right points near {right.FirstOrDefault()}";
                    }
        Require(mostInside == 0, $"neither arm should enter the guitar. Worst pose: {worst}");
    }

    private static void CheckBothArmsStayOutsideTheGuitarAsTheWorkerSits(ActorRig rig) {
        var guitar = Instrument(rig, CampfireInstrument.Guitar);
        var soundboard = Soundboard(guitar);
        foreach (var seat in Seats) {
            var actor = new ActorState { Action = ActorAction.Walk, Time = .2f, Moving = true };
            for (int frame = 0; frame <= 14; frame++) {
                actor.Update(Vector2.Zero, seat, Vector2.Right, 1 / 60f, eating: false);
                rig.Show(actor.Pose, null, false, onRock: seat == ActorAction.Perch);
                rig.AnimateCampfireMusic(CampfireInstrument.Guitar, 0, 0, 0);
                Require(!ArmSurface(rig, guitar, true).Any(p => InsideGuitar(p, soundboard)) &&
                    !ArmSurface(rig, guitar, false).Any(p => InsideGuitar(p, soundboard)),
                    $"the arms should stay outside the guitar on frame {frame} of taking a {seat} seat");
            }
        }
    }

    private static void CheckTheSeatedPlayerOfAnInstrumentKeepsTheirFeetStill(ActorRig rig) {
        foreach (var seat in Seats) {
            Seat(rig, seat, 2.7f);
            var left = rig.BootSole(true);
            var right = rig.BootSole(false);
            foreach (var instrument in CampfireInstrumentShape.All)
                for (int frame = 0; frame <= 60; frame++) {
                    Seat(rig, seat, 2.7f);
                    rig.AnimateCampfireMusic(instrument, 1, 0, frame / 60f);
                    Require(rig.BootSole(true) == left && rig.BootSole(false) == right,
                        $"{instrument} should leave both boots in the {seat} pose at frame {frame}");
                }
        }
    }

    private static void CheckTheGuitarHandCrossesTheStringsOnTheBeat(ActorRig rig) {
        var guitar = Instrument(rig, CampfireInstrument.Guitar);
        foreach (var seat in Seats) {
            for (int frame = 0; frame <= 60; frame++) {
                Seat(rig, seat, frame / 60f);
                rig.AnimateCampfireMusic(CampfireInstrument.Guitar, 1, 0, frame / 60f);
                var neck = HandAt(rig, guitar, true);
                // The left hand supports the underside of the guitar's neck, away from the body and tuners.
                Require(neck.X is > .22f and < .43f && neck.Y is > -.072f and < -.05f && Math.Abs(neck.Z) < .08f,
                    $"the left hand should stay on the guitar neck at frame {frame}, got {neck}");
                var strum = HandAt(rig, guitar, false, pad: true);
                if (frame % 60 == 0) {
                    float nearStrings = ArmSurface(rig, guitar, false)
                        .Where(p => p.X is > .025f and < .10f && Math.Abs(p.Y) < .018f)
                        .Select(p => p.Z).DefaultIfEmpty(float.PositiveInfinity).Min();
                    Require(strum.X is > .015f and < .11f && Math.Abs(strum.Y) < .015f && nearStrings is > .041f and < .074f,
                        $"the hand should cross the strings at the sound hole on the beat, got {strum} and closest surface {nearStrings}");
                    float underNeck = ArmSurface(rig, guitar, true)
                        .Where(p => p.X is > .22f and < .43f && p.Z is > .009f and < .055f)
                        .Select(p => p.Y).DefaultIfEmpty(float.NegativeInfinity).Max();
                    Require(underNeck is > -.034f and < -.018f, $"the fretting hand should support the neck from below, got {underNeck}");
                }
                if (frame == 9)
                    Require(strum.Y < -.068f, $"the downstroke should continue below the strings, got {strum}");
                if (frame == 30)
                    Require(strum.Z > .107f, $"the hand should return without brushing the strings again, got {strum}");
            }
        }
    }

    private static void CheckTheHarmonicaStaysAgainstTheMouthWhilePlaying(ActorRig rig) {
        var harmonica = Instrument(rig, CampfireInstrument.Harmonica);
        foreach (var seat in Seats)
            for (int frame = 0; frame <= 60; frame++) {
                Seat(rig, seat, frame / 60f);
                rig.AnimateCampfireMusic(CampfireInstrument.Harmonica, 1, 0, frame / 60f);
                var mouth = harmonica.Transform.AffineInverse() * (InRig(rig, rig.Head) * new Vector3(0, .074f, .108f));
                Require(Math.Abs(mouth.X) < .035f && Math.Abs(mouth.Y) < .013f && mouth.Z is > -.039f and < -.01f,
                    $"the harmonica's narrow rear edge should stay against the lips at frame {frame}, got {mouth}");
                var left = HandAt(rig, harmonica, true);
                var right = HandAt(rig, harmonica, false);
                Require(left.DistanceTo(new Vector3(.072f, 0, .015f)) < .024f &&
                    right.DistanceTo(new Vector3(-.072f, 0, .015f)) < .024f,
                    $"both ends of the harmonica should stay in the hands at frame {frame}, got {left} and {right}");
            }
    }

    private static void CheckTheDrumHandLandsOnTheHeadOnTheBeat(ActorRig rig) {
        var drum = Instrument(rig, CampfireInstrument.Drum);
        foreach (var seat in Seats)
            for (int frame = 0; frame <= 60; frame++) {
                Seat(rig, seat, frame / 60f);
                rig.AnimateCampfireMusic(CampfireInstrument.Drum, 1, 0, frame / 60f);
                var rim = HandAt(rig, drum, true);
                Require(rim.X is > .115f and < .18f && rim.Y is > .022f and < .069f && Math.Abs(rim.Z) < .035f,
                    $"the left hand should keep hold of the rim at frame {frame}, got {rim}");
                var hit = HandAt(rig, drum, false, pad: true);
                Require(new Vector2(hit.X, hit.Z).Length() < .13f && hit.Y > .059f,
                    $"the striking hand should stay over the drumhead, not go through it, at frame {frame}, got {hit}");
                if (frame % 60 == 0) Require(hit.Y < .087f, $"the striking hand should hit the skin on the beat, got {hit}");
                if (frame == 30) Require(hit.Y > .19f, $"the striking hand should lift for the next beat, got {hit}");
            }
    }

    private static void CheckInstrumentsRestInTheHandsAndGoAwayWhenTheWorkerStands(ActorRig rig) {
        foreach (var instrument in CampfireInstrumentShape.All) {
            var prop = Instrument(rig, instrument);
            Require(Triangles(prop) is > 0 and <= 1_000, $"the held {instrument} should fit the carried prop's triangle budget");
            Seat(rig, ActorAction.Sit);
            rig.AnimateCampfireMusic(instrument, 0, 0, .26f);
            foreach (var other in CampfireInstrumentShape.All)
                Require(Instrument(rig, other).Visible == (other == instrument), $"only the assigned {instrument} should be visible while sitting quietly");
            Require(prop.Position.Y < .48f, $"the {instrument} should rest in the hands below the chest, not stay raised");
        }
        rig.Show(new Pose(), null, false);
        rig.AnimateCampfireMusic(CampfireInstrument.None, 0, 0, 0);
        foreach (var instrument in CampfireInstrumentShape.All)
            Require(!Instrument(rig, instrument).Visible, $"the {instrument} should be put away when the worker stands");
    }

    private static void CheckAListenerNodsOnTheBeatWithoutMovingTheirHands(ActorRig rig) {
        Seat(rig, ActorAction.Sit, 2.8f);
        var right = rig.Fist(false);
        var left = rig.Fist(true);
        float still = rig.Head.RotationDegrees.X;
        rig.AnimateCampfireMusic(CampfireInstrument.None, 0, 1, 0);
        float down = rig.Head.RotationDegrees.X;
        Seat(rig, ActorAction.Sit, 2.8f);
        rig.AnimateCampfireMusic(CampfireInstrument.None, 0, 1, .5f);
        Require(down - still > 7 && Math.Abs(rig.Head.RotationDegrees.X - still) < 1, "the listener's chin should dip and rise on the beat");
        Require(rig.Fist(true) == left && rig.Fist(false) == right, "the listener's hands should stay still through the nod");
        rig.Show(ActorAnimation.PoseAt(ActorAction.Sit, 2.8f, false), null, false);
        Require(rig.Fist(false).DistanceTo(right) > .1f, "a listener should not lift a hand in the usual eating gesture");
    }

    private static void CheckAListenerLowersTheirHandSmoothlyWhenTheSongStarts(ActorRig rig) {
        var listener = new ActorState { Action = ActorAction.Sit, Time = 2.8f };
        rig.Show(listener.Pose, null, false);
        var eating = rig.Fist(false);
        listener.Update(Vector2.Zero, ActorAction.Sit, Vector2.Right, 1 / 60f, eating: false);
        rig.Show(listener.Pose, null, false);
        Require(rig.Fist(false).DistanceTo(eating) < .04f, "the hand should not jump away from the mouth when the song starts");
        for (int frame = 0; frame < 14; frame++) listener.Update(Vector2.Zero, ActorAction.Sit, Vector2.Right, 1 / 60f, eating: false);
        rig.Show(listener.Pose, null, false);
        Require(rig.Fist(false).DistanceTo(eating) > .1f, "the listener should finish lowering the hand to their lap");
    }
}
