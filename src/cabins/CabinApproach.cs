using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The player's cancellable walk from open ground to the start of a cabin's doorway animation.</summary>
internal sealed class CabinApproach(World world) {
    private const float Arrival = .035f;
    private const int TilesPerFrame = 256;
    private readonly WalkSearch search = new(world);
    private readonly List<Vector2> route = new();
    private int cabin, next, warp;
    private bool searching, retriedBlockedStep;

    public bool Active => cabin != 0;
    public bool GoesTo(Thing hut) => cabin == hut.Id;

    public void Start(Thing hut) {
        if (world.Downed || world.Sheltered != 0 || GoesTo(hut)) return;
        cabin = hut.Id;
        warp = world.Me.Warp;
        route.Clear();
        searching = retriedBlockedStep = false;
    }

    public bool Cancel() {
        if (!Active) return false;
        cabin = 0;
        route.Clear();
        return true;
    }

    public Vector2 Direction() {
        if (world.Downed || world.Sheltered != 0 || warp != world.Me.Warp || world.Find(cabin) is not Thing hut) {
            Cancel();
            return Vector2.Zero;
        }
        if (world.Door != Doorway.None) return Vector2.Zero;
        if (route.Count == 0) {
            next = 0;
            bool resumed = searching;
            var result = resumed ? WalkSearch.Result.Searching
                : search.Begin(world.Player, Doorways.DoorPoint(hut), route, exact: true, wholeWalk: true);
            if (result == WalkSearch.Result.Searching) result = search.Continue(route, TilesPerFrame);
            searching = result == WalkSearch.Result.Searching;
            if (searching) return Vector2.Zero;
            if (result == WalkSearch.Result.Blocked) {
                if (!resumed || !search.LayoutChanged()) NoPath();
                return Vector2.Zero;
            }
        }
        while (next < route.Count && world.Player.DistanceSquaredTo(route[next]) < Arrival * Arrival) next++;
        if (next < route.Count) return world.Player.DirectionTo(route[next]);
        world.Doorways.Enter(hut, world.Me.Sprinting);
        Cancel();
        return Vector2.Zero;
    }

    public void Move(float distance) {
        if (distance <= 0) return;
        var position = world.Player.MoveToward(route[next], distance);
        if (world.CanStand(position)) {
            world.MovePlayer(position - world.Player);
            retriedBlockedStep = false;
        } else if (retriedBlockedStep) NoPath();
        else {
            route.Clear();
            retriedBlockedStep = true;
        }
    }

    private void NoPath() { Cancel(); world.Say("No clear path to the cabin door."); }
}
