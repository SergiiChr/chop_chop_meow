using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// How a worker goes through their jobs.
/// The list is read from the top: the first job that can be done is done for one round, then the list is read again.
/// A round is a delivery, a crafted batch or another unit of the job's work.
/// </summary>
public sealed partial class World {
    private void WorkJobs(Worker w, float dt) {
        if (w.Bag.Count > 0 && !w.Loading) { Deliver(w, dt); return; }
        if (w.Engaged) {
            if (JobInHand(w) is Duty doing && Rounds(doing) == w.Mark && Do(w, doing, dt)) return;
            w.Engaged = false;
        }
        w.Plan = default;
        // A gatherer with no spot left carries off what they picked so far.
        if (w.Loading) {
            w.Loading = false;
            if (w.Bag.Count > 0) { Deliver(w, dt); return; }
        }
        string? lacking = null;
        for (int i = 0; i < w.Duties.Count; i++) {
            var duty = w.Duties[i];
            // The hauls of a haul group are done through the group's own entry.
            if (Grouped(duty)) continue;
            if (w.Assignment != duty.Thing || w.Job != duty.Job) { w.Assignment = duty.Thing; w.Job = duty.Job; w.Timer = 0; }
            if (Do(w, duty, dt)) { w.Engaged = true; w.Mark = Rounds(duty); return; }
            lacking ??= w.Status;
        }
        Idle(w, lacking, dt);
    }

    /// <summary>The job a worker has in hand, or null when it has left their list.</summary>
    private static Duty? JobInHand(Worker w) {
        foreach (var d in w.Duties) if (d.Thing == w.Assignment && d.Job == w.Job) return d;
        return null;
    }

    /// <summary>The rounds a job's place has counted, for the jobs whose round is a unit of the place's work; a delivery ends the round of the others.</summary>
    private int Rounds(Duty duty) => duty.Job is "craft" or "bellows" or "farm" or "build" or "mine" && Find(duty.Thing) is Thing place ? place.Rounds : 0;

    /// <summary>Does one of a worker's jobs for a tick and tells whether there was anything to do; with nothing, the status says what the job lacks.</summary>
    private bool Do(Worker w, Duty duty, float dt) {
        var place = Find(duty.Thing);
        if (duty.Job == "gather") return place?.Kind == ThingKind.Water ? WaterWork(w, place, dt) : Gather(w, duty, place, dt);
        if (duty.Job == HaulGroup) return Haul(w, duty, dt);
        if (place == null) return false;
        return duty.Job switch {
            "haul" => Haul(w, duty, dt),
            "craft" => place.Kind == ThingKind.Packer ? PackWork(w, place, dt) : Craft(w, place, dt),
            "bellows" => Pump(w, place, dt),
            "farm" => Reap(w, place, dt),
            "build" => BuildSite(w, place, dt),
            "mine" => Dig(w, place, dt),
            "deliveries" => ManageDeliveries(w, place, dt),
            "fuel" => TendFire(w, place, dt),
            "stock" => StockTools(w, place, dt),
            "tend" => TendCompost(w, place, dt),
            _ => false
        };
    }

    /// <summary>
    /// With no job to do, a worker waits at the nearest trading post; their status says what their first job lacks.
    /// A tool none of their jobs uses goes back to a tool station first.
    /// </summary>
    private void Idle(Worker w, string? lacking, float dt) {
        w.Assignment = w.Duties.Count > 0 ? w.Duties[0].Thing : 0; w.Job = "";
        if (w.Tool != "" && !JobUses(w, Data.Items[w.Tool].Tool) && !ReturnTool(w, dt)) return;
        WaitAtPost(w, dt);
        if (w.WorkState != WorkerWorkState.Blocked) w.Status = lacking ?? "Available";
    }

    /// <summary>Whether one of a worker's jobs uses a kind of tool: building or working a mine, or gathering what needs it or goes faster with it.</summary>
    private bool JobUses(Worker w, string kind) {
        foreach (var d in w.Duties) {
            if (d.Job == "mine" && kind == "pickaxe" || d.Job == "build" && kind is "pickaxe" or "hammer") return true;
            if (d.Job != "gather") continue;
            // A woodcutter's job covers trees and stumps, so the spot it is on now counts as well as the kind it was given.
            if (Uses(d.Kind) || Uses(Find(d.Thing)?.Kind)) return true;
        }
        return false;

        bool Uses(ThingKind? spot) => spot is ThingKind known && GatherRules.Of(known) is GatherRule rule && (rule.Needs == kind || rule.Helps == kind);
    }

    /// <summary>
    /// Carries what a worker holds to where it goes: the place it was fetched for while that still takes it,
    /// else the nearest station that uses it or storage asking for it, else open storage.
    /// Emptied hands end the round.
    /// </summary>
    private void Deliver(Worker w, float dt) {
        string item = w.Bag.First()!;
        var dest = Find(w.Destination);
        if (FillsLeftover(w, dest, item, dt)) return;
        // Rules are read again every tick, so a filter changed or a slot filled while a load is on its way sends it elsewhere.
        var into = dest == null ? null : Into(dest, item, w.Fueling);
        if (into == null) {
            dest = DepositFor(item, w.Pos); w.Fueling = false;
            into = dest == null ? null : Into(dest, item, false);
        }
        if (dest == null || into == null) { w.Status = Phrase("Holding ", Data.Name(item), " · no accepting storage"); return; }
        w.Destination = dest.Id; w.Status = Phrase("Carrying ", Data.Name(item), " → ", Data.Name(dest.Kind));
        if (!Walk(w, dest.Pos, dt)) return;
        // Every vessel carried pours, whatever it holds; the vessels stay in hand, emptier.
        if (PoursInto(dest, into, item) is VesselDef pours) PourLiters(w.Bag, into, pours, InputRoom(dest, item));
        else if (FuelSplit(dest, item, w.Bag[item]) is (int fuel, int input)) { w.Bag.TransferTo(dest.Burner!, item, fuel, true); w.Bag.TransferTo(dest.Inventory, item, input, true); } else w.Bag.TransferTo(into, item, Math.Min(w.Bag[item], RoomFor(dest, into, item)), true);
        w.Destination = 0;
        if (w.Bag.Count == 0) { w.Fueling = false; w.Engaged = false; }
    }

    /// <summary>
    /// The box of a place that takes a carried item now, or null: a station's input slot or fuel slot,
    /// a mine site or a farm plot still short of the item, or storage that holds the item and has room.
    /// </summary>
    /// <param name="fueling">The load was fetched for the fuel slot, so it goes there even when the recipe also takes the item as an input.</param>
    private Inventory? Into(Thing dest, string item, bool fueling) {
        if (IsStation(dest.Kind)) {
            if (!fueling && InputRoom(dest, item) > 0) return dest.Inventory;
            return dest.Burner is Inventory burner && Fuels(dest).Contains(item) && FuelTakes(dest, item) && burner.Room(item) > 0 ? burner : null;
        }
        if (dest.Kind == ThingKind.Mine) return MineNeed(dest, item) > 0 ? dest.Inventory : null;
        if (dest.Kind == ThingKind.Farm) return SowRoom(dest, item) > 0 ? dest.Inventory : null;
        return dest.Inventory.Room(item) > 0 && dest.Inventory.Accepts(item) && Holds(dest, item) ? dest.Inventory : null;
    }

    /// <summary>
    /// How a load of an item that a station's recipe takes both as an input and as fuel is split between the two slots:
    /// in proportion to what a batch uses of each, as far as each has room.
    /// Null when the item goes into one slot only.
    /// </summary>
    private (int Fuel, int Input)? FuelSplit(Thing t, string item, int load) {
        if (!IsStation(t.Kind) || t.Burner is not Inventory burner || !BatchCost(t).TryGetValue(item, out int batch) || !Fuels(t).Contains(item) || !FuelTakes(t, item)) return null;
        int fuelRoom = burner.Room(item);
        // A batch burns one fuel, so of every batch's worth one unit is fuel.
        int input = Math.Min(load - Math.Min((load + batch) / (batch + 1), fuelRoom), InputRoom(t, item));
        return (Math.Min(load - input, fuelRoom), input);
    }

    /// <summary>How many of a carried item a place's box takes at most: an input slot up to its stack, a mine site or a farm plot what it is short of, storage whatever fits.</summary>
    private int RoomFor(Thing dest, Inventory into, string item) =>
        dest.Kind == ThingKind.Mine ? MineNeed(dest, item) : dest.Kind == ThingKind.Farm ? SowRoom(dest, item) : IsStation(dest.Kind) && into == dest.Inventory ? InputRoom(dest, item) : int.MaxValue;

    /// <summary>
    /// How many more of an item a station's input slot takes: up to a stack of what the recipe it is on uses.
    /// A vessel counts as the liters of room for the liquid it pours.
    /// </summary>
    private int InputRoom(Thing t, string item) {
        string lands = PoursInto(t, t.Inventory, item)?.Liquid ?? item;
        if (Data.VesselFor(lands) != null && lands == item || !BatchCost(t).ContainsKey(lands)) return 0;
        return Math.Max(0, SlotRoom(t.Inventory, lands));
    }

    private bool IsStation(ThingKind kind) => Array.IndexOf(stationKinds, kind) >= 0;

    /// <summary>
    /// Whether a worker's hands are clear of a tool the job in hand has no use for.
    /// They walk it back to a tool station first; with no station to take it they keep it and carry on.
    /// </summary>
    private bool HandsFree(Worker w, float dt) {
        w.Missing = "";
        return w.Tool == "" || ReturnTool(w, dt);
    }

    private enum Kit { Ready, Busy, Lacking }

    /// <summary>Gets a worker the tool a job needs: puts back one of another kind, then takes the best from a tool station; tells whether they hold it, are on the way, or have none to take.</summary>
    private Kit Equip(Worker w, string need, Thing target, float dt) {
        if (w.Tool != "" && Data.Items[w.Tool].Tool != need) {
            if (!ReturnTool(w, dt)) return Kit.Busy;
            // With no room in a tool station the tool stays in hand, and a job that needs another kind waits for room.
            if (w.Tool != "") { w.Status = Phrase("No room to put back the ", Lower(Data.Name(w.Tool))); return Kit.Lacking; }
        }
        if (w.Tool != "") return Kit.Ready;
        FetchTool(w, need, target, dt);
        return w.Missing != "" ? Kit.Lacking : Kit.Busy;
    }

    /// <summary>How many pieces one gather cycle yields, for the player and a worker alike.</summary>
    private static int GatherYield(ThingKind kind) => kind == ThingKind.WolfCorpse ? 2 : 4;

    /// <summary>
    /// Gathers at a spot at the player's pace until the worker's hands are full, going on to the next known spot of the job when one runs out.
    /// Full hands, or no spot left, send the load off.
    /// </summary>
    private bool Gather(Worker w, Duty duty, Thing? spot, float dt) {
        if (spot == null || spot.Remaining <= 0) {
            if (duty.Kind is not ThingKind gone || !NextSpot(w, duty.Thing, gone, spot?.Pos ?? w.Pos)) return false;
            spot = Find(duty.Thing)!;
        }
        string? blocked = GatherBlock(spot, true);
        if (blocked != null) { w.Status = blocked; return false; }
        string item = ResourceItem(spot);
        if (DepositFor(item, spot.Pos) == null) { w.Status = "Needs an accepting storage chest"; return false; }
        // A gatherer also fetches a tool that only speeds the work up, such as a rake, while a tool station has one.
        var rule = GatherRules.Of(spot.Kind);
        string? need = rule?.Needs ?? (rule?.Helps is string helping && HasTool(w, helping) ? helping : null);
        if (need == null) { if (!HandsFree(w, dt)) return true; } else
            switch (Equip(w, need, spot, dt)) {
                case Kit.Busy: return true;
                // With no tool to be had, the nearest spot of the job that needs none, or one that is to be had, is worked instead.
                // A wrong tool with no room to go back to stays in hand, and the job waits at its spot.
                case Kit.Lacking: return w.Tool == "" && OtherSpot(w, spot);
            }
        w.Status = Phrase("Gathering ", Data.Name(item));
        if (!Walk(w, spot.Pos, dt)) return true;
        w.WorkState = WorkerWorkState.Gathering; w.Exhausted = null;
        string? tool = w.Tool == "" ? null : w.Tool;
        if (spot.IsDug) {
            // What is dug up comes out whole; what does not fit in hand waits in the hole for the next load.
            spot.Progress += dt * Speed(tool);
            if (!DugOut(spot)) return true;
            Unearth(spot, w.Bag);
        } else {
            w.Timer += dt;
            if (!spot.IsLoose && w.Timer < GatherTime(spot.Kind, tool)) return true;
            w.Timer = 0;
            int took = w.Bag.Add(item, spot.IsLoose ? spot.Remaining : Math.Min(spot.Remaining, GatherYield(spot.Kind))); spot.Remaining -= took;
            if (took > 0) Got(item);
            Chip(spot, tool);
        }
        w.Loading = w.Bag.Room(item) > 0;
        if (spot.Remaining > 0) return true;
        // Whoever fells a tree digs its stump up next while a shovel is to be had, and otherwise goes on to the next tree.
        if (spot.Kind == ThingKind.Tree) {
            var stump = Fell(spot);
            if (CanWork(w, stump.Kind)) { MoveTo(w, spot.Id, stump); return true; }
        }
        if (spot.IsLoose) S.Things.Remove(spot);
        if (!NextSpot(w, spot.Id, spot.Kind, spot.Pos)) w.Loading = false;
        return true;
    }

    /// <summary>A crafter works a station that someone has to stand at, while it holds a batch or is on one.</summary>
    private bool Craft(Worker w, Thing station, float dt) {
        if (!Data.Recipes.TryGetValue(station.Recipe, out var recipe)) { w.Status = "Nothing queued or marked to make"; return false; }
        if (!Worked(station)) { w.Status = "Waiting for the next load"; return false; }
        if (station.Tools != null && recipe.Tool != "" && BenchTool(station, recipe.Tool, recipe.Tier) == null) {
            w.Status = Phrase("Needs ", A(Data.ToolName(recipe.Tool, recipe.Tier)), " in the ", Lower(Data.Name(station.Kind)));
            return false;
        }
        if (Stalled(station, recipe)) { w.Status = "Output slot is full"; return false; }
        if (!station.Processing && !Stocked(station)) {
            w.Status = Lacks(station, recipe) is string lack ? Phrase("Needs ", Lower(Data.Name(lack)), " at the ", Lower(Data.Name(station.Kind))) : "Waiting for the next load";
            return false;
        }
        if (!HandsFree(w, dt)) return true;
        w.Status = "Working at station";
        if (Walk(w, station.Pos, dt)) w.WorkState = WorkerWorkState.Hammering;
        return true;
    }

    /// <summary>Whether a station's finished batch waits for room in its output slot, so working it does nothing.</summary>
    private static bool Stalled(Thing station, RecipeDef recipe) =>
        station.Processing && station.Progress >= recipe.Seconds && SlotRoom(station.Inventory, recipe.Output) < recipe.Amount;

    /// <summary>The first input a station is short of for a batch, or the fuel it lacks, named for a status; null when something else keeps it from starting.</summary>
    private string? Lacks(Thing station, RecipeDef recipe) {
        foreach (var (input, needed) in recipe.Inputs) if (station.Inventory[input] < needed) return input;
        return recipe.Fuels.Count > 0 && FuelOf(station) == null ? recipe.Fuels[0] : null;
    }

    /// <summary>
    /// One worker pumps a reinforced furnace's bellows while it smelts or holds a batch to smelt.
    /// With nothing to smelt, or someone else at the bellows, there is nothing to do.
    /// </summary>
    private bool Pump(Worker w, Thing forge, float dt) {
        foreach (var other in S.Workers)
            if (other != w && other.Assignment == forge.Id && other.Job == "bellows" && other.WageDebt <= 0 && OnDuty(other)) { w.Status = "Bellows are manned"; return false; }
        if (!forge.Processing && !Stocked(forge) || Data.Recipes.TryGetValue(forge.Recipe, out var recipe) && Stalled(forge, recipe)) { w.Status = "Nothing to smelt"; return false; }
        if (!HandsFree(w, dt)) return true;
        w.Status = "Operating bellows";
        if (Walk(w, forge.Pos + BellowsStand, dt, .1f)) { w.WorkState = WorkerWorkState.Pumping; forge.Heat = Math.Min(100, forge.Heat + dt * 18); }
        return true;
    }

    /// <summary>A campfire's keeper brings wood from storage once the fire runs low.</summary>
    private bool TendFire(Worker w, Thing fire, float dt) {
        if (fire.Burn > 120 || fire.Inventory["wood"] > 0) { w.Status = "Tending fire"; return false; }
        var source = SourceFor("wood", w.Pos);
        if (source == null) { w.Status = "Needs wood in output-enabled storage"; return false; }
        if (!HandsFree(w, dt)) return true;
        w.Status = "Fetching firewood";
        if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, "wood", 3, true); w.Destination = fire.Id; }
        return true;
    }
}
