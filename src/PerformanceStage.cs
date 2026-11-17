using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The server's half of a performance run: it keeps the player fed, holds the clock at the day sprint's start until the
/// player sets off, puts things in front of them for the first time as they reach the camp and turns the clock to
/// midnight once they reach the corner.
/// The player's own moves stay in the window, which sprints them along <see cref="Route"/> as a player would.
/// </summary>
internal partial class PerformanceStage(World world) : Node {
    /// <summary>Seconds standing on the beach before the day sprint, while the first pipelines compile.</summary>
    public const double WarmupSeconds = 15;
    /// <summary>Seconds between the day sprint and the night one, while the player is moved back and lights a torch.</summary>
    public const double SettleSeconds = 3;
    /// <summary>Clock at the start of the day and night sprints, as shares of a day starting at 06:00: 13:30 and midnight.</summary>
    private const float DayStart = 7.5f / 24, NightStart = 18f / 24;
    /// <summary>Where both sprints start: on the beach below the fixture's camp.</summary>
    public static readonly Vector2 Start = Terrain.Point(Terrain.Coast(Terrain.Across(Terrain.Spawn)) - 2, Terrain.Across(Terrain.Spawn));
    /// <summary>The pond's shore facing the camp, where the sprint touches the pond.</summary>
    private static readonly Vector2 Pond = Terrain.WaterPoint(Terrain.Spawn);
    /// <summary>Units from the pond's middle, along or across the valley, that the sprint keeps clear of it by.</summary>
    private const float PondClearance = Terrain.LakeRadius * 1.2f;
    /// <summary>Across the valley, the line clear of the pond on its left that the sprint follows up past it.</summary>
    private static readonly float BesidePond = Terrain.Across(Terrain.Lake) - PondClearance;
    /// <summary>Where the sprint stops in the camp: between the two furnaces seaward of the fixture's middle.</summary>
    private static readonly Vector2 Camp = Terrain.Spawn + Terrain.Point(2, .35f);
    /// <summary>The valley's top corner, where the sprint ends.</summary>
    private static readonly Vector2 Corner = Terrain.Point(22, 0);
    /// <summary>
    /// The sprint's legs, each through its stops: up into the camp, across to the pond's shore, then round the pond's
    /// left side, up past it and on to the valley's top corner.
    /// Out of the camp the sprint first steps three units across toward the pond, so its diagonal clears the bench.
    /// </summary>
    public static readonly (string Name, Vector2[] Stops)[] Route = {
        ("ocean-camp", [Camp]),
        ("camp-pond", [Camp + Terrain.Point(0, 3), Pond]),
        ("pond-corner", [Terrain.Point(Terrain.Along(Pond), BesidePond), Terrain.Point(Terrain.Along(Terrain.Lake) - PondClearance, BesidePond), Corner]),
    };
    /// <summary>Seconds after the player reaches the camp when something first appears in front of them, which should draw from models warmed while loading.</summary>
    public static readonly (double After, string Name)[] FirstTimes = { (0, "cart"), (.5, "buildings"), (1, "drops"), (1.5, "second cart") };
    /// <summary>Units along and across the valley within which a stop counts as reached.</summary>
    private const float AxisReach = .15f;
    /// <summary>Units within which the server counts the player as at a place; wider than a stop, as it sees them less often.</summary>
    private const float Arrived = 1;

    /// <summary>
    /// The movement keys a player holds toward <paramref name="to"/>, as 1, 0 or -1 each: S or W up or down the valley
    /// until level with it, and D or A across until in line, both at once on a diagonal.
    /// </summary>
    public static (int Along, int Across) Heading(Vector2 from, Vector2 to) {
        var offset = to - from;
        int along = Math.Abs(Terrain.Along(offset)) > AxisReach ? Math.Sign(Terrain.Along(offset)) : 0;
        int across = Math.Abs(Terrain.Across(offset)) > AxisReach ? Math.Sign(Terrain.Across(offset)) : 0;
        return (along, across);
    }

    /// <summary>Every point the sprint passes, one tenth of a unit apart, with the leg each belongs to.</summary>
    public static IEnumerable<(Vector2 At, int Leg)> Path() {
        var at = Start;
        for (int leg = 0; leg < Route.Length; leg++)
            foreach (var stop in Route[leg].Stops)
                for (var heading = Heading(at, stop); heading != (0, 0); heading = Heading(at, stop)) {
                    at += PlayerControl.KeysDirection(heading.Across, heading.Along) * .1f;
                    yield return (at, leg);
                }
    }

    private bool onBeach, setOff, night;
    private ulong campReached;
    private int firsts;

    public override void _Process(double delta) {
        if (world.Players.Count == 0) return;
        var at = world.Players[0].Pos;
        var s = world.S;
        foreach (var p in world.Players) p.Hunger = p.Thirst = p.Health = 100;
        float day = MathF.Floor(s.Time / world.Data.DaySeconds) * world.Data.DaySeconds;
        // The window moves the player to the beach for the warmup; the clock holds until they leave it.
        if (!setOff) {
            onBeach |= at.DistanceTo(Start) < Arrived;
            setOff = onBeach && at.DistanceTo(Start) >= Arrived;
            s.Time = day + world.Data.DaySeconds * DayStart;
            return;
        }
        if (campReached == 0 && at.DistanceTo(Camp) < Arrived) campReached = Time.GetTicksMsec();
        if (campReached != 0 && firsts < FirstTimes.Length && Time.GetTicksMsec() - campReached >= FirstTimes[firsts].After * 1000) Show(FirstTimes[firsts++].Name);
        if (!night && at.DistanceTo(Corner) < Arrived) { night = true; s.Time = day + world.Data.DaySeconds * NightStart; }
    }

    /// <summary>
    /// Puts a first appearance in front of the player: the carts at the signpost, and the buildings and drops at fixed
    /// spots left of the camp stop, off the sprint, which comes up the camp's middle and turns right toward the pond.
    /// </summary>
    private void Show(string name) {
        static Vector2 NearCamp(float along, float across) => Camp + Terrain.Point(along, across);
        var post = world.Things.First(t => t.Kind == ThingKind.Signpost);
        switch (name) {
            case "cart" or "second cart": world.S.Orders.Add(new TradeOrder { Signpost = post.Id, Crates = 3, Goods = { ["berries"] = 30 } }); break;
            case "buildings": foreach (var (kind, at) in new[] { (ThingKind.Cannery, NearCamp(8, -2.5f)), (ThingKind.Cooktable, NearCamp(6.5f, -2.5f)), (ThingKind.Packer, NearCamp(5, -2.5f)), (ThingKind.Hut, NearCamp(7, -5)) }) world.Add(kind, at); break;
            case "drops": world.Add(ThingKind.WolfCorpse, NearCamp(2, -2.5f), World.WolfMeat); world.Add(ThingKind.Grave, NearCamp(.5f, -2.5f)); world.Add(ThingKind.Crate, NearCamp(-1, -2.5f)); break;
        }
    }
}
