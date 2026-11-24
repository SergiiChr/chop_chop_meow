using System;

namespace ChopChopMeow;

/// <summary>
/// The Gather job: a worker gathers at a resource spot at the player's pace and carries the load to storage.
/// A place with its own way of gathering, such as the lake, extends it.
/// </summary>
internal class Gathering {
    protected readonly World world;

    public Gathering(World world) => this.world = world;

    /// <summary>
    /// Gathers at a spot until the worker's hands are full, going on to the next known spot of the job when one runs out.
    /// Full hands, or no spot left, send the load off.
    /// </summary>
    public virtual bool Work(Worker w, Duty duty, Thing? spot, float dt) {
        if (spot == null || spot.Remaining <= 0 || TreePlanting.IsYoung(spot)) {
            if (duty.Kind is not ThingKind gone || !world.NextSpot(w, duty.Thing, gone, spot?.Pos ?? w.Pos)) return false;
            spot = world.Find(duty.Thing)!;
        }
        string? blocked = world.GatherBlock(spot, true);
        if (blocked != null) { w.Status = blocked; return false; }
        string item = World.ResourceItem(spot);
        if (world.DepositFor(item, spot.Pos) == null) { w.Status = "Needs an accepting storage chest"; return false; }
        // A gatherer also fetches a tool that only speeds the work up, such as a rake, while a tool station has one.
        var rule = GatherRules.Of(spot.Kind);
        string? need = rule?.Needs ?? (rule?.Helps is string helping && world.HasTool(w, helping) ? helping : null);
        if (need == null) { if (!world.HandsFree(w, dt)) return true; } else
            switch (world.Equip(w, need, spot, dt)) {
                case World.Kit.Busy: return true;
                // With no tool to be had, the nearest spot of the job that needs none, or one that is to be had, is worked instead.
                // A wrong tool with no room to go back to stays in hand, and the job waits at its spot.
                case World.Kit.Lacking: return w.Tool == "" && world.OtherSpot(w, spot);
            }
        w.Status = world.Phrase("Gathering ", world.Data.Name(item));
        if (!world.Walk(w, spot.Pos, dt)) return true;
        w.WorkState = WorkerWorkState.Gathering; w.Exhausted = null;
        string? tool = w.Tool == "" ? null : w.Tool;
        if (spot.IsDug) {
            // What is dug up comes out whole; what does not fit in hand waits in the hole for the next load.
            spot.Progress += dt * world.Speed(tool);
            if (!World.DugOut(spot)) return true;
            world.Unearth(spot, w.Bag);
        } else {
            w.Timer += dt;
            if (!spot.IsLoose && w.Timer < world.GatherTime(spot.Kind, tool)) return true;
            w.Timer = 0;
            int took = w.Bag.Add(item, spot.IsLoose ? spot.Remaining : Math.Min(spot.Remaining, World.GatherYield(spot.Kind))); spot.Remaining -= took;
            if (took > 0) { world.Got(item); world.Chip(spot, tool); }
        }
        w.Loading = w.Bag.Room(item) > 0;
        if (spot.Remaining > 0) return true;
        // Whoever fells a tree digs its stump up next while a shovel is to be had, and otherwise goes on to the next tree.
        if (spot.Kind == ThingKind.Tree) {
            var stump = world.Fell(spot, w.Pos);
            if (world.CanWork(w, stump.Kind)) { World.MoveTo(w, spot.Id, stump); return true; }
        }
        if (spot.IsLoose) world.S.Things.Remove(spot);
        if (!world.NextSpot(w, spot.Id, spot.Kind, spot.Pos)) w.Loading = false;
        return true;
    }
}
