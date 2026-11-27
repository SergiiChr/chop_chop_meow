using Godot;

namespace ChopChopMeow;

/// <summary>The player actions a guest sends to the host.</summary>
public enum Verb { Assign, Attack, AutoSell, Build, Buy, CancelCraft, ClearFilters, Collect, Consume, Craft, Dismantle, Duty, EndingShown, Enter, FetchPrices, Fire, FuelFire, HaulGroup, Hire, Hold, Leave, LoadBatch, LoadFuel, LoadInput, MoveJob, MoveInCrate, PickUp, PickUpCrate, PlaceCrate, Pump, QueueBatch, Release, SelectHotbar, SelectInHand, Sell, SetCompost, SetFilter, SetHotbar, SetPacking, SetRecipe, Settle, SkipFood, SkipMorning, Sleep, SlotTool, SortBag, SupplyMine, TakeUpPath, Torch, Transfer, TransferSlot, TurnKey, Unqueue, UnslotTool, PlaceBarrel, PickUpBarrel, MoveInBarrel, TakeUpPipe, BeginDig, CancelDig, FinishDig, SwingNet, Wake, TakePage, ReadDiary, PlaceLantern, PickUpLantern, RefillLantern, ToggleLanternInHand }
/// <summary>A player action a guest sends to the host, which runs it for that guest. What each field means depends on the verb.</summary>
/// <remarks>
/// <see cref="Held"/> is the mark "from held stack": the command takes its units from the player's held stack (<see cref="World.FromHeldStack"/>).
/// <see cref="Spares"/> is the mark "spares only": it takes none from backpack stacks bound to the hotbar (<see cref="World.FromSpares"/>).
/// </remarks>
public readonly record struct Command(Verb Verb, int Target, int Other, string Item, int Count, Vector2 At, bool Held = false, bool Spares = false, float Rotation = 0, PipeShape PipeShape = PipeShape.Straight);
