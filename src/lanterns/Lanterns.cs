using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A portable jar holding up to ten firebugs, each keeping the time it dies.</summary>
public sealed class Lanterns(World world) {
    public const string Item = "lantern";
    public const string EmptyName = "Empty lantern";
    public const int Capacity = 10;

    /// <summary>The number a lantern's key finds it by, or 0 for anything else.</summary>
    public static int Id(string? item) => item != null && ChopChopMeow.Item.IsKind(item, Item) ? ChopChopMeow.Item.IdOf(item) : 0;
    internal static bool IsItem(string? item) => Id(item) != 0;

    /// <summary>A new lantern with an empty jar, for a box to take.</summary>
    internal ChopChopMeow.Item NewEmpty() => Empty(world.S.NextId++);
    /// <summary>A lantern with this number and an empty jar.</summary>
    internal static ChopChopMeow.Item Empty(int id) => new() { Kind = Item, Id = id, Contents = new Inventory { Slots = 1, StackSize = Capacity } };

    /// <summary>A lantern whose jar holds nothing but up to ten firebugs, each with its time.</summary>
    internal static bool Valid(ChopChopMeow.Item lantern) =>
        lantern.Kind == Item && lantern.Id > 0 && lantern.Contents is { Slots: 1, StackSize: Capacity, CarriedBy: Inventory.Carrier.None } jar
        && jar.Items.Count == (jar.Count == 0 ? 0 : 1) && jar.Count == jar[Firebugs.Item] && jar.ValidUnits(_ => false);
    internal static bool Valid(Thing t) => t.Kind == ThingKind.Lantern ? t.Lantern is { } lantern && Valid(lantern) : t.Lantern == null;

    /// <summary>The live firebugs in a lantern, or 0 for none or no lantern.</summary>
    public static int BugCount(ChopChopMeow.Item? lantern) => lantern?.Contents?[Firebugs.Item] ?? 0;
    /// <summary>The live firebugs in the lantern a box's cell holds by this key.</summary>
    public static int BugCount(Inventory box, string? item) => item == null ? 0 : BugCount(box.Object(item));
    public static int BugCount(InventoryView box, string? item) => BugCount(box.Inventory, item);
    /// <summary>A lantern's name, which says when it holds no living firebugs.</summary>
    internal string Name(int bugs) => bugs == 0 ? EmptyName : world.Data.Name(Item);

    /// <summary>The stack of living firebugs in this lantern, with their average game minutes left.</summary>
    internal (int Count, int AverageMinutes) FirebugStack(ChopChopMeow.Item? lantern) {
        int count = BugCount(lantern);
        return (count, count == 0 ? 0 : world.Firebugs.MinutesLeft(lantern!.Contents!.AverageExpiry(Firebugs.Item)));
    }

    internal (int Count, int AverageMinutes) FirebugStack(InventoryView box, string? item) => FirebugStack(item == null ? null : box.Inventory.Object(item));

    /// <summary>Fills a lantern for the visual review fixture with freshly caught firebugs.</summary>
    internal void FillForReview(ChopChopMeow.Item lantern, int bugs) => world.Firebugs.Catch(lantern.Contents!, Math.Clamp(bugs, 0, Capacity));

    /// <summary>The named lantern's key in the acting player's backpack.</summary>
    public string? Carried(int id) => Carried(world.S.Backpack, id);

    internal static string? Carried(Inventory backpack, int id) {
        if (id == 0) return null;
        foreach (var pair in backpack.Items) if (Id(pair.Key) == id) return pair.Key;
        return null;
    }

    /// <summary>The lantern the player took in hand, regardless of the tool selected.</summary>
    internal static string? InHand(PlayerState player) => Carried(player.Backpack, player.LanternInHand);

    internal static void KeepInHand(PlayerState player) {
        if (player.LanternInHand != 0 && InHand(player) == null) player.LanternInHand = 0;
    }

    /// <summary>Takes the carried lantern in hand or puts it back if it is already equipped.</summary>
    internal void ToggleInHand(int id) {
        if (id == 0 || Carried(id) == null || world.Busy() || world.Forwarded(Verb.ToggleLanternInHand, id)) return;
        var player = world.S.Actor;
        bool stow = player.LanternInHand == id;
        player.LanternInHand = stow ? 0 : id;
        if (!stow && (player.TorchLit || player.TorchKindling > 0)) world.PutOutTorch();
    }

    /// <summary>Puts down the lantern without changing how long its firebugs have left.</summary>
    public Thing? Place(string item, Vector2 at, float rotation = 0) {
        if (!IsItem(item)) return null;
        if (world.Construction.PlacementBlock(ThingKind.Lantern, at, item, rotation) is string blocked) { world.Say(blocked); return null; }
        if (world.Busy() || world.Forwarded(Verb.PlaceLantern, item: item, count: world.S.Backpack.PreferredSlot, at: at, rotation: rotation)) return null;
        if ((world.MarkedSource ?? world.S.Backpack).TakeObject(item) is not { } unit) return null;
        KeepInHand(world.S.Actor);
        var lantern = world.Add(ThingKind.Lantern, at, rotation: rotation);
        lantern.Lantern = unit;
        world.ClearGround(lantern);
        world.Play("place_building", at);
        return lantern;
    }

    /// <summary>Lifts a placed lantern back into the backpack.</summary>
    public bool PickUp(Thing lantern) {
        if (lantern.Kind != ThingKind.Lantern || lantern.Lantern is not { } unit || world.Find(lantern.Id) != lantern) return false;
        if (!world.Near(lantern)) { world.Say("Walk closer to pick up the lantern."); return false; }
        if (world.S.Backpack.Room(unit.Key) == 0) { world.Say("Make room in your backpack for the lantern."); return false; }
        if (world.Busy() || world.Forwarded(Verb.PickUpLantern, lantern.Id)) return true;
        world.S.Backpack.Add(unit);
        world.S.Things.Remove(lantern);
        world.Play("wood_rocks_inventory", lantern.Pos);
        return true;
    }

    /// <summary>Moves up to the requested number of firebugs into the lantern, keeping each bug's lifetime.</summary>
    public bool Refill(int id, Thing? placed = null, int count = Capacity) {
        var bag = world.S.Backpack;
        string? item = placed == null ? Carried(id) : placed.Lantern?.Key;
        if (item == null || id == 0 || Id(item) != id || count <= 0
            || placed != null && (placed.Kind != ThingKind.Lantern || world.Find(placed.Id) != placed)) return false;
        if (placed != null && !world.Reaches(placed)) { world.Say("Walk closer to refill the lantern."); return false; }
        var lantern = placed?.Lantern ?? bag.Object(item);
        int room = Math.Min(count, Capacity - BugCount(lantern));
        if (room == 0) { world.Say("The lantern is full."); return false; }
        if (world.Busy() || world.Forwarded(Verb.RefillLantern, placed?.Id ?? 0, other: id, item: Firebugs.Item, count: room,
            at: new Vector2(bag.PreferredSlot, 0))) return true;
        if (placed == null && !bag.Layout.Exists(slot => slot.Item == item && slot.Held == 0)) return false;
        if (bag.TransferTo(lantern!.Contents!, Firebugs.Item, room) == 0) { world.Say("No firebugs in your backpack."); return false; }
        return true;
    }
}
