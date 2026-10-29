using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One player's body and belongings.
/// The host's own player is saved with the world; a guest's player is in the world only while connected and is saved on the guest's machine.
/// </summary>
public sealed class PlayerState {
    /// <summary>Network peer id: 1 for the host and in single player.</summary>
    public int Id { get; set; } = 1;
    public float PlayerX { get; set; } = Terrain.Spawn.X;
    public float PlayerY { get; set; } = Terrain.Spawn.Y;
    public float Health { get; set; } = 100;
    public float Hunger { get; set; } = 100;
    public float Thirst { get; set; } = 100;
    public Inventory Backpack { get; set; } = new() { Slots = 24 };
    public List<string> HotbarSlots { get; set; } = new() { "steel_axe", "steel_pick", "chest", "campfire", "furnace", "anvil", "bench", "berries", "water" };
    public int ActiveHotbar { get; set; }
    /// <summary>Recipes queued in the personal crafting menu; their inputs are already taken from the backpack.</summary>
    public List<string> Crafting { get; set; } = new();
    /// <summary>Seconds spent on the first queued recipe, at the speed of the tool used.</summary>
    public float CraftTime { get; set; }
    /// <summary>Burn time left on the torch in the player's left hand, or zero when the hand is empty; it only burns while lit.</summary>
    public float Torch { get; set; }
    public bool TorchLit { get; set; }
    /// <summary>Seconds left striking flint and steel before the torch catches.</summary>
    public float TorchKindling { get; set; }
    public float RespawnIn { get; set; }
    /// <summary>A wolf has pinned the player; it stays set through the following death until respawn.</summary>
    public bool Mauled { get; set; }
    /// <summary>Id of the cabin the player is inside, or zero; inside, wolves back off and the player is hidden.</summary>
    public int Sheltered { get; set; }
    /// <summary>The resource the player is working on, so other machines can show it.</summary>
    public int GatherTarget { get; set; }
    /// <summary>The item in hand, so other machines can show it.</summary>
    public string Held { get; set; } = "";
    /// <summary>Moving with Shift held, which drains food and drink faster; carried so other machines show the run.</summary>
    public bool Sprinting { get; set; }
    /// <summary>Seconds left of the brighter flare that follows a torch swing.</summary>
    public float TorchFlare { get; set; }
    /// <summary>Counts moves the simulation made, such as a respawn; a guest only takes its own position from the host when it changes.</summary>
    public int Warp { get; set; }
    /// <summary>Which shirt colour tells this player apart: 0 for the host, and the first free one for each guest.</summary>
    public int Shirt { get; set; }
    /// <summary>Contents of the packed crates in the backpack; only a guest's own save carries them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<int, Inventory>? Crates { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(PlayerX, PlayerY); set { PlayerX = value.X; PlayerY = value.Y; } }
    // Only the host's simulation uses these.
    internal float GatherClock, AttackClock, NightSpawn;
    internal bool Busy, PackAttack;
    internal readonly List<float> TorchSwings = new();
}

/// <summary>A player action a guest sends to the host, which runs it for that guest. What each field means depends on the verb.</summary>
public readonly record struct Command(string Verb, int Target, int Other, string Item, int Count, Vector2 At);

/// <summary>The players in the world and the guest side of actions.</summary>
public sealed partial class World {
    /// <summary>The player at this machine; the host and a single-player game are 1, a guest gets its peer id.</summary>
    public int LocalId { get; set; } = 1;
    public PlayerState Me {
        get {
            foreach (var p in S.Players) if (p.Id == LocalId) return p;
            return S.Players[0];
        }
    }
    /// <summary>Set while this world is a guest's copy of the host's: player actions go there instead of changing the copy.</summary>
    public Action<Command>? Host;
    public bool Remote => Host != null;
    /// <summary>Messages for players at other machines; null means all of them.</summary>
    public Action<PlayerState?, string>? Relay;
    // While the tick runs the shared world, what it says is for everyone.
    private bool everyone;

    private void SayToAll(string message) {
        bool was = everyone; everyone = true;
        Say(message); everyone = was;
    }

    /// <summary>Runs an action as another player: their backpack, position and needs, and they hear what it says.</summary>
    public void As(PlayerState p, Action action) { using (ActAs(p)) action(); }

    public T As<T>(PlayerState p, Func<T> action) { using (ActAs(p)) return action(); }

    /// <summary>Acts as another player until the returned scope ends; a struct, so code that runs every tick switches players without allocating.</summary>
    private Acting ActAs(PlayerState p) {
        var scope = new Acting(this, S.Actor, everyone);
        S.Actor = p; everyone = false;
        return scope;
    }

    private readonly struct Acting(World world, PlayerState actor, bool everyone) : IDisposable {
        public void Dispose() { world.S.Actor = actor; world.everyone = everyone; }
    }

    /// <summary>In a guest's copy, sends the action to the host and tells the caller to stop.</summary>
    private bool Forwarded(string verb, int target = 0, int other = 0, string item = "", int count = 0, Vector2 at = default) {
        Host?.Invoke(new Command(verb, target, other, item, count, at));
        return Host != null;
    }

    /// <summary>Host: runs a guest's action as that guest. The action checks reach and costs as it does for the host.</summary>
    public void Run(PlayerState p, Command c) => As(p, () => {
        var t = Find(c.Target);
        var w = S.Workers.Find(w => w.Id == c.Other);
        switch (c.Verb) {
            case "build": Build(c.Item, c.At); break;
            case "place_crate": PlaceCrate(c.Item, c.At); break;
            case "consume": S.Backpack.PreferredSlot = c.Count; try { Consume(c.Item); } finally { S.Backpack.PreferredSlot = -1; } break;
            case "torch": UseTorch(c.At); break;
            case "attack": Attack(); break;
            case "leave": Leave(); break;
            case "craft": Craft(c.Item); break;
            case "cancel_craft": CancelCraft(c.Count); break;
            case "transfer":
                if (Resolve(c.Target) is Inventory from && Resolve(c.Other) is Inventory to) {
                    from.PreferredSlot = c.At.Y == -1 ? (int)c.At.X : -1;
                    try { Transfer(from, to, c.Item, c.Count); } finally { from.PreferredSlot = -1; }
                }
                break;
            case "transfer_slot": if (Resolve(c.Target) is Inventory slotFrom && Resolve(c.Other) is Inventory slotTo) TransferSlot(slotFrom, slotTo, (int)c.At.X, (int)c.At.Y, c.Item, c.Count); break;
            case "sort_bag": SortBackpack(); break;
            case "skip_morning": SkipMorning(); break;
            case "assign": if (w != null) Assign(w, t); break;
            case "duty": if (w != null && t != null) SetDuty(w, t, c.Item, c.Count != 0); break;
            case "fire": if (w != null) Fire(w); break;
            case "settle": if (w != null) SettleDebt(w); break;
        }
        if (t == null) return;
        switch (c.Verb) {
            case "pick_up": if (t.IsLoose) Gather(t, 0); break;
            case "pump": Pump(t); break;
            case "fuel_fire": FuelFire(t); break;
            case "set_recipe": SetRecipe(t, c.Item); break;
            case "set_filter": SetFilter(t, c.Item, (c.Count & 1) != 0, (c.Count & 2) != 0); break;
            case "clear_filters": ClearFilters(t); break;
            case "set_compost": SetCompost(t, c.Count != 0); break;
            case "skip_food": SetSkipFood(t, c.Count != 0); break;
            case "supply_mine": SupplyMine(t); break;
            case "load_batch": LoadBatch(t, Math.Clamp(c.Count, 1, 5)); break;
            case "load_input": LoadInput(t, c.Item, c.Count); break;
            case "load_fuel": LoadFuel(t, c.Item, c.Count); break;
            case "collect": Collect(t); break;
            case "enter": Enter(t); break;
            case "sleep": Sleep(t); break;
            case "sell": Sell(t); break;
            case "buy": Buy(t, c.Item, Math.Min(c.Count, 5)); break;
            case "pick_up_crate": PickUpCrate(t); break;
            case "slot_tool": SlotTool(t, c.Item); break;
            case "unslot_tool": UnslotTool(t, c.Item); break;
            case "fetch_prices": FetchPrices(t); break;
            case "hire": if (c.Count >= 0 && c.Count < Data.HireOffers.Count) Hire(t, Data.HireOffers[c.Count]); break;
        }
    });

    /// <summary>
    /// An inventory as a number another machine can look up: 0 is the acting player's backpack, otherwise its holder's id and which of its boxes.
    /// </summary>
    private int Locate(Inventory inventory) {
        if (inventory == S.Backpack) return 0;
        foreach (var t in S.Things) {
            int box = Array.IndexOf(Boxes(t), inventory);
            if (box >= 0) return t.Id * BoxCodes + box + 1;
        }
        return -1;
    }

    public const int BoxCodes = 6;
    private static Inventory?[] Boxes(Thing t) => [t.Inventory, t.Output, t.Tools, t.Wages, t.Burner];
    private Inventory? Resolve(int at) => at == 0 ? S.Backpack : Find(at / BoxCodes) is not Thing t || at % BoxCodes == 0 ? null : Boxes(t)[at % BoxCodes - 1];

    /// <summary>A manual move between the backpack and a building, or two buildings, all within reach; automation filters do not apply.</summary>
    public int Transfer(Inventory from, Inventory to, string item, int quantity) {
        int a = Locate(from), b = Locate(to);
        if (a < 0 || b < 0 || Forwarded("transfer", a, b, item, quantity, new Vector2(from.PreferredSlot, -1))) return 0;
        if (new[] { a, b }.Where(at => at > 0).Any(at => !Near(Find(at / BoxCodes)!))) { Say("Walk closer first."); return 0; }
        if (b > 0 && BoxBlock(Find(b / BoxCodes)!, to, item) is string refused) { Say(refused); return 0; }
        int moved = from.TransferTo(to, item, quantity);
        // What goes into a fuel slot becomes the fuel workers keep it stocked with.
        if (moved > 0 && b > 0 && Find(b / BoxCodes) is Thing station && to == station.Burner) station.Fuel = item;
        return moved;
    }

    public void SortBackpack() {
        if (Downed || Forwarded("sort_bag")) return;
        S.Backpack.Sort();
    }

    public int TransferSlot(Inventory from, Inventory to, int sourceSlot, int targetSlot, string item, int quantity) {
        int a = Locate(from), b = Locate(to);
        if (a < 0 || b < 0 || Downed) return 0;
        if ((uint)sourceSlot >= (uint)from.Slots || (uint)targetSlot >= (uint)to.Slots) return 0;
        if (Forwarded("transfer_slot", a, b, item, quantity, new Vector2(sourceSlot, targetSlot))) return 0;
        if (a > 0 && !Near(Find(a / BoxCodes)!) || b > 0 && !Near(Find(b / BoxCodes)!)) { Say("Walk closer first."); return 0; }
        // Bring stacks in line with totals changed directly, then reject a stale drag rather than moving a different item.
        from.EnsureLayout();
        if ((uint)sourceSlot >= (uint)from.Layout.Count || from.Layout[sourceSlot].Item != item) return 0;
        if (from != to && b > 0 && BoxBlock(Find(b / BoxCodes)!, to, item) is string refused) { Say(refused); return 0; }
        to.EnsureLayout();
        // A drop on another item's cell in another inventory still stores the stack wherever it fits.
        int moved;
        if (from != to && to.Layout[targetSlot].Item is var there && there != "" && there != item) {
            from.PreferredSlot = sourceSlot;
            try { moved = from.TransferTo(to, item, quantity); } finally { from.PreferredSlot = -1; }
        } else moved = from.MoveSlotTo(sourceSlot, to, targetSlot, quantity);
        if (moved > 0 && b > 0 && Find(b / BoxCodes) is Thing station && to == station.Burner) station.Fuel = item;
        return moved;
    }

    public void SetFilter(Thing chest, string item, bool output, bool allowed) {
        if (!chest.IsStorage || !Data.Items.ContainsKey(item) || Forwarded("set_filter", chest.Id, item: item, count: (output ? 1 : 0) | (allowed ? 2 : 0))) return;
        var filter = output ? chest.Inventory.OutputFilter : chest.Inventory.InputFilter;
        if (allowed) filter.Add(item); else filter.Remove(item);
    }

    /// <summary>Stops or restarts workers bringing food to a compost.</summary>
    public void SetSkipFood(Thing pile, bool skip) {
        if (!pile.Compost || Forwarded("skip_food", pile.Id, count: skip ? 1 : 0)) return;
        pile.SkipFood = skip;
    }

    public void ClearFilters(Thing chest) {
        if (!chest.IsStorage || Forwarded("clear_filters", chest.Id)) return;
        chest.Inventory.InputFilter.Clear(); chest.Inventory.OutputFilter.Clear();
    }

    /// <summary>Gives new wolves and trade carts ids, which other machines tell them apart by.</summary>
    public void NumberWolvesAndOrders() {
        foreach (var w in S.Wolves.Where(w => w.Id == 0)) w.Id = S.NextId++;
        foreach (var o in S.Orders.Where(o => o.Id == 0)) o.Id = S.NextId++;
    }

    /// <summary>Host: puts a guest where their own machine walked them, as <see cref="MovePlayer"/> does for the host.</summary>
    public void MoveTo(Vector2 p) {
        if (Downed || S.Sheltered != 0 || p == Player) return;
        playerBusy = true; Player = p;
    }

    /// <summary>Host: puts a guest where their own machine walked them, acting as that guest.</summary>
    public void MoveTo(PlayerState p, Vector2 to) { using (ActAs(p)) MoveTo(to); }

    /// <summary>
    /// Host: brings a guest in with the player they saved on their machine.
    /// Anything missing, unknown or out of range gives them a new player instead: an empty backpack at the clearing.
    /// </summary>
    public PlayerState AddGuest(int id, string? saved) {
        var p = Adopt(saved) ?? new PlayerState();
        p.Id = id; p.Warp++; p.Shirt = Enumerable.Range(1, 3).First(i => S.Players.All(o => o.Shirt != i));
        S.Players.Add(p);
        NormalizeBottleStorage();
        SayToAll("A neighbor joined the settlement.");
        return p;
    }

    private PlayerState? Adopt(string? saved) {
        PlayerState? p;
        try { p = saved == null ? null : JsonSerializer.Deserialize<PlayerState>(saved, GameData.Json); } catch (JsonException) { return null; }
        if (p?.Backpack?.Items == null || p.HotbarSlots == null || p.Crafting == null) return null;
        var crates = p.Crates ?? new();
        bool Known(string item) => Data.Items.ContainsKey(item) && !item.StartsWith(PackedPrefix);
        bool valid = p.Backpack.Items.All(pair => pair.Value > 0 && (Known(pair.Key) || crates.ContainsKey(PackedId(pair.Key))))
            && crates.Values.All(box => box?.Items != null && box.Items.All(pair => pair.Value > 0 && Known(pair.Key)) && box.Count <= Data.CrateCapacity)
            && p.Crafting.All(id => Data.Recipes.TryGetValue(id, out var r) && r.Hand)
            // A player who left while dead comes back still waiting to respawn.
            && (p.Health is > 0 and <= 100 || p.Health <= 0 && p.RespawnIn > 0) && p.Hunger is >= 0 and <= 100 && p.Thirst is >= 0 and <= 100
            && p.Torch >= 0 && p.Torch <= Data.CampfireSeconds && p.RespawnIn >= 0 && p.RespawnIn <= Data.RespawnSeconds;
        var backpack = new Inventory { Slots = 24 };
        int legacySlots = p.Backpack.Items.Sum(pair => {
            int stack = Water.IsBottle(pair.Key) ? 64 : backpack.StackOf(pair.Key);
            return (pair.Value + stack - 1) / stack;
        });
        if (!valid || !backpack.Fits(p.Backpack.Items) && legacySlots > 24) return null;
        backpack.Slots = Math.Max(24, p.Backpack.AggregateSlotsNeeded);
        var renamed = new Dictionary<string, string>();
        // Packed crates get new numbers here, since the guest's old ones may be taken in this world.
        foreach (var (item, quantity) in p.Backpack.Items) {
            string id = item;
            if (crates.TryGetValue(PackedId(item), out var box)) {
                var crate = NewCrate(); foreach (var (inside, count) in box.Items) crate.Add(inside, count);
                id = Pack(crate); p.HotbarSlots = p.HotbarSlots.Select(slot => slot == item ? id : slot).ToList();
                renamed[item] = id;
            }
            backpack.Add(id, quantity);
        }
        p.Backpack.EnsureLayout();
        backpack.Layout = p.Backpack.Layout.Select(slot => new Inventory.Slot {
            Item = renamed.GetValueOrDefault(slot.Item, slot.Item),
            Count = slot.Count
        }).ToList();
        backpack.Slots = 24;
        p.Backpack = backpack; p.Crates = null;
        p.HotbarSlots = Enumerable.Range(0, 9).Select(i => p.HotbarSlots.ElementAtOrDefault(i) is string id && (Data.Items.ContainsKey(id) || Data.Buildings.ContainsKey(id) || p.Backpack[id] > 0) ? id : "").ToList();
        p.ActiveHotbar = Math.Clamp(p.ActiveHotbar, 0, 8);
        p.Sheltered = 0; p.Mauled = false; p.TorchKindling = 0; p.TorchLit = p.TorchLit && p.Torch > 0; p.GatherTarget = 0; p.Held = ""; p.TorchFlare = 0;
        if (!CanStand(p.Pos)) p.Pos = Spawn;
        return p;
    }

    /// <summary>Host: a guest has gone, with everything they carry; the contents of their packed crates go with them.</summary>
    public void RemoveGuest(int id) {
        if (id == Me.Id || S.Players.Find(p => p.Id == id) is not PlayerState p) return;
        foreach (var item in p.Backpack.Items.Keys) S.Crates.Remove(PackedId(item));
        S.Players.Remove(p);
        SayToAll("A neighbor left the settlement.");
    }

    /// <summary>A player as a guest saves them on their own machine, with what their packed crates hold.</summary>
    public string ExportPlayer(PlayerState p) {
        p.Crates = p.Backpack.Items.Keys.Where(id => Packed(id) != null).ToDictionary(PackedId, id => Packed(id)!);
        try { return JsonSerializer.Serialize(p, GameData.Json); } finally { p.Crates = null; }
    }

    /// <summary>A guest takes the host's whole world as its copy.</summary>
    public void Mirror(SaveState state, List<PlayerState> players, int localId) {
        state.Players = players; LocalId = localId; S = state; S.Actor = Me;
        UpdateLunchFire(); ExploredRevision++; Look();
    }
}
