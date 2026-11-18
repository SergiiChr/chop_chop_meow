using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Developer review fixture for <c>--wolf-scene</c>: wolves by a cabin at dusk, moved by a script and run by the game's own
/// rules, for a look at how they turn on a patrol, turn to a player they smell and howl, feed on one they brought down,
/// follow one who waved a torch at them, and fight one with an axe.
/// </summary>
public sealed partial class WolfReview : Node {
    /// <summary>The scenes <c>--wolf-scene</c> takes.</summary>
    public static readonly string[] Scenes = { "patrol", "howl", "feast", "follow", "rout", "stand", "fight" };
    public World World = null!;
    public WorldView View = null!;
    public Thing Cabin = null!;
    public string Scene = "";
    private (float At, Action Do)[] script = Array.Empty<(float, Action)>();
    private int next;
    private float time;
    /// <summary>The wolf the player walks at, and how near to it they stop; no wolf while they stand.</summary>
    private Wolf? walkAt;
    private float walkUntil;

    public override void _Ready() {
        if (!Scenes.Contains(Scene)) throw new ArgumentException($"--wolf-scene takes {string.Join(", ", Scenes)}, not {Scene}");
        DoorwayReview.Stage(World, View, Cabin, true);
        var door = Doorways.DoorPoint(Cabin);
        var open = door + new Vector2(0, 5.5f);
        script = Scene switch {
            // A wolf patrols past the cabin, whose player is behind a locked door: it bears off a little, comes about, doubles back and bears off again.
            "patrol" => new (float, Action)[] {
                (0, () => { StayIn(); Patrol(door + new Vector2(-5, 6), 0); }),
                (2, () => Head(50)), (4, () => Head(160)), (6.5f, () => Head(-20)), (9, () => Head(-80)),
            },
            // Two wolves patrol out of sight of the cabin, one walking away from it and one across; the player steps out, and each turns to them and howls.
            "howl" => new (float, Action)[] {
                (0, () => { StayIn(); Patrol(door + new Vector2(-3, 15.5f), 90); Patrol(door + new Vector2(-1, 18.5f), 0); }),
                (2, () => { World.S.Sheltered = 0; Cabin.Locked = false; World.Player = door + new Vector2(0, 3); }),
            },
            // A badly hurt player stands out in the open with four wolves round them: one leaps and pins them, and the others feed.
            "feast" => new (float, Action)[] {
                (0, () => { World.Player = door + new Vector2(0, 5); World.S.Health = 40; }),
                (.2f, () => Hunters(new Vector2(2.5f, .5f), new Vector2(-3, 1.5f), new Vector2(1, 4), new Vector2(-1.5f, -3.5f))),
            },
            // Three wolves come at a player with a lit torch from one side; a wave of it puts them in follow mode, and they spread round the player and keep circling.
            "follow" => new (float, Action)[] {
                (0, () => { Torch(open); Hunters(new Vector2(7, 1), new Vector2(7.5f, -1), new Vector2(8, 2.5f)); }),
                (.8f, () => World.UseTorch(Vector2.Right)),
            },
            // One wolf follows a player with a torch; they walk at it, it stands braced, and at 2 m it runs out of their sight.
            "rout" => new (float, Action)[] {
                (0, () => { Torch(open); Hunters(new Vector2(6, 0)); }),
                (.8f, () => World.UseTorch(Vector2.Right)),
                (5, () => WalkAt(0, 1.8f)),
            },
            // Two wolves follow a player with a torch; they walk up to one, which stands braced and growls, then on at it, and it turns on them.
            "stand" => new (float, Action)[] {
                (0, () => { Torch(open); Hunters(new Vector2(6, .5f), new Vector2(6.5f, -1)); }),
                (.8f, () => World.UseTorch(Vector2.Right)),
                (6, () => WalkAt(0, 2.6f)), (11, () => WalkAt(0, 1.8f)),
            },
            // A player with a steel axe and no torch meets two wolves: each gathers itself and snaps, and the player swings at
            // them, the overhead chop and the sideways sweep in turn.
            _ => new (float, Action)[] {
                (0, () => { World.Player = open; World.S.Backpack.Add("steel_axe", 1); Hunters(new Vector2(5, .5f), new Vector2(5.5f, -1.5f)); }),
                (.75f, Swing), (1.45f, Swing), (2.15f, Swing), (2.85f, Swing), (3.55f, Swing), (4.25f, Swing), (4.95f, Swing), (5.65f, Swing), (6.35f, Swing), (7.05f, Swing),
            },
        };
    }

    /// <summary>Puts the player inside the cabin behind a locked door, where no wolf smells them.</summary>
    private void StayIn() { World.Player = Doorways.DoorPoint(Cabin); World.S.Sheltered = Cabin.Id; Cabin.Locked = true; }

    /// <summary>Sets a wolf patrolling from a spot on a heading in degrees, east at 0 and south at 90, leaving the cabin alone.</summary>
    private void Patrol(Vector2 from, float degrees) => World.S.Wolves.Add(new Wolf { Pos = from, Heading = Mathf.DegToRad(degrees), Tried = { Cabin.Id } });

    /// <summary>Turns every patrolling wolf onto a heading in degrees.</summary>
    private void Head(float degrees) { foreach (var wolf in World.S.Wolves) wolf.Heading = Mathf.DegToRad(degrees); }

    /// <summary>Puts wolves that hunt the player at spots from where the player stands.</summary>
    private void Hunters(params Vector2[] from) { foreach (var spot in from) World.S.Wolves.Add(new Wolf { Pos = World.Player + spot, Target = World.Me.Id }); }

    /// <summary>Stands the player at a spot with a lit torch in hand.</summary>
    private void Torch(Vector2 at) { World.Player = at; World.S.Backpack.Add("torch", 1); World.UseTorch(Vector2.Zero); World.Tick(World.KindleSeconds); }

    /// <summary>Sends the player walking at one of the wolves until they are a distance from it.</summary>
    private void WalkAt(int wolf, float until) { if (wolf < World.S.Wolves.Count) { walkAt = World.S.Wolves[wolf]; walkUntil = until; } }

    /// <summary>The player swings the axe at the nearest wolf.</summary>
    private void Swing() {
        if (World.S.Wolves.Count > 0) World.Attack(World.S.Wolves.MinBy(wolf => wolf.Pos.DistanceTo(World.Player))!.Pos - World.Player);
    }

    /// <summary>Runs the script up to this frame, walks the player at the wolf they were sent at, and keeps the camera on the wolves, or on the player they come for.</summary>
    public override void _Process(double delta) {
        float dt = Math.Min((float)delta, .1f);
        time += dt;
        while (next < script.Length && script[next].At <= time) script[next++].Do();
        if (Scene == "feast" && !World.S.Mauled) return;
        if (Scene is "patrol" or "howl") {
            var middle = Vector2.Zero;
            foreach (var wolf in World.S.Wolves) middle += wolf.Pos / World.S.Wolves.Count;
            if (World.S.Wolves.Count > 0) View.CameraOn = middle;
            return;
        }
        // A scene keeps its player alive, so the wolves on them can be looked at for longer than the game gives them;
        // all but the feast keep them above half health too, where no wolf leaps for the throat.
        World.S.Health = Math.Max(World.S.Health, Scene == "feast" ? DoorwayReview.KeptAlive : World.TakedownHealth + 10);
        // The player stops once the wolf they walk at has run or turned on them.
        if (walkAt == null || walkAt.Routed || walkAt.Bold || !World.S.Wolves.Contains(walkAt)) return;
        var to = walkAt.Pos - World.Player;
        if (to.Length() > walkUntil) World.MovePlayer(to.Normalized() * Math.Min(to.Length() - walkUntil, dt * World.Data.MoveSpeed(false)));
    }
}
