using System;
using System.Linq;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Inventory-driven pile appearance refreshes without changing stock or sharing visibility state.</summary>
public static class PileModelChecks {
    public static void Run(GameData data) {
        PileShape.Warm(data);
        var thing = new Thing { Kind = ThingKind.Pile, Inventory = new Inventory { Slots = 14 } };
        var first = (PileProp)WorldModels.Build(thing); var second = (PileProp)WorldModels.Build(thing);
        try {
            thing.Inventory.Add("wood", 14 * 64);
            WorldModels.SyncPile(first, thing, data);
            WorldModels.SyncPile(second, thing, data);
            Require(first.Stock.Count == 14 && first.Stock.Slots.All(s => s.Visible), "full inventory must show every pile slot");
            var logs = first.Stock.Slots[0].Mesh!;
            Require(second.Stock.Slots[0].Mesh!.GetInstanceId() == logs.GetInstanceId(), "piles must share cached stock geometry");

            thing.Inventory.Clear(); thing.Inventory.Add("stone", 64);
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock.Slots[0].Visible && first.Stock.Slots.Skip(1).All(s => !s.Visible), "removing stock must hide its slots");
            Require(first.Stock.Slots[0].Mesh!.GetInstanceId() != logs.GetInstanceId(), "replacing wood with stone must change the model");
            Require(second.Stock.Slots.All(s => s.Visible) && second.Stock.Slots[0].Mesh!.GetInstanceId() == logs.GetInstanceId(), "a refresh must leave another pile's remembered appearance alone");

            thing.Inventory.Clear(); thing.Inventory.Add("coal", 64);
            WorldModels.SyncPile(first, thing, data);
            var coal = first.Stock.Slots[0].Mesh!;
            thing.Inventory.Clear(); thing.Inventory.Add("charcoal", 64);
            WorldModels.SyncPile(first, thing, data);
            var charcoal = first.Stock.Slots[0].Mesh!;
            // Three six-sided briquettes with a hole through each: six sides, six hole walls and two ringed ends of six, two triangles apiece.
            Require(charcoal.GetInstanceId() != coal.GetInstanceId() && charcoal.GetFaces().Length / 3 == 144, "charcoal must pile as three holed briquettes rather than as coal's lumps");

            thing.Inventory.Clear(); thing.Inventory.Add("torch", 64);
            WorldModels.SyncPile(first, thing, data);
            var fallback = first.Stock.Slots[0].Mesh!;
            thing.Inventory.Clear(); thing.Inventory.Add("unmodelled_item", 64);
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock.Slots[0].Mesh!.GetInstanceId() == fallback.GetInstanceId(), "unmodelled items must use the shared fallback");
            Require(first.Stock.Slots[0].MaterialOverride is StandardMaterial3D material && material.AlbedoColor.IsEqualApprox(new Color("b0a58a")), "unknown items must have a visible fallback color");

            thing.Inventory.Clear();
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock.Slots.All(s => !s.Visible), "emptying a pile must hide all contents");

            // Stations showing piled goods use the same handler, so a good looks the same on them as in a storage pile.
            thing.Inventory.Add("wood", 64);
            WorldModels.SyncPile(first, thing, data);
            var furnace = (FurnaceProp)WorldModels.Build(new Thing { Kind = ThingKind.Furnace, Burner = new Inventory() });
            var packer = (PackerProp)WorldModels.Build(new Thing { Kind = ThingKind.Packer });
            try {
                var fire = new Thing { Kind = ThingKind.Furnace, Burner = new Inventory() }; fire.Burner.Add("wood", 3);
                WorldModels.SyncFuel(furnace, fire, data);
                Require(furnace.Stock.Slots.Count(s => s.Visible) == 2 && furnace.Stock.Slots[0].Mesh == first.Stock.Slots[0].Mesh,
                        "a furnace should show its stocked wood as storage pile logs, a pile for every two");
                packer.Stock.Fill("wood", 5, data.CrateCapacity / 3, data);
                Require(packer.Stock.Slots.Count(s => s.Visible) == 2 && packer.Stock.Slots[0].Mesh == first.Stock.Slots[0].Mesh,
                        "a packing station should heap its goods as storage pile stock");
            } finally { furnace.Root.Free(); packer.Root.Free(); }
        } finally { first.Root.Free(); second.Root.Free(); }
    }

    /// <summary>Every item's icon is rendered from a model, and the goods that are only ever carried or stored pile as that same model, light enough for a full pile.</summary>
    public static void Goods(GameData data) {
        foreach (string item in data.Items.Keys)
            Require(ResourceLoader.Exists($"res://assets/icons/{item}.png"), $"{item} must have an icon rendered from its model");
        PileShape.Warm(data);
        var thing = new Thing { Kind = ThingKind.Pile, Inventory = new Inventory { Slots = 14 } };
        var pile = (PileProp)WorldModels.Build(thing);
        try {
            var block = ModelSteps.Piled(pile, thing, "unmodelled_item", data);
            string[] goods = { "steel_parts", "tin_can", "grass", "fertilizer", "wheat_seeds", "wheat", "flour", "dough", "raw_meat", "burnt_steak", "bread",
                "crushed_berries", "berry_jam", "meat_stew", "canned_jam", "canned_meat_stew" };
            // A pile and a packing station are buildings, each held to 2,500 triangles with every slot of stock showing.
            var packer = (PackerProp)WorldModels.Build(new Thing { Kind = ThingKind.Packer });
            try {
                foreach (string good in goods) {
                    Require(ModelSteps.Piled(pile, thing, good, data).GetInstanceId() != block.GetInstanceId(), $"{good} must pile as its own model rather than as the plain block");
                    pile.Stock.Fill(good, int.MaxValue, 1, data); packer.Stock.Fill(good, int.MaxValue, 1, data);
                    Require(ModelSteps.Triangles(pile.Root) <= 2500, $"a pile full of {good} must stay a light building: {ModelSteps.Triangles(pile.Root)} triangles");
                    Require(ModelSteps.Triangles(packer.Root) <= 2500, $"a packing station full of {good} must stay a light building: {ModelSteps.Triangles(packer.Root)} triangles");
                }
            } finally { packer.Root.Free(); }
            // Three open cans stand together, each eight-sided: a wall and a rim of sixteen triangles and a hollow of eight.
            Require(ModelSteps.Piled(pile, thing, "tin_can", data).GetFaces().Length / 3 == 120, "tin cans must pile three to a slot");
        } finally { pile.Root.Free(); }
    }
}
