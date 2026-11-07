using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Anything that walks the map on its own: it steps straight at where it is going, or follows a route around what blocks it.</summary>
public interface IWalker {
    Vector2 Pos { get; set; }
    List<Vector2> Route { get; }
    Vector2 RouteTarget { get; set; }
}

/// <summary>A small cave at the foot of the mountains, <see cref="Face"/> pointing out into the valley; its wolves come out at dusk and go back in before dawn.</summary>
public readonly record struct Den(Vector2 Mouth, Vector2 Face) {
    /// <summary>Open ground in front of the mouth, where the wolves come out and go back in.</summary>
    public Vector2 Door => Mouth + Face * 1.5f;
}

/// <summary>Wolves at night: they come out of the dens at dusk and patrol until one smells a player, howls and goes after them, and before dawn they all go home.</summary>
public sealed partial class World {
    /// <summary>The dens along the valley from its inland end, each dug into the range on one side.</summary>
    public static readonly Den[] Dens = { FootDen(17, 1), FootDen(56, -1), FootDen(92, 1), FootDen(166, -1), FootDen(211, 1) };
    private static Den FootDen(float along, int side) => new(Terrain.Point(along, side * (Terrain.HalfWidth(along, side) - 1)), Terrain.Sideways * -side);
    public static Den NearestDen(Vector2 p) {
        var nearest = Dens[0];
        foreach (var den in Dens) if (den.Door.DistanceTo(p) < nearest.Door.DistanceTo(p)) nearest = den;
        return nearest;
    }
    /// <summary>How far the player a wolf runs at may move before it looks for a new way around what blocks it.</summary>
    private const float RouteSlack = 2;
    private readonly Random wolfRandom = new(1913);
    /// <summary>Metres a second a wolf patrols, stalks and backs off at; it speeds up a little each day.</summary>
    private float WolfTrot => Math.Min(3.8f, 3 + Day * .1f);
    /// <summary>From an hour before dawn every wolf gives up and heads back to the nearest den.</summary>
    public bool WolvesRetreat => DayPhase >= Data.WolfRetreatStarts;

    /// <summary>Lets each den's wolves out once nobody can see its door, and moves every wolf that hunts nobody.</summary>
    private void TickDens(float dt) {
        if (!WolvesRetreat)
            for (int i = 0; i < Dens.Length; i++) {
                if ((S.DensOut & 1 << i) != 0 || !Unseen(Dens[i].Door)) continue;
                S.DensOut |= 1 << i;
                // Each wolf of a den sets off its own way.
                float heading = (float)wolfRandom.NextDouble() * MathF.Tau;
                for (int n = 0; n < Data.WolvesPerDen; n++) S.Wolves.Add(new Wolf { Pos = Dens[i].Door, Health = 4 + Day * .4f, Heading = heading + n * MathF.Tau / Data.WolvesPerDen });
            }
        for (int i = S.Wolves.Count - 1; i >= 0; i--) {
            var wolf = S.Wolves[i];
            if (WolvesRetreat && wolf.Mode != WolfMode.Home) GoHome(wolf);
            if (wolf.Target != 0) continue;
            float step = dt * WolfTrot;
            switch (wolf.Mode) {
                case WolfMode.Patrol:
                    Wander(wolf, step); Smell(wolf);
                    break;
                case WolfMode.Called:
                    // A called wolf still noses the air on the way, so it does not run past a player it meets.
                    if (!Travel(wolf, wolf.Call, step, step) || wolf.Pos.DistanceTo(wolf.Call) < 2) Patrol(wolf, wolf.Heading);
                    Smell(wolf);
                    break;
                case WolfMode.Home:
                    var door = NearestDen(wolf.Pos).Door;
                    Travel(wolf, door, step, step);
                    if (wolf.Pos.DistanceTo(door) < 1) S.Wolves.RemoveAt(i);
                    break;
            }
        }
    }
    /// <summary>Walks a patrolling wolf on its heading; ground ahead nobody can stand on turns it a new random way it can take.</summary>
    private void Wander(Wolf wolf, float step) {
        var ahead = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * step;
        for (int tries = 0; !CanStand(ahead) && tries < 12; tries++) {
            wolf.Heading = (float)wolfRandom.NextDouble() * MathF.Tau;
            ahead = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * step;
        }
        if (CanStand(ahead)) wolf.Pos = ahead;
    }
    /// <summary>Sends a wolf back to patrolling on a heading, hunting nobody.</summary>
    private static void Patrol(Wolf wolf, float heading) {
        wolf.Target = 0; wolf.Mode = WolfMode.Patrol; wolf.Heading = heading; wolf.Chase = 0; wolf.Scared = wolf.Sprinted = false; wolf.Route.Clear();
    }
    /// <summary>Calls a wolf off whatever it is doing and sends it to the nearest den.</summary>
    private static void GoHome(Wolf wolf) {
        wolf.Target = 0; wolf.Mode = WolfMode.Home; wolf.Howl = wolf.Leap = wolf.Chase = wolf.Flee = 0; wolf.Feeding = wolf.Pinning = wolf.Scared = wolf.Sprinted = false; wolf.Route.Clear();
    }
    /// <summary>
    /// A wolf that smells a player out in the open within <see cref="GameData.SmellRadius"/> takes them as its prey and howls before it goes after them.
    /// The howl calls every patrolling wolf within <see cref="GameData.HowlRadius"/> to where it stands.
    /// </summary>
    private void Smell(Wolf wolf) {
        PlayerState? prey = null;
        foreach (var p in S.Players)
            if (p.RespawnIn <= 0 && p.Sheltered == 0 && p.Pos.DistanceTo(wolf.Pos) <= Data.SmellRadius && (prey == null || p.Pos.DistanceTo(wolf.Pos) < prey.Pos.DistanceTo(wolf.Pos))) prey = p;
        if (prey == null) return;
        // The night's first howl is a lone wolf; every later one sounds like the pack answering.
        // The wolf holds its howl as long as the recording plays.
        bool answer = S.Howled;
        wolf.Target = prey.Id; wolf.Mode = WolfMode.Patrol; wolf.Howl = wolf.HowlLength = answer ? Data.PackHowlSeconds : Data.LoneHowlSeconds; wolf.Route.Clear();
        Play(answer ? "wolf_howl_group" : "wolf_howl", wolf.Pos);
        S.Howled = true;
        foreach (var other in S.Wolves)
            if (other.Target == 0 && other.Mode != WolfMode.Home && other.Pos.DistanceTo(wolf.Pos) <= Data.HowlRadius) { other.Mode = WolfMode.Called; other.Call = wolf.Pos; other.Route.Clear(); }
    }
}
