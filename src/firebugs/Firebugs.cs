using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Firebugs in <paramref name="world"/>: they come out after dark away from buildings and are gone at dawn.
/// One hovers about its place, darts off from a player who comes near and then rests a moment where it stopped.
/// A swing of a bug net catches those in its sweep and sends the others near it darting.
/// </summary>
public sealed class Firebugs(World world) {
    /// <summary>Clusters of firebugs that come out as night falls, with one more after each hour of the night.</summary>
    public const int AtNightfall = 8;
    /// <summary>The chance a cluster has three firebugs, or else two or one at even odds.</summary>
    public const float ThreeChance = .25f;
    /// <summary>Metres from a cluster's first firebug within which the others come out.</summary>
    public const float ClusterRadius = 1;
    public const string Item = "firebug";
    private const int LifeDays = 3;
    internal float NewExpiry => world.Time + LifeDays * world.Data.DaySeconds;
    private float nextExpiry = float.PositiveInfinity;
    private readonly List<(int Id, string Name)> changedCrates = new();

    /// <summary>Starts the three-day lifetime of each firebug this inventory can take from a catch.</summary>
    internal int Catch(Inventory to, int count) {
        int room = Math.Min(count, to.Room(Item));
        if (room <= 0) return 0;
        float expiry = NewExpiry;
        Span<float> expires = room <= 64 ? stackalloc float[room] : new float[room];
        expires.Fill(expiry);
        nextExpiry = Math.Min(nextExpiry, expiry);
        return to.AddFirebugs(expires);
    }

    /// <summary>Average game minutes left among the firebugs in one stack, or the whole box when no slot is named.</summary>
    internal int AverageMinutes(Inventory box, int slot = -1) =>
        MinutesLeft(slot < 0 ? box.AverageFirebugExpiry() : box.AverageFirebugExpiry(slot));

    /// <summary>The average of some firebugs' death times, or 0 for none.</summary>
    internal static float Average(ReadOnlySpan<float> expiries) {
        double total = 0;
        foreach (float expiry in expiries) total += expiry;
        return expiries.IsEmpty ? 0 : (float)(total / expiries.Length);
    }

    internal int MinutesLeft(float expiry) => (int)MathF.Ceiling(Math.Max(0, expiry - world.Time) / world.Data.HourSeconds * 60);

    /// <summary>Clears dead bugs from the inventories that can hold them when the next catch expires.</summary>
    internal void Age() {
        float now = world.Time;
        if (now < nextExpiry) return;
        nextExpiry = float.PositiveInfinity;
        changedCrates.Clear();
        var saved = world.S;
        foreach (var (id, box) in saved.Crates) {
            int before = box[Item];
            Check(box);
            if (box[Item] != before) changedCrates.Add((id, World.PackedName(box, id)));
        }
        foreach (var player in saved.Players) Check(player.Backpack);
        foreach (var player in saved.Absent.Values) Check(player.Backpack);
        foreach (var worker in saved.Workers) Check(worker.Bag);
        foreach (var thing in saved.Things) {
            Check(thing.Inventory);
            if (thing.Output is { } output) Check(output);
        }
        foreach (var (id, name) in changedCrates) if (name == "crate") saved.Crates.Remove(id);

        void Check(Inventory box) {
            if (box[Item] > 0) nextExpiry = Math.Min(nextExpiry, box.ExpireFirebugs(now));
            foreach (var (id, name) in changedCrates) box.RenamePackedCrate(id, name);
        }
    }
    /// <summary>Metres a firebug keeps from every building as it comes out.</summary>
    public const float BuildingRoom = 25;
    /// <summary>Metres within which a player sets a firebug darting, unless it rests.</summary>
    public const float StartleRadius = 4;
    /// <summary>Seconds one dart lasts, and seconds a firebug then rests whoever comes near.</summary>
    public const float DartSeconds = 1.2f, RestSeconds = 1.6f;
    /// <summary>Metres a hovering firebug strays from its place at most.</summary>
    public const float HoverRadius = .35f;
    /// <summary>Half the width of a firebug's body, for a net's sweep to touch.</summary>
    public const float BodyRadius = .03f;
    /// <summary>Metres within which a swing that misses a firebug sets it darting, resting or not.</summary>
    public const float ScareRadius = 5;
    /// <summary>How far to either side of straight away from a player a dart can head, in radians.</summary>
    private const float Swerve = .6f;

    /// <summary>Metres a second a firebug darts: halfway between a player's walk and sprint, so only a sprint gains on it.</summary>
    public float DartSpeed => (world.Data.WalkSpeed + world.Data.MoveSpeed(true)) / 2;

    /// <summary>One firebug's flight: the way it darts, the seconds its dart still lasts and the seconds it still rests.</summary>
    private sealed class Flight {
        public Vector2 Heading;
        public float Dart, Rest;
    }

    // Kept only while the world runs: a loaded firebug hovers until a player comes near.
    private readonly Dictionary<int, Flight> flights = new();
    private readonly List<Thing> caught = new();
    private const int Seed = 2203;
    /// <summary>More than the clusters a night lets out, so no two of any nights share a seed.</summary>
    private const int PerNightSeeds = 64;
    // Which way a startled firebug swerves. Seeded, and started again with each world, so a recording of a night plays the same every time.
    private Random swerves = new(Seed);

    internal void Reset() { flights.Clear(); swerves = new Random(Seed); nextExpiry = world.Time; }

    /// <summary>Where a firebug is now: its place and how far its hovering has carried it from there.</summary>
    public Vector2 At(Thing bug) => bug.Pos + Hover(bug.Id, world.Time);

    /// <summary>
    /// How far a hovering firebug is from its place at a time on the world's clock, within <see cref="HoverRadius"/>:
    /// two slow swings each way, at paces of its own.
    /// </summary>
    public static Vector2 Hover(int id, float time) {
        float first = Thing.Variation(id, 11) * MathF.Tau, second = Thing.Variation(id, 12) * MathF.Tau;
        float quick = .5f + Thing.Variation(id, 13) * .4f, slow = .23f + Thing.Variation(id, 14) * .2f;
        var swing = new Vector2(MathF.Sin(time * quick + first) + .5f * MathF.Sin(time * slow * 2.3f + second), MathF.Sin(time * slow + second) + .5f * MathF.Sin(time * quick * 1.7f + first));
        // Each way swings up to 1.5, so the two together reach 1.5 times the square root of 2.
        return swing * (HoverRadius / 2.13f);
    }

    /// <summary>Brings out the night's firebugs as their hours come, flies each one, and clears them all away by day.</summary>
    internal void Tick(float dt) {
        var s = world.S;
        var bugs = s.Things.Of(ThingKind.Firebug);
        if (!world.Night) {
            if (bugs.Count > 0) { s.Things.RemoveAll(ThingKind.Firebug, static _ => true); flights.Clear(); }
            s.FirebugsOut = 0;
            return;
        }
        if (!world.QuietNights) {
            int due = AtNightfall + (int)((world.DayPhase - world.Data.NightStarts) * 24);
            // Each cluster is seeded by the day and its turn, so a night loaded part way lets the rest out where they would have come anyway, and no night repeats another.
            for (; s.FirebugsOut < due; s.FirebugsOut++) LetOut(new Random(Seed + world.Day * PerNightSeeds + s.FirebugsOut));
        }
        foreach (var bug in bugs) Fly(bug, dt);
    }

    /// <summary>
    /// Lets one cluster out at a random spot: three firebugs at <see cref="ThreeChance"/>, otherwise two or one.
    /// The others come out within <see cref="ClusterRadius"/> of the first, seen or not, and one whose spot is not
    /// <see cref="Free"/> is left out.
    /// </summary>
    private void LetOut(Random random) {
        if (world.UnseenSpot(random, Free) is not Vector2 first) return;
        world.Add(ThingKind.Firebug, first, 1);
        int size = random.NextDouble() < ThreeChance ? 3 : random.Next(1, 3);
        for (int i = 1; i < size; i++) {
            // The square root spreads them evenly over the circle instead of crowding its middle.
            var spot = first + Vector2.Right.Rotated((float)random.NextDouble() * MathF.Tau) * ClusterRadius * MathF.Sqrt((float)random.NextDouble());
            if (Free(spot)) world.Add(ThingKind.Firebug, spot, 1);
        }
    }

    /// <summary>Lets no more firebugs out tonight; those already out stay.</summary>
    internal void KeepIn() => world.S.FirebugsOut = AtNightfall + 24;

    /// <summary>Whether a firebug may come out over a spot: ground to stand on, away from every building.</summary>
    private bool Free(Vector2 p) => world.CanStand(p) && !NearBuilding(p);

    private bool NearBuilding(Vector2 p) {
        foreach (var kind in world.Data.Buildings.Keys)
            foreach (var t in world.S.Things.Of(kind))
                if (t.Pos.DistanceTo(p) < BuildingRoom) return true;
        return false;
    }

    private void Fly(Thing bug, float dt) {
        var flight = FlightOf(bug);
        if (flight.Dart > 0) {
            var step = flight.Heading * DartSpeed * Math.Min(dt, flight.Dart);
            // It turns back from water, cliffs and the valley's edge.
            if (World.Open(bug.Pos + step)) bug.Pos += step; else flight.Heading = -flight.Heading;
            flight.Dart -= dt;
            if (flight.Dart <= 0) flight.Rest = RestSeconds;
            return;
        }
        if (flight.Rest > 0) { flight.Rest -= dt; return; }
        if (Nearest(At(bug), StartleRadius) is Vector2 player) Startle(bug, flight, player);
    }

    private Flight FlightOf(Thing bug) {
        if (!flights.TryGetValue(bug.Id, out var flight)) flights[bug.Id] = flight = new Flight();
        return flight;
    }

    /// <summary>Where the nearest player out in the open within <paramref name="radius"/> metres of a spot stands, or null.</summary>
    private Vector2? Nearest(Vector2 spot, float radius) {
        Vector2? nearest = null;
        float best = radius * radius;
        foreach (var player in world.S.Players) {
            if (player.RespawnIn > 0 || player.Sheltered != 0) continue;
            float distance = player.Pos.DistanceSquaredTo(spot);
            if (distance <= best) { best = distance; nearest = player.Pos; }
        }
        return nearest;
    }

    /// <summary>Sets a firebug darting away from a spot, a little to one side, whether it rests or not.</summary>
    private void Startle(Thing bug, Flight flight, Vector2 from) {
        var away = At(bug) - from;
        flight.Heading = (away.LengthSquared() > .0001f ? away.Normalized() : Vector2.Right).Rotated(((float)swerves.NextDouble() * 2 - 1) * Swerve);
        flight.Dart = DartSeconds; flight.Rest = 0;
    }

    /// <summary>
    /// Lands the acting player's net swing along <paramref name="aim"/>, a unit vector: every firebug in the sweep goes
    /// into their backpack while it has room, and every other one within <see cref="ScareRadius"/> darts off.
    /// </summary>
    internal void Sweep(Vector2 aim) {
        var s = world.S;
        caught.Clear();
        foreach (var bug in s.Things.Of(ThingKind.Firebug)) {
            var at = At(bug);
            if (Swings.Net.Touches(world.Player, aim, at, BodyRadius, 0)) caught.Add(bug);
            else if (at.DistanceTo(world.Player) <= ScareRadius) Startle(bug, FlightOf(bug), world.Player);
        }
        foreach (var bug in caught) {
            if (Catch(s.Backpack, 1) == 0) { world.Say("Backpack full. Store or sell some supplies."); Startle(bug, FlightOf(bug), world.Player); continue; }
            s.Things.Remove(bug); flights.Remove(bug.Id); world.Got(World.ResourceItem(bug));
        }
    }
}
