using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Draws a wolf at the door of a cabin with someone inside: scratching at it, stepping over to its free edge and working
/// its snout in, pushing in round the door, standing in the doorway and launching itself in.
/// Everything follows from the stage of the visit and the seconds into it, so every view of one visit shows the same
/// thing, and the door stands as far open as the wolf's body has pushed it.
/// Spots are measured from the cabin's door point: X across the door toward its free edge, Y out from it.
/// </summary>
public static class VisitView {
    /// <summary>Where a wolf stands and how far it has turned to its left of facing straight into the door, in degrees.</summary>
    private readonly record struct Stand(Vector2 At, float Turn) {
        /// <summary>The way the wolf faces, and the way to its left.</summary>
        public Vector2 Ahead => new(-MathF.Sin(Mathf.DegToRad(Turn)), -MathF.Cos(Mathf.DegToRad(Turn)));
        public Vector2 Left => new(-MathF.Cos(Mathf.DegToRad(Turn)), MathF.Sin(Mathf.DegToRad(Turn)));
        /// <summary>A spot in the wolf's model, X to its left and Y ahead, measured from the door point.</summary>
        public Vector2 Out(Vector2 model) => At + Left * model.X + Ahead * model.Y;
        /// <summary>A spot measured from the door point, in the wolf's model.</summary>
        public Vector2 In(Vector2 spot) => new((spot - At).Dot(Left), (spot - At).Dot(Ahead));
        public static Stand Lerp(Stand a, Stand b, float t) => new(a.At.Lerp(b.At, t), Mathf.Lerp(a.Turn, b.Turn, t));
    }

    /// <summary>
    /// The wolf's stands: in front of the door to scratch at it, at the door's free edge to work its snout in, in the
    /// doorway with its forepaws on the step, and inside, where its launch ends.
    /// </summary>
    private static readonly Stand Front = new(new(0, -.01f), 0), Edge = new(new(.58f, -.02f), 16), Doorway = new(new(0, -.4f), 0), Inside = new(new(0, -.95f), 0);

    /// <summary>Seconds into a visit, counted from the first scratch, at which each stage starts.</summary>
    private const float PryAt = DoorVisits.ScratchSeconds, OpenAt = PryAt + DoorVisits.PrySeconds, StareAt = OpenAt + DoorVisits.OpenSeconds,
        LaunchAt = StareAt + DoorVisits.StareSeconds, End = LaunchAt + DoorVisits.LaunchSeconds;
    /// <summary>Seconds into a visit at which the scratches of the recording start, and how long a scratch lasts.</summary>
    private static ReadOnlySpan<float> Strokes => new[] { .28f, .8f, 1.34f, 1.85f };
    private const float StrokeSeconds = .22f;
    /// <summary>Seconds into a visit between which the wolf sets its scratching paw down, steps over to the door's edge, and steps round the door into the doorway.</summary>
    private const float PawDownBy = 2.45f, ToEdgeFrom = 2.5f, ToEdgeBy = 3.35f, ToDoorwayFrom = OpenAt + .15f, ToDoorwayBy = StareAt - .2f;
    /// <summary>How far the paws stand apart, as a share of the legs' own width.</summary>
    private const float Spread = 1.1f;
    /// <summary>Where the door's hinge is, and how long the door is from it to its free edge.</summary>
    private static readonly Vector2 Hinge = new(-.41f, -StairSteps.DoorDepth);
    private const float Leaf = .82f;
    /// <summary>How many degrees clear of the wolf's body the door stands, for its thickness and the fur.</summary>
    private const float Clear = 1.5f;
    /// <summary>Seconds a wolf takes to settle into its stand at the door from where it arrived.</summary>
    private const float SettleSeconds = .3f;

    /// <summary>Seconds into a visit, counted from the first scratch.</summary>
    public static float Clock(WolfVisit stage, float time) => stage switch {
        WolfVisit.Scratching => Math.Min(time, PryAt),
        WolfVisit.Prying => PryAt + Math.Min(time, DoorVisits.PrySeconds),
        WolfVisit.Opening => OpenAt + Math.Min(time, DoorVisits.OpenSeconds),
        WolfVisit.Staring => StareAt + Math.Min(time, DoorVisits.StareSeconds),
        WolfVisit.Launching => LaunchAt + Math.Min(time, DoorVisits.LaunchSeconds),
        WolfVisit.TakingOut => End,
        _ => 0
    };

    /// <summary>The stands a wolf is between this far into a visit, and how far from the first to the second, 0 to 1.</summary>
    private static (Stand From, Stand To, float Along) Between(float clock) =>
        clock < ToEdgeFrom ? (Front, Front, 0)
        : clock < ToDoorwayFrom ? (Front, Edge, Math.Clamp((clock - ToEdgeFrom) / (ToEdgeBy - ToEdgeFrom), 0, 1))
        : clock < LaunchAt ? (Edge, Doorway, Math.Clamp((clock - ToDoorwayFrom) / (ToDoorwayBy - ToDoorwayFrom), 0, 1))
        : (Doorway, Inside, Math.Clamp((clock - LaunchAt) / DoorVisits.LaunchSeconds, 0, 1));

    /// <summary>
    /// The stands a wolf's paws step between this far into a visit, and how far through that step, 0 to 1: the way from one
    /// stand to the next takes <see cref="Strides"/> steps of each paw, timed by how far the body has come, so no paw is left
    /// further behind than its leg reaches.
    /// </summary>
    private static (Stand From, Stand To, float Along) Stride(float clock) {
        var (from, to, along) = Between(clock);
        if (clock >= LaunchAt) return (from, to, along);
        float come = WorldView.Ease(along) * Strides, stride = MathF.Min(MathF.Floor(come), Strides - 1);
        return (Stand.Lerp(from, to, stride / Strides), Stand.Lerp(from, to, (stride + 1) / Strides), come - stride);
    }
    private const int Strides = 3;

    /// <summary>Where a wolf stands this far into a visit.</summary>
    private static Stand StandAt(float clock) {
        var (from, to, along) = Between(clock);
        // Stepping between stands, the body eases off one and onto the other; launching, it flies at an even pace.
        return Stand.Lerp(from, to, clock < LaunchAt ? WorldView.Ease(along) : along);
    }

    /// <summary>Where a paw stands at a stand, measured from the door point.</summary>
    private static Vector2 Planted(Stand stand, int leg) => stand.Out(new Vector2(WolfShape.Pivot(leg).X * Spread, WolfModel.Home(leg)));

    /// <summary>
    /// The share of a step from one stand to another over which each paw swings to its new place: the paws on the side the
    /// wolf steps toward go first, fore then hind, and the other side's follow, so no leg is left crossed under the body.
    /// </summary>
    private static (float From, float To) Swing(int leg, Stand from, Stand to) {
        bool leftward = (to.At - from.At).Dot(from.Left) > 0;
        // Legs run front left, front right, hind left, hind right.
        bool leading = leg % 2 == (leftward ? 0 : 1);
        return (leading, leg < 2) switch { (true, true) => (0, .3f), (true, false) => (.2f, .5f), (false, true) => (.45f, .75f), _ => (.65f, .95f) };
    }

    /// <summary>
    /// A wolf's pose this far into a visit, <paramref name="time"/> being a clock that runs on for its breath and its snarl:
    /// its paws stay where they stand while its body moves over them, and step one at a time from stand to stand.
    /// </summary>
    public static WolfPose PoseAt(float clock, float time) {
        if (clock >= LaunchAt) {
            // The spring starts from the crouch it stared in.
            float launched = (clock - LaunchAt) / DoorVisits.LaunchSeconds;
            return WolfPose.Lerp(PoseAt(LaunchAt - .001f, time), Launching(launched), Mathf.SmoothStep(0, .25f, launched));
        }
        var body = Body(clock, time);
        var (from, to, along) = Stride(clock);
        var stand = StandAt(clock);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            var (starts, lands) = Swing(leg, from, to);
            float swung = Math.Clamp((along - starts) / (lands - starts), 0, 1);
            var at = stand.In(Planted(from, leg).Lerp(Planted(to, leg), WorldView.Ease(swung)));
            float lift = (hind ? .06f : .08f) * MathF.Sin(MathF.PI * swung);
            body = body.WithLeg(leg, WolfModel.ReachTo(body, leg, new Vector3(at.X, WolfShape.Sole + lift, at.Y), (hind ? 2 : 10) - 30 * MathF.Sin(MathF.PI * swung), -20 * MathF.Sin(MathF.PI * swung)));
        }
        return clock < PawDownBy ? Scratching(body, clock) : body;
    }

    /// <summary>
    /// A wolf springing in through the doorway at whoever is inside, <paramref name="launched"/> of the way: it drives off its
    /// hind legs, low and level, its forepaws reaching for the floor inside and its jaws opening wide.
    /// </summary>
    private static WolfPose Launching(float launched) {
        float drive = Mathf.SmoothStep(0, .35f, launched), air = Mathf.SmoothStep(.2f, 1, launched), reach = Mathf.SmoothStep(.35f, 1, launched);
        // The cabin's floor is three treads up from the ground the wolf stood on.
        float floor = 3 * StairSteps.StepRise;
        var body = new WolfPose(Lift: Mathf.Lerp(-.14f, floor - .08f, air) + .07f * MathF.Sin(MathF.PI * air), Pitch: -10 * drive + 6 * reach, Flex: Mathf.Lerp(10, -12, air),
            Neck: Mathf.Lerp(26, 6, air), Head: Mathf.Lerp(-24, -2, reach), Jaw: 12 + 26 * reach, Ears: -22, Tail: Mathf.Lerp(-82, -16, air), Curl: -6);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            float x = WolfShape.Pivot(leg).X * Spread;
            // The hind paws push off and trail out behind; the forepaws tuck under the chest, then reach out and down onto the floor.
            var at = hind ? new Vector3(x, WolfShape.Sole + Mathf.Lerp(0, floor - .1f, air), Mathf.Lerp(WolfModel.Home(leg), -.72f, air))
                : new Vector3(x, Mathf.Lerp(WolfShape.Sole + 2 * StairSteps.StepRise, floor + .22f, drive) - .22f * reach + WolfShape.Sole * reach, Mathf.Lerp(WolfModel.Home(leg), .3f, drive) + .22f * reach);
            float lower = hind ? Mathf.Lerp(2, -60, air) : Mathf.Lerp(10, -80, drive * (1 - reach)) + 30 * reach;
            body = body.WithLeg(leg, WolfModel.Reach(body, leg, at, lower, hind ? lower - 25 * air : lower - 15));
        }
        return body;
    }

    /// <summary>
    /// How high each paw stands over the ground this far into a visit, on the cabin's step: a paw swinging from one stand to
    /// the next goes from the height it left to the one it lands on, clearing a tread's edge on the way.
    /// </summary>
    private static void GroundAt(float clock, Span<float> ground) {
        var (from, to, along) = Stride(clock);
        // Launching, the wolf's paws leave what they stood on in the doorway.
        float standing = 1 - Mathf.SmoothStep(0, .25f, Launched(clock));
        for (int leg = 0; leg < 4; leg++) {
            if (clock >= LaunchAt) { ground[leg] = standing * Under(Planted(Doorway, leg)); continue; }
            var (starts, lands) = Swing(leg, from, to);
            float swung = Math.Clamp((along - starts) / (lands - starts), 0, 1);
            float left = Under(Planted(from, leg)), landing = Under(Planted(to, leg));
            ground[leg] = Mathf.Lerp(left, landing, WorldView.Ease(swung)) + (left == landing ? 0 : OverEdge * MathF.Sin(MathF.PI * swung));
        }
        // The scratching paw is up at the door, where its own path keeps it clear of the step.
        if (clock < PawDownBy) ground[1] = 0;
    }
    /// <summary>How high what a paw at a spot stands on is: a paw at the edge of a tread stands on the tread, not half in it.</summary>
    private static float Under(Vector2 spot) {
        float height = StairSteps.HeightAt(spot);
        foreach (var way in PawEdges) height = Math.Max(height, StairSteps.HeightAt(spot + way * PawHalf));
        return height;
    }
    /// <summary>Half the width of a paw, and the ways to its edges.</summary>
    private const float PawHalf = .05f;
    private static readonly Vector2[] PawEdges = { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down };

    /// <summary>How much higher, in metres, a paw swings to clear a tread's edge on its way up or down a step.</summary>
    private const float OverEdge = .07f;

    /// <summary>A wolf's body, neck, head and tail this far into a visit, before its legs are set.</summary>
    private static WolfPose Body(float clock, float time) {
        float breath = MathF.Sin(time * 2.4f);
        // Scratching, it leans on its left foreleg and watches its paw.
        var scratching = new WolfPose(Lift: .01f + .004f * breath, Pitch: -3, Roll: -2, Shift: .02f, Neck: 16, NeckTurn: -8, Head: 2, Jaw: 3, Ears: 10, Tail: -58, Wag: 10 * MathF.Sin(time * 3.1f), Curl: 10);
        // At the door's edge it lowers its nose to the gap and sniffs.
        float sniff = MathF.Sin(time * 11);
        var sniffing = new WolfPose(Lift: -.03f, Pitch: 5, Surge: .03f + .008f * sniff, Neck: 34, NeckTurn: 4, Head: -6 + 2 * sniff, HeadTurn: 6, Jaw: 2, Ears: 14, Tail: -66, Wag: 5 * MathF.Sin(time * 2.2f), Curl: 8);
        // Prying, it drives its snout in behind the door's edge, twisting its head from side to side.
        float pry = Mathf.SmoothStep(0, 1, (clock - PryAt) / DoorVisits.PrySeconds), twist = MathF.Sin((clock - PryAt) * 8.5f);
        var prying = new WolfPose(Lift: -.05f, Pitch: 7, Surge: .05f + .1f * pry + .012f * twist, Neck: 36, NeckTurn: 2 + 3 * twist, Head: -4, HeadTurn: 4 + 5 * twist, HeadTilt: 14 * twist, Jaw: 4, Ears: -6, Tail: -72, Wag: 6 * twist, Curl: 4);
        // Pushing in round the door, it keeps its head low and its ears back.
        var pushing = new WolfPose(Lift: -.06f, Pitch: 5, Surge: .05f, Neck: 30, Head: -10, Jaw: 5, Ears: -10, Tail: -76, Curl: 2);
        // In the doorway it crouches, looks up from under its brow and snarls.
        float stare = Mathf.SmoothStep(0, 1, (clock - StareAt) / DoorVisits.StareSeconds), snarl = MathF.Sin(time * 23);
        var staring = new WolfPose(Lift: -.08f - .02f * stare + .003f * breath, Pitch: 4, Surge: .03f + .05f * stare, Neck: 30 + breath, Head: -28, HeadTilt: 2 * MathF.Sin(time * 1.3f), Jaw: 10 + 4 * stare + 1.5f * snarl,
            Ears: -14, Tail: -84, Wag: 2 * MathF.Sin(time * .9f));
        return clock < ToEdgeFrom ? scratching
            : clock < PryAt ? WolfPose.Lerp(scratching, sniffing, Mathf.SmoothStep(ToEdgeFrom, ToEdgeBy, clock))
            : clock < OpenAt ? WolfPose.Lerp(sniffing, prying, Mathf.SmoothStep(PryAt, PryAt + .3f, clock))
            : clock < StareAt ? WolfPose.Lerp(prying, pushing, Mathf.SmoothStep(OpenAt, OpenAt + .5f, clock))
            : WolfPose.Lerp(pushing, staring, Mathf.SmoothStep(StareAt - .2f, StareAt + .3f, clock));
    }

    /// <summary>
    /// A scratching wolf's right forepaw this far into a visit: up off the ground to the door, then down it once for each
    /// scratch of the recording, drawing back off the boards between them, and down to the ground again.
    /// </summary>
    private static WolfPose Scratching(WolfPose body, float clock) {
        // The paw's place in the model: beside the right shoulder, up the door and ahead to its boards.
        var ground = new Vector3(WolfShape.Pivot(1).X * Spread, WolfShape.Sole, WolfModel.Home(1));
        // The boards start above the deck the door stands on, and the paw comes back down over the step in an arc that clears it.
        Vector3 top = new(-.13f, .86f, .845f), bottom = new(-.13f, .52f, .855f);
        const float overStep = .26f;
        const float topAngle = 55, bottomAngle = 25, curl = 25;
        var strokes = Strokes;
        Vector3 at; float angle, toes;
        if (clock < strokes[0]) {
            // The paw stays down while the wolf settles into its stand, then comes up to the boards for the first scratch.
            float up = Mathf.SmoothStep(PawUpFrom, strokes[0], clock);
            (at, angle, toes) = (ground.Lerp(top, up) + Vector3.Up * .1f * MathF.Sin(MathF.PI * up), Mathf.Lerp(10, topAngle, up), curl * up);
        } else if (clock >= strokes[^1] + StrokeSeconds) {
            float down = Mathf.SmoothStep(0, 1, (clock - strokes[^1] - StrokeSeconds) / (PawDownBy - strokes[^1] - StrokeSeconds));
            (at, angle, toes) = (bottom.Lerp(ground, down) + Vector3.Up * overStep * MathF.Sin(MathF.PI * down), Mathf.Lerp(bottomAngle, 10, down), curl * (1 - down));
        } else {
            int stroke = 0;
            while (stroke < strokes.Length - 1 && clock >= strokes[stroke + 1]) stroke++;
            float since = clock - strokes[stroke];
            if (since < StrokeSeconds) {
                // Each scratch rakes down the boards, gathering speed.
                float raked = MathF.Pow(since / StrokeSeconds, 1.5f);
                (at, angle, toes) = (top.Lerp(bottom, raked), Mathf.Lerp(topAngle, bottomAngle, raked), curl);
            } else {
                // Between scratches the paw comes off the boards and back up.
                float back = (since - StrokeSeconds) / (strokes[stroke + 1] - strokes[stroke] - StrokeSeconds);
                (at, angle, toes) = (bottom.Lerp(top, Mathf.SmoothStep(0, 1, back)) - new Vector3(0, 0, .09f * MathF.Sin(MathF.PI * back)), Mathf.Lerp(bottomAngle, topAngle, back), curl);
            }
        }
        return body.WithLeg(1, WolfModel.Reach(body, 1, at, angle, angle + toes));
    }

    /// <summary>Seconds into a visit at which a scratching wolf starts lifting its paw.</summary>
    private const float PawUpFrom = .12f;

    /// <summary>Where a wolf's model stands this far into a visit to the cabin whose door point is <paramref name="door"/>, and the way it faces.</summary>
    public static (Vector3 At, float Yaw) Placement(Vector2 door, float clock) {
        var stand = StandAt(clock);
        return (WorldView.To3(door + stand.At), MathF.Atan2(stand.Ahead.X, stand.Ahead.Y));
    }

    /// <summary>Poses a wolf model this far into a visit, standing on the cabin's step.</summary>
    public static void Pose(WolfModel model, float clock, float time, float dt) {
        Span<float> ground = stackalloc float[4];
        GroundAt(clock, ground);
        model.Pose(WolfMotion.Door, PoseAt(clock, time), ground, dt);
    }

    /// <summary>Shows a wolf at a cabin's door as its visit has it, settling into its stand from where it arrived.</summary>
    public static void Show(Wolf wolf, WolfModel model, Thing cabin, float time, float dt, bool visible) {
        var door = Doorways.DoorPoint(cabin);
        float clock = Clock(wolf.Visit, wolf.VisitTime);
        if (model.Motion != WolfMotion.Door) { model.StepFrom = model.Root.Position; model.FromYaw = model.Yaw; model.Settled = 0; }
        model.Settled = Math.Min(1, model.Settled + dt / SettleSeconds);
        var (at, yaw) = Placement(door, clock);
        float settle = WorldView.Ease(model.Settled);
        model.Yaw = Mathf.LerpAngle(model.FromYaw, yaw, settle);
        model.KnockBack = Vector2.Zero; model.Stagger = wolf.Stagger; model.Toward = Vector2.Zero; model.Pinned = 0;
        model.Turning.Hold(model.Yaw); model.Root.Rotation = new Vector3(0, model.Yaw, 0);
        model.Root.Position = model.StepFrom.Lerp(at, settle);
        model.Root.Visible = visible;
        Pose(model, clock, time, dt);
        model.Drift = new Vector2(model.Root.Position.X, model.Root.Position.Z) - wolf.Pos;
    }

    /// <summary>How far through its launch into the cabin a wolf is this far into a visit, 0 to 1.</summary>
    public static float Launched(float clock) => Math.Clamp((clock - LaunchAt) / DoorVisits.LaunchSeconds, 0, 1);

    /// <summary>How many degrees open a wolf has pushed a cabin's door this far into its visit.</summary>
    public static float DoorDegrees(float clock) => clock <= PryAt ? 0 : pushed[Math.Min(pushed.Length - 1, (int)((clock - PryAt) * PushedRate))];

    /// <summary>The door's opening at each sixtieth of a second from the wolf's first push: it only ever opens further.</summary>
    private static readonly float[] pushed = Pushed();
    private const int PushedRate = 60;

    private static float[] Pushed() {
        var table = new float[(int)((End - PryAt) * PushedRate) + 1];
        Span<(Vector3 At, float Radius)> volumes = stackalloc (Vector3, float)[WolfModel.VolumeCount];
        float open = 0;
        for (int i = 0; i < table.Length; i++) {
            float clock = PryAt + (float)i / PushedRate;
            var stand = StandAt(clock);
            WolfModel.VolumesOf(PoseAt(clock, clock), volumes);
            foreach (var (centre, radius) in volumes) open = Math.Max(open, Cleared(stand.Out(new Vector2(centre.X, centre.Z)), radius, open));
            table[i] = open;
        }
        return table;
    }

    /// <summary>
    /// How many degrees open the door has to stand to clear a round part of the wolf at <paramref name="centre"/> that is
    /// behind the door, or touching it, as it stands <paramref name="open"/> degrees open; zero for a part out in front of the door.
    /// </summary>
    private static float Cleared(Vector2 centre, float radius, float open) {
        var from = centre - Hinge;
        float far = from.Length(), turn = Mathf.DegToRad(open);
        var along = new Vector2(MathF.Cos(turn), MathF.Sin(turn));
        // How far the part is out in front of the door, and how far along the door from its hinge.
        float ahead = along.X * from.Y - along.Y * from.X, reach = along.Dot(from);
        if (ahead >= radius || reach <= 0 || reach >= Leaf + radius || far <= radius) return 0;
        // Past the door's free edge only that edge has to clear the part; nearer the hinge the whole face does.
        float half = far > Leaf ? MathF.Acos(Math.Clamp((Leaf * Leaf + far * far - radius * radius) / (2 * Leaf * far), -1, 1)) : MathF.Asin(radius / far);
        return Mathf.RadToDeg(MathF.Atan2(from.Y, from.X) + half) + Clear;
    }
}
