using System;
using System.Globalization;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>A portable jar holding up to ten firebugs, each with the same expiry it had in its stack.</summary>
public sealed class Lanterns(World world) {
    public const string Item = "lantern";
    public const string EmptyName = "Empty lantern";
    public const int Capacity = 10;
    /// <summary>The firebugs a refill takes out of the backpack, kept to take without allocating.</summary>
    private readonly Item[] refill = new Item[Capacity];
    private const string Prefix = Item + "_";
    private const int NumberLength = 8;
    private static readonly int HeaderLength = Prefix.Length + NumberLength;

    /// <summary>Names the lantern by its permanent number, followed by the times its firebugs die.</summary>
    private static string WithFirebugs(int id, ReadOnlySpan<float> expires) {
        var text = new StringBuilder(HeaderLength + expires.Length * NumberLength);
        Span<char> number = stackalloc char[NumberLength];
        text.Append(Prefix);
        id.TryFormat(number, out _, "X8", CultureInfo.InvariantCulture);
        text.Append(number);
        foreach (float expiry in expires) {
            BitConverter.SingleToInt32Bits(expiry).TryFormat(number, out _, "X8", CultureInfo.InvariantCulture);
            text.Append(number);
        }
        return text.ToString();
    }

    private static float ReadExpiry(ReadOnlySpan<char> number) => ReadNumber(number, out int bits) ? BitConverter.Int32BitsToSingle(bits) : float.NaN;
    private static bool ReadNumber(ReadOnlySpan<char> number, out int value) => int.TryParse(number, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);

    public static int Id(string? item) {
        if (item == null || item.Length < HeaderLength || item.Length > HeaderLength + Capacity * NumberLength
            || (item.Length - HeaderLength) % NumberLength != 0 || !item.StartsWith(Prefix, StringComparison.Ordinal)) return 0;
        return ReadNumber(item.AsSpan(Prefix.Length, NumberLength), out int id) && id > 0 ? id : 0;
    }

    internal static bool IsItem(string? item) => Id(item) != 0;
    /// <summary>The shared lantern item for a lantern of any charge, or the item itself.</summary>
    internal static string Kind(string item) => IsItem(item) ? Item : item;
    internal static bool ValidItem(string? item) {
        if (!IsItem(item)) return false;
        float previous = 0;
        for (int at = HeaderLength; at < item!.Length; at += NumberLength) {
            float expiry = ReadExpiry(item.AsSpan(at, NumberLength));
            if (!float.IsFinite(expiry) || expiry <= 0 || expiry < previous) return false;
            previous = expiry;
        }
        return true;
    }

    internal static bool Valid(Thing t) => t.Kind == ThingKind.Lantern ? ValidItem(t.LanternItem) : t.LanternItem == null;
    public int BugCount(string? item) => Living(item, default);
    /// <summary>A lantern's name, which says when it holds no living firebugs.</summary>
    internal string Name(int bugs) => bugs == 0 ? EmptyName : world.Data.Name(Item);

    /// <summary>The stack of living firebugs in this lantern, with their average game minutes left.</summary>
    internal (int Count, int AverageMinutes) FirebugStack(string? item) {
        Span<float> dates = stackalloc float[Capacity];
        int count = Living(item, dates);
        return (count, count == 0 ? 0 : world.Firebugs.MinutesLeft(Firebugs.Average(dates[..count])));
    }

    private int Living(string? item, Span<float> copy) {
        if (!IsItem(item)) return 0;
        int count = 0;
        for (int at = HeaderLength; at < item!.Length; at += NumberLength) {
            float expiry = ReadExpiry(item.AsSpan(at, NumberLength));
            if (!(expiry > world.Time)) continue;
            if (!copy.IsEmpty) copy[count] = expiry;
            count++;
        }
        return count;
    }

    internal string NewEmpty() => WithFirebugs(world.S.NextId++, default);

    /// <summary>Fills a lantern for the visual review fixture with freshly caught firebugs.</summary>
    internal string FillForReview(string item, int bugs) {
        Span<float> expires = stackalloc float[Math.Clamp(bugs, 0, Capacity)];
        expires.Fill(world.Firebugs.NewExpiry);
        return WithFirebugs(Id(item), expires);
    }

    /// <summary>The named lantern in the acting player's backpack, with whatever it holds now.</summary>
    public string? Carried(int id) => Carried(world.S.Backpack, id);

    internal static string? Carried(Inventory backpack, int id) {
        if (id == 0) return null;
        foreach (var pair in backpack.Items) if (Id(pair.Key) == id) return pair.Key;
        return null;
    }

    /// <summary>The lantern the player took in hand, regardless of the tool selected or its current charge.</summary>
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
        if ((world.MarkedSource ?? world.S.Backpack).Remove(item, 1) != 1) return null;
        KeepInHand(world.S.Actor);
        var lantern = world.Add(ThingKind.Lantern, at, rotation: rotation);
        lantern.LanternItem = item;
        world.ClearGround(lantern);
        world.Play("place_building", at);
        return lantern;
    }

    /// <summary>Lifts a placed lantern back into the backpack.</summary>
    public bool PickUp(Thing lantern) {
        if (lantern.Kind != ThingKind.Lantern || lantern.LanternItem is not string item || world.Find(lantern.Id) != lantern) return false;
        if (!world.Near(lantern)) { world.Say("Walk closer to pick up the lantern."); return false; }
        if (world.S.Backpack.Room(item) == 0) { world.Say("Make room in your backpack for the lantern."); return false; }
        if (world.Busy() || world.Forwarded(Verb.PickUpLantern, lantern.Id)) return true;
        world.S.Backpack.Add(item, 1);
        world.S.Things.Remove(lantern);
        world.Play("wood_rocks_inventory", lantern.Pos);
        return true;
    }

    /// <summary>Moves up to the requested number of firebugs into the lantern, keeping each bug's lifetime.</summary>
    public bool Refill(int id, Thing? placed = null, int count = Capacity) {
        string? item = placed == null ? Carried(id) : placed.LanternItem;
        if (item == null || id == 0 || Id(item) != id || count <= 0
            || placed != null && (placed.Kind != ThingKind.Lantern || world.Find(placed.Id) != placed)) return false;
        if (placed != null && !world.Reaches(placed)) { world.Say("Walk closer to refill the lantern."); return false; }
        Span<float> expires = stackalloc float[Capacity];
        int living = Living(item, expires);
        int room = Math.Min(count, Capacity - living);
        if (room == 0) { world.Say("The lantern is full."); return false; }
        if (world.Busy() || world.Forwarded(Verb.RefillLantern, placed?.Id ?? 0, other: id, item: Firebugs.Item, count: room,
            at: new Vector2(world.S.Backpack.PreferredSlot, 0))) return true;
        var bag = world.S.Backpack;
        if (placed == null && !bag.Layout.Exists(slot => slot.Item == item && slot.Held == 0)) return false;
        int moved = bag.TakeUnits(Firebugs.Item, refill.AsSpan(0, room));
        if (moved == 0) { world.Say("No firebugs in your backpack."); return false; }
        for (int i = 0; i < moved; i++) { expires[living + i] = refill[i].Expires; refill[i] = null!; }
        expires = expires[..(living + moved)];
        expires.Sort();
        var filled = WithFirebugs(id, expires);
        if (placed != null) placed.LanternItem = filled;
        else bag.Replace(item, filled);
        return true;
    }
}
