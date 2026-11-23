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
    private readonly Dictionary<Worker, Seat> reserved = new();
    private readonly List<Worker> waiting = new();
    private readonly List<Seat> fires = new();
    private readonly HashSet<(int Worker, Vector2 Seat)> unreachable = new();
    private readonly HashSet<Worker> cutOffFromGroup = new();
    private readonly List<Worker> leaving = new();
    private int fireCount;

    public void Reset() { reserved.Clear(); unreachable.Clear(); cutOffFromGroup.Clear(); fireCount = 0; }

    private static bool OnBreak(Worker w) => !w.Home && w.Away <= 0 && w.Commute == 0 && w.WageDebt <= 0;

    /// <summary>Keeps each worker's seat until they leave lunch, lose the seat or take a place by a fire.</summary>
    public void Prepare() {
        if (!world.Lunch) { Reset(); return; }
        var campfires = world.ThingsOf(ThingKind.Campfire);
        var crew = world.Workers;
        int count = 0, assigned = 0;
        foreach (var w in crew) {
            if (!OnBreak(w)) continue;
            count++;
            if (reserved.TryGetValue(w, out var seat) && FireStillThere(seat)) assigned++;
        }
        if (assigned == count && assigned == reserved.Count && fireCount == campfires.Count) return;
        if (assigned != reserved.Count) {
            leaving.Clear();
            foreach (var (worker, seat) in reserved)
                if (!OnBreak(worker) || !crew.Contains(worker) || !FireStillThere(seat)) leaving.Add(worker);
            foreach (var worker in leaving) reserved.Remove(worker);
        }
        fireCount = campfires.Count;
        waiting.Clear();
        foreach (var w in crew) if (OnBreak(w) && !reserved.ContainsKey(w)) waiting.Add(w);
        fires.Clear();
        foreach (var fire in campfires) {
            for (int i = 0; i < FireSeats; i++) {
                var position = Around(fire.Pos, FireRadius, i, FireSeats);
                if (RoomForSeat(position)) fires.Add(new Seat(position, fire.Pos, fire));
            }
        }
        if (fires.Count > 0) {
            // Keep ground reservations until a worker actually takes a fire seat.
            foreach (var w in crew) if (reserved.TryGetValue(w, out var seat) && seat.Fire == null) waiting.Add(w);
            ReserveFires();
            for (int i = waiting.Count - 1; i >= 0; i--) if (reserved.ContainsKey(waiting[i])) waiting.RemoveAt(i);
        }
        for (int i = waiting.Count - 1; i >= 0; i--) {
            var w = waiting[i];
            if (!cutOffFromGroup.Contains(w)) continue;
            ReserveWhereStanding(w);
            waiting.RemoveAt(i);
        }
        while (waiting.Count > 0) ReserveGroup();
    }

    private bool FireStillThere(Seat seat) => seat.Fire == null || world.Find(seat.Fire.Id) == seat.Fire;

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
        if (!ReserveWhereStanding(waiting[0])) cutOffFromGroup.Add(waiting[0]);
        waiting.RemoveAt(0);
    }

    private bool ReserveWhereStanding(Worker w) {
        if (!RoomForSeat(w.Pos)) return false;
        reserved.Add(w, new Seat(w.Pos, w.Pos));
        return true;
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
        reserved[waiting[index]] = seat;
        waiting.RemoveAt(index);
    }

    public void Sit(Worker w, float dt) {
        if (!reserved.TryGetValue(w, out var seat)) { WaitForSpace(w, dt); return; }
        if (!world.CanStand(seat.Position)) { reserved.Remove(w); return; }
        w.Status = seat.Fire != null ? "Lunch break by the campfire" : "Lunch break";
        if (!world.Walk(w, seat.Position, dt, .08f, exact: true)) {
            if (w.WorkState != WorkerWorkState.Blocked) return;
            reserved.Remove(w);
            if (seat.Fire != null) { unreachable.Add((w.Id, seat.Position)); return; }
            cutOffFromGroup.Add(w);
            if (!ReserveWhereStanding(w)) { WaitForSpace(w, dt); return; }
            seat = reserved[w];
            w.Status = "Lunch break";
        }
        w.Pos = seat.Position;
        w.LunchHeading = (seat.Center - seat.Position).Angle();
        w.WorkState = seat.Fire != null && w.Id % 2 == 0 ? WorkerWorkState.Perching : WorkerWorkState.Resting;
    }

    // A worker waiting on their feet can move away from an occupied seat without moving the worker in it.
    private void WaitForSpace(Worker w, float dt) {
        w.Status = "Lunch break";
        foreach (var seat in reserved.Values) {
            var away = w.Pos - seat.Position;
            if (away.LengthSquared() >= SeatGap * SeatGap) continue;
            var heading = away == Vector2.Zero ? Vector2.Right : away.Normalized();
            w.Pos = world.Slide(w.Pos, heading * Math.Min(dt, .08f));
            return;
        }
    }
}
