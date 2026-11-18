using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Follow mode in <paramref name="world"/>: wolves a torch wave scared off keep circling the acting player at the edge of
/// the torchlight, spread evenly round them. One the player walks at stands braced where it is. From
/// <see cref="StandOrRunAt"/> a wolf following alone runs out of the player's line of sight and goes back to patrolling,
/// while one of several stands its ground: it growls from <see cref="GrowlFrom"/> and turns on the player, for good.
/// </summary>
public sealed class WolfFollow(World world) {
    /// <summary>Metres from a following wolf at which a player walking at it makes it run, alone, or turn on them, among others.</summary>
    public const float StandOrRunAt = 2;
    /// <summary>Metres from which a wolf standing its ground among others growls, which leaves the player room to back off.</summary>
    public const float GrowlFrom = 3;
    /// <summary>Seconds from one growl to the next: the longer growl recording and a breath.</summary>
    public const float GrowlEvery = 3.5f;
    /// <summary>Metres within which a player come nearer than the circle makes a wolf stop and brace, and beyond which it circles again.</summary>
    public const float BraceFrom = 3.6f, BraceUntil = 4;
    /// <summary>How far the circle swells and shrinks for each wolf as it goes, in metres, so the pack does not walk one plain ring.</summary>
    public const float Wander = .7f;
    /// <summary>Seconds a wolf that runs from a player gives itself to get out of their sight before it patrols anyway.</summary>
    public const float RoutSeconds = 8;
    /// <summary>How far ahead of its place on the circle a wolf heads, in radians, which keeps it walking round.</summary>
    private const float Lead = .3f;
    /// <summary>The furthest round the circle a wolf heads in one go, in radians, so it goes round the player and never across them.</summary>
    private const float Arc = .6f;
    /// <summary>Metres a second a wolf moves for each metre it is from where it heads: a walk round the circle, a gallop to catch up.</summary>
    private const float Keenness = 1.2f;

    /// <summary>The wolves following the acting player this tick, in order round them.</summary>
    private readonly List<Wolf> ring = new();
    /// <summary>The bearing from the player of the first place on the circle, in radians.</summary>
    private float first;

    /// <summary>
    /// Orders the wolves following the acting player by their bearing round them, and turns the circle's evenly spread
    /// places to where they fit those wolves best.
    /// </summary>
    internal void Arrange(List<Wolf> hunters) {
        ring.Clear();
        foreach (var wolf in hunters) {
            if (!wolf.Scared) continue;
            int at = ring.Count;
            while (at > 0 && Bearing(ring[at - 1]) > Bearing(wolf)) at--;
            ring.Insert(at, wolf);
        }
        // The mean, round the circle, of how far each wolf is from the place its turn in the order gives it.
        var mean = Vector2.Zero;
        for (int i = 0; i < ring.Count; i++) mean += Vector2.Right.Rotated(Bearing(ring[i]) - i * MathF.Tau / ring.Count);
        // Wolves all on one side cancel out: the first of them keeps its place and the rest spread from it.
        first = mean.LengthSquared() > 1e-4f ? mean.Angle() : ring.Count > 0 ? Bearing(ring[0]) : 0;
    }

    /// <summary>
    /// Moves a following wolf: round the circle toward its place, or not at all while the player is close, when it braces,
    /// growls, and from <see cref="StandOrRunAt"/> runs or turns on them. <paramref name="braced"/> tells that it stood
    /// braced last tick.
    /// </summary>
    internal void Follow(Wolf wolf, bool braced, float dt, float trot, float chase) {
        float near = wolf.Pos.DistanceTo(world.Player);
        if (near < StandOrRunAt) {
            wolf.Scared = false; wolf.GrowlIn = 0;
            if (ring.Count > 1) wolf.Bold = true; else { wolf.Routed = true; wolf.Flee = RoutSeconds; }
            return;
        }
        if (near < (braced ? BraceUntil : BraceFrom)) {
            wolf.Braced = true; wolf.Chase = 0;
            if (ring.Count > 1 && near < GrowlFrom) Growl(wolf, dt); else wolf.GrowlIn = 0;
            return;
        }
        wolf.GrowlIn = 0;
        // Each wolf's place drifts ahead and back and its circle in and out at its own pace, by its saved id, so it replays the same.
        float time = world.S.Time, own = wolf.Id * 1.7f;
        float place = first + ring.IndexOf(wolf) * MathF.Tau / ring.Count + Lead * (1 + .5f * MathF.Sin(time * .7f + own * 1.4f));
        float bearing = Bearing(wolf), radius = World.StalkDistance + Wander * (.65f * MathF.Sin(time * .9f + own) + .35f * MathF.Sin(time * 2.3f + own * 1.7f));
        var to = world.Player + Vector2.Right.Rotated(bearing + Math.Clamp(Mathf.AngleDifference(bearing, place), -Arc, Arc)) * radius - wolf.Pos;
        float pace = Math.Min(chase, to.Length() * Keenness);
        // Above a trot it gallops to catch up, as the view shows a chasing wolf.
        wolf.Chase = pace > trot ? dt : 0;
        wolf.Pos = world.Slide(wolf.Pos, to.LimitLength(dt * pace));
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
