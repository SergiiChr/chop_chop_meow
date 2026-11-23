using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

/// <summary>One player's body and belongings, saved with the world under the key of the machine they play from.</summary>
public sealed class PlayerState {
    /// <summary>The key of the machine this player plays from, which the save stores them under; empty until a machine claims them.</summary>
    [JsonIgnore] public string Key { get; set; } = "";
    /// <summary>Network peer id while playing on a server; 1 in a world this machine runs itself.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int Id { get; set; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float PlayerX { get; set; } = Terrain.Spawn.X;
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float PlayerY { get; set; } = Terrain.Spawn.Y;
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float Health { get; set; } = 100;
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float Hunger { get; set; } = 100;
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public float Thirst { get; set; } = 100;
    public Inventory Backpack { get; set; } = new() { Slots = 24, CarriedBy = Inventory.Carrier.Player };
    public List<string> HotbarSlots { get; set; } = new() { "steel_axe", "steel_pick", "chest", "campfire", "furnace", "anvil", "bench", "berries", Water.Full };
    public int ActiveHotbar { get; set; }
    /// <summary>A tool selected in hand from the backpack without a hotbar slot, or empty; while one is, no hotbar slot is selected.</summary>
    public string InHand { get; set; } = "";
    /// <summary>Recipes waiting in the personal crafting menu, one entry per recipe in the order first queued; their inputs are already taken from the backpack.</summary>
    [JsonConverter(typeof(QueueJson))] public List<Queued> Crafting { get; set; } = new();
    /// <summary>The hand recipe being made now, taken off the queue when work on it started, or empty.</summary>
    public string Making { get; set; } = "";
    /// <summary>Seconds spent on <see cref="Making"/>, at the speed of the tool used.</summary>
    public float CraftTime { get; set; }
    /// <summary>Burn time left on the backpack torch the player has lit, shown in the left hand, or zero for none; it only burns while lit, and goes once the backpack has no torch.</summary>
    public float Torch { get; set; }
    public bool TorchLit { get; set; }
    /// <summary>Seconds left striking flint and steel before the torch catches.</summary>
    public float TorchKindling { get; set; }
    public float RespawnIn { get; set; }
    /// <summary>A wolf has pinned the player; it stays set through the following death until respawn.</summary>
    public bool Mauled { get; set; }
    /// <summary>Id of the cabin the player is inside, or zero; inside, wolves back off and the player is hidden.</summary>
    public int Sheltered { get; set; }
    public int CabinEntry { get; set; }
    /// <summary>What the player is doing at a cabin's door, which <see cref="DoorCabin"/> names.</summary>
    public Doorway Door { get; set; }
    public int DoorCabin { get; set; }
    /// <summary>Seconds into <see cref="Door"/>.</summary>
    public float DoorTime { get; set; }
    /// <summary>Where the body began its current doorway step, and where it is along that step now.</summary>
    public Vector2 DoorFrom { get; set; }
    public Vector2 DoorBody { get; set; }
    /// <summary>The player sprinted to the door, so they step in at a run and slam it.</summary>
    public bool DoorRush { get; set; }
    /// <summary>When the player came to the door, numbered by <see cref="SaveState.DoorArrivals"/>, which sets who of those tugged back out goes in first.</summary>
    public int DoorQueued { get; set; }
    /// <summary>The resource the player is working on, so other machines can show it.</summary>
    public int GatherTarget { get; set; }
    /// <summary>The item selected on the hotbar, which shows in the player's hand, so other machines can show it.</summary>
    public string Selected { get; set; } = "";
    /// <summary>The stack this player holds on the cursor, or null, for their own window to show; the server builds it from <see cref="HeldSlot"/> each time it sends the player.</summary>
    public HeldStack? HeldStack { get; set; }
    /// <summary>Moving with Shift held, which drains food and drink faster; carried so other machines show the run.</summary>
    public bool Sprinting { get; set; }
    /// <summary>Braking against a key for the other way (<see cref="Momentum"/>); carried so other machines show the lunge.</summary>
    public bool Braking { get; set; }
    /// <summary>Seconds left of the brighter flare that follows a torch swing.</summary>
    public float TorchFlare { get; set; }
    /// <summary>Counts moves the simulation made, such as a respawn; a guest only takes its own position from the host when it changes.</summary>
    public int Warp { get; set; }
    /// <summary>Which shirt colour tells this player apart: the first one no other player wears when they join.</summary>
    public int Shirt { get; set; }
    [JsonIgnore] public Vector2 Pos { get => new(PlayerX, PlayerY); set { PlayerX = value.X; PlayerY = value.Y; } }
    // Only the host's simulation uses these.
    internal float GatherClock, AttackClock;
    /// <summary>Seconds since the player last moved or gathered; hand crafting only goes on while they stand still.</summary>
    internal float Still;
    internal bool Busy, PackAttack;
    /// <summary>The swing this player has started that has yet to land.</summary>
    internal Swing? Swing;
    /// <summary>The next axe swing is the sideways sweep: it takes turns with the overhead chop.</summary>
    internal bool SweepNext;
    internal readonly List<float> TorchSwings = new();
    /// <summary>Server: the inventory and slot the held stack lies in; the slot keeps its identity when a swap or a sort moves it.</summary>
    internal Inventory? HeldBox;
    internal Inventory.Slot? HeldSlot;
}
