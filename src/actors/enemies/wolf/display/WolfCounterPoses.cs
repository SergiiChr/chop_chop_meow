using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Poses the recoil, two positioning hops and forepaw landing of a wolf circling its attacker.</summary>
internal static class WolfCounterPoses {
    internal static void Show(WolfModel model, Wolf wolf, Vector2 player, WolfCounterStage stage, float u, float dt) {
        float side = wolf.CounterSide;
        float down = Mathf.SmoothStep(0, 1, u);
        float air = MathF.Sin(Mathf.Pi * u);
        var body = stage switch {
            WolfCounterStage.Wince => new WolfPose(Lift: -.13f * down, Roll: side * 14 * down, Pitch: 7 * down,
                Flex: 11 * down, Neck: 20 * down, Head: -23 * down, HeadTurn: -side * 32 * down, Ears: 42 * down,
                Jaw: 18 * (1 - .7f * down), Snarl: down, Tail: -65 - 20 * down, Wag: -side * 22 * down),
            WolfCounterStage.SideHop => new WolfPose(Lift: Mathf.Lerp(-.09f, -.02f, down),
                Roll: side * (12 * air - 9 * down),
                Pitch: 6 * down, Flex: 12 * air, Neck: 20, Head: -16, Ears: 25, Jaw: 10, Snarl: .75f,
                Tail: -45 - 18 * down, Wag: -side * (20 + 25 * air)),
            WolfCounterStage.Slide => new WolfPose(Lift: -.08f - .065f * air, Roll: -side * (13 - 8 * down),
                Pitch: 8 + 8 * air, Flex: 7, Neck: 22, Head: -21, Jaw: 11, Ears: 16, Snarl: .65f,
                Tail: -42 - 20 * down, Wag: -side * (32 - 20 * down)),
            WolfCounterStage.Bound => new WolfPose(Lift: -.04f + .06f * air, Pitch: Mathf.Lerp(-9, 18, down),
                Flex: -13 * air, Neck: 10, Head: -12, Ears: 22, Jaw: 8, Tail: -28 - 18 * down,
                Wag: -side * 10 * air),
            _ => new WolfPose(Lift: -.085f + .06f * down, Pitch: 17 * (1 - down), Roll: side * 9 * air,
                Flex: 7 * (1 - down), Bend: side * 10 * air, Neck: 17 - 19 * down, Head: -12,
                Ears: 19 * (1 - down), Jaw: 12 + 9 * down, Snarl: 1, Tail: -30 - 40 * down, Wag: -side * 32 * air)
        };
        if (stage is WolfCounterStage.SideHop or WolfCounterStage.Slide) {
            float facing = ActorAnimation.Yaw(player - wolf.Pos, model.Yaw);
            float look = Math.Clamp(Mathf.RadToDeg(Mathf.AngleDifference(model.Yaw, facing)), -78, 78);
            body = body with { NeckTurn = .50f * look, HeadTurn = .32f * look };
        }
        var toward = Vector2.FromAngle(wolf.CounterBearing + side * WolfCounter.SideHopBearing);
        var sideways = model.Root.Basis.Inverse() * WorldView.To3(toward);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            float width = stage is WolfCounterStage.Slide or WolfCounterStage.Pivot ? 1.32f : 1.12f;
            var paw = new Vector3(WolfShape.Pivot(leg).X * width, WolfShape.Sole, WolfModel.Home(leg));
            if (stage == WolfCounterStage.SideHop) {
                // Both forepaws reach into the travel direction before they touch, with the nearer paw out first.
                float lead = leg == (side > 0 ? 1 : 0) ? .035f : 0;
                float reach = hind ? -.10f * air : .24f * Mathf.SmoothStep(.18f + lead, .94f, u) + lead;
                paw += sideways * reach + Vector3.Up * (hind ? .08f : .045f) * air;
            } else if (stage == WolfCounterStage.Slide) {
                paw.Z += hind ? -.08f * (1 - down) : .19f - .07f * down;
                paw.Y += hind ? .065f * (1 - down) : 0;
            } else if (stage == WolfCounterStage.Bound) {
                paw.Z += hind ? -.19f * air : .16f * air + .015f * down;
                paw.Y += hind ? .20f * down : .07f * air;
            } else if (stage == WolfCounterStage.Pivot) {
                paw.Z += hind ? -.06f * (1 - down) : .015f;
                paw.Y += hind ? .20f * (1 - Mathf.SmoothStep(0, .46f, u)) : 0;
            }
            body = body.WithLeg(leg, WolfModel.ReachTo(body, leg, paw, hind ? 2 : 8, 0));
        }
        var motion = stage switch {
            WolfCounterStage.Slide => WolfMotion.CounterSlide,
            WolfCounterStage.Pivot => WolfMotion.CounterPivot,
            _ => WolfMotion.CounterAir
        };
        model.Pose(motion, body, dt);
    }
}
