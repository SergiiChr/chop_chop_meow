using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Reserves a seat for each worker at lunch, around a campfire or in a small circle on open ground.</summary>
internal sealed class LunchBreak(World world) {
    internal const int FireSeats = 6;
    private const int GroupSeats = 3;
    private const float FireRadius = 1.4f, GroupRadius = .8f, SeatGap = .9f;
    private const int GroundSearch = 8;

    private readonly record struct Seat(Vector2 Position, Vector2 Center, Thing? Fire = null);
    private readonly Dictionary<int, Seat> reserved = new();
    private readonly List<Worker> waiting = new();
    private readonly List<Seat> fires = new();
    private readonly HashSet<(int Worker, Vector2 Seat)> unreachable = new();
    private int fireCount;

    public void Reset() { reserved.Clear(); unreachable.Clear(); }

    private static bool OnBreak(Worker w) => !w.Home && w.Away <= 0 && w.Commute == 0 && w.WageDebt <= 0;

    /// <summary>Keeps seats fixed while workers walk to them and as long as the same crew and fires are available.</summary>
    public void Prepare() {
        if (!world.Lunch) { Reset(); return; }
        var campfires = world.ThingsOf(ThingKind.Campfire);
        int count = 0, assigned = 0;
        bool firesChanged = fireCount != campfires.Count;
        foreach (var w in world.Workers) {
            if (!OnBreak(w)) continue;
            count++;
            if (!reserved.TryGetValue(w.Id, out var seat)) continue;
            assigned++;
            firesChanged |= seat.Fire is Thing fire && world.Find(fire.Id) != fire;
        }
        if (firesChanged || assigned != reserved.Count) {
            Reset();
            fireCount = campfires.Count;
            assigned = 0;
        }
        if (assigned == count) return;
        waiting.Clear();
        // A newly free fire seat is also available to the crew on the ground.
        foreach (var w in world.Workers) {
            if (!OnBreak(w) || reserved.TryGetValue(w.Id, out var seat) && seat.Fire != null) continue;
            reserved.Remove(w.Id);
            waiting.Add(w);
        }
        fires.Clear();
        foreach (var fire in campfires) {
            for (int i = 0; i < FireSeats; i++) {
                var position = Around(fire.Pos, FireRadius, i, FireSeats);
                if (RoomForSeat(position)) fires.Add(new Seat(position, fire.Pos, fire));
            }
        }
        ReserveFires();
        while (waiting.Count > 0) ReserveGroup();
    }

    private void ReserveFires() {
        while (waiting.Count > 0 && fires.Count > 0) {
            int worker = -1, seat = 0;
            float nearestFire = float.MaxValue, nearestSeat = float.MaxValue;
            for (int i = 0; i < waiting.Count; i++) {
                var from = waiting[i].Pos;
                for (int j = 0; j < fires.Count; j++) {
                    var option = fires[j];
                    if (unreachable.Contains((waiting[i].Id, option.Position))) continue;
                    float fireDistance = from.DistanceSquaredTo(option.Center), seatDistance = from.DistanceSquaredTo(option.Position);
                    if (fireDistance > nearestFire || fireDistance == nearestFire && seatDistance >= nearestSeat) continue;
                    worker = i; seat = j; nearestFire = fireDistance; nearestSeat = seatDistance;
                }
            }
            if (worker < 0) return;
            var chosen = fires[seat];
            Reserve(worker, chosen);
            for (int j = fires.Count - 1; j >= 0; j--)
                if (fires[j].Position.DistanceSquaredTo(chosen.Position) < SeatGap * SeatGap) fires.RemoveAt(j);
        }
    }

    private void ReserveGroup() {
        var anchor = waiting[0].Pos;
        int count = Math.Min(GroupSeats, waiting.Count);
        for (int radius = 0; radius <= GroundSearch; radius++) {
            for (int y = -radius; y <= radius; y++) {
                for (int x = -radius; x <= radius; x++) {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                    var center = anchor + new Vector2(x, y);
                    if (!RoomForGroup(center, count)) continue;
                    Reserve(0, new Seat(Around(center, GroupRadius, 0, count), center));
                    for (int i = 1; i < count; i++) {
                        int nearest = 0;
                        for (int j = 1; j < waiting.Count; j++)
                            if (waiting[j].Pos.DistanceSquaredTo(anchor) < waiting[nearest].Pos.DistanceSquaredTo(anchor)) nearest = j;
                        Reserve(nearest, new Seat(Around(center, GroupRadius, i, count), center));
                    }
                    return;
                }
            }
        }
        Reserve(0, new Seat(anchor, anchor));
    }

    private bool RoomForGroup(Vector2 center, int count) {
        if (!world.CanStand(center)) return false;
        foreach (var fire in world.ThingsOf(ThingKind.Campfire))
            if (center.DistanceSquaredTo(fire.Pos) < 2.8f * 2.8f) return false;
        for (int i = 0; i < count; i++) if (!RoomForSeat(Around(center, GroupRadius, i, count))) return false;
        return true;
    }

    private bool RoomForSeat(Vector2 position) {
        if (!world.CanStand(position)) return false;
        foreach (var seat in reserved.Values)
            if (position.DistanceSquaredTo(seat.Position) < SeatGap * SeatGap) return false;
        return true;
    }

    private static Vector2 Around(Vector2 center, float radius, int i, int count) =>
        center + Vector2.FromAngle(i * Mathf.Tau / count) * radius;

    private void Reserve(int index, Seat seat) {
        reserved.Add(waiting[index].Id, seat);
        waiting.RemoveAt(index);
    }

    public void Sit(Worker w, float dt) {
        if (!reserved.TryGetValue(w.Id, out var seat)) return;
        if (seat.Center != seat.Position && !world.CanStand(seat.Position)) { reserved.Remove(w.Id); return; }
        w.Status = seat.Fire != null ? "Lunch break by the campfire" : "Lunch break";
        if (!world.Walk(w, seat.Position, dt, .08f, exact: true)) {
            if (w.WorkState != WorkerWorkState.Blocked) return;
            if (seat.Fire != null) {
                unreachable.Add((w.Id, seat.Position));
                reserved.Remove(w.Id);
                return;
            }
            // A worker cut off from their group can still sit on the ground where they stopped.
            seat = new Seat(w.Pos, w.Pos);
            reserved[w.Id] = seat;
            w.Status = "Lunch break";
        }
        w.LunchHeading = (seat.Center - seat.Position).Angle();
        w.WorkState = seat.Fire != null && w.Id % 2 == 0 ? WorkerWorkState.Perching : WorkerWorkState.Resting;
    }
}
