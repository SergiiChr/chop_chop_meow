using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Anything that walks the map on its own: it steps straight at where it is going, or follows a route around what blocks it.</summary>
public interface IWalker {
    Vector2 Pos { get; set; }
    List<Vector2> Route { get; }
    Vector2 RouteTarget { get; set; }
    /// <summary>When a walker that found no way to its destination looks again.</summary>
    float RouteRetry { get; set; }
}

/// <summary>A small cave at the foot of the mountains, <see cref="Face"/> pointing out into the valley; its wolves come out at dusk and go back in before dawn.</summary>
public readonly record struct Den(Vector2 Mouth, Vector2 Face) {
    /// <summary>The den is a mine outcrop at this size.</summary>
    public const float Scale = .5f;
    /// <summary>Open ground in front of the mouth, where the wolves come out and go back in.</summary>
    public Vector2 Door => Mouth + Face * 1.5f;
    /// <summary>Where a mine's cart would stand: a mine's tunnel opens 2.2 m in from it, so at half size the den's opens at <see cref="Mouth"/>.</summary>
    public Vector2 Spot => Mouth + Face * 1.1f;
    /// <summary>How far from the door nothing may be built, so the wolves always have a way out.</summary>
    public const float Clearance = 4;
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
    /// <summary>A wolf hunting nobody walks its patrol at this share of its trot.</summary>
    private const float PatrolPace = .5f;
    /// <summary>From an hour before dawn every wolf gives up and heads back to the nearest den.</summary>
    public bool WolvesRetreat => DayPhase >= Data.WolfRetreatStarts;

    /// <summary>Seconds after dusk between one den opening and the next, so their wolves do not all appear on the same frame.</summary>
    public const float DenGapSeconds = 2;
    public static bool NearDen(Vector2 p, float distance) {
        foreach (var den in Dens) if (den.Door.DistanceTo(p) < distance) return true;
        return false;
    }

    /// <summary>Lets each den's wolves out in turn once nobody can see its door, and moves every wolf that hunts nobody.</summary>
    private void TickDens(float dt) {
        float sinceDusk = (DayPhase - Data.DuskStarts) * Data.DaySeconds;
        if (!WolvesRetreat)
            for (int i = 0; i < Dens.Length; i++) {
                if ((S.DensOut & 1 << i) != 0 || sinceDusk < i * DenGapSeconds || !Unseen(Dens[i].Door)) continue;
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
                    Wander(wolf, step * PatrolPace); Smell(wolf);
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
    /// <summary>
    /// Walks a patrolling wolf on its heading; ground ahead nobody can stand on, or firelight, turns it a new random way it can take.
    /// A wolf already in firelight, such as one let out of a den beside a camp, may walk on to get out of it.
    /// </summary>
    private void Wander(Wolf wolf, float step) {
        bool lit = Protected(wolf.Pos);
        var ahead = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * step;
        for (int tries = 0; !Roams(ahead, lit) && tries < 12; tries++) {
            wolf.Heading = (float)wolfRandom.NextDouble() * MathF.Tau;
            ahead = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * step;
        }
        if (Roams(ahead, lit)) wolf.Pos = ahead;
    }
    private bool Roams(Vector2 p, bool lit) => CanStand(p) && (lit || !Protected(p));
    /// <summary>Sends a wolf back to patrolling on a heading, hunting nobody.</summary>
    private static void Patrol(Wolf wolf, float heading) {
        Calm(wolf); wolf.Mode = WolfMode.Patrol; wolf.Heading = heading;
    }
    /// <summary>Calls a wolf off whatever it is doing and sends it to the nearest den.</summary>
    private static void GoHome(Wolf wolf) {
        Calm(wolf); wolf.Mode = WolfMode.Home;
    }
    /// <summary>Clears everything a wolf was doing to the player it hunted.</summary>
    private static void Calm(Wolf wolf) {
        wolf.Target = 0; wolf.Howl = wolf.Leap = wolf.Chase = wolf.Flee = wolf.Stagger = 0;
        wolf.Feeding = wolf.Pinning = wolf.Scared = wolf.Growled = wolf.Sprinted = false; wolf.Route.Clear();
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
