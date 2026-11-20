using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>What a felled tree did over its fall, frame by frame: how low its wood went, how far its foot moved, how often it slowed, the angle it landed at, when, and how fast its top was going.</summary>
internal readonly record struct Felled(float Lowest, float HingeMoved, int Slowed, float Lands, float Seconds, float TopSpeed);
/// <summary>How fast a thrown body's middle fell mid-flight, and the lowest any part of it went, against where it started.</summary>
internal readonly record struct ThrownOut(float Gravity, float Lowest, float LowestAtStart);
/// <summary>A chip's flight: how fast it fell mid-flight, how low it went, the biggest change of its pace along the ground in a frame as a share of its speed, and whether it ended at rest.</summary>
internal readonly record struct ChipFlown(float Gravity, float Lowest, float BiggestChange, bool Stopped);

/// <summary>Steps that play world objects' motions frame by frame through their models and the view's motion rules.</summary>
internal static class PropMotionSteps {
    /// <summary>Fells a tree of the look and size <paramref name="id"/> gives toward <paramref name="heading"/>, a frame of <paramref name="dt"/> at a time.</summary>
    public static Felled FellTree(int id, float heading, float dt) {
        var tree = (TreeProp)WorldModels.Build(new Thing { Id = id, Kind = ThingKind.Tree });
        var (species, variant) = WorldModels.TreeLook(id);
        var (wood, _) = Tree3DShape.Outline(species, variant);
        float scale = WorldModels.TreeScale(id);
        var fall = WorldModels.Fall(tree, heading);
        float lowest = float.MaxValue, moved = 0, before = 0, step = 0, topSpeed = 0;
        int slowed = 0, frames = 0;
        while (!fall.Landed && frames < 10000) {
            topSpeed = fall.Speed * fall.Length;
            fall.Advance(dt); frames++;
            tree.Sway.Transform = fall.Turn * new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), Vector3.Zero);
            var drawn = tree.Body.Transform * tree.Sway.Transform;
            foreach (var corner in wood) lowest = Math.Min(lowest, (drawn * corner).Y);
            moved = Math.Max(moved, (drawn * (fall.Hinge / scale)).DistanceTo(tree.Body.Transform * fall.Hinge));
            if (!fall.Landed && fall.Angle - before < step) slowed++;
            (step, before) = (fall.Angle - before, fall.Angle);
        }
        tree.Root.Free();
        return new Felled(lowest, moved, slowed, fall.Lands, frames * dt, topSpeed);
    }

    /// <summary>Fells the first look of tree drawn <paramref name="scale"/> times its model's size the same way across its model, and gives the seconds it takes and its top's speed as it lands.</summary>
    public static (float Seconds, float TopSpeed) FellSized(float scale) {
        var (wood, top) = Tree3DShape.Outline(0, 0);
        var fall = new TreeFall(wood, top, Tree3DShape.Foot(0, 0), scale, Vector3.Right);
        const float dt = 1 / 2400f;
        float seconds = 0, speed = 0;
        for (; !fall.Landed; seconds += dt) { speed = fall.Speed * fall.Length; fall.Advance(dt); }
        return (seconds, speed);
    }

    /// <summary>Throws a stump or bush out of the ground from where it stands, a frame of <paramref name="dt"/> at a time.</summary>
    public static ThrownOut ThrowDugOut(ThingKind kind, float dt) {
        var dug = (DugProp)WorldModels.Build(new Thing { Id = 7, Kind = kind, OfTree = kind == ThingKind.Stump ? 7 : 0 });
        var points = BodyPoints(dug.Body);
        var launch = dug.Body.Transform;
        float start = points.Min(p => (launch * p).Y), lowest = start;
        var heights = new List<float>();
        for (float t = dt; t <= WorldView.DugOutSeconds; t += dt) {
            var drawn = Thrown.Tumble(launch, dug.Middle, Thrown.Rise(t, WorldView.DugOutSeconds), WorldView.DugTumble * t / WorldView.DugOutSeconds, 1 - Mathf.SmoothStep(.45f, 1, t / WorldView.DugOutSeconds));
            lowest = Math.Min(lowest, points.Min(p => (drawn * p).Y));
            heights.Add((drawn * dug.Middle).Y);
        }
        dug.Root.Free();
        int mid = heights.Count / 2;
        return new ThrownOut(-(heights[mid + 1] - 2 * heights[mid] + heights[mid - 1]) / (dt * dt), lowest, start);
    }

    /// <summary>Flies a chip off a rock from <paramref name="from"/> metres up, a frame of <paramref name="dt"/> at a time.</summary>
    public static ChipFlown FlyChip(float from, float dt) {
        var heights = new List<float>(); var ways = new List<float>();
        for (float t = 0; t <= WorldModels.ChipFlight + WorldModels.ChipSettle + dt; t += dt) {
            heights.Add(Thrown.ChipHeight(t, WorldModels.ChipFlight, from));
            ways.Add(Thrown.ChipWay(t, WorldModels.ChipFlight, WorldModels.ChipSettle));
        }
        int mid = (int)(WorldModels.ChipFlight / 2 / dt);
        float speed = (ways[2] - ways[1]) / dt, change = 0;
        for (int i = 2; i < ways.Count; i++) change = Math.Max(change, Math.Abs(ways[i] - 2 * ways[i - 1] + ways[i - 2]) / dt / speed);
        return new ChipFlown(-(heights[mid + 1] - 2 * heights[mid] + heights[mid - 1]) / (dt * dt), heights.Min(), change, ways[^1] == 1 && ways[^2] == 1);
    }

    /// <summary>How fast a cabin's door turns on each frame of a slam from wide open, in degrees a second.</summary>
    public static float[] SlamDoor(float dt) {
        var hut = CabinModel.Build();
        hut.Open = 1;
        var speeds = new List<float>();
        float last = -hut.Door.RotationDegrees.Y;
        for (int frame = 0; hut.Open > 0 && frame < 100; frame++) {
            DoorwayView.SwingDoor(hut, false, dt, DoorwayView.SlamSeconds, true);
            float now = -hut.Door.RotationDegrees.Y;
            speeds.Add((last - now) / dt);
            last = now;
        }
        hut.Root.Free();
        return speeds.ToArray();
    }

    /// <summary>
    /// Follows a count running at one a second that a server sends every <paramref name="update"/> seconds, drawn a frame
    /// of <paramref name="frame"/> at a time for <paramref name="seconds"/>: what the clock moved on each frame once
    /// settled, and how far ahead of the count it ends.
    /// </summary>
    public static (List<float> Steps, float Lead) FollowCount(float frame, float update, float seconds) {
        var clock = new LocalClock(1);
        var steps = new List<float>();
        float shown = 0, count = 0;
        for (float t = 0; t < seconds; t += frame) {
            if (t - count >= update - 1e-4f) count = MathF.Round(t / update) * update;
            float now = clock.Follow(count, count, frame);
            if (t > .5f) steps.Add(now - shown);
            shown = now;
        }
        return (steps, shown - count);
    }

    /// <summary>Every corner of the triangles of a model's body, in the body's own frame.</summary>
    private static List<Vector3> BodyPoints(Node3D body) {
        var points = new List<Vector3>();
        foreach (var mesh in ModelSteps.AllMeshes(body)) {
            var to = Transform3D.Identity;
            for (Node node = mesh; node != body; node = node.GetParent()) to = ((Node3D)node).Transform * to;
            foreach (var corner in mesh.Mesh.GetFaces()) points.Add(to * corner);
        }
        return points;
    }
}
