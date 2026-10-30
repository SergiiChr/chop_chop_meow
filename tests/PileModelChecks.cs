using System;
using System.Linq;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Inventory-driven pile appearance refreshes without changing stock or sharing visibility state.</summary>
public static class PileModelChecks {
    public static void Run(GameData data) {
        PileShape.Warm(data);
        var thing = new Thing { Kind = "pile", Inventory = new Inventory { Slots = 14 } };
        var first = WorldModels.Build(thing); var second = WorldModels.Build(thing);
        try {
            thing.Inventory.Add("wood", 14 * 64);
            WorldModels.SyncPile(first, thing, data);
            WorldModels.SyncPile(second, thing, data);
            Require(first.Stacks.Length == 14 && first.Stacks.All(s => s.Visible), "full inventory must show every pile slot");
            var logs = first.Stacks[0].Mesh!;
            Require(second.Stacks[0].Mesh!.GetInstanceId() == logs.GetInstanceId(), "piles must share cached stock geometry");

            thing.Inventory.Items.Clear(); thing.Inventory.Add("stone", 64);
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stacks[0].Visible && first.Stacks.Skip(1).All(s => !s.Visible), "removing stock must hide its slots");
            Require(first.Stacks[0].Mesh!.GetInstanceId() != logs.GetInstanceId(), "replacing wood with stone must change the model");
            Require(second.Stacks.All(s => s.Visible) && second.Stacks[0].Mesh!.GetInstanceId() == logs.GetInstanceId(), "a refresh must leave another pile's remembered appearance alone");

            thing.Inventory.Items.Clear(); thing.Inventory.Add("fertilizer", 64);
            WorldModels.SyncPile(first, thing, data);
            var fallback = first.Stacks[0].Mesh!;
            thing.Inventory.Items.Clear(); thing.Inventory.Add("unmodelled_item", 64);
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stacks[0].Mesh!.GetInstanceId() == fallback.GetInstanceId(), "unmodelled items must use the shared fallback");
            Require(first.Stacks[0].MaterialOverride is StandardMaterial3D material && material.AlbedoColor.IsEqualApprox(new Color("b0a58a")), "unknown items must have a visible fallback color");

            thing.Inventory.Items.Clear();
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stacks.All(s => !s.Visible), "emptying a pile must hide all contents");
        } finally { first.Root.Free(); second.Root.Free(); }
    }
}
