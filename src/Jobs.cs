using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

/// <summary>The kinds of work a worker can be given at a place, in the order the job buttons show them.</summary>
[JsonConverter(typeof(JobConverter))]
public enum JobKind { Gather, Haul, Craft, Bellows, Farm, Build, Mine, Deliveries, Fuel, Stock, Tend }

/// <summary>Reads and writes a job as its id, such as "haul".</summary>
public sealed class JobConverter() : JsonStringEnumConverter<JobKind>(JsonNamingPolicy.SnakeCaseLower);

/// <summary>What a job is called, what it does and how a worker goes about it.</summary>
/// <param name="Job">The job the row is for.</param>
/// <param name="Title">The job's name on buttons and rows.</param>
/// <param name="Hint">What the job does, on its button.</param>
/// <param name="Rounds">Whether a round of it is a unit of the place's work, such as a batch, rather than a delivery.</param>
/// <param name="Tools">The tool kinds the job itself takes; a gather job takes what its spot needs.</param>
/// <param name="Do">Does the job for a tick at its place, which is null only for a gather job whose spot is gone; tells whether there was anything to do.</param>
public sealed record JobDef(JobKind Job, string Title, string Hint, bool Rounds, string[] Tools, Func<World, Worker, Duty, Thing?, float, bool> Do);

/// <summary>One entry of a worker's job list: a job at one place, or a haul group.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Entry")]
[JsonDerivedType(typeof(Duty), "job")]
[JsonDerivedType(typeof(HaulGroup), "group")]
public abstract class JobEntry;

/// <summary>One job a worker does at one place, such as working the bellows of a furnace or hauling for it.</summary>
public sealed class Duty : JobEntry {
    public int Thing { get; set; }
    public JobKind Job { get; set; }
    /// <summary>For a gather job, the kind of resource it keeps gathering once its spot is spent or gone.</summary>
    [JsonConverter(typeof(OptionalKind))] public ThingKind? Kind { get; set; }
}

/// <summary>Hauls that count as one job: each load is picked by the haul rules across all their places.</summary>
public sealed class HaulGroup : JobEntry {
    /// <summary>Tells the worker's groups apart in commands; numbered from 1 as they are added.</summary>
    public int Id { get; set; }
    public List<Duty> Hauls { get; set; } = new();
}

/// <summary>Where an entry lies in a worker's job list: its place in the list, and for a haul in a group its place among the group's hauls.</summary>
/// <param name="Haul">The place among the hauls of the group at <paramref name="Entry"/>, or -1 for the entry itself.</param>
public readonly record struct JobSpot(int Entry, int Haul = -1) {
    public bool InGroup => Haul >= 0;
}

/// <summary>Every job in a worker's list, the hauls of each group included, in order; a loop over it allocates nothing.</summary>
public readonly struct DutyList(List<JobEntry> jobs) {
    public Enumerator GetEnumerator() => new(jobs);

    public struct Enumerator(List<JobEntry> jobs) {
        private int entry = -1, haul = -1;
        public Duty Current { get; private set; } = null!;

        public bool MoveNext() {
            while (true) {
                if (entry >= 0 && jobs[entry] is HaulGroup group && ++haul < group.Hauls.Count) { Current = group.Hauls[haul]; return true; }
                if (++entry >= jobs.Count) return false;
                haul = -1;
                if (jobs[entry] is Duty duty) { Current = duty; return true; }
            }
        }
    }
}

/// <summary>Converts jobs to and from the ids that saves, commands and the names of controls use, such as "haul".</summary>
public static class JobKinds {
    private static readonly string[] ids;
    private static readonly Dictionary<string, JobKind> byId = new();

    static JobKinds() {
        var all = Enum.GetValues<JobKind>();
        ids = new string[all.Length];
        foreach (var job in all) {
            ids[(int)job] = JsonNamingPolicy.SnakeCaseLower.ConvertName(job.ToString());
            byId[ids[(int)job]] = job;
        }
    }

    /// <summary>The job's id, the one its converter reads and writes; cached, so asking every frame allocates nothing.</summary>
    public static string Id(this JobKind job) => ids[(int)job];

    public static bool TryParse(string id, out JobKind job) => byId.TryGetValue(id, out job);
}

/// <summary>A worker's job list: the table of jobs, and adding, moving and grouping the entries.</summary>
public sealed partial class World {
    private static readonly JobDef[] JobDefs = ByKind(
        new(JobKind.Gather, "Gather", "Gathers and carries to storage.", false, [],
            static (world, w, d, place, dt) => place?.Kind == ThingKind.Water ? world.WaterWork(w, place, dt) : world.Gather(w, d, place, dt)),
        new(JobKind.Haul, "Haul", "Brings ingredients and fuel and carries what is made to storage.", false, [], static (world, w, d, _, dt) => world.Haul(w, d, dt)),
        new(JobKind.Craft, "Craft", "Works the station; it only makes progress while someone works it.", true, [],
            static (world, w, _, place, dt) => place!.Kind == ThingKind.Packer ? world.PackWork(w, place, dt) : world.Craft(w, place, dt)),
        new(JobKind.Bellows, "Bellows", "Keeps the fire hot. Only one worker pumps at a time.", true, [], static (world, w, _, place, dt) => world.Pump(w, place!, dt)),
        new(JobKind.Farm, "Farm", "Harvests the ripe wheat.", true, [], static (world, w, _, place, dt) => world.Reap(w, place!, dt)),
        new(JobKind.Build, "Build", "Digs the entrance and fits the frame. Needs a pickaxe, then a hammer.", true, ["pickaxe", "hammer"], static (world, w, _, place, dt) => world.Mines.Build(w, place!, dt)),
        new(JobKind.Mine, "Mine", "Mines ore and stone underground with a pickaxe.", true, ["pickaxe"], static (world, w, _, place, dt) => world.Mines.Mine(w, place!, dt)),
        new(JobKind.Deliveries, "Manage deliveries", "Fetches fresh prices on request and carries deliveries to storage.", false, [], static (world, w, _, place, dt) => world.ManageDeliveries(w, place!, dt)),
        new(JobKind.Fuel, "Tend fire", "Keeps the fire fed with wood from storage.", false, [], static (world, w, _, place, dt) => world.TendFire(w, place!, dt)),
        new(JobKind.Stock, "Stock", "Brings tools from storage to the rack.", false, [], static (world, w, _, place, dt) => world.StockTools(w, place!, dt)),
        new(JobKind.Tend, "Tend", "Brings what the compost takes from storage and carries fertilizer back.", false, [], static (world, w, _, place, dt) => world.TendCompost(w, place!, dt))
    );

    /// <summary>A job's row in the table.</summary>
    public static JobDef Def(JobKind job) => JobDefs[(int)job];

    /// <summary>The table's rows by job, for <see cref="Def"/> to look up; the game does not start with a job that has no row or two.</summary>
    private static JobDef[] ByKind(params JobDef[] rows) {
        var byKind = new JobDef?[Enum.GetValues<JobKind>().Length];
        foreach (var row in rows) {
            if (byKind[(int)row.Job] != null) throw new InvalidOperationException($"The job {row.Job} has two rows in the job table.");
            byKind[(int)row.Job] = row;
        }
        for (int i = 0; i < byKind.Length; i++) if (byKind[i] == null) throw new InvalidOperationException($"The job {(JobKind)i} has no row in the job table.");
        return byKind!;
    }

    /// <summary>The jobs a place offers workers, in the order the panel shows them.</summary>
    public JobKind[] Jobs(Thing t) => t.IsResource ? GatherJobs : t.Kind switch {
        ThingKind.Reinforced => BellowsJobs,
        ThingKind.Bench or ThingKind.Anvil or ThingKind.Cooktable or ThingKind.Packer => CraftJobs,
        ThingKind.Furnace or ThingKind.Cannery or ThingKind.Barrel or ThingKind.Chest or ThingKind.Crate => HaulJobs,
        ThingKind.Hut when Barrel.In(t.Inventory) != null => HaulJobs,
        ThingKind.Signpost => DeliveryJobs,
        ThingKind.Campfire => FireJobs,
        ThingKind.Farm => FarmJobs,
        ThingKind.Mine => MineJobs,
        ThingKind.Toolstation => StockJobs,
        ThingKind.Pile when t.Compost => TendJobs,
        _ => NoJobs
    };
    private static readonly JobKind[] GatherJobs = [JobKind.Gather], BellowsJobs = [JobKind.Bellows, JobKind.Haul], CraftJobs = [JobKind.Craft, JobKind.Haul], HaulJobs = [JobKind.Haul],
        DeliveryJobs = [JobKind.Deliveries], FireJobs = [JobKind.Fuel], FarmJobs = [JobKind.Farm, JobKind.Haul], MineJobs = [JobKind.Build, JobKind.Haul, JobKind.Mine], StockJobs = [JobKind.Stock],
        TendJobs = [JobKind.Tend], NoJobs = [];

    /// <summary>Whether the worker has a job at a place, or the given job there, on its own or in a haul group.</summary>
    public static bool HasDuty(Worker w, int place, JobKind? job = null) {
        foreach (var d in w.Duties) if (d.Thing == place && (job == null || d.Job == job)) return true;
        return false;
    }

    private static Duty NewDuty(Thing place, JobKind job) => new() { Thing = place.Id, Job = job, Kind = job == JobKind.Gather ? place.Kind : null };

    /// <summary>The place of a worker's first job, or zero with none.</summary>
    private static int FirstPlace(Worker w) {
        foreach (var d in w.Duties) return d.Thing;
        return 0;
    }

    /// <summary>Puts a worker on every job at one place, instead of whatever they did before, or takes all their jobs away.</summary>
    public void Assign(Worker w, Thing? target) {
        if (target != null && NoRoomAt(w, target)) return;
        if (Busy() || Forwarded(Verb.Assign, target?.Id ?? 0, w.Id)) return;
        w.Jobs.Clear(); ListChanged(w);
        if (target != null) foreach (var job in Jobs(target)) w.Jobs.Add(NewDuty(target, job));
        Started(w, target);
    }

    /// <summary>Gives a worker one job at a place, at the end of their list, or takes it away wherever it is, keeping their other jobs.</summary>
    public void SetDuty(Worker w, Thing target, JobKind job, bool on) {
        if (on == HasDuty(w, target.Id, job) || on && Array.IndexOf(Jobs(target), job) < 0) return;
        if (on && NoRoomAt(w, target)) return;
        if (Busy() || Forwarded(Verb.Duty, target.Id, w.Id, job.Id(), on ? 1 : 0)) return;
        if (on) w.Jobs.Add(NewDuty(target, job));
        else {
            w.Jobs.RemoveAll(e => e is Duty d && d.Thing == target.Id && d.Job == job);
            foreach (var e in w.Jobs) if (e is HaulGroup group) group.Hauls.RemoveAll(d => d.Thing == target.Id && d.Job == job);
        }
        ListChanged(w);
        bool first = on && w.Jobs.Count == 1 && w.Jobs[0] is Duty;
        if (first || !on && w.Assignment == target.Id && !HasDuty(w, target.Id)) Started(w, Find(FirstPlace(w)));
    }

    /// <summary>Whether a place is a mine with a full crew that the worker is not in, which the player is told.</summary>
    private bool NoRoomAt(Worker w, Thing place) {
        if (place.Kind != ThingKind.Mine || HasDuty(w, place.Id) || Crew(place) < Data.Mine.Workers) return false;
        Say($"A mine has room for {Data.Mine.Workers} workers.");
        return true;
    }

    /// <summary>Has a worker read their changed list on the next tick, even one waiting with nothing to do.</summary>
    private static void ListChanged(Worker w) => w.ReadAgain = 0;

    /// <summary>Adds an empty haul group to the end of a worker's jobs.</summary>
    public void AddHaulGroup(Worker w) {
        if (Busy() || Forwarded(Verb.HaulGroup, other: w.Id, count: 1)) return;
        int last = 0;
        foreach (var e in w.Jobs) if (e is HaulGroup group) last = Math.Max(last, group.Id);
        w.Jobs.Add(new HaulGroup { Id = last + 1 }); ListChanged(w);
    }

    /// <summary>Takes a worker's haul group apart: its hauls take its place in the list, as jobs of their own.</summary>
    public void RemoveHaulGroup(Worker w, int id) {
        if (Busy() || Forwarded(Verb.HaulGroup, other: w.Id, at: new Vector2(id, 0))) return;
        int at = w.Jobs.FindIndex(e => e is HaulGroup group && group.Id == id);
        if (at < 0) return;
        var hauls = ((HaulGroup)w.Jobs[at]).Hauls;
        w.Jobs.RemoveAt(at); w.Jobs.InsertRange(at, hauls); ListChanged(w);
    }

    /// <summary>The entry at a spot of a worker's list, or null when there is none there.</summary>
    public static JobEntry? JobAt(Worker w, JobSpot spot) {
        if ((uint)spot.Entry >= (uint)w.Jobs.Count) return null;
        if (!spot.InGroup) return w.Jobs[spot.Entry];
        return w.Jobs[spot.Entry] is HaulGroup group && spot.Haul < group.Hauls.Count ? group.Hauls[spot.Haul] : null;
    }

    /// <summary>What tells an entry from the others in a command, such as "haul@12" or "group#1".</summary>
    public static string JobKey(JobEntry entry) => entry is HaulGroup group ? $"group#{group.Id}" : $"{((Duty)entry).Job.Id()}@{((Duty)entry).Thing}";

    /// <summary>
    /// Moves an entry of a worker's list to another spot, which is the order the jobs come first in.
    /// Only a haul goes into a haul group; a group moves with its hauls.
    /// </summary>
    /// <param name="to">The spot it goes in front of, counted as the list stands before the move: an entry, or a haul of a group; past the end puts it last.</param>
    public void MoveJob(Worker w, JobSpot from, JobSpot to) {
        if (JobAt(w, from) is not JobEntry entry) return;
        if (to.InGroup && ((uint)to.Entry >= (uint)w.Jobs.Count || w.Jobs[to.Entry] is not HaulGroup || entry is not Duty { Job: JobKind.Haul })) return;
        if (Busy() || Forwarded(Verb.MoveJob, from.Entry, w.Id, JobKey(entry), from.Haul, new Vector2(to.Entry, to.Haul))) return;
        // Taking the entry out moves what came after it in the same list up by one.
        if (from.InGroup) {
            ((HaulGroup)w.Jobs[from.Entry]).Hauls.RemoveAt(from.Haul);
            if (to.InGroup && to.Entry == from.Entry && to.Haul > from.Haul) to = to with { Haul = to.Haul - 1 };
        } else {
            w.Jobs.RemoveAt(from.Entry);
            if (to.Entry > from.Entry) to = to with { Entry = to.Entry - 1 };
        }
        if (to.InGroup) {
            var hauls = ((HaulGroup)w.Jobs[to.Entry]).Hauls;
            hauls.Insert(Math.Clamp(to.Haul, 0, hauls.Count), (Duty)entry);
        } else w.Jobs.Insert(Math.Clamp(to.Entry, 0, w.Jobs.Count), entry);
        ListChanged(w);
    }

    /// <summary>Drops jobs at places that are gone, other than gather jobs, saying so when that leaves the worker without a job; haul groups stay, emptied or not.</summary>
    private void TidyDuties(Worker w) {
        int had = 0, left = 0;
        for (int i = w.Jobs.Count - 1; i >= 0; i--) {
            if (w.Jobs[i] is Duty duty) { had++; if (Gone(duty)) w.Jobs.RemoveAt(i); else left++; continue; }
            var hauls = ((HaulGroup)w.Jobs[i]).Hauls;
            for (int h = hauls.Count - 1; h >= 0; h--) { had++; if (Gone(hauls[h])) hauls.RemoveAt(h); else left++; }
        }
        if (left > 0) return;
        if (had > 0) Say($"{w.Name} without a job.");
        w.Assignment = 0; w.Doing = null;

        bool Gone(Duty d) => d.Kind == null && Find(d.Thing) == null;
    }

    /// <summary>Whether a worker's job list from a save holds only known jobs, and haul groups of hauls with numbers of their own.</summary>
    private static bool ValidJobs(List<JobEntry>? jobs) {
        if (jobs == null) return false;
        var groups = new HashSet<int>();
        foreach (var entry in jobs) {
            // The job converter also reads a number, which may name no job.
            if (entry is Duty duty) { if (!Enum.IsDefined(duty.Job)) return false; continue; }
            if (entry is not HaulGroup { Hauls: not null } group || group.Id <= 0 || !groups.Add(group.Id)) return false;
            foreach (var haul in group.Hauls) if (haul?.Job != JobKind.Haul) return false;
        }
        return true;
    }
}
