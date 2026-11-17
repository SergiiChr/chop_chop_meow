using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Developer review fixture for <c>--doorway</c>: two players and, at dusk, wolves at a cabin's door, moved by a script
/// and run by the game's own rules, for a look at how they queue, knock on the door and get tugged out.
/// </summary>
public sealed partial class DoorwayReview : Node {
    /// <summary>The scenes <c>--doorway</c> takes.</summary>
    public static readonly string[] Scenes = { "queue", "occupied", "wolf", "wounded" };
    public World World = null!;
    public WorldView View = null!;
    public Thing Cabin = null!;
    public string Scene = "";
    private PlayerState host = null!, guest = null!;
    private readonly HashSet<PlayerState> walking = new();
    private (float At, Action Do)[] script = Array.Empty<(float, Action)>();
    private int next;
    private float time;
    /// <summary>Metres round the cabin cleared of everything else.</summary>
    private const float Clearing = 15;
    /// <summary>Seconds into the neighbor's knocking at which the host steps out in the occupied scene, and whether they have.</summary>
    private const float LeaveAfterKnocks = .4f;
    private bool hostLeft;

    public override void _Ready() {
        if (!Scenes.Contains(Scene)) throw new ArgumentException($"--doorway takes {string.Join(", ", Scenes)}, not {Scene}");
        bool night = Scene is "wolf" or "wounded";
        // Wolves come out at dusk, while there is still light to see them by.
        World.S.Time = World.Data.DaySeconds * (night ? World.Data.DuskStarts + .02f : .3f);
        // Only the scene's own wolf comes: every den stays shut tonight.
        World.S.Wolves.Clear(); World.S.DensOut = (1 << World.Dens.Length) - 1; World.S.DenHours = 24;
        // No crew walks through the scene.
        foreach (var w in World.Workers) World.Assign(w, null);
        World.S.Workers.Clear();
        // Nothing else stands near the cabin, so a camera turned round or brought low sees the players and wolves clearly.
        World.S.Things.RemoveAll(t => t != Cabin && t.Pos.DistanceTo(Cabin.Pos) < Clearing);
        // The fixture's hand crafting would crouch the host the moment they are outside.
        World.S.Crafting.Clear(); World.S.Making = "";
        // A burning fire keeps wolves off everyone near it, the cabin's door included.
        if (night) foreach (var fire in World.Things.Where(t => t.Kind == ThingKind.Campfire)) fire.Burn = 0;
        host = World.Me; guest = World.Arrive("neighbor", 2);
        var door = World.DoorPoint(Cabin);
        World.Player = door + new Vector2(-2.5f, 1.5f);
        // The neighbor comes from behind the cabin, so they walk round it to the door.
        World.As(guest, () => World.Player = Cabin.Pos + new Vector2(1, -3.5f));
        View.Zoom = Launch.Number("--zoom", 2.4f);
        View.CameraTurn = Launch.Number("--camera-turn", 0);
        if (Launch.Value("--camera-pitch") != null) View.CameraPitch = Launch.Number("--camera-pitch", 30);
        script = Scene switch {
            // Both walk up; the host gets there first and steps in, the neighbor follows, finds the door shut and knocks on it.
            "queue" => new (float, Action)[] { (0, () => walking.Add(host)), (.4f, () => walking.Add(guest)) },
            // The host is inside; the neighbor walks up and knocks, and while they knock the host steps out, the neighbor standing aside for them, and the neighbor goes in.
            "occupied" => new (float, Action)[] {
                (0, () => { World.Player = door; World.S.Sheltered = Cabin.Id; }), (0, () => walking.Add(guest)),
            },
            // The host steps in at a walk with a wolf close by and the neighbor at the door behind them; a second wolf comes for the neighbor while they wait.
            "wolf" => new (float, Action)[] {
                (0, () => { World.Player = door; World.As(guest, () => World.Player = door + new Vector2(.4f, 0)); }),
                (0, () => World.Enter(Cabin, false)), (.1f, () => World.As(guest, () => World.Enter(Cabin, false))),
                (.15f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-1.6f, 1.2f), Target = host.Id })),
                (.9f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(2.2f, 1.4f), Target = guest.Id })),
            },
            // A badly hurt host is tugged out of the doorway and pinned.
            _ => new (float, Action)[] {
                (0, () => { World.Player = door; World.S.Health = 45; World.As(guest, () => World.Player = door + new Vector2(2.5f, 2)); }),
                (0, () => World.Enter(Cabin, false)),
                (.1f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-2.5f, 1.5f), Target = host.Id })),
            },
        };
    }


    /// <summary>Runs the script up to this frame, then walks each player sent to the door a step toward it and steps them in on arrival, as a click on the cabin does.</summary>
    public override void _Process(double delta) {
        float dt = Math.Min((float)delta, .1f);
        time += dt;
        while (next < script.Length && script[next].At <= time) script[next++].Do();
        if (Scene == "occupied" && !hostLeft && guest.Door == Doorway.Knocking && guest.DoorTime >= World.KnockLead + LeaveAfterKnocks) { World.Leave(); hostLeft = true; }
        foreach (var p in walking.ToArray())
            World.As(p, () => {
                var toward = World.TowardDoor(Cabin, World.Player);
                if (toward != Vector2.Zero) World.MovePlayer(World.StepToDoor(Cabin, toward * dt * World.Data.MoveSpeed(false)));
                else if (World.Enter(Cabin, false)) walking.Remove(p);
            });
    }
}
