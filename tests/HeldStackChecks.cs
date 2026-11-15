using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks for held stacks: units a player picks up stay in their slot, nobody else takes them, and they land by the rules of where they are put down.</summary>
public static class HeldStackChecks {
    public static void Run(GameData data) {
        CheckAHeldStackStaysInItsSlotUntilLetGo(data);
        CheckPuttingDownFillsTheCellAndKeepsTheRestHeld(data);
        CheckPuttingDownOnAnotherItemInTheBackpackSwapsThem(data);
        CheckAnotherPlayerCannotTakeAHeldStackAndIsToldWhy(data);
        CheckTakeAllAndCollectSayHowManyAnotherPlayerHolds(data);
        CheckAStationStartsNoBatchWhileAStackInItIsHeld(data);
        CheckAStationInputTakesOnlyWhatItsRecipeUses(data);
        CheckAHeldStackIsLetGoOutOfReachAndOnLeaving(data);
        CheckAHeldBottleStaysOutOfGatheringWater(data);
        CheckAnEmptyCratePacksOnlyTheHeldUnits(data);
        CheckAHeldStackIsLetGoOnceItsCrateIsPutDown(data);
        CheckCompostMakesNothingWhileAStackInItIsHeld(data);
        CheckACrateHeldFromAChestIsPlacedFromThere(data);
        CheckAGuestsCommandFromTheHeldStackTakesNoMoreThanItHolds(data);
        CheckACommandFromTheHeldStackTakesOnlyFromWhereItLies(data);
    }

    private static void CheckAGuestsCommandFromTheHeldStackTakesNoMoreThanItHolds(GameData data) {
        var (world, guest, _) = Hosted(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        guest.Pos = world.Player;
        guest.Backpack.Add("wood", 10);
        world.Run(guest, new Command(Verb.Hold, 0, 4, "wood", SlotOf(guest.Backpack, "wood"), default));
        world.Run(guest, new Command(Verb.Transfer, 0, world.Locate(chest.Inventory), "wood", 10, new Vector2(-1, -1), Held: true));
        Require(chest.Inventory["wood"] == 4 && guest.Backpack["wood"] == 6 && world.As(guest, () => world.Held) == null,
            "a guest's move of 10 marked \"from held stack\" should store only the 4 wood held, and hold nothing after");
    }

    private static void CheckACommandFromTheHeldStackTakesOnlyFromWhereItLies(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        var other = world.Add(ThingKind.Chest, world.Player + new Vector2(-1, 1));
        Give(world, "wood", 8);
        world.Transfer(world.Backpack, chest.Inventory, "wood", 4);
        world.Hold(chest.Inventory, 0, 4);
        world.FromHeldStack(() => world.Transfer(world.Backpack, other.Inventory, "wood", 4));
        Require(other.Inventory["wood"] == 0 && world.Backpack["wood"] == 4 && world.Held is { Count: 4 },
            "a move from the backpack marked \"from held stack\" should do nothing while the stack is held in a chest, rather than move the backpack's wood");
    }

    private static void CheckACrateHeldFromAChestIsPlacedFromThere(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + new Vector2(-2, 0));
        Give(world, "crate", 2);
        world.Transfer(world.Backpack, chest.Inventory, "crate", 2);
        world.Hold(chest.Inventory, SlotOf(chest.Inventory, "crate"), 2);
        var spot = world.Player + new Vector2(2, 0);
        Require(world.PlaceCrate("crate", spot) == null, "a crate placed without the mark \"from held stack\" should come only from the backpack, which has none");
        world.FromHeldStack(() => world.PlaceCrate("crate", spot));
        Require(world.Things.Count(t => t.Kind == ThingKind.Crate) == 1, "a crate held from a chest should go down on open ground");
        Require(chest.Inventory["crate"] == 1 && world.Held is { Item: "crate", Count: 1 }, "the crate should come from the chest, the other still held");
    }

    private static void CheckCompostMakesNothingWhileAStackInItIsHeld(GameData data) {
        var world = EmptyWorld(data);
        var compost = world.Add(ThingKind.Pile, world.Player + Vector2.One);
        world.SetCompost(compost, true);
        compost.Inventory.Add("leaves", World.CompostBatch);
        world.Hold(compost.Inventory, SlotOf(compost.Inventory, "leaves"), World.CompostBatch);
        world.Tick(data.HourSeconds + 1);
        Require(compost.Inventory["fertilizer"] == 0 && compost.Inventory["leaves"] == World.CompostBatch, "compost should make nothing while a player holds its leaves");
        world.Release();
        world.Tick(data.HourSeconds + 1); world.Tick(.1f);
        Require(compost.Inventory["fertilizer"] == 1, "compost should go on once the leaves are let go of");
    }

    private static void CheckAHeldBottleStaysOutOfGatheringWater(GameData data) {
        var world = EmptyWorld(data);
        var lake = new Thing { Kind = ThingKind.Water };
        Give(world, Water.Empty, 1);
        world.Hold(world.Backpack, SlotOf(world.Backpack, Water.Empty), 1);
        SetNeeds(world, thirst: 25);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceLiter && world.Held?.Item == Water.Empty,
            "with the only bottle held, gathering water should drink from the source and leave the bottle held");
    }

    private static void CheckAnEmptyCratePacksOnlyTheHeldUnits(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "crate", 1); Give(world, "wood", 10);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "wood"), 5);
        world.FromHeldStack(() => world.MoveInCrate(0, "wood", int.MaxValue, true));
        string packed = world.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        Require(world.Packed(packed)!["wood"] == 5 && world.Backpack["wood"] == 5 && world.Held == null,
            "five held wood put on an empty crate should pack just those five");
    }

    private static void CheckAHeldStackIsLetGoOnceItsCrateIsPutDown(GameData data) {
        var world = EmptyWorld(data);
        string packed = PackCrate(world, ("berries", 4));
        var contents = world.Packed(packed)!;
        world.Hold(contents, 0, 4);
        var crate = world.PlaceCrate(packed, world.Player + new Vector2(2, 0))!;
        world.Tick(.1f);
        Require(world.Held == null && crate.Inventory.Layout[0].Held == 0, "putting the crate down should let go of the stack held from its contents");
    }

    private static void CheckAHeldStackStaysInItsSlotUntilLetGo(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "wood", 10);
        int cell = SlotOf(world.Backpack, "wood");
        world.Hold(world.Backpack, cell, 6);
        Require(world.Held is { Item: "wood", Count: 6 } && world.Backpack.Layout[cell].Count == 10, "picking up 6 of 10 wood should hold them in their slot");
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 10) == 4 && world.Backpack.Layout[cell].Count == 6, "only the 4 wood nobody holds should move out");
        world.Release();
        Require(world.Held == null && world.Backpack.Layout[cell].Held == 0, "letting go should leave the wood in the same slot, held by nobody");
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 6) == 6, "once let go of, the wood should move out");
    }

    private static void CheckPuttingDownFillsTheCellAndKeepsTheRestHeld(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "wood", 10);
        world.Transfer(world.Backpack, chest.Inventory, "wood", 10);
        Give(world, "wood", 60);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "wood"), 60);
        PutDown(world, chest.Inventory, 0, 60);
        Require(chest.Inventory.Layout[0].Count == 64 && world.Held is { Count: 6 } && world.Backpack["wood"] == 6,
            "a held 60 wood put on a chest slot of 10 should fill it to 64 and keep 6 held");
        PutDown(world, chest.Inventory, 1, 1);
        Require(chest.Inventory.Layout[1] is { Item: "wood", Count: 1 } && world.Held is { Count: 5 }, "putting down one should place one and keep the rest held");
    }

    private static void CheckPuttingDownOnAnotherItemInTheBackpackSwapsThem(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 5); Give(world, "stone", 3);
        int wood = SlotOf(world.Backpack, "wood"), stone = SlotOf(world.Backpack, "stone");
        world.Hold(world.Backpack, wood, 5);
        PutDown(world, world.Backpack, stone, 5);
        Require(world.Backpack.Layout[stone].Item == "wood" && world.Backpack.Layout[wood].Item == "stone" && world.Held == null,
            "a whole held stack put on another item's slot should swap the two and hold nothing");
    }

    private static void CheckAnotherPlayerCannotTakeAHeldStackAndIsToldWhy(GameData data) {
        var (world, guest, heard) = Hosted(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        guest.Pos = world.Player;
        Give(world, "coal", 8);
        world.Transfer(world.Backpack, chest.Inventory, "coal", 8);
        world.Hold(chest.Inventory, 0, 8);
        world.Run(guest, new Command(Verb.Transfer, world.Locate(chest.Inventory), 0, "coal", 8, new Vector2(-1, -1)));
        world.Run(guest, new Command(Verb.Hold, world.Locate(chest.Inventory), 8, "coal", 0, default));
        Require(guest.Backpack["coal"] == 0 && world.As(guest, () => world.Held) == null && chest.Inventory.Layout[0].Count == 8,
            "another player should neither take nor pick up a stack someone holds");
        Require(heard.Contains("8 Coal was held by another player.") && heard.Contains(World.HeldByAnother), $"the other player should be told why, each time: {string.Join(" | ", heard)}");
    }

    private static void CheckTakeAllAndCollectSayHowManyAnotherPlayerHolds(GameData data) {
        var (world, guest, heard) = Hosted(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        guest.Pos = world.Player;
        Give(world, "coal", 8);
        world.Transfer(world.Backpack, chest.Inventory, "coal", 8);
        world.Hold(chest.Inventory, 0, 5);
        // Take all asks for everything of each item.
        world.Run(guest, new Command(Verb.Transfer, world.Locate(chest.Inventory), 0, "coal", 999, new Vector2(-1, -1)));
        Require(guest.Backpack["coal"] == 3 && heard.Contains("5 Coal was held by another player."), $"taking all should take the 3 coal nobody holds and say 5 were held: {string.Join(" | ", heard)}");
        world.Release();
        var furnace = world.Add(ThingKind.Furnace, world.Player + new Vector2(-1, 1));
        furnace.Inventory.Add("stone", 4);
        world.Hold(furnace.Inventory, SlotOf(furnace.Inventory, "stone"), 4);
        heard.Clear();
        world.Run(guest, new Command(Verb.Collect, furnace.Id, 0, "", 0, default));
        Require(guest.Backpack["stone"] == 0 && heard.Contains("4 Stone was held by another player."), $"collecting should leave the 4 held stone and say so: {string.Join(" | ", heard)}");
    }

    private static void CheckAStationStartsNoBatchWhileAStackInItIsHeld(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "raw_meat", 4); Give(world, "coal", 4);
        world.QueueBatch(furnace, "burnt_steak", 2);
        // Two batches' meat lies in the station, so what is not held is still enough for one.
        world.Hold(furnace.Inventory, SlotOf(furnace.Inventory, "raw_meat"), 1);
        Require(furnace.Inventory["raw_meat"] == 4 && world.Held is { Count: 1 }, "the furnace should hold two batches of meat, one unit of it held");
        world.Tick(.1f);
        Require(!furnace.Processing, "a station should start no batch while a player holds a stack in it");
        world.Release();
        world.Tick(.1f);
        Require(furnace.Processing, "the station should start once the stack is let go");
    }

    private static void CheckAStationInputTakesOnlyWhatItsRecipeUses(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "iron_ore", 10); Give(world, "stone", 5);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "iron_ore"), 10);
        world.FromHeldStack(() => world.LoadInput(furnace, "iron_ore", 10));
        Require(furnace.Inventory["iron_ore"] == 10 && world.Held == null, "held iron ore put on a furnace smelting iron should load its input slot");
        world.Hold(world.Backpack, SlotOf(world.Backpack, "stone"), 5);
        world.FromHeldStack(() => world.LoadInput(furnace, "stone", 5));
        Require(furnace.Inventory["stone"] == 0 && world.Held is { Item: "stone", Count: 5 }, "stone the recipe does not use should stay held");
    }

    private static void CheckAHeldStackIsLetGoOutOfReachAndOnLeaving(GameData data) {
        var (world, guest, _) = Hosted(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        guest.Pos = world.Player;
        Give(world, "wood", 12);
        world.Transfer(world.Backpack, chest.Inventory, "wood", 12);
        world.Hold(chest.Inventory, 0, 12);
        world.Player = chest.Pos + new Vector2(World.ItemReach + 5, 0);
        world.Tick(.1f);
        Require(world.Held == null && chest.Inventory["wood"] == 12, "walking out of reach should let go of a stack held from a chest");
        world.Run(guest, new Command(Verb.Hold, world.Locate(chest.Inventory), 12, "wood", 0, default));
        Require(world.As(guest, () => world.Held) is { Count: 12 }, "the guest should hold the chest's wood");
        world.Depart(GuestId);
        Require(chest.Inventory["wood"] == 12, "a player who leaves should let go of what they held");
    }
}
