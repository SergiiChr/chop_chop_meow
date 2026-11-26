using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Wolves at night in <paramref name="world"/>: they come out of the dens from dusk and patrol until one smells a player, howls and goes after them, and before dawn they all go home.</summary>
public sealed class WolfPatrols(World world) {
    /// <summary>The dens along the valley from its inland end, each dug into the range on one side.</summary>
    public static readonly Den[] Dens = { FootDen(17, 1), FootDen(56, -1), FootDen(92, 1), FootDen(166, -1), FootDen(211, 1) };
    private static Den FootDen(float along, int side) => new(Terrain.Point(along, side * (Terrain.HalfWidth(along, side) - 1)), Terrain.Sideways * -side);
    public static Den NearestDen(Vector2 p) {
        var nearest = Dens[0];
        foreach (var den in Dens) if (den.Door.DistanceTo(p) < nearest.Door.DistanceTo(p)) nearest = den;
        return nearest;
    }
    private readonly Random wolfRandom = new(1913);
    /// <summary>A wolf hunting nobody walks its patrol at this share of its trot.</summary>
    internal const float PatrolPace = .5f;
    /// <summary>From an hour before dawn every wolf gives up and heads back to the nearest den.</summary>
    public bool WolvesRetreat => world.DayPhase >= world.Data.WolfRetreatStarts;

    /// <summary>Seconds after dusk between one den opening and the next, so their wolves do not all appear on the same frame.</summary>
    public const float DenGapSeconds = 2;
    /// <summary>Whether a point lies within a den's outcrop: a mine's outcrop at the den's smaller size.</summary>
    public static bool InOutcrop(Vector2 p, float margin = 0) {
        foreach (var den in Dens) if (Mines.OutcropCovers((p - den.Spot) / Den.Scale, den.Face, margin / Den.Scale)) return true;
        return false;
    }
    public static bool NearDen(Vector2 p, float distance) {
        foreach (var den in Dens) if (den.Door.DistanceTo(p) < distance) return true;
        return false;
    }

    /// <summary>
    /// Lets each den's wolves out in turn at dusk once nobody can see its door, then one more from a den picked at random every hour,
    /// and moves every wolf that hunts nobody.
    /// </summary>
    internal void TickDens(float dt) {
        var (s, data) = (world.S, world.Data);
        float sinceDusk = (world.DayPhase - data.DuskStarts) * data.DaySeconds;
        if (!WolvesRetreat) {
            for (int i = 0; i < Dens.Length; i++) {
                if ((s.DensOut & 1 << i) != 0 || sinceDusk < i * DenGapSeconds || !world.Unseen(Dens[i].Door)) continue;
                s.DensOut |= 1 << i;
                // Each wolf of a den sets off its own way.
                float heading = (float)wolfRandom.NextDouble() * MathF.Tau;
                for (int n = 0; n < data.WolvesPerDen; n++) LetOut(Dens[i], heading + n * MathF.Tau / data.WolvesPerDen);
            }
            int hours = (int)(sinceDusk / data.HourSeconds);
            if (s.DenHours < hours && RandomUnseenDen() is Den den) { s.DenHours = hours; LetOut(den, (float)wolfRandom.NextDouble() * MathF.Tau); }
        }
        for (int i = s.Wolves.Count - 1; i >= 0; i--) {
            var wolf = s.Wolves[i];
            if (WolvesRetreat && wolf.Mode != WolfMode.Home) GoHome(wolf);
            if (wolf.Target != 0) continue;
            if (wolf.DoorCheck != DoorCheckStage.None) { world.DoorCheck.Tick(wolf, dt); continue; }
            if (world.WolfHunt.RecoverFromHit(wolf, dt)) continue;
            float step = dt * (wolf.Aggressive ? world.WolfHunt.RunSpeed : data.WolfTrot);
            var from = wolf.Pos;
            switch (wolf.Mode) {
                case WolfMode.Patrol:
                    Wander(wolf, wolf.Aggressive ? step : step * PatrolPace);
                    if (!Smell(wolf, data.SmellRadius, wolf.Pos - from)) world.DoorCheck.Smell(wolf);
                    break;
                case WolfMode.Called:
                    // A called wolf still noses the air on the way, so it does not run past a player or a cabin with someone inside.
                    if (!world.Travel(wolf, wolf.Call, step, step) || wolf.Pos.DistanceTo(wolf.Call) < 2) Patrol(wolf, wolf.Heading);
                    if (!Smell(wolf, data.SmellRadius, wolf.Pos - from)) world.DoorCheck.Smell(wolf);
                    break;
                case WolfMode.Home:
                    var door = NearestDen(wolf.Pos).Door;
                    world.Travel(wolf, door, step, step);
                    if (wolf.Pos.DistanceTo(door) < 1) s.Wolves.RemoveAt(i);
                    break;
            }
        }
    }
    private void LetOut(Den den, float heading) => world.S.Wolves.Add(new Wolf { Pos = den.Door, Health = world.Data.WolfHealth, Heading = heading });
    /// <summary>One of the dens whose door no player sees, each as likely as the next, or null when every door is watched.</summary>
    private Den? RandomUnseenDen() {
        Den? pick = null; int unseen = 0;
        foreach (var den in Dens) if (world.Unseen(den.Door) && wolfRandom.Next(++unseen) == 0) pick = den;
        return pick;
    }
    /// <summary>
    /// Walks a patrolling wolf on its heading; ground ahead nobody can stand on, or firelight, turns it a new random way it can take.
    /// A wolf already in firelight, such as one let out of a den beside a camp, may walk on to get out of it.
    /// </summary>
    private void Wander(Wolf wolf, float step) {
        bool lit = world.Protected(wolf.Pos);
        var ahead = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * step;
        for (int tries = 0; !Roams(ahead, lit) && tries < 12; tries++) {
            wolf.Heading = (float)wolfRandom.NextDouble() * MathF.Tau;
            ahead = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * step;
        }
        if (Roams(ahead, lit)) wolf.Pos = ahead;
    }
    private bool Roams(Vector2 p, bool lit) => world.CanStand(p) && (lit || !world.Protected(p));
    /// <summary>Sends a wolf back to patrolling on a heading, hunting nobody.</summary>
    internal static void Patrol(Wolf wolf, float heading) {
        Calm(wolf); wolf.Mode = WolfMode.Patrol; wolf.Heading = heading;
    }
    /// <summary>Calls a wolf off whatever it is doing and sends it to the nearest den.</summary>
    private static void GoHome(Wolf wolf) {
        Calm(wolf); wolf.Mode = WolfMode.Home;
    }
    /// <summary>Clears everything a wolf was doing to the player it hunted.</summary>
    private static void Calm(Wolf wolf) {
        wolf.Target = wolf.Round = 0; wolf.Follow = FollowStage.None;
        wolf.Howl = wolf.HowlIn = wolf.Flee = wolf.Stagger = 0; wolf.Galloping = false;
        wolf.Leap = wolf.Snap = wolf.Recover = wolf.Attack = 0;
        wolf.Threaten = wolf.Circle = 0; WolfCounter.Clear(wolf);
        wolf.Feeding = wolf.Pinning = wolf.Tugging = wolf.Growled = wolf.Sprinted = false; wolf.LungeFrom = Vector2.Zero; wolf.Route.Clear();
        wolf.LetGoOfArm(); wolf.EndDoorCheck();
    }
    /// <summary>
    /// A wolf that smells a player out in the open <paramref name="within"/> metres takes them as its prey, dropping whatever it did at a
    /// cabin's door; returns whether it did.
    /// One that finds them out of its sight first turns to them from the way it <paramref name="went"/>, then howls before it goes after them.
    /// It leaves alone each player it ran from, until it has been out of smell range of that one.
    /// </summary>
    internal bool Smell(Wolf wolf, float within, Vector2 went = default) {
        var (s, data) = (world.S, world.Data);
        for (int i = wolf.Shunned.Count - 1; i >= 0; i--)
            if (!InSmell(wolf, wolf.Shunned[i])) wolf.Shunned.RemoveAt(i);
        PlayerState? prey = null;
        foreach (var p in s.Players)
            if (p.RespawnIn <= 0 && !Doorways.Indoors(p) && !wolf.Shunned.Contains(p.Id) && p.Pos.DistanceTo(wolf.Pos) <= within && (prey == null || p.Pos.DistanceTo(wolf.Pos) < prey.Pos.DistanceTo(wolf.Pos))) prey = p;
        if (prey == null) return false;
        Patrol(wolf, wolf.Heading);
        wolf.Target = prey.Id;
        if (prey.Pos.DistanceTo(wolf.Pos) <= data.WolfSightRange) return true;
        // A wolf that stood still turns as far as one facing away.
        float turn = went == Vector2.Zero ? MathF.PI : Math.Abs(went.AngleTo(prey.Pos - wolf.Pos));
        wolf.HowlIn = HowlTurnSeconds * MathF.Sqrt(turn / MathF.PI);
        if (wolf.HowlIn == 0) Howl(wolf);
        return true;
    }
    /// <summary>Whether the player with an id is still in the game and within smell range of a wolf.</summary>
    private bool InSmell(Wolf wolf, int player) => world.PlayerById(player) is { } p && p.Pos.DistanceTo(wolf.Pos) <= world.Data.SmellRadius;
    /// <summary>
    /// Seconds a wolf takes to turn half a turn where it stands, to face a player it smelled before it howls.
    /// A smaller turn takes the square root of its share of that: a wolf's body comes round fast at first and settles slowly.
    /// </summary>
    public const float HowlTurnSeconds = 1.3f;

    /// <summary>
    /// A wolf that has turned to the player it smelled howls, which calls every patrolling wolf within <see cref="GameData.HowlRadius"/> to where it stands.
    /// The night's first howl is a lone wolf; every later one sounds like the pack answering.
    /// The wolf holds its howl as long as the recording plays.
    /// </summary>
    internal void Howl(Wolf wolf) {
        var (s, data) = (world.S, world.Data);
        bool answer = s.Howled;
        wolf.HowlIn = 0; wolf.Howl = wolf.HowlLength = answer ? data.PackHowlSeconds : data.LoneHowlSeconds;
        world.Play(answer ? "wolf_howl_group" : "wolf_howl", wolf.Pos);
        s.Howled = true;
        foreach (var other in s.Wolves)
            if (other.Target == 0 && other.Mode != WolfMode.Home && other.Pos.DistanceTo(wolf.Pos) <= data.HowlRadius) { other.Mode = WolfMode.Called; other.Call = wolf.Pos; other.Route.Clear(); }
    }
}
