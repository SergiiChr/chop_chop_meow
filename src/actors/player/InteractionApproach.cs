using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Uses a selected thing now or walks to its working spot and uses it once on arrival.</summary>
internal sealed class InteractionApproach(World world, UiKit kit) {
    private const float Arrival = .015f;
    private const int TilesPerFrame = 256;
    private readonly WalkSearch search = new(world);
    private readonly List<Vector2> route = new();
    private readonly record struct Use(int Id, Vector2 Stand, Action<Thing> Act, string Blocked);
    private Use? pending;
    private int next, warp;
    private bool searching, retried;

    internal bool Active => pending != null;

    internal void Start(Thing thing, Vector2 stand, bool ready, Action<Thing> act, string blocked) {
        if (world.Occupied || world.Sheltered != 0) return;
        if (ready || world.Player.DistanceSquaredTo(stand) <= Arrival * Arrival) {
            Cancel();
            act(thing);
            return;
        }
        var use = new Use(thing.Id, stand, act, blocked);
        if (pending == use) return;
        Cancel();
        pending = use;
        warp = world.Me.Warp;
    }

    internal void Cancel() {
        pending = null;
        route.Clear();
        searching = retried = false;
    }

    internal Vector2 Direction(Vector2 input, int selected) {
        if (pending is not Use use) return input;
        if (input != Vector2.Zero || world.Occupied || world.Sheltered != 0 || selected != use.Id
            || warp != world.Me.Warp || world.Find(use.Id) is not Thing thing) {
            Cancel();
            return input;
        }
        if (route.Count == 0) {
            next = 0;
            var found = searching ? WalkSearch.Result.Searching
                : search.Begin(world.Player, use.Stand, route, exact: true, wholeWalk: true);
            if (found == WalkSearch.Result.Searching) found = search.Continue(route, TilesPerFrame);
            searching = found == WalkSearch.Result.Searching;
            if (searching) return Vector2.Zero;
            if (found == WalkSearch.Result.Blocked) { NoPath(); return Vector2.Zero; }
        }
        while (next < route.Count && world.Player.DistanceSquaredTo(route[next]) <= Arrival * Arrival) next++;
        if (next < route.Count) return world.Player.DirectionTo(route[next]);
        Cancel();
        use.Act(thing);
        return Vector2.Zero;
    }

    internal void Move(float distance) {
        if (distance <= 0 || next >= route.Count) return;
        var from = world.Player;
        world.MovePlayer(from.MoveToward(route[next], distance) - from);
        if (world.Player != from) retried = false;
        else if (retried) NoPath();
        else { route.Clear(); retried = true; }
    }

    private void NoPath() {
        if (pending is Use use) kit.Notify(use.Blocked);
        Cancel();
    }
}
