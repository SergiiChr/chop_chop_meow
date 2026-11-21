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
        if (w.InHand is JobEntry doing) {
            if (w.Jobs.Contains(doing) && Rounds(doing) == w.Mark && Do(w, doing, dt)) return;
            w.InHand = null;
        }
        w.Plan = default;
        w.WaterWalk = null;
        // A gatherer with no spot left carries off what they picked so far.
        if (w.Loading) {
            w.Loading = false;
            if (w.Bag.Count > 0) { Deliver(w, dt); return; }
        }
        // With nothing to do, the list is read again a few times a second rather than on every tick.
        if (S.Time < w.ReadAgain) { Idle(w, w.Lacking, dt); return; }
        string? lacking = null;
        foreach (var entry in w.Jobs) {
            if (entry is Duty duty && (w.Assignment != duty.Thing || w.Doing != duty.Job)) { w.Assignment = duty.Thing; w.Doing = duty.Job; w.Timer = 0; }
            if (Do(w, entry, dt)) { w.InHand = entry; w.Mark = Rounds(entry); return; }
            lacking ??= w.Status;
        }
        w.Lacking = lacking; w.ReadAgain = S.Time + JobListSeconds;
        Idle(w, lacking, dt);
    }

    /// <summary>Seconds a worker with nothing to do waits before reading their job list again.</summary>
    private const float JobListSeconds = .25f;

    /// <summary>The rounds a job's place has counted, for the jobs whose round is a unit of the place's work; a delivery ends the round of the others.</summary>
    private int Rounds(JobEntry entry) => entry is Duty duty && Def(duty.Job).Rounds && Find(duty.Thing) is Thing place ? place.Rounds : 0;

    /// <summary>Does one of a worker's jobs, or a haul group, for a tick and tells whether there was anything to do; with nothing, the status says what the job lacks.</summary>
    private bool Do(Worker w, JobEntry entry, float dt) {
        if (entry is HaulGroup group) return hauling.Haul(w, group, dt);
        var duty = (Duty)entry;
        var place = Find(duty.Thing);
        // Only a gatherer goes on without their place: on to the next spot of the same kind.
        return (place != null || duty.Job == JobKind.Gather) && Def(duty.Job).Do(this, w, duty, place, dt);
    }

    /// <summary>
    /// With no job to do, a worker waits at the nearest trading post; their status says what their first job lacks.
    /// A tool none of their jobs uses goes back to a tool station first.
    /// </summary>
    private void Idle(Worker w, string? lacking, float dt) {
        w.Assignment = FirstPlace(w); w.Doing = null;
        if (w.Tool != "" && !JobUses(w, ToolKind(w.Tool)) && !ReturnTool(w, dt)) return;
        WaitAtPost(w, dt);
        if (w.WorkState != WorkerWorkState.Blocked) w.Status = lacking ?? "Available";
    }

    /// <summary>Whether one of a worker's jobs uses a kind of tool: building or working a mine, or gathering what needs it or goes faster with it.</summary>
    private bool JobUses(Worker w, string kind) {
        foreach (var d in w.Duties) {
            if (d.Job == JobKind.Haul && Find(d.Thing)?.Kind is ThingKind.Hut or ThingKind.Barrel && BarrelHaul.Vessel(w.Tool)) return true;
            if (Array.IndexOf(Def(d.Job).Tools, kind) >= 0) return true;
            // A woodcutter's job covers trees and stumps, so the spot it is on now counts as well as the kind it was given.
            if (d.Job == JobKind.Gather && (Uses(d.Kind) || Uses(Find(d.Thing)?.Kind))) return true;
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
        // A stack a player holds on the cursor stays in hand; the rest goes on its way.
        string? item = null;
        foreach (var (held, _) in w.Bag.Items) if (w.Bag.Free(held) > 0) { item = held; break; }
        if (item == null) { w.Status = "Waiting for the player to let go"; return; }
        var dest = Find(w.Destination);
        if (FillsLeftover(w, dest, item, dt)) return;
        // Rules are read again every tick, so a filter changed or a slot filled while a load is on its way sends it elsewhere.
        var intake = dest == null ? null : Intake(dest, item, w.Fueling);
        if (intake == null) {
            dest = DepositFor(item, w.Pos); w.Fueling = false;
            intake = dest == null ? null : Intake(dest, item, false);
        }
        if (dest == null || intake is not (Inventory into, int room)) { w.Status = Phrase("Holding ", Data.Name(item), " · no accepting storage"); return; }
        w.Destination = dest.Id; w.Status = Phrase("Carrying ", Data.Name(item), " → ", Data.Name(dest.Kind));
        if (!Walk(w, dest.Pos, dt)) return;
        // Every vessel carried pours, whatever it holds; the vessels stay in hand, emptier.
        if (PoursInto(dest, into, item) is VesselDef pours) Carry(w.Bag, into, dest, pours.Liquid, room, room);
        else if (FuelSplit(dest, item, w.Bag[item]) is (int fuel, int input)) {
            w.Bag.TransferTo(dest.Burner!, item, fuel, true);
            w.Bag.TransferTo(dest.Inventory, item, input, true);
        } else w.Bag.TransferTo(into, item, Math.Min(w.Bag[item], room), true);
        w.Destination = 0;
        if (w.Bag.Count == 0) { w.Fueling = false; w.InHand = null; }
    }

    /// <summary>
    /// Which box of a place takes a carried item now, and how many of it, or null when none does:
    /// a station's input slot up to a stack, or its fuel slot; what a mine site or a farm plot is short of;
    /// storage that holds the item, as far as it has room: a trading post only crates its filter asks for, a packing station its good only while a slot stays left for crates,
    /// compost set to skip food no food, and a placed crate no crate.
    /// A vessel's room is the liters its liquid has room for.
    /// </summary>
    /// <param name="fueling">The load was fetched for the fuel slot, so it goes there even when the recipe also takes the item as an input.</param>
    internal (Inventory Box, int Room)? Intake(Thing dest, string item, bool fueling) {
        int room;
        if (IsStation(dest.Kind)) {
            if (!fueling && (room = InputRoom(dest, item)) > 0) return (dest.Inventory, room);
            return dest.Burner is Inventory burner && Fuels(dest).Contains(item) && FuelTakes(dest, item) && (room = burner.Room(item)) > 0 ? (burner, room) : null;
        }
        room = dest.Kind switch {
            ThingKind.Mine => Mines.Need(dest, item),
            ThingKind.Farm => SowRoom(dest, item),
            // A trading post takes only the crates its filter asks for, which is how it asks for goods to sell.
            ThingKind.Signpost when !dest.Inventory.Wants(item) => 0,
            ThingKind.Packer when item == dest.Packs && !PackerTakes(dest, item) => 0,
            _ when dest.Compost && dest.SkipFood && Data.IsFood(item) => 0,
            _ when Nests(dest, item) => 0,
            _ => dest.Inventory.Accepts(item) && Holds(dest, item) ? dest.Inventory.Room(item) : 0
        };
        return room > 0 ? (dest.Inventory, room) : null;
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

    /// <summary>
    /// How many more of an item a station's input slot takes: up to a stack of what the recipe it is on uses.
    /// A vessel counts as the liters of room for the liquid it pours.
    /// </summary>
    private int InputRoom(Thing t, string item) {
        string lands = PoursInto(t, t.Inventory, item)?.Liquid ?? item;
        if (Data.VesselFor(lands) != null && lands == item || !BatchCost(t).ContainsKey(lands)) return 0;
        return Math.Max(0, t.Inventory.SlotRoom(lands));
    }

    private bool IsStation(ThingKind kind) => Array.IndexOf(stationKinds, kind) >= 0;

    /// <summary>
    /// Whether a worker's hands are clear of a tool the job in hand has no use for.
    /// They walk it back to a tool station first; with no station to take it they keep it and carry on.
    /// </summary>
    internal bool HandsFree(Worker w, float dt) {
        w.Missing = "";
        return w.Tool == "" || ReturnTool(w, dt);
    }

    internal enum Kit { Ready, Busy, Lacking }

    /// <summary>Gets a worker the tool a job needs: puts back one of another kind, then takes the best from a tool station; tells whether they hold it, are on the way, or have none to take.</summary>
    internal Kit Equip(Worker w, string need, Thing target, float dt) {
        if (w.Tool != "" && ToolKind(w.Tool) != need) {
            if (!ReturnTool(w, dt)) return Kit.Busy;
            // With no room in a tool station the tool stays in hand, and a job that needs another kind waits for room.
            if (w.Tool != "") { w.Status = Phrase("No room to put back the ", Lower(Data.Name(w.Tool))); return Kit.Lacking; }
        }
        if (w.Tool != "") return Kit.Ready;
        FetchTool(w, need, target, dt);
        return w.Missing != "" ? Kit.Lacking : Kit.Busy;
    }

    /// <summary>How many pieces one gather cycle yields, for the player and a worker alike.</summary>
    internal static int GatherYield(ThingKind kind) => kind == ThingKind.WolfCorpse ? 2 : 4;

    /// <summary>Whether a station's finished batch waits for room in its output slot, so working it does nothing.</summary>
    internal static bool Stalled(Thing station, RecipeDef recipe) =>
        station.Processing && station.Progress >= recipe.Seconds && station.Inventory.SlotRoom(recipe.Output) < recipe.Amount;

    /// <summary>
    /// One worker pumps a reinforced furnace's bellows while it smelts or holds a batch to smelt.
    /// With nothing to smelt, or someone else at the bellows, there is nothing to do.
    /// </summary>
    private bool Pump(Worker w, Thing forge, float dt) {
        foreach (var other in S.Workers) if (other != w && AtBellows(other, forge)) { w.Status = "Bellows are manned"; return false; }
        if (!forge.Processing && !Stocked(forge) || Data.Recipes.TryGetValue(forge.Recipe, out var recipe) && Stalled(forge, recipe)) { w.Status = "Nothing to smelt"; return false; }
        if (!HandsFree(w, dt)) return true;
        w.Status = "Operating bellows";
        if (Walk(w, forge.Pos + BellowsStand, dt, .1f)) { w.WorkState = WorkerWorkState.Pumping; forge.Heat = Math.Min(100, forge.Heat + dt * 18); }
        return true;
    }

    /// <summary>Whether a worker has a furnace's bellows in hand, on duty and with nothing to carry off first.</summary>
    private bool AtBellows(Worker w, Thing forge) =>
        w.Assignment == forge.Id && w.Doing == JobKind.Bellows && w.Bag.Count == 0 && w.WageDebt <= 0 && w.WorkState != WorkerWorkState.Blocked && OnDuty(w);

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
