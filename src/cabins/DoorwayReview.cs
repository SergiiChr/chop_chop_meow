using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Developer review fixture for <c>--doorway</c>: two players and, at dusk, wolves at a cabin's door, moved by a script
/// and run by the game's own rules, for a look at how they queue, knock on the door and get tugged out, and at wolves
/// scratching at the door of a cabin with someone inside and forcing it.
/// </summary>
public sealed partial class DoorwayReview : Node {
    /// <summary>The scenes <c>--doorway</c> takes.</summary>
    public static readonly string[] Scenes = { "queue", "occupied", "wolf", "wounded", "locked", "forced", "pair", "pack", "sides", "takedown" };
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
        Stage(World, View, Cabin, Scene is not ("queue" or "occupied"));
        host = World.Me; guest = World.Arrive("neighbor", 2);
        var door = Doorways.DoorPoint(Cabin);
        World.Player = door + new Vector2(-2.5f, 1.5f);
        // The neighbor comes from behind the cabin, so they walk round it to the door.
        World.As(guest, () => World.Player = Cabin.Pos + new Vector2(1, -3.5f));
        void HostInside() { World.Player = door; World.S.Sheltered = Cabin.Id; }
        void GuestAway() => World.As(guest, () => World.Player = door + new Vector2(60, 0));
        // One close behind the host and one out to each side of them.
        void ThreeWolves() { foreach (var from in new[] { new Vector2(-.5f, 1.6f), new Vector2(-3, 2), new Vector2(3, 2.5f) }) World.S.Wolves.Add(new Wolf { Pos = door + from, Target = host.Id }); }
        script = Scene switch {
            // Both walk up; the host gets there first and steps in, the neighbor follows, finds the door shut and knocks on it.
            "queue" => new (float, Action)[] { (0, () => walking.Add(host)), (.4f, () => walking.Add(guest)) },
            // The host is inside; the neighbor walks up and knocks, and while they knock the host steps out, the neighbor standing aside for them, and the neighbor goes in.
            "occupied" => new (float, Action)[] {
                (0, HostInside), (0, () => walking.Add(guest)),
            },
            // The host steps in at a walk with a wolf close by and the neighbor at the door behind them; a second wolf comes for the neighbor while they wait.
            "wolf" => new (float, Action)[] {
                (0, () => { World.Player = door; World.As(guest, () => World.Player = door + new Vector2(.4f, 0)); }),
                (0, () => World.Doorways.Enter(Cabin, false)), (.1f, () => World.As(guest, () => World.Doorways.Enter(Cabin, false))),
                (.15f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-1.6f, 1.2f), Target = host.Id })),
                (.9f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(2.2f, 1.4f), Target = guest.Id })),
            },
            // The host is inside behind a locked door; a wolf comes, scratches at it and goes away.
            "locked" => new (float, Action)[] {
                (0, () => { HostInside(); Cabin.Locked = true; GuestAway(); }),
                (.2f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-1.5f, 4) })),
            },
            // The host is inside with the door unlocked; a wolf scratches, forces the door and takes them out and lets them go outside.
            "forced" => new (float, Action)[] {
                (0, () => { HostInside(); GuestAway(); }),
                (.2f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-1.5f, 4) })),
            },
            // Both are inside, the neighbor last; the wolf takes the neighbor out while the host watches from inside.
            "pair" => new (float, Action)[] {
                (0, () => { World.Player = door; World.S.Sheltered = Cabin.Id; World.S.Actor.DoorQueued = ++World.S.DoorArrivals; }),
                (0, () => World.As(guest, () => { World.Player = door; World.S.Sheltered = Cabin.Id; World.S.Actor.DoorQueued = ++World.S.DoorArrivals; })),
                (.2f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-1.5f, 4) })),
            },
            // The host is inside with the door unlocked; three wolves come, one forces the door while the others wait behind it.
            "pack" => new (float, Action)[] {
                (0, () => { HostInside(); GuestAway(); }),
                (.2f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-1.5f, 4) })),
                (.6f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(3, 6) })),
                (1, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-4, 7) })),
            },
            // The host steps in at a walk with three wolves after them: one tugs them out and one more takes each of their arms.
            "sides" => new (float, Action)[] {
                (0, () => { World.Player = door; GuestAway(); }),
                (0, () => World.Doorways.Enter(Cabin, false)),
                (.15f, ThreeWolves),
            },
            // The same with the host hurt enough to go down once all three have bitten: the wolf that tugged pins them and the others feed.
            "takedown" => new (float, Action)[] {
                (0, () => { World.Player = door; World.S.Health = 80; GuestAway(); }),
                (0, () => World.Doorways.Enter(Cabin, false)),
                (.15f, ThreeWolves),
            },
            // A badly hurt host is tugged out of the doorway and pinned.
            _ => new (float, Action)[] {
                (0, () => { World.Player = door; World.S.Health = 45; World.As(guest, () => World.Player = door + new Vector2(2.5f, 2)); }),
                (0, () => World.Doorways.Enter(Cabin, false)),
                (.1f, () => World.S.Wolves.Add(new Wolf { Pos = door + new Vector2(-2.5f, 1.5f), Target = host.Id })),
            },
        };
    }

    /// <summary>
    /// Clears the ground round a cabin for a review scene, by day or at dusk with no wolves but the scene's own, and sets
    /// the camera as <c>--zoom</c>, <c>--camera-turn</c> and <c>--camera-pitch</c> ask.
    /// </summary>
    internal static void Stage(World world, WorldView view, Thing cabin, bool night) {
        // Wolves come out at dusk, while there is still light to see them by.
        world.S.Time = world.Data.DaySeconds * (night ? world.Data.DuskStarts + .02f : .3f);
        // Only the scene's own wolf comes: every den stays shut tonight.
        world.S.Wolves.Clear(); world.S.DensOut = (1 << WolfPatrols.Dens.Length) - 1; world.S.DenHours = 24;
        // No crew walks through the scene.
        foreach (var w in world.Workers) world.Assign(w, null);
        world.S.Workers.Clear();
        // Nothing else stands near the cabin, so a camera turned round or brought low sees the players and wolves clearly.
        world.S.Things.RemoveAll(t => t != cabin && t.Pos.DistanceTo(cabin.Pos) < Clearing);
        // The fixture's hand crafting would crouch the host the moment they are outside.
        world.S.Crafting.Clear(); world.S.Making = "";
        // A burning fire keeps wolves off everyone near it, the cabin's door included.
        if (night) foreach (var fire in world.Things.Where(t => t.Kind == ThingKind.Campfire)) fire.Burn = 0;
        view.Zoom = Launch.Number("--zoom", 2.4f);
        view.CameraTurn = Launch.Number("--camera-turn", 0);
        if (Launch.Value("--camera-pitch") != null) view.CameraPitch = Launch.Number("--camera-pitch", 30);
    }

    /// <summary>The least health a review scene keeps a pinned player at, so the wolves on them can be looked at for longer than the game gives them.</summary>
    internal const float KeptAlive = 30;

    /// <summary>Runs the script up to this frame, then walks each player sent to the door a step toward it and steps them in on arrival, as a click on the cabin does.</summary>
    public override void _Process(double delta) {
        float dt = Math.Min((float)delta, .1f);
        time += dt;
        while (next < script.Length && script[next].At <= time) script[next++].Do();
        if (Scene == "takedown" && World.S.Mauled) World.S.Health = Math.Max(World.S.Health, KeptAlive);
        if (Scene == "occupied" && !hostLeft && guest.Door == Doorway.Knocking && guest.DoorTime >= Doorways.KnockLead + LeaveAfterKnocks) { World.Doorways.Leave(); hostLeft = true; }
        foreach (var p in walking.ToArray())
            World.As(p, () => {
                var toward = Doorways.TowardDoor(Cabin, World.Player);
                if (toward != Vector2.Zero) World.MovePlayer(World.Doorways.StepToDoor(Cabin, toward * dt * World.Data.MoveSpeed(false)));
                else if (World.Doorways.Enter(Cabin, false)) walking.Remove(p);
            });
    }
}
