namespace ChopChopMeow;

/// <summary>What a bottle, bucket or barrel holds, read from its object; an empty one is a plain item with no object.</summary>
public static class Vessels {
    /// <summary>The liters a container holds, 0 for an empty one.</summary>
    public static int LitersIn(Item? container) => container?.Contents?.Count ?? 0;
    /// <summary>The liquid a container holds, or null for an empty one.</summary>
    public static string? LiquidIn(Item? container) => container?.Contents?.First();
    /// <summary>The icon and model id an item shows: a bottle's at its fill, such as <c>water_2</c>, or its kind's.</summary>
    public static string LookOf(GameData data, Inventory? box, string item) {
        string kind = Item.KindOf(item);
        var unit = box?.Object(item);
        return LiquidIn(unit) is string liquid && data.VesselOf(kind, liquid) is VesselDef v ? v.Look(LitersIn(unit)) : kind;
    }
    /// <summary>How many containers of a kind a box holds with exactly so many liters of a liquid; 0 liters counts the empty ones.</summary>
    public static int Count(Inventory box, string container, string liquid, int liters) {
        if (liters <= 0) return box[container];
        int count = 0;
        foreach (var slot in box.Layout) if (Holds(slot, container, liquid, liters)) count++;
        return count;
    }
    public static int Count(InventoryView box, string container, string liquid, int liters) => Count(box.Inventory, container, liquid, liters);
    /// <summary>The first container of a kind in a box holding exactly so many liters of a liquid, the plain item for 0 liters, or null for none.</summary>
    public static string? First(Inventory box, string container, string liquid, int liters) {
        if (liters <= 0) return box[container] > 0 ? container : null;
        foreach (var slot in box.Layout) if (Holds(slot, container, liquid, liters)) return slot.Item;
        return null;
    }
    public static string? First(InventoryView box, string container, string liquid, int liters) => First(box.Inventory, container, liquid, liters);
    /// <summary>The liters of what a container in a box holds, 0 for an empty one.</summary>
    public static int LitersIn(Inventory box, string item) => LitersIn(box.Object(item));
    public static int LitersIn(InventoryView box, string item) => LitersIn(box.Inventory.Object(item));
    /// <summary>The liquid a container in a box holds, or null for an empty one.</summary>
    public static string? LiquidIn(InventoryView box, string item) => LiquidIn(box.Inventory.Object(item));

    private static bool Holds(Inventory.Slot slot, string container, string liquid, int liters) =>
        slot.Units is [{ } unit] && unit.Kind == container && LiquidIn(unit) == liquid && LitersIn(unit) == liters;

    /// <summary>A container holding liters of a liquid, as a new object numbered <paramref name="id"/>.</summary>
    public static Item Holding(string container, int id, int capacity, string liquid, int liters) {
        var contents = new Inventory { Slots = 1, StackSize = capacity, Capacity = capacity };
        contents.Add(liquid, liters);
        return new Item { Kind = container, Id = id, Contents = contents };
    }
}
