using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The server's half of a performance run, on the timetable the window samples on: it keeps the player fed, runs the clock quickly through a day, and puts what appears for the first time in front of them.
/// The player's own moves stay in the window, which walks them as a player would.
/// </summary>
internal partial class PerformanceStage(World world) : Node {
    public const double WarmupSeconds = 15, CycleSeconds = 240, RunSeconds = 360;
    /// <summary>Times during production when something first appears mid-game, which should draw from models warmed while loading.</summary>
    public static readonly (double Seconds, string Name)[] FirstTimes = { (260, "cart"), (280, "buildings"), (300, "drops"), (320, "second cart") };
    private ulong started;
    private int firsts;
    private bool settlement;

    public override void _Process(double delta) {
        if (world.Players.Count == 0) return;
        if (started == 0) started = Time.GetTicksMsec();
        double seconds = (Time.GetTicksMsec() - started) / 1000.0 - WarmupSeconds;
        var s = world.S;
        foreach (var p in world.Players) p.Hunger = p.Thirst = p.Health = 100;
        if (seconds >= CycleSeconds && !settlement) { settlement = true; s.Time = world.Data.DaySeconds * .22f; }
        // Only the clock is accelerated. Movement, production, particles and simulation still receive their normal frame delta.
        if (!settlement && seconds >= 0) s.Time += Math.Min((float)delta, .1f) * 2.5f;
        if (seconds < 0) s.Time = world.Data.DaySeconds * .12f;
        if (firsts < FirstTimes.Length && seconds >= FirstTimes[firsts].Seconds) Show(FirstTimes[firsts++].Name);
    }

    private void Show(string name) {
        var at = world.Players[0].Pos;
        var post = world.Things.First(t => t.Kind == ThingKind.Signpost);
        switch (name) {
            case "cart" or "second cart": world.S.Orders.Add(new TradeOrder { Signpost = post.Id, Crates = 3, Goods = { ["berries"] = 30 } }); break;
            case "buildings": foreach (var (kind, x) in new[] { (ThingKind.Cannery, -3f), (ThingKind.Cooktable, -1f), (ThingKind.Packer, 1f), (ThingKind.Hut, 4f) }) world.Add(kind, at + new Vector2(x, 3)); break;
            case "drops": world.Add(ThingKind.WolfCorpse, at + new Vector2(-2, -2), World.WolfMeat); world.Add(ThingKind.Grave, at + new Vector2(0, -2)); world.Add(ThingKind.Crate, at + new Vector2(2, -2)); break;
        }
    }
}
