using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Developer review fixture for <c>--wolves</c>: wolves by a cabin at dusk, moved by a script and run by the game's own
/// rules, for a look at how they turn on a patrol, turn to a player they smell and howl, and feed on one they brought down.
/// </summary>
public sealed partial class WolfReview : Node {
    /// <summary>The scenes <c>--wolves</c> takes.</summary>
    public static readonly string[] Scenes = { "patrol", "howl", "feast" };
    public World World = null!;
    public WorldView View = null!;
    public Thing Cabin = null!;
    public string Scene = "";
    private (float At, Action Do)[] script = Array.Empty<(float, Action)>();
    private int next;
    private float time;

    public override void _Ready() {
        if (!Scenes.Contains(Scene)) throw new ArgumentException($"--wolves takes {string.Join(", ", Scenes)}, not {Scene}");
        DoorwayReview.Stage(World, View, Cabin, true);
        var door = Doorways.DoorPoint(Cabin);
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
            _ => new (float, Action)[] {
                (0, () => { World.Player = door + new Vector2(0, 5); World.S.Health = 40; }),
                (.2f, () => {
                    foreach (var from in new[] { new Vector2(2.5f, .5f), new Vector2(-3, 1.5f), new Vector2(1, 4), new Vector2(-1.5f, -3.5f) })
                        World.S.Wolves.Add(new Wolf { Pos = World.Player + from, Target = World.Me.Id });
                }),
            },
        };
    }

    /// <summary>Puts the player inside the cabin behind a locked door, where no wolf smells them.</summary>
    private void StayIn() { World.Player = Doorways.DoorPoint(Cabin); World.S.Sheltered = Cabin.Id; Cabin.Locked = true; }

    /// <summary>Sets a wolf patrolling from a spot on a heading in degrees, east at 0 and south at 90, leaving the cabin alone.</summary>
    private void Patrol(Vector2 from, float degrees) => World.S.Wolves.Add(new Wolf { Pos = from, Heading = Mathf.DegToRad(degrees), Tried = Cabin.Id });

    /// <summary>Turns every patrolling wolf onto a heading in degrees.</summary>
    private void Head(float degrees) { foreach (var wolf in World.S.Wolves) wolf.Heading = Mathf.DegToRad(degrees); }

    /// <summary>Runs the script up to this frame and keeps the camera on the wolves, or on the player they feed on.</summary>
    public override void _Process(double delta) {
        time += Math.Min((float)delta, .1f);
        while (next < script.Length && script[next].At <= time) script[next++].Do();
        if (Scene == "feast") { if (World.S.Mauled) World.S.Health = Math.Max(World.S.Health, DoorwayReview.KeptAlive); return; }
        var middle = Vector2.Zero;
        foreach (var wolf in World.S.Wolves) middle += wolf.Pos / World.S.Wolves.Count;
        if (World.S.Wolves.Count > 0) View.CameraOn = middle;
    }
}
