using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static CatgirlSteps;
using static Checks;

/// <summary>Checks of the catgirls dancing on the hero's screen in the intro film: the dance and their bodies as drawn.</summary>
public static class CatgirlDanceChecks {
    public static void Run() {
        Test(CheckDancersHoldAndChangePosesTogether);
        Test(CheckDancersDipTogetherOnTheBeat);
        Test(CheckDancersPlantAFootOnTheStageInEveryPose);
        Test(CheckTheChestBendsAboveAStablePelvis);
        Test(CheckEachDancerBodyIsOneClosedSurface);
    }

    /// <summary>The dancers share pose changes, with time to read a pose and quicker changes through a flourish.</summary>
    private static void CheckDancersHoldAndChangePosesTogether() {
        var lead = CatgirlDance.Solo(0);
        var sides = CatgirlDance.Chorus(0);
        int lastChange = 0, shortestHold = 4, longestHold = 0;
        for (int frame = 1; frame < 150; frame++) {
            float time = frame / 30f;
            var nextLead = CatgirlDance.Solo(time);
            var nextSides = CatgirlDance.Chorus(time);
            Require((nextLead == lead) == (nextSides == sides), $"lead and chorus should change pose together at frame {frame}");
            if (nextLead == lead) continue;
            int hold = frame - lastChange;
            Require(hold >= 2 && hold <= 4, $"pose from frame {lastChange} should hold for two to four frames, held for {hold}");
            shortestHold = Math.Min(shortestHold, hold);
            longestHold = Math.Max(longestHold, hold);
            lastChange = frame;
            lead = nextLead;
            sides = nextSides;
        }
        Require(shortestHold == 2 && longestHold == 4 && lastChange >= 146,
            "both quick and slow pose changes should play across the dance");
    }

    /// <summary>The center and side dancers put weight into the same accented poses.</summary>
    private static void CheckDancersDipTogetherOnTheBeat() {
        foreach (int frame in new[] { 13, 40, 93, 147 }) {
            float before = (frame - 3) / 30f, on = frame / 30f, after = (frame + 4) / 30f;
            float lead = CatgirlDance.Solo(on).Pose.HipHeight;
            float sides = CatgirlDance.Chorus(on).Pose.HipHeight;
            Require(lead < CatgirlDance.Solo(before).Pose.HipHeight && lead < CatgirlDance.Solo(after).Pose.HipHeight,
                $"the lead should dip and rebound at frame {frame}");
            Require(sides < CatgirlDance.Chorus(before).Pose.HipHeight && sides < CatgirlDance.Chorus(after).Pose.HipHeight,
                $"the sides should dip and rebound at frame {frame}");
        }
    }

    /// <summary>
    /// Through the whole phrase, the lowest point of each dancer's drawn body, a sole, rests on the stage, also in poses with
    /// one foot in the air; the dancers stand on shins a little longer than their thighs.
    /// </summary>
    private static void CheckDancersPlantAFootOnTheStageInEveryPose() {
        Require(HumanShape.Slim.Shin / ActorAnimation.Thigh is > 1.05f and < 1.12f,
            $"the dancers' shins should be slightly longer than their thighs, got {HumanShape.Slim.Shin / ActorAnimation.Thigh:0.00}");
        var soloist = new Catgirl(CatgirlOutfit.Blouse, Vector3.Zero);
        var chorus = new Catgirl(CatgirlOutfit.Maid, Vector3.Zero);
        for (int frame = 0; frame < 160; frame++) {
            soloist.Show(CatgirlDance.Solo(frame / 30f), 0);
            chorus.Show(CatgirlDance.Chorus(frame / 30f), 0);
            float soloSole = DrawnBody(soloist).Points.Min(point => point.Y), chorusSole = DrawnBody(chorus).Points.Min(point => point.Y);
            Require(Math.Abs(soloSole) < .006f, $"the soloist should stand on the stage at frame {frame}, her lowest sole is at {soloSole:0.000} m");
            Require(Math.Abs(chorusSole) < .006f, $"the chorus should stand on the stage at frame {frame}, her lowest sole is at {chorusSole:0.000} m");
        }
        soloist.Rig.Root.Free();
        chorus.Rig.Root.Free();
    }

    /// <summary>
    /// When the chest bends, the drawn body below the waist stays over the legs and the collar follows the chest.
    /// The arms are held out so that none of them hangs beside the hips.
    /// </summary>
    private static void CheckTheChestBendsAboveAStablePelvis() {
        var dancer = new Catgirl(CatgirlOutfit.Blouse, Vector3.Zero);
        var armsOut = new DanceFrame { Pose = new Pose(OutL: 85, OutR: 85) };
        dancer.Show(armsOut, 0);
        var upright = DrawnBody(dancer).Points;
        dancer.Show(armsOut with { Chest = new Vector3(-10, 16, 14) }, 0);
        var bent = DrawnBody(dancer).Points;
        float hips = armsOut.Pose.HipHeightFor(HumanShape.Slim.Shin);
        // Below this height the body is the pelvis's alone; above it the waist starts to turn with the chest.
        float belowWaist = hips + HumanShape.Slim.Waist - .07f;
        float pelvisMoved = upright.Select((point, i) => point.Y < belowWaist ? point.DistanceTo(bent[i]) : 0).Max();
        int collar = Array.IndexOf(upright, upright.MaxBy(point => point.Y));
        Require(pelvisMoved < .001f, $"the pelvis should stay over the legs when the chest bends, it moved {pelvisMoved:0.000} m");
        Require(upright[collar].DistanceTo(bent[collar]) > .04f, "the collar should follow the chest as it bends");
        dancer.Rig.Root.Free();
    }

    /// <summary>Each dancer's drawn body is one closed surface: every edge joins two triangles that run along it opposite ways.</summary>
    private static void CheckEachDancerBodyIsOneClosedSurface() {
        foreach (var outfit in Enum.GetValues<CatgirlOutfit>()) {
            var dancer = new Catgirl(outfit, Vector3.Zero);
            var triangles = DrawnBody(dancer).Triangles;
            var edges = Edges(triangles);
            Require(edges.Values.All(edge => edge is (2, 0)), $"each edge of the {outfit} dancer's body should join two faces with opposite winding");
            Require(triangles.Distinct().Count() - edges.Count + triangles.Length / 3 == 2, $"the {outfit} dancer's body should be one closed surface");
            dancer.Rig.Root.Free();
        }
    }
}
