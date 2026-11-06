using System;
using System.Collections;
using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>
/// The world's things in the order they were added, also kept by kind and by id, so a search for one kind passes over the rest.
/// A thing's kind and id never change, so every change to the list keeps both up to date.
/// </summary>
public sealed class ThingList : IList<Thing> {
    private readonly List<Thing> all = new();
    private readonly List<Thing>[] byKind = Array.ConvertAll(Kinds.All, _ => new List<Thing>());
    private readonly Dictionary<int, Thing> byId = new();

    public int Count => all.Count;
    public bool IsReadOnly => false;
    public ListView<Thing> View => new(all);
    /// <summary>The things of one kind, in the order they were added.</summary>
    public ListView<Thing> Of(ThingKind kind) => new(byKind[(int)kind]);
    public Thing? Find(int id) => byId.GetValueOrDefault(id);
    public bool Contains(Thing t) => byKind[(int)t.Kind].Contains(t);
    public int IndexOf(Thing t) => all.IndexOf(t);

    // Putting a thing anywhere but the end is rare, so it lists everything again rather than finding its place in its kind's list.
    public Thing this[int index] { get => all[index]; set { all[index] = value; Reindex(); } }
    public void Insert(int index, Thing t) { all.Insert(index, t); Reindex(); }
    public void Add(Thing t) { all.Add(t); byKind[(int)t.Kind].Add(t); byId[t.Id] = t; }

    public bool Remove(Thing t) {
        if (!all.Remove(t)) return false;
        byKind[(int)t.Kind].Remove(t); byId.Remove(t.Id);
        return true;
    }
    public void RemoveAt(int index) => Remove(all[index]);
    public int RemoveAll(Predicate<Thing> match) {
        int removed = all.RemoveAll(match);
        if (removed > 0) Reindex();
        return removed;
    }
    /// <summary>Removes the things of one kind that match, looking at no others.</summary>
    public void RemoveAll(ThingKind kind, Predicate<Thing> match) {
        var list = byKind[(int)kind];
        for (int i = list.Count - 1; i >= 0; i--) if (match(list[i])) Remove(list[i]);
    }
    public void Clear() {
        all.Clear(); byId.Clear();
        foreach (var list in byKind) list.Clear();
    }

    private void Reindex() {
        byId.Clear();
        foreach (var list in byKind) list.Clear();
        foreach (var t in all) { byKind[(int)t.Kind].Add(t); byId[t.Id] = t; }
    }

    public void CopyTo(Thing[] array, int index) => all.CopyTo(array, index);
    public List<Thing>.Enumerator GetEnumerator() => all.GetEnumerator();
    IEnumerator<Thing> IEnumerable<Thing>.GetEnumerator() => all.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => all.GetEnumerator();
}
