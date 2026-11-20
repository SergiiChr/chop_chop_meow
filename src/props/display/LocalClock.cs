using System;

namespace ChopChopMeow;

/// <summary>
/// A count the server keeps that a drawn motion follows, such as how far a stump has been dug or how far a cart is into
/// its trip, run on by the window between the server's updates.
/// A window applies updates thirty times a second, so a motion drawn straight from the count would step every other frame.
/// The clock moves on every drawn frame at the rate the count last ran at against the world's own clock, and each update
/// corrects it: a small difference is made up over the next frames without going back the way the count came, and a
/// big one, such as a pump of the bellows or a new stage, is taken at once.
/// A world run inside the window moves the count every frame, and the clock then shows it as it is.
/// </summary>
public sealed class LocalClock(float jump) {
    /// <summary>Seconds the clock runs on past the last update before it waits for the next: three updates' worth.</summary>
    public const float MaxAhead = .1f;
    /// <summary>How quickly a small difference from the count is made up: about a fifth of it every frame at 60 a second.</summary>
    private const float CatchUp = 12;
    private float count, stamp, rate, since, way;
    private bool started;
    /// <summary>The count as drawn now.</summary>
    private float shown;

    /// <summary>Takes the count as it is, as a thing drawn from memory or seen again shows it.</summary>
    public float Set(float now, float time) {
        (shown, count, stamp, rate, since, way, started) = (now, now, time, 0, 0, 0, true);
        return now;
    }

    /// <summary>
    /// Moves the clock on by <paramref name="dt"/> seconds of drawing toward <paramref name="now"/>, the count as the world
    /// holds it at <paramref name="time"/> on the world's clock, and returns it as drawn.
    /// </summary>
    public float Follow(float now, float time, float dt) {
        if (!started || time < stamp) return Set(now, time);
        if (time != stamp) {
            rate = (now - count) / (time - stamp);
            if (rate != 0) way = Math.Sign(rate);
            (count, stamp, since) = (now, time, 0);
            if (Math.Abs(now - shown) > jump) { shown = now; return shown; }
        } else since += dt;
        float target = count + rate * Math.Min(since, MaxAhead);
        float next = shown + rate * dt;
        next += (target - next) * (1 - MathF.Exp(-CatchUp * dt));
        // A small lead is held rather than given back, so the motion never turns back on itself.
        shown = way > 0 ? Math.Max(next, shown) : way < 0 ? Math.Min(next, shown) : next;
        return shown;
    }
}
