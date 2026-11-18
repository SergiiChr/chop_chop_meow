using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Follow mode in <paramref name="world"/>: wolves a torch wave scared off keep circling the acting player at a walk, at
/// the edge of the torchlight, each its own way round. One the player walks at stands braced where it is. From
/// <see cref="StandOrRunAt"/> a wolf following alone runs out of the player's line of sight and goes back to patrolling,
/// while one of several stands its ground: it growls from <see cref="GrowlFrom"/>, and then every wolf hunting the
/// player within their line of sight turns on them, for good.
/// </summary>
public sealed class WolfFollow(World world) {
    /// <summary>Metres from a following wolf at which a player walking at it makes it run, alone, or the pack turn on them, among others.</summary>
    public const float StandOrRunAt = 2;
    /// <summary>Metres from which a wolf standing its ground among others growls, which leaves the player room to back off.</summary>
    public const float GrowlFrom = 3;
    /// <summary>Seconds from one growl to the next: the longer growl recording and a breath.</summary>
    public const float GrowlEvery = 3.5f;
    /// <summary>Metres within which a player come nearer than the circle makes a wolf stop and brace, and beyond which it circles again.</summary>
    public const float BraceFrom = 3.6f, BraceUntil = 4;
    /// <summary>
    /// How far off the circle a wolf strays, in metres: wolves going one way round walk <see cref="Lane"/> outside it and
    /// the others inside, so the two pass each other, and each drifts <see cref="Drift"/> in and out as it goes, so the pack
    /// does not walk one plain ring.
    /// </summary>
    public const float Wander = Lane + Drift;
    private const float Lane = .45f, Drift = .3f;
    /// <summary>Seconds a wolf that runs from a player gives itself to get out of their sight before it patrols anyway.</summary>
    public const float RoutSeconds = 8;
    /// <summary>
    /// How far ahead of its place on the circle a wolf heads, in radians, which keeps it walking round: at about two thirds
    /// of its walk once in place, so one behind its place still has pace to make it up.
    /// </summary>
    private const float Lead = .2f;
    /// <summary>The furthest round the circle a wolf heads in one go, in radians, so it goes round the player and never across them.</summary>
    private const float Arc = .6f;
    /// <summary>Metres a second a wolf moves for each metre it is from where it heads, up to its walk.</summary>
    private const float Keenness = 1.2f;

    /// <summary>The wolves hunting the acting player this tick, and those of them that follow.</summary>
    private List<Wolf> pack = new();
    private int following;
    /// <summary>The followers going each way round the player, in order round them, and the bearing of each way's first place, in radians.</summary>
    private readonly List<Wolf> clockwise = new(), counter = new();
    private float firstClockwise, firstCounter;
    // Seeded, so a recording of a night plays the same every time.
    private readonly Random ways = new(3571);

    /// <summary>
    /// Counts the wolves following the acting player, gives each new one a way round them at random, and for each way
    /// orders its wolves by bearing and turns its evenly spread places to where they fit those wolves best.
    /// </summary>
    internal void Arrange(List<Wolf> hunters) {
        pack = hunters; following = 0;
        clockwise.Clear(); counter.Clear();
        foreach (var wolf in hunters) {
            if (!wolf.Scared) continue;
            following++;
            if (wolf.Round == 0) wolf.Round = ways.Next(2) == 0 ? 1 : -1;
            var ring = wolf.Round > 0 ? clockwise : counter;
            int at = ring.Count;
            while (at > 0 && Bearing(ring[at - 1]) > Bearing(wolf)) at--;
            ring.Insert(at, wolf);
        }
        firstClockwise = First(clockwise); firstCounter = First(counter);
    }

    /// <summary>The bearing of the first of a ring's evenly spread places that fits its wolves best.</summary>
    private float First(List<Wolf> ring) {
        // The mean, round the circle, of how far each wolf is from the place its turn in the order gives it.
        var mean = Vector2.Zero;
        for (int i = 0; i < ring.Count; i++) mean += Vector2.Right.Rotated(Bearing(ring[i]) - i * MathF.Tau / ring.Count);
        // Wolves all on one side cancel out: the first of them keeps its place and the rest spread from it.
        return mean.LengthSquared() > 1e-4f ? mean.Angle() : ring.Count > 0 ? Bearing(ring[0]) : 0;
    }

    /// <summary>
    /// Moves a following wolf: round the circle toward its place at a walk, or not at all while the player is close, when
    /// it braces, growls, and from <see cref="StandOrRunAt"/> runs or sets the pack on them. <paramref name="braced"/>
    /// tells that it stood braced last tick, and <paramref name="walk"/> is a wolf's walking pace.
    /// </summary>
    internal void Follow(Wolf wolf, bool braced, float dt, float walk) {
        float near = wolf.Pos.DistanceTo(world.Player);
        wolf.Chase = 0;
        if (near < StandOrRunAt) {
            if (following > 1) TurnOnPlayer(); else { wolf.Scared = false; wolf.GrowlIn = 0; wolf.Routed = true; wolf.Flee = RoutSeconds; }
            return;
        }
        if (near < (braced ? BraceUntil : BraceFrom)) {
            wolf.Braced = true;
            if (following > 1 && near < GrowlFrom) Growl(wolf, dt); else wolf.GrowlIn = 0;
            return;
        }
        wolf.GrowlIn = 0;
        var ring = wolf.Round > 0 ? clockwise : counter;
        // Each wolf's place drifts ahead and back and its circle in and out at its own pace, by its saved id, so it replays the same.
        float time = world.S.Time, own = wolf.Id * 1.7f;
        float place = (wolf.Round > 0 ? firstClockwise : firstCounter) + ring.IndexOf(wolf) * MathF.Tau / ring.Count
            + wolf.Round * Lead * (1 + .5f * MathF.Sin(time * .7f + own * 1.4f));
        float bearing = Bearing(wolf), radius = World.StalkDistance + wolf.Round * Lane + Drift * (.65f * MathF.Sin(time * .9f + own) + .35f * MathF.Sin(time * 2.3f + own * 1.7f));
        var to = world.Player + Vector2.Right.Rotated(bearing + Math.Clamp(Mathf.AngleDifference(bearing, place), -Arc, Arc)) * radius - wolf.Pos;
        wolf.Pos = world.Slide(wolf.Pos, to.LimitLength(dt * Math.Min(walk, to.Length() * Keenness)));
    }

    /// <summary>
    /// Every wolf hunting the acting player within their line of sight turns on them: it attacks as with no torch lit, and
    /// no torch wave scares it again.
    /// </summary>
    private void TurnOnPlayer() {
        foreach (var wolf in pack) {
            if (wolf.Routed || wolf.Pos.DistanceTo(world.Player) > world.Data.SightRadius) continue;
            wolf.Scared = wolf.Braced = false; wolf.Bold = true; wolf.GrowlIn = 0;
        }
    }

    /// <summary>A wolf that ran from the acting player goes back to patrolling, and leaves them alone until it has been out of smell range of them.</summary>
    internal void GiveUp(Wolf wolf) {
        int player = wolf.Target;
        WolfPatrols.Patrol(wolf, world.WolfHunt.AwayFromPlayer(wolf).Angle());
        if (!wolf.Shunned.Contains(player)) wolf.Shunned.Add(player);
    }

    private float Bearing(Wolf wolf) => (wolf.Pos - world.Player).Angle();

    /// <summary>A wolf standing its ground growls again and again, starting at once.</summary>
    private void Growl(Wolf wolf, float dt) {
        wolf.GrowlIn -= dt;
        if (wolf.GrowlIn > 0) return;
        wolf.GrowlIn = GrowlEvery; world.Play("wolf_growl_approaching", wolf.Pos);
    }
}
