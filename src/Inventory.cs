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
    public int Capacity { get; set; } = 160;
    public int Count => Items.Values.Sum();
    public int Free => Math.Max(0, Capacity - Count);
    public int this[string item] => Items.GetValueOrDefault(item);

    public bool Accepts(string item) => InputFilter.Count == 0 || InputFilter.Contains(item);
    public bool AllowsWithdrawal(string item) => OutputFilter.Count == 0 || OutputFilter.Contains(item);

    public int Add(string item, int quantity)
    {
        int amount = Math.Min(Math.Max(0, quantity), Free);
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
        int amount = Math.Min(this[item], Math.Min(Math.Max(0, quantity), destination.Free));
        Remove(item, amount);
        return destination.Add(item, amount);
    }
}
