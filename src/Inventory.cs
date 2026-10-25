using System;
using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Item quantities and explicit automation access rules. Player access is independent of automation filters.</summary>
public sealed class Inventory
{
    public Dictionary<string, int> Items { get; set; } = new();
    public HashSet<string> InputFilter { get; set; } = new();
    public HashSet<string> OutputFilter { get; set; } = new();
    public int Slots { get; set; } = 18;
    public int StackSize { get; set; } = 64;
    public const string Coins = "coins";
    public int Count => Items.Values.Sum();
    public int UsedSlots => Items.Sum(pair => SlotsFor(pair.Key, pair.Value));
    public int this[string item] => Items.GetValueOrDefault(item);

    /// <summary>Coins stack to 1000 in any container; every other item uses the container's stack size.</summary>
    public int StackOf(string item) => item == Coins ? 1000 : StackSize;
    private int SlotsFor(string item, int quantity) => (quantity + StackOf(item) - 1) / StackOf(item);

    /// <summary>Units of an item that still fit: empty slots plus the unfilled part of its last stack.</summary>
    public int Room(string item) => Math.Max(0, (Slots - UsedSlots) * StackOf(item) + (StackOf(item) - this[item] % StackOf(item)) % StackOf(item));

    /// <summary>Each item split into full stacks followed by its remainder.</summary>
    public IEnumerable<(string Item, int Count)> Stacks() => Items.SelectMany(pair =>
        Enumerable.Range(0, SlotsFor(pair.Key, pair.Value)).Select(i => (pair.Key, Math.Min(StackOf(pair.Key), pair.Value - i * StackOf(pair.Key)))));

    public bool Accepts(string item) => InputFilter.Count == 0 || InputFilter.Contains(item);
    public bool AllowsWithdrawal(string item) => OutputFilter.Count == 0 || OutputFilter.Contains(item);

    public int Add(string item, int quantity)
    {
        int amount = Math.Min(Math.Max(0, quantity), Room(item));
        if (amount > 0) Items[item] = this[item] + amount;
        return amount;
    }

    public int Remove(string item, int quantity)
    {
        int amount = Math.Min(Math.Max(0, quantity), this[item]);
        if (amount == this[item]) Items.Remove(item);
        else Items[item] -= amount;
        return amount;
    }

    public bool Fits(IReadOnlyDictionary<string, int> added) => Items.Keys.Union(added.Keys).Sum(item => SlotsFor(item, this[item] + added.GetValueOrDefault(item))) <= Slots;

    public bool Has(IReadOnlyDictionary<string, int> cost) => cost.All(pair => this[pair.Key] >= pair.Value);

    public bool Spend(IReadOnlyDictionary<string, int> cost)
    {
        if (!Has(cost)) return false;
        foreach (var pair in cost) Remove(pair.Key, pair.Value);
        return true;
    }

    public int TransferTo(Inventory destination, string item, int quantity, bool automated = false)
    {
        if (automated && (!AllowsWithdrawal(item) || !destination.Accepts(item))) return 0;
        int amount = Math.Min(this[item], Math.Min(Math.Max(0, quantity), destination.Room(item)));
        Remove(item, amount);
        return destination.Add(item, amount);
    }
}
