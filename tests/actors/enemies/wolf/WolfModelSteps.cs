using System;
using ChopChopMeow;
using Godot;

/// <summary>
/// Watches a wolf model's paws frame by frame: how far any part of a sole goes into the ground under it, how far a part of a
/// sole on the ground slides, and how far a joint bends past a dog's range.
/// </summary>
internal sealed class PawWatch(Func<Vector2, float>? groundAt = null) {
    /// <summary>The deepest any part of a sole has gone into the ground, as a negative height, and where and when.</summary>
    public float Deepest, DeepestAt;
    public int DeepestLeg;
    /// <summary>The furthest a part of a sole has slid while on the ground, and where and when.</summary>
    public float Slide, SlideAt;
    public int SlideLeg;
    /// <summary>The furthest a joint has bent past its range, in degrees, and which.</summary>
    public float PastRange;
    public string PastJoint = "";
    /// <summary>Where each point of each sole touched the ground, while it stays there.</summary>
    private readonly Vector2?[,] touched = new Vector2?[4, WolfModel.SolePoints];

    /// <summary>How near the ground, in metres, a point of a sole counts as on it.</summary>
    private const float OnTheGround = .004f;

    /// <summary>Looks at the model as placed and posed this frame, <paramref name="time"/> seconds in.</summary>
    public void See(WolfModel wolf, float time) {
        var root = wolf.Root.Transform;
        for (int leg = 0; leg < 4; leg++)
            for (int point = 0; point < WolfModel.SolePoints; point++) {
                var at = root * wolf.SolePoint(leg, point);
                var spot = new Vector2(at.X, at.Z);
                float over = at.Y - (groundAt?.Invoke(spot) ?? 0);
                if (over < Deepest) (Deepest, DeepestAt, DeepestLeg) = (over, time, leg);
                if (over > OnTheGround) { touched[leg, point] = null; continue; }
                touched[leg, point] ??= spot;
                float slid = spot.DistanceTo(touched[leg, point]!.Value);
                if (slid > Slide) (Slide, SlideAt, SlideLeg) = (slid, time, leg);
            }
        // A dog's elbow stops 15 degrees short of straight and its stifle 18, its wrist opens 16 past straight and its hock stops 16 short.
        for (int leg = 0; leg < 4; leg++) {
            var l = wolf.Shown.Leg(leg);
            bool hind = leg >= 2;
            Past(hind ? "stifle" : "elbow", hind ? l.Middle + 18 : 15 - l.Middle);
            Past(hind ? "hock" : "wrist", hind ? 16 - l.Lower : l.Lower - 16);
        }
    }

    private void Past(string joint, float by) {
        if (by > PastRange) (PastRange, PastJoint) = (by, joint);
    }
}

/// <summary>Steps that move and place a wolf model as the world view does.</summary>
internal static class WolfModelSteps {
    /// <summary>Poses a wolf taking a player out of a cabin and returns the four paw positions in the wolf's frame.</summary>
    public static Vector3[] PawsDuringTugFromCabin(GameData data, float rotation) {
        var world = WorldSteps.EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, new Vector2(24, 24), rotation: rotation);
        var door = Doorways.DoorPoint(cabin);
        world.Player = door;
        var held = new ActorState { Position = door + new Vector2(0, -1.35f).Rotated(rotation), Yaw = Mathf.Pi - rotation, HipHeight = .9f, Lift = .42f };
        Doorways.Stage(world.Me, Doorway.Tugged, cabin.Id);
        world.Me.DoorTime = .3f;
        var wolf = new Wolf { Target = world.Me.Id, DoorCheckCabin = cabin.Id, DoorCheck = DoorCheckStage.TakingOut, Pos = door };
        var view = new TugView(() => world, _ => held);
        var model = new WolfModel { Yaw = held.Yaw };
        model.Root.Position = WorldView.To3(door + new Vector2(0, -.45f).Rotated(rotation));
        for (int frame = 0; frame < 12; frame++) view.ShowTugging(wolf, model, .3f, 1f / 60);
        Vector3[] paws = [model.Paw(0), model.Paw(1), model.Paw(2), model.Paw(3)];
        model.Root.Free();
        return paws;
    }

    /// <summary>Moves a wolf model on along +Z for one frame at <paramref name="speed"/>, places it there and sets its paws down; returns how far it has come.</summary>
    public static float RunOn(WolfModel wolf, float travelled, float speed, bool chasing, float dt) {
        if (speed < .05f) wolf.Stand(0, dt, false); else wolf.Move(speed * dt, dt, chasing, false);
        travelled += speed * dt;
        wolf.Root.Position = new Vector3(0, 0, travelled);
        wolf.Plant();
        return travelled;
    }

    /// <summary>Whether every point of every sole of a wolf model is off the ground.</summary>
    public static bool InTheAir(WolfModel wolf) {
        var root = wolf.Root.Transform;
        for (int leg = 0; leg < 4; leg++)
            for (int point = 0; point < WolfModel.SolePoints; point++) if ((root * wolf.SolePoint(leg, point)).Y < .004f) return false;
        return true;
    }
}
