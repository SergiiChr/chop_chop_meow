using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Draws a player knocked or tugged off their feet falling backward onto their back: the path of the fall, the legs on
/// the ground, where their throat is as they go over, and their hands pushing at the wolf's head.
/// </summary>
public static class FallBackView {
    // Shares of the fall: the yank before the hips drop, the seat landing, and the back landing.
    private const float FallDrops = .2f, FallSits = .55f, FallLies = .72f;
    /// <summary>How high the hips stand above the boots in the rig through a fall backward.</summary>
    public const float FallHips = .77f;
    // How high the hips lie once down, and how far the rig tips back by the time the seat lands.
    private const float LyingHips = .17f, SittingTip = 30;
    /// <summary>
    /// A player yanked backward off their feet a share <paramref name="u"/> through <see cref="ActorAnimation.FallBackSeconds"/>, posed as
    /// <paramref name="pose"/>: the hips drop onto the seat faster and faster with the boots sliding out ahead along the ground,
    /// then the body rolls back onto the back, the legs coming up, and lies with the hips <paramref name="lying"/> back from where they stood.
    /// Gives the pose with its legs solved to the ground, how far the rig tips back in degrees, and where the hips are: ahead of where the player stood, and up.
    /// </summary>
    public static (Pose Pose, float Tip, Vector2 Hips) FallingBack(Pose pose, float u, float lying) {
        u = Math.Clamp(u, 0, 1);
        var (tip, hips) = FallPath(u, lying);
        var fitted = pose with { Hips = FallHips };
        // The boots are held to the ground from the yank until the body rolls back, sliding out ahead as the seat drops.
        float planted = Mathf.SmoothStep(.1f, FallDrops + .04f, u) * (1 - Mathf.SmoothStep(FallSits - .05f, FallLies - .05f, u));
        if (planted <= 0) return (fitted, tip, hips);
        float t = Mathf.DegToRad(tip), slide = Mathf.SmoothStep(FallDrops, FallSits, u);
        var left = OnGround(Mathf.Lerp(.42f, .62f, slide) - hips.X, hips.Y, t);
        var right = OnGround(Mathf.Lerp(-.12f, .42f, slide) - hips.X, hips.Y, t);
        return (fitted with {
            SwingL = Mathf.Lerp(pose.SwingL, left.Swing, planted),
            KneeL = Mathf.Lerp(pose.KneeL, left.Knee, planted),
            FootL = Mathf.Lerp(pose.FootL, left.Foot, planted),
            SwingR = Mathf.Lerp(pose.SwingR, right.Swing, planted),
            KneeR = Mathf.Lerp(pose.KneeR, right.Knee, planted),
            FootR = Mathf.Lerp(pose.FootR, right.Foot, planted),
        }, tip, hips);
    }

    /// <summary>
    /// How far a body falling backward has tipped back, in degrees, and where its hips are, ahead of where it stood and up,
    /// a share <paramref name="u"/> through the fall; see <see cref="FallingBack"/>.
    /// </summary>
    public static (float Tip, Vector2 Hips) FallPath(float u, float lying) {
        u = Math.Clamp(u, 0, 1);
        float drop = Math.Clamp((u - FallDrops) / (FallSits - FallDrops), 0, 1), roll = Math.Clamp((u - FallSits) / (FallLies - FallSits), 0, 1);
        float top = FallHips - .05f * Mathf.SmoothStep(0, FallDrops, u);
        float bounce = u > FallSits && u < FallSits + .1f ? .025f * MathF.Sin(MathF.PI * (u - FallSits) / .1f) : 0;
        float up = top - (top - LyingHips) * drop * drop + bounce, back = lying * Mathf.SmoothStep(FallDrops - .05f, FallSits, u);
        return (SittingTip * drop * drop + (90 - SittingTip) * roll * roll, new Vector2(back, up));
    }

    /// <summary>The leg angles that stand a boot flat on the ground <paramref name="ahead"/> of hips <paramref name="up"/> above it, in a rig tipped back <paramref name="tip"/> radians.</summary>
    private static ActorAnimation.LegAngles OnGround(float ahead, float up, float tip) {
        // The ankle stands a sole's height above the ground; in the tipped rig, ahead tilts up and down tilts back.
        float below = up - ActorAnimation.Sole;
        var (swing, knee) = ActorAnimation.LegTo(ahead * MathF.Cos(tip) - below * MathF.Sin(tip), ahead * MathF.Sin(tip) + below * MathF.Cos(tip));
        return new ActorAnimation.LegAngles(swing, knee, -Mathf.RadToDeg(tip) + swing - knee);
    }

    /// <summary>How far a knocked-down player's feet slide out ahead of where they stood, toward the wolf, as they go over onto their back.</summary>
    public const float KnockDownSlide = .65f;
    /// <summary>
    /// Poses a player knocked or yanked off their feet backward as <see cref="FallingBack"/> has them, onto
    /// their back, <see cref="KnockDownSlide"/> on from where they stood, where <see cref="Throat"/> finds their throat.
    /// </summary>
    public static void FallBack(ActorRig rig, ActorState state, Pose standing, float downedFor, bool torch, WolfModel? wolf) {
        var (pose, tip, hips) = FallingBack(standing, downedFor / ActorAnimation.FallBackSeconds, KnockDownSlide - FallHips);
        state.HipHeight = pose.HipHeight; state.Lift = 0; state.LiftSpeed = 0; state.OnSteps = false;
        float t = Mathf.DegToRad(tip);
        rig.Root.Position = FallRoot(state.Position, ActorAnimation.Forward(state.Yaw), t, hips);
        rig.Root.Rotation = new Vector3(-t, state.Yaw, 0);
        // The hands push the wolf's head off by the back of the jaw, while it is in their reach; with the head too close for the
        // arm to stay out at the side, a hand takes the neck further back instead.
        float rollL = 0, rollR = 0;
        if (wolf != null) {
            var place = wolf.Root.Transform;
            var across = place.Basis.X.Normalized() * JawHold;
            var (neckRoot, jaws) = wolf.NeckLine;
            for (int arm = 0; arm < 2; arm++) {
                bool left = arm == 0;
                var shoulder = ArmReach.Shoulder(pose, rig.Root.Transform, left);
                Vector3 hold = Vector3.Zero;
                for (float share = WolfModel.JawShare; share >= 0; share -= .05f) {
                    var on = place * neckRoot.Lerp(jaws, share);
                    // Each hand takes the side of the neck nearer its own shoulder.
                    hold = shoulder.DistanceTo(on + across) < shoulder.DistanceTo(on - across) ? on + across : on - across;
                    if (shoulder.DistanceTo(hold) >= HoldFrom) break;
                }
                float grip = Mathf.SmoothStep(ArmLength + .08f, ArmLength - .04f, shoulder.DistanceTo(hold)) * Mathf.SmoothStep(HoldFrom - .08f, HoldFrom, shoulder.DistanceTo(hold));
                if (grip <= 0) continue;
                // The elbow stays out at its own side and down.
                (pose, float roll) = ArmReach.Reaching(pose, hold, rig.Root.Transform, left, grip, new Vector3(left ? 1 : -1, -1, 0));
                if (left) rollL = roll; else rollR = roll;
            }
        }
        rig.Show(pose, null, false, false, torch);
        if (rollL != 0) rig.RollArm(true, rollL);
        if (rollR != 0) rig.RollArm(false, rollR);
    }

    /// <summary>How far to either side of the middle of a wolf's jaw a player's hands push at it, and how far a hand reaches from its shoulder.</summary>
    private const float JawHold = .07f, ArmLength = ActorRig.UpperArm + ActorRig.Forearm;
    /// <summary>How far from its shoulder a hand pushing a wolf off holds it at least, with the elbow out at the side rather than folded across the chest.</summary>
    private const float HoldFrom = .3f;
    /// <summary>The rig's throat: above the feet and a little in front, before a knockdown tips it back.</summary>
    private const float ThroatHeight = 1.31f, ThroatAhead = .06f;
    /// <summary>
    /// Where the root of a player's rig is <paramref name="tip"/> radians into a fall backward with the hips at
    /// <paramref name="hips"/>, ahead of where they stood at <paramref name="at"/> facing <paramref name="forward"/> and up: the rig
    /// turns about its feet, so they go where the tipped hips need them.
    /// </summary>
    private static Vector3 FallRoot(Vector2 at, Vector2 forward, float tip, Vector2 hips) =>
        WorldView.StandsAt(at) + WorldView.To3(forward) * (hips.X + FallHips * MathF.Sin(tip)) + Vector3.Up * (hips.Y - FallHips * MathF.Cos(tip));

    /// <summary>Where the throat of a player at <paramref name="at"/> facing <paramref name="toward"/> is, <paramref name="downedFor"/> seconds into being knocked down; it follows FallBack.</summary>
    public static Vector3 Throat(Vector2 at, Vector2 toward, float downedFor) {
        var (tip, hips) = FallPath(downedFor / ActorAnimation.FallBackSeconds, KnockDownSlide - FallHips);
        float back = Mathf.DegToRad(tip);
        var forward = toward.Normalized();
        return FallRoot(at, forward, back, hips) + WorldView.To3(forward) * (ThroatAhead * MathF.Cos(back) - ThroatHeight * MathF.Sin(back)) + Vector3.Up * (ThroatHeight * MathF.Cos(back) + ThroatAhead * MathF.Sin(back));
    }
}
