using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

/// <summary>The players in the world and the guest side of actions.</summary>
public sealed partial class World {
    /// <summary>The player at this machine: 1 in a world it runs itself, its peer id in a server's world, and 0 on a server, which has none.</summary>
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
    /// <summary>Server: a player made something their panel opens on, a mine or a worker, given as the player's id and its id.</summary>
    public Action<int, int>? Made;
    // While the tick runs the shared world, what it says is for everyone.
    private bool everyone;

    internal void SayToAll(string message) {
        bool was = everyone; everyone = true;
        Say(message); everyone = was;
    }

    /// <summary>Runs an action as another player: their backpack, position and needs, and they hear what it says.</summary>
    public void As(PlayerState p, Action action) { using (ActAs(p)) action(); }

    public T As<T>(PlayerState p, Func<T> action) { using (ActAs(p)) return action(); }

    /// <summary>Acts as another player until the returned scope ends; a struct, so code that runs every tick switches players without allocating.</summary>
    internal Acting ActAs(PlayerState p) {
        var scope = new Acting(this, S.Actor, everyone);
        S.Actor = p; everyone = false;
        return scope;
    }

    internal readonly struct Acting(World world, PlayerState actor, bool everyone) : IDisposable {
        public void Dispose() { world.S.Actor = actor; world.everyone = everyone; }
    }

    /// <summary>
    /// In a guest's copy, sends the action to the host and tells the caller to stop.
    /// On the server, a marked action (<see cref="FromHeldStack"/>, <see cref="FromSpares"/>) goes through the same checks a guest's does first, then comes back here to run.
    /// </summary>
    /// <summary>Whether the acting player is occupied, telling them so: anything they try then does nothing.</summary>
    internal bool Busy() {
        if (!Occupied) return false;
        Say("You are busy.");
        return true;
    }

    internal bool Forwarded(Verb verb, int target = 0, int other = 0, string item = "", int count = 0, Vector2 at = default, float rotation = 0, PipeShape pipeShape = PipeShape.Straight) {
        var c = new Command(verb, target, other, item, count, at, fromHeld, fromSpares, rotation, pipeShape);
        if (Host != null) { Host(c); return true; }
        if (!(c.Held || c.Spares) || runningMarked) return false;
        RunMarked(c);
        return true;
    }

    // Set while a command marked "spares only" is being sent.
    private bool fromSpares;
    // Set while the server runs a marked command, which then runs as any other.
    private bool runningMarked;

    /// <summary>
    /// Runs a backpack batch, such as a Ctrl-click or Store all, with the mark "spares only": it takes no units from stacks bound to the hotbar.
    /// It runs as a guest's command does, so what it returns tells nothing.
    /// </summary>
    public void FromSpares(Action command) {
        fromSpares = true;
        try { command(); } finally { fromSpares = false; }
    }

    /// <summary>Server: runs a marked command by its mark.</summary>
    private void RunMarked(Command c) {
        runningMarked = true;
        try {
            if (c.Held) RunFromHeld(c);
            else { S.Backpack.SparesOnly = true; try { Dispatch(c with { Spares = false }); } finally { S.Backpack.SparesOnly = false; } }
        } finally { runningMarked = false; }
    }

    /// <summary>Selects a hotbar slot; the server keeps it, so a save has it.</summary>
    public void SelectHotbar(int slot) {
        if (slot is < 0 or > 8) return;
        S.ActiveHotbar = slot; S.InHand = "";
        Forwarded(Verb.SelectHotbar, count: slot);
    }

    /// <summary>
    /// Puts a building plan, an item to wait for, or nothing on a hotbar slot and selects that slot; the server keeps it, so a save has it.
    /// Marked "from held stack", it binds the held stack to the slot instead (<see cref="BindHeld"/>).
    /// </summary>
    public void SetHotbar(int slot, string item) {
        if (slot is < 0 or > 8) return;
        if (MarkedSource == null) { Unbind(slot); S.HotbarSlots[slot] = item; }
        S.ActiveHotbar = slot; S.InHand = "";
        if (Busy() || Forwarded(Verb.SetHotbar, count: slot, item: item)) return;
        if (heldSource != null) BindHeld(slot);
    }

    /// <summary>Selects a carried tool in hand without a hotbar slot, or with "" empties the hand; the server keeps it, so a save has it.</summary>
    public void SelectInHand(string tool) {
        if (tool != "" && (!(Data.Items.TryGetValue(tool, out var def) && def.Tool != "") || S.Backpack[tool] == 0)) return;
        S.InHand = tool;
        Forwarded(Verb.SelectInHand, item: tool);
    }

    /// <summary>Remembers in the save that its owner has seen the ending.</summary>
    public void MarkEndingShown() {
        S.EndingShown = true;
        Forwarded(Verb.EndingShown);
    }

    /// <summary>Runs an action with a cell of an inventory first in line to leave; without one named (-1), the cell already first in line stays first, such as a held stack's.</summary>
    private static void Preferring(Inventory box, int slot, Action action) {
        int first = box.PreferredSlot;
        if (slot >= 0) box.PreferredSlot = slot;
        try { action(); } finally { box.PreferredSlot = first; }
    }

    /// <summary>Host: runs a guest's action as that guest. The action checks reach and costs as it does for the host.</summary>
    public void Run(PlayerState p, Command c) => As(p, () => { if (c.Held || c.Spares) RunMarked(c); else Dispatch(c); });

    private void Dispatch(Command c) {
        var t = Find(c.Target);
        var w = S.Workers.Find(w => w.Id == c.Other);
        switch (c.Verb) {
            case Verb.Build: if (Kinds.Parse(c.Item) is ThingKind kind) Construction.Build(kind, c.At, c.Rotation, c.PipeShape); break;
            case Verb.BeginDig: Holes.Begin(c.At, c.Count); break;
            case Verb.CancelDig: Holes.Cancel(c.Count); break;
            case Verb.FinishDig: Holes.Finish(c.Count); break;
            case Verb.TakeUpPath: GravelPaths.TakeUp(Terrain.TileAt(c.At)); break;
            case Verb.TakeUpPipe: Construction.TakeUpPipe(Terrain.TileAt(c.At)); break;
            case Verb.PlaceCrate: Preferring(S.Backpack, c.Count, () => PlaceCrate(c.Item, c.At, c.Rotation)); break;
            case Verb.ToggleLanternInHand: Lanterns.ToggleInHand(c.Target); break;
            case Verb.PlaceLantern: Preferring(S.Backpack, c.Count, () => Lanterns.Place(c.Item, c.At, c.Rotation)); break;
            case Verb.RefillLantern: if (c.Target == 0 || t != null) Preferring(S.Backpack, (int)c.At.X, () => Lanterns.Refill(c.Other, t, c.Count)); break;
            case Verb.PlaceBarrel: Preferring(S.Backpack, c.Count, () => PlaceBarrel(c.Item, c.At, c.Rotation)); break;
            case Verb.MoveInBarrel:
                if ((uint)c.Target <= 2 * Barrel.Capacity) {
                    string barrel = c.Target <= Barrel.Capacity ? Barrel.AtFill(c.Target) : Barrel.AtFill(c.Target - Barrel.Capacity, BerryJam.Liquid);
                    Preferring(S.Backpack, (int)c.At.X, () => MoveInBarrel(barrel, c.Item, c.Count, c.Other != 0, (int)c.At.Y));
                }
                break;
            case Verb.MoveInCrate: MoveInCrate(c.Target, c.Item, c.Count, c.Other != 0); break;
            case Verb.Consume: S.Backpack.PreferredSlot = c.Count; try { Consume(c.Item); } finally { S.Backpack.PreferredSlot = -1; } break;
            case Verb.Torch: UseTorch(c.At); break;
            case Verb.Attack: Attack(c.At, c.Item == "" ? null : c.Item); break;
            case Verb.SwingNet: SwingNet(c.At); break;
            case Verb.Leave: Doorways.Leave(); break;
            case Verb.TurnKey: Doorways.TurnKey(); break;
            case Verb.Craft: Craft(c.Item, c.Count); break;
            case Verb.CancelCraft: CancelCraft(c.Item, c.Count); break;
            case Verb.Transfer:
                if (Resolve(c.Target) is Inventory from && Resolve(c.Other) is Inventory to) {
                    Preferring(from, c.At.Y == -1 ? (int)c.At.X : -1, () => Transfer(from, to, c.Item, c.Count));
                }
                break;
            case Verb.TransferSlot: if (Resolve(c.Target) is Inventory slotFrom && Resolve(c.Other) is Inventory slotTo) TransferSlot(slotFrom, slotTo, (int)c.At.X, (int)c.At.Y, c.Item, c.Count); break;
            case Verb.SortBag: SortBackpack(); break;
            case Verb.Hold: if (Resolve(c.Target) is Inventory box && (uint)c.Count < (uint)box.Layout.Count && box.Layout[c.Count].Item == c.Item) Hold(box, c.Count, c.Other); break;
            case Verb.Release: Release(); break;
            case Verb.SelectHotbar: SelectHotbar(c.Count); break;
            case Verb.SelectInHand: SelectInHand(c.Item); break;
            case Verb.SetHotbar: SetHotbar(c.Count, c.Item); break;
            case Verb.EndingShown: MarkEndingShown(); break;
            case Verb.SkipMorning: SkipMorning(); break;
            case Verb.Wake: Sleep.Wake(); break;
            case Verb.Assign: if (w != null) Assign(w, t); break;
            case Verb.Duty: if (w != null && JobKinds.TryParse(c.Item, out var job) && (t ?? DutyPlace(w, c.Target)) is Thing place) SetDuty(w, place, job, c.Count != 0); break;
            case Verb.Fire: if (w != null) Fire(w); break;
            case Verb.HaulGroup: if (w != null) { if (c.Count != 0) AddHaulGroup(w); else RemoveHaulGroup(w, (int)c.At.X); } break;
            // A stale move, of a job that has left that place in the list, is dropped rather than moving another job.
            case Verb.MoveJob: if (w != null && JobAt(w, new JobSpot(c.Target, c.Count)) is JobEntry moved && JobKey(moved) == c.Item) MoveJob(w, new JobSpot(c.Target, c.Count), new JobSpot((int)c.At.X, (int)c.At.Y)); break;
            case Verb.Settle: if (w != null) SettleDebt(w); break;
        }
        if (t == null) return;
        switch (c.Verb) {
            case Verb.PickUp: if (t.IsLoose) Gather(t, 0); break;
            case Verb.Pump: Pump(t); break;
            case Verb.FuelFire: FuelFire(t); break;
            case Verb.SetRecipe: SetRecipe(t, c.Item); break;
            case Verb.Unqueue: Unqueue(t, c.Item, c.Count); break;
            case Verb.QueueBatch: QueueBatch(t, c.Item, c.Count); break;
            case Verb.SetFilter: SetFilter(t, c.Item, (c.Count & 1) != 0, (c.Count & 2) != 0); break;
            case Verb.ClearFilters: ClearFilters(t); break;
            case Verb.SetCompost: SetCompost(t, c.Count != 0); break;
            case Verb.SkipFood: SetSkipFood(t, c.Count != 0); break;
            case Verb.SupplyMine: Mines.Supply(t, c.Item, c.Count); break;
            case Verb.LoadBatch: LoadBatch(t, Math.Clamp(c.Count, 1, 5)); break;
            case Verb.LoadInput: LoadInput(t, c.Item, c.Count); break;
            case Verb.LoadFuel: LoadFuel(t, c.Item, c.Count); break;
            case Verb.Collect: Collect(t); break;
            case Verb.Enter: Doorways.Enter(t, c.Count == 1); break;
            case Verb.Sleep: Sleep.Start(t); break;
            case Verb.Sell: Sell(t); break;
            case Verb.AutoSell: SetAutoSell(t, c.Count != 0); break;
            case Verb.SetPacking: SetPacking(t, c.Item); break;
            case Verb.Buy: Buy(t, c.Item, Math.Min(c.Count, 5)); break;
            case Verb.PickUpLantern: Lanterns.PickUp(t); break;
            case Verb.PickUpBarrel: PickUpBarrel(t); break;
            case Verb.PickUpCrate: PickUpCrate(t); break;
            case Verb.Dismantle: Construction.Dismantle(t); break;
            case Verb.SlotTool: SlotTool(t, c.Item); break;
            case Verb.UnslotTool: UnslotTool(t, c.Item); break;
            case Verb.FetchPrices: FetchPrices(t); break;
            case Verb.Hire: if (c.Count >= 0 && c.Count < Data.HireOffers.Count) Hire(t, Data.HireOffers[c.Count]); break;
        }
    }

    /// <summary>
    /// An inventory as a number another machine can look up: 0 is the acting player's backpack, otherwise its holder's id and which of its boxes.
    /// </summary>
    public int Locate(Inventory inventory) {
        if (inventory == S.Backpack) return 0;
        foreach (var pair in S.Crates) if (pair.Value == inventory && CarriedCrate(pair.Key) != null) return CrateCode - pair.Key;
        foreach (var w in S.Workers) if (inventory == w.Bag || inventory == w.ToolSlot) return w.Id * BoxCodes + (inventory == w.Bag ? 1 : 2);
        foreach (var t in S.Things) if (BoxIndex(t, inventory) is int box and >= 0) return t.Id * BoxCodes + box + 1;
        return -1;
    }

    public const int BoxCodes = 7;
    /// <summary>What <see cref="Locate"/> takes the number of a packed crate in the backpack from, to name its contents.</summary>
    private const int CrateCode = -2;
    internal static Inventory?[] Boxes(Thing t) => [t.Inventory, t.Output, t.Tools, t.Wages, t.Burner, t.PlumbingSlot];
    /// <summary>One of a thing's boxes in the order of <see cref="Boxes"/>, looked up without building the array, as ticks do.</summary>
    private static Inventory? Box(Thing t, int index) => index switch { 0 => t.Inventory, 1 => t.Output, 2 => t.Tools, 3 => t.Wages, 4 => t.Burner, 5 => t.PlumbingSlot, _ => null };
    /// <summary>Which of a thing's boxes an inventory is, or -1.</summary>
    private static int BoxIndex(Thing t, Inventory inventory) {
        for (int i = 0; i < BoxCodes - 1; i++) if (Box(t, i) == inventory) return i;
        return -1;
    }
    private Inventory? Resolve(int at) => at == 0 ? S.Backpack : at <= CrateCode ? (CarriedCrate(CrateCode - at) != null ? S.Crates.GetValueOrDefault(CrateCode - at) : null)
        : Find(at / BoxCodes) is Thing t ? Box(t, at % BoxCodes - 1) : Carrier(at) is Worker w ? (at % BoxCodes == 1 ? w.Bag : at % BoxCodes == 2 ? w.ToolSlot : null) : null;

    /// <summary>The worker a number from <see cref="Locate"/> names the hands or the tool slot of, or null.</summary>
    private Worker? Carrier(int at) {
        if (at > 0) foreach (var w in S.Workers) if (w.Id == at / BoxCodes) return w;
        return null;
    }

    /// <summary>Whether the acting player reaches the holder of a box, by its number from <see cref="Locate"/>: a building, a worker, or their own backpack and the crates in it.</summary>
    private bool InReach(int at) => at <= 0 || (Find(at / BoxCodes) is Thing t ? Reaches(t) : Carrier(at) is Worker w && Reaches(w));

    /// <summary>Why an item cannot go into a box by hand, by the box's number from <see cref="Locate"/>, or null.</summary>
    private string? BoxBlock(int at, Inventory to, string item) =>
        at == 0 ? CarryBarrelBlock(to, item)
        : at <= CrateCode && Barrel.Is(item) ? "Barrel does not fit in wood crate."
        : at <= 0 ? null : Find(at / BoxCodes) is Thing t ? BoxBlock(t, to, item)
        : Carrier(at) is Worker w && to == w.ToolSlot && !FitsWorkerToolSlot(item) ? "A worker's tool slot only takes tools." : null;

    internal bool FitsWorkerToolSlot(string item) => ToolKind(item) != "";

    /// <summary>A manual move between the backpack and a building, or two buildings, all within reach; automation filters do not apply.</summary>
    public int Transfer(Inventory from, Inventory to, string item, int quantity) {
        int a = Locate(from), b = Locate(to);
        if (a < 0 || b < 0 || Busy() || Forwarded(Verb.Transfer, a, b, item, quantity, new Vector2(from.PreferredSlot, -1))) return 0;
        if (!InReach(a) || !InReach(b)) { Say("Walk closer first."); return 0; }
        if (BoxBlock(b, to, item) is string refused) { Say(refused); return 0; }
        var into = b > 0 ? Find(b / BoxCodes) : null;
        if (FillBlock(from, to, item, into) is string dry) { Say(dry); return 0; }
        if (quantity > 0 && from != to && from[item] > 0 && into?.Kind == ThingKind.Packer
            && to == into.Inventory && PackingRoom(into, item) == 0) { Say(FullInput(item)); return 0; }
        int moved = Carry(from, to, into, item, quantity);
        if (moved > 0 && into != null) AfterTransfer(into, to);
        if (moved < quantity) SayHeldBehind(from, item);
        return moved;
    }

    /// <summary>After a move from a box, says how many units of the item stayed behind because other players hold them, such as "5 Coal was held by another player."</summary>
    private void SayHeldBehind(Inventory from, string item) {
        if (from.HeldByOthers(item, S.Actor.Id) is int held and > 0) Say($"{held} {Data.Name(item)} was held by another player.");
    }

    /// <summary>Moves items out of the backpack a view shows.</summary>
    public int Transfer(InventoryView from, Inventory to, string item, int quantity) => Transfer(from.Inventory, to, item, quantity);

    /// <summary>Moves items into the backpack a view shows.</summary>
    public int Transfer(Inventory from, InventoryView to, string item, int quantity) => Transfer(from, to.Inventory, item, quantity);

    public void SortBackpack() {
        if (Busy() || Forwarded(Verb.SortBag)) return;
        Release();
        S.Backpack.Sort();
    }

    /// <summary>Moves a stack from a cell of the backpack a view shows.</summary>
    public int TransferSlot(InventoryView from, Inventory to, int sourceSlot, int targetSlot, string item, int quantity) => TransferSlot(from.Inventory, to, sourceSlot, targetSlot, item, quantity);

    /// <summary>Moves a stack between two cells of the backpack a view shows.</summary>
    public int TransferSlot(InventoryView from, InventoryView to, int sourceSlot, int targetSlot, string item, int quantity) => TransferSlot(from.Inventory, to.Inventory, sourceSlot, targetSlot, item, quantity);

    public int TransferSlot(Inventory from, Inventory to, int sourceSlot, int targetSlot, string item, int quantity) {
        int a = Locate(from), b = Locate(to);
        if (a < 0 || b < 0 || Downed) return 0;
        if ((uint)sourceSlot >= (uint)from.Slots || (uint)targetSlot >= (uint)to.Slots) return 0;
        if (Busy() || Forwarded(Verb.TransferSlot, a, b, item, quantity, new Vector2(sourceSlot, targetSlot))) return 0;
        if (!InReach(a) || !InReach(b)) { Say("Walk closer first."); return 0; }
        // Reject a stale move rather than moving a different item.
        if ((uint)sourceSlot >= (uint)from.Layout.Count || from.Layout[sourceSlot].Item != item) return 0;
        if (from.Layout[sourceSlot].HeldByOther(S.Actor.Id) || to.Layout[targetSlot].HeldByOther(S.Actor.Id)) { Say(HeldByAnother); return 0; }
        if (from != to && BoxBlock(b, to, item) is string refused) { Say(refused); return 0; }
        var into = b > 0 ? Find(b / BoxCodes) : null;
        if (from != to && FillBlock(from, to, item, into) is string dry) { Say(dry); return 0; }
        if (quantity > 0 && from != to && into?.Kind == ThingKind.Packer && to == into.Inventory) {
            int room = PackingRoom(into, item);
            if (room == 0) { Say(FullInput(item)); return 0; }
            quantity = Math.Min(quantity, room);
        }
        // A drop on another item's cell in another inventory still stores the stack wherever it fits, and so does a liquid or a vessel that pours.
        int moved;
        if (from != to && (Data.VesselFor(item) != null || PoursInto(into, to, item) != null || into?.Kind == ThingKind.Farm)) {
            from.PreferredSlot = sourceSlot;
            try { moved = Carry(from, to, into, item, quantity); } finally { from.PreferredSlot = -1; }
        } else if (from != to && to.Layout[targetSlot].Item is var there && there != "" && there != item) {
            from.PreferredSlot = sourceSlot;
            try { moved = from.TransferTo(to, item, quantity); } finally { from.PreferredSlot = -1; }
        } else moved = from.MoveSlotTo(sourceSlot, to, targetSlot, quantity);
        if (moved > 0 && into != null) AfterTransfer(into, to);
        return moved;
    }

    private void AfterTransfer(Thing into, Inventory to) {
        if (into.Kind == ThingKind.Barrel) PileFilters(into);
        if (to != into.Inventory) return;
        if (into.Kind == ThingKind.Hole) Trees.PlantIn(into);
        else if (TreePlanting.NeedsWater(into) && to[Water.Liquid] >= TreePlanting.WaterNeeded) {
            ReleaseIn(to);
            Trees.BeginGrowth(into);
        }
    }

    public void SetFilter(Thing chest, string item, bool output, bool allowed) {
        if (Lanterns.IsItem(item)) item = Lanterns.Item;
        bool pile = IsSingleResourceStore(chest);
        if (chest.Kind == ThingKind.Barrel && Data.VesselFor(item) == null) return;
        if (!(chest.IsStorage || chest.Kind == ThingKind.Signpost) || !(Data.Items.ContainsKey(item) || CrateGood(item) != null || pile && item == MixedCrate || !pile && item == Inventory.Everything) || Nests(chest, item)) return;
        if (pile && (!PileFilterChanges(chest, allowed) || !allowed && PileFilter(chest) != PileEntry(item)) || Busy() || Forwarded(Verb.SetFilter, chest.Id, item: item, count: (output ? 1 : 0) | (allowed ? 2 : 0))) return;
        if (pile && allowed) SetPileFilter(chest, PileEntry(item));
        else if (pile) chest.Inventory.ClearFilters();
        else chest.Inventory.Filter(item, output, allowed);
    }
    /// <summary>Whether a plain pile's filter may change: only while it is empty, saying so to the player otherwise, and to a new resource only once the one it names is taken off.</summary>
    private bool PileFilterChanges(Thing pile, bool adding) {
        if (pile.Inventory.First() != null) { Say($"Can't change filter on a non-empty {Lower(Data.Name(pile.Kind))}."); return false; }
        return !adding || pile.Inventory.InputFilter.Count == 0;
    }

    /// <summary>Stops or restarts workers bringing food to a compost.</summary>
    public void SetSkipFood(Thing pile, bool skip) {
        if (!pile.Compost || Busy() || Forwarded(Verb.SkipFood, pile.Id, count: skip ? 1 : 0)) return;
        pile.SkipFood = skip;
    }

    public void ClearFilters(Thing chest) {
        if (!(chest.IsStorage || chest.Kind == ThingKind.Signpost) || IsSingleResourceStore(chest) && !PileFilterChanges(chest, false) || Busy() || Forwarded(Verb.ClearFilters, chest.Id)) return;
        chest.Inventory.ClearFilters();
    }

    /// <summary>Gives new wolves and trade carts ids, which other machines tell them apart by.</summary>
    public void NumberWolvesAndOrders() {
        foreach (var w in S.Wolves.Where(w => w.Id == 0)) w.Id = S.NextId++;
        foreach (var o in S.Orders.Where(o => o.Id == 0)) o.Id = S.NextId++;
    }

    /// <summary>Host: puts a guest where their own machine walked them, as <see cref="MovePlayer"/> does for the host.</summary>
    public void MoveTo(Vector2 p) {
        if (Occupied || S.Sheltered != 0 || p == Player) return;
        playerBusy = true; Player = p;
    }

    /// <summary>
    /// Server: brings in the player who plays from the machine with <paramref name="key"/>: as they left, else the player no machine has claimed yet,
    /// such as a new world's first, else a newcomer with an axe and a bottle at the clearing.
    /// </summary>
    public PlayerState Arrive(string key, int id) {
        var p = S.Absent.Remove(key, out var back) ? back : S.Absent.Remove("", out var unclaimed) ? unclaimed : Newcomer();
        p.Key = key; p.Id = id; p.Warp++; p.Shirt = Enumerable.Range(0, 4).First(i => S.Players.All(o => o.Shirt != i));
        // What only made sense while they played: a cabin may have gone and a building gone up where they stood, a wolf that had them pinned has moved on, and the other properties follow input they no longer give.
        // A cabin that others filled meanwhile leaves the newcomer at its door.
        if (Find(p.Sheltered)?.Kind != ThingKind.Hut || !Doorways.HasPlace(p.Sheltered, p)) p.Sheltered = 0;
        p.Door = Doorway.None;
        if (p.Sheltered == 0 && !CanStand(p.Pos)) p.Pos = Terrain.Spawn;
        Forget(p);
        p.SleepingAt = 0;
        p.Mauled = false; p.GatherTarget = 0; p.Selected = ""; p.Sprinting = p.Braking = false; p.TorchKindling = 0;
        GroundHoles.Clear(p);
        S.Players.Add(p);
        // A packed crate's own name is no item, so it leaves no mark.
        foreach (var pair in p.Backpack.Items) if (Data.Items.ContainsKey(pair.Key)) Got(pair.Key);
        if (S.Players.Count == 1) S.Actor = p;
        if (S.Players.Count > 1) SayToAll("A neighbor joined the settlement.");
        return p;
    }

    private static PlayerState Newcomer() {
        var p = new PlayerState();
        p.Backpack.Add("steel_axe", 1); p.Backpack.Add(Water.Empty, 1);
        return p;
    }

    /// <summary>Server: a player has gone; they stay in the world's save, with everything they carry, until they come back.</summary>
    public void Depart(int id) {
        if (PlayerById(id) is not PlayerState p) return;
        ReleaseFor(p);
        p.SleepingAt = 0;
        GroundHoles.Clear(p);
        S.Players.Remove(p); S.Absent[p.Key] = p;
        Doorways.UnlockEmpty(p.Sheltered);
        if (S.Actor == p) S.Actor = S.Players.FirstOrDefault() ?? new PlayerState();
        SayToAll("A neighbor left the settlement.");
    }

    /// <summary>Server: everyone leaves a world it has just made or loaded, to come back as each one joins.</summary>
    public void Vacate() {
        LocalId = 0;
        foreach (var p in S.Players) S.Absent[p.Key] = p;
        S.Players.Clear(); S.Actor = new PlayerState();
    }

    /// <summary>Server: the machine starting this world takes it over, with the player of the machine that saved it unless it has one here already.</summary>
    public void Claim(string key) {
        if (key != S.Owner && !S.Absent.ContainsKey(key) && S.Absent.Remove(S.Owner, out var p)) { p.Key = key; S.Absent[key] = p; }
        S.Owner = key;
    }

    /// <summary>A guest takes the host's whole world as its copy.</summary>
    public void Mirror(SaveState state, List<PlayerState> players, int localId) {
        state.Players = players; LocalId = localId; S = state; S.Actor = Me;
        ExploredRevision++;
        Replaced?.Invoke();
    }
}
