using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The sun or moon on its arc across the sky. Only the moon can be hovered.</summary>
public partial class DayDial(World world) : Control {
    private const float IconRoom = 17, MoonRadius = 11, MoonRim = 13;
    private const int MoonSteps = 20, Rays = 12;
    private static readonly Color NightArc = new("8fa6c4"), Moon = new("e5ecf7"), MoonShadow = new("526680");
    private static readonly Color Crater = new("a6b5cb"), CraterShadow = new("3c4e68"), PaleRim = new("f6f8fe"), DarkRim = new("6b7e98");
    private static readonly Color SunGold = new("edb94f"), SunCore = new("ffdb79"), SunGlint = new("fff0b4");
    private static readonly (Vector2 At, float Radius)[] Craters = [
        (new(-3.7f, 1.6f), 2.35f), (new(-.5f, -4.8f), 1.7f), (new(3.9f, 3.0f), 1.55f),
        (new(1.0f, 7.1f), .9f), (new(6.0f, -2.8f), 1.05f), (new(-6.7f, -3.8f), .85f)
    ];
    private readonly Vector2[] litMoon = new Vector2[2 * MoonSteps];

    private Vector2 Centre => new(Size.X / 2, Size.Y - IconRoom);
    private float Radius => Math.Max(0, Math.Min(Size.X / 2 - IconRoom, Size.Y - 2 * IconRoom));
    private float Angle => Mathf.Pi + Mathf.Clamp(world.SkyArc, 0, 1) * Mathf.Pi;
    internal Vector2 Marker => Centre + Vector2.FromAngle(Angle) * Radius;

    public override bool _HasPoint(Vector2 point) => world.MoonUp && point.DistanceSquaredTo(Marker) <= MoonRim * MoonRim;

    public override string _GetTooltip(Vector2 atPosition) {
        if (!_HasPoint(atPosition)) return "";
        var (days, high) = world.Beach.NextTide;
        int tenths = Mathf.CeilToInt(days * 10);
        return $"{tenths / 10f:0.#} {(tenths == 10 ? "day" : "days")} till {(high ? "high" : "low")} tide";
    }

    public override void _Draw() {
        bool moonUp = world.MoonUp;
        var centre = Centre;
        float radius = Radius, angle = Angle;
        DrawArc(centre, radius, Mathf.Pi, Mathf.Tau, 40, Style.Line, 8, true);
        DrawArc(centre, radius, Mathf.Pi, Math.Max(Mathf.Pi * 1.02f, angle), 40, moonUp ? NightArc : Style.Mustard, 8, true);
        var marker = centre + Vector2.FromAngle(angle) * radius;
        if (moonUp) DrawMoon(marker); else DrawSun(marker);
    }

    private void DrawSun(Vector2 at) {
        DrawCircle(at, 15.75f, Style.Rim, antialiased: true);
        for (int i = 0; i < Rays; i++) {
            var outward = Vector2.FromAngle(i * Mathf.Tau / Rays);
            DrawLine(at + outward * 11, at + outward * (i % 2 == 0 ? 14.6f : 13.7f), Style.Mustard, 2, true);
        }
        DrawCircle(at, 10, Style.Line, antialiased: true);
        DrawCircle(at, 8.9f, SunGold, antialiased: true);
        DrawCircle(at + new Vector2(-1.1f, -1.2f), 6.65f, SunCore, antialiased: true);
        DrawArc(at, 6.5f, Mathf.Pi * .93f, Mathf.Pi * 1.53f, 12, SunGlint, 1.2f, true);
    }

    private void DrawMoon(Vector2 at) {
        float light = world.Moonlight, side = world.MoonWaxing ? 1 : -1, edge = 1 - 2 * light;
        DrawCircle(at, MoonRim, Style.Rim, antialiased: true);
        DrawCircle(at, MoonRadius + .8f, Style.Line, antialiased: true);
        DrawCircle(at, MoonRadius, light >= .995f ? Moon : MoonShadow, antialiased: true);
        if (light is > .005f and < .995f) {
            // Trace the lit rim first, then the curved edge between light and shadow back to the top.
            litMoon[0] = at - Vector2.Down * MoonRadius;
            litMoon[MoonSteps] = at + Vector2.Down * MoonRadius;
            for (int i = 1; i < MoonSteps; i++) {
                float angle = i * Mathf.Pi / MoonSteps;
                float x = side * MoonRadius * Mathf.Sin(angle), y = -MoonRadius * Mathf.Cos(angle);
                litMoon[i] = at + new Vector2(x, y);
                litMoon[2 * MoonSteps - i] = at + new Vector2(x * edge, y);
            }
            DrawColoredPolygon(litMoon, Moon);
        }
        foreach (var (offset, size) in Craters) {
            float edgeAt = edge * MathF.Sqrt(MoonRadius * MoonRadius - offset.Y * offset.Y);
            float lit = Mathf.SmoothStep(-size, size, offset.X * side - edgeAt);
            DrawCircle(at + offset, size, CraterShadow.Lerp(Crater, lit), antialiased: true);
            DrawArc(at + offset, size, .18f, 2.45f, 10, DarkRim.Lerp(PaleRim, lit), .8f, true);
        }
    }
}
