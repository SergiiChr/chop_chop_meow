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
            Require(first.Stock!.Count == 14 && first.Stock!.Slots.All(s => s.Visible), "full inventory must show every pile slot");
            var logs = first.Stock!.Slots[0].Mesh!;
            Require(second.Stock!.Slots[0].Mesh!.GetInstanceId() == logs.GetInstanceId(), "piles must share cached stock geometry");

            thing.Inventory.Items.Clear(); thing.Inventory.Add("stone", 64);
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock!.Slots[0].Visible && first.Stock!.Slots.Skip(1).All(s => !s.Visible), "removing stock must hide its slots");
            Require(first.Stock!.Slots[0].Mesh!.GetInstanceId() != logs.GetInstanceId(), "replacing wood with stone must change the model");
            Require(second.Stock!.Slots.All(s => s.Visible) && second.Stock!.Slots[0].Mesh!.GetInstanceId() == logs.GetInstanceId(), "a refresh must leave another pile's remembered appearance alone");

            thing.Inventory.Items.Clear(); thing.Inventory.Add("fertilizer", 64);
            WorldModels.SyncPile(first, thing, data);
            var fallback = first.Stock!.Slots[0].Mesh!;
            thing.Inventory.Items.Clear(); thing.Inventory.Add("unmodelled_item", 64);
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock!.Slots[0].Mesh!.GetInstanceId() == fallback.GetInstanceId(), "unmodelled items must use the shared fallback");
            Require(first.Stock!.Slots[0].MaterialOverride is StandardMaterial3D material && material.AlbedoColor.IsEqualApprox(new Color("b0a58a")), "unknown items must have a visible fallback color");

            thing.Inventory.Items.Clear();
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock!.Slots.All(s => !s.Visible), "emptying a pile must hide all contents");

            // Stations showing piled goods use the same handler, so a good looks the same on them as in a storage pile.
            thing.Inventory.Add("wood", 64);
            WorldModels.SyncPile(first, thing, data);
            var furnace = WorldModels.Build(new Thing { Kind = "furnace", Burner = new Inventory() });
            var packer = WorldModels.Build(new Thing { Kind = "packer" });
            try {
                var fire = new Thing { Kind = "furnace", Burner = new Inventory() }; fire.Burner.Add("wood", 3);
                WorldModels.SyncFuel(furnace, fire, data);
                Require(furnace.Stock!.Slots.Count(s => s.Visible) == 2 && furnace.Stock.Slots[0].Mesh == first.Stock!.Slots[0].Mesh,
                        "a furnace should show its stocked wood as storage pile logs, a pile for every two");
                packer.Stock!.Fill("wood", 5, data.CrateCapacity / 3, data);
                Require(packer.Stock.Slots.Count(s => s.Visible) == 2 && packer.Stock.Slots[0].Mesh == first.Stock.Slots[0].Mesh,
                        "a packing station should heap its goods as storage pile stock");
            } finally { furnace.Root.Free(); packer.Root.Free(); }
        } finally { first.Root.Free(); second.Root.Free(); }
    }
}
