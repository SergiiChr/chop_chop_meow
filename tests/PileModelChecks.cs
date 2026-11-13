using System;
using System.Linq;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>A pile heaps up the good it holds from its inventory, without changing stock or sharing what it shows with another pile.</summary>
public static class PileModelChecks {
    public static void Run(GameData data) {
        PileShape.Warm(data);
        var thing = new Thing { Kind = ThingKind.Pile, Inventory = new Inventory { Slots = 14 } };
        var first = (PileProp)WorldModels.Build(thing); var second = (PileProp)WorldModels.Build(thing);
        try {
            thing.Inventory.Add("wood", 14 * 64);
            WorldModels.SyncPile(first, thing, data);
            WorldModels.SyncPile(second, thing, data);
            // A log is a seven-sided wall of fourteen triangles and two ends of five, less the side it lies on: 2,390 triangles of heap hold 108.
            Require(first.Stock.Count == 108, $"a full pile of wood must heap as many logs as its triangles allow: {first.Stock.Count}");
            var logs = first.Stock.Piece!;
            Require(second.Stock.Piece!.GetInstanceId() == logs.GetInstanceId(), "piles must share cached heap geometry");

            Require(ModelSteps.Piled(first, thing, "wood", data).GetInstanceId() == logs.GetInstanceId() && first.Stock.Count == 8,
                $"a pile with one of its fourteen stacks filled must show a fourteenth of its heap, rounded up: {first.Stock.Count}");
            Require(second.Stock.Count == 108, "a refresh must leave another pile's remembered appearance alone");
            Require(ModelSteps.Piled(first, thing, "stone", data).GetInstanceId() != logs.GetInstanceId(), "replacing wood with stone must change the model");

            var coal = ModelSteps.Piled(first, thing, "coal", data);
            var charcoal = ModelSteps.Piled(first, thing, "charcoal", data);
            // A briquette is six sides, six hole walls and two ringed ends of six, two triangles apiece, less the side it lies on and the hole's roof.
            Require(charcoal.GetInstanceId() != coal.GetInstanceId() && charcoal.GetFaces().Length / 3 == 44, $"charcoal must heap as holed briquettes rather than as coal's lumps: {charcoal.GetFaces().Length / 3}");

            var fallback = ModelSteps.Piled(first, thing, "unmodelled_item", data);
            Require(ModelSteps.Piled(first, thing, "another_unmodelled_item", data).GetInstanceId() == fallback.GetInstanceId(), "unmodelled items must use the shared fallback");
            Require(first.Stock.Material is StandardMaterial3D material && material.AlbedoColor.IsEqualApprox(new Color("b0a58a")), "unknown items must have a visible fallback color");

            thing.Inventory.Clear();
            WorldModels.SyncPile(first, thing, data);
            Require(first.Stock.Count == 0, "emptying a pile must hide all contents");

            // Stations showing piled goods use the same heap, so a good looks the same on them as in a storage pile.
            var furnace = (FurnaceProp)WorldModels.Build(new Thing { Kind = ThingKind.Furnace, Burner = new Inventory() });
            var packer = (PackerProp)WorldModels.Build(new Thing { Kind = ThingKind.Packer });
            try {
                var fire = new Thing { Kind = ThingKind.Furnace, Burner = new Inventory() }; fire.Burner.Add("wood", 3);
                WorldModels.SyncFuel(furnace, fire, data);
                Require(furnace.Stock.Count == 3 && furnace.Stock.Piece == logs, "a furnace should show its stocked wood as storage pile logs, one for each");
                packer.Stock.Fill("wood", .5f, data);
                Require(packer.Stock.Count > 0 && packer.Stock.Piece == logs, "a packing station should heap its goods as a storage pile does");
            } finally { furnace.Root.Free(); packer.Root.Free(); }
        } finally { first.Root.Free(); second.Root.Free(); }
    }

    /// <summary>
    /// Every item's icon and the workers' hard hat are rendered from models, and every item heaps as its model rather
    /// than as plain blocks, light enough for a full pile and a full packing station.
    /// </summary>
    public static void Goods(GameData data) {
        foreach (string item in data.Items.Keys.Append("hardhat"))
            Require(ResourceLoader.Exists($"res://assets/icons/{item}.png"), $"{item} must have an icon rendered from its model");
        PileShape.Warm(data);
        var thing = new Thing { Kind = ThingKind.Pile, Inventory = new Inventory { Slots = 14 } };
        var pile = (PileProp)WorldModels.Build(thing);
        var packer = (PackerProp)WorldModels.Build(new Thing { Kind = ThingKind.Packer });
        try {
            var block = ModelSteps.Piled(pile, thing, "unmodelled_item", data);
            // A pile and a packing station are buildings, each held to 2,500 triangles with a full heap showing.
            foreach (string good in data.Items.Keys) {
                Require(ModelSteps.Piled(pile, thing, good, data).GetInstanceId() != block.GetInstanceId(), $"{good} must heap as its own model rather than as plain blocks");
                pile.Stock.Fill(good, 1, data); packer.Stock.Fill(good, 1, data);
                Require(ModelSteps.Triangles(pile.Root) <= 2500, $"a pile full of {good} must stay a light building: {ModelSteps.Triangles(pile.Root)} triangles");
                Require(ModelSteps.Triangles(packer.Root) <= 2500, $"a packing station full of {good} must stay a light building: {ModelSteps.Triangles(packer.Root)} triangles");
            }
        } finally { pile.Root.Free(); packer.Root.Free(); }
    }

    /// <summary>What a heap is made of and how it is laid: six-packs of cans and bars, a loaf in its own shape, upper pieces over the gaps, and compost as one mound.</summary>
    public static void Heaps(GameData data) {
        PileShape.Warm(data);
        var thing = new Thing { Kind = ThingKind.Pile, Inventory = new Inventory { Slots = 14 } };
        var pile = (PileProp)WorldModels.Build(thing);
        try {
            // Six open cans stand in two rows of three, each eight-sided: a wall of sixteen triangles and a hollow of eight.
            Require(ModelSteps.Piled(pile, thing, "tin_can", data).GetFaces().Length / 3 == 144, "tin cans must heap as six-packs");
            // An ingot is 28 triangles; of six, the flat undersides and the three tops the upper layer covers are left out, two triangles each.
            Require(ModelSteps.Piled(pile, thing, "iron_bar", data).GetFaces().Length / 3 == 150, "bars must heap as six-packs, three across three");

            var one = PileShape.Stock("bread", data.Items["bread"].Color);
            Vector3 loaf = one.Mesh.GetAabb().Size, piled = ModelSteps.Piled(pile, thing, "bread", data).GetAabb().Size;
            one.Free();
            Require(MathF.Abs(piled.X / piled.Z - loaf.X / loaf.Z) < .01f && MathF.Abs(piled.X / piled.Y - loaf.X / loaf.Y) < .01f, "a heaped loaf must keep its shape");

            // The first crate above the ground stands over the gap between four already laid, half a crate from each.
            Vector3 crate = ModelSteps.Piled(pile, thing, "crate", data, 14 * 64).GetAabb().Size;
            int raised = Enumerable.Range(0, pile.Stock.Count).First(i => pile.Stock.Place(i).Origin.Y > .01f);
            var above = pile.Stock.Place(raised).Origin;
            int under = Enumerable.Range(0, raised).Select(i => pile.Stock.Place(i).Origin - above)
                .Count(off => off.Y < 0 && MathF.Abs(MathF.Abs(off.X) - crate.X * .52f) < .01f && MathF.Abs(MathF.Abs(off.Z) - crate.Z * .52f) < .01f);
            Require(under == 4, $"a crate on the second level must stand over four already laid: {under}");

            thing.Compost = true;
            var mound = ModelSteps.Piled(pile, thing, "leaves", data, 100);
            float small = pile.Stock.Place(0).Basis.Scale.X;
            thing.Inventory.Add("fertilizer", 300);
            WorldModels.SyncPile(pile, thing, data);
            Require(pile.Stock.Count == 1 && pile.Stock.Piece == mound && pile.Stock.Place(0).Basis.Scale.X > small, "a compost pile must show one mound that swells with all it holds");
        } finally { pile.Root.Free(); }
    }
}
