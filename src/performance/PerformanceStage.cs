using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The server's half of a performance run: it keeps the player fed, holds the clock at the day sprint's start until the
/// player sets off, puts things in front of them for the first time as they reach the camp and turns the clock to
/// midnight once the window moves them back to the beach for the night sprint.
/// The player's own moves stay in the window, which sprints them along <see cref="PerformanceRoute"/> as a player would.
/// </summary>
internal partial class PerformanceStage(World world) : Node {
    /// <summary>Seconds standing on the beach before the day sprint, while the first pipelines compile.</summary>
    public const double WarmupSeconds = 15;
    /// <summary>Seconds between the day sprint and the night one, while the player is moved back and lights a torch.</summary>
    public const double SettleSeconds = 3;
    /// <summary>Clock at the start of the day and night sprints, as shares of a day starting at 06:00: 13:30 and midnight.</summary>
    internal const float DayStart = 7.5f / 24, NightStart = 18f / 24;
    /// <summary>Seconds after the player reaches the camp when something first appears in front of them, which should draw from models warmed while loading.</summary>
    public static readonly (double After, string Name)[] FirstTimes = { (0, "cart"), (.5, "buildings"), (1, "drops"), (1.5, "second cart") };
    /// <summary>Units within which the server counts the player as at a place; wider than a stop, as it sees them less often.</summary>
    private const float Arrived = 1;

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
            onBeach |= at.DistanceTo(PerformanceRoute.Start) < Arrived;
            setOff = onBeach && at.DistanceTo(PerformanceRoute.Start) >= Arrived;
            s.Time = day + world.Data.DaySeconds * DayStart;
            return;
        }
        if (campReached == 0 && at.DistanceTo(PerformanceRoute.Camp) < Arrived) campReached = Time.GetTicksMsec();
        if (campReached != 0 && firsts < FirstTimes.Length && Time.GetTicksMsec() - campReached >= FirstTimes[firsts].After * 1000) Show(FirstTimes[firsts++].Name);
        if (!night && at.DistanceTo(PerformanceRoute.Start) < Arrived) { night = true; s.Time = day + world.Data.DaySeconds * NightStart; }
    }

    /// <summary>
    /// Puts a first appearance in front of the player: the carts at the signpost, and the buildings and drops at fixed
    /// spots left of the camp stop, off the sprint, which comes up the camp's middle and turns right toward the pond.
    /// </summary>
    private void Show(string name) {
        static Vector2 NearCamp(float along, float across) => PerformanceRoute.Camp + Terrain.Point(along, across);
        var post = world.Things.First(t => t.Kind == ThingKind.Signpost);
        switch (name) {
            case "cart" or "second cart": world.S.Orders.Add(new TradeOrder { Signpost = post.Id, Crates = 3, Goods = { ["berries"] = 30 } }); break;
            case "buildings": foreach (var (kind, at) in new[] { (ThingKind.Cannery, NearCamp(8, -2.5f)), (ThingKind.Cooktable, NearCamp(6.5f, -2.5f)), (ThingKind.Packer, NearCamp(5, -2.5f)), (ThingKind.Hut, NearCamp(7, -5)) }) world.Add(kind, at); break;
            case "drops": world.Add(ThingKind.WolfCorpse, NearCamp(2, -2.5f), World.WolfMeat); world.Add(ThingKind.Grave, NearCamp(.5f, -2.5f)); world.Add(ThingKind.Crate, NearCamp(-1, -2.5f)); break;
        }
    }
}
