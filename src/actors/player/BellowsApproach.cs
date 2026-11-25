using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Walks the player to a selected furnace's bellows and presses once on arrival.</summary>
internal sealed class BellowsApproach(World world, UiKit kit) {
    private const float Arrival = .035f;
    private const int TilesPerFrame = 256;
    private readonly WalkSearch search = new(world);
    private readonly List<Vector2> route = new();
    private int furnace, next, warp;
    private bool searching, retried;

    internal bool Active => furnace != 0;

    internal void Start(Thing forge) {
        if (furnace == forge.Id) return;
        Cancel();
        furnace = forge.Id;
        warp = world.Me.Warp;
    }

    internal void Cancel() {
        furnace = 0;
        route.Clear();
        searching = retried = false;
    }

    internal Vector2 Direction(Vector2 input, int selected) {
        if (input != Vector2.Zero || world.Occupied || world.Sheltered != 0 || selected != furnace
            || warp != world.Me.Warp || world.Find(furnace) is not Thing forge) {
            Cancel();
            return input;
        }
        if (route.Count == 0) {
            next = 0;
            var found = searching ? WalkSearch.Result.Searching
                : search.Begin(world.Player, forge.ToWorld(World.BellowsStand), route, exact: true, wholeWalk: true);
            if (found == WalkSearch.Result.Searching) found = search.Continue(route, TilesPerFrame);
            searching = found == WalkSearch.Result.Searching;
            if (searching) return Vector2.Zero;
            if (found == WalkSearch.Result.Blocked) { NoPath(); return Vector2.Zero; }
        }
        while (next < route.Count && world.Player.DistanceSquaredTo(route[next]) < Arrival * Arrival) next++;
        if (next < route.Count) return world.Player.DirectionTo(route[next]);
        Cancel();
        world.Pump(forge);
        return Vector2.Zero;
    }

    internal void Move(float distance) {
        if (distance <= 0) return;
        var from = world.Player;
        world.MovePlayer(from.MoveToward(route[next], distance) - from);
        if (world.Player != from) retried = false;
        else if (retried) NoPath();
        else { route.Clear(); retried = true; }
    }

    private void NoPath() { Cancel(); kit.Notify("No clear path to the furnace bellows."); }
}
