using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>What the player does by hand in the simulation checks.</summary>
internal static class PlayerSteps {
    /// <summary>The slot of a backpack a stack of an item lies in, or -1.</summary>
    public static int SlotOf(InventoryView box, string item) => box.Layout.ToList().FindIndex(s => s.Item == item);

    /// <summary>The slot of a building's inventory a stack of an item lies in, or -1.</summary>
    public static int SlotOf(Inventory box, string item) => box.Layout.FindIndex(s => s.Item == item);

    /// <summary>Puts units of the held stack into storage, as a storage slot does: into slot <paramref name="slot"/>, or with -1 wherever the inventory takes them.</summary>
    public static void PutDown(World world, Inventory to, int slot, int count) => world.FromHeldStack(() => {
        if (world.Held is not HeldStack held || world.HeldBox is not Inventory from) return;
        if (slot >= 0) world.TransferSlot(from, to, held.Slot, slot, held.Item, count); else world.Transfer(from, to, held.Item, count);
    });

    /// <summary>Binds a stack to a hotbar slot the way a player does: picks it up, puts it on the slot, and lets go.</summary>
    public static void PutOnHotbar(World world, Inventory box, int cell, int slot) {
        world.Hold(box, cell, int.MaxValue);
        world.FromHeldStack(() => world.SetHotbar(slot, box.Layout[cell].Item));
        world.Release();
    }

    /// <summary>Binds a backpack stack to a hotbar slot.</summary>
    public static void PutOnHotbar(World world, int cell, int slot) => PutOnHotbar(world, world.Backpack.Inventory, cell, slot);

    /// <summary>Moves <paramref name="count"/> units of a backpack stack into the first empty backpack cell, returning that cell.</summary>
    public static int SplitStack(World world, int cell, int count) {
        int empty = SlotOf(world.Backpack, "");
        world.TransferSlot(world.Backpack, world.Backpack, cell, empty, world.Backpack.Layout[cell].Item, count);
        return empty;
    }

    /// <summary>Puts units of the held stack into a backpack slot.</summary>
    public static void PutDown(World world, InventoryView to, int slot, int count) => PutDown(world, to.Inventory, slot, count);

    /// <summary>Fills a crate at the player's feet and picks it up, returning the packed crate item now in the backpack.</summary>
    public static string PackCrate(World world, params (string Item, int Count)[] goods) {
        var crate = world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        foreach (var (item, count) in goods) Require(crate.Inventory.Add(item, count) == count, $"a crate should take {count} {item}");
        var before = world.Backpack.Items.Keys.ToHashSet();
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        return world.Backpack.Items.Keys.Single(id => !before.Contains(id));
    }

    /// <summary>
    /// Packs <paramref name="count"/> crates of berries as a player does and stores them in new chests along a row from
    /// <paramref name="from"/>, five to a chest, then puts the player back where they stood; returns the chests.
    /// </summary>
    public static List<Thing> StoreCrates(World world, Vector2 from, int count) {
        var stood = world.Player;
        var chests = new List<Thing>();
        for (var at = from; count > 0; at += new Vector2(2, 0)) {
            var chest = world.Add(ThingKind.Chest, at);
            chests.Add(chest);
            Warp(world, at + new Vector2(0, 1));
            for (int i = 0; i < 5 && count > 0; i++, count--) {
                string packed = PackCrate(world, ("berries", world.Data.CrateCapacity));
                Require(world.Transfer(world.Backpack, chest.Inventory, packed, 1) == 1, $"a packed crate should go into the chest at {chest.Pos}");
            }
        }
        Warp(world, stood);
        return chests;
    }

    public static void GatherUntil(World world, Thing resource, string item, int quantity) {
        world.Player = resource.Pos;
        int attempts = 0;
        while (world.Backpack[item] < quantity && attempts++ < 200) {
            int before = world.Backpack[item];
            world.Gather(resource, world.Data.GatherSeconds);
            Require(world.Backpack[item] > before, $"gathering {item} should produce raw material");
            SafeTick(world, world.Data.GatherSeconds);
        }
        Require(world.Backpack[item] >= quantity, $"gathered enough {item} for the progression");
    }

    public static void PickUp(World world, Thing loose) {
        world.Player = loose.Pos;
        world.Gather(loose, 0);
        Require(world.Find(loose.Id) == null, $"picking up {loose.Kind} should take the whole piece at once");
    }

    public static Thing BuildAt(World world, ThingKind kind, Vector2 position) {
        world.Player = position + new Vector2(-1.5f, 0);
        var built = world.Construction.Build(kind, position);
        Require(built != null, $"build {kind} at a clear nearby location");
        return built!;
    }

    /// <summary>Packs an empty crate from the backpack beside the post with goods from the backpack, picks it up and puts it on the post's cart.</summary>
    public static void LoadCrate(World world, Thing sign, string item, int count) {
        world.Player = sign.Pos;
        var box = world.PlaceCrate("crate", sign.Pos + new Vector2(0, 2));
        Require(box != null, "an empty crate should be placed from the backpack");
        Require(world.Transfer(world.Backpack, box!.Inventory, item, count) == count, $"{count} {item} should move from the backpack into the crate");
        Require(world.PickUpCrate(box), "the packed crate should be picked up");
        string packed = world.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix));
        Require(world.Packed(packed)?[item] == count && world.Transfer(world.Backpack, sign.Inventory, packed, 1) == 1, $"the crate of {item} should go on the cart");
    }

    public static void CompleteBatches(World world, Thing station, string output, int quantity, bool pump = false) {
        int limit = 3000;
        while (station.Inventory[output] < quantity && limit-- > 0) {
            if (pump && station.Heat < 90) world.Pump(station);
            SafeTick(world, Step);
        }
        Require(limit > 0 && station.Inventory[output] >= quantity,
                $"station should complete {quantity} {output} batch(es): kind={station.Kind}, recipe={station.Recipe}, output={station.Inventory[output]}, processing={station.Processing}, progress={station.Progress:0.0}, player={world.Player}, stationPos={station.Pos}, health={world.Health:0.0}, time={world.Time:0.0}");
    }

    public static void CraftByHand(World world, string recipe, int times) {
        for (int i = 0; i < times; i++) Require(world.Craft(recipe), $"{recipe} should be craftable by hand: {world.CraftBlock(recipe)}");
        for (int limit = 400; (world.Crafting.Count > 0 || world.Making != "") && limit > 0; limit--) SafeTick(world, Step);
        Require(world.Crafting.Count == 0 && world.Making == "", $"hand crafting {recipe} should finish: rate={world.CraftRate}, progress={world.CraftTime}, slots={world.Backpack.UsedSlots}, items={string.Join(",", world.Backpack.Items.Select(p => p.Key + "=" + p.Value))}, room={world.Backpack.Room(world.Data.Recipes[recipe].Output)}");
    }

    /// <summary>
    /// A bench beside the player with a wooden mallet slotted and a batch of <paramref name="recipe"/> queued with its
    /// inputs from the backpack, left twenty seconds to make it.
    /// </summary>
    public static Thing BenchLeftWithAMallet(World world, string recipe) {
        var bench = world.Add(ThingKind.Bench, Site(0, 4));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        Give(world, "wood_hammer", 1);
        Require(world.SlotTool(bench, "wood_hammer"), "a wooden mallet should fit the hammer slot");
        world.QueueBatch(bench, recipe);
        SafeTick(world, 20);
        return bench;
    }

    /// <summary>Swaps the mallet in a bench's hammer slot for a steel hammer.</summary>
    public static void SwapMalletForSteelHammer(World world, Thing bench) {
        world.UnslotTool(bench, "wood_hammer");
        Give(world, "steel_hammer", 1);
        world.SlotTool(bench, "steel_hammer");
    }
}
